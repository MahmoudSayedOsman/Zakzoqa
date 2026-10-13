using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Zakzoqa.Tests
{
    // Pictures of every screen, in every language, for a person to look at. Shadow Escape's rule was
    // "look at it in Arabic" before a string lands; this makes looking one command:
    //   Tools/ui-snapshots.ps1
    // which runs these with graphics and writes PNGs to TestResults/Snapshots.
    //
    // Skipped in the normal test run (no graphics device there, and nothing to assert about pixels).
    public class UiSnapshots
    {
        public const string OutVariable = "ZAKZOQA_SNAPSHOTS";

        static readonly (string name, string uxml)[] ScreenList =
        {
            ("MainMenu", "Assets/UI/Screens/MainMenu.uxml"),
        };

        [UnityTest]
        public IEnumerator EveryScreen_InEveryLanguage()
        {
            string outDir = System.Environment.GetEnvironmentVariable(OutVariable);
            if (string.IsNullOrEmpty(outDir)) Assert.Ignore("Snapshots run only from Tools/ui-snapshots.ps1.");
            Directory.CreateDirectory(outDir);

            yield return LocalizationSettings.InitializationOperation;

            PanelSettings shared = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ZakzoqaPanel.asset");
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            PanelSettings panel = Object.Instantiate(shared);
            panel.targetTexture = rt;
            panel.clearColor = true;

            string savedChoice = Language.Saved;
            try
            {
                foreach (var screen in ScreenList)
                {
                    foreach (string code in new[] { Language.English, Language.Arabic })
                    {
                        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
                        yield return LocalizationSettings.InitializationOperation;
                        yield return LocalizationSettings.StringDatabase.GetTableAsync(Language.Table);

                        GameObject go = new GameObject("snapshot");
                        UIDocument doc = go.AddComponent<UIDocument>();
                        doc.panelSettings = panel;
                        doc.visualTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(screen.uxml);
                        yield return null;
                        Language.ApplyTo(doc.rootVisualElement, code);

                        // Bindings resolve over a few frames (the table loads asynchronously).
                        for (int i = 0; i < 15; i++) yield return null;

                        // A blank label in a picture is easy to miss; a blank label in the log is not.
                        foreach (TextElement t in doc.rootVisualElement.Query<TextElement>().ToList())
                            if (string.IsNullOrEmpty(t.text)) Debug.LogWarning($"[Snapshots] {screen.name}/{code}: '{t.name}' has no text.");
                        Save(rt, Path.Combine(outDir, $"{screen.name}_{code}.png"));
                        Object.Destroy(go);
                        yield return null;
                    }
                }
            }
            finally
            {
                if (savedChoice != null) Language.Set(savedChoice);
                rt.Release();
            }
        }

        static void Save(RenderTexture rt, string path)
        {
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
