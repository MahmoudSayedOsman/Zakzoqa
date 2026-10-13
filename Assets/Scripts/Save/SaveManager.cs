using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zakzoqa
{
    // The one authority for wiping the save and for what happens when the save changes underneath a
    // running game. It does not own the keys: each owner reads and writes its own through Prefs.
    public static class SaveManager
    {
        // Raised when the save was replaced (a reset, or the cloud's save applied), before the game
        // goes back to the main menu. Anything that caches the save in statics clears it here.
        // Shadow Escape listed those caches by hand inside this class, and each new one had to be
        // remembered; here each owner subscribes for itself.
        public static event Action SaveReplaced;

        public static void ResetAllProgress()
        {
            WipeProgress();
            ReloadAfterSaveChanged();
        }

        // The data half of a reset, without leaving the scene. Purchases are NOT progress: every key
        // under the purchases prefix is carried across the wipe exactly — each entitlement the player
        // paid for, and the store's record of coin-pack orders already paid, without which a
        // re-delivered order would pay its coins a second time.
        //
        // Coins bought with money are progress and ARE wiped. The reset screen must say so before
        // the player confirms (Decisions §12.5).
        public static void WipeProgress()
        {
            List<(string key, Prefs.Kind kind, int i, float f, string s)> purchases = new List<(string, Prefs.Kind, int, float, string)>();
            foreach (KeyValuePair<string, Prefs.Kind> e in Prefs.Keys)
            {
                if (!e.Key.StartsWith(SaveKeys.EntitlementPrefix, StringComparison.Ordinal) || !Prefs.HasKey(e.Key)) continue;
                purchases.Add((e.Key, e.Value, Prefs.GetInt(e.Key), Prefs.GetFloat(e.Key), Prefs.GetString(e.Key)));
            }

            Prefs.DeleteAll();

            foreach (var p in purchases)
            {
                switch (p.kind)
                {
                    case Prefs.Kind.Int: Prefs.SetInt(p.key, p.i); break;
                    case Prefs.Kind.Float: Prefs.SetFloat(p.key, p.f); break;
                    default: Prefs.SetString(p.key, p.s); break;
                }
            }

            // The empty save is the current format, and it has to beat the cloud's, or the next
            // launch would find the cloud further along and quietly put everything back.
            SaveMigrations.Run();
            CloudSaveManager.NoteDeliberateReset();
            Prefs.Save();
        }

        public static void ReloadAfterSaveChanged()
        {
            SaveReplaced?.Invoke();
            // A reset is reachable from the pause menu, where time is stopped.
            Time.timeScale = 1f;
            SceneManager.LoadScene(Scenes.MainMenu);
        }
    }
}
