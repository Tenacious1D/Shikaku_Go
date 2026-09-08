using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shikaku.Menu;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Analytics;

#if UNITY_6000_2_OR_NEWER
using UnityEngine.UnityConsent;
#endif

/// <summary>
/// Central Analytics service for Shikaku Go.
///
/// Example:
/// GameAnalytics.PuzzleStarted(
///     "FreePlay",
///     6,
///     "Medium",
///     "FreePlay_6x6_042"
/// );
/// </summary>
public sealed class GameAnalytics : MonoBehaviour
{
    private const string ConsentPlayerPrefsKey = "analytics_consent";
    private const string EverGrantedPlayerPrefsKey = "analytics_ever_granted";
    private const int ConsentUndecided = 0;
    private const int ConsentGranted = 1;
    private const int ConsentDenied = 2;
    private const int MaximumPendingEvents = 100;

    private static GameAnalytics instance;
    private static bool servicesInitialized;
    private static bool analyticsEnabled;
    private static bool initializationStarted;
    private static readonly Queue<CustomEvent> PendingEvents =
        new Queue<CustomEvent>();

    public enum AnalyticsConsentChoice
    {
        Undecided = ConsentUndecided,
        Granted = ConsentGranted,
        Denied = ConsentDenied
    }

    public static event Action ConsentChanged;

    public static bool IsReady =>
        servicesInitialized && analyticsEnabled;
    public static bool HasConsentChoice =>
        ConsentChoice != AnalyticsConsentChoice.Undecided;
    public static bool IsConsentGranted =>
        ConsentChoice == AnalyticsConsentChoice.Granted;
    public static bool HasEverGrantedConsent =>
        PlayerPrefs.GetInt(EverGrantedPlayerPrefsKey, 0) == 1;
    public static bool CanRequestDataDeletion => servicesInitialized;
    public static AnalyticsConsentChoice ConsentChoice =>
        (AnalyticsConsentChoice)PlayerPrefs.GetInt(
            ConsentPlayerPrefsKey,
            ConsentUndecided);

    // Automatically creates this object before the first scene loads.
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateAutomatically()
    {
        if (instance != null)
            return;

        ApplySavedConsentBeforeInitialization();

        GameObject analyticsObject =
            new GameObject(nameof(GameAnalytics));

        instance = analyticsObject.AddComponent<GameAnalytics>();

        DontDestroyOnLoad(analyticsObject);
    }

