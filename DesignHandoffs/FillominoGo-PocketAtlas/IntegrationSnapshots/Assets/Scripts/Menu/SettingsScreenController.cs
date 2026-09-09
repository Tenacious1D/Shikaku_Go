using System;
using System.Collections.Generic;
using Shikaku.Achievements;
using Shikaku.Ads;
using Shikaku.Notifications;
using Shikaku.Privacy;
using Shikaku.Settings;
using Shikaku.Store;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Shikaku.Menu
{
    public sealed class SettingsScreenController : IDisposable
    {
        private const string WebsiteUrl = "https://smoothbraingames.com";

        private readonly Action _returnHome;
        private readonly Action _startTutorial;
        private readonly VisualElement _documentRoot;
        private readonly VisualElement _screen;
        private readonly VisualElement _mainView;
        private readonly ScrollView _scrollView;
        private readonly Button _backButton;
        private readonly DropdownField _appearanceDropdown;
        private readonly Toggle _colorLabelsToggle;
        private readonly Toggle _vibrationToggle;
        private readonly Toggle _reduceMotionToggle;
        private readonly Toggle _notificationsToggle;
        private readonly Button _notificationSystemButton;
        private readonly SliderInt _volumeSlider;
        private readonly Label _volumeValueLabel;
        private readonly Button _emailSupportButton;
        private readonly Button _reportAdButton;
        private readonly Button _howToPlayButton;
        private readonly Button _achievementsButton;
        private readonly Button _websiteButton;
        private readonly Button _privacyPreferencesButton;
        private readonly Button _privacyPolicyButton;
        private readonly Button _termsButton;
        private readonly Button _rateButton;
        private readonly Label _versionLabel;
        private readonly Button _restorePurchasesButton;
        private readonly Label _restorePurchasesStatus;
        private readonly VisualElement _adReportModal;
        private readonly Button _adReportBackdropButton;
        private readonly Button _adReportEmailButton;
        private readonly Button _adReportSupportPageButton;
        private readonly Button _adReportCancelButton;
        private readonly PrivacyPreferencesController
            _privacyPreferencesController;

        private bool _isVisible;
        private int _diagnosticTapCount;
        private float _lastDiagnosticTapAt = -1f;
        private bool _isAdReportModalVisible;

        public SettingsScreenController(
            VisualElement documentRoot,
            Action returnHome,
            Action startTutorial)
        {
            _returnHome = returnHome;
            _startTutorial = startTutorial;
            _documentRoot = documentRoot;
            _screen = RequireElement<VisualElement>(
                documentRoot,
                "settings-screen"
            );
            _mainView = RequireElement<VisualElement>(
                documentRoot,
                "settings-main-view"
            );
            _backButton = RequireElement<Button>(
                documentRoot,
                "settings-back-button"
            );
            _appearanceDropdown = RequireElement<DropdownField>(
                documentRoot,
                "settings-appearance-dropdown"
            );
            _appearanceDropdown.choices = new List<string>
            {
                "System",
                "Light",
                "Dark"
            };
            _colorLabelsToggle = RequireElement<Toggle>(
                documentRoot,
                "settings-color-labels-toggle"
            );
            _vibrationToggle = RequireElement<Toggle>(
                documentRoot,
                "settings-vibration-toggle"
            );
            _reduceMotionToggle = RequireElement<Toggle>(
                documentRoot,
                "settings-reduce-motion-toggle"
            );
            _notificationsToggle = RequireElement<Toggle>(
                documentRoot,
                "settings-notifications-toggle"
            );
            _notificationSystemButton = RequireElement<Button>(
                documentRoot,
                "settings-notification-system-button"
            );
            _volumeSlider = RequireElement<SliderInt>(
                documentRoot,
                "settings-volume-slider"
            );
            _volumeValueLabel = RequireElement<Label>(
                documentRoot,
                "settings-volume-value"
            );
            _emailSupportButton = RequireElement<Button>(
                documentRoot,
                "settings-email-support-button"
            );
            _reportAdButton = RequireElement<Button>(
                documentRoot,
                "settings-report-ad-button"
            );
            _howToPlayButton = RequireElement<Button>(
                documentRoot,
                "settings-how-to-play-button"
            );
            _achievementsButton = RequireElement<Button>(
                documentRoot,
                "settings-achievements-button"
            );
            _websiteButton = RequireElement<Button>(
                documentRoot,
                "settings-website-button"
            );
            _privacyPreferencesButton = RequireElement<Button>(
                documentRoot,
                "settings-privacy-preferences-button"
            );
            _privacyPolicyButton = RequireElement<Button>(
                documentRoot,
                "settings-privacy-policy-button"
            );
            _termsButton = RequireElement<Button>(
                documentRoot,
                "settings-terms-button"
            );
            _rateButton = RequireElement<Button>(
                documentRoot,
                "settings-rate-button"
            );
            _restorePurchasesButton = RequireElement<Button>(
                documentRoot,
                "settings-restore-purchases-button"
            );
            _restorePurchasesStatus = RequireElement<Label>(
                documentRoot,
                "settings-restore-purchases-status"
            );
            _versionLabel = RequireElement<Label>(
                documentRoot,
                "settings-version-label"
            );
            _adReportModal = RequireElement<VisualElement>(
                documentRoot,
                "settings-ad-report-modal"
            );
            _adReportBackdropButton = RequireElement<Button>(
                documentRoot,
                "settings-ad-report-backdrop-button"
            );
            _adReportEmailButton = RequireElement<Button>(
                documentRoot,
                "settings-ad-report-email-button"
            );
            _adReportSupportPageButton = RequireElement<Button>(
                documentRoot,
                "settings-ad-report-support-page-button"
            );
            _adReportCancelButton = RequireElement<Button>(
                documentRoot,
                "settings-ad-report-cancel-button"
            );

            _scrollView = RequireElement<ScrollView>(
                documentRoot,
                "settings-scroll-view"
            );
            _scrollView.verticalScrollerVisibility =
                ScrollerVisibility.Hidden;
            _scrollView.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;

            _privacyPreferencesController =
                new PrivacyPreferencesController(
                    documentRoot,
                    ShowMainSettings);

            ThemeManager.EnsureInitialized();
            ThemeManager.Changed += RefreshAppearanceControl;
            AdsManager.BannerPresentationChanged +=
                OnBannerPresentationChanged;
            _screen.RegisterCallback<GeometryChangedEvent>(
                OnScreenGeometryChanged);

            _backButton.clicked += ReturnHome;
            _appearanceDropdown.RegisterValueChangedCallback(
                OnAppearanceChanged
            );
            _colorLabelsToggle.RegisterValueChangedCallback(
                OnColorLabelsChanged
            );
            _vibrationToggle.RegisterValueChangedCallback(
                OnVibrationChanged
            );
            _reduceMotionToggle.RegisterValueChangedCallback(
                OnReduceMotionChanged
            );
            _notificationsToggle.RegisterValueChangedCallback(
                OnNotificationsChanged
            );
            _notificationSystemButton.clicked +=
                OpenSystemNotificationSettings;
            NotificationReminderService.StateChanged +=
                RefreshNotificationControls;
            _volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);
            _emailSupportButton.clicked += EmailSupport;
            _reportAdButton.clicked += ShowAdReportModal;
            _adReportBackdropButton.clicked += HideAdReportModal;
            _adReportEmailButton.clicked += EmailAdReport;
            _adReportSupportPageButton.clicked += OpenAdReportSupportPage;
            _adReportCancelButton.clicked += HideAdReportModal;
            _howToPlayButton.clicked += StartTutorial;
            _achievementsButton.clicked += ShowAchievements;
            _websiteButton.clicked += OpenWebsite;
            _privacyPreferencesButton.clicked += ShowPrivacyPreferences;
            _privacyPolicyButton.clicked += OpenPrivacyPolicy;
            _termsButton.clicked += OpenTermsOfService;
            _rateButton.clicked += RateShikakuGo;

            _restorePurchasesButton.clicked += RestorePurchases;
            _versionLabel.RegisterCallback<PointerDownEvent>(
                OnVersionLabelPressed);
            RemoveAdsPurchaseService.StateChanged +=
                RefreshRestorePurchases;
            RemoveAdsPurchaseService.EnsureInitialized();
            ApplyBannerInset();
            Hide();
        }

        public void Show()
        {
            AppSettings.EnsureLoaded();

            RefreshAppearanceControl();
            _colorLabelsToggle.SetValueWithoutNotify(
                AppSettings.ColorLabels
            );
            _vibrationToggle.SetValueWithoutNotify(
                AppSettings.VibrationEnabled
            );
            _reduceMotionToggle.SetValueWithoutNotify(
                AppSettings.ReduceMotion
            );
            RefreshNotificationControls();
            _volumeSlider.SetValueWithoutNotify(AppSettings.MasterVolume);
            UpdateVolumeText(AppSettings.MasterVolume);
            _versionLabel.text = $"Version {Application.version}";
            _achievementsButton.SetEnabled(
                AchievementService.IsPlatformConfigured
            );

            HideAdReportModal();
            _isVisible = true;
            RefreshRestorePurchases();
            _screen.style.display = DisplayStyle.Flex;
            ShowMainSettings();
        }

        public void Hide()
        {
            _isVisible = false;
            HideAdReportModal();
            _privacyPreferencesController.Hide();
            _mainView.style.display = DisplayStyle.None;
            _screen.style.display = DisplayStyle.None;
        }

        public bool HandleBack()
        {
            if (!_isVisible)
                return false;

            if (_isAdReportModalVisible)
            {
                HideAdReportModal();
                return true;
            }

            if (_privacyPreferencesController.HandleBack())
                return true;

            ReturnHome();
            return true;
        }

        public void Dispose()
        {
            ThemeManager.Changed -= RefreshAppearanceControl;
            AdsManager.BannerPresentationChanged -=
                OnBannerPresentationChanged;
            _screen.UnregisterCallback<GeometryChangedEvent>(
                OnScreenGeometryChanged);

            _backButton.clicked -= ReturnHome;
            _appearanceDropdown.UnregisterValueChangedCallback(
                OnAppearanceChanged
            );
            _colorLabelsToggle.UnregisterValueChangedCallback(
                OnColorLabelsChanged
            );
            _vibrationToggle.UnregisterValueChangedCallback(
                OnVibrationChanged
            );
            _reduceMotionToggle.UnregisterValueChangedCallback(
                OnReduceMotionChanged
            );
            _notificationsToggle.UnregisterValueChangedCallback(
                OnNotificationsChanged
            );
            _notificationSystemButton.clicked -=
                OpenSystemNotificationSettings;
            NotificationReminderService.StateChanged -=
                RefreshNotificationControls;
            _volumeSlider.UnregisterValueChangedCallback(OnVolumeChanged);
            _emailSupportButton.clicked -= EmailSupport;
            _reportAdButton.clicked -= ShowAdReportModal;
            _adReportBackdropButton.clicked -= HideAdReportModal;
            _adReportEmailButton.clicked -= EmailAdReport;
            _adReportSupportPageButton.clicked -= OpenAdReportSupportPage;
            _adReportCancelButton.clicked -= HideAdReportModal;
            _howToPlayButton.clicked -= StartTutorial;
            _achievementsButton.clicked -= ShowAchievements;
            _websiteButton.clicked -= OpenWebsite;
            _privacyPreferencesButton.clicked -= ShowPrivacyPreferences;
            _privacyPolicyButton.clicked -= OpenPrivacyPolicy;
            _termsButton.clicked -= OpenTermsOfService;
            _rateButton.clicked -= RateShikakuGo;
            _restorePurchasesButton.clicked -= RestorePurchases;
            RemoveAdsPurchaseService.StateChanged -=
                RefreshRestorePurchases;
            _versionLabel.UnregisterCallback<PointerDownEvent>(
                OnVersionLabelPressed);
            _privacyPreferencesController.Dispose();
        }

        private void OnVersionLabelPressed(PointerDownEvent evt)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            const float tapWindowSeconds = 3f;
            if (_lastDiagnosticTapAt < 0f ||
                Time.unscaledTime - _lastDiagnosticTapAt > tapWindowSeconds)
            {
                _diagnosticTapCount = 0;
            }

            _lastDiagnosticTapAt = Time.unscaledTime;
            _diagnosticTapCount++;

            if (_diagnosticTapCount < 7)
                return;

            _diagnosticTapCount = 0;
            evt.StopPropagation();
            AdDiagnostics.ShowPanel(_documentRoot);
#endif
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
                    $"Settings UI element '{name}' was not found."
                );
            }

            return element;
        }

        private void ReturnHome()
        {
            _returnHome?.Invoke();
        }

        private void ShowPrivacyPreferences()
        {
            _mainView.style.display = DisplayStyle.None;
            _privacyPreferencesController.Show();
        }

        private void ShowMainSettings()
        {
            if (!_isVisible)
                return;

            _privacyPreferencesController.Hide();
            _mainView.style.display = DisplayStyle.Flex;
        }

        private static void OnAppearanceChanged(ChangeEvent<string> evt)
        {
            AppThemeMode mode;

            switch (evt.newValue)
            {
                case "Light":
                    mode = AppThemeMode.Light;
                    break;

                case "Dark":
                    mode = AppThemeMode.Dark;
                    break;

                default:
                    mode = AppThemeMode.System;
                    break;
            }

            AppSettings.SetThemeMode(mode);
        }

        private void RefreshAppearanceControl()
        {
            string value;

            switch (AppSettings.ThemeMode)
            {
                case AppThemeMode.Light:
                    value = "Light";
                    break;

                case AppThemeMode.Dark:
                    value = "Dark";
                    break;

                default:
                    value = "System";
                    break;
            }

            _appearanceDropdown.SetValueWithoutNotify(value);
            _appearanceDropdown.SetEnabled(true);
        }

        private static void OnColorLabelsChanged(
            ChangeEvent<bool> evt)
        {
            AppSettings.SetColorLabels(evt.newValue);
        }

        private static void OnVibrationChanged(ChangeEvent<bool> evt)
        {
            AppSettings.SetVibrationEnabled(evt.newValue);
        }

        private static void OnReduceMotionChanged(
            ChangeEvent<bool> evt)
        {
            AppSettings.SetReduceMotion(evt.newValue);
        }

        private void OnNotificationsChanged(ChangeEvent<bool> evt)
        {
            if (!evt.newValue)
            {
                NotificationReminderService.Disable();
                RefreshNotificationControls();
                return;
            }

            _notificationsToggle.SetEnabled(false);

            NotificationReminderService.RequestEnable(
                OnNotificationEnableCompleted);
        }

        private void OnNotificationEnableCompleted(bool allowed)
        {
            RefreshNotificationControls();
        }

        private void RefreshNotificationControls()
        {
            bool supported =
                NotificationReminderService.IsSupported;
            bool enabled =
                NotificationReminderService.IsEnabled;
            ReminderPermissionState permission =
                NotificationReminderService.PermissionState;

            _notificationsToggle.SetValueWithoutNotify(enabled);
            _notificationsToggle.SetEnabled(
                supported &&
                permission != ReminderPermissionState.Requesting);

            bool blocked =
                supported &&
                (permission == ReminderPermissionState.Denied ||
                 permission ==
                    ReminderPermissionState.DeniedPermanently ||
                 (enabled &&
                  permission == ReminderPermissionState.Allowed &&
                  !NotificationReminderService.IsSystemAvailable));

            _notificationSystemButton.style.display =
                blocked
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

        }

        private static void OpenSystemNotificationSettings()
        {
            NotificationReminderService
                .OpenSystemNotificationSettings();
        }

        private void OnVolumeChanged(ChangeEvent<int> evt)
        {
            UpdateVolumeText(evt.newValue);
            AppSettings.SetMasterVolume(evt.newValue);
        }

        private void UpdateVolumeText(int value)
        {
            _volumeValueLabel.text =
                value == 0 ? "Off" : value.ToString();
        }

        private static void OpenWebsite()
        {
            Application.OpenURL(WebsiteUrl);
        }

        private static void OpenPrivacyPolicy()
        {
            Application.OpenURL(LegalLinks.PrivacyPolicyUrl);
        }

        private static void OpenTermsOfService()
        {
            Application.OpenURL(LegalLinks.TermsOfServiceUrl);
        }

        private static void RateShikakuGo()
        {
            StoreRatingService.OpenStoreListing();
        }

        private void ShowAdReportModal()
        {
            if (!_isVisible)
                return;

            _isAdReportModalVisible = true;
            _adReportModal.style.display = DisplayStyle.Flex;
            _adReportModal.BringToFront();
        }

        private void HideAdReportModal()
        {
            _isAdReportModalVisible = false;
            _adReportModal.style.display = DisplayStyle.None;
        }

        private void EmailAdReport()
        {
            HideAdReportModal();
            AdReportService.OpenEmailReport();
        }

        private void OpenAdReportSupportPage()
        {
            HideAdReportModal();
            AdReportService.OpenSupportPage();
        }

        private static void EmailSupport()
        {
            string subject =
                UnityWebRequest.EscapeURL("Shikaku Go Support");
            string body = UnityWebRequest.EscapeURL(
                "Please describe the issue you are experiencing:\n\n" +
                $"App version: {Application.version}\n" +
                $"Platform: {Application.platform}\n"
            );

            Application.OpenURL(
                $"mailto:{AdReportService.SupportEmail}?subject={subject}&body={body}"
            );
        }

        private void StartTutorial()
        {
            _startTutorial?.Invoke();
        }

        private void RestorePurchases()
        {
            if (!RemoveAdsPurchaseService.RestorePurchases())
                RefreshRestorePurchases();
        }

        private void RefreshRestorePurchases()
        {
            _restorePurchasesButton.SetEnabled(
                RemoveAdsPurchaseService.CanRestorePurchases);

            string statusText;

            switch (RemoveAdsPurchaseService.RestoreStatus)
            {
                case PurchaseRestoreStatus.InProgress:
                    statusText = "Checking for purchases...";
                    break;
                case PurchaseRestoreStatus.Succeeded:
                    statusText = "Purchases restored.";
                    break;
                case PurchaseRestoreStatus.NothingFound:
                    statusText =
                        "No purchases were found for this account.";
                    break;
                case PurchaseRestoreStatus.Failed:
                    statusText =
                        "Could not restore purchases. Please try again.";
                    break;
                case PurchaseRestoreStatus.Unavailable:
#if UNITY_EDITOR
                    statusText =
                        "Restore purchases from a device build.";
#else
                    statusText =
                        "The store is unavailable. Please try again later.";
#endif
                    break;
                default:
                    statusText = string.Empty;
                    break;
            }

            _restorePurchasesStatus.text = statusText;
            _restorePurchasesStatus.style.display =
                string.IsNullOrEmpty(statusText)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
        }

        private static void ShowAchievements()
        {
            AchievementService.ShowPlatformAchievements();
        }
    }
}
