using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Zakzoqa.Tests
{
    // The two systems that touch real money. (From Shadow Escape's MonetizationTests, which caught a
    // free Remove Ads button before it shipped.) Both run on stubs that succeed instantly, so these
    // drive them through fake providers that can fail, cancel and refuse on demand.
    //
    // A grant is money. A grant without a purchase is revenue lost; a purchase without a grant is a
    // refund request and a one-star review. And in a children's game, a purchase without a grown-up
    // is a complaint to the store.
    public class MonetizationTests
    {
        SaveSandbox sandbox;
        IapManager iap;
        AdManager ads;
        FakeConsent consent;
        readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            sandbox = SaveSandbox.Capture();
            Prefs.SetInt(SaveKeys.Coins, 0);
            Prefs.SetInt(AdManager.LevelCountKey, 0);
            foreach (string id in Entitlements.Owned()) Entitlements.Revoke(id);
            ParentalGate.ResetForTests();

            // The managers create themselves before the first scene and live on. The tests spawn their
            // own, so the self-made ones go first or they would stay Instance.
            if (IapManager.Instance != null) UnityEngine.Object.DestroyImmediate(IapManager.Instance.gameObject);
            if (AdManager.Instance != null) UnityEngine.Object.DestroyImmediate(AdManager.Instance.gameObject);

            // Every ad asks ConsentManager first. Installed explicitly, allowing, so the ad tests test
            // ad policy and not whatever the launch-time answer was.
            consent = new FakeConsent();
            Consent().SetProvider(consent);
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (GameObject go in spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            spawned.Clear();
            if (ConsentManager.Instance != null) ConsentManager.Instance.SetProvider(new StubConsentProvider());
            ParentalGate.ResetForTests();
            sandbox.Restore();
            yield return null;
        }

        static ConsentManager Consent()
        {
            if (ConsentManager.Instance == null)
            {
                GameObject go = new GameObject("ConsentManager");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<ConsentManager>();
            }
            return ConsentManager.Instance;
        }

        // --- fixture -------------------------------------------------------------------------

        class FakeStore : IPurchaseProvider
        {
            public PurchaseResult Answer = PurchaseResult.Success;
            public bool RestoreSucceeds = true;
            public string Price;

            public readonly List<string> Purchased = new List<string>();
            public int RestoreCalls;

            public void Purchase(string productId, Action<PurchaseResult> onComplete)
            {
                Purchased.Add(productId);
                onComplete?.Invoke(Answer);
            }

            public void Restore(Action<bool> onComplete)
            {
                RestoreCalls++;
                onComplete?.Invoke(RestoreSucceeds);
            }

            public string GetLocalizedPrice(string productId) => Price;

            public readonly HashSet<string> Owned = new HashSet<string>();
            public bool Owns(string productId) => Owned.Contains(productId);

            public event Action<string> OwnershipFound;
            public void Find(string productId)
            {
                Owned.Add(productId);
                OwnershipFound?.Invoke(productId);
            }

            public event Action OwnershipRefreshed;
            public void Refresh() => OwnershipRefreshed?.Invoke();

            public event Action<string> ConsumableDelivered;
            public void Deliver(string productId) => ConsumableDelivered?.Invoke(productId);
        }

        class FakeConsent : IConsentProvider
        {
            public bool Allows = true;
            public string GatherError;
            public int GatherCalls;
            public bool? AskedUnderAge;
            public int OptionsShown;

            public bool CanRequestAds => Allows;
            public bool PrivacyOptionsRequired => true;

            public void Gather(bool tagForUnderAgeOfConsent, Action<string> onDone)
            {
                GatherCalls++;
                AskedUnderAge = tagForUnderAgeOfConsent;
                onDone?.Invoke(GatherError);
            }

            public void ShowPrivacyOptions(Action<string> onDone)
            {
                OptionsShown++;
                onDone?.Invoke(null);
            }
        }

        class FakeAds : IAdProvider
        {
            public bool Ready = true;
            public bool Watched = true;
            public readonly List<string> Rewarded = new List<string>();
            public readonly List<string> Interstitials = new List<string>();

            public bool IsRewardedReady(string placement) => Ready;

            public void ShowRewarded(string placement, Action<bool> onFinished)
            {
                Rewarded.Add(placement);
                onFinished?.Invoke(Watched);
            }

            public void ShowInterstitial(string placement, Action onClosed)
            {
                Interstitials.Add(placement);
                onClosed?.Invoke();
            }
        }

        // In memory, so these tests describe the shapes that matter rather than whatever the shipped
        // asset holds today; StoreRulesTests checks the shipped one.
        static IapCatalog Catalog()
        {
            IapCatalog catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.products = new List<IapProductDef>
            {
                new IapProductDef { productId = IapManager.ProductRemoveAds, kind = IapProductKind.NonConsumable, entitlement = Entitlements.RemoveAds, fallbackPriceLabel = "$2.99" },
                new IapProductDef { productId = IapManager.ProductCoinDoubler, kind = IapProductKind.NonConsumable, entitlement = Entitlements.CoinDoubler, fallbackPriceLabel = "$2.99" },
                new IapProductDef { productId = IapManager.ProductCoinsSmall, kind = IapProductKind.Consumable, coinGrant = 500, fallbackPriceLabel = "$0.99" },
            };
            return catalog;
        }

        FakeStore SpawnIap()
        {
            GameObject go = new GameObject("TestIapManager");
            spawned.Add(go);
            go.SetActive(false);
            iap = go.AddComponent<IapManager>();
            iap.catalog = Catalog();
            go.SetActive(true);            // Awake runs here, installing the stub

            FakeStore store = new FakeStore();
            iap.SetProvider(store);
            return store;
        }

        FakeAds SpawnAds()
        {
            GameObject go = new GameObject("TestAdManager");
            spawned.Add(go);
            go.SetActive(false);
            ads = go.AddComponent<AdManager>();
            go.SetActive(true);

            FakeAds fake = new FakeAds();
            ads.SetProvider(fake);
            return fake;
        }

        // What the grown-up does on the gate screen.
        static void ParentAnswers()
        {
            ParentalGate.Question q = ParentalGate.Ask();
            Assert.IsTrue(ParentalGate.TryAnswer(q.Answer));
        }

        static PurchaseResult Buy(IapManager manager, string productId)
        {
            ParentAnswers();
            PurchaseResult captured = PurchaseResult.Failed;
            manager.Purchase(productId, r => captured = r);
            return captured;
        }

        static bool HasRemoveAds => Entitlements.Has(Entitlements.RemoveAds);

        // =====================================================================================
        // The parental gate
        // =====================================================================================

        // THE ONE THAT MATTERS MOST IN A CHILDREN'S GAME. No grown-up, no store.
        [Category("P0")]
        [UnityTest]
        public IEnumerator WithoutAGrownUp_TheStoreIsNeverAsked()
        {
            FakeStore store = SpawnIap();
            yield return null;

            PurchaseResult got = PurchaseResult.Success;
            iap.Purchase(IapManager.ProductCoinsSmall, r => got = r);

            Assert.AreEqual(PurchaseResult.ParentalGateClosed, got);
            Assert.IsEmpty(store.Purchased, "a child reached the payment screen without the parental gate.");
            Assert.AreEqual(0, Wallet.Balance);
        }

        // One answer, one purchase: the gate does not stay open for a second tap.
        [Category("P0")]
        [UnityTest]
        public IEnumerator OneAnswer_AllowsOnePurchase()
        {
            FakeStore store = SpawnIap();
            yield return null;

            Assert.AreEqual(PurchaseResult.Success, Buy(iap, IapManager.ProductCoinsSmall));

            PurchaseResult second = PurchaseResult.Success;
            iap.Purchase(IapManager.ProductCoinsSmall, r => second = r);
            Assert.AreEqual(PurchaseResult.ParentalGateClosed, second, "a second purchase went through on the first answer.");
            Assert.AreEqual(1, store.Purchased.Count);
        }

        // Restoring charges nothing, so it needs no grown-up.
        [UnityTest]
        public IEnumerator Restore_NeedsNoGate()
        {
            FakeStore store = SpawnIap();
            yield return null;

            store.Owned.Add(IapManager.ProductRemoveAds);
            bool ok = false;
            iap.RestorePurchases(r => ok = r);

            Assert.IsTrue(ok);
            Assert.IsTrue(HasRemoveAds);
        }

        // =====================================================================================
        // Buying
        // =====================================================================================

        [Category("P0")]
        [UnityTest]
        public IEnumerator ASuccessfulPurchase_GrantsWhatItPromised()
        {
            SpawnIap();
            yield return null;

            Assert.AreEqual(PurchaseResult.Success, Buy(iap, IapManager.ProductRemoveAds));
            Assert.IsTrue(HasRemoveAds, "Remove Ads was bought and not granted.");

            Assert.AreEqual(PurchaseResult.Success, Buy(iap, IapManager.ProductCoinDoubler));
            Assert.IsTrue(Wallet.HasCoinDoubler, "the coin doubler was bought and not granted.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator ACancelledOrFailedPurchase_GrantsNothing()
        {
            FakeStore store = SpawnIap();
            yield return null;

            foreach (PurchaseResult answer in new[] { PurchaseResult.Cancelled, PurchaseResult.Failed })
            {
                store.Answer = answer;
                Assert.AreEqual(answer, Buy(iap, IapManager.ProductRemoveAds));
                Assert.IsFalse(HasRemoveAds, $"a {answer} purchase still granted Remove Ads.");
                Assert.AreEqual(answer, Buy(iap, IapManager.ProductCoinsSmall));
                Assert.AreEqual(0, Wallet.Balance, $"a {answer} purchase still paid coins.");
            }
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator ACoinPack_PaysItsCoins_AndTheLedgerSaysSo()
        {
            SpawnIap();
            yield return null;

            Assert.AreEqual(PurchaseResult.Success, Buy(iap, IapManager.ProductCoinsSmall));
            Assert.AreEqual(500, Wallet.Balance, "a bought coin pack paid nothing.");
            StringAssert.Contains("|500|Purchase|500", CoinLedger.Lines()[CoinLedger.Lines().Length - 1]);
        }

        // Coins bought are exactly what was paid for, doubler or not.
        [Category("P0")]
        [UnityTest]
        public IEnumerator ACoinPack_IsNeverDoubled()
        {
            SpawnIap();
            yield return null;
            Entitlements.Grant(Entitlements.CoinDoubler);

            Buy(iap, IapManager.ProductCoinsSmall);
            Assert.AreEqual(500, Wallet.Balance);
        }

        // The app closed between paying and being told: the coins are paid at the next launch.
        [Category("P0")]
        [UnityTest]
        public IEnumerator ACoinPackDeliveredLater_IsStillPaid()
        {
            FakeStore store = SpawnIap();
            yield return null;

            store.Deliver(IapManager.ProductCoinsSmall);
            Assert.AreEqual(500, Wallet.Balance, "a coin pack delivered after a restart paid nothing.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator ANonConsumableAlreadyOwned_IsNotChargedAgain()
        {
            FakeStore store = SpawnIap();
            yield return null;

            Buy(iap, IapManager.ProductRemoveAds);
            store.Purchased.Clear();

            Assert.AreEqual(PurchaseResult.AlreadyOwned, Buy(iap, IapManager.ProductRemoveAds));
            Assert.IsEmpty(store.Purchased, "the store was asked to charge for something the player already owns.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator AProductNotInTheCatalog_IsRefusedWithoutContactingTheStore()
        {
            FakeStore store = SpawnIap();
            yield return null;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not in the catalog"));
            Assert.AreEqual(PurchaseResult.Failed, Buy(iap, "coins_typo"));
            Assert.IsEmpty(store.Purchased, "an unknown product id reached the store.");
            Assert.AreEqual(0, Wallet.Balance);
        }

        [UnityTest]
        public IEnumerator ThePriceLabel_PrefersWhatTheStoreReports()
        {
            FakeStore store = SpawnIap();
            yield return null;

            Assert.AreEqual("$2.99", iap.GetPriceLabel(IapManager.ProductRemoveAds));
            store.Price = "٩٩ ج.م";
            Assert.AreEqual("٩٩ ج.م", iap.GetPriceLabel(IapManager.ProductRemoveAds),
                "the store reported a price and the UI ignored it.");
        }

        [UnityTest]
        public IEnumerator AStoreThatSaysAlreadyOwned_Grants_ButPaysNoCoins()
        {
            FakeStore store = SpawnIap();
            store.Answer = PurchaseResult.AlreadyOwned;
            yield return null;

            Buy(iap, IapManager.ProductRemoveAds);
            Assert.IsTrue(HasRemoveAds, "the store said the player owns it and the game did not grant it.");
        }

        [UnityTest]
        public IEnumerator ADeferredPayment_GrantsNothingYet()
        {
            FakeStore store = SpawnIap();
            store.Answer = PurchaseResult.Deferred;
            yield return null;

            Assert.AreEqual(PurchaseResult.Deferred, Buy(iap, IapManager.ProductRemoveAds));
            Assert.IsFalse(HasRemoveAds, "a payment that has not cleared unlocked Remove Ads.");
        }

        // =====================================================================================
        // Restoring, and the store as the truth
        // =====================================================================================

        [Category("P0")]
        [UnityTest]
        public IEnumerator ASuccessfulRestore_GivesBackWhatTheStoreSaysIsOwned()
        {
            FakeStore store = SpawnIap();
            yield return null;

            store.Owned.Add(IapManager.ProductRemoveAds);
            store.Owned.Add(IapManager.ProductCoinDoubler);
            bool reported = false;
            iap.RestorePurchases(ok => reported = ok);

            Assert.AreEqual(1, store.RestoreCalls);
            Assert.IsTrue(reported);
            Assert.IsTrue(HasRemoveAds, "the restore did not give Remove Ads back.");
            Assert.IsTrue(Wallet.HasCoinDoubler, "the restore did not give the coin doubler back.");
            Assert.AreEqual(0, Wallet.Balance, "a restore paid coins.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator AFailedRestore_GrantsNothing()
        {
            FakeStore store = SpawnIap();
            store.RestoreSucceeds = false;
            store.Owned.Add(IapManager.ProductRemoveAds);
            yield return null;

            bool reported = true;
            iap.RestorePurchases(ok => reported = ok);

            Assert.IsFalse(reported);
            Assert.IsFalse(HasRemoveAds);
        }

        // THE BUG A REAL STORE WOULD HAVE EXPOSED (Shadow Escape): "succeeded" only means the store
        // answered. A player who bought nothing gets nothing.
        [Category("P0")]
        [UnityTest]
        public IEnumerator ASuccessfulRestore_WithNothingOwned_GrantsNothing()
        {
            SpawnIap();
            yield return null;

            bool reported = false;
            iap.RestorePurchases(ok => reported = ok);

            Assert.IsTrue(reported);
            Assert.IsFalse(HasRemoveAds, "a restore gave Remove Ads to a player who never bought it.");
            Assert.IsFalse(Wallet.HasCoinDoubler, "a restore gave the coin doubler to a player who never bought it.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator OwnershipTheStoreFindsOnItsOwn_IsGranted()
        {
            FakeStore store = SpawnIap();
            yield return null;

            store.Find(IapManager.ProductCoinDoubler);
            Assert.IsTrue(Wallet.HasCoinDoubler, "a reinstall did not get the coin doubler back.");
        }

        // Refunded, or put into the save by hand: gone the moment the store is asked.
        [Category("P0")]
        [UnityTest]
        public IEnumerator EntitlementsTheStoreDoesNotList_AreTakenBack()
        {
            FakeStore store = SpawnIap();
            yield return null;

            Entitlements.Grant(Entitlements.RemoveAds);
            Entitlements.Grant(Entitlements.CoinDoubler);
            store.Owned.Add(IapManager.ProductRemoveAds);
            store.Refresh();

            Assert.IsTrue(HasRemoveAds, "something the store lists was taken away.");
            Assert.IsFalse(Wallet.HasCoinDoubler, "the store did not list the coin doubler and the game kept it.");
        }

        // An entitlement no product sells (a promo, a future skin pack) is not the store's to take.
        [UnityTest]
        public IEnumerator AnEntitlementTheCatalogDoesNotSell_IsLeftAlone()
        {
            FakeStore store = SpawnIap();
            yield return null;

            Entitlements.Grant("SkinPack_Promo");
            store.Refresh();
            Assert.IsTrue(Entitlements.Has("SkinPack_Promo"));
        }

        [UnityTest]
        public IEnumerator SetProviderNull_KeepsTheWorkingOne()
        {
            SpawnIap();
            iap.SetProvider(null);
            yield return null;

            bool reported = false;
            iap.RestorePurchases(ok => reported = ok);
            Assert.IsTrue(iap.IsAvailable);
            Assert.IsTrue(reported);
        }

        // =====================================================================================
        // Ads
        // =====================================================================================

        [Category("P0")]
        [UnityTest]
        public IEnumerator ARewardedAdThatWasNotWatched_ReportsFailure()
        {
            FakeAds fake = SpawnAds();
            yield return null;

            fake.Watched = false;
            bool? result = null;
            ads.ShowRewarded(AdManager.PlacementRevive, ok => result = ok);

            Assert.AreEqual(false, result, "a skipped ad reported success — every reward behind one is now free.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator RemoveAds_SuppressesInterstitialsButNotRewardedAds()
        {
            SpawnAds();
            yield return null;

            for (int i = 0; i < 9; i++) ads.NotifyLevelCompleted();
            Assert.IsTrue(ads.ShouldShowInterstitial(), "precondition: one should be due by now.");

            Entitlements.Grant(Entitlements.RemoveAds);
            Assert.IsFalse(ads.ShouldShowInterstitial(), "a player who paid to remove ads is still being shown them.");

            bool? watched = null;
            ads.ShowRewarded(AdManager.PlacementDoubleReward, ok => watched = ok);
            Assert.AreEqual(true, watched, "Remove Ads also took away the opt-in reward.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator NoInterstitial_BeforeTheGraceLevels()
        {
            SpawnAds();
            yield return null;

            for (int i = 1; i < ads.minLevelsBeforeFirstInterstitial; i++)
            {
                ads.NotifyLevelCompleted();
                Assert.IsFalse(ads.ShouldShowInterstitial(), $"an interstitial is due after only {i} completions.");
            }
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator TwoInterstitials_CannotLandBackToBack()
        {
            FakeAds fake = SpawnAds();
            yield return null;

            for (int i = 0; i < 9; i++) ads.NotifyLevelCompleted();
            ads.ShowInterstitialIfDue(AdManager.PlacementBetweenLevels, () => { });
            for (int i = 0; i < 3; i++) ads.NotifyLevelCompleted();
            ads.ShowInterstitialIfDue(AdManager.PlacementBetweenLevels, () => { });

            Assert.AreEqual(1, fake.Interstitials.Count, "a second interstitial showed within the minimum gap.");
        }

        [Category("P0")]
        [UnityTest]
        public IEnumerator TheContinueCallback_FiresEvenWhenNoAdIsShown()
        {
            SpawnAds();
            yield return null;

            bool continued = false;
            ads.ShowInterstitialIfDue(AdManager.PlacementBetweenLevels, () => continued = true);
            Assert.IsTrue(continued, "the level-complete screen would wait forever.");
        }

        // =====================================================================================
        // Consent
        // =====================================================================================

        [Category("P0")]
        [UnityTest]
        public IEnumerator WithoutConsent_NoAdOfAnyKindReachesTheNetwork()
        {
            FakeAds fake = SpawnAds();
            yield return null;
            consent.Allows = false;

            Assert.IsFalse(ads.IsRewardedReady(AdManager.PlacementRevive));
            bool? watched = null;
            ads.ShowRewarded(AdManager.PlacementDoubleReward, ok => watched = ok);
            Assert.AreEqual(false, watched);
            Assert.IsEmpty(fake.Rewarded);

            for (int i = 0; i < 9; i++) ads.NotifyLevelCompleted();
            bool continued = false;
            ads.ShowInterstitialIfDue(AdManager.PlacementBetweenLevels, () => continued = true);
            Assert.IsEmpty(fake.Interstitials);
            Assert.IsTrue(continued, "consent must never be the price of playing.");
        }

        // The children's-game answer reaches the consent platform (the opposite of Shadow Escape's).
        [Category("P0")]
        [UnityTest]
        public IEnumerator Consent_IsGatheredForChildren()
        {
            yield return null;

            Consent().Gather();

            Assert.AreEqual(1, consent.GatherCalls);
            Assert.AreEqual(true, consent.AskedUnderAge,
                "players were tagged as adults in a children's game — a Families policy violation.");
            Assert.IsTrue(Consent().Gathered);
        }

        [UnityTest]
        public IEnumerator AFailedGather_StillAnswers()
        {
            yield return null;
            consent.GatherError = "no network";

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("gather failed"));
            bool changed = false;
            Action onChanged = () => changed = true;
            ConsentManager.Changed += onChanged;
            try { Consent().Gather(); }
            finally { ConsentManager.Changed -= onChanged; }

            Assert.IsTrue(changed);
            Assert.IsTrue(Consent().Gathered);
        }
    }
}
