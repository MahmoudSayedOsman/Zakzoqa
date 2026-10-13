using System.Collections.Generic;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // Tests run against the editor's real PlayerPrefs, which also hold the developer's own playtest
    // save. Capture() before a test that writes the save, Restore() after it, and the save comes out
    // exactly as it went in — every key the game's index knows, the index itself and the guard's
    // signature, raw — however much the test wrote, wiped or broke in between.
    public sealed class SaveSandbox
    {
        readonly Dictionary<string, (Prefs.Kind kind, int i, float f, string s)> keys =
            new Dictionary<string, (Prefs.Kind, int, float, string)>();

        public static SaveSandbox Capture()
        {
            Prefs.ForgetCache();
            SaveGuard.EnsureLoaded();

            SaveSandbox box = new SaveSandbox();
            foreach (KeyValuePair<string, Prefs.Kind> e in Prefs.Keys)
            {
                if (!PlayerPrefs.HasKey(e.Key)) continue;
                box.keys[e.Key] = (e.Value, PlayerPrefs.GetInt(e.Key, 0), PlayerPrefs.GetFloat(e.Key, 0f), PlayerPrefs.GetString(e.Key, ""));
            }
            foreach (string machinery in new[] { Prefs.IndexKey, SaveGuard.GuardKey })
                if (PlayerPrefs.HasKey(machinery))
                    box.keys[machinery] = (Prefs.Kind.String, 0, 0f, PlayerPrefs.GetString(machinery, ""));
            return box;
        }

        public void Restore()
        {
            // Everything the test may have added, removed through the index or not.
            Prefs.ForgetCache();
            foreach (string key in new List<string>(Prefs.Keys.Keys)) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(Prefs.IndexKey);
            PlayerPrefs.DeleteKey(SaveGuard.GuardKey);

            foreach (var e in keys)
            {
                switch (e.Value.kind)
                {
                    case Prefs.Kind.Int: PlayerPrefs.SetInt(e.Key, e.Value.i); break;
                    case Prefs.Kind.Float: PlayerPrefs.SetFloat(e.Key, e.Value.f); break;
                    default: PlayerPrefs.SetString(e.Key, e.Value.s); break;
                }
            }
            PlayerPrefs.Save();
            Prefs.ForgetCache();
            SaveGuard.EnsureLoaded();
        }
    }
}
