using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zakzoqa
{
    // The save's format version, and the steps that bring an older save up to it (Decisions §8).
    //
    // Shadow Escape had no version on its local save, so any change to what a key meant had to be
    // guessed from what was there. Here every save says which version wrote it, and an update that
    // changes the format ships a step that converts the old one — the player keeps their progress.
    //
    // TO CHANGE THE FORMAT: add one step to the END of Steps and raise Current by one. Never edit,
    // remove or reorder a step that has shipped: a phone somewhere still has a save that needs it.
    // SaveMigrationsTests fails if Current and the number of steps disagree.
    public static class SaveMigrations
    {
        public const int Current = 1;

        // Steps[i] takes a save from version i + 1 to version i + 2, reading and writing through Prefs.
        public static readonly IReadOnlyList<Action> Steps = new Action[]
        {
        };

        // Run once per launch, after SaveGuard's check (AfterAssembliesLoaded) and before any scene
        // reads the save. Also run after a cloud save is applied, which may be older than this build.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Run() => RunWith(Current, Steps);

        // The runner, with the version and steps passed in so the tests can exercise it with their own.
        // A fresh install has no version yet and is already the current format: nothing to convert.
        public static void RunWith(int current, IReadOnlyList<Action> steps)
        {
            if (!Prefs.HasKey(SaveKeys.SaveVersion))
            {
                Prefs.SetInt(SaveKeys.SaveVersion, current);
                Prefs.Save();
                return;
            }

            int version = Prefs.GetInt(SaveKeys.SaveVersion, current);
            if (version > current)
            {
                // Written by a newer build, then the app was downgraded. Leave it exactly as it is:
                // converting backwards is guesswork, and the newer build will read it again correctly.
                Debug.LogWarning($"[Save] the save is version {version}, newer than this build's {current}; left untouched.");
                return;
            }

            while (version < current)
            {
                steps[version - 1]();
                version++;
                // Recorded after every step, so a phone killed halfway resumes from the right step.
                Prefs.SetInt(SaveKeys.SaveVersion, version);
                Prefs.Save();
                Debug.Log($"[Save] converted the save to version {version}.");
            }
        }
    }
}
