using UnityEngine;
using System;
using System.Collections.Generic;
using Shikaku.SaveSystem;

namespace Shikaku.Menu
{
    /// <summary>
    /// Persistent progression for Free Play, Adventure, and Daily puzzles.
    /// Adventure adds a chapter gate on top of generic pack progression so the
    /// default first level of an unreached pack does not unlock every chapter.
    /// </summary>
    public static class Progression
    {
        private static string KeyUnlocked(int size) => $"unlocked_freeplay_{size}";
        private static string KeyUnlockedPack(string packPath) => $"unlocked_pack_{packPath}";

        public static int GetUnlockedLevel(int size)
        {
            return SaveManager.GetUnlockedLevel(size);
        }

        public static void SetUnlockedLevel(int size, int unlockedLevel)
        {
            SaveManager.SetUnlockedLevel(size, unlockedLevel);
        }

        public static void MarkSolvedFreePlay(int size, int levelIndex)
        {
            int currentUnlocked = GetUnlockedLevel(size);
            if (levelIndex >= currentUnlocked)
                SetUnlockedLevel(size, levelIndex + 1);
        }

        // ---------------- Pack-based progression ----------------

        public static int GetUnlockedLevelForPack(string packPath)
        {
            if (string.IsNullOrEmpty(packPath))
                return 1;

            return SaveManager.GetUnlockedLevelForPack(packPath);
        }

        public static void SetUnlockedLevelForPack(
            string packPath,
            int unlockedLevel)
        {
            if (string.IsNullOrEmpty(packPath))
                return;

            SaveManager.SetUnlockedLevelForPack(packPath, unlockedLevel);
        }

        public static void MarkSolvedFreePlayPack(
            string packPath,
            int levelIndex)
        {
            int currentUnlocked = GetUnlockedLevelForPack(packPath);
            if (levelIndex >= currentUnlocked)
                SetUnlockedLevelForPack(packPath, levelIndex + 1);
        }

        // ---------------- Story / Adventure progression ----------------

        private static string KeyStoryCurrentPack() => "story_current_pack";
        private static string KeyLastPlayedPack(string packPath) =>
            $"last_played_pack_{packPath}";

        public static string GetStoryCurrentPack()
        {
            return SaveManager.GetStoryCurrentPack();
        }

        public static void SetStoryCurrentPack(string packPath)
        {
            SaveManager.SetStoryCurrentPack(packPath);
        }

        /// <summary>
        /// Advances the current Story pack without allowing a replay of an older
        /// chapter to move the player's main Continue position backward.
        /// </summary>
        public static void SetStoryCurrentPackIfLater(string packPath)
        {
            if (string.IsNullOrEmpty(packPath))
                return;

            IReadOnlyList<PuzzleCatalog.PackInfo> packs =
                PuzzleCatalog.StoryPacks;

            int requestedIndex = FindStoryPackIndex(packs, packPath);
            if (requestedIndex < 0)
                return;

            string currentPath = GetStoryCurrentPack();
            int currentIndex = FindStoryPackIndex(packs, currentPath);

            if (currentIndex < 0 || requestedIndex > currentIndex)
                SetStoryCurrentPack(packPath);
        }

        public static int GetLastPlayedLevelForPack(string packPath)
        {
            if (string.IsNullOrEmpty(packPath))
                return 1;

            return SaveManager.GetLastPlayedLevelForPack(packPath);
        }

        public static void SetLastPlayedLevelForPack(
            string packPath,
            int levelIndex)
        {
            if (string.IsNullOrEmpty(packPath))
                return;

            SaveManager.SetLastPlayedLevelForPack(packPath, levelIndex);
        }

        /// <summary>
        /// Returns the highest Story chapter index the player has genuinely
        /// reached. Existing saves are recognized through their current pack,
        /// saved pack progress, and completed-puzzle keys.
        /// </summary>
        public static int GetHighestUnlockedStoryChapterIndex()
        {
            IReadOnlyList<PuzzleCatalog.PackInfo> packs =
                PuzzleCatalog.StoryPacks;

            if (packs == null || packs.Count == 0)
                return -1;

            int highest = 0;
            int currentIndex =
                FindStoryPackIndex(packs, GetStoryCurrentPack());

            if (currentIndex >= 0)
                highest = currentIndex;

            for (int i = 0; i < packs.Count; i++)
            {
                string packPath = packs[i].PackPath;
                IReadOnlyList<string> ids =
                    PuzzleCatalog.GetPackPuzzleIds(packPath);

                bool anyCompleted = false;
                bool allCompleted = ids.Count > 0;

                for (int level = 0; level < ids.Count; level++)
                {
                    bool completed = IsPuzzleCompleted(ids[level]);
                    anyCompleted |= completed;
                    allCompleted &= completed;
                }

                // A value above one is evidence that this pack was played.
                if (GetUnlockedLevelForPack(packPath) > 1 || anyCompleted)
                    highest = Mathf.Max(highest, i);

                if (allCompleted && i + 1 < packs.Count)
                    highest = Mathf.Max(highest, i + 1);
            }

            return Mathf.Clamp(highest, 0, packs.Count - 1);
        }

