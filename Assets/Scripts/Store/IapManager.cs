using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zakzoqa
{
    public enum PurchaseResult { Success, Cancelled, Failed, AlreadyOwned, Deferred, ParentalGateClosed }

    // What a real store SDK must implement. Gameplay and UI never touch it directly — they go
    // through IapManager, so swapping SDKs is a one-class change. (From Shadow Escape.)
    public interface IPurchaseProvider
    {
        void Purchase(string productId, Action<PurchaseResult> onComplete);
        // Required by Apple for non-consumables, and right on Android: a player reinstalling gets
        // what they bought back without paying again.
        void Restore(Action<bool> onComplete);
        // The store's localized price string ("US$2.99", "‏٩٫٩٩ ج.م"). Null until the store connects.
        string GetLocalizedPrice(string productId);

        // Whether the store says THIS player owns a product: known once the store has told us, after
        // connecting and after a restore. A restore that succeeds is not proof of ownership — it only
        // means the store answered — so what gets granted is decided per product, here.
        bool Owns(string productId);

        // Raised when the store reports a product owned with no purchase flow in front of it: a
        // reinstall, another device, a payment that cleared after the app closed.
        event Action<string> OwnershipFound;

        // Raised when the store has just listed EVERYTHING this account owns, so Owns() is complete:
        // what it does not list, the player does not own — refunded, or never bought.
        event Action OwnershipRefreshed;

        // Raised when a CONSUMABLE has been paid for with no purchase flow waiting on it — the app
        // closed between paying and being told, and the store delivered the order at the next
        // launch. The coins are owed. Never raised for a purchase whose own callback reports it.
        event Action<string> ConsumableDelivered;
    }

    // Stand-in for the editor and the tests. Succeeds instantly so purchase-dependent UI and grant
    // logic are testable without a store. It grants real entitlements — that is the point — so it
    // must never ship: IsUsingStubProvider says so on screen, and only a phone build gets the real one.
    public class StubPurchaseProvider : IPurchaseProvider
    {
        readonly HashSet<string> owned = new HashSet<string>();
        bool restored;

        public event Action<string> OwnershipFound { add { } remove { } }
        public event Action OwnershipRefreshed { add { } remove { } }
        public event Action<string> ConsumableDelivered { add { } remove { } }

        public void Purchase(string productId, Action<PurchaseResult> onComplete)
        {
            Debug.Log($"[IAP] (stub) purchased '{productId}'.");
            owned.Add(productId);
            onComplete?.Invoke(PurchaseResult.Success);
        }

        // Pretends the player owns everything once they restore: an account that bought it all.
        public void Restore(Action<bool> onComplete)
        {
            Debug.Log("[IAP] (stub) restore completed.");
            restored = true;
            onComplete?.Invoke(true);
        }

        public string GetLocalizedPrice(string productId) => null;

        public bool Owns(string productId) => restored || owned.Contains(productId);
    }

    // The single entry point for buying things. Owns the grant logic — what a product actually gives
    // the player — so it lives next to the catalog rather than being re-implemented per shop screen.
    // (From Shadow Escape, with the parental gate and entitlements by id added.)
    public class IapManager : MonoBehaviour
    {
        public static IapManager Instance { get; private set; }

        // Product ids, matching Resources/IapCatalog.asset and the Play Console.
        public const string ProductRemoveAds = "remove_ads";
        public const string ProductCoinDoubler = "coin_doubler";
        public const string ProductCoinsSmall = "coins_small";
        public const string ProductCoinsMedium = "coins_medium";
        public const string ProductCoinsLarge = "coins_large";

        public IapCatalog catalog;

        IPurchaseProvider provider;

        public bool IsAvailable => provider != null;

        // TRUE WHILE THE STAND-IN IS IN PLACE, and the store screen says so on screen: a build shipped
        // with it hands out everything for free.
        public bool IsUsingStubProvider => provider is StubPurchaseProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("IapManager");
            Instance = go.AddComponent<IapManager>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (catalog == null)
                catalog = Resources.Load<IapCatalog>(IapCatalog.ResourcePath);

            SetProvider(CreatePlatformProvider(catalog));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (provider == null) return;
            provider.OwnershipFound -= OnOwnershipFound;
            provider.OwnershipRefreshed -= OnOwnershipRefreshed;
            provider.ConsumableDelivered -= OnConsumableDelivered;
        }

        // The real store on a phone build; the stand-in everywhere else — the editor and the tests
        // above all, which must never open a real billing dialog.
        static IPurchaseProvider CreatePlatformProvider(IapCatalog catalog)
        {
#if UNITY_PURCHASING && UNITY_ANDROID && !UNITY_EDITOR
            if (catalog != null) return new UnityIapProvider(catalog.products);
#endif
            return new StubPurchaseProvider();
        }

        // A null provider never replaces a working one: a failed SDK start must not take the store down.
        public void SetProvider(IPurchaseProvider newProvider)
        {
            if (newProvider == null) return;
            if (provider != null)
            {
                provider.OwnershipFound -= OnOwnershipFound;
                provider.OwnershipRefreshed -= OnOwnershipRefreshed;
                provider.ConsumableDelivered -= OnConsumableDelivered;
            }
            provider = newProvider;
            provider.OwnershipFound += OnOwnershipFound;
            provider.OwnershipRefreshed += OnOwnershipRefreshed;
            provider.ConsumableDelivered += OnConsumableDelivered;
        }

        // The store found something this player owns: give its ENTITLEMENT back, not its coins — this
        // is a reinstall or another phone, and the coins were paid when it was bought.
        void OnOwnershipFound(string productId)
        {
            IapProductDef def = catalog != null ? catalog.Find(productId) : null;
            if (def != null && def.kind == IapProductKind.NonConsumable) Grant(def, fresh: false);
        }

        // A coin pack paid for while nothing was waiting (the app closed mid-purchase): owed, paid now.
        void OnConsumableDelivered(string productId)
        {
            IapProductDef def = catalog != null ? catalog.Find(productId) : null;
            if (def != null && def.kind == IapProductKind.Consumable) Grant(def, fresh: true);
        }

        // THE STORE IS THE TRUTH. It has just listed everything this account owns, so every
        // entitlement the catalog sells is made to agree with it: kept if ANY owned product grants
        // it, taken away if none does. The saved entitlement is only a cache for when the store
        // cannot be asked; a refunded purchase, or one put into the save by hand, lasts until the
        // next time the phone reaches the store, and no longer.
        //
        // Decided per entitlement over all products at once: with two products granting the same
        // thing, "not owned, revoke" for one must not undo "owned, grant" for the other.
        void OnOwnershipRefreshed()
        {
            if (catalog == null) return;

            Dictionary<string, bool> owns = new Dictionary<string, bool>();
            foreach (IapProductDef def in catalog.products)
            {
                if (def == null || def.kind != IapProductKind.NonConsumable || string.IsNullOrEmpty(def.entitlement)) continue;
                owns.TryGetValue(def.entitlement, out bool already);
                owns[def.entitlement] = already || provider.Owns(def.productId);
            }

            foreach (KeyValuePair<string, bool> e in owns)
            {
                bool has = Entitlements.Has(e.Key);
                if (e.Value && !has)
                {
                    Entitlements.Grant(e.Key);
                    Debug.Log($"[IAP] {e.Key} is owned according to the store; granted.");
                }
                else if (!e.Value && has)
                {
                    Entitlements.Revoke(e.Key);
                    Debug.Log($"[IAP] {e.Key} is not owned according to the store; taken back.");
                }
            }
        }

        // The store's own localized price when available, else the catalog's fallback. Never
        // hardcode a price in UI text.
        public string GetPriceLabel(string productId)
        {
            string live = provider != null ? provider.GetLocalizedPrice(productId) : null;
            if (!string.IsNullOrEmpty(live)) return live;

            IapProductDef def = catalog != null ? catalog.Find(productId) : null;
            return def != null ? def.fallbackPriceLabel : "";
        }

        // Whether a product should be offered to this player now: not an entitlement they hold.
        public bool IsOnSale(IapProductDef def)
        {
            if (def == null) return false;
            return !(def.kind == IapProductKind.NonConsumable && !string.IsNullOrEmpty(def.entitlement) && Entitlements.Has(def.entitlement));
        }

        // Real money. Refused before the store is asked unless a grown-up has just answered the
        // parental gate — and the answer is used up by this purchase, whatever the store says.
        public void Purchase(string productId, Action<PurchaseResult> onComplete)
        {
            if (provider == null) { onComplete?.Invoke(PurchaseResult.Failed); return; }

            IapProductDef def = catalog != null ? catalog.Find(productId) : null;
            if (def == null)
            {
                Debug.LogError($"[IAP] '{productId}' is not in the catalog — purchase refused. Add it to the IapCatalog asset AND to the Play Console.");
                onComplete?.Invoke(PurchaseResult.Failed);
                return;
            }

            if (!IsOnSale(def))
            {
                onComplete?.Invoke(PurchaseResult.AlreadyOwned);
                return;
            }

            if (!ParentalGate.IsOpen)
            {
                onComplete?.Invoke(PurchaseResult.ParentalGateClosed);
                return;
            }
            ParentalGate.Close();

            provider.Purchase(productId, result =>
            {
                // Granted only on a confirmed success, never optimistically. "Already owned" from the
                // STORE gives the entitlement back (bought on another install) but never coins again.
                if (result == PurchaseResult.Success) Grant(def, fresh: true);
                else if (result == PurchaseResult.AlreadyOwned) Grant(def, fresh: false);
                onComplete?.Invoke(result);
            });
        }

        // Gives back the non-consumables the store says this account owns: their entitlements, never
        // their coins. No gate: nothing is charged.
        public void RestorePurchases(Action<bool> onComplete)
        {
            if (provider == null) { onComplete?.Invoke(false); return; }

            provider.Restore(success =>
            {
                // ONLY WHAT THE STORE SAYS THIS PLAYER OWNS. A restore "succeeds" on Google Play for a
                // player who never bought anything; granting every non-consumable on success would be
                // a free Remove Ads button (Shadow Escape found this before it shipped).
                if (success && catalog != null)
                {
                    foreach (IapProductDef def in catalog.products)
                    {
                        if (def == null || def.kind != IapProductKind.NonConsumable) continue;
                        if (provider.Owns(def.productId)) Grant(def, fresh: false);
                    }
                }
                onComplete?.Invoke(success);
            });
        }

        // What a product gives. `fresh` is a purchase made now; only that pays coins.
        void Grant(IapProductDef def, bool fresh)
        {
            if (!string.IsNullOrEmpty(def.entitlement) && !Entitlements.Has(def.entitlement))
            {
                Entitlements.Grant(def.entitlement);
                Debug.Log($"[IAP] granted {def.entitlement} from {def.productId}.");
            }

            if (fresh && def.coinGrant > 0)
            {
                Wallet.Credit(def.coinGrant, CoinSource.Purchase);
                Debug.Log($"[IAP] {def.productId}: +{def.coinGrant} coins.");
            }
        }
    }
}
