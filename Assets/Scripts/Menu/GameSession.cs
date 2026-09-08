using System;
using UnityEngine;
using System.Collections.Generic;

namespace Shikaku.Menu
{
    /// <summary>
    /// Tiny, PlayerPrefs-backed session state used to pass menu selections into Gameplay.
    /// BoardController already reads PlayerPrefs["puzzleId"], so we just standardize extras.
    /// </summary>
    public static class GameSession
    {
        private const string KMode = "session_mode";
        private const string KSize = "session_size";
        private const string KLevelIndex = "session_levelIndex"; // 1-based
        private const string KDailyKey = "session_dailyKey"; // yyyyMMdd
        private const string KPackPath = "session_packPath"; // FreePlay/... etc
        private const string KTimeLimitSeconds = "session_timeLimitSeconds"; // for time trial
        private const string KTimeTrialSolvedCount = "session_timeTrialSolvedCount";
        private const string KTimeTrialCompletedSquares = "session_timeTrialCompletedSquares";
        private const string KIsTutorial = "session_isTutorial";
        private const string KTutorialReturnToSettings =
            "session_tutorialReturnToSettings";
        private const string KTutorialAutoLaunched =
            "tutorial_autoLaunched_v1";
        private const string KTutorialCompleted =
            "tutorial_completed_v1";

        public static MenuMode Mode
        {
            get => (MenuMode)PlayerPrefs.GetInt(KMode, (int)MenuMode.None);
            set => PlayerPrefs.SetInt(KMode, (int)value);
        }

        public static int Size
        {
            get => PlayerPrefs.GetInt(KSize, 0);
            set => PlayerPrefs.SetInt(KSize, value);
        }

        public static int LevelIndex
        {
            get => PlayerPrefs.GetInt(KLevelIndex, 0);
            set => PlayerPrefs.SetInt(KLevelIndex, value);
        }


        public static int TimeLimitSeconds
        {
            get => PlayerPrefs.GetInt(KTimeLimitSeconds, 0);
            set => PlayerPrefs.SetInt(KTimeLimitSeconds, value);
        }

        public static int TimeTrialSolvedCount
        {
            get => PlayerPrefs.GetInt(KTimeTrialSolvedCount, 0);
            set => PlayerPrefs.SetInt(KTimeTrialSolvedCount, value);
        }

        public static int TimeTrialCompletedSquares
        {
            get => PlayerPrefs.GetInt(KTimeTrialCompletedSquares, 0);
            set => PlayerPrefs.SetInt(KTimeTrialCompletedSquares, value);
        }

        public static bool IsTutorial
        {
            get => PlayerPrefs.GetInt(KIsTutorial, 0) == 1;
            set => PlayerPrefs.SetInt(KIsTutorial, value ? 1 : 0);
        }

        public static bool TutorialReturnToSettings
        {
            get => PlayerPrefs.GetInt(KTutorialReturnToSettings, 0) == 1;
            set => PlayerPrefs.SetInt(
                KTutorialReturnToSettings,
                value ? 1 : 0);
        }

        public static bool ShouldAutoLaunchTutorial =>
            !TutorialCompleted &&
            PlayerPrefs.GetInt(KTutorialAutoLaunched, 0) == 0;

        public static bool TutorialCompleted =>
            Shikaku.SaveSystem.SaveManager.TutorialCompleted;

        /// <summary>
        /// Configures Gameplay to use the real first Adventure puzzle while
        /// keeping tutorial behavior isolated from normal Story sessions.
        /// </summary>
        public static bool TryPrepareTutorial(
            bool returnToSettings,
            bool markAutoLaunched)
        {
            IReadOnlyList<PuzzleCatalog.PackInfo> packs =
                PuzzleCatalog.StoryPacks;

            if (packs == null || packs.Count == 0)
            {
                Debug.LogError(
                    "Tutorial could not start because no Adventure packs were found.");
                return false;
            }

            string packPath = packs[0].PackPath;
            IReadOnlyList<string> puzzleIds =
                PuzzleCatalog.GetPackPuzzleIds(packPath);

            if (puzzleIds == null || puzzleIds.Count == 0)
            {
                Debug.LogError(
                    $"Tutorial could not start because '{packPath}' has no puzzles.");
                return false;
            }

            Mode = MenuMode.Story;
            LevelIndex = 1;
            PackPath = packPath;
            SetPuzzle(puzzleIds[0]);
            IsTutorial = true;
            TutorialReturnToSettings = returnToSettings;

            if (markAutoLaunched)
                PlayerPrefs.SetInt(KTutorialAutoLaunched, 1);

            PlayerPrefs.Save();
            return true;
        }

        public static void MarkTutorialCompleted()
        {
            Shikaku.SaveSystem.SaveManager.MarkTutorialCompleted();
        }

        public static void EndTutorialSession()
        {
            IsTutorial = false;
            TutorialReturnToSettings = false;
            PlayerPrefs.Save();
        }

