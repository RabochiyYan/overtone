import json
import hashlib
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import play_webgl as launcher


class FreshnessTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.project = Path(self.temp.name)
        for folder in ("Assets", "Packages", "ProjectSettings"):
            (self.project / folder).mkdir()
        self.script = self.project / "Assets" / "Game.cs"
        self.script.write_text("old source")
        self.build = self.project / "Builds" / "WebGL"
        (self.build / "Build").mkdir(parents=True)
        names = ["a.loader.js", "b.data.unityweb", "c.wasm.unityweb", "d.framework.js.unityweb"]
        (self.build / "index.html").write_text("\n".join(f"'Build/{name}'" for name in names))
        for name in names:
            (self.build / "Build" / name).write_bytes(b"built contents")
        self.stamp = self.build / "source-version.json"
        self.stamp.write_text(json.dumps({"sourceHash": launcher.source_hash(self.project)}))
        patcher = mock.patch.multiple(launcher, PROJECT=self.project, BUILD=self.build, STAMP=self.stamp)
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_changed_source_and_missing_binary_require_rebuild(self):
        self.assertTrue(launcher.is_current())
        self.script.write_text("new source")
        self.assertFalse(launcher.is_current())
        self.script.write_text("old source")
        self.assertTrue(launcher.is_current())
        (self.build / "Build" / "c.wasm.unityweb").unlink()
        self.assertFalse(launcher.is_current())

    def test_fingerprint_ignores_builds_and_timestamps(self):
        before = launcher.source_hash(self.project)
        self.script.touch()
        (self.build / "Build" / "a.loader.js").write_bytes(b"rebuilt")
        self.assertEqual(before, launcher.source_hash(self.project))

    def test_fingerprint_matches_unity_ordinal_file_order(self):
        (self.project / "Assets" / "Folder").mkdir()
        (self.project / "Assets" / "Folder.meta").write_bytes(b"meta")
        (self.project / "Assets" / "Folder" / "item").write_bytes(b"asset")
        expected = hashlib.sha256()
        for relative in ("Assets/Folder.meta", "Assets/Folder/item", "Assets/Game.cs"):
            expected.update(relative.encode() + b"\0")
            expected.update((self.project / relative).read_bytes() + b"\0")
        self.assertEqual(expected.hexdigest(), launcher.source_hash(self.project))

    def test_missing_or_invalid_stamp_is_stale(self):
        self.stamp.write_text("invalid json")
        self.assertFalse(launcher.is_current())
        self.stamp.unlink()
        self.assertFalse(launcher.is_current())

    def test_unchanged_sources_do_not_start_unity(self):
        with mock.patch.object(launcher.subprocess, "run") as run:
            launcher.ensure_build()
        run.assert_not_called()


class BuildTests(unittest.TestCase):
    @mock.patch.object(launcher.shutil, "which", return_value="/fake/unity")
    def test_open_editor_uses_running_editor_build(self, _which):
        results = [subprocess.CompletedProcess([], 0), subprocess.CompletedProcess([], 0),
                   subprocess.CompletedProcess([], 0, stdout='{"status":"queued","buildId":"1"}'),
                   subprocess.CompletedProcess([], 0, stdout='{"status":"completed","buildId":"1","result":"Succeeded"}')]
        with mock.patch.object(launcher, "is_current", side_effect=[False, True]), mock.patch.object(launcher.subprocess, "run", side_effect=results) as run:
            launcher.ensure_build()
        self.assertIn("recompile", run.call_args_list[0].args[0])
        self.assertEqual(run.call_args_list[2].args[0][1:4], ["command", "build", "run"])

    @mock.patch.object(launcher.shutil, "which", return_value="/fake/unity")
    def test_closed_editor_uses_custom_batch_builder(self, _which):
        results = [subprocess.CompletedProcess([], 12), subprocess.CompletedProcess([], 0)]
        with mock.patch.object(launcher, "is_current", side_effect=[False, True]), mock.patch.object(launcher.subprocess, "run", side_effect=results) as run:
            launcher.ensure_build()
        self.assertIn("--execute-method", run.call_args_list[-1].args[0])

    @mock.patch.object(launcher.shutil, "which", return_value="/fake/unity")
    def test_failed_build_does_not_fall_back_to_old_version(self, _which):
        results = [subprocess.CompletedProcess([], 12), subprocess.CompletedProcess([], 1)]
        with mock.patch.object(launcher, "is_current", return_value=False), mock.patch.object(launcher.subprocess, "run", side_effect=results):
            with self.assertRaisesRegex(RuntimeError, "Старая версия не будет открыта"):
                launcher.ensure_build()


if __name__ == "__main__":
    unittest.main()
