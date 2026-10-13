using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Zakzoqa
{
    // THE ONE DOOR TO PLAYERPREFS. Every read, write and delete in the game comes through here.
    // (From Shadow Escape, where it was proven in closed testing.)
    //
    // Why a door for WRITES: cloud save has to copy the player's progress off the phone, and Android
    // cannot list the keys PlayerPrefs holds. Many keys are built from ids at runtime — one per level,
    // per achievement — so a hand-kept list would be wrong the first time someone added a key and
    // forgot it, and the failure would be silent. So the list keeps itself: every key written is
    // recorded, with its type, in an index stored alongside the keys.
    //
    // Why a door for READS: progress and purchases are answered from SaveGuard's signed, masked copy,
    // never from the file, so an edited save file changes nothing. Settings are read from PlayerPrefs
    // directly — nothing is gained by cheating a volume slider.
    //
    // CloudSaveTests scans the scripts and fails if anything touches PlayerPrefs directly.
    public static class Prefs
    {
        // The index: one line per key, "i:Key", "f:Key" or "s:Key". Stored in PlayerPrefs itself, so
        // it lives and dies with the keys it describes.
        public const string IndexKey = SaveKeys.PrefsIndex;

        public enum Kind { Int, Float, String }

        static Dictionary<string, Kind> index;

        // Raised after any key is written or deleted, with the key (null for DeleteAll).
        // CloudSaveManager listens to know when there is progress to upload.
        public static event Action<string> Changed;

        // --- reads ------------------------------------------------------------------------------

        public static int GetInt(string key, int fallback = 0) =>
            SaveGuard.Covers(key) ? SaveGuard.GetInt(key, fallback) : PlayerPrefs.GetInt(key, fallback);

        public static float GetFloat(string key, float fallback = 0f) =>
            SaveGuard.Covers(key) ? SaveGuard.GetFloat(key, fallback) : PlayerPrefs.GetFloat(key, fallback);

        public static string GetString(string key, string fallback = "") =>
            SaveGuard.Covers(key) ? SaveGuard.GetString(key, fallback) : PlayerPrefs.GetString(key, fallback);

        public static bool HasKey(string key) =>
            SaveGuard.Covers(key) ? SaveGuard.Has(key) : PlayerPrefs.HasKey(key);

        // --- writes -----------------------------------------------------------------------------
        //
        // A guarded key is written to the file too, in the clear: the file is what the guard checks
        // at the next launch. The guard is loaded FIRST, so its launch check never mistakes the value
        // being written for an edit.

        public static void SetInt(string key, int value)
        {
            bool guarded = SaveGuard.Covers(key);
            if (guarded) SaveGuard.EnsureLoaded();
            PlayerPrefs.SetInt(key, value);
            if (guarded) SaveGuard.SetInt(key, value);
            Record(key, Kind.Int);
        }

        public static void SetFloat(string key, float value)
        {
            bool guarded = SaveGuard.Covers(key);
            if (guarded) SaveGuard.EnsureLoaded();
            PlayerPrefs.SetFloat(key, value);
            if (guarded) SaveGuard.SetFloat(key, value);
            Record(key, Kind.Float);
        }

        public static void SetString(string key, string value)
        {
            bool guarded = SaveGuard.Covers(key);
            if (guarded) SaveGuard.EnsureLoaded();
            PlayerPrefs.SetString(key, value);
            if (guarded) SaveGuard.SetString(key, value);
            Record(key, Kind.String);
        }

        public static void DeleteKey(string key)
        {
            bool guarded = SaveGuard.Covers(key);
            if (guarded) SaveGuard.EnsureLoaded();
            PlayerPrefs.DeleteKey(key);
            if (guarded) SaveGuard.Delete(key);
            Load();
            if (index.Remove(key)) SaveIndex();
            Changed?.Invoke(key);
        }

        // Everything, index included. What is left is a clean, empty, trusted save.
        public static void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            index = new Dictionary<string, Kind>();
            SaveGuard.Clear();
            Changed?.Invoke(null);
        }

        public static void Save() => PlayerPrefs.Save();

        // Every key written through here since the index began, with its type.
        public static IReadOnlyDictionary<string, Kind> Keys
        {
            get
            {
                Load();
                return index;
            }
        }

        // Forgets the in-memory copies, so the next use reads the index and the guard back from
        // PlayerPrefs, as a fresh launch would. For tests that change PlayerPrefs underneath them.
        public static void ForgetCache()
        {
            index = null;
            SaveGuard.ForgetForTests();
        }

        static void Record(string key, Kind kind)
        {
            Load();
            if (key != IndexKey && (!index.TryGetValue(key, out Kind known) || known != kind))
            {
                index[key] = kind;
                SaveIndex();
            }
            Changed?.Invoke(key);
        }

        static void Load()
        {
            if (index != null) return;
            index = new Dictionary<string, Kind>();
            string raw = PlayerPrefs.GetString(IndexKey, "");
            foreach (string line in raw.Split('\n'))
            {
                if (line.Length < 3 || line[1] != ':') continue;
                Kind kind = line[0] == 'i' ? Kind.Int : line[0] == 'f' ? Kind.Float : Kind.String;
                index[line.Substring(2)] = kind;
            }
        }

        static void SaveIndex()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, Kind> e in index)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(e.Value == Kind.Int ? 'i' : e.Value == Kind.Float ? 'f' : 's').Append(':').Append(e.Key);
            }
            PlayerPrefs.SetString(IndexKey, sb.ToString());
        }
    }
}
