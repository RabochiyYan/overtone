using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.Callbacks;
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

        // Also covers builds made through the CLI's running-Editor build pipeline.
        [PostProcessBuild(1000)]
        public static void RecordSourceVersion(BuildTarget target,string output)
        {
            if(target!=BuildTarget.WebGL)return;
            string project=Path.GetFullPath(".");
            var files=new[]{"Assets","Packages","ProjectSettings"}
                .SelectMany(folder=>Directory.GetFiles(Path.Combine(project,folder),"*",SearchOption.AllDirectories))
                .Where(path=>Path.GetFileName(path)!=".DS_Store")
                .OrderBy(path=>path,StringComparer.Ordinal);
            using(var hash=SHA256.Create()) {
                var buffer=new byte[65536];var separator=new byte[]{0};
                foreach(string file in files) {
                    string relative=file.Substring(project.Length+1).Replace('\\','/');
                    byte[] name=Encoding.UTF8.GetBytes(relative+"\0");
                    hash.TransformBlock(name,0,name.Length,name,0);
                    using(var stream=File.OpenRead(file)) {
                        int count;
                        while((count=stream.Read(buffer,0,buffer.Length))>0) hash.TransformBlock(buffer,0,count,buffer,0);
                    }
                    hash.TransformBlock(separator,0,1,separator,0);
                }
                hash.TransformFinalBlock(new byte[0],0,0);
                string fingerprint=BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
                File.WriteAllText(Path.Combine(output,"source-version.json"),"{\"sourceHash\":\""+fingerprint+"\",\"builtUtc\":\""+DateTime.UtcNow.ToString("o")+"\"}");
                PrepareGitHubPages(output);
            }
        }

        // GitHub Pages can publish /docs directly; /Builds stays a local build cache.
        public static void PrepareGitHubPages(string output)
        {
            string source=Path.GetFullPath(output);
            string destination=Path.GetFullPath("docs");
            if(!Directory.Exists(source) || !File.Exists(Path.Combine(source,"index.html")))
                throw new DirectoryNotFoundException("WebGL build must contain index.html: "+source);
            if(!string.Equals(source,destination,StringComparison.OrdinalIgnoreCase)) {
                foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)) {
                    string relative=file.Substring(source.Length+1);
                    string target=Path.Combine(destination,relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file,target,true);
                }
            }
            File.WriteAllText(Path.Combine(destination,".nojekyll"),"");
            Debug.Log("GitHub Pages files ready: "+destination);
        }

        public static void Configure()
        {
            PlayerSettings.WebGL.template = "PROJECT:Overtone";
            PlayerSettings.WebGL.nameFilesAsHashes = true;
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
