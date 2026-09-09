using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Shikaku.Ads;
using Shikaku.Store;
using Shikaku.Settings;
using Shikaku.Achievements;
using Shikaku.SaveSystem;
using Shikaku.Services;
using System.Collections;
using System.Collections.Generic;

namespace Shikaku.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameplayHUD : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private BoardController board;
        [SerializeField] private RectTransform boardContainer;
        [SerializeField] private UIDocument overlayDocument;

        [Header("Time Trial Hint Penalty")]
        [SerializeField] private Color hintPenaltyColor = Color.red;
        [SerializeField] private float hintPenaltyFlashDuration = 0.6f;
        [SerializeField] private float hintPenaltyPulseScale = 1.2f;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _overlayRoot;
        private VisualElement _safeArea;
        private VisualElement _boardSlot;
        private VisualElement _playCluster;
        private VisualElement _actions;
        private VisualElement _adSpacer;
        private VisualElement _bestStat;
        private VisualElement _bestDivider;
        private Label _bestCaption;
        private CanvasGroup _boardCanvasGroup;
        private Label cellProgressText;
        private Label _progressCaption;
        private Label timerText;
        private Label bestTimeText;
        private VisualElement _bestHintIcon;
        private Label titleText;
        private Label _subtitleText;
        private Button exitButton;
        private Button _nextPuzzleButton;
        private Button hintButton;
        private Button resetButton;
        private Button _backgroundButton;
        private Label _hintCountBadge;
        private VisualElement _hintVideoBadge;
        private Label _adStatus;
        private IVisualElementScheduledItem _hideAdStatusTask;
        private VisualElement _notQuiteWarning;
        private VisualElement _solvedModal;
        private VisualElement _ratePromptModal;
        private Button _ratePromptRateButton;
        private Button _ratePromptLaterButton;
        private Button _ratePromptBackdrop;
        private VisualElement _resetModal;
        private VisualElement _exitModal;
        private Label _solvedTitle;
        private Label solvedTimeText;
        private Button _solvedSameDifficultyButton;
        private Button _solvedPrimaryButton;
        private Button _solvedExitButton;
        private Button _confirmResetButton;
        private Button _cancelResetButton;
        private VisualElement _streakCelebration;
        private Label _streakCelebrationTitle;
        private VisualElement _streakCelebrationCountRow;
        private Label _streakCelebrationNumber;
        private Label _streakCelebrationReward;
        private Button _confirmExitButton;
        private Button _cancelExitButton;
        private Button _exitBackdrop;
        private Label _exitTitle;
        private Label _exitMessage;
        private Button _solvedBackdrop;
        private Button _resetBackdrop;
        private VisualElement _tutorialRoot;
        private VisualElement _tutorialCard;
        private VisualElement _tutorialCardRow;
        private VisualElement _tutorialProgress;
        private VisualElement _tutorialGuideTile;
        private VisualElement _tutorialActionBubble;
        private VisualElement _tutorialActionTail;
        private Label _tutorialGuideNumber;
        private Label _tutorialTitle;
        private Label _tutorialMessage;
        private Label _tutorialAction;
        private Button _tutorialPreviousButton;
        private Button _tutorialForwardButton;
        private Button _tutorialNextButton;
        private int _tutorialStep = -1;
        private int[] _atlasTutorialTargets;
        private readonly bool[] _tutorialStepsReached =
            new bool[TUTORIAL_STEP_COUNT];
        private bool _tutorialRunCompleted;
        private int _analyticsHintsUsedThisPuzzle;
        private bool _exitAnalyticsRecorded;
        private bool _timeTrialAnalyticsRecorded;
        private bool _tutorialSwipeTracking;
        private int _tutorialSwipePointerId = -1;
        private Vector2 _tutorialSwipeStart;
        private int _tutorialBubbleDirection = -1;
        private Vector2 _tutorialBubbleLastPosition =
            new Vector2(float.NaN, float.NaN);
        private Vector2 _tutorialCardLastPosition =
            new Vector2(float.NaN, float.NaN);
        private bool _responsiveLayoutQueued;
        private float _lastBoardSlotHeight = -1f;
        private Rect _lastAppliedBoardSlotRect =
            new Rect(float.NaN, float.NaN, float.NaN, float.NaN);

        private Coroutine _hintPenaltyFlashRoutine;
        private Coroutine _hintIdlePulseRoutine;
        private Coroutine _tutorialBuildTransitionRoutine;
        private Coroutine _tutorialIllegalMoveRoutine;
        private bool _tutorialFirstIllegalMoveComplete;
        private bool _tutorialIllegalMoveTransitioning;
        private Coroutine _streakCelebrationRoutine;
        private DailyStreakUpdate _pendingStreakUpdate;
        private string _pendingCompletionRewardTitle;
        private int _pendingCompletionHintReward;
        private Coroutine _tutorialSingleEraseRoutine;

        private float _elapsed;
        private bool _timerRunning = true;
        private float _remaining;
        private bool _countDown;
        private bool _timeTrialSummaryShown;
        private bool _puzzleInteractionLocked;
        private bool _notQuiteWarningActive;
        private float _notQuiteWarningCycleStartedAt;
        private float _nextHintIdlePulseAt;
        private const float TIME_TRIAL_HINT_COST_SECONDS = 10f;
        private const float NOT_QUITE_PHASE_SECONDS = 3f;
        private const float HINT_IDLE_SECONDS = 15f;
        private const float HINT_IDLE_PULSE_ON_SECONDS = 0.28f;
        private const float HINT_IDLE_PULSE_OFF_SECONDS = 0.24f;
        private const int HINT_IDLE_PULSE_COUNT = 4;
        private const int TUTORIAL_THREE_ANCHOR = 12;
        private const int TUTORIAL_THREE_NEAR = 13;
        private const int TUTORIAL_THREE_FAR = 14;
        private const int TUTORIAL_TWO_ANCHOR = 9;
        private const int TUTORIAL_ILLEGAL_ABOVE = 2;
        private const int TUTORIAL_ILLEGAL_CONNECTED_ANCHOR = 1;
        private const int TUTORIAL_ILLEGAL_LEFT = 5;
        private const int TUTORIAL_ILLEGAL_ANCHOR = 6;
        private const int TUTORIAL_SHARED_ANCHOR_LEFT = 0;   // (0,0) = 3
        private const int TUTORIAL_SHARED_ANCHOR_RIGHT = 1;  // (1,0) = 3
        private const int TUTORIAL_SHARED_FILL = 4;          // (0,1) = empty
        private const int TUTORIAL_STEP_COUNT = 7;
        private const float TUTORIAL_SWIPE_THRESHOLD = 70f;
        private const float TUTORIAL_CARD_GAP = 22f;
        private const float TUTORIAL_CARD_AD_GAP = 18f;
        private const float TUTORIAL_CARD_FALLBACK_HEIGHT = 330f;
        private const float COMPACT_LAYOUT_HEIGHT = 1350f;
        private const float COMPACT_LAYOUT_ASPECT = 1.55f;
        private const float TALL_PHONE_ASPECT = 1.78f;
        private const float TALL_PHONE_HORIZONTAL_PADDING = 0f;
        private const string UNAVAILABLE_VALUE = "\u2014";

        private static readonly int[] TUTORIAL_BUILD_TARGETS =
            { TUTORIAL_THREE_ANCHOR, TUTORIAL_THREE_NEAR, TUTORIAL_THREE_FAR };
        private static readonly int[] TUTORIAL_SINGLE_ERASE_TARGETS =
            { TUTORIAL_THREE_FAR };
        private static readonly int[] TUTORIAL_REGION_ERASE_TARGETS =
            { TUTORIAL_THREE_ANCHOR };
        private static readonly int[] TUTORIAL_SHARED_TARGETS =
            { TUTORIAL_SHARED_ANCHOR_LEFT, TUTORIAL_SHARED_ANCHOR_RIGHT, TUTORIAL_SHARED_FILL };
        private static readonly int[] TUTORIAL_ILLEGAL_LEFT_TARGETS =
            { TUTORIAL_ILLEGAL_ANCHOR, TUTORIAL_ILLEGAL_LEFT };
        private static readonly int[] TUTORIAL_ILLEGAL_ABOVE_TARGETS =
            { TUTORIAL_ILLEGAL_ANCHOR, TUTORIAL_ILLEGAL_ABOVE };
        private static readonly string[] TUTORIAL_STEP_NAMES =
        {
            "goal",
            "read_clue",
            "draw_rectangle",
            "match_area",
            "one_clue",
            "replace_region",
            "cover_board"
        };

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (board == null)
                board = FindObjectOfType<BoardController>();

            if (boardContainer != null)
            {
                _boardCanvasGroup = boardContainer.GetComponent<CanvasGroup>();
                if (_boardCanvasGroup == null)
                    _boardCanvasGroup = boardContainer.gameObject.AddComponent<CanvasGroup>();
            }

            BindUi();
            AdsManager.BannerPresentationChanged +=
                OnBannerPresentationChanged;
            ApplyBannerInset();

            ThemeManager.EnsureInitialized();
            ApplyTheme();
            ThemeManager.Changed += ApplyTheme;

            if (board != null)
            {
                board.PuzzleSolved -= OnPuzzleSolved;
                board.PuzzleSolved += OnPuzzleSolved;
                board.InputPerformed += OnBoardInputPerformed;
                board.BoardTouched += OnBoardTouched;
            }

            AdsManager.RewardedAvailabilityChanged -= RefreshHintButtonState;
            AdsManager.RewardedAvailabilityChanged += RefreshHintButtonState;

            HintWallet.BalanceChanged -= RefreshHintButtonState;
            HintWallet.BalanceChanged += RefreshHintButtonState;

            HideAllModals();
            RegisterCallbacks();
            ResetHintIdleCountdown();
        }

        private void OnDestroy()
        {
            ThemeManager.Changed -= ApplyTheme;
            AdsManager.BannerPresentationChanged -=
                OnBannerPresentationChanged;

            if (board != null)
            {
                board.PuzzleSolved -= OnPuzzleSolved;
                board.InputPerformed -= OnBoardInputPerformed;
                board.BoardTouched -= OnBoardTouched;
            }

            AdsManager.RewardedAvailabilityChanged -= RefreshHintButtonState;
            HintWallet.BalanceChanged -= RefreshHintButtonState;
            StopHintIdlePulse();
            UnregisterCallbacks();
        }

        private void BindUi()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogError("GameplayHUD could not access the Gameplay UIDocument.");
                return;
            }

            _safeArea = _root.Q<VisualElement>("gameplay-safe-area");
            _playCluster = _root.Q<VisualElement>("gameplay-play-cluster");
            _boardSlot = _root.Q<VisualElement>("gameplay-board-slot");
            _actions = _root.Q<VisualElement>("gameplay-actions");
            _adSpacer = _root.Q<VisualElement>("gameplay-ad-spacer");
            _bestStat = _root.Q<VisualElement>("gameplay-best-stat");
            _bestDivider = _root.Q<VisualElement>("gameplay-best-divider");
            _bestCaption = _bestStat?.Q<Label>(
                className: "gameplay-stat-caption");
            _progressCaption = _root.Q<Label>(className: "gameplay-stat-caption");
            cellProgressText = _root.Q<Label>("gameplay-progress");
            timerText = _root.Q<Label>("gameplay-timer");
            bestTimeText = _root.Q<Label>("gameplay-best");
            _bestHintIcon = _root.Q<VisualElement>(
                "gameplay-best-hint-icon");
            titleText = _root.Q<Label>("gameplay-title");
            _subtitleText = _root.Q<Label>("gameplay-subtitle");
            exitButton = _root.Q<Button>("gameplay-exit-button");
            _nextPuzzleButton = _root.Q<Button>("gameplay-next-button");
            hintButton = _root.Q<Button>("gameplay-hint-button");
            _hintCountBadge = _root.Q<Label>("gameplay-hint-count-badge");
            _hintVideoBadge = _root.Q<VisualElement>("gameplay-hint-video-badge");
            _adStatus = _root.Q<Label>("gameplay-ad-status");
            resetButton = _root.Q<Button>("gameplay-reset-button");
            _backgroundButton = _root.Q<Button>("gameplay-background-button");

            _overlayRoot = overlayDocument != null
                ? overlayDocument.rootVisualElement
                : null;
            if (_overlayRoot == null)
            {
                Debug.LogError("GameplayHUD could not access the Gameplay overlay UIDocument.");
                return;
            }

            _notQuiteWarning = _overlayRoot.Q<VisualElement>(
                "gameplay-not-quite-warning");
            _solvedModal = _overlayRoot.Q<VisualElement>("gameplay-solved-modal");
            _ratePromptModal = _overlayRoot.Q<VisualElement>(
                "gameplay-rate-prompt-modal");
            _ratePromptRateButton = _overlayRoot.Q<Button>(
                "gameplay-rate-prompt-rate-button");
            _ratePromptLaterButton = _overlayRoot.Q<Button>(
                "gameplay-rate-prompt-later-button");
            _ratePromptBackdrop = _overlayRoot.Q<Button>(
                "gameplay-rate-prompt-backdrop");
            _resetModal = _overlayRoot.Q<VisualElement>("gameplay-reset-modal");
            _exitModal = _overlayRoot.Q<VisualElement>("gameplay-exit-modal");
            _solvedTitle = _overlayRoot.Q<Label>("gameplay-solved-title");
            solvedTimeText = _overlayRoot.Q<Label>("gameplay-solved-message");
            _solvedSameDifficultyButton = _overlayRoot.Q<Button>("gameplay-solved-same-difficulty-button");
            _solvedPrimaryButton = _overlayRoot.Q<Button>("gameplay-solved-primary-button");
            _solvedExitButton = _overlayRoot.Q<Button>("gameplay-solved-exit-button");
            _confirmResetButton = _overlayRoot.Q<Button>("gameplay-confirm-reset-button");
            _streakCelebration = _overlayRoot.Q<VisualElement>(
                "gameplay-streak-celebration");
            _streakCelebrationTitle = _overlayRoot.Q<Label>(
                "gameplay-streak-celebration-title");
            _streakCelebrationCountRow = _overlayRoot.Q<VisualElement>(
                "gameplay-streak-celebration-count-row");
            _streakCelebrationNumber = _overlayRoot.Q<Label>(
                "gameplay-streak-celebration-number");
            _streakCelebrationReward = _overlayRoot.Q<Label>(
                "gameplay-streak-celebration-reward");
            _cancelResetButton = _overlayRoot.Q<Button>("gameplay-cancel-reset-button");
            _confirmExitButton = _overlayRoot.Q<Button>("gameplay-confirm-exit-button");
            _cancelExitButton = _overlayRoot.Q<Button>("gameplay-cancel-exit-button");
            _exitBackdrop = _overlayRoot.Q<Button>("gameplay-exit-backdrop");
            _exitTitle = _overlayRoot.Q<Label>("gameplay-exit-title");
            _exitMessage = _overlayRoot.Q<Label>("gameplay-exit-message");
            _solvedBackdrop = _overlayRoot.Q<Button>("gameplay-solved-backdrop");
            _resetBackdrop = _overlayRoot.Q<Button>("gameplay-reset-backdrop");
            _tutorialRoot = _overlayRoot.Q<VisualElement>(
                "gameplay-tutorial-root");
            _tutorialCard = _overlayRoot.Q<VisualElement>(
                "gameplay-tutorial-card");
            _tutorialCardRow = _overlayRoot.Q<VisualElement>(
                "gameplay-tutorial-card-row");
            _tutorialProgress = _overlayRoot.Q<VisualElement>(
                "gameplay-tutorial-progress");
            _tutorialGuideTile = _overlayRoot.Q<VisualElement>(
                "gameplay-tutorial-guide-tile");
            _tutorialActionBubble = _overlayRoot.Q<VisualElement>(
                "gameplay-tutorial-action-bubble");
            _tutorialActionTail = _overlayRoot.Q<VisualElement>(
                "gameplay-tutorial-action-tail");
            _tutorialGuideNumber = _overlayRoot.Q<Label>(
                "gameplay-tutorial-guide-number");
            _tutorialTitle = _overlayRoot.Q<Label>("gameplay-tutorial-title");
            _tutorialMessage = _overlayRoot.Q<Label>("gameplay-tutorial-message");
            _tutorialAction = _overlayRoot.Q<Label>("gameplay-tutorial-action");
            _tutorialPreviousButton = _overlayRoot.Q<Button>(
                "gameplay-tutorial-previous-button");
            _tutorialForwardButton = _overlayRoot.Q<Button>(
                "gameplay-tutorial-forward-button");
            _tutorialNextButton = _overlayRoot.Q<Button>(
                "gameplay-tutorial-next-button");

            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void ApplyTheme()
        {
            ThemeManager.ApplyTo(_root);
            ThemeManager.ApplyTo(_overlayRoot);
        }

        private void RegisterCallbacks()
        {
            if (exitButton != null) exitButton.clicked += RequestExitToMenu;
            if (_confirmExitButton != null) _confirmExitButton.clicked += ConfirmExitToMenu;
            if (_cancelExitButton != null) _cancelExitButton.clicked += CancelExitToMenu;
            if (_exitBackdrop != null) _exitBackdrop.clicked += CancelExitToMenu;
            if (_nextPuzzleButton != null) _nextPuzzleButton.clicked += OnHeaderNextPressed;
            if (hintButton != null) hintButton.clicked += OnHintButtonPressed;
            if (resetButton != null) resetButton.clicked += ShowResetConfirmation;
            if (_backgroundButton != null) _backgroundButton.clicked += ClearBoardSelection;
            if (_solvedSameDifficultyButton != null) _solvedSameDifficultyButton.clicked += OnSolvedSameDifficultyPressed;
            if (_solvedPrimaryButton != null) _solvedPrimaryButton.clicked += OnSolvedPrimaryPressed;
            if (_solvedExitButton != null) _solvedExitButton.clicked += OnSolvedExitPressed;
            if (_confirmResetButton != null) _confirmResetButton.clicked += ConfirmReset;
            if (_cancelResetButton != null) _cancelResetButton.clicked += CancelReset;
            if (_solvedBackdrop != null) _solvedBackdrop.clicked += HideSolvedModal;
            if (_resetBackdrop != null) _resetBackdrop.clicked += CancelReset;
            if (_ratePromptRateButton != null)
                _ratePromptRateButton.clicked += RateFromPrompt;
            if (_ratePromptLaterButton != null)
                _ratePromptLaterButton.clicked += DismissRatePrompt;
            if (_ratePromptBackdrop != null)
                _ratePromptBackdrop.clicked += DismissRatePrompt;

            if (_tutorialPreviousButton != null)
                _tutorialPreviousButton.clicked += NavigateTutorialBackward;
            if (_tutorialForwardButton != null)
                _tutorialForwardButton.clicked += NavigateTutorialForward;
            if (_tutorialNextButton != null)
                _tutorialNextButton.clicked += AdvanceTutorial;

            if (_tutorialCard != null)
            {
                _tutorialCard.RegisterCallback<PointerDownEvent>(
                    OnTutorialPointerDown);
                _tutorialCard.RegisterCallback<PointerMoveEvent>(
                    OnTutorialPointerMove);
                _tutorialCard.RegisterCallback<PointerUpEvent>(
                    OnTutorialPointerUp);
                _tutorialCard.RegisterCallback<PointerCancelEvent>(
                    OnTutorialPointerCancel);
            }
        }

        private void UnregisterCallbacks()
        {
            if (exitButton != null) exitButton.clicked -= RequestExitToMenu;
            if (_confirmExitButton != null) _confirmExitButton.clicked -= ConfirmExitToMenu;
            if (_cancelExitButton != null) _cancelExitButton.clicked -= CancelExitToMenu;
            if (_exitBackdrop != null) _exitBackdrop.clicked -= CancelExitToMenu;
            if (_nextPuzzleButton != null) _nextPuzzleButton.clicked -= OnHeaderNextPressed;
            if (hintButton != null) hintButton.clicked -= OnHintButtonPressed;
            if (resetButton != null) resetButton.clicked -= ShowResetConfirmation;
            if (_backgroundButton != null) _backgroundButton.clicked -= ClearBoardSelection;
            if (_solvedSameDifficultyButton != null) _solvedSameDifficultyButton.clicked -= OnSolvedSameDifficultyPressed;
            if (_solvedPrimaryButton != null) _solvedPrimaryButton.clicked -= OnSolvedPrimaryPressed;
            if (_solvedExitButton != null) _solvedExitButton.clicked -= OnSolvedExitPressed;
            if (_confirmResetButton != null) _confirmResetButton.clicked -= ConfirmReset;
            if (_cancelResetButton != null) _cancelResetButton.clicked -= CancelReset;
            if (_solvedBackdrop != null) _solvedBackdrop.clicked -= HideSolvedModal;
            if (_resetBackdrop != null) _resetBackdrop.clicked -= CancelReset;
            if (_ratePromptRateButton != null)
                _ratePromptRateButton.clicked -= RateFromPrompt;
            if (_ratePromptLaterButton != null)
                _ratePromptLaterButton.clicked -= DismissRatePrompt;
            if (_ratePromptBackdrop != null)
                _ratePromptBackdrop.clicked -= DismissRatePrompt;

            if (_tutorialPreviousButton != null)
                _tutorialPreviousButton.clicked -= NavigateTutorialBackward;
            if (_tutorialForwardButton != null)
                _tutorialForwardButton.clicked -= NavigateTutorialForward;
            if (_tutorialNextButton != null)
                _tutorialNextButton.clicked -= AdvanceTutorial;

            if (_tutorialCard != null)
            {
                _tutorialCard.UnregisterCallback<PointerDownEvent>(
                    OnTutorialPointerDown);
                _tutorialCard.UnregisterCallback<PointerMoveEvent>(
                    OnTutorialPointerMove);
                _tutorialCard.UnregisterCallback<PointerUpEvent>(
                    OnTutorialPointerUp);
                _tutorialCard.UnregisterCallback<PointerCancelEvent>(
                    OnTutorialPointerCancel);
            }

            if (_root != null)
                _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            ApplySafeArea();
            ApplyBannerInset();
            ScheduleResponsiveGameplayLayout();
        }

        private void OnBannerPresentationChanged(bool isPresented)
        {
            ApplyBannerInset();
            ScheduleResponsiveGameplayLayout();
        }

        private void ApplyBannerInset()
        {
            if (_adSpacer == null || _root == null)
                return;

            _adSpacer.style.height =
                AdsManager.GetBannerContentInset(
                    _root,
                    includeBottomSafeArea: false);
        }

        private void ApplySafeArea()
        {
            if (_safeArea == null || _root == null ||
                Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safe = Screen.safeArea;
            float panelWidth = _root.resolvedStyle.width;
            float panelHeight = _root.resolvedStyle.height;
            if (panelWidth <= 0f || panelHeight <= 0f)
                return;

            bool useTallPhoneLayout =
                panelHeight / panelWidth >= TALL_PHONE_ASPECT;
            float minimumHorizontalPadding = useTallPhoneLayout
                ? TALL_PHONE_HORIZONTAL_PADDING
                : 38f;

            _safeArea.style.paddingLeft = Mathf.Max(
                minimumHorizontalPadding,
                safe.xMin / Screen.width * panelWidth);
            _safeArea.style.paddingRight = Mathf.Max(
                minimumHorizontalPadding,
                (Screen.width - safe.xMax) / Screen.width * panelWidth);
            _safeArea.style.paddingTop = Mathf.Max(
                46f,
                (Screen.height - safe.yMax) / Screen.height * panelHeight);
            _safeArea.style.paddingBottom = Mathf.Max(
                20f,
                safe.yMin / Screen.height * panelHeight);
        }

        private void ScheduleResponsiveGameplayLayout()
        {
            if (_root == null || _responsiveLayoutQueued)
                return;

            _responsiveLayoutQueued = true;
            _root.schedule.Execute(() =>
            {
                _responsiveLayoutQueued = false;
                ApplyResponsiveGameplayLayout();
            }).ExecuteLater(0);
        }

        private void ApplyResponsiveGameplayLayout()
        {
            if (_root == null || _safeArea == null ||
                _playCluster == null || _boardSlot == null ||
                _actions == null || _adSpacer == null ||
                boardContainer == null || board == null)
            {
                return;
            }

            float panelWidth = _root.resolvedStyle.width;
            float panelHeight = _root.resolvedStyle.height;
            if (panelWidth <= 1f || panelHeight <= 1f)
                return;

            float panelAspect = panelHeight / panelWidth;
            bool useCompactLayout =
                panelHeight < COMPACT_LAYOUT_HEIGHT &&
                panelAspect < COMPACT_LAYOUT_ASPECT;
            bool useTallPhoneLayout =
                panelAspect >= TALL_PHONE_ASPECT;
            bool isTutorial = Shikaku.Menu.GameSession.IsTutorial;
            bool useBalancedLayout =
                useTallPhoneLayout &&
                !AdsManager.IsBannerPresented &&
                !isTutorial;
            bool compactLayoutChanged =
                _root.ClassListContains("gameplay-layout-compact") !=
                useCompactLayout;
            bool tallPhoneLayoutChanged =
                _root.ClassListContains("gameplay-layout-tall") !=
                useTallPhoneLayout;
            bool balancedLayoutChanged =
                _root.ClassListContains("gameplay-layout-balanced") !=
                useBalancedLayout;

            _root.EnableInClassList(
                "gameplay-layout-compact",
                useCompactLayout);
            _root.EnableInClassList(
                "gameplay-layout-tall",
                useTallPhoneLayout);
            _root.EnableInClassList(
                "gameplay-layout-balanced",
                useBalancedLayout);

            bool useEdgeToEdgeBoard =
                useTallPhoneLayout &&
                Mathf.Max(board.Width, board.Height) >= 8;
            board.SetEdgeToEdgeBoardLayout(useEdgeToEdgeBoard);

            if (compactLayoutChanged ||
                tallPhoneLayoutChanged ||
                balancedLayoutChanged)
            {
                ScheduleResponsiveGameplayLayout();
                return;
            }

            Rect clusterBounds = _playCluster.worldBound;
            Rect adBounds = _adSpacer.worldBound;
            if (clusterBounds.width <= 1f || adBounds.width <= 1f)
                return;

            float stageHeight = adBounds.yMin - clusterBounds.yMin;
            if (stageHeight <= 1f)
                return;

            float actionsHeight = isTutorial
                ? 0f
                : GetResolvedOuterHeight(_actions);
            float tutorialReserve = isTutorial
                ? GetTutorialCardRowHeight() +
                    TUTORIAL_CARD_GAP + TUTORIAL_CARD_AD_GAP
                : 0f;

            float maximumBoardHeightPanel = Mathf.Max(
                1f,
                stageHeight - actionsHeight - tutorialReserve);
            float maximumBoardWidthPanel = Mathf.Max(
                1f,
                _boardSlot.worldBound.width > 1f
                    ? _boardSlot.worldBound.width
                    : _safeArea.contentRect.width);

            Canvas boardCanvas =
                boardContainer.GetComponentInParent<Canvas>();
            RectTransform canvasRect =
                boardCanvas != null
                    ? boardCanvas.transform as RectTransform
                    : null;
            if (canvasRect == null ||
                canvasRect.rect.width <= 1f ||
                canvasRect.rect.height <= 1f)
            {
                return;
            }

            float maximumBoardWidthCanvas =
                maximumBoardWidthPanel / panelWidth *
                canvasRect.rect.width;
            float maximumBoardHeightCanvas =
                maximumBoardHeightPanel / panelHeight *
                canvasRect.rect.height;

            Vector2 preferredSlotCanvas =
                board.GetPreferredBoardSlotSize(
                    maximumBoardWidthCanvas,
                    maximumBoardHeightCanvas);
            if (preferredSlotCanvas.x <= 1f ||
                preferredSlotCanvas.y <= 1f)
            {
                return;
            }

            float targetSlotHeight = Mathf.Min(
                maximumBoardHeightPanel,
                preferredSlotCanvas.y /
                    canvasRect.rect.height * panelHeight);
            targetSlotHeight = Mathf.Max(1f, targetSlotHeight);

            if (Mathf.Abs(targetSlotHeight - _lastBoardSlotHeight) > 0.5f)
            {
                _boardSlot.style.height = targetSlotHeight;
                _lastBoardSlotHeight = targetSlotHeight;
            }

            _root.schedule.Execute(() =>
            {
                ApplyBoardSlot();
                PositionTutorialCard();
            }).ExecuteLater(0);
        }

        private static float GetResolvedOuterHeight(
            VisualElement element)
        {
            if (element == null ||
                element.resolvedStyle.display == DisplayStyle.None)
            {
                return 0f;
            }

            return element.worldBound.height +
                element.resolvedStyle.marginTop +
                element.resolvedStyle.marginBottom;
        }

        private float GetTutorialCardRowHeight()
        {
            if (_tutorialCardRow == null)
                return TUTORIAL_CARD_FALLBACK_HEIGHT;

            float height = _tutorialCardRow.resolvedStyle.height;
            if (float.IsNaN(height) || height <= 1f)
                return TUTORIAL_CARD_FALLBACK_HEIGHT;

            return Mathf.Max(250f, height);
        }

        private void ApplyBoardSlot()
        {
            if (_root == null || _boardSlot == null ||
                boardContainer == null)
            {
                return;
            }

            Rect rootRect = _root.worldBound;
            Rect slotRect = _boardSlot.worldBound;
            if (rootRect.width <= 1f || rootRect.height <= 1f ||
                slotRect.width <= 1f || slotRect.height <= 1f)
            {
                return;
            }

            if (ApproximatelyEqual(slotRect, _lastAppliedBoardSlotRect))
                return;

            float xMin = Mathf.Clamp01(
                (slotRect.xMin - rootRect.xMin) / rootRect.width);
            float xMax = Mathf.Clamp01(
                (slotRect.xMax - rootRect.xMin) / rootRect.width);
            float yMin = Mathf.Clamp01(
                1f - (slotRect.yMax - rootRect.yMin) /
                    rootRect.height);
            float yMax = Mathf.Clamp01(
                1f - (slotRect.yMin - rootRect.yMin) /
                    rootRect.height);

            boardContainer.anchorMin = new Vector2(xMin, yMin);
            boardContainer.anchorMax = new Vector2(xMax, yMax);
            boardContainer.offsetMin = Vector2.zero;
            boardContainer.offsetMax = Vector2.zero;
            _lastAppliedBoardSlotRect = slotRect;

            board.RefreshBoardLayout();
        }

        private static bool ApproximatelyEqual(Rect a, Rect b)
        {
            if (float.IsNaN(b.x))
                return false;

            const float tolerance = 0.5f;
            return Mathf.Abs(a.x - b.x) <= tolerance &&
                Mathf.Abs(a.y - b.y) <= tolerance &&
                Mathf.Abs(a.width - b.width) <= tolerance &&
                Mathf.Abs(a.height - b.height) <= tolerance;
        }
        private void ClearBoardSelection()
        {
            board?.ClearSelection();
        }

        public void OnPuzzleSolved()
        {
            using (SaveManager.BeginBatch())
            {
                OnPuzzleSolvedCore();
            }
        }

        private void OnPuzzleSolvedCore()
        {
            bool isTimeTrial =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial;
            bool isTutorial = Shikaku.Menu.GameSession.IsTutorial;
            DailyStreakUpdate streakUpdate =
                DailyStreakService.RecordPuzzleCompleted();
            StoreRatingService.QueueForStreak(streakUpdate);

            if (streakUpdate.Advanced)
                _pendingStreakUpdate = streakUpdate;

            SetPuzzleInteractionLocked(true);
            if (!isTutorial)

            SetHeaderNextVisible(false);

            if (!isTimeTrial)
            {
                _timerRunning = false;

            }

            if (ShouldShowBestTime())
            {
                bool usedHint =
                    _analyticsHintsUsedThisPuzzle > 0;

                SaveBestTimeIfBetter(
                    _elapsed,
                    usedHint);

                RefreshBestTime();
            }

            if (!isTimeTrial)
            {
                bool wasAlreadyCompleted = IsCompleted(
                    Shikaku.Menu.GameSession.GetPuzzleId());

                MarkCompleted();
                if (!isTutorial || !wasAlreadyCompleted)
                    UpdateProgressionOnSolved();

                if (!isTutorial && !wasAlreadyCompleted)
                    TryAwardCompletionHintReward();
            }

            AchievementService.RecordPuzzleCompleted(
                Shikaku.Menu.GameSession.GetPuzzleId());

            if (isTutorial)
            {
                _tutorialRunCompleted = true;
                GameAnalytics.TutorialCompleted();
            }
            else if (!isTimeTrial && board != null)
            {
                GameAnalytics.PuzzleCompleted(
                    board.Width,
                    board.Height,
                    _elapsed,
                    _analyticsHintsUsedThisPuzzle);
            }

            if (isTutorial)
            {
                Shikaku.Menu.GameSession.MarkTutorialCompleted();
                HideTutorialPresentation(restoreControls: false);

                if (solvedTimeText != null)
                {
                    solvedTimeText.text =
                        "You know the rules and controls. You are ready to continue Adventure.";
                }

                ShowSolvedModal(false);
                return;
            }

            if (solvedTimeText != null)
                solvedTimeText.text = $"Solved in {FormatResultTime(_elapsed)}";

            if (!isTimeTrial)
                ShowSolvedModal(false);
        }
        private System.Collections.IEnumerator Start()
        {
            // Wait for BoardController.Awake + its Start coroutine to run
            yield return null;
            yield return new WaitForEndOfFrame();

            RefreshAll();

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                BeginTutorial();
            }
            else if (Shikaku.Menu.GameSession.Mode ==
                     Shikaku.Menu.MenuMode.TimeTrial &&
                     board != null)
            {
                GameAnalytics.TimeTrialStarted(
                    board.Width,
                    board.Height,
                    Shikaku.Menu.GameSession.TimeLimitSeconds);
            }
        }

        private void BeginTutorial()
        {
            if (_tutorialRoot == null || board == null)
            {
                Debug.LogError("Tutorial could not start because its HUD or board is missing.");
                return;
            }

            _timerRunning = false;
            GameAnalytics.TutorialStarted();
            _tutorialStep = -1;
            _tutorialSwipeTracking = false;
            if (_tutorialCard != null)
                _tutorialCard.pickingMode = PickingMode.Ignore;
            board.ClearTutorialInputFilter();
            board.SetTutorialHighlights();
            _tutorialRoot.RemoveFromClassList("screen-hidden");
            SetHeaderNextVisible(false);

            if (_actions != null)
                _actions.style.display = DisplayStyle.None;
            else
            {
                if (hintButton != null) hintButton.style.display = DisplayStyle.None;
                if (resetButton != null) resetButton.style.display = DisplayStyle.None;
            }

            if (_tutorialProgress != null)
                _tutorialProgress.style.display = DisplayStyle.Flex;
            if (_tutorialPreviousButton != null)
                _tutorialPreviousButton.style.display = DisplayStyle.Flex;
            if (_tutorialForwardButton != null)
                _tutorialForwardButton.style.display = DisplayStyle.Flex;

            SetTutorialStep(0);
            ScheduleResponsiveGameplayLayout();
            RefreshTitle();
            UpdateTimerText();
            RefreshBestTime();
        }
        private void AdvanceTutorial()
        {
            NavigateTutorial(1);
        }

        private void NavigateTutorialBackward()
        {
            NavigateTutorial(-1);
        }

        private void NavigateTutorialForward()
        {
            NavigateTutorial(1);
        }

        private void NavigateTutorial(int direction)
        {
            if (!Shikaku.Menu.GameSession.IsTutorial ||
                board == null ||
                direction == 0)
            {
                return;
            }

            int targetStep = Mathf.Clamp(
                _tutorialStep + direction,
                0,
                TUTORIAL_STEP_COUNT - 1);

            if (targetStep == _tutorialStep)
                return;

            CancelTutorialStepTransitions();

            if (!PrepareTutorialBoardForStep(targetStep))
            {
                Debug.LogWarning(
                    "Tutorial could not prepare the requested step.");
                return;
            }

            SetTutorialStep(targetStep);
        }

        private void CancelTutorialStepTransitions()
        {
            if (_tutorialBuildTransitionRoutine != null)
            {
                StopCoroutine(_tutorialBuildTransitionRoutine);
                _tutorialBuildTransitionRoutine = null;
            }

            if (_tutorialSingleEraseRoutine != null)
            {
                StopCoroutine(_tutorialSingleEraseRoutine);
                _tutorialSingleEraseRoutine = null;
            }

            if (_tutorialIllegalMoveRoutine != null)
            {
                StopCoroutine(_tutorialIllegalMoveRoutine);
                _tutorialIllegalMoveRoutine = null;
            }

            _tutorialIllegalMoveTransitioning = false;
            _tutorialFirstIllegalMoveComplete = false;
            _tutorialSwipeTracking = false;
            _tutorialSwipePointerId = -1;
        }

        private bool PrepareTutorialBoardForStep(int step)
        {
            if (board == null)
                return false;

            // The Atlas tutorial teaches one continuous solve. Navigating its
            // cards must not erase rectangles the player has already charted.
            return true;
        }

        private void OnTutorialPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            _tutorialSwipeTracking = true;
            _tutorialSwipePointerId = evt.pointerId;
            _tutorialSwipeStart = evt.position;
        }

        private void OnTutorialPointerMove(PointerMoveEvent evt)
        {
            if (!_tutorialSwipeTracking ||
                evt.pointerId != _tutorialSwipePointerId)
            {
                return;
            }

            Vector2 delta = (Vector2)evt.position - _tutorialSwipeStart;
            if (Mathf.Abs(delta.x) < TUTORIAL_SWIPE_THRESHOLD ||
                Mathf.Abs(delta.x) <= Mathf.Abs(delta.y) * 1.2f)
            {
                return;
            }

            _tutorialSwipeTracking = false;
            _tutorialSwipePointerId = -1;

            NavigateTutorial(delta.x < 0f ? 1 : -1);
            evt.StopPropagation();
        }

        private void OnTutorialPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _tutorialSwipePointerId)
            {
                _tutorialSwipeTracking = false;
                _tutorialSwipePointerId = -1;
            }
        }

        private void OnTutorialPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _tutorialSwipePointerId)
            {
                _tutorialSwipeTracking = false;
                _tutorialSwipePointerId = -1;
            }
        }

        private void SetTutorialStep(int step)
        {
            _tutorialStep = Mathf.Clamp(step, 0, TUTORIAL_STEP_COUNT - 1);

            if (!_tutorialStepsReached[_tutorialStep])
            {
                _tutorialStepsReached[_tutorialStep] = true;
                GameAnalytics.TutorialStepReached(
                    _tutorialStep + 1,
                    TUTORIAL_STEP_NAMES[_tutorialStep]);
            }

            if (_tutorialNextButton != null)
                _tutorialNextButton.text = "Continue";

            UpdateTutorialGuideTile();
            UpdateTutorialProgress();
            UpdateTutorialNavigation();
            board.ClearTutorialInputFilter();
            board.SetTutorialHighlights();
            _atlasTutorialTargets = null;

            switch (_tutorialStep)
            {
                case 0:
                    SetPuzzleInteractionLocked(true);
                    SetTutorialCopy(
                        "Chart the whole board",
                        "Welcome, explorer! Divide the grid into rectangles until every square belongs to one mapped district.",
                        "Pip will guide your first route.",
                        true);
                    break;

                case 1:
                    SetPuzzleInteractionLocked(true);
                    int clueIndex = FindFirstTutorialClue();
                    if (clueIndex >= 0)
                    {
                        _atlasTutorialTargets = new[] { clueIndex };
                        board.SetTutorialHighlights(_atlasTutorialTargets);
                    }
                    SetTutorialCopy(
                        "Read the survey marker",
                        "Each number is the area of its rectangle. A 6 can be 1 × 6, 2 × 3, 3 × 2, or 6 × 1 when the board allows it.",
                        "Every district keeps its numbered marker.",
                        true);
                    break;

                case 2:
                    SetPuzzleInteractionLocked(false);
                    _atlasTutorialTargets = board.GetFirstSolutionRegionCells();
                    if (_atlasTutorialTargets != null)
                        board.SetTutorialHighlights(_atlasTutorialTargets);
                    board.SetTutorialInputFilter(
                        (action, index) =>
                            action == BoardInputAction.BeginRegion ||
                            action == BoardInputAction.UpdateRegion ||
                            action == BoardInputAction.CommitRegion ||
                            action == BoardInputAction.OverrideRegion ||
                            action == BoardInputAction.InvalidRegion ||
                            action == BoardInputAction.Select);
                    SetTutorialCopy(
                        "Draw corner to corner",
                        "Press any corner and drag to the opposite corner. The live label shows width × height = area.",
                        "Chart the highlighted rectangle.",
                        false);
                    break;

                case 3:
                    SetPuzzleInteractionLocked(true);
                    SetTutorialCopy(
                        "Match the area",
                        "A rectangle is accepted only when its area matches its marker. Red borders mean the shape is not ready to commit.",
                        "Use the live area label before releasing.",
                        true);
                    break;

                case 4:
                    SetPuzzleInteractionLocked(true);
                    SetTutorialCopy(
                        "One marker per district",
                        "Every rectangle must contain exactly one number. A shape with no marker or two markers is rejected.",
                        "Clear borders make each district easy to inspect.",
                        true);
                    break;

                case 5:
                    SetPuzzleInteractionLocked(false);
                    board.ClearTutorialInputFilter();
                    SetTutorialCopy(
                        "Revise your route",
                        "Drag across an unlocked rectangle to replace it. Double-tap a rectangle when you want to remove it completely.",
                        "Try another rectangle, or continue when ready.",
                        true);
                    break;

                default:
                    SetPuzzleInteractionLocked(false);
                    board.ClearTutorialInputFilter();
                    SetFinalTutorialHighlights();
                    SetTutorialCopy(
                        "Cover every square",
                        "Finish the map with valid rectangles. When there are no gaps, your Pocket Atlas is complete.",
                        "Solve the rest of the board.",
                        false);
                    break;
            }
        }
        private void SetTutorialCopy(
            string title,
            string message,
            string action,
            bool showContinue)
        {
            if (_tutorialTitle != null)
                _tutorialTitle.text = title;
            if (_tutorialMessage != null)
                _tutorialMessage.text = message;
            if (_tutorialAction != null)
                _tutorialAction.text = action;

            if (_tutorialActionBubble != null)
            {
                _tutorialActionBubble.style.display =
                    string.IsNullOrEmpty(action)
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
            }

            if (_tutorialNextButton != null)
            {
                _tutorialNextButton.style.display =
                    showContinue ? DisplayStyle.Flex : DisplayStyle.None;
                _tutorialNextButton.SetEnabled(showContinue);
            }

            _tutorialCardLastPosition =
                new Vector2(float.NaN, float.NaN);
            ScheduleResponsiveGameplayLayout();
        }

        private void UpdateTutorialGuideTile()
        {
            int guideNumber = _tutorialStep + 1;
            if (_tutorialGuideNumber != null)
                _tutorialGuideNumber.text = guideNumber.ToString();

            if (_tutorialGuideTile == null || board == null || board.Palette == null)
                return;

            Color fill = board.Palette.GetColorForNumber(guideNumber);
            Color border = new Color(
                fill.r * 0.62f,
                fill.g * 0.62f,
                fill.b * 0.62f,
                fill.a);

            _tutorialGuideTile.style.backgroundColor = new StyleColor(fill);
            _tutorialGuideTile.style.borderTopColor = new StyleColor(border);
            _tutorialGuideTile.style.borderRightColor = new StyleColor(border);
            _tutorialGuideTile.style.borderBottomColor = new StyleColor(border);
            _tutorialGuideTile.style.borderLeftColor = new StyleColor(border);
        }
        private void UpdateTutorialProgress()
        {
            if (_tutorialProgress == null)
                return;

            int index = 0;
            foreach (VisualElement dot in _tutorialProgress.Children())
            {
                if (index == _tutorialStep)
                    dot.AddToClassList("gameplay-tutorial-dot-active");
                else
                    dot.RemoveFromClassList("gameplay-tutorial-dot-active");

                index++;
            }
        }

        private void UpdateTutorialNavigation()
        {
            if (_tutorialPreviousButton != null)
                _tutorialPreviousButton.SetEnabled(_tutorialStep > 0);

            if (_tutorialForwardButton != null)
            {
                _tutorialForwardButton.SetEnabled(
                    _tutorialStep < TUTORIAL_STEP_COUNT - 1);
            }
        }

        private int[] GetTutorialActionTargets()
        {
            return _atlasTutorialTargets;
        }

        private int FindFirstTutorialClue()
        {
            if (board == null)
                return -1;

            int cellCount = board.Width * board.Height;
            for (int index = 0; index < cellCount; index++)
            {
                if (board.IsAnchorCell(index))
                    return index;
            }

            return -1;
        }

        private void LateUpdate()
        {
            PositionNotQuiteWarning();

            if (Shikaku.Menu.GameSession.IsTutorial &&
                _tutorialStep >= 0)
            {
                PositionTutorialCard();
                PositionTutorialActionBubble();
            }
        }

        private void PositionNotQuiteWarning()
        {
            if (_notQuiteWarning == null ||
                _notQuiteWarning.resolvedStyle.display == DisplayStyle.None ||
                _overlayRoot == null ||
                _root == null ||
                _boardSlot == null)
            {
                return;
            }

            Rect rootRect = _root.worldBound;
            float overlayWidth = _overlayRoot.resolvedStyle.width;
            float overlayHeight = _overlayRoot.resolvedStyle.height;
            if (rootRect.width <= 1f || rootRect.height <= 1f ||
                overlayWidth <= 1f || overlayHeight <= 1f)
            {
                return;
            }

            float warningWidth = _notQuiteWarning.resolvedStyle.width;
            float warningHeight = _notQuiteWarning.resolvedStyle.height;
            if (float.IsNaN(warningWidth) || warningWidth <= 1f)
                warningWidth = Mathf.Min(760f, overlayWidth * 0.82f);
            if (float.IsNaN(warningHeight) || warningHeight <= 1f)
                warningHeight = 128f;

            float boardTop =
                (_boardSlot.worldBound.yMin - rootRect.yMin) /
                rootRect.height * overlayHeight;
            float left = Mathf.Clamp(
                (overlayWidth - warningWidth) * 0.5f,
                14f,
                Mathf.Max(14f, overlayWidth - warningWidth - 14f));
            float top = Mathf.Clamp(
                boardTop + 18f,
                14f,
                Mathf.Max(14f, overlayHeight - warningHeight - 14f));

            _notQuiteWarning.style.left = left;
            _notQuiteWarning.style.top = top;
        }

        private void PositionTutorialCard()
        {
            if (_tutorialRoot == null ||
                _tutorialCardRow == null ||
                _overlayRoot == null ||
                _root == null ||
                _boardSlot == null ||
                _adSpacer == null ||
                _tutorialRoot.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            Rect rootRect = _root.worldBound;
            float overlayWidth = _overlayRoot.resolvedStyle.width;
            float overlayHeight = _overlayRoot.resolvedStyle.height;
            if (rootRect.width <= 1f || rootRect.height <= 1f ||
                overlayWidth <= 1f || overlayHeight <= 1f)
            {
                return;
            }

            float rowWidth = _tutorialCardRow.resolvedStyle.width;
            float rowHeight = _tutorialCardRow.resolvedStyle.height;
            if (float.IsNaN(rowWidth) || rowWidth <= 1f)
                rowWidth = Mathf.Min(960f, overlayWidth * 0.94f);
            if (float.IsNaN(rowHeight) || rowHeight <= 1f)
                rowHeight = TUTORIAL_CARD_FALLBACK_HEIGHT;

            float boardBottom =
                (_boardSlot.worldBound.yMax - rootRect.yMin) /
                rootRect.height * overlayHeight;
            float adTop =
                (_adSpacer.worldBound.yMin - rootRect.yMin) /
                rootRect.height * overlayHeight;

            float desiredTop = boardBottom + TUTORIAL_CARD_GAP;
            float maximumTop = Mathf.Max(
                12f,
                adTop - rowHeight - TUTORIAL_CARD_AD_GAP);
            float top = Mathf.Clamp(
                Mathf.Min(desiredTop, maximumTop),
                12f,
                Mathf.Max(12f, overlayHeight - rowHeight - 12f));
            float left = Mathf.Clamp(
                (overlayWidth - rowWidth) * 0.5f,
                12f,
                Mathf.Max(12f, overlayWidth - rowWidth - 12f));

            Vector2 position = new Vector2(left, top);
            if (float.IsNaN(_tutorialCardLastPosition.x) ||
                (position - _tutorialCardLastPosition).sqrMagnitude > 0.25f)
            {
                _tutorialCardRow.style.left = position.x;
                _tutorialCardRow.style.top = position.y;
                _tutorialCardLastPosition = position;
            }
        }
        private void PositionTutorialActionBubble()
        {
            if (_tutorialActionBubble == null ||
                _tutorialActionBubble.resolvedStyle.display ==
                    DisplayStyle.None ||
                _overlayRoot == null ||
                board == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
            {
                return;
            }

            if (!TryGetTutorialTargetPanelRect(out Rect targetRect))
                return;

            float panelWidth = _overlayRoot.resolvedStyle.width;
            float panelHeight = _overlayRoot.resolvedStyle.height;
            if (panelWidth <= 1f || panelHeight <= 1f)
                return;

            float bubbleWidth =
                _tutorialActionBubble.resolvedStyle.width;
            float bubbleHeight =
                _tutorialActionBubble.resolvedStyle.height;

            if (float.IsNaN(bubbleWidth) || bubbleWidth <= 1f)
                bubbleWidth = Mathf.Min(540f, panelWidth * 0.68f);
            if (float.IsNaN(bubbleHeight) || bubbleHeight <= 1f)
                bubbleHeight = 90f;

            const float gap = 20f;
            const float edge = 18f;

            float centeredX =
                targetRect.center.x - bubbleWidth * 0.5f;
            float centeredY =
                targetRect.center.y - bubbleHeight * 0.5f;

            Rect bestRect = new Rect(
                centeredX,
                targetRect.yMin - gap - bubbleHeight,
                bubbleWidth,
                bubbleHeight);
            int bestDirection = 0;
            float bestScore = ScoreTutorialBubbleCandidate(
                bestRect,
                targetRect,
                panelWidth,
                panelHeight,
                edge);

            for (int direction = 1; direction < 4; direction++)
            {
                Rect candidate;
                switch (direction)
                {
                    case 1:
                        candidate = new Rect(
                            centeredX,
                            targetRect.yMax + gap,
                            bubbleWidth,
                            bubbleHeight);
                        break;
                    case 2:
                        candidate = new Rect(
                            targetRect.xMin - gap - bubbleWidth,
                            centeredY,
                            bubbleWidth,
                            bubbleHeight);
                        break;
                    default:
                        candidate = new Rect(
                            targetRect.xMax + gap,
                            centeredY,
                            bubbleWidth,
                            bubbleHeight);
                        break;
                }

                float score = ScoreTutorialBubbleCandidate(
                    candidate,
                    targetRect,
                    panelWidth,
                    panelHeight,
                    edge);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestRect = candidate;
                    bestDirection = direction;
                }
            }

            Vector2 position = new Vector2(
                Mathf.Clamp(
                    bestRect.x,
                    edge,
                    Mathf.Max(edge, panelWidth - bubbleWidth - edge)),
                Mathf.Clamp(
                    bestRect.y,
                    edge,
                    Mathf.Max(edge, panelHeight - bubbleHeight - edge)));

            if (float.IsNaN(_tutorialBubbleLastPosition.x) ||
                (position - _tutorialBubbleLastPosition).sqrMagnitude > 0.25f)
            {
                _tutorialActionBubble.style.left = position.x;
                _tutorialActionBubble.style.top = position.y;
                _tutorialBubbleLastPosition = position;
            }

            ApplyTutorialBubbleTail(bestDirection);
        }

        private bool TryGetTutorialTargetPanelRect(
            out Rect panelRect)
        {
            panelRect = default;
            int[] targets = GetTutorialActionTargets();
            if (targets == null || targets.Length == 0)
                return false;

            float panelWidth = _overlayRoot.resolvedStyle.width;
            float panelHeight = _overlayRoot.resolvedStyle.height;
            bool found = false;

            for (int i = 0; i < targets.Length; i++)
            {
                if (!board.TryGetCellScreenRect(
                        targets[i],
                        out Rect screenRect))
                {
                    continue;
                }

                Rect converted = Rect.MinMaxRect(
                    screenRect.xMin / Screen.width * panelWidth,
                    (Screen.height - screenRect.yMax) /
                        Screen.height * panelHeight,
                    screenRect.xMax / Screen.width * panelWidth,
                    (Screen.height - screenRect.yMin) /
                        Screen.height * panelHeight);

                if (!found)
                {
                    panelRect = converted;
                    found = true;
                }
                else
                {
                    panelRect = Rect.MinMaxRect(
                        Mathf.Min(panelRect.xMin, converted.xMin),
                        Mathf.Min(panelRect.yMin, converted.yMin),
                        Mathf.Max(panelRect.xMax, converted.xMax),
                        Mathf.Max(panelRect.yMax, converted.yMax));
                }
            }

            return found;
        }

        private float ScoreTutorialBubbleCandidate(
            Rect candidate,
            Rect target,
            float panelWidth,
            float panelHeight,
            float edge)
        {
            float overflow =
                Mathf.Max(0f, edge - candidate.xMin) +
                Mathf.Max(0f, candidate.xMax - (panelWidth - edge)) +
                Mathf.Max(0f, edge - candidate.yMin) +
                Mathf.Max(0f, candidate.yMax - (panelHeight - edge));

            float score = overflow * 10000f;
            score += IntersectionArea(candidate, target) * 100f;

            if (_tutorialCard != null)
            {
                score +=
                    IntersectionArea(candidate, _tutorialCard.worldBound) *
                    25f;
            }

            return score;
        }

        private static float IntersectionArea(Rect a, Rect b)
        {
            float width =
                Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) -
                              Mathf.Max(a.xMin, b.xMin));
            float height =
                Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) -
                              Mathf.Max(a.yMin, b.yMin));
            return width * height;
        }

        private void ApplyTutorialBubbleTail(int direction)
        {
            if (_tutorialActionTail == null ||
                direction == _tutorialBubbleDirection)
            {
                return;
            }

            _tutorialActionTail.RemoveFromClassList(
                "tutorial-tail-bottom");
            _tutorialActionTail.RemoveFromClassList(
                "tutorial-tail-top");
            _tutorialActionTail.RemoveFromClassList(
                "tutorial-tail-left");
            _tutorialActionTail.RemoveFromClassList(
                "tutorial-tail-right");

            switch (direction)
            {
                case 0:
                    _tutorialActionTail.AddToClassList(
                        "tutorial-tail-bottom");
                    break;
                case 1:
                    _tutorialActionTail.AddToClassList(
                        "tutorial-tail-top");
                    break;
                case 2:
                    _tutorialActionTail.AddToClassList(
                        "tutorial-tail-right");
                    break;
                default:
                    _tutorialActionTail.AddToClassList(
                        "tutorial-tail-left");
                    break;
            }

            _tutorialBubbleDirection = direction;
        }
        private void SetFinalTutorialHighlights()
        {
            if (board == null)
                return;

            var emptyCells = new List<int>();
            int total = board.Width * board.Height;
            for (int i = 0; i < total; i++)
            {
                if (board.ValueAt(i) == 0)
                    emptyCells.Add(i);
            }

            board.SetTutorialHighlights(emptyCells.ToArray());
        }

        private void OnBoardInputPerformed(
    BoardInputAction action,
    int cellIndex)
        {
            if (!Shikaku.Menu.GameSession.IsTutorial)
                return;

            if (_tutorialStep == 2 &&
                (action == BoardInputAction.CommitRegion ||
                 action == BoardInputAction.OverrideRegion))
            {
                SetTutorialStep(3);
                return;
            }

            if (_tutorialStep == TUTORIAL_STEP_COUNT - 1 &&
                (action == BoardInputAction.CommitRegion ||
                 action == BoardInputAction.OverrideRegion ||
                 action == BoardInputAction.RemoveRegion))
            {
                SetFinalTutorialHighlights();
            }
        }

        private System.Collections.IEnumerator CompleteBuildRegionLesson()
        {
            SetPuzzleInteractionLocked(true);

            if (_tutorialAction != null)
                _tutorialAction.text = "good job";

            yield return new WaitForSecondsRealtime(1f);

            if (board == null ||
                !Shikaku.Menu.GameSession.IsTutorial ||
                _tutorialStep != 1)
            {
                _tutorialBuildTransitionRoutine = null;
                yield break;
            }

            bool prepared =
                board.OverwriteTutorialCell(
                    TUTORIAL_TWO_ANCHOR,
                    TUTORIAL_THREE_NEAR);

            if (!prepared &&
                !PrepareTutorialBoardForStep(2))
            {
                Debug.LogWarning(
                    "Tutorial could not prepare the overwrite lesson.");
                _tutorialBuildTransitionRoutine = null;
                SetPuzzleInteractionLocked(false);
                yield break;
            }

            _tutorialBuildTransitionRoutine = null;
            SetTutorialStep(2);
        }

        private System.Collections.IEnumerator RestoreTutorialErase()
        {
            // Prevent the player from doing anything during the brief restore.
            SetPuzzleInteractionLocked(true);

            if (_tutorialAction != null)
            {
                _tutorialAction.text =
                    "Restoring...";
            }

            yield return new WaitForSecondsRealtime(1f);

            if (board == null ||
                !Shikaku.Menu.GameSession.IsTutorial)
            {
                _tutorialSingleEraseRoutine = null;
                yield break;
            }

            board.ClearTutorialInputFilter();

            bool restored = board.RestoreTutorialCell(
                TUTORIAL_THREE_ANCHOR,
                TUTORIAL_THREE_FAR);

            if (!restored)
            {
                Debug.LogWarning(
                    "Tutorial could not restore the erased square.");
            }

            _tutorialSingleEraseRoutine = null;

            SetTutorialStep(4);
        }

        private System.Collections.IEnumerator AdvanceAfterFirstIllegalMove()
        {
            _tutorialIllegalMoveTransitioning = true;

            yield return new WaitForSecondsRealtime(0.28f);

            if (board == null ||
                !Shikaku.Menu.GameSession.IsTutorial)
            {
                _tutorialIllegalMoveTransitioning = false;
                _tutorialIllegalMoveRoutine = null;
                yield break;
            }

            _tutorialFirstIllegalMoveComplete = true;
            _tutorialIllegalMoveTransitioning = false;
            board.SetTutorialHighlights(
                TUTORIAL_ILLEGAL_ABOVE,
                TUTORIAL_ILLEGAL_ANCHOR);

            if (_tutorialAction != null)
            {
                _tutorialAction.text =
                    "Good. Make another illegal move.";
            }

            _tutorialIllegalMoveRoutine = null;
        }

        private System.Collections.IEnumerator CompleteIllegalMoveLesson()
        {
            _tutorialIllegalMoveTransitioning = true;

            yield return new WaitForSecondsRealtime(0.28f);

            if (board == null ||
                !Shikaku.Menu.GameSession.IsTutorial)
            {
                _tutorialIllegalMoveTransitioning = false;
                _tutorialIllegalMoveRoutine = null;
                yield break;
            }

            _tutorialIllegalMoveTransitioning = false;
            board.ClearTutorialInputFilter();
            _tutorialIllegalMoveRoutine = null;
            SetTutorialStep(7);
        }
        private void HideTutorialPresentation(bool restoreControls)
        {
            _tutorialRoot?.AddToClassList("screen-hidden");
            board?.ClearTutorialInputFilter();
            board?.SetTutorialHighlights();
            _tutorialStep = -1;

            if (!restoreControls)
                return;

            if (_actions != null)
            {
                _actions.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (hintButton != null)
                    hintButton.style.display = DisplayStyle.Flex;
                if (resetButton != null)
                    resetButton.style.display = DisplayStyle.Flex;
            }

            _tutorialCardLastPosition =
                new Vector2(float.NaN, float.NaN);
            ScheduleResponsiveGameplayLayout();
        }

        private void Update()
        {
            if (_timerRunning)
            {
                if (_countDown)
                {
                    _remaining -= Time.deltaTime;

                    if (_remaining <= 0f)
                    {
                        _remaining = 0f;
                        _timerRunning = false;

                        RefreshHintButtonState();
                        ShowTimeTrialSummary();
                        return;
                    }
                }
                else
                {
                    _elapsed += Time.deltaTime;
                }

                UpdateTimerText();
            }

            RefreshHintButtonState();
            UpdateNotQuiteWarning();
            UpdateHintIdleReminder();

            // Cheap + safe for now (max 81 cells).
            RefreshProgressTexts();

            bool backPressed =
                Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame;

            backPressed |=
                Gamepad.current != null &&
                Gamepad.current.buttonEast.wasPressedThisFrame;

            if (backPressed && !CloseTopModal())
                RequestExitToMenu();
        }

        private void UpdateNotQuiteWarning()
        {
            if (_notQuiteWarning == null)
                return;

            bool shouldWarn =
                board != null &&
                !_puzzleInteractionLocked &&
                board.IsBoardCompletelyFilled &&
                !board.IsPuzzleSolved;

            if (!shouldWarn)
            {
                _notQuiteWarningActive = false;
                _notQuiteWarning.AddToClassList("screen-hidden");
                return;
            }

            if (!_notQuiteWarningActive)
            {
                _notQuiteWarningActive = true;
                _notQuiteWarningCycleStartedAt = Time.unscaledTime;
            }

            float elapsed = Time.unscaledTime -
                            _notQuiteWarningCycleStartedAt;
            bool visible =
                Mathf.Repeat(
                    elapsed,
                    NOT_QUITE_PHASE_SECONDS * 2f) <
                NOT_QUITE_PHASE_SECONDS;

            if (visible)
                _notQuiteWarning.RemoveFromClassList("screen-hidden");
            else
                _notQuiteWarning.AddToClassList("screen-hidden");
        }

        private void OnBoardTouched()
        {
            ResetHintIdleCountdown();
        }

        private void ResetHintIdleCountdown()
        {
            StopHintIdlePulse();
            _nextHintIdlePulseAt =
                Time.unscaledTime + HINT_IDLE_SECONDS;
        }

        private void UpdateHintIdleReminder()
        {
            bool isTimeTrial =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial;
            bool modalVisible =
                IsModalVisible(_solvedModal) ||
                IsModalVisible(_ratePromptModal) ||
                IsModalVisible(_resetModal) ||
                IsModalVisible(_exitModal);
            bool canRemind =
                !isTimeTrial &&
                !Shikaku.Menu.GameSession.IsTutorial &&
                !_puzzleInteractionLocked &&
                !modalVisible &&
                board != null &&
                !board.InputLocked &&
                hintButton != null &&
                hintButton.enabledSelf &&
                hintButton.resolvedStyle.display != DisplayStyle.None;

            if (!canRemind)
            {
                StopHintIdlePulse();
                _nextHintIdlePulseAt =
                    Time.unscaledTime + HINT_IDLE_SECONDS;
                return;
            }

            if (_hintIdlePulseRoutine == null &&
                Time.unscaledTime >= _nextHintIdlePulseAt)
            {
                _hintIdlePulseRoutine =
                    StartCoroutine(HintIdlePulseRoutine());
            }
        }

        private IEnumerator HintIdlePulseRoutine()
        {
            for (int i = 0; i < HINT_IDLE_PULSE_COUNT; i++)
            {
                hintButton?.AddToClassList("gameplay-hint-idle-glow");
                yield return new WaitForSecondsRealtime(
                    HINT_IDLE_PULSE_ON_SECONDS);

                hintButton?.RemoveFromClassList(
                    "gameplay-hint-idle-glow");
                yield return new WaitForSecondsRealtime(
                    HINT_IDLE_PULSE_OFF_SECONDS);
            }

            hintButton?.RemoveFromClassList("gameplay-hint-idle-glow");
            _hintIdlePulseRoutine = null;
            _nextHintIdlePulseAt =
                Time.unscaledTime + HINT_IDLE_SECONDS;
        }

        private void StopHintIdlePulse()
        {
            if (_hintIdlePulseRoutine != null)
            {
                StopCoroutine(_hintIdlePulseRoutine);
                _hintIdlePulseRoutine = null;
            }

            hintButton?.RemoveFromClassList(
                "gameplay-hint-idle-glow");
        }

        private void ShowTimeTrialSummary()
        {
            if (_timeTrialSummaryShown) return;
            _timeTrialSummaryShown = true;

            SetPuzzleInteractionLocked(true);
            SetHeaderNextVisible(false);

            int solved = Shikaku.Menu.GameSession.TimeTrialSolvedCount;
            int baseScore = Shikaku.Menu.GameSession.TimeTrialCompletedSquares;
            int partialScore = board != null ? board.CountCompletedSquaresForTimeTrial() : 0;
            int totalScore = baseScore + partialScore;
            int size = Shikaku.Menu.GameSession.Size;

            AchievementService.RecordTimeTrialSessionFinished(
                size,
                totalScore);
            Shikaku.Menu.GameSession.SaveTimeTrialBestSquaresIfHigher(size, totalScore);
            PlayerPrefs.Save();

            int best = Shikaku.Menu.GameSession.GetTimeTrialBestSquares(size);

            if (!_timeTrialAnalyticsRecorded)
            {
                _timeTrialAnalyticsRecorded = true;
                GameAnalytics.TimeTrialFinished(
                    board != null ? board.Width : size,
                    board != null ? board.Height : size,
                    totalScore,
                    solved,
                    best,
                    Mathf.Max(
                        0f,
                        Shikaku.Menu.GameSession.TimeLimitSeconds -
                        _remaining),
                    "timer");
            }

            if (solvedTimeText != null)
            {
                solvedTimeText.text =
                    $"Solved puzzles: {solved}\n" +
                    $"Score: {totalScore} Best: {best}";
            }

            RefreshBestTime();

            ShowSolvedModal(true);
        }

        public void RestartTimeTrialRun()
        {
            // Reset timer
            _elapsed = 0f;
            _countDown = true;
            _remaining = Shikaku.Menu.GameSession.TimeLimitSeconds;
            _timerRunning = true;

            _analyticsHintsUsedThisPuzzle = 0;
            _exitAnalyticsRecorded = false;
            _timeTrialAnalyticsRecorded = false;
            _timeTrialSummaryShown = false;
            SetPuzzleInteractionLocked(false);
            SetHeaderNextVisible(false);
            Shikaku.Menu.GameSession.TimeTrialSolvedCount = 0;
            Shikaku.Menu.GameSession.TimeTrialCompletedSquares = 0;
            PlayerPrefs.Save();

            // Load a new random time trial puzzle
            HideSolvedModal();
            if (board != null)
                board.LoadRandomTimeTrialPuzzle();

            RefreshAll();
            if (board != null)
            {
                GameAnalytics.TimeTrialStarted(
                    board.Width,
                    board.Height,
                    Shikaku.Menu.GameSession.TimeLimitSeconds);
            }
        }

        public void ResumeAfterSolvedPanel()
        {
            _timerRunning = true;   // resume countdown after Next
            _analyticsHintsUsedThisPuzzle = 0;
            _exitAnalyticsRecorded = false;
        }

        private void ExitTutorialToMenu()
        {
            bool returnToSettings =
                Shikaku.Menu.GameSession.TutorialReturnToSettings;

            HideTutorialPresentation(restoreControls: false);
            Shikaku.Menu.GameSession.EndTutorialSession();

            PlayerPrefs.DeleteKey("menu_return");
            PlayerPrefs.DeleteKey("menu_return_pack");
            PlayerPrefs.DeleteKey(
                Shikaku.Menu.HomeScreenController.AdventurePackKey);

            if (returnToSettings)
            {
                PlayerPrefs.SetString(
                    Shikaku.Menu.HomeScreenController.StartScreenKey,
                    Shikaku.Menu.HomeScreenController.SettingsScreenId);
            }
            else
            {
                PlayerPrefs.DeleteKey(
                    Shikaku.Menu.HomeScreenController.StartScreenKey);
            }

            PlayerPrefs.Save();
            SceneManager.LoadScene("HomeUI");
        }

        private void OnSolvedExitPressed()
        {
            _solvedExitButton?.SetEnabled(false);
            SetHeaderNextVisible(false);

            ExitToMenu();
        }

        private void ExitToMenu()
        {
            RecordExitAnalytics();

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                ExitTutorialToMenu();
                return;
            }

            if (Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.Daily)
            {
                PlayerPrefs.DeleteKey("menu_return");
                PlayerPrefs.DeleteKey("menu_return_pack");
                PlayerPrefs.SetString(
                    Shikaku.Menu.HomeScreenController.StartScreenKey,
                    Shikaku.Menu.HomeScreenController.DailyScreenId);
                PlayerPrefs.Save();
                SceneManager.LoadScene("HomeUI");
                return;
            }

            if (Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.TimeTrial)
            {
                PlayerPrefs.DeleteKey("menu_return");
                PlayerPrefs.DeleteKey("menu_return_pack");
                PlayerPrefs.SetString(
                    Shikaku.Menu.HomeScreenController.StartScreenKey,
                    Shikaku.Menu.HomeScreenController.TimeTrialScreenId);
                PlayerPrefs.Save();
                SceneManager.LoadScene("HomeUI");
                return;
            }

            if (Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.FreePlay)
            {
                PlayerPrefs.DeleteKey("menu_return");
                PlayerPrefs.DeleteKey("menu_return_pack");
                PlayerPrefs.SetString(
                    Shikaku.Menu.HomeScreenController.StartScreenKey,
                    Shikaku.Menu.HomeScreenController.FreePlayScreenId);
                PlayerPrefs.SetString(
                    Shikaku.Menu.HomeScreenController.FreePlayPackKey,
                    Shikaku.Menu.GameSession.PackPath);
                PlayerPrefs.Save();
                SceneManager.LoadScene("HomeUI");
                return;
            }

            if (Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.Story)
            {
                PlayerPrefs.DeleteKey("menu_return");
                PlayerPrefs.DeleteKey("menu_return_pack");
                PlayerPrefs.SetString(
                    Shikaku.Menu.HomeScreenController.StartScreenKey,
                    Shikaku.Menu.HomeScreenController.AdventureScreenId);
                PlayerPrefs.SetString(
                    Shikaku.Menu.HomeScreenController.AdventurePackKey,
                    Shikaku.Menu.GameSession.PackPath);
                PlayerPrefs.Save();
                SceneManager.LoadScene("HomeUI");
                return;
            }

            PlayerPrefs.DeleteKey("menu_return");
            PlayerPrefs.DeleteKey("menu_return_pack");
            PlayerPrefs.Save();
            SceneManager.LoadScene("HomeUI");
        }

        private void RecordExitAnalytics()
        {
            if (_exitAnalyticsRecorded)
                return;

            _exitAnalyticsRecorded = true;

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                if (!_tutorialRunCompleted)
                    GameAnalytics.TutorialExited(
                        Mathf.Max(0, _tutorialStep + 1));

                return;
            }

            if (Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial)
            {
                if (_timeTrialAnalyticsRecorded)
                    return;

                _timeTrialAnalyticsRecorded = true;
                int size = Shikaku.Menu.GameSession.Size;
                int solved =
                    Shikaku.Menu.GameSession.TimeTrialSolvedCount;
                int score =
                    Shikaku.Menu.GameSession.TimeTrialCompletedSquares +
                    (board != null
                        ? board.CountCompletedSquaresForTimeTrial()
                        : 0);

                GameAnalytics.TimeTrialFinished(
                    board != null ? board.Width : size,
                    board != null ? board.Height : size,
                    score,
                    solved,
                    Shikaku.Menu.GameSession.GetTimeTrialBestSquares(size),
                    Mathf.Max(
                        0f,
                        Shikaku.Menu.GameSession.TimeLimitSeconds -
                        _remaining),
                    "exit");
                return;
            }

            if (board != null && !board.IsPuzzleSolved)
            {
                GameAnalytics.PuzzleAbandoned(
                    board.Width,
                    board.Height,
                    _elapsed,
                    board.CountPlayerPlacedCells());
            }
        }

        private void RequestExitToMenu()
        {
            // Always confirm before leaving the tutorial.
            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                ShowExitConfirmation(
                    "Leave Tutorial?",
                    "Your tutorial progress will be lost."
                );

                return;
            }

            bool isTimeTrial =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial;

            if (isTimeTrial && _timeTrialSummaryShown)
            {
                ExitToMenu();
                return;
            }

            // Leaving an active Time Trial ends the entire run,
            // so always ask for confirmation.
            if (isTimeTrial && !_timeTrialSummaryShown)
            {
                ShowExitConfirmation(
                    "Leave Time Trial?",
                    "Your current run will end."
                );

                return;
            }

            // A solved normal puzzle has no unfinished progress to lose.
            if (board != null && board.IsPuzzleSolved)
            {
                ExitToMenu();
                return;
            }

            // If the player has actually made progress, warn them.
            if (board != null && board.HasPlayerPlacedCells())
            {
                ShowExitConfirmation(
                    "Leave Puzzle?",
                    "Your current progress will be lost."
                );

                return;
            }

            // Untouched puzzle: leave immediately.
            ExitToMenu();
        }

        private void ShowExitConfirmation(
            string title,
            string message)
        {
            if (_exitTitle != null)
                _exitTitle.text = title;

            if (_exitMessage != null)
                _exitMessage.text = message;

            ShowModal(_exitModal);
        }

        private void CancelExitToMenu()
        {
            HideModal(_exitModal);
        }

        private void ConfirmExitToMenu()
        {
            HideModal(_exitModal);
            ExitToMenu();
        }

        private void ResetTimer()
        {
            _elapsed = 0f;

            if (_countDown)
                _remaining = Shikaku.Menu.GameSession.TimeLimitSeconds;

            UpdateTimerText();
        }

        private void UpdateTimerText()
        {
            if (timerText == null) return;

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                timerText.text = UNAVAILABLE_VALUE;
                return;
            }

            if (_countDown)
                timerText.text = FormatLiveTime(_remaining);
            else
                timerText.text = FormatLiveTime(_elapsed);
        }
        private void RefreshAll()
        {
            bool shouldShowBest = ShouldShowBestHudSlot();
            if (_bestStat != null)
                _bestStat.style.display =
                    shouldShowBest ? DisplayStyle.Flex : DisplayStyle.None;

            if (_bestDivider != null)
                _bestDivider.style.display =
                    shouldShowBest ? DisplayStyle.Flex : DisplayStyle.None;

            RefreshProgressTexts();
            UpdateTimerText();
            RefreshTitle();

            if (ShouldShowBestHudSlot())
                RefreshBestTime();

            bool shouldCountDown =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial &&
                Shikaku.Menu.GameSession.TimeLimitSeconds > 0;

            if (shouldCountDown != _countDown)
            {
                _countDown = shouldCountDown;

                if (_countDown)
                    _remaining = Shikaku.Menu.GameSession.TimeLimitSeconds;
            }
            else
            {
                _countDown = shouldCountDown;
            }

            RefreshHintButtonState();
            ScheduleResponsiveGameplayLayout();
        }

        private void RefreshTitle()
        {
            if (titleText == null) return;

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                titleText.text = "HOW TO PLAY";
                if (_subtitleText != null)
                    _subtitleText.text = "GUIDED PUZZLE";
                return;
            }

            if (Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.Daily)
            {
                titleText.text = BuildDailyTitle();
                if (_subtitleText != null)
                    _subtitleText.text = "DAILY PUZZLE";
                return;
            }

            int level = Shikaku.Menu.GameSession.LevelIndex;
            if (level <= 0) level = 1;

            string sizeLabel = BuildPackLabel();

            titleText.text = $"LEVEL {level}";
            if (_subtitleText != null)
                _subtitleText.text = sizeLabel;
        }
        private string BuildDailyTitle()
        {
            string sessionPuzzleId =
                Shikaku.Menu.GameSession.GetPuzzleId();

            // Session format:
            // Daily/2026/Daily_2026_Easy|Daily_2026_06_02_Easy

            string puzzleId = sessionPuzzleId;

            int pipeIndex = puzzleId.LastIndexOf('|');
            if (pipeIndex >= 0 && pipeIndex < puzzleId.Length - 1)
                puzzleId = puzzleId.Substring(pipeIndex + 1);

            const string prefix = "Daily_";

            if (!puzzleId.StartsWith(prefix))
                return "DAILY PUZZLE";

            string[] parts = puzzleId.Split('_');

            // Expected:
            // [0] Daily
            // [1] 2026
            // [2] 06
            // [3] 02
            // [4] Easy
            if (parts.Length < 5)
                return "DAILY PUZZLE";

            if (!int.TryParse(parts[1], out int year) ||
                !int.TryParse(parts[2], out int month) ||
                !int.TryParse(parts[3], out int day))
            {
                return "DAILY PUZZLE";
            }

            if (!System.DateTime.TryParse(
                    $"{year}-{month}-{day}",
                    out System.DateTime date))
            {
                return "DAILY PUZZLE";
            }

            string difficulty = parts[4];
            string dateText = $"{date:MMMM} {GetOrdinalDay(date.Day)}";

            return $"{dateText}  •  {difficulty}";
        }

        private static string GetOrdinalDay(int day)
        {
            // 11th, 12th and 13th are exceptions.
            int lastTwoDigits = day % 100;

            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
                return $"{day}th";

            switch (day % 10)
            {
                case 1:
                    return $"{day}st";

                case 2:
                    return $"{day}nd";

                case 3:
                    return $"{day}rd";

                default:
                    return $"{day}th";
            }
        }

        public void ForceRefreshHeader()
        {
            // Every call to this method follows a successful puzzle load.
            // Clear per-puzzle state here so a hint used on the previous
            // puzzle cannot mark the new puzzle's best time as assisted.
            _analyticsHintsUsedThisPuzzle = 0;
            _exitAnalyticsRecorded = false;

            SetPuzzleInteractionLocked(false);
            SetHeaderNextVisible(false);
            HideSolvedModal();

            if (Shikaku.Menu.GameSession.Mode != Shikaku.Menu.MenuMode.TimeTrial)
                ResetTimer();

            _timerRunning = true;

            RefreshTitle();

            if (ShouldShowBestHudSlot())
                RefreshBestTime();

            UpdateTimerText();
            ScheduleResponsiveGameplayLayout();
        }

        private string BuildPackLabel()
        {
            string packPath = Shikaku.Menu.GameSession.PackPath;

            // If we're in Story/Adventure, show chapter number in the "sizeLabel" spot.
            if (Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.Story)
            {
                int chapter = ExtractChapterNumber(Shikaku.Menu.GameSession.PackPath);
                return chapter > 0 ? $"CH {chapter}" : "CH";
            }

            // Example packPath: "FreePlay/freeplay_3x3_pack01"
            if (string.IsNullOrEmpty(packPath))
                return "";

            // Find an "NxM" pattern anywhere in the path (like 3x3, 10x10, 12x8)
            for (int i = 0; i < packPath.Length - 2; i++)
            {
                if (!char.IsDigit(packPath[i])) continue;

                int a = i;
                while (a < packPath.Length && char.IsDigit(packPath[a])) a++;
                if (a >= packPath.Length || packPath[a] != 'x') continue;

                int b = a + 1;
                if (b >= packPath.Length || !char.IsDigit(packPath[b])) continue;

                int c = b;
                while (c < packPath.Length && char.IsDigit(packPath[c])) c++;

                string w = packPath.Substring(i, a - i);
                string h = packPath.Substring(b, c - b);
                return $"{w}x{h}"; // <-- NO "pack"
            }

            return "";
        }

        private static int ExtractChapterNumber(string packPath)
        {
            // Supports "ch01", "ch1", "chapter02" anywhere in the pack path
            if (string.IsNullOrEmpty(packPath)) return 0;

            string s = packPath.ToLowerInvariant();

            int idx = s.IndexOf("chapter", System.StringComparison.Ordinal);
            int tokenLen = 7;

            if (idx < 0)
            {
                idx = s.IndexOf("ch", System.StringComparison.Ordinal);
                tokenLen = 2;
            }

            if (idx < 0) return 0;

            int i = idx + tokenLen;

            while (i < s.Length && !char.IsDigit(s[i])) i++;

            int start = i;
            while (i < s.Length && char.IsDigit(s[i])) i++;

            if (start == i) return 0;

            if (int.TryParse(s.Substring(start, i - start), out int chapter))
                return chapter;

            return 0;
        }

        private void RefreshProgressTexts()
        {
            if (cellProgressText == null)
                return;

            if (_progressCaption != null)
            {
                _progressCaption.text =
                    Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.TimeTrial
                        ? "SCORE"
                        : "FILLED";
            }

            // TIME TRIAL: show the player's current session score.
            if (Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial)
            {
                int completedPuzzleScore =
                    Shikaku.Menu.GameSession.TimeTrialCompletedSquares;

                int currentPuzzleScore =
                    board != null
                        ? board.CountCompletedSquaresForTimeTrial()
                        : 0;

                int currentSessionScore =
                    completedPuzzleScore + currentPuzzleScore;

                cellProgressText.text = currentSessionScore.ToString();
                return;
            }

            // ALL OTHER MODES: retain the normal filled-cell progress.
            if (board == null)
                return;

            if (board.Width <= 0 || board.Height <= 0)
                return;

            int boardArea = board.Width * board.Height;
            int totalCells = 0;
            int filled = 0;

            for (int i = 0; i < boardArea; i++)
            {
                if (!board.IsPlayableCell(i))
                    continue;

                totalCells++;

                if (board.IsCellAssigned(i))
                    filled++;
            }

            cellProgressText.text = $"{filled}/{totalCells}";
        }

        private string CurrentPuzzleKey()
        {
            string id = Shikaku.Menu.GameSession.GetPuzzleId();
            return string.IsNullOrEmpty(id) ? "unknown" : id;
        }

        private void RefreshBestTime()
        {
            if (bestTimeText == null)
                return;

            // Default to hidden. It is only turned on below
            // when the displayed best time actually used a hint.
            if (_bestHintIcon != null)
            {
                _bestHintIcon.style.display =
                    DisplayStyle.None;
            }

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                if (_bestCaption != null)
                    _bestCaption.text = "MODE";

                bestTimeText.text = "GUIDED";
                return;
            }

            if (_bestCaption != null)
                _bestCaption.text = "BEST";

            if (Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial)
            {
                int size =
                    Shikaku.Menu.GameSession.Size;

                int bestSquares =
                    Shikaku.Menu.GameSession
                        .GetTimeTrialBestSquares(size);

                bestTimeText.text =
                    bestSquares > 0
                        ? bestSquares.ToString()
                        : UNAVAILABLE_VALUE;

                return;
            }

            if (!Shikaku.Menu.PuzzleProgressStore.TryGetBestTime(
                    CurrentPuzzleKey(),
                    out float best,
                    out bool bestTimeUsedHint))
            {
                bestTimeText.text = UNAVAILABLE_VALUE;
                return;
            }

            bestTimeText.text =
                FormatResultTime(best);

            if (_bestHintIcon != null)
            {
                _bestHintIcon.style.display =
                    bestTimeUsedHint
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }
        }

        private void SaveBestTimeIfBetter(
    float seconds,
    bool usedHint)
        {
            Shikaku.Menu.PuzzleProgressStore.SaveBestTimeIfBetter(
                CurrentPuzzleKey(),
                seconds,
                usedHint);
        }

        // Live timer: no milliseconds; always show minutes and two-digit seconds.
        // Examples: 0:05 -> "0:05", 0:42 -> "0:42", 1:05 -> "1:05"
        private static string FormatLiveTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;

            int total = Mathf.FloorToInt(seconds);
            int minutes = total / 60;
            int secs = total % 60;

            return $"{minutes}:{secs:00}";
        }

        // Result/best time: show hundredths, no leading zero minutes
        // Examples: 0:05.20 -> "5.20", 0:42.03 -> "42.03", 1:05.20 -> "1:05.20"
        private static string FormatResultTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;

            int minutes = Mathf.FloorToInt(seconds / 60f);
            float secRemainder = seconds - minutes * 60f; // 0.00 .. 59.99

            if (minutes <= 0)
                return secRemainder.ToString("0.00");     // "5.20", "42.03"

            // minutes shown => pad seconds to 2 digits
            return $"{minutes}:{secRemainder:00.00}";     // "1:05.20"
        }

        public void ExitToMenuPublic()
        {
            RequestExitToMenu();
        }

        private void MarkCompleted()
        {
            Shikaku.Menu.PuzzleProgressStore.MarkCompleted(
                CurrentPuzzleKey());
        }

        private void UpdateProgressionOnSolved()
        {
            switch (Shikaku.Menu.GameSession.Mode)
            {
                case Shikaku.Menu.MenuMode.FreePlay:
                    if (!string.IsNullOrEmpty(Shikaku.Menu.GameSession.PackPath))
                        Shikaku.Menu.Progression.MarkSolvedFreePlayPack(
                            Shikaku.Menu.GameSession.PackPath,
                            Shikaku.Menu.GameSession.LevelIndex);
                    else
                        Shikaku.Menu.Progression.MarkSolvedFreePlay(
                            Shikaku.Menu.GameSession.Size,
                            Shikaku.Menu.GameSession.LevelIndex);

                    PlayerPrefs.Save();
                    break;

                case Shikaku.Menu.MenuMode.Story:
                    if (string.IsNullOrEmpty(Shikaku.Menu.GameSession.PackPath))
                        return;

                    Shikaku.Menu.Progression.MarkSolvedStoryPack(
                        Shikaku.Menu.GameSession.PackPath,
                        Shikaku.Menu.GameSession.LevelIndex);

                    PlayerPrefs.Save();
                    break;

                case Shikaku.Menu.MenuMode.Daily:
                    MarkDailyCompleted();
                    break;
            }
        }

        private void TryAwardCompletionHintReward()
        {
            string rewardTitle = null;
            string claimKey = null;
            int hintReward = 0;

            switch (Shikaku.Menu.GameSession.Mode)
            {
                case Shikaku.Menu.MenuMode.Story:
                    if (TryGetCompletedStoryChapter(
                            Shikaku.Menu.GameSession.PackPath,
                            out int chapterNumber))
                    {
                        rewardTitle =
                            $"Chapter {chapterNumber} Complete!";
                        claimKey =
                            "completion_hint_reward_story_v1_" +
                            Shikaku.Menu.GameSession.PackPath;
                        hintReward = 3;
                    }
                    break;

                case Shikaku.Menu.MenuMode.FreePlay:
                    if (TryGetCompletedFreePlayCollection(
                            Shikaku.Menu.GameSession.PackPath,
                            out string collectionId))
                    {
                        rewardTitle = "Puzzle Pack Complete!";
                        claimKey =
                            "completion_hint_reward_freeplay_v1_" +
                            collectionId;
                        hintReward = 10;
                    }
                    break;

                case Shikaku.Menu.MenuMode.Daily:
                    DateTime dailyDate = ParseDailyDateFromSession();
                    if (dailyDate.Date == DateTime.Today &&
                        Shikaku.Menu.Progression
                            .IsDailyDateFullyCompleted(dailyDate.Date))
                    {
                        rewardTitle = "Daily Challenge Complete!";
                        claimKey =
                            "completion_hint_reward_daily_v1_" +
                            dailyDate.ToString("yyyyMMdd");
                        hintReward = 1;
                    }
                    break;
            }

            if (hintReward <= 0 ||
                string.IsNullOrEmpty(rewardTitle) ||
                string.IsNullOrEmpty(claimKey) ||
                SaveManager.HasClaimedReward(claimKey))
            {
                return;
            }

            // Save the claim marker and balance change atomically so replaying
            // a completed final puzzle can never grant the reward again.
            if (!HintWallet.TryClaimReward(hintReward, claimKey))
                return;

            _pendingCompletionRewardTitle = rewardTitle;
            _pendingCompletionHintReward = hintReward;

            Debug.Log(
                $"GameplayHUD: {rewardTitle} Awarded " +
                $"{hintReward} hint(s).");
        }

        private static bool TryGetCompletedStoryChapter(
            string packPath,
            out int chapterNumber)
        {
            chapterNumber = 0;
            if (string.IsNullOrEmpty(packPath) ||
                !IsCatalogPackCompleted(packPath))
            {
                return false;
            }

            IReadOnlyList<Shikaku.Menu.PuzzleCatalog.PackInfo> chapters =
                Shikaku.Menu.PuzzleCatalog.StoryPacks;
            for (int i = 0; i < chapters.Count; i++)
            {
                if (!string.Equals(
                        chapters[i].PackPath,
                        packPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                chapterNumber = i + 1;
                return true;
            }

            return false;
        }

        private static bool TryGetCompletedFreePlayCollection(
            string currentPackPath,
            out string collectionId)
        {
            collectionId = null;
            if (string.IsNullOrEmpty(currentPackPath))
                return false;

            IReadOnlyList<
                Shikaku.Menu.PuzzleCatalog.FreePlayCollectionInfo>
                collections =
                    Shikaku.Menu.PuzzleCatalog.FreePlayCollections;

            for (int collectionIndex = 0;
                 collectionIndex < collections.Count;
                 collectionIndex++)
            {
                Shikaku.Menu.PuzzleCatalog.FreePlayCollectionInfo
                    collection = collections[collectionIndex];
                bool containsCurrentPack = false;
                bool allPacksCompleted =
                    collection.SizePacks.Count > 0;

                for (int packIndex = 0;
                     packIndex < collection.SizePacks.Count;
                     packIndex++)
                {
                    string packPath =
                        collection.SizePacks[packIndex].PackPath;
                    containsCurrentPack |= string.Equals(
                        packPath,
                        currentPackPath,
                        StringComparison.Ordinal);
                    allPacksCompleted &=
                        IsCatalogPackCompleted(packPath);
                }

                if (!containsCurrentPack || !allPacksCompleted)
                    continue;

                collectionId = collection.PackId;
                return !string.IsNullOrEmpty(collectionId);
            }

            return false;
        }

        private static bool IsCatalogPackCompleted(string packPath)
        {
            IReadOnlyList<string> puzzleIds =
                Shikaku.Menu.PuzzleCatalog.GetPackPuzzleIds(packPath);
            if (puzzleIds == null || puzzleIds.Count == 0)
                return false;

            for (int i = 0; i < puzzleIds.Count; i++)
            {
                if (!Shikaku.Menu.PuzzleProgressStore.IsCompleted(
                        puzzleIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void MarkDailyCompleted()
        {
            System.DateTime date = ParseDailyDateFromSession();
            string difficulty = ParseDailyDifficultyFromPuzzleId();

            if (string.IsNullOrEmpty(difficulty))
            {
                Debug.LogWarning("Daily puzzle solved, but difficulty could not be detected from puzzleId: "
                                 + Shikaku.Menu.GameSession.GetPuzzleId());
                return;
            }

            Shikaku.Menu.Progression.SetDailyCompleted(date, difficulty, true);

            Debug.Log($"Marked daily completed: {date:yyyy-MM-dd} {difficulty}");
        }

        private System.DateTime ParseDailyDateFromSession()
        {
            string key = Shikaku.Menu.GameSession.DailyKey;

            if (!string.IsNullOrEmpty(key) && key.Length == 8)
            {
                if (System.DateTime.TryParseExact(
                        key,
                        "yyyyMMdd",
                        null,
                        System.Globalization.DateTimeStyles.None,
                        out System.DateTime parsed))
                {
                    return parsed.Date;
                }
            }

            Debug.LogWarning("Could not parse GameSession.DailyKey. Falling back to today. DailyKey was: " + key);
            return System.DateTime.Today;
        }

        private string ParseDailyDifficultyFromPuzzleId()
        {
            string puzzleId = Shikaku.Menu.GameSession.GetPuzzleId();

            // Example:
            // Daily/2026/Daily_2026_Easy|Daily_2026_06_16_Easy

            if (string.IsNullOrEmpty(puzzleId))
                return "";

            if (puzzleId.EndsWith("_Easy"))
                return "Easy";

            if (puzzleId.EndsWith("_Medium"))
                return "Medium";

            if (puzzleId.EndsWith("_Hard"))
                return "Hard";

            return "";
        }

        public static bool IsCompleted(string puzzleId)
        {
            return Shikaku.Menu.PuzzleProgressStore.IsCompleted(puzzleId);
        }

        private bool ShouldShowBestTime()
        {
            if (Shikaku.Menu.GameSession.IsTutorial)
                return false;

            var mode = Shikaku.Menu.GameSession.Mode;
            return mode != Shikaku.Menu.MenuMode.TimeTrial;
        }
        private bool ShouldShowBestHudSlot()
        {
            // Time Trial uses this slot for best score. Every other mode,
            // including Adventure, uses it for best puzzle time.
            return true;
        }

        private void OnHintButtonPressed()
        {
            if (board == null)
            {
                Debug.LogWarning(
                    "GameplayHUD: Cannot use a hint because BoardController is missing."
                );
                return;
            }

            bool isTimeTrial =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial;

            // TIME TRIAL:
            // Keep the existing 10-second hint behavior.
            if (isTimeTrial)
            {
                if (!_timerRunning ||
                    _timeTrialSummaryShown ||
                    _remaining < TIME_TRIAL_HINT_COST_SECONDS)
                {
                    RefreshHintButtonState();
                    return;
                }

                _remaining = Mathf.Max(
                    0f,
                    _remaining - TIME_TRIAL_HINT_COST_SECONDS
                );

                UpdateTimerText();
                PlayHintPenaltyFlash();
                RefreshHintButtonState();

                board.ShowHint();
                RecordHintUsed("time_penalty");

                // Handle exactly 10 seconds remaining.
                if (_remaining <= 0f)
                {
                    _timerRunning = false;
                    ShowTimeTrialSummary();
                }

                return;
            }

            // NORMAL / DAILY:
            // If the player owns a hint, spend it immediately.
            if (HintWallet.Balance > 0)
            {
                if (HintWallet.TrySpendHint())
                {
                    board.ShowHint();
                    RecordHintUsed("inventory");
                }

                return;
            }

            // No purchased hints:
            // automatically try to show a rewarded ad.
            if (AdsManager.Instance == null)
            {
                Debug.LogWarning(
                    "GameplayHUD: Rewarded hint ad is unavailable because AdsManager is missing."
                );
                return;
            }

            bool adStarted =
                AdsManager.Instance.ShowRewardedHint(() =>
                {
                    if (board != null)
                    {
                        board.ShowHint();
                        RecordHintUsed("rewarded_ad");
                    }
                });

            if (!adStarted)
            {
                Debug.LogWarning(
                    "GameplayHUD: Rewarded hint ad is not currently available."
                );
                ShowAdStatus(
                    AdsManager.Instance.RewardedHintStatusMessage);
            }
        }

        private void ShowAdStatus(string message)
        {
            if (_adStatus == null || string.IsNullOrWhiteSpace(message))
                return;

            _hideAdStatusTask?.Pause();
            _adStatus.text = message;
            _adStatus.RemoveFromClassList("screen-hidden");
            _hideAdStatusTask = _adStatus.schedule.Execute(() =>
            {
                _adStatus?.AddToClassList("screen-hidden");
            });
            _hideAdStatusTask.ExecuteLater(2200);
        }

        private void RecordHintUsed(string source)
        {
            if (board == null)
                return;

            _analyticsHintsUsedThisPuzzle++;
            GameAnalytics.HintUsed(
                board.Width,
                board.Height,
                source,
                _analyticsHintsUsedThisPuzzle);
        }

        private void RefreshHintButtonState()
        {
            if (hintButton == null)
                return;

            bool isTutorial = Shikaku.Menu.GameSession.IsTutorial;
            bool isTimeTrial =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial;
            int balance = HintWallet.Balance;
            bool showWalletBadges = !isTutorial && !isTimeTrial;
            bool canUsePurchasedHint = balance > 0;
            bool canWatchRewardedAd =
                AdsManager.Instance != null &&
                AdsManager.Instance.CanOfferRewardedHint;
            bool showCountBadge = showWalletBadges && canUsePurchasedHint;
            bool showVideoBadge =
                showWalletBadges && !canUsePurchasedHint && canWatchRewardedAd;

            if (_hintCountBadge != null)
            {
                _hintCountBadge.text = balance.ToString();
                _hintCountBadge.style.display =
                    showCountBadge ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_hintVideoBadge != null)
                _hintVideoBadge.style.display =
                    showVideoBadge ? DisplayStyle.Flex : DisplayStyle.None;

            if (_puzzleInteractionLocked)
            {
                hintButton.SetEnabled(false);
                return;
            }

            if (!isTimeTrial)
            {
                hintButton.SetEnabled(
                    canUsePurchasedHint || canWatchRewardedAd);
                return;
            }

            bool canAffordHint =
                _timerRunning &&
                !_timeTrialSummaryShown &&
                _remaining >= TIME_TRIAL_HINT_COST_SECONDS;

            hintButton.SetEnabled(canAffordHint);
        }

        private void PlayHintPenaltyFlash()
        {
            if (timerText == null)
                return;

            if (_hintPenaltyFlashRoutine != null)
                StopCoroutine(_hintPenaltyFlashRoutine);

            _hintPenaltyFlashRoutine = StartCoroutine(HintPenaltyFlashRoutine());
        }

        private IEnumerator HintPenaltyFlashRoutine()
        {
            timerText.AddToClassList("gameplay-timer-penalty");
            yield return new WaitForSecondsRealtime(hintPenaltyFlashDuration);
            timerText.RemoveFromClassList("gameplay-timer-penalty");
            _hintPenaltyFlashRoutine = null;
        }

        private void ShowResetConfirmation()
        {
            // In Time Trial, reset immediately with no confirmation popup.
            if (Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial)
            {
                ConfirmReset();
                return;
            }

            // Other modes still require confirmation.
            ShowModal(_resetModal);
        }

        private void CancelReset()
        {
            HideModal(_resetModal);
        }

        private void ConfirmReset()
        {
            HideModal(_resetModal);
            board?.RestartPuzzle();
            ResetHintIdleCountdown();
            _analyticsHintsUsedThisPuzzle = 0;
            _exitAnalyticsRecorded = false;

            if (Shikaku.Menu.GameSession.Mode != Shikaku.Menu.MenuMode.TimeTrial)
                ResetTimer();

            RefreshAll();
        }

        private void ShowSolvedModal(bool timeTrialSummary)
        {
            SetHeaderNextVisible(false);

            bool isTutorial =
                Shikaku.Menu.GameSession.IsTutorial;

            bool isDaily =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.Daily;

            // ---------------------------------------------------------
            // TITLE
            // ---------------------------------------------------------
            if (_solvedTitle != null)
            {
                _solvedTitle.text = isTutorial
                    ? "Tutorial Complete!"
                    : timeTrialSummary
                        ? "Time's Up!"
                        : "Puzzle Solved!";
            }

            // ---------------------------------------------------------
            // DEFAULT: hide the new Daily-only button
            // ---------------------------------------------------------
            if (_solvedSameDifficultyButton != null)
            {
                _solvedSameDifficultyButton.AddToClassList(
                    "screen-hidden");

                _solvedSameDifficultyButton.SetEnabled(true);
            }

            // ---------------------------------------------------------
            // PRIMARY BUTTON
            // ---------------------------------------------------------
            if (_solvedPrimaryButton != null)
            {
                _solvedPrimaryButton.RemoveFromClassList("screen-hidden");

                _solvedPrimaryButton.text = isTutorial
                    ? Shikaku.Menu.GameSession.TutorialReturnToSettings
                        ? "Back to Settings"
                        : "Continue Adventure"
                    : timeTrialSummary
                        ? "Retry"
                        : "Next Puzzle";

                _solvedPrimaryButton.SetEnabled(true);
            }

            // ---------------------------------------------------------
            // DAILY PUZZLE BUTTONS
            // ---------------------------------------------------------
            if (isDaily &&
                !isTutorial &&
                !timeTrialSummary)
            {
                System.DateTime currentDate =
                    ParseDailyDateFromSession();

                string currentDifficulty =
                    ParseDailyDifficultyFromPuzzleId();

                // ---------------------------------------------
                // Existing sequence button:
                // Easy -> Medium
                // Medium -> Hard
                // Hard -> next day's Easy
                // ---------------------------------------------
                if (_solvedPrimaryButton != null)
                {
                    bool hasNextDailyPuzzle =
                        board != null &&
                        board.HasNextDailyPuzzleAvailable();

                    if (!hasNextDailyPuzzle)
                    {
                        _solvedPrimaryButton.AddToClassList("screen-hidden");
                        _solvedPrimaryButton.SetEnabled(false);
                    }

                    switch (currentDifficulty)
                    {
                        case "Easy":
                            _solvedPrimaryButton.text =
                                "Next: Medium";
                            break;

                        case "Medium":
                            _solvedPrimaryButton.text =
                                "Next: Hard";
                            break;

                        case "Hard":
                            _solvedPrimaryButton.text =
                                "Next: Easy";
                            break;

                        default:
                            _solvedPrimaryButton.text =
                                "Next Puzzle";
                            break;
                    }
                }

                // ---------------------------------------------
                // Same-difficulty button.
                //
                // Only show it when another calendar day
                // actually exists.
                // ---------------------------------------------
                if (_solvedSameDifficultyButton != null &&
                    !string.IsNullOrEmpty(currentDifficulty) &&
                    currentDate.Date < System.DateTime.Today)
                {
                    _solvedSameDifficultyButton.text =
                        $"Next Day's {currentDifficulty}";

                    _solvedSameDifficultyButton.RemoveFromClassList(
                        "screen-hidden");
                }
            }

            // ---------------------------------------------------------
            // EXIT BUTTON
            // ---------------------------------------------------------
            if (_solvedExitButton != null)
            {
                _solvedExitButton.text =
                    isTutorial &&
                    !Shikaku.Menu.GameSession.TutorialReturnToSettings
                        ? "Home"
                        : "Exit";
            }

            ShowModal(_solvedModal);
            ShowPendingHintCelebrations();
        }

        private void ShowPendingHintCelebrations()
        {
            bool hasCompletionReward =
                _pendingCompletionHintReward > 0 &&
                !string.IsNullOrEmpty(_pendingCompletionRewardTitle);
            bool hasStreakReward = _pendingStreakUpdate.Advanced;

            if (!hasCompletionReward && !hasStreakReward)
                return;

            if (_streakCelebration == null ||
                _streakCelebrationTitle == null ||
                _streakCelebrationCountRow == null ||
                _streakCelebrationNumber == null ||
                _streakCelebrationReward == null)
            {
                return;
            }

            if (_streakCelebrationRoutine != null)
                StopCoroutine(_streakCelebrationRoutine);

            _streakCelebrationRoutine = StartCoroutine(
                PlayPendingHintCelebrations(
                    hasCompletionReward,
                    hasStreakReward));
        }

        private IEnumerator PlayPendingHintCelebrations(
            bool hasCompletionReward,
            bool hasStreakReward)
        {
            if (hasCompletionReward)
            {
                string rewardTitle = _pendingCompletionRewardTitle;
                int hintReward = _pendingCompletionHintReward;
                _pendingCompletionRewardTitle = null;
                _pendingCompletionHintReward = 0;

                yield return PlayHintRewardCelebration(
                    rewardTitle,
                    hintReward);
            }

            if (hasStreakReward)
            {
                DailyStreakUpdate update = _pendingStreakUpdate;
                _pendingStreakUpdate = default;
                yield return PlayStreakCelebration(update);
            }

            _streakCelebrationRoutine = null;
            TryShowPendingRatePrompt();
        }

        private void TryShowPendingRatePrompt()
        {
            bool isTimeTrial =
                Shikaku.Menu.GameSession.Mode ==
                Shikaku.Menu.MenuMode.TimeTrial;
            if (isTimeTrial ||
                Shikaku.Menu.GameSession.IsTutorial ||
                !IsModalVisible(_solvedModal))
            {
                return;
            }

            if (StoreRatingService.TryRequestPendingNativeReview())
                return;

            if (_ratePromptModal == null ||
                !StoreRatingService.MarkPromptShown())
            {
                return;
            }

            ShowModal(_ratePromptModal);
        }

        private void RateFromPrompt()
        {
            StoreRatingService.OpenStoreListing();
            HideModal(_ratePromptModal);
        }

        private void DismissRatePrompt()
        {
            HideModal(_ratePromptModal);
        }

        private IEnumerator PlayHintRewardCelebration(
            string title,
            int hintReward)
        {
            _streakCelebrationTitle.text = title;
            _streakCelebrationCountRow.AddToClassList("screen-hidden");
            _streakCelebrationReward.text =
                hintReward == 1
                    ? "+1 Hint"
                    : $"+{hintReward} Hints";
            _streakCelebration.RemoveFromClassList(
                "gameplay-streak-celebration-visible");
            _streakCelebration.RemoveFromClassList("screen-hidden");

            yield return null;

            _streakCelebration.AddToClassList(
                "gameplay-streak-celebration-visible");
            yield return new WaitForSecondsRealtime(4f);
            _streakCelebration.RemoveFromClassList(
                "gameplay-streak-celebration-visible");
            yield return new WaitForSecondsRealtime(0.28f);
            _streakCelebration.AddToClassList("screen-hidden");
            _streakCelebrationCountRow.RemoveFromClassList("screen-hidden");
        }

        private IEnumerator PlayStreakCelebration(
            DailyStreakUpdate update)
        {
            _streakCelebrationTitle.text = "DAILY STREAK";
            _streakCelebrationCountRow.RemoveFromClassList("screen-hidden");
            _streakCelebrationReward.text =
                update.HintReward == 1
                    ? "+1 Hint"
                    : $"+{update.HintReward} Hints";
            _streakCelebrationNumber.text =
                update.PreviousStreak.ToString();
            _streakCelebration.RemoveFromClassList(
                "gameplay-streak-celebration-visible");
            _streakCelebrationNumber.RemoveFromClassList(
                "gameplay-streak-number-pop");
            _streakCelebration.RemoveFromClassList("screen-hidden");

            yield return null;

            _streakCelebration.AddToClassList(
                "gameplay-streak-celebration-visible");

            yield return new WaitForSecondsRealtime(0.4f);

            _streakCelebrationNumber.text =
                update.CurrentStreak.ToString();
            _streakCelebrationNumber.AddToClassList(
                "gameplay-streak-number-pop");

            yield return new WaitForSecondsRealtime(0.32f);

            _streakCelebrationNumber.RemoveFromClassList(
                "gameplay-streak-number-pop");

            yield return new WaitForSecondsRealtime(2f);

            _streakCelebration.RemoveFromClassList(
                "gameplay-streak-celebration-visible");

            yield return new WaitForSecondsRealtime(0.28f);

            _streakCelebration.AddToClassList("screen-hidden");
        }

        private void HideStreakCelebrationImmediately()
        {
            if (_streakCelebrationRoutine != null)
            {
                StopCoroutine(_streakCelebrationRoutine);
                _streakCelebrationRoutine = null;
            }

            _streakCelebration?.RemoveFromClassList(
                "gameplay-streak-celebration-visible");
            _streakCelebrationNumber?.RemoveFromClassList(
                "gameplay-streak-number-pop");
            _streakCelebrationCountRow?.RemoveFromClassList(
                "screen-hidden");
            _streakCelebration?.AddToClassList("screen-hidden");
        }

        private void HideSolvedModal()
        {
            HideStreakCelebrationImmediately();
            HideModal(_solvedModal);
            _solvedPrimaryButton?.SetEnabled(true);

            bool canShowNext =
                _puzzleInteractionLocked &&
                !_timeTrialSummaryShown &&
                Shikaku.Menu.GameSession.Mode !=
                    Shikaku.Menu.MenuMode.TimeTrial;

            if (Shikaku.Menu.GameSession.Mode ==
                    Shikaku.Menu.MenuMode.Daily &&
                (board == null ||
                 !board.HasNextDailyPuzzleAvailable()))
            {
                canShowNext = false;
            }

            SetHeaderNextVisible(canShowNext);
        }

        private void OnSolvedPrimaryPressed()
        {
            _solvedPrimaryButton?.SetEnabled(false);
            SetHeaderNextVisible(false);

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                CompleteTutorialPrimaryAction();
                return;
            }

            if (_timeTrialSummaryShown)
            {
                RestartTimeTrialRun();
                return;
            }

            board?.ContinueAfterSolved();
        }

        private void OnSolvedSameDifficultyPressed()
        {
            if (Shikaku.Menu.GameSession.Mode !=
                Shikaku.Menu.MenuMode.Daily)
            {
                return;
            }

            _solvedSameDifficultyButton?.SetEnabled(false);
            _solvedPrimaryButton?.SetEnabled(false);

            SetHeaderNextVisible(false);

            board?.ContinueAfterSolvedSameDailyDifficulty();
        }

        private void OnHeaderNextPressed()
        {
            if (!_puzzleInteractionLocked ||
                _timeTrialSummaryShown ||
                Shikaku.Menu.GameSession.Mode ==
                    Shikaku.Menu.MenuMode.TimeTrial)
            {
                return;
            }

            _nextPuzzleButton?.SetEnabled(false);
            SetHeaderNextVisible(false);

            if (Shikaku.Menu.GameSession.IsTutorial)
            {
                CompleteTutorialPrimaryAction();
                return;
            }

            board?.ContinueAfterSolved();
        }

        private void CompleteTutorialPrimaryAction()
        {
            if (Shikaku.Menu.GameSession.TutorialReturnToSettings)
            {
                ExitTutorialToMenu();
                return;
            }

            HideTutorialPresentation(restoreControls: true);
            Shikaku.Menu.GameSession.EndTutorialSession();
            board?.ContinueAfterSolved();
        }
        private void SetHeaderNextVisible(bool visible)
        {
            if (_nextPuzzleButton == null)
                return;

            if (visible)
                _nextPuzzleButton.RemoveFromClassList("screen-hidden");
            else
                _nextPuzzleButton.AddToClassList("screen-hidden");

            _nextPuzzleButton.SetEnabled(visible);
        }

        private void SetPuzzleInteractionLocked(bool locked)
        {
            _puzzleInteractionLocked = locked;
            board?.SetInputLocked(locked);

            resetButton?.SetEnabled(!locked);

            if (locked)
                hintButton?.SetEnabled(false);
            else
                RefreshHintButtonState();

            RefreshBoardModalState();
        }

        private void HideAllModals()
        {
            HideStreakCelebrationImmediately();
            HideModal(_solvedModal);
            HideModal(_ratePromptModal);
            HideModal(_resetModal);
            HideModal(_exitModal);
        }

        private void ShowModal(VisualElement modal)
        {
            modal?.RemoveFromClassList("screen-hidden");
            RefreshBoardModalState();
        }

        private void HideModal(VisualElement modal)
        {
            modal?.AddToClassList("screen-hidden");
            RefreshBoardModalState();
        }

        private static bool IsModalVisible(VisualElement modal)
        {
            return modal != null && !modal.ClassListContains("screen-hidden");
        }

        private void RefreshBoardModalState()
        {
            if (_boardCanvasGroup == null)
                return;

            bool modalVisible =
                IsModalVisible(_solvedModal) ||
                IsModalVisible(_ratePromptModal) ||
                IsModalVisible(_resetModal) ||
                IsModalVisible(_exitModal);

            bool boardLocked =
                modalVisible || _puzzleInteractionLocked;

            _boardCanvasGroup.alpha = 1f;
            _boardCanvasGroup.interactable = !boardLocked;
            _boardCanvasGroup.blocksRaycasts = !boardLocked;

            TMPro.TMP_Text[] boardLabels =
                boardContainer.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (TMPro.TMP_Text boardLabel in boardLabels)
                boardLabel.enabled = !modalVisible;
        }

        private bool CloseTopModal()
        {
            if (IsModalVisible(_ratePromptModal))
            {
                DismissRatePrompt();
                return true;
            }

            // Leave confirmation is the highest-priority remaining modal.
            // Pressing Back here means "Stay".
            if (IsModalVisible(_exitModal))
            {
                CancelExitToMenu();
                return true;
            }

            if (IsModalVisible(_resetModal))
            {
                CancelReset();
                return true;
            }

            if (IsModalVisible(_solvedModal))
            {
                HideSolvedModal();
                return true;
            }

            return false;
        }
    }
}
