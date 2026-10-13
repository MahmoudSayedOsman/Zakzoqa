using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;
using Zakzoqa.EditorTools;

namespace Zakzoqa.Tests
{
    // Arabic and English (Decisions §7). Shadow Escape's LocalizationManager was its most-changed file
    // (94 edits): text began inside the code "for now", Arabic was broken on a UI that could not shape
    // it, and every fix after that was a patch. Each rule that went wrong there is a test here.
    public class LocalizationTests
    {
        static StringTableCollection Collection() => LocalizationEditorSettings.GetStringTableCollection(Language.Table);

        static StringTable Table(string code) =>
            (StringTable)Collection().GetTable(new LocaleIdentifier(code));

        // =====================================================================================
        // The tables
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void EveryKey_HasTextInBothLanguages()
        {
            Assert.IsNotNull(Collection(), "the UI string table is missing: run Zakzoqa > Apply Project Settings.");
            var missing = new List<string>();
            foreach (var entry in Collection().SharedData.Entries)
                foreach (string code in new[] { Language.English, Language.Arabic })
                {
                    StringTableEntry e = Table(code).GetEntry(entry.Id);
                    if (e == null || string.IsNullOrWhiteSpace(e.Value)) missing.Add($"{entry.Key} ({code})");
                }
            Assert.IsEmpty(missing, "these strings would show as blank:\n  " + string.Join("\n  ", missing));
        }

        // ATG wraps Arabic on its own. A hand-placed break was Shadow Escape's workaround (220 of them),
        // and it breaks the line in the wrong place the moment a font or a size changes.
        [Category("P0")]
        [Test]
        public void NoString_BreaksItsOwnLines()
        {
            var broken = Collection().SharedData.Entries
                .SelectMany(e => new[] { Language.English, Language.Arabic }.Select(c => (e.Key, c, Table(c).GetEntry(e.Id)?.Value ?? "")))
                .Where(x => x.Item3.Contains("\n"))
                .Select(x => $"{x.Key} ({x.c})").ToList();
            Assert.IsEmpty(broken, "line breaks in strings; let the layout wrap them:\n  " + string.Join("\n  ", broken));
        }

        // A font change that drops a letter shows empty boxes in one language and nothing at all in the
        // other, so nobody notices until a player does.
        [Category("P0")]
        [Test]
        public void TheFont_HasEveryCharacterTheTablesUse()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(UiSetup.FontPath);
            Assert.IsNotNull(font);

            var missing = new SortedSet<string>();
            foreach (string code in new[] { Language.English, Language.Arabic })
                foreach (var entry in Table(code).Values)
                    foreach (char c in entry.Value ?? "")
                        if (!char.IsControl(c) && !char.IsWhiteSpace(c) && !char.IsSurrogate(c) && !IsFormatChar(c) && !font.HasCharacter(c))
                            missing.Add($"U+{(int)c:X4} '{c}' in {entry.Key} ({code})");

            Assert.IsEmpty(missing, "the font cannot draw:\n  " + string.Join("\n  ", missing));
        }

        // Smart-string placeholders and direction marks are not drawn.
        static bool IsFormatChar(char c) => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format;

        // =====================================================================================
        // No words outside the tables
        // =====================================================================================

