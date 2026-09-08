using System;
using System.Collections.Generic;
using System.Globalization;
using Shikaku.Menu;
using Shikaku.Services;
using Shikaku.SaveSystem;
using UnityEngine;

namespace Shikaku.Achievements
{
    /// <summary>
    /// Stores achievement progress locally first, then reports unlocked
    /// achievements whenever the current platform is configured and signed in.
    /// </summary>
    public static class AchievementService
    {
        private const string LocalUnlockedPrefix = "achievement_unlocked_v1_";
        private const string GoogleReportedPrefix = "achievement_google_reported_v1_";
        private const string AppleReportedPrefix = "achievement_apple_reported_v1_";
        private const string GoogleStepsPrefix = "achievement_google_steps_v1_";
        private const string AppleStepsPrefix = "achievement_apple_steps_v1_";
        private const string LifetimeCountedPuzzlePrefix =
            "achievement_lifetime_counted_puzzle_v1_";
        private const string LifetimeCountGenerationKey =
            "achievement_lifetime_count_generation_v1";
        private const string LifetimePuzzleCountKey = "achievement_lifetime_puzzles_v1";
        private const string TimeTrialModeMaskKey = "achievement_time_trial_modes_v1";
        private const string HighestTimeTrialScoreKey = "achievement_time_trial_high_score_v1";
        private const string TripleThreatObservedKey = "achievement_triple_threat_v1";

        private const int RequiredTimeTrialMask =
            (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3);

        private static readonly AchievementId[] AllIds =
        {
            AchievementId.AdventureBegins,
            AchievementId.SeasonedExplorer,
            AchievementId.FreeThinker,
            AchievementId.PackItUp,
            AchievementId.TripleThreat,
            AchievementId.PerfectWeek,
            AchievementId.AgainstTheClock,
            AchievementId.Clockwork,
            AchievementId.CenturyClub,
            AchievementId.ShikakuMaster
        };

        private static IAchievementPlatformAdapter _platform;
        private static bool _initialized;
        private static bool _authenticationInProgress;

        private static bool _manualAuthenticationQueued;
        private static bool _showAchievementsAfterAuthentication;
        public static event Action<AchievementId> UnlockedLocally;

        public static int LifetimePuzzleCount =>
            SaveManager.LifetimePuzzleCount;

        public static int HighestTimeTrialScore =>
            SaveManager.HighestAchievementTimeTrialScore;

