using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// WebGL build for YouTube Playables, then a check of the bundle against the Playables file rules.
    /// Settings that also affect mobile builds (splash, run in background) are restored afterwards.
    /// Command line:
    ///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Onet.EditorTools.PlayablesBuild.BuildBatch [-out Builds/Playables]
    /// </summary>
    public static class PlayablesBuild
    {
        const string DefaultOutput = "Builds/Playables";
        const string Template = "PROJECT:YouTubePlayables";

        // Playables limits (certification: stability and performance).
        const long MaxFileBytes = 30L << 20, SuggestedFileBytes = 512L << 10, MaxTotalBytes = 250L << 20;
        const int MaxFiles = 8000;
        static readonly Regex FileNameRule = new Regex(@"^[A-Za-z0-9_.\-]+$");

        [MenuItem("Onet/Build/YouTube Playables (WebGL)")]
        static void BuildMenu()
        {
            if (Build(DefaultOutput)) EditorUtility.RevealInFinder(Path.Combine(DefaultOutput, "index.html"));
        }

        public static void BuildBatch()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-out");
            string output = i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultOutput;
            EditorApplication.Exit(Build(output) ? 0 : 1);
        }

        public static bool Build(string output)
        {
            bool splash = PlayerSettings.SplashScreen.show, background = PlayerSettings.runInBackground;
            try
            {
                PlayerSettings.WebGL.template = Template;
                // Playables compresses the upload itself and does not support Unity's gzip / Brotli builds.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.dataCaching = false;          // IndexedDB cache; Playables allows only cloud save
                PlayerSettings.WebGL.nameFilesAsHashes = false;
                PlayerSettings.WebGL.threadsSupport = false;
                PlayerSettings.WebGL.showDiagnostics = false;
                PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
                // The .wasm must stay under the 30 MiB per-file limit: optimise IL2CPP and the linker for size.
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
                PlayerSettings.stripEngineCode = true;
                SetWasmCodeOptimization("DiskSizeLTO");
                PlayerSettings.SplashScreen.show = false;
                // Pausing is driven only by the SDK's onPause / onResume, not by the page losing focus.
                PlayerSettings.runInBackground = true;

                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (Directory.Exists(output)) Directory.Delete(output, true);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"[Playables] build {report.summary.result}: {report.summary.totalErrors} errors");
                    return false;
                }
                return CheckBundle(output);
            }
            finally
            {
                PlayerSettings.SplashScreen.show = splash;
                PlayerSettings.runInBackground = background;
            }
        }

        // UnityEditor.WebGL.UserBuildSettings lives in the WebGL module's editor assembly, which asmdefs cannot reference.
        static void SetWasmCodeOptimization(string value)
        {
            var settings = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
            var prop = settings?.GetProperty("codeOptimization", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop == null) { Debug.LogWarning("[Playables] WebGL code optimisation setting not found; left as is"); return; }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, value));
            Debug.Log("[Playables] WebGL code optimisation: " + value);
        }

        /// <summary>Logs every rule the bundle breaks; returns false if any hard limit fails.</summary>
        public static bool CheckBundle(string root)
        {
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                                 .Where(f => !Path.GetFileName(f).StartsWith(".")).Select(f => new FileInfo(f)).ToList();
            bool ok = true;
            void Fail(string msg) { ok = false; Debug.LogError("[Playables] " + msg); }

            if (!File.Exists(Path.Combine(root, "index.html"))) Fail("index.html missing at bundle root");
            if (files.Count > MaxFiles) Fail($"{files.Count} files, limit is {MaxFiles}");
            long total = 0;
            foreach (var f in files.OrderByDescending(f => f.Length))
            {
                total += f.Length;
                string rel = Path.GetRelativePath(root, f.FullName);
                if (!FileNameRule.IsMatch(f.Name)) Fail($"file name not allowed: {rel}");
                if (f.Length > MaxFileBytes) Fail($"{rel} is {Mb(f.Length)}, limit is {Mb(MaxFileBytes)}");
                else if (f.Length > SuggestedFileBytes) Debug.Log($"[Playables] {rel}: {Mb(f.Length)} (over the suggested 512 KiB)");
            }
            if (total > MaxTotalBytes) Fail($"bundle is {Mb(total)}, limit is {Mb(MaxTotalBytes)}");
            Debug.Log($"[Playables] bundle {(ok ? "OK" : "FAILED")}: {files.Count} files, {Mb(total)} uncompressed, at {Path.GetFullPath(root)}");
            return ok;
        }

        static string Mb(long bytes) => $"{bytes / 1048576.0:0.0} MiB";
    }
}
