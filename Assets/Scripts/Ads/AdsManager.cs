using System;
using UnityEngine;
using Unity.Services.LevelPlay;
using UnityEngine.UIElements;
using Shikaku.Store;
using Shikaku.Privacy;

namespace Shikaku.Ads
{
    /// <summary>
    /// Initializes LevelPlay once and manages rewarded hint ads.
    /// Persists while switching between MainMenu and Gameplay.
    /// </summary>
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }
        public static event Action<bool> BannerPresentationChanged;
        public static event Action RewardedAvailabilityChanged;

        public const float BannerDockBaseInset = 242f;

        public static bool IsBannerPresented =>
            Instance != null && Instance._bannerPresented;

        [Header("LevelPlay Keys")]
        [Tooltip("LevelPlay app key from the Unity LevelPlay dashboard.")]
        [SerializeField] private string androidAppKey = "";

        [Tooltip("Use the iOS app key when you create the iOS version.")]
        [SerializeField] private string iosAppKey = "";

        [Header("Rewarded Ad Unit IDs")]
        [Tooltip("Android rewarded ad unit ID.")]
        [SerializeField] private string androidRewardedAdUnitId = "";

        [Tooltip("iOS rewarded ad unit ID.")]
        [SerializeField] private string iosRewardedAdUnitId = "";

        [Header("Rewarded Placement")]
        [SerializeField] private string hintPlacementName = "hint";

        [SerializeField]
        private string streakRestorePlacementName = "hint";

        [Header("Banner Ad Unit IDs")]
        [Tooltip("Android banner ad unit ID.")]
        [SerializeField] private string androidBannerAdUnitId = "";

        [Tooltip("iOS banner ad unit ID.")]
        [SerializeField] private string iosBannerAdUnitId = "";

        [Header("Banner Settings")]
        [SerializeField] private string bannerPlacementName = "bottom_banner";

        [Tooltip("Turn this off when you do not want banners during testing.")]
        [SerializeField] private bool enableBannerAds = true;

        [Header("Banner Dock UI")]
        [SerializeField] private VisualTreeAsset bannerDockVisualTree;
        [SerializeField] private PanelSettings bannerDockPanelSettings;

        [Header("Testing")]
        [Tooltip("Disables all ad loading and display for ordinary game testing.")]
        [SerializeField] private bool disableAllAdsForTesting;

        [Tooltip("Enables the LevelPlay integration test suite metadata.")]
        [SerializeField] private bool enableIntegrationTestSuite;

        private LevelPlayBannerAd _bannerAd;

        private LevelPlayRewardedAd _rewardedAd;

        private Action _pendingRewardAction;
        private string _pendingRewardPlacementName = string.Empty;
        private string _pendingRewardType = string.Empty;

        private bool AllAdsDisabled => disableAllAdsForTesting;
        private bool BannerAdsDisabled =>
            disableAllAdsForTesting || AdEntitlement.AdsRemoved;
        public bool AdsDisabledForTesting =>
    disableAllAdsForTesting;

        private bool _sdkInitialized;
        private bool _rewardGrantedForCurrentAd;
        private bool _rewardedAdIsShowing;

        private volatile bool _initializeAdsOnMainThread;
        private volatile bool _beginATTOnMainThread;
        private bool _attRequestStarted;
        private float _attEarliestRequestAt = -1f;
        private bool _applicationFocused = true;
        private bool _applicationPaused;
        private const int BannerPlacementCappedErrorCode = 604;
        private const float BannerViewportPollSeconds = 0.2f;
        private const float BannerViewportDebounceSeconds = 0.4f;
        private const float BannerLoadTimeoutSeconds = 20f;
        private const float BannerMissingWatchdogSeconds = 25f;
        private const float RewardedLoadTimeoutSeconds = 20f;
        private const float NetworkRecoveryDelaySeconds = 2f;
        private static readonly float[] RewardedRetryDelaysSeconds =
        {
            5f,
            15f,
            30f,
            60f
        };
        private static readonly float[] BannerRetryDelaysSeconds =
        {
            5f,
            15f,
            30f,
            60f
        };

        private string _adSessionId;
        private int _bannerLoadAttemptCount;
        private int _bannerConsecutiveFailures;
        private float _nextBannerRetryAt = -1f;
        private bool _bannerLoadInProgress;
        private float _bannerLoadStartedAt = -1f;
        private float _bannerMissingSince = -1f;
        private bool _bannerHasDisplayed;
        private bool _bannerPresented;
        private bool _bannerPlacementCapped;
        private bool _bannerRespectsSafeArea = true;
        private bool _hasAppliedBannerViewport;
        private bool _hasObservedBannerViewport;
        private bool _androidInsetsQueryFailureLogged;
        private float _nextBannerViewportPollAt;
        private float _bannerViewportStableSince;
        private BannerViewportState _appliedBannerViewport;
        private BannerViewportState _observedBannerViewport;
        private UIDocument _bannerDockDocument;
        private BannerDockController _bannerDockController;
        private bool _rewardedLoadInProgress;
        private float _rewardedLoadStartedAt = -1f;
        private float _nextRewardedRetryAt = -1f;
        private int _rewardedConsecutiveFailures;
        private string _lastBannerEvent = "not started";
        private string _lastRewardedEvent = "not started";
        private NetworkReachability _lastNetworkReachability;
        private float _networkRecoveryAt = -1f;
        private int _networkTransitionCount;

        private struct BannerViewportState
        {
            public int Width;
            public int Height;
            public int SafeLeft;
            public int SafeRight;
            public int SafeTop;
            public int BottomInset;
            public bool RespectSafeArea;

            public bool Matches(BannerViewportState other)
            {
                return Width == other.Width &&
                       Height == other.Height &&
                       SafeLeft == other.SafeLeft &&
                       SafeRight == other.SafeRight &&
                       SafeTop == other.SafeTop &&
                       BottomInset == other.BottomInset &&
                       RespectSafeArea == other.RespectSafeArea;
            }
        }

        private string AdLogPrefix =>
            $"AdsManager[{_adSessionId}]";

        public bool IsRewardedHintReady
        {
            get
            {
                return _sdkInitialized &&
                       !_rewardedAdIsShowing &&
                       _rewardedAd != null &&
                       _rewardedAd.IsAdReady();
            }
        }
        public bool CanOfferRewardedHint
        {
            get
            {
                return disableAllAdsForTesting ||
                       (_sdkInitialized && _rewardedAd != null);
            }
        }

        public string RewardedHintStatusMessage
        {
            get
            {
                if (Application.internetReachability ==
                    NetworkReachability.NotReachable)
                {
                    return "No internet connection";
                }

                if (!_sdkInitialized || _rewardedAd == null)
                    return "Ads are still starting";

                if (_rewardedLoadInProgress)
                    return "Ad loading - try again shortly";

                if (_nextRewardedRetryAt >= 0f)
                    return "Ad unavailable - trying again soon";

                return "Ad unavailable - try again shortly";
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string GetDiagnosticState()
        {
            float bannerLoadAge = _bannerLoadInProgress
                ? Mathf.Max(0f, Time.unscaledTime - _bannerLoadStartedAt)
                : -1f;
            float rewardedLoadAge = _rewardedLoadInProgress
                ? Mathf.Max(0f, Time.unscaledTime - _rewardedLoadStartedAt)
                : -1f;
            float bannerRetryIn = _nextBannerRetryAt >= 0f
                ? Mathf.Max(0f, _nextBannerRetryAt - Time.unscaledTime)
                : -1f;
            float rewardedRetryIn = _nextRewardedRetryAt >= 0f
                ? Mathf.Max(0f, _nextRewardedRetryAt - Time.unscaledTime)
                : -1f;

            return
                $"AdsManager session: {_adSessionId}\n" +
                $"SDK initialized: {_sdkInitialized}\n" +
                $"Ads disabled for testing: {disableAllAdsForTesting}\n" +
                $"Remove Ads owned: {AdEntitlement.AdsRemoved}\n" +
                $"Network: {Application.internetReachability}, transitions={_networkTransitionCount}, recoveryIn={(_networkRecoveryAt >= 0f ? Mathf.Max(0f, _networkRecoveryAt - Time.unscaledTime) : -1f):0.0}s\n" +
                "Banner:\n" +
                $"  enabled={enableBannerAds}, idAssigned={!string.IsNullOrWhiteSpace(CurrentBannerAdUnitId)}, capped={_bannerPlacementCapped}\n" +
                $"  object={_bannerAd != null}, loading={_bannerLoadInProgress}, loadAge={bannerLoadAge:0.0}s\n" +
                $"  displayedBefore={_bannerHasDisplayed}, presented={_bannerPresented}, attempts={_bannerLoadAttemptCount}\n" +
                $"  failures={_bannerConsecutiveFailures}, retryIn={bannerRetryIn:0.0}s\n" +
                $"  last={_lastBannerEvent}\n" +
                $"  viewport={_appliedBannerViewport.Width}x{_appliedBannerViewport.Height}, bottomInset={_appliedBannerViewport.BottomInset}, safeArea={_appliedBannerViewport.RespectSafeArea}\n" +
                "Rewarded hint:\n" +
                $"  object={_rewardedAd != null}, offer={CanOfferRewardedHint}, ready={IsRewardedHintReady}\n" +
                $"  loading={_rewardedLoadInProgress}, loadAge={rewardedLoadAge:0.0}s, showing={_rewardedAdIsShowing}\n" +
                $"  failures={_rewardedConsecutiveFailures}, retryIn={rewardedRetryIn:0.0}s\n" +
                $"  last={_lastRewardedEvent}";
        }

        public void DebugLaunchTestSuite()
        {
            if (!_sdkInitialized)
            {
                AdDiagnostics.Record(
                    "sdk",
                    "test suite requested before LevelPlay initialized");
                return;
            }

            AdDiagnostics.Record("sdk", "launching LevelPlay test suite");
            LevelPlay.LaunchTestSuite();
        }
        public void DebugRetryBanner()
        {
            _lastBannerEvent = "manual developer retry";
            AdDiagnostics.Record("banner", _lastBannerEvent);
            _bannerMissingSince =
                Time.unscaledTime - BannerMissingWatchdogSeconds;
            _nextBannerRetryAt = -1f;
            _bannerLoadInProgress = false;
            if (_sdkInitialized && !_bannerPlacementCapped)
                RebuildBannerForViewport(CaptureBannerViewportState());
        }

        public void DebugRetryRewarded()
        {
            _lastRewardedEvent = "manual developer retry";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            _rewardedLoadInProgress = false;
            _nextRewardedRetryAt = -1f;
            if (_sdkInitialized && _rewardedAd == null &&
                !string.IsNullOrWhiteSpace(CurrentRewardedAdUnitId))
            {
                CreateRewardedAd();
            }

            LoadRewardedAd();
        }
#endif


        private string CurrentAppKey
        {
            get
            {
#if UNITY_IOS
                return iosAppKey;
#else
                return androidAppKey;
#endif
            }
        }

        private string CurrentRewardedAdUnitId
        {
            get
            {
#if UNITY_IOS
                return iosRewardedAdUnitId;
#else
                return androidRewardedAdUnitId;
#endif
            }
        }

        private string CurrentBannerAdUnitId
        {
            get
            {
#if UNITY_IOS
        return iosBannerAdUnitId;
#else
                return androidBannerAdUnitId;
#endif
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _lastNetworkReachability = Application.internetReachability;

            CreateBannerDock();

            _adSessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

            Debug.Log(
                $"{AdLogPrefix}: Session started. " +
                $"Version={Application.version}, " +
                $"DebugBuild={Debug.isDebugBuild}, " +
                $"Network={Application.internetReachability}, " +
                $"BannerEnabled={enableBannerAds}, " +
                $"BannerIdAssigned=" +
                $"{!string.IsNullOrWhiteSpace(CurrentBannerAdUnitId)}, " +
                $"AdsRemoved={AdEntitlement.AdsRemoved}, " +
                $"AdsDisabledForTesting={disableAllAdsForTesting}."
            );

        }

        public static float GetBottomSafeAreaPanelHeight(
            VisualElement panelRoot)
        {
            if (panelRoot == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
            {
                return 0f;
            }

            float panelHeight = panelRoot.resolvedStyle.height;
            if (float.IsNaN(panelHeight) || panelHeight <= 0f)
                panelHeight = panelRoot.contentRect.height;
            if (float.IsNaN(panelHeight) || panelHeight <= 0f)
                return 0f;

            return Screen.safeArea.yMin / Screen.height * panelHeight;
        }

        public static float GetBannerBottomInsetPanelHeight(
            VisualElement panelRoot)
        {
            if (panelRoot == null || Screen.height <= 0)
                return 0f;

            float panelHeight = panelRoot.resolvedStyle.height;
            if (float.IsNaN(panelHeight) || panelHeight <= 0f)
                panelHeight = panelRoot.contentRect.height;
            if (float.IsNaN(panelHeight) || panelHeight <= 0f)
                return 0f;

            float bottomInsetPixels =
                Instance != null && Instance._hasAppliedBannerViewport
                    ? Instance._appliedBannerViewport.BottomInset
                    : Screen.safeArea.yMin;

            return bottomInsetPixels / Screen.height * panelHeight;
        }

        public static float GetBannerContentInset(
            VisualElement panelRoot,
            bool includeBottomSafeArea)
        {
            if (!IsBannerPresented)
                return 0f;

            return BannerDockBaseInset +
                   (includeBottomSafeArea
                       ? GetBannerBottomInsetPanelHeight(panelRoot)
                       : 0f);
        }

        private void CreateBannerDock()
        {
            if (bannerDockVisualTree == null ||
                bannerDockPanelSettings == null)
            {
                Debug.LogWarning(
                    "AdsManager: Banner dock UI assets have not been assigned.");
                return;
            }

            GameObject dockObject = new GameObject("BannerDockUI");
            dockObject.transform.SetParent(transform, false);
            dockObject.SetActive(false);

            try
            {
                _bannerDockDocument = dockObject.AddComponent<UIDocument>();
                _bannerDockDocument.panelSettings = bannerDockPanelSettings;
                _bannerDockDocument.visualTreeAsset = bannerDockVisualTree;
                dockObject.SetActive(true);
                _bannerDockController = new BannerDockController(
                    _bannerDockDocument.rootVisualElement);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "AdsManager: Failed to create the banner dock UI.");
                Debug.LogException(exception);
                Destroy(dockObject);
                _bannerDockDocument = null;
                _bannerDockController = null;
            }
        }

        private void SetBannerPresentation(bool isPresented)
        {
            if (_bannerPresented == isPresented)
                return;

            _bannerPresented = isPresented;
            BannerPresentationChanged?.Invoke(isPresented);
        }

        private void Start()
        {
            if (AllAdsDisabled)
            {
                Debug.Log(
                    $"{AdLogPrefix}: Initialization skipped because " +
                    "ads are disabled for testing."
                );
                return;
            }

#if UNITY_EDITOR
            Debug.Log(
                "AdsManager: Skipping the consent form inside the Unity Editor; " +
                "test ads will still initialize.");
            _initializeAdsOnMainThread = true;
#else
            Debug.Log(
                $"{AdLogPrefix}: Waiting for the Google UMP consent gate."
            );
            AdConsentManager.EnsureConsentThenRun(() =>
            {
                // UMP may invoke this callback from a non-Unity thread.
                // Let Update continue the privacy sequence on Unity's main thread.
                _beginATTOnMainThread = true;
            });
#endif
        }

        private void Update()
        {
            if (_beginATTOnMainThread && !_attRequestStarted)
            {
                IOSATTAuthorizationStatus currentStatus =
                    IOSAppTrackingTransparency.CurrentStatus;
                bool promptRequired =
                    IOSAppTrackingTransparency.RequiresSystemPrompt(
                        currentStatus);
                bool onboardingReady =
                    PrivacyOnboardingState.HasAcknowledgedCurrentVersion;
                bool appReady =
                    _applicationFocused &&
                    !_applicationPaused &&
                    Application.isFocused;

                if (!promptRequired || (onboardingReady && appReady))
                {
                    if (_attEarliestRequestAt < 0f)
                    {
                        _attEarliestRequestAt =
                            Time.unscaledTime + 1f;
                    }

                    if (!promptRequired ||
                        Time.unscaledTime >= _attEarliestRequestAt)
                    {
                        _beginATTOnMainThread = false;
                        _attRequestStarted = true;

                        Debug.Log(
                            $"{AdLogPrefix}: UMP gate passed; resolving " +
                            "Apple tracking authorization before LevelPlay.");

                        IOSAppTrackingTransparency.RequestIfNeeded(status =>
                        {
                            bool trackingAllowed =
                                IOSAppTrackingTransparency.AllowsTracking(
                                    status);

                            Debug.Log(
                                $"{AdLogPrefix}: ATT resolved as {status}. " +
                                $"TrackingAllowed={trackingAllowed}. " +
                                "LevelPlay may now initialize.");

                            _initializeAdsOnMainThread = true;
                        });
                    }
                }
            }

            if (_initializeAdsOnMainThread)
            {
                _initializeAdsOnMainThread = false;

                Debug.Log(
                    $"{AdLogPrefix}: Privacy gates passed; starting " +
                    "LevelPlay initialization on the Unity main thread."
                );

                try
                {
                    InitializeAds();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "AdsManager: Exception while initializing LevelPlay:\n" +
                        exception
                    );
                }
            }

            TickNetworkRecovery();
            TickBannerRetry();
            TickBannerViewport();
            TickAdWatchdogs();
        }

        private void TickNetworkRecovery()
        {
            if (_applicationPaused || !_applicationFocused)
                return;

            NetworkReachability current =
                Application.internetReachability;
            if (current != _lastNetworkReachability)
            {
                NetworkReachability previous = _lastNetworkReachability;
                _lastNetworkReachability = current;
                _networkTransitionCount++;

                string transition =
                    $"network {previous} -> {current}";
                AdDiagnostics.Record("network", transition);

                if (current == NetworkReachability.NotReachable)
                {
                    _networkRecoveryAt = -1f;
                    return;
                }

                _networkRecoveryAt =
                    Time.unscaledTime + NetworkRecoveryDelaySeconds;
                _lastRewardedEvent =
                    "network restored; recovery pending";
                _lastBannerEvent = _lastRewardedEvent;
                NotifyRewardedAvailabilityChanged();
            }

            if (_networkRecoveryAt < 0f ||
                Time.unscaledTime < _networkRecoveryAt)
            {
                return;
            }

            _networkRecoveryAt = -1f;
            RecoverAdsAfterNetworkRestored();
        }

        private void RecoverAdsAfterNetworkRestored()
        {
            if (!_sdkInitialized)
                return;

            AdDiagnostics.Record(
                "network",
                "network stable; requesting one ad recovery pass");

            if (!IsRewardedHintReady && !_rewardedAdIsShowing)
            {
                _rewardedLoadInProgress = false;
                _rewardedLoadStartedAt = -1f;
                _nextRewardedRetryAt = -1f;
                _rewardedConsecutiveFailures = 0;
                LoadRewardedAd();
            }

            if (!BannerAdsDisabled &&
                enableBannerAds &&
                !_bannerPlacementCapped &&
                !_bannerPresented)
            {
                _bannerLoadInProgress = false;
                _bannerLoadStartedAt = -1f;
                _nextBannerRetryAt = -1f;
                _bannerConsecutiveFailures = 0;

                if (_bannerAd == null)
                    CreateBannerAd();

                LoadBannerAd();
            }
        }

        private void TickAdWatchdogs()
        {
            if (_applicationPaused || !_applicationFocused)
                return;

            if (_rewardedLoadInProgress &&
                Time.unscaledTime - _rewardedLoadStartedAt >=
                RewardedLoadTimeoutSeconds)
            {
                _rewardedLoadInProgress = false;
                _rewardedLoadStartedAt = -1f;
                _rewardedConsecutiveFailures++;
                _lastRewardedEvent = "load timeout";
                AdDiagnostics.Record("rewarded", _lastRewardedEvent);
                NotifyRewardedAvailabilityChanged();
                ScheduleRewardedRetry();
            }

            if (_nextRewardedRetryAt >= 0f &&
                Time.unscaledTime >= _nextRewardedRetryAt)
            {
                _nextRewardedRetryAt = -1f;
                LoadRewardedAd();
            }

            if (_bannerLoadInProgress &&
                Time.unscaledTime - _bannerLoadStartedAt >=
                BannerLoadTimeoutSeconds)
            {
                _bannerLoadInProgress = false;
                _bannerConsecutiveFailures++;
                _lastBannerEvent = "load timeout";
                AdDiagnostics.Record("banner", _lastBannerEvent);
                DestroyBannerAd();
                CreateBannerAd();
                ScheduleBannerRetry("load timed out");
                return;
            }

            bool bannerEligible = _sdkInitialized &&
                                  !BannerAdsDisabled &&
                                  !_bannerPlacementCapped &&
                                  enableBannerAds &&
                                  !string.IsNullOrWhiteSpace(
                                      CurrentBannerAdUnitId);

            if (!bannerEligible || _bannerPresented ||
                _bannerLoadInProgress || _nextBannerRetryAt >= 0f)
            {
                _bannerMissingSince = -1f;
                return;
            }

            if (_bannerMissingSince < 0f)
            {
                _bannerMissingSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _bannerMissingSince >=
                BannerMissingWatchdogSeconds)
            {
                _bannerMissingSince = Time.unscaledTime;
                _lastBannerEvent =
                    "eligible banner missing; watchdog retry";
                AdDiagnostics.Record("banner", _lastBannerEvent);

                if (_bannerAd == null)
                    CreateBannerAd();

                LoadBannerAd();
            }
        }
        private void TickBannerRetry()
        {
            if (_nextBannerRetryAt < 0f ||
                _applicationPaused ||
                !_applicationFocused ||
                Time.unscaledTime < _nextBannerRetryAt)
            {
                return;
            }

            _nextBannerRetryAt = -1f;

            if (BannerAdsDisabled ||
                !enableBannerAds ||
                !_sdkInitialized ||
                _bannerAd == null)
            {
                Debug.Log(
                    $"{AdLogPrefix}: Scheduled banner retry cancelled. " +
                    $"BannerAdsDisabled={BannerAdsDisabled}, " +
                    $"BannerEnabled={enableBannerAds}, " +
                    $"SdkInitialized={_sdkInitialized}, " +
                    $"BannerCreated={_bannerAd != null}."
                );
                return;
            }

            LoadBannerAd();
        }

        private void TickBannerViewport()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_sdkInitialized || BannerAdsDisabled ||
                !enableBannerAds || _bannerAd == null ||
                _applicationPaused || !_applicationFocused ||
                Time.unscaledTime < _nextBannerViewportPollAt)
            {
                return;
            }

            _nextBannerViewportPollAt =
                Time.unscaledTime + BannerViewportPollSeconds;
            BannerViewportState current = CaptureBannerViewportState();

            if (!_hasObservedBannerViewport ||
                !_observedBannerViewport.Matches(current))
            {
                _observedBannerViewport = current;
                _hasObservedBannerViewport = true;
                _bannerViewportStableSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _bannerViewportStableSince <
                BannerViewportDebounceSeconds)
            {
                return;
            }

            if (!_hasAppliedBannerViewport)
            {
                ApplyBannerViewportState(current);
                return;
            }

            if (!_appliedBannerViewport.Matches(current))
                RebuildBannerForViewport(current);
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _applicationFocused = hasFocus;

            if (hasFocus)
                HandleApplicationResumed();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _applicationPaused = pauseStatus;

            if (!pauseStatus)
                HandleApplicationResumed();
        }

        private void HandleApplicationResumed()
        {
            RequestBannerViewportCheck();
            RequestAdRecoveryCheck();
        }

        private void RequestBannerViewportCheck()
        {
            _nextBannerViewportPollAt = 0f;
            _hasObservedBannerViewport = false;
        }

        private void RequestAdRecoveryCheck()
        {
            NetworkReachability current =
                Application.internetReachability;
            _lastNetworkReachability = current;

            if (current == NetworkReachability.NotReachable)
            {
                _networkRecoveryAt = -1f;
                return;
            }

            _networkRecoveryAt =
                Time.unscaledTime + NetworkRecoveryDelaySeconds;
            _lastRewardedEvent = "app resumed; recovery pending";
            _lastBannerEvent = _lastRewardedEvent;
            AdDiagnostics.Record("network", _lastRewardedEvent);
            NotifyRewardedAvailabilityChanged();
        }

        private void InitializeAds()
        {
            if (_sdkInitialized)
            {
                Debug.Log("AdsManager: LevelPlay is already initialized.");
                return;
            }

            Debug.Log("AdsManager: InitializeAds entered.");

            if (AllAdsDisabled)
            {
                Debug.Log("AdsManager: Ads are disabled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentAppKey))
            {
                Debug.LogError(
                    "AdsManager: LevelPlay app key has not been assigned."
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentRewardedAdUnitId))
            {
                Debug.LogWarning(
                    "AdsManager: Rewarded ad unit ID has not been assigned."
                );
            }

            if (enableBannerAds &&
    string.IsNullOrWhiteSpace(CurrentBannerAdUnitId))
            {
                Debug.LogWarning(
                    "AdsManager: Banner ad unit ID has not been assigned."
                );
            }

            LevelPlay.OnInitSuccess += OnLevelPlayInitialized;
            LevelPlay.OnInitFailed += OnLevelPlayInitializationFailed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LevelPlay.SetMetaData("is_test_suite", "enable");
#else
            if (enableIntegrationTestSuite)
                LevelPlay.SetMetaData("is_test_suite", "enable");
#endif

            // Shikaku City is not child-directed unless you intentionally publish it that way.
            LevelPlayPrivacySettings.SetCOPPA(false);

            Debug.Log("AdsManager: Initializing LevelPlay.");

            LevelPlay.Init(CurrentAppKey);
        }

        private void OnLevelPlayInitialized(
            LevelPlayConfiguration configuration)
        {
            _sdkInitialized = true;
            AdDiagnostics.Record("sdk", "LevelPlay initialized");
            NotifyRewardedAvailabilityChanged();

            Debug.Log(
                $"{AdLogPrefix}: LevelPlay initialized successfully. " +
                $"BannerEligible=" +
                $"{!AdEntitlement.AdsRemoved && enableBannerAds && !string.IsNullOrWhiteSpace(CurrentBannerAdUnitId)}, " +
                $"AdsRemoved={AdEntitlement.AdsRemoved}, " +
                $"BannerEnabled={enableBannerAds}, " +
                $"BannerIdAssigned=" +
                $"{!string.IsNullOrWhiteSpace(CurrentBannerAdUnitId)}."
            );

            if (!string.IsNullOrWhiteSpace(CurrentRewardedAdUnitId))
            {
                CreateRewardedAd();
                LoadRewardedAd();
            }

            if (!AdEntitlement.AdsRemoved &&
                enableBannerAds &&
    !string.IsNullOrWhiteSpace(CurrentBannerAdUnitId))
            {
                CreateBannerAd();
                LoadBannerAd();
            }

            // Uncomment temporarily when you want to open LevelPlay's
            // full integration test screen on a development build.
            //
            // if (enableIntegrationTestSuite)
            //     LevelPlay.LaunchTestSuite();
        }

        private void OnLevelPlayInitializationFailed(
            LevelPlayInitError error)
        {
            int errorCode = error?.ErrorCode ?? -1;
            string errorMessage = error?.ErrorMessage ?? "Unknown";

            Debug.LogError(
                $"{AdLogPrefix}: LevelPlay initialization failed. " +
                $"Code={errorCode}, Message={errorMessage}. " +
                "No banner can load during this session."
            );

            _sdkInitialized = false;
            SetBannerPresentation(false);
            _lastBannerEvent = $"SDK init failed: {errorCode} {errorMessage}";
            _lastRewardedEvent = _lastBannerEvent;
            AdDiagnostics.Record("sdk", _lastBannerEvent);
            NotifyRewardedAvailabilityChanged();

        }

        private void CreateRewardedAd()
        {
            if (_rewardedAd != null)
                return;

            _rewardedAd =
                new LevelPlayRewardedAd(CurrentRewardedAdUnitId);

            _rewardedAd.OnAdLoaded += OnRewardedAdLoaded;
            _rewardedAd.OnAdLoadFailed += OnRewardedAdLoadFailed;
            _rewardedAd.OnAdDisplayed += OnRewardedAdDisplayed;
            _rewardedAd.OnAdDisplayFailed += OnRewardedAdDisplayFailed;
            _rewardedAd.OnAdRewarded += OnRewardedAdRewarded;
            _rewardedAd.OnAdClosed += OnRewardedAdClosed;
            _rewardedAd.OnAdClicked += OnRewardedAdClicked;

            _lastRewardedEvent = "rewarded object created";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            NotifyRewardedAvailabilityChanged();
        }

        private void LoadRewardedAd()
        {
            if (!_sdkInitialized ||
                _rewardedAd == null ||
                _rewardedLoadInProgress ||
                _rewardedAdIsShowing ||
                IsRewardedHintReady)
            {
                return;
            }

            if (Application.internetReachability ==
                NetworkReachability.NotReachable)
            {
                _nextRewardedRetryAt = -1f;
                _lastRewardedEvent = "waiting for network";
                AdDiagnostics.Record("rewarded", _lastRewardedEvent);
                NotifyRewardedAvailabilityChanged();
                return;
            }

            _nextRewardedRetryAt = -1f;
            _rewardedLoadInProgress = true;
            _rewardedLoadStartedAt = Time.unscaledTime;
            _lastRewardedEvent = "loading";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            NotifyRewardedAvailabilityChanged();

            try
            {
                _rewardedAd.LoadAd();
            }
            catch (Exception exception)
            {
                _rewardedLoadInProgress = false;
                _rewardedLoadStartedAt = -1f;
                _rewardedConsecutiveFailures++;
                _lastRewardedEvent =
                    $"LoadAd exception: {exception.Message}";
                AdDiagnostics.Record("rewarded", _lastRewardedEvent);
                NotifyRewardedAvailabilityChanged();
                ScheduleRewardedRetry();
            }
        }

        private void ScheduleRewardedRetry()
        {
            if (!_sdkInitialized || _rewardedAd == null ||
                _rewardedAdIsShowing || IsRewardedHintReady)
            {
                return;
            }

            int index = Mathf.Clamp(
                _rewardedConsecutiveFailures - 1,
                0,
                RewardedRetryDelaysSeconds.Length - 1);
            float delay = RewardedRetryDelaysSeconds[index];
            _nextRewardedRetryAt = Time.unscaledTime + delay;
            _lastRewardedEvent = $"retry scheduled in {delay:0}s";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
        }

        private static void NotifyRewardedAvailabilityChanged()
        {
            RewardedAvailabilityChanged?.Invoke();
        }
        /// <summary>
        /// Shows a rewarded ad. The supplied callback runs only when
        /// LevelPlay confirms that the player earned the reward.
        /// </summary>
        public bool ShowRewardedHint(Action grantHint) =>
            ShowRewarded(
                hintPlacementName,
                "hint",
                grantHint);

        public bool ShowRewardedStreakRestore(Action grantRestoration) =>
            ShowRewarded(
                streakRestorePlacementName,
                "streak_restore",
                grantRestoration);

        private bool ShowRewarded(
            string placementName,
            string rewardType,
            Action grantReward)
        {
            if (disableAllAdsForTesting)
            {
                Debug.Log("AdsManager: Rewarded ads are disabled for testing.");

                GameAnalytics.RewardedAdRequested(placementName);
                GameAnalytics.RewardedAdCompleted(
                    placementName,
                    rewardType);
                // Grant the selected reward immediately during development.
                grantReward?.Invoke();
                return true;
            }

            if (grantReward == null)
            {
                Debug.LogWarning(
                    "AdsManager: No rewarded-ad callback was supplied."
                );
                return false;
            }

            if (string.IsNullOrWhiteSpace(placementName))
            {
                Debug.LogWarning(
                    "AdsManager: No rewarded-ad placement was supplied.");
                return false;
            }

            if (_rewardedAdIsShowing)
            {
                Debug.LogWarning(
                    "AdsManager: A rewarded ad is already showing."
                );
                return false;
            }

            if (!IsRewardedHintReady)
            {
                Debug.LogWarning(
                    "AdsManager: Rewarded ad is not ready."
                );

                _lastRewardedEvent = "reward tapped while ad was not ready";
                AdDiagnostics.Record("rewarded", _lastRewardedEvent);

                if (!_rewardedLoadInProgress && _nextRewardedRetryAt < 0f)
                    LoadRewardedAd();

                return false;
            }

            if (LevelPlayRewardedAd.IsPlacementCapped(
                    placementName))
            {
                Debug.LogWarning(
                    $"AdsManager: Placement '{placementName}' is capped."
                );
                return false;
            }

            _rewardedAdIsShowing = true;
            _pendingRewardAction = grantReward;
            _pendingRewardPlacementName = placementName;
            _pendingRewardType = rewardType;
            _lastRewardedEvent = "show requested";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            NotifyRewardedAvailabilityChanged();
            _rewardGrantedForCurrentAd = false;
            GameAnalytics.RewardedAdRequested(placementName);

            Debug.Log(
                $"AdsManager: Showing rewarded ad for '{rewardType}'.");

            _rewardedAd.ShowAd(placementName);
            return true;
        }

        private void OnRewardedAdLoaded(LevelPlayAdInfo adInfo)
        {
            _rewardedLoadInProgress = false;
            _rewardedConsecutiveFailures = 0;
            _nextRewardedRetryAt = -1f;
            _lastRewardedEvent = "ready";
            _rewardedLoadStartedAt = -1f;
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            NotifyRewardedAvailabilityChanged();
        }

        private void OnRewardedAdLoadFailed(LevelPlayAdError error)
        {
            _rewardedLoadInProgress = false;
            _rewardedConsecutiveFailures++;
            _lastRewardedEvent = $"load failed: {error}";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            _rewardedLoadStartedAt = -1f;
            NotifyRewardedAvailabilityChanged();
            ScheduleRewardedRetry();
        }

        private void OnRewardedAdDisplayed(LevelPlayAdInfo adInfo)
        {
            _lastRewardedEvent = "displayed";
            AdReportService.RecordDisplayedAd(
                "rewarded",
                _pendingRewardPlacementName,
                adInfo);
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            NotifyRewardedAvailabilityChanged();
        }

        private void OnRewardedAdDisplayFailed(
    LevelPlayAdInfo adInfo,
    LevelPlayAdError error)
        {
            Debug.LogWarning(
                $"AdsManager: Rewarded ad failed to display: {error}"
            );

            _rewardedAdIsShowing = false;
            _lastRewardedEvent = $"display failed: {error}";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            ClearPendingReward();
            NotifyRewardedAvailabilityChanged();
            LoadRewardedAd();
        }

        private void OnRewardedAdRewarded(
            LevelPlayAdInfo adInfo,
            LevelPlayReward reward)
        {
            // Protect against an accidental duplicate reward callback.
            if (_rewardGrantedForCurrentAd)
                return;

            _rewardGrantedForCurrentAd = true;

            Debug.Log(
                $"AdsManager: Reward earned. " +
                $"{reward.Name}: {reward.Amount}"
            );

            string placementName = string.IsNullOrEmpty(
                _pendingRewardPlacementName)
                    ? hintPlacementName
                    : _pendingRewardPlacementName;
            string rewardType = string.IsNullOrEmpty(_pendingRewardType)
                ? "hint"
                : _pendingRewardType;
            GameAnalytics.RewardedAdCompleted(
                placementName,
                rewardType);
            Action rewardAction = _pendingRewardAction;
            _pendingRewardAction = null;
            rewardAction?.Invoke();
        }

        private void OnRewardedAdClosed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("AdsManager: Rewarded ad closed.");

            _rewardedAdIsShowing = false;
            _lastRewardedEvent = "closed; reloading";
            AdDiagnostics.Record("rewarded", _lastRewardedEvent);
            NotifyRewardedAvailabilityChanged();

            // OnAdRewarded can occur after OnAdClosed, so only clear here
            // when the reward callback already occurred.
            if (_rewardGrantedForCurrentAd)
                ClearPendingReward();

            LoadRewardedAd();
        }

        private void OnRewardedAdClicked(LevelPlayAdInfo adInfo)
        {
            Debug.Log("AdsManager: Rewarded ad clicked.");
        }

        private void ClearPendingReward()
        {
            _pendingRewardAction = null;
            _pendingRewardPlacementName = string.Empty;
            _pendingRewardType = string.Empty;
            _rewardGrantedForCurrentAd = false;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SetBannerPresentation(false);
            _bannerDockController?.Dispose();
            _bannerDockController = null;
            _bannerDockDocument = null;
            _nextBannerRetryAt = -1f;
            _bannerLoadInProgress = false;

            LevelPlay.OnInitSuccess -= OnLevelPlayInitialized;
            LevelPlay.OnInitFailed -= OnLevelPlayInitializationFailed;

            if (_rewardedAd != null)
            {
                _rewardedAd.OnAdLoaded -= OnRewardedAdLoaded;
                _rewardedAd.OnAdLoadFailed -= OnRewardedAdLoadFailed;
                _rewardedAd.OnAdDisplayed -= OnRewardedAdDisplayed;
                _rewardedAd.OnAdDisplayFailed -=
                    OnRewardedAdDisplayFailed;
                _rewardedAd.OnAdRewarded -= OnRewardedAdRewarded;
                _rewardedAd.OnAdClosed -= OnRewardedAdClosed;
                _rewardedAd.OnAdClicked -= OnRewardedAdClicked;
            }

            DestroyBannerAd();

            Instance = null;
        }

        private void CreateBannerAd()
        {
            if (_bannerAd != null)
                return;

            if (!_hasAppliedBannerViewport)
                ApplyBannerViewportState(CaptureBannerViewportState());

            var bannerConfig =
                new LevelPlayBannerAd.Config.Builder()
                    .SetSize(LevelPlayAdSize.BANNER)
                    .SetPosition(LevelPlayBannerPosition.BottomCenter)
                    .SetDisplayOnLoad(true)
                    .SetRespectSafeArea(_bannerRespectsSafeArea)
                    .SetPlacementName(bannerPlacementName)
                    .Build();

            _bannerAd = new LevelPlayBannerAd(
                CurrentBannerAdUnitId,
                bannerConfig
            );

            Debug.Log(
                $"{AdLogPrefix}: Banner object created. " +
                $"Placement={bannerPlacementName}, " +
                "Size=BANNER, Position=BottomCenter, " +
                "DisplayOnLoad=True, " +
                $"RespectSafeArea={_bannerRespectsSafeArea}, " +
                $"BottomInset={_appliedBannerViewport.BottomInset}px."
            );

            _bannerAd.OnAdLoaded += OnBannerAdLoaded;
            _bannerAd.OnAdLoadFailed += OnBannerAdLoadFailed;
            _bannerAd.OnAdDisplayed += OnBannerAdDisplayed;
            _bannerAd.OnAdDisplayFailed += OnBannerAdDisplayFailed;
            _bannerAd.OnAdClicked += OnBannerAdClicked;
            _bannerAd.OnAdCollapsed += OnBannerAdCollapsed;
            _bannerAd.OnAdLeftApplication += OnBannerAdLeftApplication;
            _bannerAd.OnAdExpanded += OnBannerAdExpanded;
        }

        private BannerViewportState CaptureBannerViewportState()
        {
            Rect safeArea = Screen.safeArea;
            var state = new BannerViewportState
            {
                Width = Screen.width,
                Height = Screen.height,
                SafeLeft = Mathf.RoundToInt(safeArea.xMin),
                SafeRight = Mathf.RoundToInt(Screen.width - safeArea.xMax),
                SafeTop = Mathf.RoundToInt(Screen.height - safeArea.yMax),
                BottomInset = Mathf.Max(0, Mathf.RoundToInt(safeArea.yMin)),
                RespectSafeArea = true
            };

#if UNITY_ANDROID && !UNITY_EDITOR
            if (TryGetAndroidNavigationBarState(
                    out bool navigationBarVisible,
                    out int navigationBarBottomInset))
            {
                state.BottomInset = navigationBarVisible
                    ? Mathf.Max(0, navigationBarBottomInset)
                    : 0;
                state.RespectSafeArea = navigationBarVisible;
            }
            else
            {
                state.RespectSafeArea = state.BottomInset > 1;
            }
#endif

            return state;
        }

        private bool TryGetAndroidNavigationBarState(
            out bool isVisible,
            out int bottomInset)
        {
            isVisible = false;
            bottomInset = 0;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass(
                           "com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>(
                           "currentActivity"))
                using (AndroidJavaObject window =
                       activity?.Call<AndroidJavaObject>("getWindow"))
                using (AndroidJavaObject decorView =
                       window?.Call<AndroidJavaObject>("getDecorView"))
                using (AndroidJavaObject rootInsets =
                       decorView?.Call<AndroidJavaObject>(
                           "getRootWindowInsets"))
                using (var insetType = new AndroidJavaClass(
                           "android.view.WindowInsets$Type"))
                {
                    if (rootInsets == null)
                        return false;

                    int navigationBars =
                        insetType.CallStatic<int>("navigationBars");
                    isVisible = rootInsets.Call<bool>(
                        "isVisible",
                        navigationBars);

                    using (AndroidJavaObject insets =
                           rootInsets.Call<AndroidJavaObject>(
                               "getInsets",
                               navigationBars))
                    {
                        if (insets != null)
                            bottomInset = insets.Get<int>("bottom");
                    }

                    return true;
                }
            }
            catch (Exception exception)
            {
                if (!_androidInsetsQueryFailureLogged)
                {
                    _androidInsetsQueryFailureLogged = true;
                    Debug.LogWarning(
                        $"{AdLogPrefix}: Android WindowInsets query " +
                        $"unavailable; using Unity safe area. " +
                        exception.Message);
                }
            }