        public static bool IsStoryChapterUnlocked(string packPath)
        {
            IReadOnlyList<PuzzleCatalog.PackInfo> packs =
                PuzzleCatalog.StoryPacks;

            int packIndex = FindStoryPackIndex(packs, packPath);
            if (packIndex < 0)
                return false;

            return packIndex <= GetHighestUnlockedStoryChapterIndex();
        }

        /// <summary>
        /// Returns the first unfinished level in an unlocked chapter, using a
        /// 1-based level number. Returns -1 when the chapter is locked or fully
        /// completed.
        /// </summary>
        public static int GetNextStoryLevel(string packPath)
        {
            if (!IsStoryChapterUnlocked(packPath))
                return -1;

            IReadOnlyList<string> ids =
                PuzzleCatalog.GetPackPuzzleIds(packPath);

            for (int i = 0; i < ids.Count; i++)
            {
                if (!IsPuzzleCompleted(ids[i]))
                    return i + 1;
            }

            return -1;
        }

        /// <summary>
        /// Finds the puzzle used by the Home screen's direct Adventure button.
        /// </summary>
        public static bool TryGetStoryContinue(
            out string packPath,
            out int levelIndex)
        {
            packPath = null;
            levelIndex = 0;

            IReadOnlyList<PuzzleCatalog.PackInfo> packs =
                PuzzleCatalog.StoryPacks;

            if (packs == null || packs.Count == 0)
                return false;

            int highest = GetHighestUnlockedStoryChapterIndex();
            if (highest < 0)
                return false;

            int currentIndex =
                FindStoryPackIndex(packs, GetStoryCurrentPack());

            if (currentIndex >= 0 && currentIndex <= highest)
            {
                int currentNext =
                    GetNextStoryLevel(packs[currentIndex].PackPath);

                if (currentNext > 0)
                {
                    packPath = packs[currentIndex].PackPath;
                    levelIndex = currentNext;
                    return true;
                }
            }

            // Prefer the furthest available unfinished chapter.
            for (int i = highest; i >= 0; i--)
            {
                int next = GetNextStoryLevel(packs[i].PackPath);
                if (next <= 0)
                    continue;

                packPath = packs[i].PackPath;
                levelIndex = next;
                return true;
            }

            // Entire available Adventure is complete: replay its final puzzle.
            IReadOnlyList<string> finalIds =
                PuzzleCatalog.GetPackPuzzleIds(packs[highest].PackPath);

            if (finalIds.Count == 0)
                return false;

            packPath = packs[highest].PackPath;
            levelIndex = finalIds.Count;
            return true;
        }

        public static void MarkSolvedStoryPack(
            string packPath,
            int levelIndex)
        {
            if (string.IsNullOrEmpty(packPath))
                return;

            int currentUnlocked = GetUnlockedLevelForPack(packPath);
            if (levelIndex >= currentUnlocked)
                SetUnlockedLevelForPack(packPath, levelIndex + 1);

            SetLastPlayedLevelForPack(packPath, levelIndex + 1);
            SetStoryCurrentPackIfLater(packPath);

            IReadOnlyList<PuzzleCatalog.PackInfo> packs =
                PuzzleCatalog.StoryPacks;
            int packIndex = FindStoryPackIndex(packs, packPath);
            IReadOnlyList<string> ids =
                PuzzleCatalog.GetPackPuzzleIds(packPath);

            // Unlock the next chapter as soon as the final puzzle is solved.
            // This works even when the player exits from the solved panel.
            if (packIndex >= 0 &&
                levelIndex >= ids.Count &&
                packIndex + 1 < packs.Count)
            {
                string nextPackPath = packs[packIndex + 1].PackPath;
                SetUnlockedLevelForPack(nextPackPath, 1);

                if (GetLastPlayedLevelForPack(nextPackPath) <= 1)
                    SetLastPlayedLevelForPack(nextPackPath, 1);

                SetStoryCurrentPackIfLater(nextPackPath);
            }
        }

        private static int FindStoryPackIndex(
            IReadOnlyList<PuzzleCatalog.PackInfo> packs,
            string packPath)
        {
            if (packs == null || string.IsNullOrEmpty(packPath))
                return -1;

            for (int i = 0; i < packs.Count; i++)
            {
                if (string.Equals(
                    packs[i].PackPath,
                    packPath,
                    StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsPuzzleCompleted(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId))
                return false;

            return PuzzleProgressStore.IsCompleted(puzzleId);
        }

        // ---------------- Daily progression ----------------

        private const string DailyPrefix = "DailyPuzzle";

        private static string KeyDailyPuzzle(
            DateTime date,
            string difficulty)
        {
            return $"{DailyPrefix}_{date:yyyy-MM-dd}_{difficulty}";
        }

        public static bool IsDailyCompleted(
            DateTime date,
            string difficulty)
        {
            return SaveManager.IsDailyCompleted(date, difficulty);
        }

        public static void SetDailyCompleted(
            DateTime date,
            string difficulty,
            bool completed = true)
        {
            SaveManager.SetDailyCompleted(date, difficulty, completed);
        }

        public static bool IsDailyDateFullyCompleted(DateTime date)
        {
            return IsDailyCompleted(date, "Easy")
                && IsDailyCompleted(date, "Medium")
                && IsDailyCompleted(date, "Hard");
        }
    }
}