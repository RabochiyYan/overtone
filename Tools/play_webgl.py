#!/usr/bin/env python3
"""Build stale Unity WebGL sources, then serve the current build on loopback."""
import argparse
import functools
import hashlib
import http.server
import json
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
import webbrowser
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
BUILD = PROJECT / "Builds" / "WebGL"
STAMP = BUILD / "source-version.json"
PROJECT_ID = hashlib.sha256(str(PROJECT).encode()).hexdigest()


def source_hash(project=PROJECT):
    digest = hashlib.sha256()
    files = sorted((p for folder in ("Assets", "Packages", "ProjectSettings")
                    for p in (project / folder).rglob("*")
                    if p.is_file() and p.name != ".DS_Store"),
                   key=lambda p: p.relative_to(project).as_posix())
    for path in files:
        digest.update(path.relative_to(project).as_posix().encode() + b"\0")
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(65536), b""):
                digest.update(block)
        digest.update(b"\0")
    return digest.hexdigest()


def read_stamp():
    try:
        return json.loads(STAMP.read_text())
    except (OSError, ValueError):
        return {}


def is_current():
    index = BUILD / "index.html"
    if not index.is_file() or read_stamp().get("sourceHash") != source_hash(PROJECT):
        return False
    files = set(re.findall(r"['\"]Build/([^'\"]+)['\"]", index.read_text()))
    return len(files) >= 4 and all((BUILD / "Build" / name).is_file() and (BUILD / "Build" / name).stat().st_size > 0 for name in files)


def ensure_build():
    if is_current():
        print("WebGL-сборка актуальна.", flush=True)
        return
    print("Исходники изменились. Собираю новую WebGL-версию; это может занять несколько минут…", flush=True)
    unity = shutil.which("unity")
    if not unity:
        candidate = Path.home() / ".unity" / "bin" / "unity"
        if candidate.is_file():
            unity = str(candidate)
    if not unity:
        raise RuntimeError("Не найден Unity CLI. Соберите игру через Unity → Overtone → Build WebGL.")
    compile_result = subprocess.run([
        unity, "recompile", str(PROJECT), "--timeout", "120", "--format", "json",
    ], cwd=PROJECT, check=False, capture_output=True, text=True)
    command = [unity, "build", str(PROJECT), "--target", "WebGL",
               "--output", str(BUILD), "--timeout", "1200"]
    if compile_result.returncode == 0:
        configure = subprocess.run([
            unity, "command", "script", "eval", "--code",
            'if(UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Выйдите из Play Mode перед сборкой WebGL."); Overtone.Editor.WebGLBuilder.Configure(); return true;',
        ], cwd=PROJECT, check=False)
        if configure.returncode:
            raise RuntimeError("Не удалось подготовить открытый Unity Editor. Проверьте Console и выйдите из Play Mode.")
        build_in_editor(unity)
        if not is_current():
            raise RuntimeError("Сборка не соответствует исходникам. Проверьте Unity Console и повторите запуск.")
        return
    elif compile_result.returncode == 12:
        command.extend(["--execute-method", "Overtone.Editor.WebGLBuilder.Build"])
    else:
        print(compile_result.stdout, file=sys.stderr)
        print(compile_result.stderr, file=sys.stderr)
        raise RuntimeError("Unity не смог скомпилировать проект. Старая версия не будет открыта.")
    result = subprocess.run(command, cwd=PROJECT, check=False)
    if result.returncode:
        raise RuntimeError("Сборка не завершена. Старая версия не будет открыта; проверьте сообщения Unity выше.")
    if not is_current():
        raise RuntimeError("Сборка не соответствует исходникам. Проверьте Unity Console и повторите запуск.")


def pipeline_command(unity, *command):
    result = subprocess.run([unity, "command", *command, "--format", "json"],
                            cwd=PROJECT, check=False, capture_output=True, text=True, timeout=90)
    if result.returncode:
        raise RuntimeError(f"Unity: {result.stderr or result.stdout}")
    try:
        payload = json.loads(result.stdout)
    except ValueError as error:
        raise RuntimeError("Unity вернул непонятный результат. Проверьте Console.") from error
    if payload.get("success") is False:
        raise RuntimeError(f"Unity: {payload.get('errors')}")
    return payload


