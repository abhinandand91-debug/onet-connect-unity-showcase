using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Google Play build: applies the Android player settings (package name, API 36, IL2CPP ARM64, portrait, app icon)
    /// and builds a signed .aab. The keystore and its passwords come from environment variables only, so they never end
    /// up in ProjectSettings or git:
    ///   ONET_KEYSTORE        path to the .keystore / .jks file
    ///   ONET_KEYSTORE_PASS   keystore password
    ///   ONET_KEY_ALIAS       key alias
    ///   ONET_KEY_PASS        key password (defaults to the keystore password)
    /// Command line (Editor closed):
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Onet.EditorTools.AndroidBuild.BuildBundleBatch [-versionName 1.0.0] [-versionCode 1]
    /// A debug-signed .apk for testing on a phone needs no keystore: Onet ▸ Build ▸ Android Debug APK (or BuildDebugApkBatch).
    /// </summary>
    public static class AndroidBuild
    {
        public const string PackageName = "com.abhinandan.onetconnect";
        public const string ProductName = "Onet Connect";
        const int TargetSdk = 36;                   // Google Play requirement for new apps and updates from 31 Aug 2026
        const string OutputDir = "Builds/Android";
        const string IconDir = "Assets/OnetConnect/Art/AppIcon";

        [MenuItem("Onet/Build/Android App Bundle (Google Play)")]
        static void BuildBundleMenu() => BuildBundle(null, -1);

        [MenuItem("Onet/Build/Android Debug APK (test on a phone)")]
        static void BuildDebugApkMenu() => BuildDebugApk();

        public static void BuildBundleBatch()
        {
            EditorApplication.Exit(BuildBundle(Arg("-versionName"), int.TryParse(Arg("-versionCode"), out int code) ? code : -1) ? 0 : 1);
        }

        public static void BuildDebugApkBatch() => EditorApplication.Exit(BuildDebugApk() ? 0 : 1);

        /// <summary>Signed release .aab for upload to Google Play.</summary>
        public static bool BuildBundle(string versionName, int versionCode)
        {
            string keystore = Environment.GetEnvironmentVariable("ONET_KEYSTORE");
            string storePass = Environment.GetEnvironmentVariable("ONET_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("ONET_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("ONET_KEY_PASS") ?? storePass;
            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore) || string.IsNullOrEmpty(storePass) || string.IsNullOrEmpty(alias))
            {
                Debug.LogError("[Android] set ONET_KEYSTORE (existing file), ONET_KEYSTORE_PASS and ONET_KEY_ALIAS to sign the release bundle");
                return false;
            }

            ApplySettings();
            if (!string.IsNullOrEmpty(versionName)) PlayerSettings.bundleVersion = versionName;
            if (versionCode > 0) PlayerSettings.Android.bundleVersionCode = versionCode;

            bool hadCustom = PlayerSettings.Android.useCustomKeystore;
            string hadName = PlayerSettings.Android.keystoreName, hadAlias = PlayerSettings.Android.keyaliasName;
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = keyPass;
                EditorUserBuildSettings.buildAppBundle = true;
                string file = $"{OutputDir}/OnetConnect-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab";
                return Build(file, BuildOptions.None);
            }
            finally
            {
                // keep the keystore out of ProjectSettings: the passwords are never saved, and the path is put back
                PlayerSettings.Android.keystorePass = PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = hadName;
                PlayerSettings.Android.keyaliasName = hadAlias;
                PlayerSettings.Android.useCustomKeystore = hadCustom;
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Debug-signed .apk to install with adb or by copying to a phone. Google Play does not accept it.</summary>
        public static bool BuildDebugApk()
        {
            ApplySettings();
            bool hadCustom = PlayerSettings.Android.useCustomKeystore;
            try
            {
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                return Build($"{OutputDir}/OnetConnect-debug.apk", BuildOptions.None);
            }
            finally { PlayerSettings.Android.useCustomKeystore = hadCustom; }
        }

        /// <summary>Android player settings. Saved to ProjectSettings so the Editor's Android target matches the builds.</summary>
        public static void ApplySettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            // portrait only on phones; the web build ignores this and still adapts to any window shape
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SplashScreen.show = false;
            ApplyIcons();
        }

        /// <summary>Adaptive (background + foreground), round and legacy launcher icons from Art/AppIcon (ArtSource/app_icon.py).</summary>
        static void ApplyIcons()
        {
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconDir}/icon_adaptive_background.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconDir}/icon_adaptive_foreground.png");
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconDir}/icon_legacy.png");
            if (bg == null || fg == null || legacy == null) { Debug.LogError("[Android] app icon textures missing; run ArtSource/app_icon.py"); return; }

            var target = NamedBuildTarget.Android;
            // Kinds come from the Android module (UnityEditor.Android); look them up by name so this script compiles without it
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
            {
                var icons = PlayerSettings.GetPlatformIcons(target, kind);
                foreach (var icon in icons)
                {
                    if (kind.ToString() == "Adaptive") icon.SetTextures(bg, fg);
                    else icon.SetTexture(legacy);
                }
                PlayerSettings.SetPlatformIcons(target, kind, icons);
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
        }

        static bool Build(string file, BuildOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = file, target = BuildTarget.Android, options = options,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Android] build {report.summary.result}: {report.summary.totalErrors} errors");
                return false;
            }
            Debug.Log($"[Android] built {file} ({new FileInfo(file).Length / 1048576.0:0.0} MiB), {PackageName} " +
                      $"v{PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), target API {TargetSdk}");
            return true;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
