using System;
using Shikaku.Ads;
using UnityEngine.UIElements;

namespace Shikaku.Menu
{
    public sealed class PrivacyPreferencesController : IDisposable
    {
        private readonly Action _returnToSettings;
        private readonly VisualElement _documentRoot;
        private readonly VisualElement _screen;
        private readonly ScrollView _scrollView;
        private readonly Button _backButton;
        private readonly Toggle _analyticsToggle;
        private readonly Button _adPrivacyChoicesButton;
        private readonly Button _deleteAnalyticsButton;
        private readonly VisualElement _deleteAnalyticsModal;
        private readonly Button _deleteAnalyticsCancelButton;
        private readonly Button _deleteAnalyticsConfirmButton;
        private readonly IVisualElementScheduledItem _privacyRefreshPoll;

        private bool _isVisible;
        private bool _privacyOptionsBusy;
        private bool _deleteAnalyticsModalVisible;
        private volatile bool _privacyRefreshPending;

        public PrivacyPreferencesController(
            VisualElement documentRoot,
            Action returnToSettings)
        {
            _returnToSettings = returnToSettings;
            _documentRoot = documentRoot;
            _screen = RequireElement<VisualElement>(
                documentRoot,
                "privacy-preferences-view");
            _backButton = RequireElement<Button>(
                documentRoot,
                "privacy-preferences-back-button");
            _analyticsToggle = RequireElement<Toggle>(
                documentRoot,
                "privacy-preferences-analytics-toggle");
            _adPrivacyChoicesButton = RequireElement<Button>(
                documentRoot,
                "privacy-preferences-ad-choices-button");
            _deleteAnalyticsButton = RequireElement<Button>(
                documentRoot,
                "privacy-preferences-delete-analytics-button");
            _deleteAnalyticsModal = RequireElement<VisualElement>(
                documentRoot,
                "analytics-delete-modal");
            _deleteAnalyticsCancelButton = RequireElement<Button>(
                documentRoot,
                "analytics-delete-cancel-button");
            _deleteAnalyticsConfirmButton = RequireElement<Button>(
                documentRoot,
                "analytics-delete-confirm-button");

            _scrollView = RequireElement<ScrollView>(
                documentRoot,
                "privacy-preferences-scroll-view");
            _scrollView.verticalScrollerVisibility =
                ScrollerVisibility.Hidden;
            _scrollView.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;

            _backButton.clicked += ReturnToSettings;
            _analyticsToggle.RegisterValueChangedCallback(
                OnAnalyticsChanged);
            _adPrivacyChoicesButton.clicked += ShowAdPrivacyChoices;
            _deleteAnalyticsButton.clicked +=
                ShowDeleteAnalyticsConfirmation;
            _deleteAnalyticsCancelButton.clicked +=
                HideDeleteAnalyticsConfirmation;
            _deleteAnalyticsConfirmButton.clicked +=
                ConfirmDeleteAnalyticsData;
            GameAnalytics.ConsentChanged += RefreshAnalyticsControls;
            AdsManager.BannerPresentationChanged +=
                OnBannerPresentationChanged;
            _screen.RegisterCallback<GeometryChangedEvent>(
                OnScreenGeometryChanged);

            _privacyRefreshPoll = _screen.schedule
                .Execute(PollPrivacyRefresh)
                .Every(100);

            ApplyBannerInset();
            Hide();
        }

        public void Show()
        {
            _isVisible = true;
            RefreshAnalyticsControls();
            RefreshAdPrivacyChoices();
            _screen.style.display = DisplayStyle.Flex;

#if !UNITY_EDITOR
            if (!AdConsentManager.HasCompletedConsentFlow)
            {
                AdConsentManager.EnsureConsentResolvedThenRun(
                    () => _privacyRefreshPending = true);
            }
#endif
        }

        public void Hide()
        {
            _isVisible = false;
            HideDeleteAnalyticsConfirmation();
            _screen.style.display = DisplayStyle.None;
        }

        public bool HandleBack()
        {
            if (!_isVisible)
                return false;

            if (_deleteAnalyticsModalVisible)
            {
                HideDeleteAnalyticsConfirmation();
                return true;
            }

            ReturnToSettings();
            return true;
        }

