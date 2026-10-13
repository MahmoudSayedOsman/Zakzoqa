using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // "Reset progress" (SaveManager.WipeProgress). Purchases are not progress: a player who paid for
    // something keeps it through a reset. Shadow Escape protected the one purchase it sold by name;
    // Zakzoqa sells several, so every entitlement must survive, including ones added later.
    //
    // A wipe really does delete every PlayerPrefs key, so the whole file is captured raw first and
    // written back raw at the end.
    public class ResetTests
    {
        Dictionary<string, (Prefs.Kind kind, string text, int i, float f)> before;

        [SetUp]
        public void SetUp()
        {
            Prefs.ForgetCache();
            SaveGuard.EnsureLoaded();
            before = new Dictionary<string, (Prefs.Kind, string, int, float)>();
            foreach (KeyValuePair<string, Prefs.Kind> e in Prefs.Keys)
                before[e.Key] = (e.Value, PlayerPrefs.GetString(e.Key, ""), PlayerPrefs.GetInt(e.Key, 0), PlayerPrefs.GetFloat(e.Key, 0f));
            before[Prefs.IndexKey] = (Prefs.Kind.String, PlayerPrefs.GetString(Prefs.IndexKey, ""), 0, 0f);
            before[SaveGuard.GuardKey] = (Prefs.Kind.String, PlayerPrefs.GetString(SaveGuard.GuardKey, ""), 0, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            foreach (var e in before)
            {
                switch (e.Value.kind)
                {
                    case Prefs.Kind.Int: PlayerPrefs.SetInt(e.Key, e.Value.i); break;
                    case Prefs.Kind.Float: PlayerPrefs.SetFloat(e.Key, e.Value.f); break;
                    default: PlayerPrefs.SetString(e.Key, e.Value.text); break;
                }
            }
            PlayerPrefs.Save();
            Prefs.ForgetCache();
            SaveGuard.EnsureLoaded();
        }

        [Category("P0")]
        [Test]
        public void AReset_KeepsEveryPurchase_AndWipesTheProgress()
        {
            Prefs.SetInt(SaveKeys.TotalStars, 30);
            Prefs.SetInt(SaveKeys.Coins, 500);
            Entitlements.Grant(Entitlements.RemoveAds);
            Entitlements.Grant(Entitlements.CoinDoubler);
            Entitlements.Grant("SkinPack_Sea");   // one no code names: added in some later update

            SaveManager.WipeProgress();
            Prefs.ForgetCache();                   // and the next launch agrees

            Assert.IsTrue(Entitlements.Has(Entitlements.RemoveAds), "a reset took away Remove Ads.");
            Assert.IsTrue(Entitlements.Has(Entitlements.CoinDoubler), "a reset took away the coin doubler.");
            Assert.IsTrue(Entitlements.Has("SkinPack_Sea"), "a reset took away a purchase nobody listed.");
            Assert.AreEqual(0, Prefs.GetInt(SaveKeys.TotalStars), "the stars survived a reset.");
            Assert.AreEqual(0, Prefs.GetInt(SaveKeys.Coins), "the coins survived a reset.");
            Assert.IsFalse(SaveGuard.Untrusted, "a reset left the save untrusted.");
        }

        [Category("P0")]
        [Test]
        public void AReset_LeavesACurrentFormatSave_ThatBeatsTheCloud()
        {
            SaveManager.WipeProgress();

            Assert.AreEqual(SaveMigrations.Current, Prefs.GetInt(SaveKeys.SaveVersion));
            Assert.AreEqual(1, Prefs.GetInt(CloudSaveManager.ResetPendingKey),
                "the next launch would find the cloud further along and undo the reset.");
        }
    }
}
