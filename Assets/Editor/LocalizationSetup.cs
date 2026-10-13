using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Zakzoqa.EditorTools
{
    // Sets up Unity Localization for Zakzoqa (Decisions §7): Arabic and English, one string table
    // collection named "UI", and the startup order — the player's saved choice, then the phone's
    // language, then English.
    //
    // CREATE ONCE, THEN HANDS OFF. The tables are created with the first strings below, and from then
    // on they are the truth, edited in Window > Asset Management > Localization Tables. Running this
    // again adds nothing and overwrites nothing (Shadow Escape's builders overwrote hand edits).
    public static class LocalizationSetup
    {
        public const string Root = "Assets/Localization";
        public const string SettingsPath = Root + "/LocalizationSettings.asset";
        public const string LocalesDir = Root + "/Locales";
        public const string TablesDir = Root + "/Tables";

        // The first strings: key, English, Arabic. Only used when the table is created.
        static readonly string[,] FirstStrings =
        {
            { "game.title", "Zakzoqa", "زقزوقة" },
            { "menu.play", "Play", "العب" },
            { "menu.store", "Store", "المتجر" },
            { "menu.settings", "Settings", "الإعدادات" },
            // The OTHER language's name, in that language: the button that switches to it.
            { "menu.switch_language", "العربية", "English" },
            { "common.back", "Back", "رجوع" },

            { "store.coins_small", "Small bag of coins", "كيس عملات صغير" },
            { "store.coins_medium", "Chest of coins", "صندوق عملات" },
            { "store.coins_large", "Vault of coins", "خزنة عملات" },
            { "store.coin_doubler", "Coin doubler: ×2 coins from every level, forever", "مضاعِف العملات: عملات كل مرحلة ×2 للأبد" },
            { "store.remove_ads", "Remove ads", "إزالة الإعلانات" },
            { "store.badge.best_value", "Best value", "أفضل قيمة" },
            { "store.restore", "Restore purchases", "استرجاع المشتريات" },
            { "store.test_store", "Test store: nothing is charged.", "متجر تجريبي: لن يُخصم أي مبلغ." },

            { "gate.title", "Ask a grown-up", "اطلب من شخص كبير" },
            { "gate.question", "What is {0} × {1}?", "كم ناتج {0} × {1}؟" },
            { "gate.wrong", "Not quite. Here is a new one.", "ليست صحيحة. إليك سؤالًا جديدًا." },

            { "reset.warning", "Resetting deletes your stars and coins, including coins you bought. Things you bought to keep, like Remove Ads, stay.",
                               "إعادة البدء تمسح النجوم والعملات، ومنها العملات التي اشتريتها. أما ما اشتريته ليبقى، مثل إزالة الإعلانات، فيبقى." },
        };

        [MenuItem("Zakzoqa/Set Up Localization")]
        public static void CreateIfMissing()
        {
            Directory.CreateDirectory(LocalesDir);
            Directory.CreateDirectory(TablesDir);

            LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            // Saved choice, then the phone's language, then English. The package's own PlayerPrefs
            // selector is left out: the choice goes through Prefs like every other setting.
            List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear();
            selectors.Add(new SavedLocaleSelector());
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier(Language.English) });
            EditorUtility.SetDirty(settings);

            Locale english = EnsureLocale(Language.English);
            Locale arabic = EnsureLocale(Language.Arabic);

            if (LocalizationEditorSettings.GetStringTableCollection(Language.Table) == null)
            {
                StringTableCollection ui = LocalizationEditorSettings.CreateStringTableCollection(Language.Table, TablesDir,
                    new List<Locale> { english, arabic });
                var en = (UnityEngine.Localization.Tables.StringTable)ui.GetTable(english.Identifier);
                var ar = (UnityEngine.Localization.Tables.StringTable)ui.GetTable(arabic.Identifier);
                for (int i = 0; i < FirstStrings.GetLength(0); i++)
                {
                    en.AddEntry(FirstStrings[i, 0], FirstStrings[i, 1]);
                    ar.AddEntry(FirstStrings[i, 0], FirstStrings[i, 2]);
                }
                EditorUtility.SetDirty(en);
                EditorUtility.SetDirty(ar);
                EditorUtility.SetDirty(ui.SharedData);
                Debug.Log("Zakzoqa: localization tables created at " + TablesDir);
            }

            // Loaded with Localization itself, before the first screen. Loaded on demand, the table
            // arrives a few frames after the screen that asked for it, which shows its buttons blank
            // for those frames (the snapshot tool caught it). The UI table is small; preloading it costs
            // nothing a player could notice.
            LocalizationEditorSettings.GetStringTableCollection(Language.Table).SetPreloadTableFlag(true);

            AssetDatabase.SaveAssets();
        }

        static Locale EnsureLocale(string code)
        {
            string path = $"{LocalesDir}/{code}.asset";
            Locale locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(new LocaleIdentifier(code));
                AssetDatabase.CreateAsset(locale, path);
            }
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }
    }
}
