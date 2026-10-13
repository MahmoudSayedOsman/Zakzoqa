using NUnit.Framework;

namespace Zakzoqa.Tests
{
    // "Reset progress" (SaveManager.WipeProgress). Purchases are not progress: a player who paid for
    // something keeps it through a reset. Shadow Escape protected the one purchase it sold by name;
    // Zakzoqa sells several, so every purchase record must survive, including ones added later.
    public class ResetTests
    {
        SaveSandbox sandbox;

        [SetUp]
        public void SetUp() => sandbox = SaveSandbox.Capture();

        [TearDown]
        public void TearDown() => sandbox.Restore();

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

        // The store's record of coin-pack orders already paid. Wiped, a re-delivered order would pay
        // its coins a second time.
        [Category("P0")]
        [Test]
        public void AReset_KeepsTheRecordOfPaidOrders()
        {
            string paidOrders = SaveKeys.EntitlementPrefix + "PaidOrders";
            Prefs.SetString(paidOrders, "GPA.1|GPA.2");

            SaveManager.WipeProgress();
            Prefs.ForgetCache();

            Assert.AreEqual("GPA.1|GPA.2", Prefs.GetString(paidOrders));
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
