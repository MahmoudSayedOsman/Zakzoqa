// EVERYTHING THAT TALKS TO THE GOOGLE MOBILE ADS PLUGIN, AND THE ONLY FILE THAT MAY.
//
// AdManager and ConsentManager know two interfaces, IAdProvider and IConsentProvider. This file is
// where the real implementations of both live — AdMob for ads, Google's UMP for consent — and
// where the choice between them and the stubs is made. Nothing else in the game names an SDK type,
// so replacing AdMob one day is this file and nothing more.
//
// DEVICE ONLY. The real providers compile for an Android player and nowhere else. The Editor and
// every test keep the stubs, which is what makes the ad and consent flows testable without a
// network, an account or a phone — and what keeps a Play-mode session from ever loading a real ad.
//
// AND ONLY WITH THE PLUGIN INSTALLED. GOOGLE_MOBILE_ADS is set by the Runtime asmdef's version
// define when the com.google.ads.mobile package is present. Without it a device build still
// compiles and simply runs on the stubs, rather than an APK build failing on a missing namespace.
#if UNITY_ANDROID && !UNITY_EDITOR && GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zakzoqa
{
    public static class AdPlatform
    {
        public static IConsentProvider CreateConsentProvider()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && GOOGLE_MOBILE_ADS
            return new UmpConsentProvider();
#else
            return new StubConsentProvider();
#endif
        }

        public static IAdProvider CreateAdProvider()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && GOOGLE_MOBILE_ADS
            return new AdMobAdProvider();
#else
            return new StubAdProvider();
#endif
        }
    }

    // What every ad request says about its audience (Decisions §6, Families). Plain values here, so
    // FamiliesAdsTests can check them in the editor; the device half below turns them into AdMob's
    // RequestConfiguration.
    public static partial class AdRequestRules
    {
        public const bool ChildDirected = ConsentManager.TagForChildDirectedTreatment;
        public const bool UnderAgeOfConsent = ConsentManager.TagForUnderAgeOfConsent;
        // AdMob's scale is G, PG, T, MA. G is "suitable for all audiences, including families and children".
        public const string MaxContentRating = "G";
    }

    // Which ad units to ask for.
    //
    // TEST UNITS IN EVERY DEBUG BUILD, always. Google bans AdMob accounts over invalid traffic, and the
    // most common source of it is a developer tapping their own live ads while testing. A development
    // build is always on test units, so a phone in your hand only
    // ever sees Google's sample ads — labelled "Test Ad" — however many times the revive is tested.
    //
    // The two test ids are Google's published Android samples; every rewarded placement shares the
    // one test unit. The live ones come from AdMob → Apps → Zakzoqa → Ad units, one per
    // placement, so AdMob reports each placement's earnings on its own. Until all of them are filled
    // in, even a release build stays on test units — a release that serves test ads earns nothing,
    // which is a far cheaper mistake than the alternative.
    public static class AdMobIds
    {
        const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";
        const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";

        // Zakzoqa's own units, made in the same AdMob account as Shadow Escape's; empty until then.
        const string LiveRewardedRevive = "";
        const string LiveRewardedDoubleCoins = "";
        const string LiveInterstitial = "";

        public static bool UsingTestUnits =>
            Debug.isDebugBuild
            || string.IsNullOrEmpty(LiveRewardedRevive)
            || string.IsNullOrEmpty(LiveRewardedDoubleCoins)
            || string.IsNullOrEmpty(LiveInterstitial);

        // An unknown placement gets the revive unit rather than nothing: a new placement added to
        // AdManager should still show ads, just reported under the wrong name until it has a unit.
        public static string RewardedFor(string placement)
        {
            if (UsingTestUnits) return TestRewarded;
            return placement == AdManager.PlacementDoubleReward ? LiveRewardedDoubleCoins : LiveRewardedRevive;
        }

        public static string Interstitial => UsingTestUnits ? TestInterstitial : LiveInterstitial;
    }

