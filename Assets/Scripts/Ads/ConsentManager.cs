using System;
using UnityEngine;

namespace Zakzoqa
{
    // What a real consent platform must implement. Shaped after Google's User Messaging Platform
    // (UMP), which ships inside the Google Mobile Ads plugin and is the one this game will use — so the
    // real provider is a thin wrapper and nothing above it changes when it lands.
    public interface IConsentProvider
    {
        // Refreshes the player's consent status and, where the law asks for it and it has not been
        // given yet, shows the consent form. onDone(null) on success, onDone(message) on failure.
        // Must fire exactly once either way — AdManager waits on the answer.
        void Gather(bool tagForUnderAgeOfConsent, Action<string> onDone);

        // True once ads may be requested: consent given, or not required where the player is. Can be
        // true before Gather finishes, from a previous session's answer — UMP behaves the same way.
        bool CanRequestAds { get; }

        // True where the player must be able to change their answer later. Google requires a visible
        // way back to the form in exactly that case, and it is the Settings button.
        bool PrivacyOptionsRequired { get; }

        // onDone fires when the form closes, or immediately with a message if it could not show.
        void ShowPrivacyOptions(Action<string> onDone);
    }

    // The Editor's and the tests' consent platform; devices use UMP (see AdPlatform). Behaves as a
    // player who already said yes: ads allowed, nothing shown. PrivacyOptionsRequired is TRUE on
    // purpose, so the Settings button exists in the Editor and its layout can be checked before a
    // real form is there to open.
    public class StubConsentProvider : IConsentProvider
    {
        public bool CanRequestAds => true;
        public bool PrivacyOptionsRequired => true;

        public void Gather(bool tagForUnderAgeOfConsent, Action<string> onDone)
        {
            Debug.Log($"[Consent] (stub) gathered — under age of consent: {tagForUnderAgeOfConsent}.");
            onDone?.Invoke(null);
        }

        public void ShowPrivacyOptions(Action<string> onDone)
        {
            // Shadow Escape also showed this on screen, because pressing the button in the Editor
            // looked like it did nothing. That waits for Zakzoqa's toast (the UI step).
            Debug.Log("[Consent] (stub) Google's privacy form opens here on a phone in the EU/UK. The Editor has no form.");
            onDone?.Invoke(null);
        }
    }

    // The one place that decides whether this game may ask for an ad.
    //
    // WHY IT EXISTS. Google has required a certified consent platform for ads served in the EEA and the
    // UK since January 2024; without one AdMob serves those players limited ads or none. The rule it
    // enforces is short — ask before any ad is requested, and let the player change their mind later —
    // and the way it gets broken is also short: one call site that requests an ad without asking. So
    // AdManager asks HERE before every ad, and no other file ever needs to know consent exists.
    //
    // NEVER A GATE ON PLAYING. A player who declines gets non-personalised ads, or none; the game never
    // waits on the form and never locks anything behind it. Consent that is the price of playing is
    // not consent, and the store policies say so too.
    //
    // Self-creating and surviving scene loads, like AdManager and for the same reason: the form is
    // asynchronous and can outlive the scene that opened it.
    public class ConsentManager : MonoBehaviour
    {
        public static ConsentManager Instance { get; private set; }

        // THE OPPOSITE OF SHADOW ESCAPE. That game is rated 13+ and sends false. Zakzoqa is made for
        // children and families (Decisions §1, row 6), so it falls under Google Play's Families
        // policy: every player is treated as under the age of consent and as a child, ads are never
        // personalised, and only content rated G is requested (AdMobAdProvider). Sending false here
        // would be a policy violation, and FamiliesAdsTests fails if it ever does.
        public const bool TagForUnderAgeOfConsent = true;
        public const bool TagForChildDirectedTreatment = true;

        // Fired whenever the answer may have changed: after Gather, and after the privacy form closes.
        // The Settings button and, later, the real ad provider's initialisation both listen.
        public static event Action Changed;

        IConsentProvider provider;
        bool gathering;

        // True once the first Gather has answered, successfully or not.
        public bool Gathered { get; private set; }

        // Fails CLOSED. With no manager there is nothing to vouch for consent, so no ad is requested.
        // In a real build the manager always exists — it creates itself before the first scene.
        public static bool CanRequestAds =>
            Instance != null && Instance.provider != null && Instance.provider.CanRequestAds;

        public static bool PrivacyOptionsRequired =>
            Instance != null && Instance.provider != null && Instance.provider.PrivacyOptionsRequired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("ConsentManager");
            Instance = go.AddComponent<ConsentManager>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Google's UMP on an Android device, the stub everywhere else. See AdPlatform.
            provider = AdPlatform.CreateConsentProvider();
        }

        // At launch, before any level can end and offer an ad. Start rather than Awake so the first
        // scene is up — the real form draws over it.
        void Start() => Gather();

        // For tests. A shipping build gets its provider from AdPlatform in Awake.
        public void SetProvider(IConsentProvider newProvider)
        {
            if (newProvider != null) provider = newProvider;
        }

        public void Gather()
        {
            if (gathering || provider == null) return;
            gathering = true;

            provider.Gather(TagForUnderAgeOfConsent, error =>
            {
                gathering = false;
                Gathered = true;

                // Logged, not surfaced. A failed update (no network, say) leaves CanRequestAds on
                // whatever the last session decided, which is what Google's own sample relies on —
                // and the player is not the one who can fix it.
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[Consent] gather failed: {error}");

                Changed?.Invoke();
            });
        }

        // For the Settings button. onClosed always fires, so the caller can refresh unconditionally.
        public static void ShowPrivacyOptions(Action onClosed)
        {
            if (Instance == null || Instance.provider == null) { onClosed?.Invoke(); return; }

            Instance.provider.ShowPrivacyOptions(error =>
            {
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[Consent] privacy options failed: {error}");
                Changed?.Invoke();
                onClosed?.Invoke();
            });
        }
    }
}