        public static string TimeTrialBestSquaresKey(int size)
        {
            return $"timetrial_best_squares_{size}x{size}";
        }

        public static int GetTimeTrialBestSquares(int size)
        {
            return Shikaku.SaveSystem.SaveManager
                .GetTimeTrialBestSquares(size);
        }

        public static void SaveTimeTrialBestSquaresIfHigher(int size, int score)
        {
            if (size <= 0) return;

            Shikaku.SaveSystem.SaveManager
                .SaveTimeTrialBestSquaresIfHigher(size, score);
        }

        private const int TimeTrialRecentPuzzleLimit = 200;

        private static string TimeTrialHistoryKey(int size)
        {
            return $"timetrial_recent_puzzles_{size}x{size}";
        }

        /// <summary>
        /// Returns the recently played puzzle IDs for this board size.
        /// The newest puzzle is the last item in the list.
        /// </summary>
        public static List<string> GetRecentTimeTrialPuzzles(int size)
        {
            var recent = new List<string>();

            if (size <= 0)
                return recent;

            string saved = PlayerPrefs.GetString(TimeTrialHistoryKey(size), "");

            if (string.IsNullOrEmpty(saved))
                return recent;

            string[] puzzleIds = saved.Split('\n');

            for (int i = 0; i < puzzleIds.Length; i++)
            {
                string id = puzzleIds[i];

                if (!string.IsNullOrEmpty(id))
                    recent.Add(id);
            }

            return recent;
        }

        /// <summary>
        /// Selects a random puzzle that is not among the 200 most recently played
        /// puzzles for this board size, then immediately adds it to the history.
        /// </summary>
        public static string PickTimeTrialPuzzleAvoidingRecent(
            int size,
            IReadOnlyList<string> allPuzzleIds)
        {
            if (size <= 0 || allPuzzleIds == null || allPuzzleIds.Count == 0)
                return null;

            List<string> recent = GetRecentTimeTrialPuzzles(size);
            var recentSet = new HashSet<string>(recent);
            var available = new List<string>();

            for (int i = 0; i < allPuzzleIds.Count; i++)
            {
                string puzzleId = allPuzzleIds[i];

                if (string.IsNullOrEmpty(puzzleId))
                    continue;

                if (!recentSet.Contains(puzzleId))
                    available.Add(puzzleId);
            }

            string chosenPuzzleId;

            if (available.Count > 0)
            {
                chosenPuzzleId = available[UnityEngine.Random.Range(0, available.Count)];
            }
            else
            {
                // This can only happen when every available puzzle is already
                // contained in the recent-history list. A repeat is unavoidable.
                //
                // Prefer the oldest puzzle instead of choosing randomly.
                chosenPuzzleId = recent.Count > 0
                    ? recent[0]
                    : allPuzzleIds[UnityEngine.Random.Range(0, allPuzzleIds.Count)];
            }

            RememberTimeTrialPuzzle(size, chosenPuzzleId);
            return chosenPuzzleId;
        }

        /// <summary>
        /// Adds a puzzle to the persistent Time Trial history for this board size.
        /// </summary>
        private static void RememberTimeTrialPuzzle(int size, string puzzleId)
        {
            if (size <= 0 || string.IsNullOrEmpty(puzzleId))
                return;

            List<string> recent = GetRecentTimeTrialPuzzles(size);

            // Remove an older occurrence before moving this puzzle to the newest slot.
            recent.RemoveAll(id => id == puzzleId);
            recent.Add(puzzleId);

            while (recent.Count > TimeTrialRecentPuzzleLimit)
                recent.RemoveAt(0);

            PlayerPrefs.SetString(
                TimeTrialHistoryKey(size),
                string.Join("\n", recent));

            PlayerPrefs.Save();
        }

        public static string DailyKey
        {
            get => PlayerPrefs.GetString(KDailyKey, "");
            set => PlayerPrefs.SetString(KDailyKey, value ?? "");
        }

        /// <summary>
        /// Selected pack path for menus that use packs (FreePlay/TimeTrial/Daily).
        /// Example: "FreePlay/freeplay_3x3_pack01" (relative to Resources/Puzzles)
        /// </summary>
        public static string PackPath
        {
            get => PlayerPrefs.GetString(KPackPath, "");
            set => PlayerPrefs.SetString(KPackPath, value ?? "");
        }

        public static void SetPuzzle(string puzzleId)
        {
            PlayerPrefs.SetString(
                "puzzleId",
                PuzzleProgressStore.NormalizePuzzleId(puzzleId));
        }

        public static string GetPuzzleId()
        {
            return PuzzleProgressStore.NormalizePuzzleId(
                PlayerPrefs.GetString("puzzleId", ""));
        }

        public static string DateKey(DateTime date)
        {
            return date.ToString("yyyyMMdd");
        }
    }
}