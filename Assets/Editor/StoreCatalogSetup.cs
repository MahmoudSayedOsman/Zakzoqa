using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zakzoqa.EditorTools
{
    // Creates Resources/IapCatalog.asset with the lineup in Decisions §6, ONLY IF IT DOES NOT EXIST.
    // After that the asset is the truth and is edited in the Inspector: Shadow Escape's scene
    // builders overwrote hand edits every time they ran, and this one never will.
    //
    // The prices are fallbacks shown until the store answers; the real ones are set in the Play
    // Console, per country.
    public static class StoreCatalogSetup
    {
        public const string AssetPath = "Assets/Resources/IapCatalog.asset";

        [MenuItem("Zakzoqa/Create Store Catalog")]
        public static void CreateIfMissing()
        {
            if (File.Exists(AssetPath))
            {
                Debug.Log("Zakzoqa: the store catalog already exists; left as it is.");
                return;
            }

            IapCatalog catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.products = new List<IapProductDef>
            {
                Coins(IapManager.ProductCoinsSmall, 500, "$0.99", ""),
                Coins(IapManager.ProductCoinsMedium, 1200, "$1.99", ""),
                Coins(IapManager.ProductCoinsLarge, 3000, "$4.99", "store.badge.best_value"),
                Owned(IapManager.ProductCoinDoubler, Entitlements.CoinDoubler, "$2.99"),
                Owned(IapManager.ProductRemoveAds, Entitlements.RemoveAds, "$2.99"),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            AssetDatabase.CreateAsset(catalog, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Zakzoqa: store catalog created at " + AssetPath);
        }

        static IapProductDef Coins(string id, int coins, string price, string badge) => new IapProductDef
        {
            productId = id,
            kind = IapProductKind.Consumable,
            displayNameKey = "store." + id,
            fallbackPriceLabel = price,
            coinGrant = coins,
            badgeKey = badge,
        };

        static IapProductDef Owned(string id, string entitlement, string price) => new IapProductDef
        {
            productId = id,
            kind = IapProductKind.NonConsumable,
            displayNameKey = "store." + id,
            fallbackPriceLabel = price,
            entitlement = entitlement,
        };
    }
}
