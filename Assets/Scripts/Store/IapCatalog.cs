using System.Collections.Generic;
using UnityEngine;

namespace Zakzoqa
{
    public enum IapProductKind
    {
        // Bought again and again: the coin packs.
        Consumable,
        // Bought once, owned forever: Remove Ads, the coin doubler, skin packs. What "Restore
        // Purchases" restores.
        NonConsumable,
    }

    // ONE PRODUCT, AND WHAT IT GRANTS. (From Shadow Escape.)
    //
    // A product grants an entitlement, coins, or both, and NOTHING ELSE: there is deliberately no
    // field for stars, upgrades or transformations, so the catalog cannot sell power however it is
    // edited (Decisions §5). Adding one is a code change that StoreRulesTests will question.
    //
    // COINS ARE GRANTED ONLY FOR A PURCHASE MADE NOW — never when the store merely reports that the
    // player owns something (a restore, a reinstall, the launch-time ownership check).
    [System.Serializable]
    public class IapProductDef
    {
        // Must match the product id in the Play Console EXACTLY. In an asset rather than a const so
        // fixing a typo needs no code change.
        public string productId = "";
        public IapProductKind kind = IapProductKind.NonConsumable;
        // A localisation key.
        public string displayNameKey = "";
        // Fallback only. Real listings show the store's localized price, since prices differ per
        // country and change without a client update.
        public string fallbackPriceLabel = "";

        [Header("What buying it grants")]
        // An Entitlements id ("RemoveAds", "CoinDoubler", a skin pack's id), or empty.
        public string entitlement = "";
        public int coinGrant = 0;

        [Header("How it is offered")]
        // A short tag on the row: "Best value". A localisation key, or empty.
        public string badgeKey = "";
    }

    // The product list, as data. Separate from IapManager so the lineup can be edited without
    // touching purchase logic.
    [CreateAssetMenu(fileName = "IapCatalog", menuName = "Zakzoqa/IAP Catalog")]
    public class IapCatalog : ScriptableObject
    {
        public const string ResourcePath = "IapCatalog";

        public List<IapProductDef> products = new List<IapProductDef>();

        public IapProductDef Find(string productId)
        {
            foreach (IapProductDef p in products)
                if (p != null && p.productId == productId) return p;
            return null;
        }
    }
}
