using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Overtone.Editor
{
    public static class WebGLBuilder
    {
        [MenuItem("Overtone/Build WebGL")]
        public static void Build()
        {
            Configure();
            string output = "Builds/WebGL";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOutput") output = args[i + 1];
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/OvertonePrototype.unity" },
                locationPathName = Path.GetFullPath(output),
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("WebGL build failed: " + report.summary.result);
            Debug.Log("WebGL build ready: " + Path.GetFullPath(output));
        }

        public static void Configure()
        {
            PlayerSettings.WebGL.template = "PROJECT:Overtone";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // Portable to static hosts that cannot set Content-Encoding headers.
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.defaultWebScreenWidth = 1600;
            PlayerSettings.defaultWebScreenHeight = 900;
            AssetDatabase.SaveAssets();
        }
    }
}
