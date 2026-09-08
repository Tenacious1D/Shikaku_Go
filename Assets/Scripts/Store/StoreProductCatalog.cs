using System;
using System.Collections.Generic;
using UnityEngine.Purchasing;

namespace Shikaku.Store
{
    public enum PurchaseOperationStatus
    {
        Idle,
        Connecting,
        LoadingProducts,
        CheckingPurchases,
        Purchasing,
        Succeeded,
        Cancelled,
        Deferred,
        Failed,
        Unavailable
    }

    public readonly struct StoreProductSpec
    {
        public StoreProductSpec(
            string id,
            ProductType type,
            int hintAmount,
            bool removesAds,
            bool grantsBonusHints,
            bool restorable)
        {
            Id = id;
            Type = type;
            HintAmount = hintAmount;
            RemovesAds = removesAds;
            GrantsBonusHints = grantsBonusHints;
            Restorable = restorable;
        }

        public string Id { get; }
        public ProductType Type { get; }
        public int HintAmount { get; }
        public bool RemovesAds { get; }
        public bool GrantsBonusHints { get; }
        public bool Restorable { get; }
    }

    /// <summary>
    /// The single authoritative product catalog shared by runtime purchasing,
    /// release validation, and editor tests.
    /// </summary>
    public static class StoreProductCatalog
    {
        public const string RemoveAdsId = "remove_ads";
        public const string RemoveAdsBundleId = "remove_ads_hints_30";
        public const string Hints10Id = "hints_10";
        public const string Hints25Id = "hints_25";
        public const string Hints60Id = "hints_60";

        private static readonly StoreProductSpec[] ProductArray =
        {
            new StoreProductSpec(
                RemoveAdsId,
                ProductType.NonConsumable,
                0,
                removesAds: true,
                grantsBonusHints: false,
                restorable: true),
            new StoreProductSpec(
                RemoveAdsBundleId,
                ProductType.NonConsumable,
                30,
                removesAds: true,
                grantsBonusHints: true,
                restorable: true),
            new StoreProductSpec(
                Hints10Id,
                ProductType.Consumable,
                10,
                removesAds: false,
                grantsBonusHints: false,
                restorable: false),
            new StoreProductSpec(
                Hints25Id,
                ProductType.Consumable,
                25,
                removesAds: false,
                grantsBonusHints: false,
                restorable: false),
            new StoreProductSpec(
                Hints60Id,
                ProductType.Consumable,
                60,
                removesAds: false,
                grantsBonusHints: false,
                restorable: false)
        };

        private static readonly IReadOnlyList<StoreProductSpec> ProductList =
            Array.AsReadOnly(ProductArray);

        public static IReadOnlyList<StoreProductSpec> Products => ProductList;

        public static bool TryGet(
            string productId,
            out StoreProductSpec product)
        {
            foreach (StoreProductSpec candidate in ProductArray)
            {
                if (string.Equals(
                        candidate.Id,
                        productId,
                        StringComparison.Ordinal))
                {
                    product = candidate;
                    return true;
                }
            }

            product = default;
            return false;
        }

        internal static List<ProductDefinition> BuildDefinitions()
        {
            var definitions = new List<ProductDefinition>(
                ProductArray.Length);

            foreach (StoreProductSpec product in ProductArray)
            {
                definitions.Add(new ProductDefinition(
                    product.Id,
                    product.Type));
            }

            return definitions;
        }
    }
}
