using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zakzoqa.EditorTools
{
    // Applies the locked decisions in Docs/Decisions.md §1 to the project. Idempotent: running it
    // twice changes nothing the second time. Run from the menu, or headless with
    //   Unity.exe -batchmode -quit -projectPath . -executeMethod Zakzoqa.EditorTools.ProjectSetup.Apply
    public static class ProjectSetup
    {
        public const string CompanyName = "MMSO Soft";
        public const string ProductName = "Zakzoqa";
        public const string AndroidId = "com.mmsosoft.zakzoqa";
        public const string BootScenePath = "Assets/Scenes/Boot.unity";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Zakzoqa/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, AndroidId);

            // Landscape for every transformation (Decisions §1, row 4). Both landscape sides are
            // allowed so the phone can be held either way; portrait never.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Same Android baseline as Shadow Escape, which passed Play review with it.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;

            SetUpBootScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Zakzoqa: project settings applied.");
        }

        // The template ships SampleScene. It becomes Boot, the first scene in the build, with the
        // camera at the locked orthographic size.
        static void SetUpBootScene()
        {
            if (!File.Exists(BootScenePath) && File.Exists(SampleScenePath))
                AssetDatabase.MoveAsset(SampleScenePath, BootScenePath);

            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                cam.orthographic = true;
                cam.orthographicSize = WorldScale.OrthographicSize;
                EditorUtility.SetDirty(cam);
            }
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
        }
    }
}
