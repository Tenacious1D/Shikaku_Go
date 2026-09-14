using System;
using Shikaku.Store;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.Menu
{
    public sealed class ShopScreenController : IDisposable
    {
        private readonly Action _returnHome;
        private readonly VisualElement _screen;
        private readonly Button _backButton;
        private readonly Button _restorePurchasesButton;
        private readonly Label _restorePurchasesStatus;
        private readonly Label _balanceLabel;
        private readonly Label _tenPriceLabel;
        private readonly Label _twentyFivePriceLabel;
        private readonly Label _sixtyPriceLabel;
        private readonly Label _removeAdsPriceLabel;
        private readonly Label _bundlePriceLabel;
        private readonly Label _purchaseStatusLabel;
        private readonly VisualElement _removeAdsCard;
        private readonly VisualElement _bundleCard;
        private readonly Button _buyTenButton;
        private readonly Button _buyTwentyFiveButton;
        private readonly Button _buySixtyButton;
        private readonly Button _buyRemoveAdsButton;
        private readonly Button _buyBundleButton;

        private bool _isVisible;

        public ShopScreenController(VisualElement documentRoot, Action returnHome)
        {
            _returnHome = returnHome;
            _screen = RequireElement<VisualElement>(documentRoot, "shop-screen");
            _backButton = RequireElement<Button>(documentRoot, "shop-back-button");
            _balanceLabel = RequireElement<Label>(documentRoot, "shop-hint-balance");
            _tenPriceLabel = RequireElement<Label>(documentRoot, "shop-ten-price");
            _twentyFivePriceLabel = RequireElement<Label>(
                documentRoot,
                "shop-twenty-five-price");
            _sixtyPriceLabel = RequireElement<Label>(
                documentRoot,
                "shop-sixty-price");
            _removeAdsPriceLabel = RequireElement<Label>(
                documentRoot,
                "shop-remove-ads-price");
            _bundlePriceLabel = RequireElement<Label>(
                documentRoot,
                "shop-bundle-price");
            _purchaseStatusLabel = RequireElement<Label>(
                documentRoot,
                "shop-purchase-status");
            _removeAdsCard = RequireElement<VisualElement>(
                documentRoot,
                "shop-remove-ads-card");
            _bundleCard = RequireElement<VisualElement>(
                documentRoot,
                "shop-bundle-card");
            _buyTenButton = RequireElement<Button>(
                documentRoot,
                "shop-buy-ten-button");
            _buyTwentyFiveButton = RequireElement<Button>(
                documentRoot,
                "shop-buy-twenty-five-button");
            _buySixtyButton = RequireElement<Button>(
                documentRoot,
                "shop-buy-sixty-button");
            _buyRemoveAdsButton = RequireElement<Button>(
                documentRoot,
                "shop-buy-remove-ads-button");
            _buyBundleButton = RequireElement<Button>(
                documentRoot,
                "shop-buy-bundle-button");

            _restorePurchasesButton = RequireElement<Button>(documentRoot, "shop-restore-purchases-button");
            _restorePurchasesStatus = RequireElement<Label>(documentRoot, "shop-restore-purchases-status");
            _restorePurchasesButton.clicked += RestorePurchases;
            _backButton.clicked += ReturnHome;
            _buyTenButton.clicked += BuyTenHints;
            _buyTwentyFiveButton.clicked += BuyTwentyFiveHints;
            _buySixtyButton.clicked += BuySixtyHints;
            _buyRemoveAdsButton.clicked += BuyRemoveAds;
            _buyBundleButton.clicked += BuyBundle;
            HintWallet.BalanceChanged += RefreshBalance;
            HintPurchaseService.StateChanged += RefreshProducts;

            HintPurchaseService.EnsureInitialized();

            Hide();
        }

        public void Show()
        {
            _isVisible = true;
            _screen.style.display = DisplayStyle.Flex;

            HintPurchaseService.EnsureInitialized();
            RefreshBalance();
            RefreshProducts();
        }

        public void Hide()
        {
            _isVisible = false;
            _screen.style.display = DisplayStyle.None;
        }

        public bool HandleBack()
        {
            if (!_isVisible)
                return false;

            ReturnHome();
            return true;
        }

        public void Dispose()
        {
            _restorePurchasesButton.clicked -= RestorePurchases;
            _backButton.clicked -= ReturnHome;
            _buyTenButton.clicked -= BuyTenHints;
            _buyTwentyFiveButton.clicked -= BuyTwentyFiveHints;
            _buySixtyButton.clicked -= BuySixtyHints;
            _buyRemoveAdsButton.clicked -= BuyRemoveAds;
            _buyBundleButton.clicked -= BuyBundle;
            HintWallet.BalanceChanged -= RefreshBalance;
            HintPurchaseService.StateChanged -= RefreshProducts;
        }

        private static T RequireElement<T>(VisualElement root, string name)
            where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException(
                    $"Shop UI element '{name}' was not found."
                );

            return element;
        }

        private void ReturnHome()
        {
            _returnHome?.Invoke();
        }

        private void BuyTenHints()
        {
            HintPurchaseService.BeginPurchase(
                HintPurchaseService.Hints10ProductId);
        }

        private void BuyTwentyFiveHints()
        {
            HintPurchaseService.BeginPurchase(
                HintPurchaseService.Hints25ProductId);
        }

        private void BuySixtyHints()
        {
            HintPurchaseService.BeginPurchase(
                HintPurchaseService.Hints60ProductId);
        }

        private void BuyRemoveAds()
        {
            RemoveAdsPurchaseService.BeginPurchase();
        }

        private void BuyBundle()
        {
            RemoveAdsBundlePurchaseService.BeginPurchase();
        }

        private void RefreshProducts()
        {
            RefreshHintProducts();
            RefreshPermanentProducts();
            RefreshPurchaseStatus();
            RefreshRestorePurchases();
        }

        private void RestorePurchases()
        {
            RemoveAdsPurchaseService.RestorePurchases();
            RefreshRestorePurchases();
        }

        private void RefreshRestorePurchases()
        {
            _restorePurchasesButton.SetEnabled(RemoveAdsPurchaseService.CanRestorePurchases);
            string message = RemoveAdsPurchaseService.RestoreStatus switch
            {
                PurchaseRestoreStatus.InProgress => "Checking for purchases...",
                PurchaseRestoreStatus.Succeeded => "Purchases restored.",
                PurchaseRestoreStatus.NothingFound => "No purchases were found for this account.",
                PurchaseRestoreStatus.Failed => "Could not restore purchases. Please try again.",
                PurchaseRestoreStatus.Unavailable => "The store is unavailable. Please try again later.",
                _ => string.Empty
            };
            _restorePurchasesStatus.text = message;
            _restorePurchasesStatus.style.display = string.IsNullOrEmpty(message)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshPurchaseStatus()
        {
            string message = HintPurchaseService.StatusMessage;
            _purchaseStatusLabel.text = message;
            _purchaseStatusLabel.style.display =
                string.IsNullOrEmpty(message)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
        }

        private void RefreshHintProducts()
        {
            _tenPriceLabel.text = HintPurchaseService.GetPriceText(
                HintPurchaseService.Hints10ProductId);
            _twentyFivePriceLabel.text = HintPurchaseService.GetPriceText(
                HintPurchaseService.Hints25ProductId);
            _sixtyPriceLabel.text = HintPurchaseService.GetPriceText(
                HintPurchaseService.Hints60ProductId);

            _buyTenButton.SetEnabled(
                HintPurchaseService.CanPurchase(
                    HintPurchaseService.Hints10ProductId));
            _buyTwentyFiveButton.SetEnabled(
                HintPurchaseService.CanPurchase(
                    HintPurchaseService.Hints25ProductId));
            _buySixtyButton.SetEnabled(
                HintPurchaseService.CanPurchase(
                    HintPurchaseService.Hints60ProductId));
        }

        private void RefreshPermanentProducts()
        {
            bool isOwned = RemoveAdsPurchaseService.IsOwned;
            DisplayStyle display = isOwned
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            _removeAdsCard.style.display = display;
            _bundleCard.style.display = display;

            if (isOwned)
                return;

            _removeAdsPriceLabel.text =
                RemoveAdsPurchaseService.PriceText;
            _bundlePriceLabel.text =
                RemoveAdsBundlePurchaseService.PriceText;

            _buyRemoveAdsButton.text = "Buy";
            _buyRemoveAdsButton.SetEnabled(
                RemoveAdsPurchaseService.CanPurchase);
            _buyBundleButton.text = "Buy";
            _buyBundleButton.SetEnabled(
                RemoveAdsBundlePurchaseService.CanPurchase);
        }

        private void RefreshBalance()
        {
            _balanceLabel.text = HintWallet.Balance.ToString();
        }
    }
}