        public static bool IsPlatformConfigured
        {
            get
            {
                EnsureInitialized();
                return _platform.IsConfigured;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureInitialized();
            ReconcileLocalProgress();
        }

        /// <summary>
        /// Records a solved board in every mode. Each normalized puzzle ID can
        /// increase the lifetime total only once, so replays are ignored.
        /// </summary>
        public static void RecordPuzzleCompleted(string puzzleId)
        {
            EnsureInitialized();

            string normalizedId = PuzzleProgressStore.NormalizePuzzleId(puzzleId);
            int lifetimeCount = LifetimePuzzleCount;
            if (!string.IsNullOrEmpty(normalizedId))
            {
                lifetimeCount =
                    SaveManager.RecordLifetimePuzzle(normalizedId);
            }
            else
            {
                Debug.LogWarning(
                    "A solved puzzle had no stable puzzle ID, so it was not " +
                    "added to the lifetime achievement count.");
            }

            Evaluate(AchievementId.CenturyClub, lifetimeCount >= 100);
            Evaluate(AchievementId.ShikakuMaster, lifetimeCount >= 1000);
            Evaluate(
                AchievementId.PerfectWeek,
                DailyStreakService.CurrentStreak >= 7);

            switch (GameSession.Mode)
            {
                case MenuMode.Story:
                    EvaluateAdventureAchievements();
                    break;
                case MenuMode.FreePlay:
                    EvaluateFreePlayAchievements();
                    break;
                case MenuMode.Daily:
                    ObserveCurrentDailyTripleThreat();
                    break;
            }

            PlayerPrefs.Save();
            SynchronizeIncrementalProgress();
        }

        /// <summary>
        /// A mode counts for Against the Clock after a finished run scores above
        /// zero. Clockwork uses the final session score, including partial board
        /// progress.
        /// </summary>
        public static void RecordTimeTrialSessionFinished(
            int boardSize,
            int score)
        {
            EnsureInitialized();

            score = Mathf.Max(0, score);
            if (score > HighestTimeTrialScore)
                SaveManager.SetHighestAchievementTimeTrialScore(score);

            if (score > 0)
            {
                int bit = TimeTrialBitForSize(boardSize);
                if (bit != 0)
                {
                    SaveManager.AddAchievementTimeTrialMode(bit);

                }
            }

            Evaluate(
                AchievementId.AgainstTheClock,
                HasCompletedEveryTimeTrialMode());
            Evaluate(
                AchievementId.Clockwork,
                HighestTimeTrialScore >= 300);

            PlayerPrefs.Save();
            SynchronizeIncrementalProgress();
        }

        public static bool IsUnlocked(AchievementId id)
        {
            return SaveManager.IsAchievementUnlocked(InternalId(id));
        }

        /// <summary>
        /// Rechecks every condition that can be reconstructed from the save.
        /// This makes platform synchronization resilient to offline play.
        /// </summary>
        public static void ReconcileLocalProgress()
        {
            EnsureInitialized();

            EvaluateAdventureAchievements();
            EvaluateFreePlayAchievements();
            Evaluate(
                AchievementId.TripleThreat,
                SaveManager.TripleThreatObserved);
            Evaluate(
                AchievementId.PerfectWeek,
                DailyStreakService.CurrentStreak >= 7);
            Evaluate(
                AchievementId.AgainstTheClock,
                HasCompletedEveryTimeTrialMode());
            Evaluate(
                AchievementId.Clockwork,
                HighestTimeTrialScore >= 300);
            Evaluate(
                AchievementId.CenturyClub,
                LifetimePuzzleCount >= 100);
            Evaluate(
                AchievementId.ShikakuMaster,
                LifetimePuzzleCount >= 1000);

            PlayerPrefs.Save();
            SynchronizePendingUnlocks();
        }

        public static void ShowPlatformAchievements()
        {
            EnsureInitialized();

            if (!_platform.IsConfigured)
            {
                _platform.ShowAchievements();
                return;
            }

            if (_platform.IsAuthenticated)
            {
                _platform.ShowAchievements();
                return;
            }

            _showAchievementsAfterAuthentication = true;
            Authenticate(userInitiated: true);
        }

        public static string GetDisplayName(AchievementId id)
        {
            switch (id)
            {
                case AchievementId.AdventureBegins: return "Adventure Begins";
                case AchievementId.SeasonedExplorer: return "Seasoned Explorer";
                case AchievementId.FreeThinker: return "Free Thinker";
                case AchievementId.PackItUp: return "Pack It Up";
                case AchievementId.TripleThreat: return "Triple Threat";
                case AchievementId.PerfectWeek: return "Perfect Week";
                case AchievementId.AgainstTheClock: return "Against the Clock";
                case AchievementId.Clockwork: return "Clockwork";
                case AchievementId.CenturyClub: return "Century Club";
                case AchievementId.ShikakuMaster: return "Shikaku Master";
                default: return id.ToString();
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _platform = AchievementPlatformFactory.Create();

            if (_platform.IsConfigured)
                Authenticate(userInitiated: false);
        }

        private static void Authenticate(bool userInitiated)
        {
            if (userInitiated)
                _manualAuthenticationQueued = true;

            if (_authenticationInProgress)
                return;

            bool isManualAttempt = _manualAuthenticationQueued;
            _manualAuthenticationQueued = false;
            _authenticationInProgress = true;
            _platform.Authenticate(isManualAttempt, success =>
            {
                _authenticationInProgress = false;
                if (!success)
                {
                    // A Settings-button tap may arrive while automatic
                    // sign-in is still running. If that silent attempt fails,
                    // immediately continue with the queued interactive sign-in.
                    if (_manualAuthenticationQueued && !isManualAttempt)
                    {
                        Authenticate(userInitiated: true);
                        return;
                    }

                    _showAchievementsAfterAuthentication = false;
                    Debug.LogWarning(
                        "Achievement sign-in was unavailable. Local unlocks " +
                        "will be synchronized after a later sign-in.");
                    return;
                }

                _manualAuthenticationQueued = false;
                SynchronizePendingUnlocks();
                if (_showAchievementsAfterAuthentication)
                {
                    _showAchievementsAfterAuthentication = false;
                    _platform.ShowAchievements();
                }
            });
        }

        private static void EvaluateAdventureAchievements()
        {
            IReadOnlyList<PuzzleCatalog.PackInfo> packs =
                PuzzleCatalog.StoryPacks;
            int completedChapters = CountCompletedPacks(packs);

            Evaluate(
                AchievementId.AdventureBegins,
                packs.Count > 0 && IsPackCompleted(packs[0].PackPath));
            Evaluate(
                AchievementId.SeasonedExplorer,
                completedChapters >= 10);
        }

        private static void EvaluateFreePlayAchievements()
        {
            int completedCollections =
                CountCompletedFreePlayCollections(
                    PuzzleCatalog.FreePlayCollections);

            Evaluate(
                AchievementId.FreeThinker,
                completedCollections >= 1);
            Evaluate(
                AchievementId.PackItUp,
                completedCollections >= 5);
        }

        private static void ObserveCurrentDailyTripleThreat()
        {
            if (!DateTime.TryParseExact(
                    GameSession.DailyKey,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime date))
            {
                return;
            }

            if (!Progression.IsDailyDateFullyCompleted(date.Date))
                return;

            SaveManager.MarkTripleThreatObserved();
            Evaluate(AchievementId.TripleThreat, true);
        }

        private static int CountCompletedPacks(
            IReadOnlyList<PuzzleCatalog.PackInfo> packs)
        {
            if (packs == null)
                return 0;

            int completed = 0;
            for (int i = 0; i < packs.Count; i++)
            {
                if (IsPackCompleted(packs[i].PackPath))
                    completed++;
            }

            return completed;
        }

        private static int CountCompletedFreePlayCollections(
            IReadOnlyList<PuzzleCatalog.FreePlayCollectionInfo> collections)
        {
            if (collections == null)
                return 0;

            int completed = 0;
            for (int collectionIndex = 0;
                 collectionIndex < collections.Count;
                 collectionIndex++)
            {
                PuzzleCatalog.FreePlayCollectionInfo collection =
                    collections[collectionIndex];
                bool collectionCompleted =
                    collection.SizePacks.Count > 0;

                for (int sizeIndex = 0;
                     sizeIndex < collection.SizePacks.Count;
                     sizeIndex++)
                {
                    if (IsPackCompleted(
                            collection.SizePacks[sizeIndex].PackPath))
                    {
                        continue;
                    }

                    collectionCompleted = false;
                    break;
                }

                if (collectionCompleted)
                    completed++;
            }

            return completed;
        }

        private static bool IsPackCompleted(string packPath)
        {
            IReadOnlyList<string> puzzleIds =
                PuzzleCatalog.GetPackPuzzleIds(packPath);
            if (puzzleIds == null || puzzleIds.Count == 0)
                return false;

            for (int i = 0; i < puzzleIds.Count; i++)
            {
                if (!PuzzleProgressStore.IsCompleted(puzzleIds[i]))
                    return false;
            }

            return true;
        }

        private static bool HasCompletedEveryTimeTrialMode()
        {
            int mask = SaveManager.AchievementTimeTrialModeMask;
            return (mask & RequiredTimeTrialMask) ==
                   RequiredTimeTrialMask;
        }

        private static int TimeTrialBitForSize(int boardSize)
        {
            switch (boardSize)
            {
                case 3: return 1 << 0;
                case 4: return 1 << 1;
                case 5: return 1 << 2;
                case 6: return 1 << 3;
                default: return 0;
            }
        }

        private static void Evaluate(AchievementId id, bool condition)
        {
            if (!condition)
                return;

            if (!IsUnlocked(id))
            {
                SaveManager.UnlockAchievement(InternalId(id));
                Debug.Log(
                    $"Achievement unlocked locally: {GetDisplayName(id)}");
                UnlockedLocally?.Invoke(id);
            }

            TrySubmit(id);
        }

        private static void SynchronizePendingUnlocks()
        {
            if (!_platform.IsConfigured || !_platform.IsAuthenticated)
                return;

            for (int i = 0; i < AllIds.Length; i++)
            {
                AchievementId id = AllIds[i];
                if (TryGetIncrementalProgress(
                        id,
                        out int completedSteps,
                        out int requiredSteps))
                {
                    TrySubmitIncrementalProgress(
                        id,
                        completedSteps,
                        requiredSteps);
                }
                else if (IsUnlocked(id))
                {
                    TrySubmit(id);
                }
            }
        }

        private static void SynchronizeIncrementalProgress()
        {
            if (!_platform.IsConfigured || !_platform.IsAuthenticated)
                return;

            for (int i = 0; i < AllIds.Length; i++)
            {
                AchievementId id = AllIds[i];
                if (TryGetIncrementalProgress(
                        id,
                        out int completedSteps,
                        out int requiredSteps))
                {
                    TrySubmitIncrementalProgress(
                        id,
                        completedSteps,
                        requiredSteps);
                }
            }
        }

        private static void TrySubmit(AchievementId id)
        {
            if (!_platform.IsConfigured || !_platform.IsAuthenticated)
                return;

            if (TryGetIncrementalProgress(
                    id,
                    out int completedSteps,
                    out int requiredSteps))
            {
                TrySubmitIncrementalProgress(
                    id,
                    completedSteps,
                    requiredSteps);
                return;
            }

            string platformId = GetCurrentPlatformId(id);
            if (!AchievementPlatformIds.IsConfigured(platformId))
            {
                Debug.LogWarning(
                    $"Achievement '{GetDisplayName(id)}' is unlocked locally, " +
                    "but its platform ID is still a placeholder.");
                return;
            }

            string reportedKey = ReportedKey(id);
            if (string.IsNullOrEmpty(reportedKey) ||
                PlayerPrefs.GetInt(reportedKey, 0) == 1)
            {
                return;
            }

            _platform.Unlock(platformId, success =>
            {
                if (!success)
                {
                    Debug.LogWarning(
                        $"Could not report '{GetDisplayName(id)}'. It remains " +
                        "queued for the next sign-in.");
                    return;
                }

                PlayerPrefs.SetInt(reportedKey, 1);
                PlayerPrefs.Save();
            });
        }

        private static void TrySubmitIncrementalProgress(
            AchievementId id,
            int completedSteps,
            int requiredSteps)
        {
            if (!_platform.IsConfigured || !_platform.IsAuthenticated)
                return;

            string platformId = GetCurrentPlatformId(id);
            if (!AchievementPlatformIds.IsConfigured(platformId))
                return;

            int steps = Mathf.Clamp(completedSteps, 0, requiredSteps);
            if (steps <= 0)
                return;

            string stepsKey = ReportedStepsKey(id);
            if (string.IsNullOrEmpty(stepsKey) ||
                PlayerPrefs.GetInt(stepsKey, 0) >= steps)
            {
                return;
            }

            _platform.SetStepsAtLeast(
                platformId,
                steps,
                requiredSteps,
                success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning(
                            $"Could not report {steps}/{requiredSteps} steps " +
                            $"for '{GetDisplayName(id)}'. Progress remains " +
                            "queued for the next sign-in.");
                        return;
                    }

                    int previousSteps = PlayerPrefs.GetInt(stepsKey, 0);
                    PlayerPrefs.SetInt(stepsKey, Mathf.Max(previousSteps, steps));
                    if (steps >= requiredSteps)
                    {
                        string reportedKey = ReportedKey(id);
                        if (!string.IsNullOrEmpty(reportedKey))
                            PlayerPrefs.SetInt(reportedKey, 1);
                    }

                    PlayerPrefs.Save();
                });
        }

