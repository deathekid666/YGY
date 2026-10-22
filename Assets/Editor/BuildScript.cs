using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YugiohAR.EditorTools
{
    /// <summary>
    /// Headless build entry point for CI (GitHub Actions): creates the scene
    /// via SceneAutoSetup, applies Android settings, and builds the APK.
    /// Invoked with -executeMethod YugiohAR.EditorTools.BuildScript.BuildAndroid
    /// Optional env var ANTHROPIC_API_KEY is baked into CardVisionScanner —
    /// anyone with the APK can extract it, so only use a throwaway key.
    /// </summary>
    public static class BuildScript
    {
        public static void BuildAndroid()
        {
            const string scenePath = "Assets/Scenes/Main.unity";
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SceneAutoSetup.BuildScene();

            var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrEmpty(key))
            {
                var scanner = UnityEngine.Object.FindFirstObjectByType<CardVisionScanner>();
                if (scanner != null) scanner.apiKey = key;
            }

            EditorSceneManager.SaveScene(scene, scenePath);

            PlayerSettings.companyName = "YGY";
            PlayerSettings.productName = "YugiohAR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ygy.yugiohar");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = "Builds/Android/YugiohAR.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Build failed: {report.summary.result}, {report.summary.totalErrors} error(s)");
        }
    }
}
