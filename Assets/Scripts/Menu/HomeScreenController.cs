using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Shikaku.Ads;
using Shikaku.Notifications;
using Shikaku.Privacy;
using Shikaku.Settings;
using Shikaku.Services;
using Shikaku.Store;

namespace Shikaku.Menu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HomeScreenController : MonoBehaviour
    {
        public const string StartScreenKey = "home_start_screen";
        public const string DailyScreenId = "daily";
        public const string TimeTrialScreenId = "time_trial";
        public const string FreePlayScreenId = "free_play";
        public const string AdventureScreenId = "adventure";
        public const string SettingsScreenId = "settings";
        public const string FreePlayPackKey = "home_free_play_pack";
        public const string AdventurePackKey = "home_adventure_pack";
        private VisualElement _homeScreen;
        private VisualElement _root;
        private Button[] _menuBackButtons;
        private Button _adventureButton;
        private Button _freePlayButton;
        private Button _timeTrialButton;
        private Button _dailyButton;
        private Button _shopButton;
        private Button _settingsButton;

        [Header("Audio")]
        [SerializeField] private AudioClip mainMenuButtonClip;
        private VisualElement _privacyWelcomeScreen;
        private Toggle _privacyWelcomeAnalyticsToggle;
        private Toggle _privacyWelcomeNotificationsToggle;
        private Button _privacyWelcomeContinueButton;
        private Button _privacyWelcomePrivacyPolicyButton;
        private Button _privacyWelcomeTermsButton;
        private VisualElement _streakRestoreModal;
        private Label _streakRestoreCopy;
        private Label _streakRestoreStatus;
        private Button _streakRestoreAdButton;
        private Button _streakRestoreHintButton;
        private Button _streakRestoreDeclineButton;
        private VisualElement _ratePromptModal;
        private Button _ratePromptRateButton;
        private Button _ratePromptLaterButton;
        private DailyScreenController _dailyScreenController;
        private SettingsScreenController _settingsScreenController;
        private ShopScreenController _shopScreenController;
        private TimeTrialScreenController _timeTrialScreenController;
        private FreePlayScreenController _freePlayScreenController;
        private AdventureScreenController _adventureScreenController;
        private bool _isLoading;
        private bool _waitingForTutorialConsent;
        private volatile bool _tutorialConsentResolved;
        private bool _startupCompleted;
        private string _requestedScreen;
        private string _requestedFreePlayPack;
        private string _requestedAdventurePack;
        private bool _hasRequestedScreen;
        private bool _startupDestinationCompleted;

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            _root = root;
            ThemeManager.EnsureInitialized();
            ThemeManager.ApplyTo(_root);
            ThemeManager.Changed += ApplyTheme;
            NotificationReminderService.ReminderOpened +=
                OpenDailyFromNotification;

            _homeScreen = root.Q<VisualElement>("safe-area");
            _dailyScreenController = new DailyScreenController(root, ShowHome);
            _settingsScreenController = new SettingsScreenController(
                root,
                ShowHome,
                OpenTutorialFromSettings);
            _shopScreenController = new ShopScreenController(root, ShowHome);
            _timeTrialScreenController = new TimeTrialScreenController(root, ShowHome);
            _freePlayScreenController = new FreePlayScreenController(root, ShowHome);
            _adventureScreenController =
                new AdventureScreenController(root, ShowHome);

            _adventureButton = FindButton(root, "adventure-button");
            _freePlayButton = FindButton(root, "free-play-button");
            _timeTrialButton = FindButton(root, "time-trial-button");
            _dailyButton = FindButton(root, "daily-button");
            _shopButton = FindButton(root, "shop-button");
            _settingsButton = FindButton(root, "settings-button");
            _privacyWelcomeScreen =
                root.Q<VisualElement>("privacy-welcome-screen");
            _privacyWelcomeAnalyticsToggle =
                FindToggle(root, "privacy-welcome-analytics-toggle");
            _privacyWelcomeNotificationsToggle =
                FindToggle(root, "privacy-welcome-notifications-toggle");
            _privacyWelcomeContinueButton =
                FindButton(root, "privacy-welcome-continue-button");
            _privacyWelcomePrivacyPolicyButton =
                FindButton(root, "privacy-welcome-privacy-policy-button");
            _privacyWelcomeTermsButton =
                FindButton(root, "privacy-welcome-terms-button");
            _streakRestoreModal =
                root.Q<VisualElement>("streak-restore-modal");
            _streakRestoreCopy =
                root.Q<Label>("streak-restore-copy");
            _streakRestoreStatus =
                root.Q<Label>("streak-restore-status");
            _streakRestoreAdButton =
                FindButton(root, "streak-restore-ad-button");
            _streakRestoreHintButton =
                FindButton(root, "streak-restore-hint-button");
            _streakRestoreDeclineButton =
                FindButton(root, "streak-restore-decline-button");
            _ratePromptModal =
                root.Q<VisualElement>("rate-prompt-modal");
            _ratePromptRateButton =
                FindButton(root, "rate-prompt-rate-button");
            _ratePromptLaterButton =
                FindButton(root, "rate-prompt-later-button");
            _menuBackButtons = new[]
            {
                root.Q<Button>("daily-back-button"),
                root.Q<Button>("settings-back-button"),
                root.Q<Button>("privacy-preferences-back-button"),
                root.Q<Button>("shop-back-button"),
                root.Q<Button>("time-trial-back-button"),
                root.Q<Button>("free-play-back-button"),
                root.Q<Button>("adventure-back-button")
            };

            RegisterCallbacks();
            AdsManager.RewardedAvailabilityChanged +=
                RefreshStreakRestoreActions;
            HintWallet.BalanceChanged += RefreshStreakRestoreActions;
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            ApplyMenuBackButtonLayout();

            _requestedScreen =
                PlayerPrefs.GetString(StartScreenKey, string.Empty);
            _requestedFreePlayPack =
                PlayerPrefs.GetString(FreePlayPackKey, string.Empty);
            _requestedAdventurePack =
                PlayerPrefs.GetString(AdventurePackKey, string.Empty);
            PlayerPrefs.DeleteKey(StartScreenKey);
            PlayerPrefs.DeleteKey(FreePlayPackKey);
            PlayerPrefs.DeleteKey(AdventurePackKey);

            _hasRequestedScreen =
                !string.IsNullOrEmpty(_requestedScreen);

            if (!_hasRequestedScreen &&
                GameSession.ShouldAutoLaunchTutorial &&
                ShouldWaitForConsentBeforeTutorial())
            {
                _waitingForTutorialConsent = true;
                _root.style.display = DisplayStyle.None;
                AdConsentManager.EnsureConsentResolvedThenRun(
                    () => _tutorialConsentResolved = true);
                return;
            }

            ContinueStartupAfterAdConsent();
        }

        private void OnDisable()
        {
            _waitingForTutorialConsent = false;
            _tutorialConsentResolved = false;
            _startupCompleted = false;
            _startupDestinationCompleted = false;
            ThemeManager.Changed -= ApplyTheme;
            AdsManager.RewardedAvailabilityChanged -=
                RefreshStreakRestoreActions;
            HintWallet.BalanceChanged -= RefreshStreakRestoreActions;
            NotificationReminderService.ReminderOpened -=
                OpenDailyFromNotification;

            if (_root != null)
                _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            UnregisterCallbacks();
            _dailyScreenController?.Dispose();
            _dailyScreenController = null;
            _settingsScreenController?.Dispose();
            _settingsScreenController = null;
            _shopScreenController?.Dispose();
            _shopScreenController = null;
            _timeTrialScreenController?.Dispose();
            _timeTrialScreenController = null;
            _freePlayScreenController?.Dispose();
            _freePlayScreenController = null;
            _adventureScreenController?.Dispose();
            _adventureScreenController = null;
            _menuBackButtons = null;
            _privacyWelcomeScreen = null;
            _privacyWelcomeAnalyticsToggle = null;
            _privacyWelcomeNotificationsToggle = null;
            _privacyWelcomeContinueButton = null;
            _privacyWelcomePrivacyPolicyButton = null;
            _privacyWelcomeTermsButton = null;
            _streakRestoreModal = null;
            _streakRestoreCopy = null;
            _streakRestoreStatus = null;
            _streakRestoreAdButton = null;
            _streakRestoreHintButton = null;
            _streakRestoreDeclineButton = null;
            _ratePromptModal = null;
            _ratePromptRateButton = null;
            _ratePromptLaterButton = null;
            _root = null;
        }

        private void ApplyTheme()
        {
            ThemeManager.ApplyTo(_root);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyBlueprintResponsiveLayout();
            ApplyMenuBackButtonLayout();
        }

        private void ApplyBlueprintResponsiveLayout()
        {
            if (_root == null)
                return;

            float panelWidth = _root.resolvedStyle.width;
            float panelHeight = _root.resolvedStyle.height;
            if (panelWidth <= 0f || panelHeight <= 0f)
                return;

            bool useTabletCards =
                panelWidth >= 1180f && panelWidth / panelHeight >= 0.68f;
            VisualElement screenRoot =
                _root.Q<VisualElement>("screen-root");
            screenRoot?.EnableInClassList(
                "blueprint-tablet-layout",
                useTabletCards);
        }

        private void ApplyMenuBackButtonLayout()
        {
            if (_root == null || _menuBackButtons == null ||
                Screen.width <= 0 || Screen.height <= 0)
                return;

            float panelWidth = _root.resolvedStyle.width;
            float panelHeight = _root.resolvedStyle.height;
            if (panelWidth <= 0f || panelHeight <= 0f)
                return;

            Rect safe = Screen.safeArea;
            float safeLeft = Mathf.Max(38f, safe.xMin / Screen.width * panelWidth);
            float safeTop = Mathf.Max(
                46f,
                (Screen.height - safe.yMax) / Screen.height * panelHeight
            );
            float gameplayColumnOffset = Mathf.Max(0f, (panelWidth - 1080f) * 0.5f);

            foreach (Button button in _menuBackButtons)
            {
                if (button == null)
                    continue;

                button.style.left = gameplayColumnOffset + safeLeft;
                button.style.top = safeTop + 17f;
            }
        }

        private void Update()
        {
            if (_tutorialConsentResolved)
            {
                _tutorialConsentResolved = false;

                if (_waitingForTutorialConsent && !_isLoading)
                {
                    _waitingForTutorialConsent = false;
                    ContinueStartupAfterAdConsent();
                }
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsStreakRestorePromptVisible())
                {
                    // Restoration requires an explicit choice so the old
                    // streak never disappears behind an ambiguous dismissal.
                    return;
                }

                if (IsRatePromptVisible())
                {
                    DismissRatePrompt();
                    return;
                }

                if (IsPrivacyWelcomeVisible())
                    return;

                if (_adventureScreenController?.HandleBack() == true)
                    return;

                if (_dailyScreenController?.HandleBack() == true)
                    return;

                if (_settingsScreenController?.HandleBack() == true)
                    return;

                if (_shopScreenController?.HandleBack() == true)
                    return;

                if (_timeTrialScreenController?.HandleBack() == true)
                    return;

                _freePlayScreenController?.HandleBack();
            }
        }

        private void ContinueStartupAfterAdConsent()
        {
            if (_startupCompleted || _isLoading)
                return;

            _root.style.display = DisplayStyle.Flex;

            if (!PrivacyOnboardingState.HasAcknowledgedCurrentVersion)
            {
                _privacyWelcomeAnalyticsToggle.SetValueWithoutNotify(
                    GameAnalytics.IsConsentGranted);
                _privacyWelcomeNotificationsToggle.SetValueWithoutNotify(
                    NotificationReminderService.IsEnabled);
                _privacyWelcomeScreen.style.display = DisplayStyle.Flex;
                return;
            }

            CompleteStartupNavigation();
        }

        private void CompleteStartupNavigation()
        {
            if (_startupCompleted || _isLoading)
                return;

            _startupCompleted = true;
            _privacyWelcomeScreen.style.display = DisplayStyle.None;

            if (!_hasRequestedScreen &&
                GameSession.ShouldAutoLaunchTutorial &&
                StartTutorial(
                    returnToSettings: false,
                    markAutoLaunched: true))
            {
                return;
            }

            // HomeUI is never itself a tutorial scene. Clear an interrupted
            // tutorial session so later menu launches behave normally.
            if (GameSession.IsTutorial)
                GameSession.EndTutorialSession();

            if (TryShowStreakRestorePrompt())
                return;

            CompleteStartupDestination();
        }

        private void CompleteStartupDestination()
        {
            if (_startupDestinationCompleted)
                return;

            _startupDestinationCompleted = true;
            if (_requestedScreen == DailyScreenId)
                ShowDaily();
            else if (_requestedScreen == TimeTrialScreenId)
                ShowTimeTrial();
            else if (_requestedScreen == FreePlayScreenId)
                ShowFreePlay(_requestedFreePlayPack);
            else if (_requestedScreen == AdventureScreenId)
                ShowAdventure(_requestedAdventurePack);
            else if (_requestedScreen == SettingsScreenId)
                ShowSettings();
            else
                ShowHome();

            TryShowPendingRatePrompt();
        }

        private bool TryShowStreakRestorePrompt()
        {
            if (_streakRestoreModal == null)
                return false;

            DailyStreakRestoreOffer offer =
                DailyStreakService.GetRestoreOffer();
            if (!offer.IsEligible)
                return false;

            string missedDayText = offer.MissedDays == 1
                ? "1 day"
                : $"{offer.MissedDays} days";
            _streakRestoreCopy.text =
                $"You missed {missedDayText}. Restore your " +
                $"{offer.CurrentStreak}-day streak, then complete a " +
                "puzzle today to continue it.";

            RefreshStreakRestoreActions();
            _streakRestoreModal.RemoveFromClassList("screen-hidden");
            return true;
        }

        private bool IsStreakRestorePromptVisible()
        {
            return _streakRestoreModal != null &&
                !_streakRestoreModal.ClassListContains("screen-hidden");
        }

        private void RefreshStreakRestoreActions()
        {
            if (_streakRestoreHintButton != null)
            {
                int hintBalance = HintWallet.Balance;
                _streakRestoreHintButton.text =
                    $"Use 1 Hint ({hintBalance})";
                _streakRestoreHintButton.SetEnabled(hintBalance > 0);
            }

            AdsManager adsManager = AdsManager.Instance;
            bool adReady = adsManager != null &&
                (adsManager.IsRewardedHintReady ||
                 adsManager.AdsDisabledForTesting);

            _streakRestoreAdButton?.SetEnabled(adReady);

            if (_streakRestoreStatus == null)
                return;

            _streakRestoreStatus.text = adReady
                ? string.Empty
                : adsManager != null
                    ? adsManager.RewardedHintStatusMessage
                    : "Ads are still starting";
        }

        private void RestoreStreakWithHint()
        {
            if (!DailyStreakService.TryRestoreWithHint())
            {
                RefreshStreakRestoreActions();
                return;
            }

            FinishStreakRestorePrompt();
        }

        private void RestoreStreakWithAd()
        {
            AdsManager adsManager = AdsManager.Instance;
            if (adsManager == null ||
                !adsManager.ShowRewardedStreakRestore(
                    OnStreakRestoreRewardEarned))
            {
                RefreshStreakRestoreActions();
            }
        }

        private void OnStreakRestoreRewardEarned()
        {
            if (!IsStreakRestorePromptVisible())
                return;

            DailyStreakService.TryGrantRestoration();
            FinishStreakRestorePrompt();
        }

        private void DeclineStreakRestoration()
        {
            DailyStreakService.DeclineRestoration();
            FinishStreakRestorePrompt();
        }

        private void FinishStreakRestorePrompt()
        {
            _streakRestoreModal?.AddToClassList("screen-hidden");
            CompleteStartupDestination();
        }

        private void TryShowPendingRatePrompt()
        {
            if (StoreRatingService.TryRequestPendingNativeReview())
                return;

            if (_ratePromptModal == null ||
                !StoreRatingService.MarkPromptShown())
            {
                return;
            }

            _ratePromptModal.RemoveFromClassList("screen-hidden");
        }

        private bool IsRatePromptVisible()
        {
            return _ratePromptModal != null &&
                !_ratePromptModal.ClassListContains("screen-hidden");
        }

        private void RateFromPrompt()
        {
            StoreRatingService.OpenStoreListing();
            DismissRatePrompt();
        }

        private void DismissRatePrompt()
        {
            _ratePromptModal?.AddToClassList("screen-hidden");
        }

        private bool IsPrivacyWelcomeVisible()
        {
            return _privacyWelcomeScreen != null &&
                _privacyWelcomeScreen.resolvedStyle.display !=
                    DisplayStyle.None;
        }

        private void CompletePrivacyWelcome()
        {
            if (_privacyWelcomeAnalyticsToggle.value)
                GameAnalytics.GrantConsent();
            else
                GameAnalytics.DenyConsent();

            if (_privacyWelcomeNotificationsToggle.value)
                NotificationReminderService.RequestEnable(null);
            else
                NotificationReminderService.Disable();

            PrivacyOnboardingState.AcknowledgeCurrentVersion();
            CompleteStartupNavigation();
        }

        private static void OpenPrivacyPolicy()
        {
            Application.OpenURL(LegalLinks.PrivacyPolicyUrl);
        }

        private static void OpenTermsOfService()
        {
            Application.OpenURL(LegalLinks.TermsOfServiceUrl);
        }

        private static bool ShouldWaitForConsentBeforeTutorial()
        {
#if UNITY_EDITOR
            return false;
#else
            if (AdEntitlement.AdsRemoved ||
                AdConsentManager.HasCompletedConsentFlow)
            {
                return false;
            }

            AdsManager adsManager = AdsManager.Instance;
            if (adsManager != null && adsManager.AdsDisabledForTesting)
                return false;

            return true;
#endif
        }

        private static Button FindButton(VisualElement root, string name)
        {
            var button = root.Q<Button>(name);
            if (button == null)
                Debug.LogError($"HomeScreenController could not find UI Toolkit button '{name}'.");

            return button;
        }

        private static Toggle FindToggle(VisualElement root, string name)
        {
            var toggle = root.Q<Toggle>(name);
            if (toggle == null)
                Debug.LogError($"HomeScreenController could not find UI Toolkit toggle '{name}'.");

            return toggle;
        }

        private void RegisterCallbacks()
        {
            RegisterMainMenuButton(_adventureButton, OpenAdventure);
            RegisterMainMenuButton(_freePlayButton, OpenFreePlay);
            RegisterMainMenuButton(_timeTrialButton, OpenTimeTrial);
            RegisterMainMenuButton(_dailyButton, OpenDaily);
            RegisterMainMenuButton(_shopButton, OpenShop);
            RegisterMainMenuButton(_settingsButton, OpenSettings);
            RegisterMainMenuButton(
                _streakRestoreAdButton,
                RestoreStreakWithAd);
            RegisterMainMenuButton(
                _streakRestoreHintButton,
                RestoreStreakWithHint);
            RegisterMainMenuButton(
                _streakRestoreDeclineButton,
                DeclineStreakRestoration);
            if (_privacyWelcomeContinueButton != null) _privacyWelcomeContinueButton.clicked += CompletePrivacyWelcome;
            if (_privacyWelcomePrivacyPolicyButton != null) _privacyWelcomePrivacyPolicyButton.clicked += OpenPrivacyPolicy;
            if (_privacyWelcomeTermsButton != null) _privacyWelcomeTermsButton.clicked += OpenTermsOfService;
            if (_ratePromptRateButton != null) _ratePromptRateButton.clicked += RateFromPrompt;
            if (_ratePromptLaterButton != null) _ratePromptLaterButton.clicked += DismissRatePrompt;
        }

        private void UnregisterCallbacks()
        {
            UnregisterMainMenuButton(_adventureButton, OpenAdventure);
            UnregisterMainMenuButton(_freePlayButton, OpenFreePlay);
            UnregisterMainMenuButton(_timeTrialButton, OpenTimeTrial);
            UnregisterMainMenuButton(_dailyButton, OpenDaily);
            UnregisterMainMenuButton(_shopButton, OpenShop);
            UnregisterMainMenuButton(_settingsButton, OpenSettings);
            UnregisterMainMenuButton(
                _streakRestoreAdButton,
                RestoreStreakWithAd);
            UnregisterMainMenuButton(
                _streakRestoreHintButton,
                RestoreStreakWithHint);
            UnregisterMainMenuButton(
                _streakRestoreDeclineButton,
                DeclineStreakRestoration);
            if (_privacyWelcomeContinueButton != null) _privacyWelcomeContinueButton.clicked -= CompletePrivacyWelcome;
            if (_privacyWelcomePrivacyPolicyButton != null) _privacyWelcomePrivacyPolicyButton.clicked -= OpenPrivacyPolicy;
            if (_privacyWelcomeTermsButton != null) _privacyWelcomeTermsButton.clicked -= OpenTermsOfService;
            if (_ratePromptRateButton != null) _ratePromptRateButton.clicked -= RateFromPrompt;
            if (_ratePromptLaterButton != null) _ratePromptLaterButton.clicked -= DismissRatePrompt;
        }

        private void RegisterMainMenuButton(
            Button button,
            System.Action action)
        {
            if (button == null)
                return;

            button.clicked += PlayMainMenuButtonSound;
            button.clicked += action;
        }

        private void UnregisterMainMenuButton(
            Button button,
            System.Action action)
        {
            if (button == null)
                return;

            button.clicked -= PlayMainMenuButtonSound;
            button.clicked -= action;
        }

        private void PlayMainMenuButtonSound()
        {
            AppSettings.EnsureLoaded();

            if (mainMenuButtonClip == null || AudioListener.volume <= 0f)
                return;

            var soundObject = new GameObject("Main Menu Button Sound");
            DontDestroyOnLoad(soundObject);

            var source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.PlayOneShot(mainMenuButtonClip);

            Destroy(soundObject, mainMenuButtonClip.length + 0.1f);
        }

        private void OpenAdventure()
        {
            if (_isLoading)
                return;

            if (!Progression.TryGetStoryContinue(
                    out string packPath,
                    out int levelIndex))
            {
                Debug.LogError(
                    "No playable Story puzzle was found. Check Resources/Puzzles/Story.");
                return;
            }

            var puzzleIds = PuzzleCatalog.GetPackPuzzleIds(packPath);
            if (puzzleIds == null || puzzleIds.Count == 0)
            {
                Debug.LogError($"Story pack '{packPath}' has no puzzles.");
                return;
            }

            levelIndex = Mathf.Clamp(levelIndex, 1, puzzleIds.Count);
            _isLoading = true;

            GameSession.Mode = MenuMode.Story;
            GameSession.LevelIndex = levelIndex;
            GameSession.PackPath = packPath;
            GameSession.SetPuzzle(puzzleIds[levelIndex - 1]);

            Progression.SetStoryCurrentPackIfLater(packPath);
            Progression.SetLastPlayedLevelForPack(packPath, levelIndex);

            PlayerPrefs.DeleteKey("menu_start_panel");
            PlayerPrefs.DeleteKey("menu_return");
            PlayerPrefs.DeleteKey("menu_return_pack");
            PlayerPrefs.Save();

            SceneManager.LoadScene("Gameplay");
        }

        private void OpenFreePlay() => ShowFreePlay();
        private void OpenTimeTrial() => ShowTimeTrial();
        private void OpenDaily() => ShowDaily();

        private void OpenDailyFromNotification()
        {
            PlayerPrefs.DeleteKey(StartScreenKey);
            PlayerPrefs.Save();

            if (!_startupCompleted || _isLoading ||
                IsStreakRestorePromptVisible())
            {
                _requestedScreen = DailyScreenId;
                _hasRequestedScreen = true;
                return;
            }

            ShowDaily();
        }

        private void OpenShop() => ShowShop();
        private void OpenSettings() => ShowSettings();

        private void OpenTutorialFromSettings()
        {
            StartTutorial(
                returnToSettings: true,
                markAutoLaunched: false);
        }

        private bool StartTutorial(
            bool returnToSettings,
            bool markAutoLaunched)
        {
            if (_isLoading)
                return false;

            if (!GameSession.TryPrepareTutorial(
                    returnToSettings,
                    markAutoLaunched))
            {
                return false;
            }

            _isLoading = true;
            PlayerPrefs.DeleteKey("menu_start_panel");
            PlayerPrefs.DeleteKey("menu_return");
            PlayerPrefs.DeleteKey("menu_return_pack");
            PlayerPrefs.Save();
            SceneManager.LoadScene("Gameplay");
            return true;
        }

        private void ShowHome()
        {
            _dailyScreenController?.Hide();
            _settingsScreenController?.Hide();
            _shopScreenController?.Hide();
            _timeTrialScreenController?.Hide();
            _freePlayScreenController?.Hide();
            _adventureScreenController?.Hide();
            if (_homeScreen != null)
                _homeScreen.style.display = DisplayStyle.Flex;
            GameAnalytics.ScreenViewed("home");
        }

        private void ShowDaily()
        {
            if (_homeScreen != null)
                _homeScreen.style.display = DisplayStyle.None;

            _settingsScreenController?.Hide();
            _shopScreenController?.Hide();
            _timeTrialScreenController?.Hide();
            _freePlayScreenController?.Hide();
            _adventureScreenController?.Hide();
            _dailyScreenController?.Show();
            GameAnalytics.ScreenViewed("daily");
        }

        private void ShowSettings()
        {
            if (_homeScreen != null)
                _homeScreen.style.display = DisplayStyle.None;

            _dailyScreenController?.Hide();
            _shopScreenController?.Hide();
            _timeTrialScreenController?.Hide();
            _freePlayScreenController?.Hide();
            _adventureScreenController?.Hide();
            _settingsScreenController?.Show();
            GameAnalytics.ScreenViewed("settings");
        }

        private void ShowShop()
        {
            if (_homeScreen != null)
                _homeScreen.style.display = DisplayStyle.None;

            _dailyScreenController?.Hide();
            _settingsScreenController?.Hide();
            _timeTrialScreenController?.Hide();
            _freePlayScreenController?.Hide();
            _adventureScreenController?.Hide();
            _shopScreenController?.Show();
            GameAnalytics.ScreenViewed("shop");
        }

        private void ShowTimeTrial()
        {
            if (_homeScreen != null)
                _homeScreen.style.display = DisplayStyle.None;

            _dailyScreenController?.Hide();
            _settingsScreenController?.Hide();
            _shopScreenController?.Hide();
            _freePlayScreenController?.Hide();
            _adventureScreenController?.Hide();
            _timeTrialScreenController?.Show();
            GameAnalytics.ScreenViewed("time_trial");
        }

        private void ShowFreePlay(string packPath = null)
        {
            if (_homeScreen != null)
                _homeScreen.style.display = DisplayStyle.None;

            _dailyScreenController?.Hide();
            _settingsScreenController?.Hide();
            _shopScreenController?.Hide();
            _timeTrialScreenController?.Hide();
            _adventureScreenController?.Hide();
            _freePlayScreenController?.Show(packPath);
            GameAnalytics.ScreenViewed("free_play");
        }

        private void ShowAdventure(string packPath = null)
        {
            if (_homeScreen != null)
                _homeScreen.style.display = DisplayStyle.None;

            _dailyScreenController?.Hide();
            _settingsScreenController?.Hide();
            _shopScreenController?.Hide();
            _timeTrialScreenController?.Hide();
            _freePlayScreenController?.Hide();
            _adventureScreenController?.Show(packPath);
            GameAnalytics.ScreenViewed("adventure");
        }

    }
}