        // Every word on screen comes from the table. A text="..." in a screen is English that stays
        // English in Arabic.
        [Category("P0")]
        [Test]
        public void NoScreen_HasTextOfItsOwn()
        {
            Regex literal = new Regex(@"\b(text|label|tooltip)\s*=\s*""([^""]*[\p{L}][^""]*)""");
            var offenders = Directory.GetFiles("Assets/UI", "*.uxml", SearchOption.AllDirectories)
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, line, i)))
                .Where(x => literal.IsMatch(x.line))
                .Select(x => $"{x.f}:{x.i + 1}  {x.line.Trim()}").ToList();
            Assert.IsEmpty(offenders, "bind these to the UI table instead:\n  " + string.Join("\n  ", offenders));
        }

        [Category("P0")]
        [Test]
        public void NoScript_PutsAWordOnScreen()
        {
            Regex literal = new Regex(@"(\.text\s*=\s*\$?""[^""]*\p{L}|new\s+(Label|Button|Toggle)\s*\(\s*\$?""[^""]*\p{L})");
            var offenders = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, line, i)))
                .Where(x => !x.line.TrimStart().StartsWith("//") && literal.IsMatch(x.line))
                .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}  {x.line.Trim()}").ToList();
            Assert.IsEmpty(offenders, "use Language.Get(key) or a binding:\n  " + string.Join("\n  ", offenders));
        }

        // =====================================================================================
        // Arabic on screen
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void TheAdvancedTextGenerator_IsOn()
        {
            Assert.IsTrue(ProjectSetup.AdvancedText,
                "UI Toolkit's Advanced Text Generator is off: Arabic letters will not join.");
        }

        [Category("P0")]
        [Test]
        public void TheTheme_UsesTheAdvancedGenerator_AndTheArabicFont()
        {
            string uss = File.ReadAllText("Assets/UI/Theme/Zakzoqa.uss");
            StringAssert.Contains("-unity-text-generator: advanced", uss);
            StringAssert.Contains(Path.GetFileName(UiSetup.FontAssetPath).Replace(" ", "%20"), uss);

            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiSetup.PanelSettingsPath);
            Assert.IsNotNull(panel);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiSetup.ThemePath), panel.themeStyleSheet,
                "the screens' panel does not use Zakzoqa's theme, so none of these rules reach them.");
        }

        [Category("P0")]
        [Test]
        public void Arabic_TurnsAScreenRightToLeft_AndEnglishTurnsItBack()
        {
            VisualElement root = new VisualElement();

            Language.ApplyTo(root, Language.Arabic);
            Assert.AreEqual(LanguageDirection.RTL, root.languageDirection);
            Assert.IsTrue(root.ClassListContains(Language.RtlClass));

            Language.ApplyTo(root, Language.English);
            Assert.AreEqual(LanguageDirection.LTR, root.languageDirection);
            Assert.IsFalse(root.ClassListContains(Language.RtlClass));
        }

        // =====================================================================================
        // The player's choice
        // =====================================================================================

        // A setting: it stays on this phone, and goes through the one door.
        [Category("P0")]
        [Test]
        public void TheLanguageChoice_IsASettingOfThisPhone()
        {
            Assert.IsFalse(SaveScope.IsProgress(Language.SavedKey), "the language would follow the player to another phone.");
            Assert.IsFalse(SaveGuard.Covers(Language.SavedKey));
        }

        [Category("P0")]
        [Test]
        public void AtLaunch_TheSavedChoiceComesFirst_ThenThePhonesLanguage()
        {
            LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSetup.SettingsPath);
            List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();

            Assert.IsInstanceOf<SavedLocaleSelector>(selectors[0], "the player's choice is not asked first.");
            Assert.IsInstanceOf<SystemLocaleSelector>(selectors[1]);
            Assert.IsFalse(selectors.Any(s => s is PlayerPrefLocaleSelector),
                "Localization's own selector writes PlayerPrefs directly, around the one door.");
        }

        [Test]
        public void BothLanguages_Exist()
        {
            var codes = LocalizationEditorSettings.GetLocales().Select(l => l.Identifier.Code).ToList();
            CollectionAssert.Contains(codes, Language.Arabic);
            CollectionAssert.Contains(codes, Language.English);
        }

        // Preloaded, or a screen's buttons are blank for the frames the table takes to arrive.
        [Test]
        public void TheUITable_IsLoadedBeforeTheFirstScreen()
        {
            foreach (string code in new[] { Language.English, Language.Arabic })
                Assert.IsTrue(LocalizationEditorSettings.GetPreloadTableFlag(Table(code)), $"the {code} table loads on demand.");
        }
    }
}