        private static bool TryGetIncrementalProgress(
            AchievementId id,
            out int completedSteps,
            out int requiredSteps)
        {
            completedSteps = 0;
            requiredSteps = 0;

            switch (id)
            {
                case AchievementId.SeasonedExplorer:
                    completedSteps = CountCompletedPacks(PuzzleCatalog.StoryPacks);
                    requiredSteps = 10;
                    return true;
                case AchievementId.PackItUp:
                    completedSteps = CountCompletedFreePlayCollections(
                        PuzzleCatalog.FreePlayCollections);
                    requiredSteps = 5;
                    return true;
                case AchievementId.AgainstTheClock:
                    completedSteps = CountCompletedTimeTrialModes();
                    requiredSteps = 4;
                    return true;
                case AchievementId.CenturyClub:
                    completedSteps = LifetimePuzzleCount;
                    requiredSteps = 100;
                    return true;
                case AchievementId.ShikakuMaster:
                    completedSteps = LifetimePuzzleCount;
                    requiredSteps = 1000;
                    return true;
                default:
                    return false;
            }
        }

        private static int CountCompletedTimeTrialModes()
        {
            int mask = SaveManager.AchievementTimeTrialModeMask &
                       RequiredTimeTrialMask;
            int completed = 0;
            while (mask != 0)
            {
                completed += mask & 1;
                mask >>= 1;
            }

            return completed;
        }

