using System;
using UnityEngine;

namespace Shikaku.Store
{
    public static class HintPurchaseService
    {
        public const string Hints10ProductId = StoreProductCatalog.Hints10Id;
        public const string Hints25ProductId = StoreProductCatalog.Hints25Id;
        public const string Hints60ProductId = StoreProductCatalog.Hints60Id;

        public static event Action StateChanged
        {
            add => StorePurchaseService.StateChanged += value;
            remove => StorePurchaseService.StateChanged -= value;
        }

        public static bool IsBusy => StorePurchaseService.IsBusy;

        public static PurchaseOperationStatus OperationStatus =>
            StorePurchaseService.OperationStatus;

        public static string StatusMessage =>
            StorePurchaseService.StatusMessage;

        public static void EnsureInitialized()
        {
            StorePurchaseService.EnsureInitialized();
        }

        public static bool CanPurchase(string productId)
        {
            return StorePurchaseService.CanPurchase(productId);
        }

        public static string GetPriceText(string productId)
        {
            string fallbackPrice;

            switch (productId)
            {
                case Hints10ProductId:
                    fallbackPrice = "$0.99";
                    break;
                case Hints25ProductId:
                    fallbackPrice = "$1.99";
                    break;
                case Hints60ProductId:
                    fallbackPrice = "$3.99";
                    break;
                default:
                    return "Unavailable";
            }

            return StorePurchaseService.GetPriceText(
                productId,
                fallbackPrice);
        }

        public static bool BeginPurchase(string productId)
        {
            if (!TryGetHintAmount(productId, out _))
            {
                Debug.LogError(
                    $"HintPurchaseService: Unknown product ID: " +
                    productId);
                return false;
            }

            return StorePurchaseService.BeginPurchase(productId);
        }

        public static bool TryGetHintAmount(
            string productId,
            out int amount)
        {
            switch (productId)
            {
                case Hints10ProductId:
                    amount = 10;
                    return true;
                case Hints25ProductId:
                    amount = 25;
                    return true;
                case Hints60ProductId:
                    amount = 60;
                    return true;
                default:
                    amount = 0;
                    return false;
            }
        }
    }
}