    private async void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeAsync();
    }

    private static async Task InitializeAsync()
    {
        if (initializationStarted)
            return;

        initializationStarted = true;

        try
        {
            var options = new InitializationOptions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            options.SetEnvironmentName("development");
#else
            options.SetEnvironmentName("production");
#endif

            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(options);

            servicesInitialized = true;

            // Apply consent previously selected by the player.
            if (IsConsentGranted)
                EnableAnalyticsCollection();
            else
                DisableAnalyticsCollection();

            Debug.Log("GameAnalytics: Unity Services initialized.");
        }
        catch (Exception exception)
        {
            servicesInitialized = false;
            analyticsEnabled = false;

            Debug.LogError(
                $"GameAnalytics initialization failed: {exception}"
            );
        }
    }

    // ------------------------------------------------------------------
    // Consent and privacy
    // ------------------------------------------------------------------

    /// <summary>
    /// Call this after the player accepts analytics collection.
    /// </summary>
    public static void GrantConsent()
    {
        PlayerPrefs.SetInt(ConsentPlayerPrefsKey, ConsentGranted);
        PlayerPrefs.SetInt(EverGrantedPlayerPrefsKey, 1);
        PlayerPrefs.Save();

        EnableAnalyticsCollection();
        ConsentChanged?.Invoke();
    }

    /// <summary>
    /// Call this when the player declines or disables analytics.
    /// </summary>
    public static void DenyConsent()
    {
        PlayerPrefs.SetInt(ConsentPlayerPrefsKey, ConsentDenied);
        PlayerPrefs.Save();

        DisableAnalyticsCollection();
        ConsentChanged?.Invoke();
    }

    /// <summary>
    /// Requests deletion of this player's Unity Analytics data.
    /// </summary>
    public static bool RequestDataDeletion()
    {
        if (!servicesInitialized)
        {
            Debug.LogWarning(
                "GameAnalytics: Services have not initialized."
            );

            return false;
        }

        try
        {
            PlayerPrefs.SetInt(ConsentPlayerPrefsKey, ConsentDenied);
            PlayerPrefs.SetInt(EverGrantedPlayerPrefsKey, 0);
            PlayerPrefs.Save();
            DisableAnalyticsCollection();
            AnalyticsService.Instance.RequestDataDeletion();
            ConsentChanged?.Invoke();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Analytics deletion request failed: {exception}"
            );
            return false;
        }
    }

    private static void EnableAnalyticsCollection()
    {
        try
        {
#if UNITY_6000_2_OR_NEWER
            EndUserConsent.SetConsentState(
                new ConsentState
                {
                    AnalyticsIntent = ConsentStatus.Granted
                }
            );
#else
            if (!servicesInitialized)
                return;

            AnalyticsService.Instance.StartDataCollection();
#endif

            analyticsEnabled = servicesInitialized;

            if (analyticsEnabled)
                FlushPendingEvents();

            Debug.Log("GameAnalytics: Data collection enabled.");
        }
        catch (Exception exception)
        {
            analyticsEnabled = false;

            Debug.LogError(
                $"GameAnalytics could not enable collection: {exception}"
            );
        }
    }

    private static void DisableAnalyticsCollection()
    {
        analyticsEnabled = false;
        PendingEvents.Clear();

        try
        {
#if UNITY_6000_2_OR_NEWER
            EndUserConsent.SetConsentState(
                new ConsentState
                {
                    AnalyticsIntent = ConsentStatus.Denied
                }
            );
#else
            if (servicesInitialized)
                AnalyticsService.Instance.StopDataCollection();
#endif
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"GameAnalytics could not stop collection: {exception.Message}"
            );
        }
    }

    private static void ApplySavedConsentBeforeInitialization()
    {
#if UNITY_6000_2_OR_NEWER
        try
        {
            EndUserConsent.SetConsentState(
                new ConsentState
                {
                    AnalyticsIntent = IsConsentGranted
                        ? ConsentStatus.Granted
                        : ConsentStatus.Denied
                }
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "GameAnalytics could not apply the startup consent state: " +
                exception.Message
            );
        }
#endif
    }

    // ------------------------------------------------------------------
    // Puzzle events
    // ------------------------------------------------------------------

    public static void PuzzleStarted(
        string mode,
        int boardSize,
        string difficulty,
        string puzzleId)
    {
        Record(
            new CustomEvent("puzzle_started")
            {
                { "mode", Clean(mode) },
                { "board_size", boardSize },
                { "difficulty", Clean(difficulty) },
                { "puzzle_id", Clean(puzzleId) }
            }
        );
    }

    public static void PuzzleCompleted(
        string mode,
        int boardSize,
        string difficulty,
        string puzzleId,
        float completionSeconds,
        int hintsUsed)
    {
        Record(
            new CustomEvent("puzzle_completed")
            {
                { "mode", Clean(mode) },
                { "board_size", boardSize },
                { "difficulty", Clean(difficulty) },
                { "puzzle_id", Clean(puzzleId) },
                { "completion_seconds", completionSeconds },
                { "hints_used", hintsUsed }
            }
        );
    }

    public static void PuzzleAbandoned(
        string mode,
        int boardSize,
        string difficulty,
        string puzzleId,
        float elapsedSeconds,
        int filledCells)
    {
        Record(
            new CustomEvent("puzzle_abandoned")
            {
                { "mode", Clean(mode) },
                { "board_size", boardSize },
                { "difficulty", Clean(difficulty) },
                { "puzzle_id", Clean(puzzleId) },
                { "elapsed_seconds", elapsedSeconds },
                { "filled_cells", filledCells }
            }
        );
    }

    public static void HintUsed(
        string mode,
        int boardSize,
        string difficulty,
        string puzzleId,
        int hintsUsed)
    {
        Record(
            new CustomEvent("hint_used")
            {
                { "mode", Clean(mode) },
                { "board_size", boardSize },
                { "difficulty", Clean(difficulty) },
                { "puzzle_id", Clean(puzzleId) },
                { "hints_used", hintsUsed }
            }
        );
    }

    // ------------------------------------------------------------------
    // Daily puzzle events
    // ------------------------------------------------------------------

    public static void DailyPuzzleStarted(
        string date,
        string difficulty,
        string puzzleId)
    {
        Record(
            new CustomEvent("daily_puzzle_started")
            {
                { "puzzle_date", Clean(date) },
                { "difficulty", Clean(difficulty) },
                { "puzzle_id", Clean(puzzleId) }
            }
        );
    }

    public static void DailyPuzzleCompleted(
        string date,
        string difficulty,
        string puzzleId,
        float completionSeconds,
        int hintsUsed)
    {
        Record(
            new CustomEvent("daily_puzzle_completed")
            {
                { "puzzle_date", Clean(date) },
                { "difficulty", Clean(difficulty) },
                { "puzzle_id", Clean(puzzleId) },
                { "completion_seconds", completionSeconds },
                { "hints_used", hintsUsed }
            }
        );
    }

    // ------------------------------------------------------------------
    // Time Trial events
    // ------------------------------------------------------------------

    public static void TimeTrialFinished(
        int boardSize,
        int score,
        int puzzlesCompleted,
        int bestScore,
        float durationSeconds)
    {
        Record(
            new CustomEvent("time_trial_finished")
            {
                { "board_size", boardSize },
                { "score", score },
                { "puzzles_completed", puzzlesCompleted },
                { "best_score", bestScore },
                { "duration_seconds", durationSeconds }
            }
        );
    }

    // ------------------------------------------------------------------
    // Advertising events
    // ------------------------------------------------------------------

    public static void RewardedAdRequested(string placement)
    {
        Record(
            new CustomEvent("rewarded_ad_requested")
            {
                { "placement", Clean(placement) }
            }
        );
    }

    public static void RewardedAdCompleted(
        string placement,
        string rewardType)
    {
        Record(
            new CustomEvent("rewarded_ad_completed")
            {
                { "placement", Clean(placement) },
                { "reward_type", Clean(rewardType) }
            }
        );
    }

    // ------------------------------------------------------------------
    // Purchase events
    // ------------------------------------------------------------------

    public static void RemoveAdsPurchased(
        string productId,
        double price,
        string currency)
    {
        PurchaseCompleted(
            productId,
            "remove_ads",
            1,
            price,
            currency);
    }

    public static void BundlePurchased(
        string productId,
        int hintsGranted,
        double price,
        string currency)
    {
        Record(
            new CustomEvent("purchase_completed")
            {
                { "product_id", Clean(productId) },
                { "product_type", "bundle" },
                { "quantity", 1 },
                { "price", price },
                { "currency", Clean(currency) },
                { "hints_granted", hintsGranted },
                { "removes_ads", true }
            });
    }

    // ------------------------------------------------------------------
    // Canonical game events
    // ------------------------------------------------------------------

    public static void ScreenViewed(string screen)
    {
        Record(
            new CustomEvent("screen_viewed")
            {
                { "screen", Clean(screen) }
            });
    }

    public static void PuzzleStarted(int width, int height)
    {
        if (GameSession.IsTutorial || GameSession.Mode == MenuMode.TimeTrial)
            return;

        Record(CreatePuzzleEvent("puzzle_started", width, height));
    }

    public static void PuzzleCompleted(
        int width,
        int height,
        float completionSeconds,
        int hintsUsed)
    {
        if (GameSession.IsTutorial || GameSession.Mode == MenuMode.TimeTrial)
            return;

        CustomEvent analyticsEvent =
            CreatePuzzleEvent("puzzle_completed", width, height);
        analyticsEvent.Add("completion_seconds", completionSeconds);
        analyticsEvent.Add("hints_used", hintsUsed);
        Record(analyticsEvent);
    }

    public static void PuzzleAbandoned(
        int width,
        int height,
        float elapsedSeconds,
        int filledCells)
    {
        if (GameSession.IsTutorial || GameSession.Mode == MenuMode.TimeTrial)
            return;

        CustomEvent analyticsEvent =
            CreatePuzzleEvent("puzzle_abandoned", width, height);
        analyticsEvent.Add("elapsed_seconds", elapsedSeconds);
        analyticsEvent.Add("filled_cells", filledCells);
        Record(analyticsEvent);
    }

    public static void HintUsed(
        int width,
        int height,
        string source,
        int hintsUsed)
    {
        CustomEvent analyticsEvent =
            CreatePuzzleEvent("hint_used", width, height);
        analyticsEvent.Add("source", Clean(source));
        analyticsEvent.Add("hints_used", hintsUsed);
        Record(analyticsEvent);
    }

    public static void TimeTrialStarted(
        int width,
        int height,
        int durationSeconds)
    {
        Record(
            new CustomEvent("time_trial_started")
            {
                { "board_width", width },
                { "board_height", height },
                { "duration_seconds", durationSeconds }
            });
    }

    public static void TimeTrialFinished(
        int width,
        int height,
        int score,
        int puzzlesCompleted,
        int bestScore,
        float durationSeconds,
        string reason)
    {
        Record(
            new CustomEvent("time_trial_finished")
            {
                { "board_width", width },
                { "board_height", height },
                { "score", score },
                { "puzzles_completed", puzzlesCompleted },
                { "best_score", bestScore },
                { "duration_seconds", durationSeconds },
                { "finish_reason", Clean(reason) }
            });
    }

    public static void TutorialStarted()
    {
        Record(new CustomEvent("tutorial_started"));
    }

    public static void TutorialStepReached(int step, string stepName)
    {
        Record(
            new CustomEvent("tutorial_step_reached")
            {
                { "step", step },
                { "step_name", Clean(stepName) }
            });
    }

    public static void TutorialCompleted()
    {
        Record(new CustomEvent("tutorial_completed"));
    }

    public static void TutorialExited(int lastStep)
    {
        Record(
            new CustomEvent("tutorial_exited")
            {
                { "last_step", lastStep }
            });
    }

    public static void PurchaseCompleted(
        string productId,
        string productType,
        int quantity,
        double price,
        string currency)
    {
        Record(
            new CustomEvent("purchase_completed")
            {
                { "product_id", Clean(productId) },
                { "product_type", Clean(productType) },
                { "quantity", quantity },
                { "price", price },
                { "currency", Clean(currency) }
            });
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private static void Record(CustomEvent analyticsEvent)
    {
        if (!IsConsentGranted)
            return;

        if (!IsReady)
        {
            if (PendingEvents.Count < MaximumPendingEvents)
                PendingEvents.Enqueue(analyticsEvent);

            return;
        }

        RecordNow(analyticsEvent);
    }

    private static void RecordNow(CustomEvent analyticsEvent)
    {
        if (!IsReady)
            return;

        try
        {
            AnalyticsService.Instance.RecordEvent(analyticsEvent);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"GameAnalytics failed to record an event: " +
                exception.Message
            );
        }
    }

    private static void FlushPendingEvents()
    {
        while (IsReady && PendingEvents.Count > 0)
            RecordNow(PendingEvents.Dequeue());
    }

    private static CustomEvent CreatePuzzleEvent(
        string eventName,
        int width,
        int height)
    {
        var analyticsEvent = new CustomEvent(eventName)
        {
            {
                "mode",
                GameSession.IsTutorial
                    ? "Tutorial"
                    : Clean(GameSession.Mode.ToString())
            },
            { "board_width", width },
            { "board_height", height },
            { "puzzle_id", Clean(GameSession.GetPuzzleId()) },
            { "pack_id", GetPackId(GameSession.PackPath) },
            { "level_index", GameSession.LevelIndex }
        };

        if (GameSession.Mode == MenuMode.Daily)
            analyticsEvent.Add("daily_key", Clean(GameSession.DailyKey));

        return analyticsEvent;
    }

    private static string GetPackId(string packPath)
    {
        if (string.IsNullOrWhiteSpace(packPath))
            return "Unknown";

        string normalized = packPath
            .Replace(Convert.ToChar(92), '/')
            .TrimEnd('/');
        int slash = normalized.LastIndexOf('/');

        return Clean(slash >= 0
            ? normalized.Substring(slash + 1)
            : normalized);
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Trim();
    }
}