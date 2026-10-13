using System;

namespace Zakzoqa
{
    // Which saved keys are the player's PROGRESS — what follows them to a new phone — and which belong
    // to this phone only.
    //
    // PROGRESS BY DEFAULT. Every game key is progress unless it is the phone's own. Shadow Escape kept
    // the phone's keys as a hand-written list that grew with every new setting; here they are known by
    // prefix (SaveKeys), so a new setting needs nothing added anywhere. The reverse mistake — a
    // forgotten key — still errs towards the player: it travels rather than being lost.
    public static class SaveScope
    {
        static readonly string[] DeviceOnly =
        {
            SaveKeys.PrefsIndex,
            // The anti-cheat signature over this phone's save (SaveGuard).
            SaveKeys.Guard,
            // Settings: volume, language, controls. One phone's screen is not another's.
            SaveKeys.SettingPrefix,
            // Purchases: the store restores those itself, and a copied entitlement would be a way to
            // get one without paying.
            SaveKeys.EntitlementPrefix,
            // Remote Config's copy of the tuning this phone last received.
            SaveKeys.RemotePrefix,
            // The cloud save's own bookkeeping.
            SaveKeys.CloudPrefix,
            // A checkpoint is a place in one level on one phone.
            SaveKeys.CheckpointPrefix,
        };

        public static bool IsProgress(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(SaveKeys.Game, StringComparison.Ordinal)) return false;
            foreach (string d in DeviceOnly)
            {
                bool isPrefix = d.EndsWith("_", StringComparison.Ordinal);
                if (isPrefix ? key.StartsWith(d, StringComparison.Ordinal) : key == d) return false;
            }
            return true;
        }
    }
}