        private static string GetCurrentPlatformId(AchievementId id)
        {
            switch (_platform.Platform)
            {
                case AchievementPlatform.GooglePlay:
                    return AchievementPlatformIds.GetGoogleId(id);
                case AchievementPlatform.AppleGameCenter:
                    return AchievementPlatformIds.GetAppleId(id);
                default:
                    return string.Empty;
            }
        }

        private static string ReportedKey(AchievementId id)
        {
            switch (_platform.Platform)
            {
                case AchievementPlatform.GooglePlay:
                    return GoogleReportedPrefix + InternalId(id);
                case AchievementPlatform.AppleGameCenter:
                    return AppleReportedPrefix + InternalId(id);
                default:
                    return string.Empty;
            }
        }

        private static string ReportedStepsKey(AchievementId id)
        {
            switch (_platform.Platform)
            {
                case AchievementPlatform.GooglePlay:
                    return GoogleStepsPrefix + InternalId(id);
                case AchievementPlatform.AppleGameCenter:
                    return AppleStepsPrefix + InternalId(id);
                default:
                    return string.Empty;
            }
        }

        private static string LifetimeCountedPuzzleKey(string puzzleId)
        {
            int generation = Mathf.Max(
                0,
                PlayerPrefs.GetInt(LifetimeCountGenerationKey, 0));
            return LifetimeCountedPuzzlePrefix + generation + "_" + puzzleId;
        }

