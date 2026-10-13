using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Zakzoqa
{
    // The game's language: which one, and what follows from it (Decisions §7).
    //
    // TEXT comes from Unity Localization's string tables (Assets/Localization), never from code.
    // LETTERS are joined and lines wrapped right to left by UI Toolkit's Advanced Text Generator,
    // switched on project-wide and on every panel by the theme. What is left for this class is the
    // DIRECTION of a screen: Arabic menus read right to left, so a panel's root is marked RTL and
    // gets the "rtl" class, and the theme's USS turns rows around and aligns text to the right.
    //
    // The player's choice is a setting: through Prefs (the one door), never carried to another phone.
    public static class Language
    {
        public const string Arabic = "ar";
        public const string English = "en";
        public const string Table = "UI";
        public const string RtlClass = "rtl";
        public const string SavedKey = SaveKeys.SettingPrefix + "Language";

        // Raised after the language changed and every open screen was turned to face it.
        public static event Action Changed;

        public static string Current
        {
            get
            {
                Locale locale = LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale : null;
                return locale != null ? locale.Identifier.Code : Saved ?? English;
            }
        }

        public static bool IsRightToLeft => IsRtl(Current);

        public static bool IsRtl(string code) => code == Arabic;

        // What the player chose, or null if they never did.
        public static string Saved
        {
            get
            {
                string code = Prefs.GetString(SavedKey, "");
                return code.Length > 0 ? code : null;
            }
        }

        public static void Set(string code)
        {
            Prefs.SetString(SavedKey, code);
            Prefs.Save();

            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        }

        // A screen's root. Called by every screen when it is built, and again on every change.
        public static void ApplyTo(VisualElement root, string code)
        {
            if (root == null) return;
            bool rtl = IsRtl(code);
            root.languageDirection = rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
            root.EnableInClassList(RtlClass, rtl);
        }

        public static void ApplyTo(VisualElement root) => ApplyTo(root, Current);

        // One lookup, for text that is not on a bound label (a toast, a store row built in code).
        // Bound labels update themselves; this does not, so callers re-read it on Changed.
        public static string Get(string key, params object[] args) =>
            LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, args);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        static void OnLocaleChanged(Locale locale)
        {
            foreach (UIDocument doc in UnityEngine.Object.FindObjectsByType<UIDocument>())
                ApplyTo(doc.rootVisualElement, locale != null ? locale.Identifier.Code : English);
            Changed?.Invoke();
        }
    }

    // First in Localization's startup selectors: the language the player chose, if they chose one.
    // Replaces the package's PlayerPrefLocaleSelector, which writes PlayerPrefs directly — around the
    // one door, and so around the rule that keeps settings off other phones. After this come the
    // phone's own language (Arabic or English), then English.
    [Serializable]
    public class SavedLocaleSelector : IStartupLocaleSelector
    {
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            string code = Language.Saved;
            return code != null ? availableLocales.GetLocale(code) : null;
        }
    }
}
