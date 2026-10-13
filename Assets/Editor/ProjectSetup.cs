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

            AdvancedText = true;
            SetUpBootScene();
            StoreCatalogSetup.CreateIfMissing();
            LocalizationSetup.CreateIfMissing();
            UiSetup.CreateIfMissing();
            AssetDatabase.SaveAssets();
            Debug.Log("Zakzoqa: project settings applied.");
        }

        // UI Toolkit's Advanced Text Generator (Decisions §7): HarfBuzz and ICU, so Arabic letters
        // join and lines wrap right to left without hand-placed line breaks. It is a project setting
        // (Project Settings > UI Toolkit > Enable Advanced Text Generator) with no public API in
        // 6000.4, so it is reached by name; if Unity renames it, this throws rather than silently
        // leaving Arabic broken.
        public static bool AdvancedText
        {
            get => (bool)AdvancedTextProperty().GetValue(null);
            set => AdvancedTextProperty().SetValue(null, value);
        }

        static System.Reflection.PropertyInfo AdvancedTextProperty()
        {
            System.Type type = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("UnityEditor")) continue;
                foreach (var t in asm.GetTypes())
                    if (t.Name == "UIToolkitProjectSettings") { type = t; break; }
                if (type != null) break;
            }
            if (type == null)
                throw new System.InvalidOperationException("UIToolkitProjectSettings not found: Unity moved the Advanced Text Generator setting.");
            return type.GetProperty("enableAdvancedText",
                       System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?? throw new System.InvalidOperationException("enableAdvancedText not found: Unity moved the Advanced Text Generator setting.");
        }

        public static string ScenePath(string name) => $"Assets/Scenes/{name}.unity";

        // The template ships SampleScene. It becomes Boot, the first scene in the build. Every other
        // scene in Scenes.All that does not exist yet starts as a copy of Boot, so it has the same 2D
        // lighting and camera. All of them go in the build, Boot first, cameras at the locked size.
        static void SetUpBootScene()
        {
            if (!File.Exists(BootScenePath) && File.Exists(SampleScenePath))
                AssetDatabase.MoveAsset(SampleScenePath, BootScenePath);

            var build = new EditorBuildSettingsScene[Scenes.All.Length];
            for (int i = 0; i < Scenes.All.Length; i++)
            {
                string path = ScenePath(Scenes.All[i]);
                if (!File.Exists(path)) AssetDatabase.CopyAsset(BootScenePath, path);

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var cam in Object.FindObjectsByType<Camera>())
                {
                    cam.orthographic = true;
                    cam.orthographicSize = WorldScale.OrthographicSize;
                    EditorUtility.SetDirty(cam);
                }
                EditorSceneManager.SaveScene(scene);
                build[i] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = build;
        }
    }
}
