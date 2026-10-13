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

        // The data half of a reset, without leaving the scene. Purchases are NOT progress: every
        // entitlement the player paid for is carried across the wipe and granted again.
        //
        // Coins bought with money are progress and ARE wiped. The reset screen must say so before
        // the player confirms (Decisions §6).
        public static void WipeProgress()
        {
            List<string> owned = Entitlements.Owned();

            Prefs.DeleteAll();

            foreach (string id in owned) Entitlements.Grant(id);

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