def build_in_editor(unity):
    queued = pipeline_command(unity, "build", "run", "--target", "WebGL", "--output-path", str(BUILD), "--confirm", "true")
    build_id = queued.get("buildId")
    if not build_id:
        raise RuntimeError("Unity не подтвердил запуск сборки.")
    started = time.monotonic()
    next_notice = 30
    while time.monotonic() - started < 1200:
        status = pipeline_command(unity, "build", "status")
        if status.get("buildId") != build_id:
            raise RuntimeError("В Unity запущена другая сборка. Дождитесь её окончания и повторите запуск.")
        if status.get("status") == "completed":
            if status.get("result") != "Succeeded":
                raise RuntimeError(f"Сборка WebGL не завершена: {status.get('result')}. Старая версия не будет открыта.")
            print(f"WebGL готов: {status.get('totalSizeBytes', 0) / 1024 / 1024:.1f} МБ.", flush=True)
            return
        elapsed = time.monotonic() - started
        if elapsed >= next_notice:
            print(f"Unity собирает WebGL… {int(elapsed)} с.", flush=True)
            next_notice += 30
        time.sleep(1)
    raise RuntimeError("Превышено время ожидания. Сборка может продолжаться в Unity; проверьте её состояние перед повторным запуском.")


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-store, no-cache, must-revalidate")
        self.send_header("Pragma", "no-cache")
        super().end_headers()

    def do_GET(self):
        if self.path.split("?", 1)[0] == "/__overtone__/status":
            data = json.dumps({"project": PROJECT_ID, "sourceHash": read_stamp().get("sourceHash")}).encode()
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        else:
            super().do_GET()


def existing_server_matches(port):
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    try:
        with opener.open(f"http://localhost:{port}/__overtone__/status", timeout=1) as response:
            data = json.load(response)
        return data.get("project") == PROJECT_ID and data.get("sourceHash") == read_stamp().get("sourceHash")
    except (OSError, ValueError):
        pass
    # A server from the original launcher may still be serving this exact folder.
    # Reuse it only if it serves the new index and every content-addressed build file.
    try:
        with opener.open(f"http://localhost:{port}/index.html?fresh=1", timeout=1) as response:
            if response.read() != (BUILD / "index.html").read_bytes():
                return False
        for path in (BUILD / "Build").iterdir():
            if path.is_file():
                request = urllib.request.Request(f"http://localhost:{port}/Build/{path.name}", method="HEAD")
                with opener.open(request, timeout=1) as response:
                    if int(response.headers.get("Content-Length", -1)) != path.stat().st_size:
                        return False
        return True
    except (OSError, ValueError):
        return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Check freshness without building or launching")
    parser.add_argument("--no-browser", action="store_true")
    parser.add_argument("--port", type=int, default=8080)
    args = parser.parse_args()
    if args.check:
        current = is_current()
        print("WebGL-сборка актуальна." if current else "WebGL-сборка устарела или отсутствует.")
        return 0 if current else 1
    ensure_build()
    server = None
    for port in range(args.port, args.port + 20):
        try:
            server = http.server.ThreadingHTTPServer(("127.0.0.1", port), functools.partial(Handler, directory=str(BUILD)))
            break
        except OSError:
            if existing_server_matches(port):
                break
    else:
        raise RuntimeError("Нет свободного локального порта для игры.")
    url = f"http://localhost:{port}/?v={read_stamp()['sourceHash'][:16]}"
    print(f"Игра: {url}", flush=True)
    if port != args.port:
        print(f"Порт {args.port} занят другим сервером. На другом порте браузер хранит отдельное сохранение.", flush=True)
    # The listening socket is ready before the browser is opened.
    if not args.no_browser:
        webbrowser.open(url)
    if server is None:
        print("Используется уже работающий сервер этой сборки.", flush=True)
        return 0
    print("Чтобы остановить сервер, нажмите Ctrl+C или закройте это окно.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, OSError, subprocess.TimeoutExpired) as error:
        print(f"Ошибка: {error}", file=sys.stderr)
        sys.exit(1)
