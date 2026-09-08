using System;
using Shikaku.Ads;

namespace Shikaku.Store
{
    public enum PurchaseRestoreStatus
    {
        Idle,
        InProgress,
        Succeeded,
        NothingFound,
        Failed,
        Unavailable
    }

    public static class RemoveAdsPurchaseService
    {
        public const string ProductId = StoreProductCatalog.RemoveAdsId;

        private const string EditorFallbackPrice = "$2.99";

        public static event Action StateChanged
        {
            add => StorePurchaseService.StateChanged += value;
            remove => StorePurchaseService.StateChanged -= value;
        }

        public static bool IsOwned => AdEntitlement.AdsRemoved;

        public static bool IsBusy => StorePurchaseService.IsBusy;

        public static bool CanRestorePurchases =>
            StorePurchaseService.CanRestorePurchases;

        public static PurchaseRestoreStatus RestoreStatus =>
            StorePurchaseService.RestoreStatus;

        public static bool CanPurchase =>
            StorePurchaseService.CanPurchase(ProductId);

        public static string PriceText =>
            StorePurchaseService.GetPriceText(
                ProductId,
                EditorFallbackPrice);

        public static void EnsureInitialized()
        {
            StorePurchaseService.EnsureInitialized();
        }

        public static bool BeginPurchase()
        {
            return StorePurchaseService.BeginPurchase(ProductId);
        }

        public static bool RestorePurchases()
        {
            return StorePurchaseService.RestorePurchases();
        }
    }

    public static class RemoveAdsBundlePurchaseService
    {
        public const string ProductId = StoreProductCatalog.RemoveAdsBundleId;
        public const int HintAmount = 30;

        private const string EditorFallbackPrice = "$3.99";

        public static event Action StateChanged
        {
            add => StorePurchaseService.StateChanged += value;
            remove => StorePurchaseService.StateChanged -= value;
        }

        public static bool IsOwned => AdEntitlement.AdsRemoved;

        public static bool CanPurchase =>
            StorePurchaseService.CanPurchase(ProductId);

        public static string PriceText =>
            StorePurchaseService.GetPriceText(
                ProductId,
                EditorFallbackPrice);

        public static void EnsureInitialized()
        {
            StorePurchaseService.EnsureInitialized();
        }

        public static bool BeginPurchase()
        {
            return StorePurchaseService.BeginPurchase(ProductId);
        }
    }
}
