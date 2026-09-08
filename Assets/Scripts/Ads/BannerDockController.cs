using System;
using Shikaku.Settings;
using Shikaku.Store;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.Ads
{
    /// <summary>
    /// Controls the persistent UI Toolkit background and Remove Ads button that
    /// frame the native LevelPlay banner.
    /// </summary>
    public sealed class BannerDockController : IDisposable
    {
        private const float SurfaceBaseHeight = 150f;
        private const float RemoveButtonBaseBottom = 150f;

        private readonly VisualElement _documentRoot;
        private readonly VisualElement _dockRoot;
        private readonly VisualElement _surface;
        private readonly Button _removeAdsButton;

        public BannerDockController(VisualElement documentRoot)
        {
            _documentRoot = documentRoot ??
                throw new ArgumentNullException(nameof(documentRoot));
            _dockRoot = RequireElement<VisualElement>(
                documentRoot,
                "banner-dock-root");
            _surface = RequireElement<VisualElement>(
                documentRoot,
                "banner-dock-surface");
            _removeAdsButton = RequireElement<Button>(
                documentRoot,
                "banner-dock-remove-ads-button");

            _removeAdsButton.clicked += BuyRemoveAds;
            _documentRoot.RegisterCallback<GeometryChangedEvent>(
                OnGeometryChanged);
            AdsManager.BannerPresentationChanged +=
                OnBannerPresentationChanged;
            RemoveAdsPurchaseService.StateChanged += OnPurchaseStateChanged;
            ThemeManager.Changed += ApplyTheme;

            ThemeManager.EnsureInitialized();
            ApplyTheme();
            RemoveAdsPurchaseService.EnsureInitialized();
            ApplySafeArea();
            Refresh();
        }

        public void Dispose()
        {
            _removeAdsButton.clicked -= BuyRemoveAds;
            _documentRoot.UnregisterCallback<GeometryChangedEvent>(
                OnGeometryChanged);
            AdsManager.BannerPresentationChanged -=
                OnBannerPresentationChanged;
            RemoveAdsPurchaseService.StateChanged -= OnPurchaseStateChanged;
            ThemeManager.Changed -= ApplyTheme;
        }

        private static T RequireElement<T>(
            VisualElement root,
            string name)
            where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"Banner dock UI element '{name}' was not found.");
            }

            return element;
        }

        private void ApplyTheme()
        {
            ThemeManager.ApplyTo(_documentRoot);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            float safeBottom = AdsManager.GetBannerBottomInsetPanelHeight(
                _documentRoot);
            _surface.style.height = SurfaceBaseHeight + safeBottom;
            _removeAdsButton.style.bottom =
                RemoveButtonBaseBottom + safeBottom;
        }

        private void OnBannerPresentationChanged(bool isPresented)
        {
            ApplySafeArea();
            Refresh();
        }

        private void OnPurchaseStateChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            bool shouldShow =
                AdsManager.IsBannerPresented &&
                !RemoveAdsPurchaseService.IsOwned;

            _dockRoot.style.display = shouldShow
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _removeAdsButton.SetEnabled(
                shouldShow &&
                !RemoveAdsPurchaseService.IsBusy &&
                RemoveAdsPurchaseService.CanPurchase);
        }

        private void BuyRemoveAds()
        {
            if (!RemoveAdsPurchaseService.BeginPurchase())
            {
                Debug.LogWarning(
                    "BannerDockController: Remove Ads is not currently " +
                    "available for purchase.");
            }
        }
    }
}
