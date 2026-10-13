using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Zakzoqa
{
    // THE TRUSTED COPY OF THE PLAYER'S SAVE. (From Shadow Escape; see its Docs/Anti-Cheat.md.)
    //
    // Everything that is progress or a purchase — coins, stars, levels, unlocked transformations,
    // Remove Ads, the coin doubler, skins — is held here, and Prefs answers every read of those keys
    // from here, never from the raw PlayerPrefs file. The protections that follow:
    //
    //   EDITING THE SAVE FILE. The copy is stored in the file with an HMAC-SHA256 signature, keyed by a
    //   secret assembled in code. At launch the file is checked against it:
    //     - signature good, file different  -> the edited values are put back, exactly;
    //     - signature broken, or removed    -> the save is marked UNTRUSTED (sealed in, so it cannot be
    //                                          removed), purchases are withdrawn — the store gives them back
    //                                          to a real owner — and an untrusted save is never uploaded;
    //     - a key added from outside        -> deleted: it is not in the copy, so it never existed.
    //   A FORGED CLOUD SAVE. The snapshot uploaded to the cloud carries the same kind of signature
    //   (SaveSnapshot.sig), so a save written into the database by hand is refused when it comes back.
    //   MEMORY EDITORS. The copy holds every number XOR'd with a mask chosen at random each launch, so
    //   a search for "the 250 I can see on screen" finds nothing to change.
    //
    // Not bound to the device on purpose: Android's Auto Backup restores the save to a new phone, and a
    // device-bound signature would call that honest player a cheat. A save copied between phones is
    // still a real save; the things worth money in it, purchases, are never trusted from a file —
    // the store confirms them at every launch.
    //
    // Honest limits: nothing on a phone is unbreakable. Someone who takes the build apart can find the
    // secret. This is here to stop the easy cheats, not a determined reverse engineer.
    public static class SaveGuard
    {
        public const string GuardKey = SaveKeys.Guard;
        const string EntitlementPrefix = SaveKeys.EntitlementPrefix;
        const string Header = "g1";
        const string UntrustedLine = "untrusted";

        struct Cell
        {
            public Prefs.Kind Kind;
            public int Masked;      // an int, or a float's bits, XOR the session mask
            public string Text;     // strings are kept as they are
        }

        static Dictionary<string, Cell> mirror;
        static int mask;
        static bool loading;

        // The save was found edited and has not been replaced by a clean one since.
        //
        // Asking checks the save first. In Shadow Escape this was a plain property, so after a fresh
        // load it answered from before the check until something else happened to read the save.
        static bool untrusted;
        public static bool Untrusted
        {
            get { EnsureLoaded(); return untrusted; }
            private set => untrusted = value;
        }

        // Edits were found (and undone, or quarantined) this launch. For the dev panel and the logs.
        static bool tamperFound;
        public static bool TamperFoundThisLaunch
        {
            get { EnsureLoaded(); return tamperFound; }
            private set => tamperFound = value;
        }

        // What the guard holds: progress, and purchases. Never settings.
        public static bool Covers(string key) =>
            key != null && key != GuardKey
            && (SaveScope.IsProgress(key) || key.StartsWith(EntitlementPrefix, StringComparison.Ordinal));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void CheckAtLaunch() => EnsureLoaded();

        // --- reads (through Prefs) -------------------------------------------------------------------

        public static bool Has(string key)
        {
            EnsureLoaded();
            return mirror.ContainsKey(key);
        }

        public static int GetInt(string key, int fallback)
        {
            EnsureLoaded();
            return mirror.TryGetValue(key, out Cell c) && c.Kind == Prefs.Kind.Int ? c.Masked ^ mask : fallback;
        }

        public static float GetFloat(string key, float fallback)
        {
            EnsureLoaded();
            return mirror.TryGetValue(key, out Cell c) && c.Kind == Prefs.Kind.Float ? BitConverter.Int32BitsToSingle(c.Masked ^ mask) : fallback;
        }

        public static string GetString(string key, string fallback)
        {
            EnsureLoaded();
            return mirror.TryGetValue(key, out Cell c) && c.Kind == Prefs.Kind.String ? c.Text : fallback;
        }

        // Every key the guard holds and its type. SaveSnapshot captures progress from here.
        public static List<KeyValuePair<string, Prefs.Kind>> Entries()
        {
            EnsureLoaded();
            List<KeyValuePair<string, Prefs.Kind>> list = new List<KeyValuePair<string, Prefs.Kind>>();
            foreach (KeyValuePair<string, Cell> e in mirror) list.Add(new KeyValuePair<string, Prefs.Kind>(e.Key, e.Value.Kind));
            return list;
        }

        // --- writes (from Prefs, after it has written PlayerPrefs) ------------------------------------

        public static void SetInt(string key, int value)
        {
            EnsureLoaded();
            Put(key, new Cell { Kind = Prefs.Kind.Int, Masked = value ^ mask });
        }

        public static void SetFloat(string key, float value)
        {
            EnsureLoaded();
            Put(key, new Cell { Kind = Prefs.Kind.Float, Masked = BitConverter.SingleToInt32Bits(value) ^ mask });
        }

        public static void SetString(string key, string value)
        {
            EnsureLoaded();
            Put(key, new Cell { Kind = Prefs.Kind.String, Text = value ?? "" });
        }

        public static void Delete(string key)
        {
            EnsureLoaded();
            if (mirror.Remove(key)) Seal();
        }

        // After PlayerPrefs.DeleteAll: a clean, empty, trusted save.
        public static void Clear()
        {
            if (mask == 0) mask = NewMask();
            mirror = new Dictionary<string, Cell>();
            Untrusted = false;
            Seal();
        }

        // A clean save has replaced this phone's: the cloud's, applied by CloudSaveManager.
        public static void MarkTrusted()
        {
            EnsureLoaded();
            if (!Untrusted) return;
            Untrusted = false;
            Seal();
        }

        // The signature SaveSnapshot puts on what it uploads, and checks on what comes back.
        public static string Sign(string text) => Mac(text);

        public static bool Verify(string text, string signature) => FixedTimeEquals(signature, Mac(text));

        // For tests: read everything back from PlayerPrefs, as a fresh launch would.
        public static void ForgetForTests()
        {
            mirror = null;
            TamperFoundThisLaunch = false;
        }

        static void Put(string key, Cell cell)
        {
            mirror[key] = cell;
            Seal();
        }

        // --- load and check ---------------------------------------------------------------------------

        public static void EnsureLoaded()
        {
            if (mirror != null || loading) return;
            loading = true;
            try { Load(); }
            finally { loading = false; }
        }

        static int NewMask() => new System.Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode()).Next() | 1;

        static void Load()
        {
            mask = NewMask();
            mirror = new Dictionary<string, Cell>();
            Untrusted = false;

            string stored = PlayerPrefs.GetString(GuardKey, null);
            if (string.IsNullOrEmpty(stored))
            {
                // No guard. A fresh install has nothing to protect yet. A save that HAS progress and no
                // guard was stripped of it — the one way to make edits look like a first launch.
                bool anyProgress = TakeFromPlayerPrefs();
                if (anyProgress) Quarantine("the save has progress and no guard");
                Seal();
                return;
            }

            if (!TryRead(stored, out Dictionary<string, Cell> cells, out bool untrusted))
            {
                TakeFromPlayerPrefs();
                Quarantine("the guard's signature does not match");
                Seal();
                return;
            }

            mirror = cells;
            Untrusted = untrusted;

            // The signature is good, so the copy is the truth: anything in the file that disagrees
            // with it was edited, and goes back.
            bool reverted = false;
            foreach (KeyValuePair<string, Cell> e in mirror)
                if (!RawMatches(e.Key, e.Value)) { WriteRaw(e.Key, e.Value); reverted = true; }

            // And a guarded key in the file that the copy never had was put there from outside.
            foreach (string key in new List<string>(Prefs.Keys.Keys))
                if (Covers(key) && !mirror.ContainsKey(key) && PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); reverted = true; }

            if (reverted)
            {
                TamperFoundThisLaunch = true;
                Debug.LogWarning("[SaveGuard] the save file was edited; the edits were undone.");
                PlayerPrefs.Save();
            }
        }

        // What the raw file holds, for every guarded key the index knows. Returns whether any of it is
        // progress.
        static bool TakeFromPlayerPrefs()
        {
            bool anyProgress = false;
            foreach (KeyValuePair<string, Prefs.Kind> e in Prefs.Keys)
            {
                if (!Covers(e.Key) || !PlayerPrefs.HasKey(e.Key)) continue;
                if (SaveScope.IsProgress(e.Key)) anyProgress = true;
                switch (e.Value)
                {
                    case Prefs.Kind.Int: mirror[e.Key] = new Cell { Kind = Prefs.Kind.Int, Masked = PlayerPrefs.GetInt(e.Key) ^ mask }; break;
                    case Prefs.Kind.Float: mirror[e.Key] = new Cell { Kind = Prefs.Kind.Float, Masked = BitConverter.SingleToInt32Bits(PlayerPrefs.GetFloat(e.Key)) ^ mask }; break;
                    default: mirror[e.Key] = new Cell { Kind = Prefs.Kind.String, Text = PlayerPrefs.GetString(e.Key) }; break;
                }
            }
            return anyProgress;
        }

        // A save the guard cannot vouch for: the progress is kept — punishing a false alarm by wiping a
        // player's game would be far worse than letting a cheat keep some coins on their own phone —
        // but it is untrusted, so it never reaches the cloud, and purchases go back to the store.
        static void Quarantine(string why)
        {
            Untrusted = true;
            TamperFoundThisLaunch = true;
            foreach (string key in new List<string>(mirror.Keys))
            {
                if (!key.StartsWith(EntitlementPrefix, StringComparison.Ordinal)) continue;
                mirror.Remove(key);
                PlayerPrefs.DeleteKey(key);
            }
            Debug.LogWarning($"[SaveGuard] {why}: the save is now untrusted — purchases go back to the store to confirm, and it will not be uploaded.");
        }

        static bool RawMatches(string key, Cell c)
        {
            if (!PlayerPrefs.HasKey(key)) return false;
            switch (c.Kind)
            {
                case Prefs.Kind.Int: return PlayerPrefs.GetInt(key) == (c.Masked ^ mask);
                case Prefs.Kind.Float: return BitConverter.SingleToInt32Bits(PlayerPrefs.GetFloat(key)) == (c.Masked ^ mask);
                default: return PlayerPrefs.GetString(key) == c.Text;
            }
        }

        static void WriteRaw(string key, Cell c)
        {
            switch (c.Kind)
            {
                case Prefs.Kind.Int: PlayerPrefs.SetInt(key, c.Masked ^ mask); break;
                case Prefs.Kind.Float: PlayerPrefs.SetFloat(key, BitConverter.Int32BitsToSingle(c.Masked ^ mask)); break;
                default: PlayerPrefs.SetString(key, c.Text); break;
            }
        }

        // --- the stored form ----------------------------------------------------------------------------
        //
        //   g1
        //   [untrusted]
        //   <type>\t<key>\t<value>      one per key, sorted — ints and float BITS as integers, so every
        //   ...                         value is exact and signs the same on every run
        //   #<base64 HMAC-SHA256 of everything above>

        static void Seal()
        {
            StringBuilder body = new StringBuilder(Header);
            if (Untrusted) body.Append('\n').Append(UntrustedLine);

            List<string> keys = new List<string>(mirror.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                Cell c = mirror[key];
                body.Append('\n').Append((int)c.Kind).Append('\t').Append(Uri.EscapeDataString(key)).Append('\t');
                body.Append(c.Kind == Prefs.Kind.String
                    ? Uri.EscapeDataString(c.Text)
                    : (c.Masked ^ mask).ToString(CultureInfo.InvariantCulture));
            }

            string text = body.ToString();
            PlayerPrefs.SetString(GuardKey, text + "\n#" + Mac(text));
        }

        static bool TryRead(string stored, out Dictionary<string, Cell> cells, out bool untrusted)
        {
            cells = new Dictionary<string, Cell>();
            untrusted = false;

            int hash = stored.LastIndexOf("\n#", StringComparison.Ordinal);
            if (hash < 0) return false;
            string body = stored.Substring(0, hash);
            if (!FixedTimeEquals(stored.Substring(hash + 2), Mac(body))) return false;

            string[] lines = body.Split('\n');
            if (lines.Length == 0 || lines[0] != Header) return false;

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i] == UntrustedLine) { untrusted = true; continue; }
                string[] parts = lines[i].Split('\t');
                if (parts.Length != 3 || !int.TryParse(parts[0], out int kind) || kind < 0 || kind > 2) return false;
                string key = Uri.UnescapeDataString(parts[1]);
                Prefs.Kind k = (Prefs.Kind)kind;
                if (k == Prefs.Kind.String)
                {
                    cells[key] = new Cell { Kind = k, Text = Uri.UnescapeDataString(parts[2]) };
                }
                else
                {
                    if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw)) return false;
                    cells[key] = new Cell { Kind = k, Masked = raw ^ mask };
                }
            }
            return true;
        }

        static string Mac(string text)
        {
            using (HMACSHA256 h = new HMACSHA256(Secret()))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
        }

        static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // The key: two halves that only mean something together, hashed. Never a string in the build,
        // so a search of the binary for something key-like finds nothing.
        static byte[] secret;

        static byte[] Secret()
        {
            if (secret != null) return secret;
            byte[] a = { 0x20, 0x6e, 0x71, 0x9c, 0x6e, 0x27, 0xe4, 0x85, 0xfc, 0xb5, 0xc8, 0x6e, 0x61, 0xdf, 0x6f, 0x38 };
            byte[] b = { 0xc2, 0xf4, 0x21, 0xae, 0x36, 0xc0, 0xe2, 0x1f, 0x6c, 0xd8, 0x98, 0xfa, 0x0c, 0x3c, 0x41, 0x9d };
            byte[] halves = new byte[a.Length];
            for (int i = 0; i < a.Length; i++) halves[i] = (byte)(a[i] ^ b[i]);
            using (SHA256 sha = SHA256.Create()) secret = sha.ComputeHash(halves);
            return secret;
        }
    }
}
