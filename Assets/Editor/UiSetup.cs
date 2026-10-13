using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Zakzoqa.EditorTools
{
    // The UI Toolkit pieces every screen shares (Decisions §7): the font asset the Advanced Text
    // Generator needs, the panel settings that put the theme on every screen, and the main menu in
    // its scene. Each is created only if missing; after that the asset is edited in the editor.
    public static class UiSetup
    {
        public const string FontPath = "Assets/Fonts/NotoNaskhArabic-Regular.ttf";
        public const string FontAssetPath = "Assets/Fonts/NotoNaskhArabic SDF.asset";
        public const string ThemePath = "Assets/UI/Theme/ZakzoqaTheme.tss";
        public const string PanelSettingsPath = "Assets/UI/ZakzoqaPanel.asset";
        public const string MainMenuUxmlPath = "Assets/UI/Screens/MainMenu.uxml";

        // Landscape (Decisions §1, row 4). Matching height keeps text the same size on a tall 16:9
        // phone and a wide 20:9 one: only the side margins change.
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        [MenuItem("Zakzoqa/Set Up UI")]
        public static void CreateIfMissing()
        {
            CreateFontAsset();
            CreatePanelSettings();
            PlaceMainMenu();
            AssetDatabase.SaveAssets();
        }

        // ATG takes dynamic font assets only: glyphs are rendered into the atlas as text needs them,
        // which is also what lets one font cover every Arabic letter form without a pre-baked list.
        static void CreateFontAsset()
        {
            if (File.Exists(FontAssetPath)) return;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            FontAsset asset = FontAsset.CreateFontAsset(font);
            asset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            // The material and atlas live inside the asset, or they vanish on the next reload.
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            // The theme's USS names this asset; now it exists, read the USS again.
            AssetDatabase.ImportAsset("Assets/UI/Theme/Zakzoqa.uss", ImportAssetOptions.ForceUpdate);
        }

        static void CreatePanelSettings()
        {
            if (File.Exists(PanelSettingsPath)) return;

            PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = ReferenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
        }

        static void PlaceMainMenu()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.ScenePath(Scenes.MainMenu), OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<MainMenuScreen>() != null) return;

            GameObject go = new GameObject("MainMenu");
            UIDocument doc = go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuUxmlPath);
            go.AddComponent<MainMenuScreen>();
            EditorSceneManager.SaveScene(scene);
        }
    }
}