#if UNITY_ANDROID && !UNITY_EDITOR && GOOGLE_MOBILE_ADS
    public static partial class AdRequestRules
    {
        public static RequestConfiguration Configuration() => new RequestConfiguration
        {
            TagForChildDirectedTreatment = ChildDirected ? TagForChildDirectedTreatment.True : TagForChildDirectedTreatment.False,
            TagForUnderAgeOfConsent = UnderAgeOfConsent ? TagForUnderAgeOfConsent.True : TagForUnderAgeOfConsent.False,
            MaxAdContentRating = MaxAdContentRating.G,
        };
    }

    // Runs SDK callbacks on Unity's main thread.
    //
    // Google's callbacks can arrive on a Java thread, and almost nothing in Unity may be touched from
    // one — a revive that sets the player's health from the wrong thread fails in ways that do not
    // reproduce. MobileAds.RaiseAdEventsOnUnityMainThread covers the ad events; nothing equivalent is
    // documented for UMP, so every callback in this file comes through here and the question never has
    // to be asked per call. A callback that was already on the main thread just runs one frame later.
    //
    // Update runs whatever Time.timeScale is, which matters: the victory and losing screens both sit at
    // a timescale of 0 while their ad is up.
    class AdMainThread : MonoBehaviour
    {
        static AdMainThread instance;
        static readonly List<Action> pending = new List<Action>();
        readonly List<Action> running = new List<Action>();

        // Called from the providers' constructors, which run on the main thread (in Awake).
        public static void Install()
        {
            if (instance != null) return;
            GameObject go = new GameObject("AdMainThread");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AdMainThread>();
        }

        public static void Post(Action action)
        {
            if (action == null) return;
            lock (pending) pending.Add(action);
        }

        void Update()
        {
            lock (pending)
            {
                if (pending.Count == 0) return;
                running.AddRange(pending);
                pending.Clear();
            }

            foreach (Action action in running)
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            running.Clear();
        }
    }

    // Google's User Messaging Platform behind IConsentProvider.
    //
    // The form's words and choices are not here. They are written in AdMob → Privacy & messaging, and
    // UMP downloads them — which is why a missing message there shows up as a gather error in the log
    // rather than as anything in this file.
    class UmpConsentProvider : IConsentProvider
    {
        // Phones that should be shown the European form while testing from outside Europe, in debug
        // builds only. UMP prints a phone's hashed id to logcat on the first Gather — a line containing
        // "addTestDeviceHashedId" — and it goes here. Empty means debug builds behave like release ones:
        // a phone in Egypt is simply not asked.
        static readonly List<string> TestDeviceHashedIds = new List<string>();

        public UmpConsentProvider() => AdMainThread.Install();

        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void Gather(bool tagForUnderAgeOfConsent, Action<string> onDone)
        {
            ConsentRequestParameters request = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = tagForUnderAgeOfConsent,
            };

            if (Debug.isDebugBuild && TestDeviceHashedIds.Count > 0)
            {
                request.ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = DebugGeography.EEA,
                    TestDeviceHashedIds = TestDeviceHashedIds,
                };
            }

            ConsentInformation.Update(request, updateError => AdMainThread.Post(() =>
            {
                if (updateError != null) { onDone?.Invoke(updateError.Message); return; }

                // Shows nothing where nothing is required, and nothing to a player who already answered.
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => AdMainThread.Post(() =>
                    onDone?.Invoke(formError != null ? formError.Message : null)));
            }));
        }

        public void ShowPrivacyOptions(Action<string> onDone)
        {
            ConsentForm.ShowPrivacyOptionsForm(formError => AdMainThread.Post(() =>
                onDone?.Invoke(formError != null ? formError.Message : null)));
        }
    }

    // AdMob behind IAdProvider: one rewarded ad per rewarded placement and one interstitial, each
    // loaded ahead of time so it is ready the moment a screen offers it.
    //
    // NOTHING IS INITIALISED OR LOADED BEFORE CONSENT ALLOWS IT. The SDK starts only once
    // ConsentManager.CanRequestAds is true — at construction if a previous session already answered,
    // otherwise on ConsentManager.Changed once this session's answer arrives. AdManager also refuses
    // to show without consent; this is the second half of the same rule, for loading.
    class AdMobAdProvider : IAdProvider
    {
        // After a failed load, how long before trying again. A phone with no network would otherwise
        // retry on every frame something asks whether an ad is ready.
        const float RetryDelay = 30f;

        bool initStarted;
        bool initialized;

        InterstitialAd interstitial;
        bool loadingInterstitial;
        float nextInterstitialAttempt;

        public AdMobAdProvider()
        {
            AdMainThread.Install();
            ConsentManager.Changed += InitializeIfAllowed;
            InitializeIfAllowed();
        }

        void InitializeIfAllowed()
        {
            if (initStarted || !ConsentManager.CanRequestAds) return;
            initStarted = true;

            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            // FAMILIES (see ConsentManager): every request is for a child, under the age of consent,
            // and for content rated G. This is what makes AdMob serve only ads that Google's
            // Families policy allows, and never personalised ones.
            MobileAds.SetRequestConfiguration(AdRequestRules.Configuration());

            MobileAds.Initialize(_ => AdMainThread.Post(() =>
            {
                initialized = true;
                Debug.Log($"[Ads] AdMob ready ({(AdMobIds.UsingTestUnits ? "TEST units" : "live units")}).");
                LoadAllRewarded();
                LoadInterstitial();
            }));
        }

        // --- rewarded ------------------------------------------------------------------------------

        // One preloaded ad per rewarded placement, because each placement has its own unit — so AdMob
        // reports what the revive earns apart from what the doubled coins earn.
        class RewardedSlot
        {
            public readonly string Placement;
            public RewardedAd Ad;
            public bool Loading;
            public float NextAttempt;

            public RewardedSlot(string placement) { Placement = placement; }
        }

        readonly Dictionary<string, RewardedSlot> rewardedSlots = new Dictionary<string, RewardedSlot>();

        RewardedSlot SlotFor(string placement)
        {
            if (!rewardedSlots.TryGetValue(placement, out RewardedSlot slot))
            {
                slot = new RewardedSlot(placement);
                rewardedSlots[placement] = slot;
            }
            return slot;
        }

        void LoadAllRewarded()
        {
            LoadRewarded(SlotFor(AdManager.PlacementRevive));
            LoadRewarded(SlotFor(AdManager.PlacementDoubleReward));
        }

        // Asking is also what retries: a screen that finds no ad ready starts a load, so the next offer
        // has one.
        public bool IsRewardedReady(string placement)
        {
            RewardedSlot slot = SlotFor(placement);
            if (slot.Ad != null && slot.Ad.CanShowAd()) return true;
            LoadRewarded(slot);
            return false;
        }

        void LoadRewarded(RewardedSlot slot)
        {
            if (!initialized || slot.Loading || slot.Ad != null) return;
            if (Time.realtimeSinceStartup < slot.NextAttempt) return;
            slot.Loading = true;

            RewardedAd.Load(AdMobIds.RewardedFor(slot.Placement), new AdRequest(), (ad, error) => AdMainThread.Post(() =>
            {
                slot.Loading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads] rewarded '{slot.Placement}' failed to load: {error}");
                    slot.NextAttempt = Time.realtimeSinceStartup + RetryDelay;
                    return;
                }
                slot.Ad = ad;
            }));
        }

        // onFinished(true) only if the reward was earned — the revive and the doubled coins hang off it.
        // Closing early, failing to show and "no ad loaded" are all false.
        public void ShowRewarded(string placement, Action<bool> onFinished)
        {
            RewardedSlot slot = SlotFor(placement);
            RewardedAd ad = slot.Ad;
            if (ad == null || !ad.CanShowAd())
            {
                onFinished?.Invoke(false);
                LoadRewarded(slot);
                return;
            }

            // One show per load. Cleared before showing, so nothing can offer this ad twice.
            slot.Ad = null;

            bool earned = false;
            bool finished = false;
            void Finish(bool ok)
            {
                if (finished) return;
                finished = true;
                ad.Destroy();
                onFinished?.Invoke(ok);
                LoadRewarded(slot);
            }

            // The reward arrives before the close; both are queued in order, so Finish reads it.
            ad.OnAdFullScreenContentClosed += () => AdMainThread.Post(() => Finish(earned));
            ad.OnAdFullScreenContentFailed += _ => AdMainThread.Post(() => Finish(false));
            ad.Show(_ => AdMainThread.Post(() => earned = true));
        }

        // --- interstitial --------------------------------------------------------------------------

        void LoadInterstitial()
        {
            if (!initialized || loadingInterstitial || interstitial != null) return;
            if (Time.realtimeSinceStartup < nextInterstitialAttempt) return;
            loadingInterstitial = true;

            InterstitialAd.Load(AdMobIds.Interstitial, new AdRequest(), (ad, error) => AdMainThread.Post(() =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads] interstitial failed to load: {error}");
                    nextInterstitialAttempt = Time.realtimeSinceStartup + RetryDelay;
                    return;
                }
                interstitial = ad;
            }));
        }

        // onClosed always fires, ad or no ad — it is the caller's only "now continue" path, and the
        // level-complete screen waits on it.
        public void ShowInterstitial(string placement, Action onClosed)
        {
            InterstitialAd ad = interstitial;
            if (ad == null || !ad.CanShowAd())
            {
                onClosed?.Invoke();
                LoadInterstitial();
                return;
            }

            interstitial = null;

            bool finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                ad.Destroy();
                onClosed?.Invoke();
                LoadInterstitial();
            }

            ad.OnAdFullScreenContentClosed += () => AdMainThread.Post(Finish);
            ad.OnAdFullScreenContentFailed += _ => AdMainThread.Post(Finish);
            ad.Show();
        }
    }
#endif
}
