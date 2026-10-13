using System;
using UnityEngine;

namespace Zakzoqa
{
    // The contract gameplay code sees. A real network (Unity Ads, AdMob, ironSource) is dropped in by
    // writing ONE class against this — no game code changes, because nothing outside this file's
    // implementations ever references an ad SDK.
    public interface IAdProvider
    {
        bool IsRewardedReady(string placement);
        // onFinished(true) only when the ad was watched to completion. Anything else — skipped,
        // failed to load, no fill — must report false, because the reward hangs off it.
        void ShowRewarded(string placement, Action<bool> onFinished);
        void ShowInterstitial(string placement, Action onClosed);
    }

    // The Editor's and the tests' ad network; devices use AdMob (see AdPlatform). Deliberately succeeds
    // instantly so the surrounding game flow (revive, double rewards, between-level breaks) is testable
    // without a network.
    public class StubAdProvider : IAdProvider
    {
        public bool IsRewardedReady(string placement) => true;

        public void ShowRewarded(string placement, Action<bool> onFinished)
        {
            Debug.Log($"[Ads] (stub) rewarded '{placement}' watched.");
            onFinished?.Invoke(true);
        }

        public void ShowInterstitial(string placement, Action onClosed)
        {
            Debug.Log($"[Ads] (stub) interstitial '{placement}' shown.");
            onClosed?.Invoke();
        }
    }

    // Single funnel for every ad in the game. (From Shadow Escape.) Owns the policy — how often
    // interstitials may appear, and that Remove Ads suppresses them — so those rules live in one
    // auditable place instead of being re-decided at each call site.
    //
    // Self-creating via RuntimeInitializeOnLoadMethod rather than placed per scene: this is one of the
    // few things that must survive scene loads (an ad callback can outlive the scene that asked for
    // it), and requiring it in every scene would guarantee it goes missing from one of them.
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        // Placement ids (Decisions §6: "carry on from here" and "double the level's coins", both
        // opt-in, plus the interstitial between levels). Constants, so the strings that must match
        // a dashboard config exist in exactly one place.
        public const string PlacementRevive = "revive";
        public const string PlacementDoubleReward = "double_reward";
        public const string PlacementBetweenLevels = "between_levels";

        [Header("Interstitial policy")]
        // Never on the first few completions: early sessions decide whether a player stays at all,
        // and an ad before the game has proven itself is the most expensive one you can show.
        public int minLevelsBeforeFirstInterstitial = 3;
        // Then at most one every N level completions.
        public int interstitialEveryNLevels = 3;
        // Hard floor between interstitials regardless of level count, so a fast replay streak can't
        // stack them back to back.
        public float minSecondsBetweenInterstitials = 120f;

        IAdProvider provider;
        int levelCompletions;
        float lastInterstitialTime = -9999f;

        // THE COUNT OUTLIVES THE SESSION (learned in Shadow Escape). Kept only on this
        // object, it would go back to zero whenever the app closed: a player who plays three levels a
        // session would sit inside the "first 3 levels" grace window every time and almost never see an ad,
        // which is not the intent. Saved, so the cadence is every N levels of play, wherever the
        // sessions fall. The two-minute floor still holds within a session.
        public const string LevelCountKey = SaveKeys.Game + "Ads_LevelCount";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("AdManager");
            Instance = go.AddComponent<AdManager>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            levelCompletions = Prefs.GetInt(LevelCountKey, 0);
            // AdMob on an Android device, the stub everywhere else. See AdPlatform.
            provider = AdPlatform.CreateAdProvider();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // For tests. A shipping build gets its provider from AdPlatform in Awake.
        public void SetProvider(IAdProvider newProvider)
        {
            if (newProvider != null) provider = newProvider;
        }

        // CONSENT COMES FIRST, for every ad this class can show. Asked here rather than by each caller
        // because the whole failure mode of consent is one call site that forgot to ask — the revive
        // screen, the double-coins button and the interstitial all come through this file, so this is
        // the one place that has to remember. See ConsentManager.
        //
        // A rewarded offer that consent does not allow yet is simply not ready: the revive and double-
        // coins buttons already hide themselves for "no ad loaded", and this is the same answer.
        public bool IsRewardedReady(string placement) =>
            ConsentManager.CanRequestAds && provider != null && provider.IsRewardedReady(placement);

        // Rewarded ads are always allowed, even with Remove Ads: the player is opting in for a benefit,
        // and taking that option away from a paying customer is a downgrade, not a perk.
        public void ShowRewarded(string placement, Action<bool> onFinished)
        {
            if (provider == null || !ConsentManager.CanRequestAds) { onFinished?.Invoke(false); return; }
            provider.ShowRewarded(placement, onFinished);
        }

        // Call on every level completion. Decides internally whether an ad is actually due, so callers
        // never implement their own frequency logic.
        public void NotifyLevelCompleted()
        {
            levelCompletions++;
            Prefs.SetInt(LevelCountKey, levelCompletions);
        }

        public bool ShouldShowInterstitial()
        {
            if (Entitlements.Has(Entitlements.RemoveAds)) return false;
            if (!ConsentManager.CanRequestAds) return false;
            int firstAfter = minLevelsBeforeFirstInterstitial;
            float minSeconds = minSecondsBetweenInterstitials;
            int every = interstitialEveryNLevels;

            if (levelCompletions < firstAfter) return false;
            if (Time.unscaledTime - lastInterstitialTime < minSeconds) return false;
            if (every <= 0) return false;
            return levelCompletions % every == 0;
        }

        // onClosed always fires — including when no ad is shown — so callers can use it as their single
        // "now continue" path instead of branching on whether an ad appeared.
        public void ShowInterstitialIfDue(string placement, Action onClosed)
        {
            if (!ShouldShowInterstitial() || provider == null)
            {
                onClosed?.Invoke();
                return;
            }

            lastInterstitialTime = Time.unscaledTime;
            provider.ShowInterstitial(placement, onClosed);
        }
    }
}