#endif

            return false;
        }

        private void ApplyBannerViewportState(BannerViewportState state)
        {
            _appliedBannerViewport = state;
            _hasAppliedBannerViewport = true;
            _bannerRespectsSafeArea = state.RespectSafeArea;
        }


        private void RebuildBannerForViewport(BannerViewportState state)
        {
            _lastBannerEvent =
                $"rebuild for viewport {state.Width}x{state.Height}, " +
                $"inset {state.BottomInset}";
            AdDiagnostics.Record("banner", _lastBannerEvent);
            Debug.Log(
                $"{AdLogPrefix}: Rebuilding banner after viewport change. " +
                $"Screen={state.Width}x{state.Height}, " +
                $"BottomInset={state.BottomInset}px, " +
                $"RespectSafeArea={state.RespectSafeArea}."
            );

            SetBannerPresentation(false);
            _nextBannerRetryAt = -1f;
            _bannerLoadInProgress = false;
            _bannerLoadStartedAt = -1f;
            _bannerHasDisplayed = false;

            DestroyBannerAd();
            ApplyBannerViewportState(state);
            CreateBannerAd();
            LoadBannerAd();
        }

        private void DestroyBannerAd()
        {
            if (_bannerAd == null)
                return;

            _bannerAd.OnAdLoaded -= OnBannerAdLoaded;
            _bannerAd.OnAdLoadFailed -= OnBannerAdLoadFailed;
            _bannerAd.OnAdDisplayed -= OnBannerAdDisplayed;
            _bannerAd.OnAdDisplayFailed -= OnBannerAdDisplayFailed;
            _bannerAd.OnAdClicked -= OnBannerAdClicked;
            _bannerAd.OnAdCollapsed -= OnBannerAdCollapsed;
            _bannerAd.OnAdLeftApplication -= OnBannerAdLeftApplication;
            _bannerAd.OnAdExpanded -= OnBannerAdExpanded;

            _bannerAd.PauseAutoRefresh();
            _bannerAd.HideAd();
            _bannerAd.DestroyAd();
            _bannerAd = null;
        }

        private void LoadBannerAd()
        {
            if (BannerAdsDisabled ||
                !_sdkInitialized ||
                !enableBannerAds ||
                _bannerAd == null ||
                _bannerPlacementCapped ||
                _bannerLoadInProgress)
            {
                return;
            }

            if (Application.internetReachability ==
                NetworkReachability.NotReachable)
            {
                _nextBannerRetryAt = -1f;
                _lastBannerEvent = "waiting for network";
                AdDiagnostics.Record("banner", _lastBannerEvent);
                return;
            }

            _nextBannerRetryAt = -1f;
            _bannerLoadInProgress = true;
            _bannerLoadStartedAt = Time.unscaledTime;
            _bannerLoadAttemptCount++;
            _lastBannerEvent =
                $"loading attempt {_bannerLoadAttemptCount}";
            AdDiagnostics.Record("banner", _lastBannerEvent);

            Debug.Log(
                $"{AdLogPrefix}: Loading banner ad. " +
                $"Attempt={_bannerLoadAttemptCount}, " +
                $"ConsecutiveFailures={_bannerConsecutiveFailures}, " +
                $"Network={Application.internetReachability}."
            );

            try
            {
                _bannerAd.LoadAd();
            }
            catch (Exception exception)
            {
                _bannerLoadInProgress = false;
                _bannerLoadStartedAt = -1f;
                _bannerConsecutiveFailures++;
                _lastBannerEvent =
                    $"LoadAd exception: {exception.Message}";
                AdDiagnostics.Record("banner", _lastBannerEvent);

                Debug.LogException(exception);
                ScheduleBannerRetry("LoadAd threw an exception");
            }
        }

        private void ScheduleBannerRetry(string reason)
        {
            if (BannerAdsDisabled ||
                !enableBannerAds ||
                !_sdkInitialized ||
                _bannerAd == null ||
                _bannerPlacementCapped)
            {
                return;
            }

            int delayIndex = Mathf.Clamp(
                _bannerConsecutiveFailures - 1,
                0,
                BannerRetryDelaysSeconds.Length - 1);
            float delaySeconds = BannerRetryDelaysSeconds[delayIndex];

            _nextBannerRetryAt = Time.unscaledTime + delaySeconds;
            _lastBannerEvent =
                $"retry in {delaySeconds:0}s: {reason}";
            AdDiagnostics.Record("banner", _lastBannerEvent);

            Debug.LogWarning(
                $"{AdLogPrefix}: Banner retry scheduled in " +
                $"{delaySeconds:0} seconds. Reason={reason}, " +
                $"ConsecutiveFailures={_bannerConsecutiveFailures}."
            );
        }

        public void ShowBanner()
        {
            if (BannerAdsDisabled || !enableBannerAds || _bannerAd == null)
                return;

            _bannerAd.ShowAd();
            _bannerAd.ResumeAutoRefresh();
        }

        public void HideBanner()
        {
            SetBannerPresentation(false);

            if (_bannerAd == null)
                return;

            _bannerAd.PauseAutoRefresh();
            _bannerAd.HideAd();
        }

        private void OnBannerAdLoaded(LevelPlayAdInfo adInfo)
        {
            _bannerLoadInProgress = false;
            _bannerLoadStartedAt = -1f;
            _bannerConsecutiveFailures = 0;
            _nextBannerRetryAt = -1f;
            _lastBannerEvent = "loaded";
            AdDiagnostics.Record("banner", _lastBannerEvent);

            Debug.Log(
                $"{AdLogPrefix}: Banner ad loaded. " +
                $"Attempt={_bannerLoadAttemptCount}, " +
                $"Network={adInfo?.AdNetwork ?? "Unknown"}, " +
                $"Instance={adInfo?.InstanceName ?? "Unknown"}, " +
                $"Country={adInfo?.Country ?? "Unknown"}, " +
                $"AuctionId={adInfo?.AuctionId ?? "Unknown"}."
            );
        }

        private void OnBannerAdLoadFailed(LevelPlayAdError error)
        {
            bool wasInitialLoad = !_bannerHasDisplayed;
            int errorCode = error?.ErrorCode ?? -1;
            string errorMessage = error?.ErrorMessage ?? "Unknown";
            _bannerLoadInProgress = false;
            _bannerLoadStartedAt = -1f;
            _lastBannerEvent =
                $"load failed {errorCode}: {errorMessage}";
            AdDiagnostics.Record("banner", _lastBannerEvent);

            if (wasInitialLoad)
                SetBannerPresentation(false);

            Debug.LogWarning(
                $"{AdLogPrefix}: Banner ad failed to load. " +
                $"Attempt={_bannerLoadAttemptCount}, " +
                $"Code={errorCode}, " +
                $"Message={errorMessage}, " +
                $"InitialLoad={wasInitialLoad}."
            );

            if (!wasInitialLoad)
            {
                Debug.Log(
                    $"{AdLogPrefix}: Leaving recovery of a banner " +
                    "auto-refresh failure to LevelPlay."
                );
                return;
            }

            if (errorCode == BannerPlacementCappedErrorCode)
            {
                _bannerPlacementCapped = true;
                _lastBannerEvent = "placement capped for this session";
                AdDiagnostics.Record("banner", _lastBannerEvent);
                Debug.LogWarning(
                    $"{AdLogPrefix}: Banner placement is capped; " +
                    "no retry will be scheduled this session."
                );
                return;
            }

            _bannerConsecutiveFailures++;
            ScheduleBannerRetry(
                $"load failed with code {errorCode}");
        }

        private void OnBannerAdDisplayed(LevelPlayAdInfo adInfo)
        {
            _bannerHasDisplayed = true;
            _bannerLoadStartedAt = -1f;
            _bannerMissingSince = -1f;
            _bannerPlacementCapped = false;
            _bannerConsecutiveFailures = 0;
            _nextBannerRetryAt = -1f;
            _lastBannerEvent = "displayed";
            AdReportService.RecordDisplayedAd(
                "banner",
                bannerPlacementName,
                adInfo);
            AdDiagnostics.Record("banner", _lastBannerEvent);
            SetBannerPresentation(true);

            Debug.Log(
                $"{AdLogPrefix}: Banner ad displayed. " +
                $"Network={adInfo?.AdNetwork ?? "Unknown"}, " +
                $"Instance={adInfo?.InstanceName ?? "Unknown"}, " +
                $"Placement={adInfo?.PlacementName ?? "Unknown"}."
            );
        }

        private void OnBannerAdDisplayFailed(
            LevelPlayAdInfo adInfo,
            LevelPlayAdError error)
        {
            int errorCode = error?.ErrorCode ?? -1;
            string errorMessage = error?.ErrorMessage ?? "Unknown";
            _bannerLoadInProgress = false;
            _bannerLoadStartedAt = -1f;
            _bannerHasDisplayed = false;
            _bannerConsecutiveFailures++;
            _lastBannerEvent =
                $"display failed {errorCode}: {errorMessage}";
            AdDiagnostics.Record("banner", _lastBannerEvent);
            SetBannerPresentation(false);

            Debug.LogWarning(
                $"{AdLogPrefix}: Banner ad failed to display. " +
                $"Code={errorCode}, " +
                $"Message={errorMessage}, " +
                $"Network={adInfo?.AdNetwork ?? "Unknown"}."
            );

            ScheduleBannerRetry(
                $"display failed with code {errorCode}");
        }

        private void OnBannerAdClicked(LevelPlayAdInfo adInfo)
        {
            Debug.Log("AdsManager: Banner ad clicked.");
        }

        private void OnBannerAdCollapsed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("AdsManager: Banner ad collapsed.");
        }

        private void OnBannerAdLeftApplication(LevelPlayAdInfo adInfo)
        {
            Debug.Log(
                "AdsManager: Banner ad caused the application to open another app."
            );
        }

        private void OnBannerAdExpanded(LevelPlayAdInfo adInfo)
        {
            Debug.Log("AdsManager: Banner ad expanded.");
        }

        public void ApplyRemoveAdsPurchase()
        {
            AdEntitlement.GrantRemoveAds();

            SetBannerPresentation(false);
            _nextBannerRetryAt = -1f;
            _bannerLoadInProgress = false;

            DestroyBannerAd();

            Debug.Log(
                $"{AdLogPrefix}: Banner ads disabled " +
                "permanently because Remove Ads is owned."
            );
        }

        /// <summary>
        /// Re-enables banner ads after a successful Google Play purchase
        /// reconciliation reports that Remove Ads is no longer owned.
        /// </summary>
        public void ApplyRemoveAdsRevocation()
        {
            bool wasOwned = AdEntitlement.AdsRemoved;
            AdEntitlement.RevokeRemoveAds();

            if (!wasOwned)
                return;

            if (AllAdsDisabled)
            {
                Debug.Log(
                    $"{AdLogPrefix}: Remove Ads was revoked, but ads remain " +
                    "disabled for testing."
                );
                return;
            }

            if (!_sdkInitialized)
            {
                Debug.Log(
                    $"{AdLogPrefix}: Remove Ads was revoked. Banner ads " +
                    "will load after LevelPlay initialization."
                );
                return;
            }

            if (enableBannerAds &&
                !_bannerPlacementCapped &&
                !string.IsNullOrWhiteSpace(CurrentBannerAdUnitId))
            {
                _nextBannerRetryAt = -1f;
                _bannerLoadInProgress = false;
                _bannerLoadStartedAt = -1f;
                _bannerConsecutiveFailures = 0;

                if (_bannerAd == null)
                    CreateBannerAd();

                LoadBannerAd();
            }

            Debug.Log(
                $"{AdLogPrefix}: Remove Ads was revoked after Google Play " +
                "reconciliation. Banner ads are enabled again."
            );
        }
    }
}
