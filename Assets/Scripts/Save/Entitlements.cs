using System;
using System.Collections.Generic;

namespace Zakzoqa
{
    // What the player owns for good: Remove Ads, the coin doubler, skin packs. Kept apart from the
    // store code so gameplay asks "does the player have Remove Ads?" without knowing whether it came
    // from a purchase, a restore or a promo.
    //
    // Shadow Escape had one hard-coded entitlement; Zakzoqa sells several (Decisions §6), so they are
    // kept by id, each under SaveKeys.EntitlementPrefix + id. Coins are not here: they are spent.
    public static class Entitlements
    {
        public const string RemoveAds = "RemoveAds";
        public const string CoinDoubler = "CoinDoubler";

        public static bool Has(string id) => Prefs.GetInt(Key(id), 0) == 1;

        public static void Grant(string id)
        {
            Prefs.SetInt(Key(id), 1);
            Prefs.Save();
        }

        // Only when the store, asked directly, says this account does not own it — refunded, or
        // never bought. Never by gameplay.
        public static void Revoke(string id)
        {
            Prefs.DeleteKey(Key(id));
            Prefs.Save();
        }

        // Every id this phone holds, from the save's own index.
        public static List<string> Owned()
        {
            List<string> owned = new List<string>();
            foreach (string key in Prefs.Keys.Keys)
                if (key.StartsWith(SaveKeys.EntitlementPrefix, StringComparison.Ordinal) && Prefs.GetInt(key, 0) == 1)
                    owned.Add(key.Substring(SaveKeys.EntitlementPrefix.Length));
            return owned;
        }

        static string Key(string id) => SaveKeys.EntitlementPrefix + id;
    }
}
