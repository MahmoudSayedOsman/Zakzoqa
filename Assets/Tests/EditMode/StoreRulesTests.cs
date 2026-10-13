using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // The economy rules in Decisions §5 (option A), checked where they live: in the types. Shadow
    // Escape sold coins that bought strength, decided against it, then sold them again with a cap.
    // Zakzoqa settles it in the code instead: coins buy looks and conveniences, stars unlock
    // upgrades, and nothing sold can buy power. These fail the day that stops being true.
    public class StoreRulesTests
    {
        SaveSandbox sandbox;

        [SetUp]
        public void SetUp()
        {
            sandbox = SaveSandbox.Capture();
            Prefs.SetInt(SaveKeys.Coins, 0);
            Prefs.DeleteKey(CoinLedger.Key);
            Entitlements.Revoke(Entitlements.CoinDoubler);
            ParentalGate.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ParentalGate.ResetForTests();
            sandbox.Restore();
        }

        // =====================================================================================
        // What coins can and cannot buy
        // =====================================================================================

        static readonly string[] PowerWords = { "upgrade", "star", "power", "damage", "health", "weapon", "transform", "skip" };

        [Category("P0")]
        [Test]
        public void Coins_CanOnlyBeSpentOnLooksAndConveniences()
        {
            foreach (string sink in Enum.GetNames(typeof(CoinSink)))
                foreach (string word in PowerWords)
                    Assert.IsFalse(sink.ToLowerInvariant().Contains(word),
                        $"CoinSink.{sink} looks like coins buying power ('{word}'). Upgrades are unlocked by stars (Decisions §5).");
        }

        [Category("P0")]
        [Test]
        public void AProduct_CanOnlyGrantAnEntitlementOrCoins()
        {
            string[] allowed = { "productId", "kind", "displayNameKey", "fallbackPriceLabel", "entitlement", "coinGrant", "badgeKey" };
            string[] fields = typeof(IapProductDef).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();

            CollectionAssert.AreEquivalent(allowed, fields,
                "a product gained a new kind of grant. If it grants anything that is power, it breaks Decisions §5.");
        }

        [Category("P0")]
        [Test]
        public void TheShippedCatalog_SellsWhatTheDecisionsSay()
        {
            IapCatalog shipped = AssetDatabase.LoadAssetAtPath<IapCatalog>(Zakzoqa.EditorTools.StoreCatalogSetup.AssetPath);
            Assert.IsNotNull(shipped, "Resources/IapCatalog.asset is missing: run Zakzoqa > Apply Project Settings.");
            Assert.IsNotEmpty(shipped.products);

            foreach (IapProductDef def in shipped.products)
            {
                Assert.IsNotNull(def, "the catalog has an empty entry.");
                Assert.IsFalse(string.IsNullOrEmpty(def.productId), "a product with no id.");
                Assert.IsFalse(string.IsNullOrEmpty(def.fallbackPriceLabel), $"'{def.productId}' has no price.");
                Assert.IsTrue(def.coinGrant > 0 || !string.IsNullOrEmpty(def.entitlement),
                    $"'{def.productId}' grants nothing — a player would pay for air.");

                if (def.kind == IapProductKind.Consumable)
                    Assert.IsTrue(string.IsNullOrEmpty(def.entitlement),
                        $"'{def.productId}' sells something owned forever as a consumable: it could never be restored.");
                else
                    Assert.AreEqual(0, def.coinGrant,
                        $"'{def.productId}' is owned forever and pays coins: a restore must never pay them, which players read as a bug.");
            }

            var ids = shipped.products.Select(p => p.productId).ToList();
            CollectionAssert.AllItemsAreUnique(ids, "two products share an id.");
            foreach (string id in new[] { IapManager.ProductRemoveAds, IapManager.ProductCoinDoubler,
                                          IapManager.ProductCoinsSmall, IapManager.ProductCoinsMedium, IapManager.ProductCoinsLarge })
                CollectionAssert.Contains(ids, id, $"'{id}' is named in code but not sold.");
        }

        // =====================================================================================
        // The wallet
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void Spending_TakesNothing_WhenThePlayerCannotAfford()
        {
            Wallet.Credit(50, CoinSource.Play);

            Assert.IsFalse(Wallet.TrySpend(80, CoinSink.Skin, "sea"));
            Assert.AreEqual(50, Wallet.Balance, "a refused purchase still took coins.");

            Assert.IsTrue(Wallet.TrySpend(50, CoinSink.Skin, "sea"));
            Assert.AreEqual(0, Wallet.Balance);
        }

        // ×2 on what is earned by playing, and nothing else: not on coins bought (the player paid for
        // exactly that many), not on a rewarded ad (already a doubling).
        [Category("P0")]
        [Test]
        public void TheCoinDoubler_DoublesPlay_NotPurchasesOrAds()
        {
            Entitlements.Grant(Entitlements.CoinDoubler);

            Assert.AreEqual(20, Wallet.Credit(10, CoinSource.Play));
            Assert.AreEqual(20, Wallet.Credit(10, CoinSource.LevelStars));
            Assert.AreEqual(500, Wallet.Credit(500, CoinSource.Purchase), "a bought coin pack was doubled.");
            Assert.AreEqual(10, Wallet.Credit(10, CoinSource.RewardedAd), "an ad's doubling was doubled again.");
            Assert.AreEqual(550, Wallet.Balance);
        }

        [Test]
        public void WithoutTheDoubler_PlayPaysWhatItSays()
        {
            Assert.AreEqual(10, Wallet.Credit(10, CoinSource.Play));
        }

        // "My coins disappeared": the ledger says where every one went.
        [Category("P0")]
        [Test]
        public void EveryCoinInOrOut_IsInTheLedger()
        {
            Wallet.Credit(100, CoinSource.Purchase);
            Wallet.TrySpend(30, CoinSink.ContinueHere, "1-04");

            string[] lines = CoinLedger.Lines();
            Assert.AreEqual(2, lines.Length);
            StringAssert.Contains("|100|Purchase|100", lines[0]);
            StringAssert.Contains("|-30|ContinueHere:1-04|70", lines[1]);
        }

        [Test]
        public void TheLedger_KeepsOnlyTheNewest()
        {
            for (int i = 0; i < CoinLedger.Keep + 25; i++) Wallet.Credit(1, CoinSource.Play);

            string[] lines = CoinLedger.Lines();
            Assert.AreEqual(CoinLedger.Keep, lines.Length);
            StringAssert.EndsWith($"|{CoinLedger.Keep + 25}", lines[lines.Length - 1], "the newest entry was dropped instead of the oldest.");
        }

        [Test]
        public void TheLedger_TravelsWithTheBalance()
        {
            Assert.IsTrue(SaveScope.IsProgress(CoinLedger.Key));
            Assert.IsTrue(SaveScope.IsProgress(SaveKeys.Coins));
        }

        // =====================================================================================
        // The parental gate
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void TheGate_OpensOnlyForTheRightAnswer_AndANewQuestionEachTry()
        {
            ParentalGate.Question q = ParentalGate.Ask();
            Assert.IsFalse(ParentalGate.TryAnswer(q.Answer + 1));
            Assert.IsFalse(ParentalGate.IsOpen);

            // The right answer to the old question no longer opens it: a wrong try used it up.
            Assert.IsFalse(ParentalGate.TryAnswer(q.Answer), "a child could guess through answers to one question.");

            q = ParentalGate.Ask();
            Assert.IsTrue(ParentalGate.TryAnswer(q.Answer));
            Assert.IsTrue(ParentalGate.IsOpen);
        }

        [Test]
        public void TheGate_IsNotOpenWithoutBeingAsked()
        {
            Assert.IsFalse(ParentalGate.TryAnswer(42));
            Assert.IsFalse(ParentalGate.IsOpen);
        }

        [Category("P0")]
        [Test]
        public void TheGate_ClosesByItself()
        {
            float now = 100f;
            ParentalGate.Clock = () => now;
            ParentalGate.Question q = ParentalGate.Ask();
            ParentalGate.TryAnswer(q.Answer);

            now += ParentalGate.OpenSeconds - 1f;
            Assert.IsTrue(ParentalGate.IsOpen);
            now += 2f;
            Assert.IsFalse(ParentalGate.IsOpen, "a grown-up's answer left the store open for whoever picked the phone up next.");
        }

        [Test]
        public void TheQuestion_IsTooHardForAYoungChild()
        {
            for (int i = 0; i < 50; i++)
            {
                ParentalGate.Question q = ParentalGate.Ask();
                Assert.That(q.A, Is.InRange(6, 9));
                Assert.That(q.B, Is.InRange(6, 9));
            }
        }

        // =====================================================================================
        // Families ads
        // =====================================================================================

        // Shadow Escape is 13+ and sends false for all of these. Zakzoqa is a children's game: sending
        // false would serve personalised and unrated ads to children, a Families policy violation.
        [Category("P0")]
        [Test]
        public void EveryAdRequest_IsForAChild_AndRatedG()
        {
            Assert.IsTrue(AdRequestRules.ChildDirected, "ad requests are not tagged child-directed.");
            Assert.IsTrue(AdRequestRules.UnderAgeOfConsent, "players are not tagged under the age of consent.");
            Assert.AreEqual("G", AdRequestRules.MaxContentRating, "ads above G may be served to children.");
            Assert.IsTrue(ConsentManager.TagForUnderAgeOfConsent, "the consent form is told the players are adults.");
        }
    }
}
