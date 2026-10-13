using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Zakzoqa
{
    // The player's progress at one moment, as data: what goes to the cloud and comes back from it.
    // (From Shadow Escape.)
    //
    // Two versions, not to be confused:
    //   version             the shape of THIS envelope (entries, sig). Changes almost never.
    //   SaveKeys.SaveVersion one of the entries: the format of the progress inside, which
    //                        SaveMigrations converts after the snapshot is applied.
    [Serializable]
    public class SaveSnapshot
    {
        public const int EnvelopeVersion = 1;

        public int version = EnvelopeVersion;
        // When it was taken, in Unix seconds. For the record; the choice between two saves is made on
        // progress, not on age.
        public long savedAt;
        public List<Entry> entries = new List<Entry>();
        // SaveGuard's signature over the entries. A save written into the cloud by hand has none that
        // matches, and FromJson refuses it.
        public string sig;

        [Serializable]
        public class Entry
        {
            public string k;
            // 0 int, 1 float, 2 string.
            public int t;
            // An int's value; for a float, its exact BITS — the one that is applied and signed, since
            // a float's text form in JSON is not guaranteed to come back bit for bit.
            public int i;
            // A float's value, for anyone reading the save by eye.
            public float f;
            public string s;
        }

        // All the player's progress, from the trusted copy (SaveGuard), signed.
        public static SaveSnapshot Capture()
        {
            SaveSnapshot snap = new SaveSnapshot { savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            List<KeyValuePair<string, Prefs.Kind>> held = SaveGuard.Entries();
            held.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
            foreach (KeyValuePair<string, Prefs.Kind> e in held)
            {
                if (!SaveScope.IsProgress(e.Key)) continue;
                Entry entry = new Entry { k = e.Key, t = (int)e.Value };
                switch (e.Value)
                {
                    case Prefs.Kind.Int: entry.i = Prefs.GetInt(e.Key); break;
                    case Prefs.Kind.Float:
                        entry.f = Prefs.GetFloat(e.Key);
                        entry.i = BitConverter.SingleToInt32Bits(entry.f);
                        break;
                    default: entry.s = Prefs.GetString(e.Key); break;
                }
                snap.entries.Add(entry);
            }
            snap.sig = SaveGuard.Sign(snap.SignedText());
            return snap;
        }

        // What the signature covers: the version and every entry, in order, values exact.
        string SignedText()
        {
            StringBuilder sb = new StringBuilder("v").Append(version);
            foreach (Entry e in entries)
            {
                if (e == null) { sb.Append("\n-"); continue; }
                sb.Append('\n').Append(e.t).Append('\t').Append(Uri.EscapeDataString(e.k ?? "")).Append('\t');
                sb.Append(e.t == 2 ? Uri.EscapeDataString(e.s ?? "") : e.i.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public bool IsSigned => !string.IsNullOrEmpty(sig) && SaveGuard.Verify(SignedText(), sig);

        // The format of the progress inside. A save from before versioning cannot exist in Zakzoqa, so
        // a snapshot without one is treated as the first version.
        public int SaveVersion => HasEntry(SaveKeys.SaveVersion) ? IntOf(SaveKeys.SaveVersion) : 1;

        // Makes this phone's progress exactly this snapshot: progress keys it does not hold are
        // removed, the ones it holds are written, and the result is converted to this build's format.
        // The phone's own settings and purchases are not touched.
        public void Apply()
        {
            HashSet<string> incoming = new HashSet<string>();
            foreach (Entry e in entries) if (e != null && SaveScope.IsProgress(e.k)) incoming.Add(e.k);

            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, Prefs.Kind> held in SaveGuard.Entries())
                if (SaveScope.IsProgress(held.Key) && !incoming.Contains(held.Key)) stale.Add(held.Key);
            foreach (string key in stale) Prefs.DeleteKey(key);

            foreach (Entry e in entries)
            {
                if (e == null || !SaveScope.IsProgress(e.k)) continue;
                switch (e.t)
                {
                    case 0: Prefs.SetInt(e.k, e.i); break;
                    case 1: Prefs.SetFloat(e.k, BitConverter.Int32BitsToSingle(e.i)); break;
                    default: Prefs.SetString(e.k, e.s ?? ""); break;
                }
            }
            SaveMigrations.Run();
            Prefs.Save();
        }

        public string ToJson() => JsonUtility.ToJson(this);

        // Null for anything that is not a snapshot this build can apply: empty, corrupt, unsigned
        // (written or edited by hand), or written by a NEWER build — this one cannot read its format,
        // and applying it would lose whatever the newer build added. A save that cannot be trusted
        // must never be applied.
        public static SaveSnapshot FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                SaveSnapshot snap = JsonUtility.FromJson<SaveSnapshot>(json);
                if (snap == null || snap.entries == null || snap.version != EnvelopeVersion) return null;
                if (!snap.IsSigned)
                {
                    Debug.LogWarning("[CloudSave] a save arrived without a valid signature and was refused.");
                    return null;
                }
                if (snap.SaveVersion > SaveMigrations.Current)
                {
                    Debug.LogWarning($"[CloudSave] the cloud's save is version {snap.SaveVersion}, newer than this build; update the game to use it.");
                    return null;
                }
                return snap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        bool HasEntry(string key)
        {
            foreach (Entry e in entries) if (e != null && e.k == key) return true;
            return false;
        }

        int IntOf(string key)
        {
            foreach (Entry e in entries) if (e != null && e.k == key && e.t == 0) return e.i;
            return 0;
        }

        int CountWithPrefix(string prefix)
        {
            int n = 0;
            foreach (Entry e in entries) if (e != null && e.k != null && e.k.StartsWith(prefix, StringComparison.Ordinal)) n++;
            return n;
        }

        // How far along a save is, for choosing between two. Only measures that never go DOWN in play:
        // stars first — the player's own measure of how far they are — then the furthest chapter
        // reached, then levels completed, then achievements unlocked.
        //
        // Not coins: they are spent, so a player who just bought a skin would look LESS far along
        // than the phone they left behind, and lose the skin to it.
        public static int CompareProgress(SaveSnapshot a, SaveSnapshot b)
        {
            int c = a.IntOf(SaveKeys.TotalStars).CompareTo(b.IntOf(SaveKeys.TotalStars));
            if (c != 0) return c;
            c = a.IntOf(SaveKeys.HighestChapter).CompareTo(b.IntOf(SaveKeys.HighestChapter));
            if (c != 0) return c;
            c = a.CountWithPrefix(SaveKeys.LevelCompletedPrefix).CompareTo(b.CountWithPrefix(SaveKeys.LevelCompletedPrefix));
            if (c != 0) return c;
            return a.CountWithPrefix(SaveKeys.AchievementPrefix).CompareTo(b.CountWithPrefix(SaveKeys.AchievementPrefix));
        }
    }
}