        public void Dispose()
        {
            AdsManager.BannerPresentationChanged -=
                OnBannerPresentationChanged;
            _screen.UnregisterCallback<GeometryChangedEvent>(
                OnScreenGeometryChanged);
            _backButton.clicked -= ReturnToSettings;
            _analyticsToggle.UnregisterValueChangedCallback(
                OnAnalyticsChanged);
            _adPrivacyChoicesButton.clicked -= ShowAdPrivacyChoices;
            _deleteAnalyticsButton.clicked -=
                ShowDeleteAnalyticsConfirmation;
            _deleteAnalyticsCancelButton.clicked -=
                HideDeleteAnalyticsConfirmation;
            _deleteAnalyticsConfirmButton.clicked -=
                ConfirmDeleteAnalyticsData;
            GameAnalytics.ConsentChanged -= RefreshAnalyticsControls;
            _privacyRefreshPoll?.Pause();
        }

        private void OnBannerPresentationChanged(bool isPresented)
        {
            ApplyBannerInset();
        }

        private void OnScreenGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyBannerInset();
        }

        private void ApplyBannerInset()
        {
            _scrollView.style.marginBottom =
                AdsManager.GetBannerContentInset(
                    _documentRoot,
                    includeBottomSafeArea: true);
        }

        private static T RequireElement<T>(
            VisualElement root,
            string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"Privacy Preferences UI element '{name}' was not found.");
            }

            return element;
        }

        private void ReturnToSettings()
        {
            Hide();
            _returnToSettings?.Invoke();
        }

        private static void OnAnalyticsChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
                GameAnalytics.GrantConsent();
            else
                GameAnalytics.DenyConsent();
        }

        private void RefreshAnalyticsControls()
        {
            _analyticsToggle.SetValueWithoutNotify(
                GameAnalytics.IsConsentGranted);
            _deleteAnalyticsButton.SetEnabled(
                GameAnalytics.CanRequestDataDeletion &&
                GameAnalytics.HasEverGrantedConsent);
        }

        private void ShowDeleteAnalyticsConfirmation()
        {
            if (!GameAnalytics.CanRequestDataDeletion ||
                !GameAnalytics.HasEverGrantedConsent)
            {
                return;
            }

            _deleteAnalyticsModalVisible = true;
            _deleteAnalyticsModal.style.display = DisplayStyle.Flex;
        }

        private void HideDeleteAnalyticsConfirmation()
        {
            _deleteAnalyticsModalVisible = false;
            _deleteAnalyticsModal.style.display = DisplayStyle.None;
        }

        private void ConfirmDeleteAnalyticsData()
        {
            if (!GameAnalytics.RequestDataDeletion())
                return;

            HideDeleteAnalyticsConfirmation();
            RefreshAnalyticsControls();
        }

        private void ShowAdPrivacyChoices()
        {
#if UNITY_EDITOR
            return;
#else
            if (_privacyOptionsBusy ||
                !AdConsentManager.IsPrivacyOptionsRequired)
            {
                return;
            }

            _privacyOptionsBusy = true;
            RefreshAdPrivacyChoices();
            AdConsentManager.ShowPrivacyOptions(
                _ => _privacyRefreshPending = true);
#endif
        }

        private void PollPrivacyRefresh()
        {
            if (_privacyRefreshPending)
            {
                _privacyRefreshPending = false;
                _privacyOptionsBusy = false;
                RefreshAdPrivacyChoices();
            }

            if (_isVisible)
                RefreshAnalyticsControls();
        }

        private void RefreshAdPrivacyChoices()
        {
#if UNITY_EDITOR
            bool shouldShow = false;
#else
            bool shouldShow =
                AdConsentManager.HasCompletedConsentFlow &&
                AdConsentManager.IsPrivacyOptionsRequired;
#endif

            _adPrivacyChoicesButton.style.display = shouldShow
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _adPrivacyChoicesButton.SetEnabled(
                shouldShow && !_privacyOptionsBusy);
        }
    }
}
