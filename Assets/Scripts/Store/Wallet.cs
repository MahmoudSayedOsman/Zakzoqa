using System;

namespace Zakzoqa
{
    // Where coins come from. Every credit names one, and the ledger records it.
    public enum CoinSource
    {
        Play,           // picked up in a level: the coin doubler applies
        LevelStars,     // the prize for stars earned the first time on a level: the doubler applies
        DailyReward,
        Achievement,
        RewardedAd,     // "double the level's coins" and the like: already a doubling, so no doubler
        Purchase,       // a coin pack: exactly what was paid for, never doubled
    }

    // What coins can buy (Decisions §5, option A). THIS LIST IS THE RULE: TrySpend takes nothing
    // else, so there is no way to write "spend coins on an upgrade" without adding a member here —
    // and StoreRulesTests fails on any member that sounds like power. Upgrades are unlocked by
    // stars, which are earned in play and never sold.
    public enum CoinSink
    {
        Skin,               // looks only
        LevelConsumable,    // tape or a bubble, one of each per level (Decisions §4)
        ContinueHere,       // "carry on from here" after losing, once per level
    }

    // THE GAME'S ONE CURRENCY. Static and stateless: the balance lives in the save (guarded, carried
    // to the cloud), so nothing here can drift from it.
    public static class Wallet
    {
        public static int Balance => Prefs.GetInt(SaveKeys.Coins, 0);

        public static bool HasCoinDoubler => Entitlements.Has(Entitlements.CoinDoubler);

        // What a credit from this source actually pays, the doubler included.
        public static int Payout(int amount, CoinSource source)
        {
            if (amount <= 0) return 0;
            bool doubles = source == CoinSource.Play || source == CoinSource.LevelStars;
            return doubles && HasCoinDoubler ? amount * 2 : amount;
        }

        // Returns what was actually credited.
        public static int Credit(int amount, CoinSource source)
        {
            int paid = Payout(amount, source);
            if (paid <= 0) return 0;
            int balance = Balance + paid;
            Prefs.SetInt(SaveKeys.Coins, balance);
            CoinLedger.Record(paid, source.ToString(), balance);
            Prefs.Save();
            return paid;
        }

        // False, and nothing taken, when the player cannot afford it.
        public static bool TrySpend(int amount, CoinSink sink, string item)
        {
            if (amount <= 0) return true;
            int balance = Balance;
            if (balance < amount) return false;

            balance -= amount;
            Prefs.SetInt(SaveKeys.Coins, balance);
            CoinLedger.Record(-amount, string.IsNullOrEmpty(item) ? sink.ToString() : $"{sink}:{item}", balance);
            Prefs.Save();
            return true;
        }
    }

    // Every coin in or out, newest last, the last Keep of them. For two people: the player who writes
    // "my coins disappeared" (support can see where they went), and the developer balancing what the
    // campaign pays against what the shop costs.
    //
    // Progress, so it travels with the balance it explains. One line per entry:
    //   unixSeconds|delta|reason|balanceAfter
    public static class CoinLedger
    {
        public const string Key = SaveKeys.Game + "Wallet_Ledger";
        public const int Keep = 200;

        public static void Record(int delta, string reason, int balanceAfter)
        {
            string line = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{delta}|{Clean(reason)}|{balanceAfter}";
            string[] old = Lines();
            int start = Math.Max(0, old.Length - (Keep - 1));
            string kept = string.Join("\n", old, start, old.Length - start);
            Prefs.SetString(Key, kept.Length > 0 ? kept + "\n" + line : line);
        }

        public static string[] Lines()
        {
            string raw = Prefs.GetString(Key, "");
            return raw.Length == 0 ? new string[0] : raw.Split('\n');
        }

        // The separators are the format.
        static string Clean(string reason) => (reason ?? "").Replace("|", "/").Replace("\n", " ");
    }
}