        private static string LocalUnlockedKey(AchievementId id) =>
            LocalUnlockedPrefix + InternalId(id);

        private static string InternalId(AchievementId id) =>
            id.ToString().ToLowerInvariant();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static string BuildDebugSummary()
        {
            var lines = new List<string>
            {
                $"Lifetime puzzles: {LifetimePuzzleCount}",
                $"Highest Time Trial score: {HighestTimeTrialScore}",
                $"Time Trial mode mask: {SaveManager.AchievementTimeTrialModeMask}",
                $"Platform configured: {IsPlatformConfigured}"
            };

            for (int i = 0; i < AllIds.Length; i++)
            {
                AchievementId id = AllIds[i];
                lines.Add(
                    $"[{(IsUnlocked(id) ? 'x' : ' ')}] " +
                    GetDisplayName(id));
            }

            return string.Join("\n", lines);
        }

        public static void ResetLocalAchievementDataForTesting()
        {
            for (int i = 0; i < AllIds.Length; i++)
            {
                string internalId = InternalId(AllIds[i]);
                PlayerPrefs.DeleteKey(LocalUnlockedPrefix + internalId);
                PlayerPrefs.DeleteKey(GoogleReportedPrefix + internalId);
                PlayerPrefs.DeleteKey(AppleReportedPrefix + internalId);
                PlayerPrefs.DeleteKey(GoogleStepsPrefix + internalId);
                PlayerPrefs.DeleteKey(AppleStepsPrefix + internalId);
            }

            SaveManager.ResetAchievementDataForTesting();
            PlayerPrefs.DeleteKey(LifetimePuzzleCountKey);
            PlayerPrefs.SetInt(
                LifetimeCountGenerationKey,
                PlayerPrefs.GetInt(LifetimeCountGenerationKey, 0) + 1);
            PlayerPrefs.DeleteKey(TimeTrialModeMaskKey);
            PlayerPrefs.DeleteKey(HighestTimeTrialScoreKey);
            PlayerPrefs.DeleteKey(TripleThreatObservedKey);
            PlayerPrefs.Save();
        }
#endif
    }
}
