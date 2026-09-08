using System;
using System.Collections.Generic;
using System.Globalization;
using Shikaku.Logic;
using Shikaku.Menu;
using UnityEngine;

namespace Shikaku.SaveSystem
{
    /// <summary>
    /// One-time bridge from the PlayerPrefs layout used before SaveManager.
    /// The old keys are intentionally left in place for release rollback safety.
    /// </summary>
    internal static class LegacySaveImporter
    {
        private static readonly string[] AchievementIds =
        {
            "adventurebegins",
            "seasonedexplorer",
            "freethinker",
            "packitup",
            "triplethreat",
            "perfectweek",
            "againsttheclock",
            "clockwork",
            "centuryclub",
            "shikakumaster"
        };

        public static void ImportInto(SaveData data)
        {
            if (data == null)
                return;

            var puzzleIds = new HashSet<string>(StringComparer.Ordinal);
            var dailyKeys = new HashSet<string>(StringComparer.Ordinal);
            var sizes = new HashSet<int>();

            CollectPuzzleIds(puzzleIds, dailyKeys, sizes);
            ImportPuzzleRecords(data, puzzleIds);
            ImportPackProgress(data, sizes);
            ImportDailyCompletions(data, dailyKeys);
            ImportTimeTrialScores(data, sizes);
            ImportEconomy(data, dailyKeys);
            ImportStreak(data);
            ImportAchievements(data, puzzleIds);

            data.storyCurrentPack = PlayerPrefs.GetString(
                "story_current_pack",
                string.Empty);
            data.tutorialCompleted =
                PlayerPrefs.GetInt("tutorial_completed_v1", 0) == 1;
        }

        private static void CollectPuzzleIds(
            HashSet<string> puzzleIds,
            HashSet<string> dailyKeys,
            HashSet<int> sizes)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>("Puzzles");
            for (int assetIndex = 0;
                 assetIndex < assets.Length;
                 assetIndex++)
            {
                TextAsset asset = assets[assetIndex];
                if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                    continue;

                try
                {
                    if (asset.text.IndexOf(
                            "\"puzzles\"",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        PuzzlePackData pack =
                            JsonUtility.FromJson<PuzzlePackData>(asset.text);
                        if (pack?.puzzles == null)
                            continue;

                        if (pack.width > 0 && pack.width == pack.height)
                            sizes.Add(pack.width);

                        for (int i = 0; i < pack.puzzles.Length; i++)
                        {
                            PuzzleEntry entry = pack.puzzles[i];
                            if (entry == null)
                                continue;

                            AddPuzzleId(
                                puzzleIds,
                                dailyKeys,
                                entry.id);

                            if (entry.width > 0 &&
                                entry.width == entry.height)
                            {
                                sizes.Add(entry.width);
                            }
                        }
                    }
                    else
                    {
                        PuzzleData puzzle =
                            JsonUtility.FromJson<PuzzleData>(asset.text);
                        if (puzzle == null)
                            continue;

                        AddPuzzleId(puzzleIds, dailyKeys, puzzle.id);
                        if (puzzle.width > 0 &&
                            puzzle.width == puzzle.height)
                        {
                            sizes.Add(puzzle.width);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"SaveManager skipped legacy scan for '{asset.name}': " +
                        exception.Message);
                }
            }

            // Current Time Trial modes. Keeping these here also migrates scores
            // when a matching Resources pack is temporarily absent.
            sizes.Add(3);
            sizes.Add(4);
            sizes.Add(5);
            sizes.Add(6);
        }

        private static void AddPuzzleId(
            HashSet<string> puzzleIds,
            HashSet<string> dailyKeys,
            string rawId)
        {
            string id = NormalizePuzzleId(rawId);
            if (string.IsNullOrEmpty(id))
                return;

            puzzleIds.Add(id);

            if (TryParseDailyId(
                    id,
                    out DateTime date,
                    out string difficulty))
            {
                dailyKeys.Add(
                    date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                    "|" + difficulty);
            }
        }

        private static void ImportPuzzleRecords(
            SaveData data,
            HashSet<string> puzzleIds)
        {
            foreach (string puzzleId in puzzleIds)
            {
                bool completed =
                    PlayerPrefs.GetInt("completed_" + puzzleId, 0) == 1;
                string bestTimeKey = "best_time_" + puzzleId;
                bool hasBestTime = PlayerPrefs.HasKey(bestTimeKey);

                if (!completed && !hasBestTime)
                    continue;

                data.puzzles.Add(new PuzzleSaveRecord
                {
                    puzzleId = puzzleId,
                    completed = completed,
                    bestTimeSeconds = hasBestTime
                        ? Mathf.Max(0f, PlayerPrefs.GetFloat(bestTimeKey, 0f))
                        : -1f
                });
            }
        }

        private static void ImportPackProgress(
            SaveData data,
            HashSet<int> sizes)
        {
            foreach (int size in sizes)
            {
                string key = $"unlocked_freeplay_{size}";
                if (!PlayerPrefs.HasKey(key))
                    continue;

                data.sizeProgress.Add(new SizeProgressRecord
                {
                    size = size,
                    unlockedLevel = Mathf.Max(
                        1,
                        PlayerPrefs.GetInt(key, 1))
                });
            }

            var packPaths = new HashSet<string>(StringComparer.Ordinal);
            AddPackPaths(packPaths, PuzzleCatalog.FreePlayPacks);
            AddPackPaths(packPaths, PuzzleCatalog.StoryPacks);

            string currentStoryPack = PlayerPrefs.GetString(
                "story_current_pack",
                string.Empty);
            if (!string.IsNullOrEmpty(currentStoryPack))
                packPaths.Add(currentStoryPack);

            foreach (string packPath in packPaths)
            {
                string unlockedKey = "unlocked_pack_" + packPath;
                string lastPlayedKey = "last_played_pack_" + packPath;
                if (!PlayerPrefs.HasKey(unlockedKey) &&
                    !PlayerPrefs.HasKey(lastPlayedKey))
                {
                    continue;
                }

                data.packProgress.Add(new PackProgressRecord
                {
                    packPath = packPath,
                    unlockedLevel = Mathf.Max(
                        1,
                        PlayerPrefs.GetInt(unlockedKey, 1)),
                    lastPlayedLevel = Mathf.Max(
                        1,
                        PlayerPrefs.GetInt(lastPlayedKey, 1))
                });
            }
        }

        private static void AddPackPaths(
            HashSet<string> paths,
            IReadOnlyList<PuzzleCatalog.PackInfo> packs)
        {
            if (packs == null)
                return;

            for (int i = 0; i < packs.Count; i++)
            {
                if (!string.IsNullOrEmpty(packs[i].PackPath))
                    paths.Add(packs[i].PackPath);
            }
        }

        private static void ImportDailyCompletions(
            SaveData data,
            HashSet<string> dailyKeys)
        {
            foreach (string composite in dailyKeys)
            {
                string[] parts = composite.Split('|');
                if (parts.Length != 2)
                    continue;

                string legacyKey =
                    $"DailyPuzzle_{parts[0]}_{parts[1]}";
                if (PlayerPrefs.GetInt(legacyKey, 0) != 1)
                    continue;

                data.dailyCompletions.Add(new DailyCompletionRecord
                {
                    date = parts[0],
                    difficulty = parts[1]
                });
            }
        }

        private static void ImportTimeTrialScores(
            SaveData data,
            HashSet<int> sizes)
        {
            foreach (int size in sizes)
            {
                int score = PlayerPrefs.GetInt(
                    $"timetrial_best_squares_{size}x{size}",
                    0);
                if (score <= 0)
                    continue;

                data.timeTrialScores.Add(new TimeTrialScoreRecord
                {
                    size = size,
                    bestSquares = score
                });
            }
        }

        private static void ImportEconomy(
            SaveData data,
            HashSet<string> dailyKeys)
        {
            data.economy.hintBalance = Mathf.Max(
                0,
                PlayerPrefs.GetInt("purchased_hint_balance", 0));

            IReadOnlyList<PuzzleCatalog.PackInfo> storyPacks =
                PuzzleCatalog.StoryPacks;
            for (int i = 0; i < storyPacks.Count; i++)
            {
                ImportClaimIfPresent(
                    data,
                    "completion_hint_reward_story_v1_" +
                    storyPacks[i].PackPath);
            }

            IReadOnlyList<PuzzleCatalog.FreePlayCollectionInfo> collections =
                PuzzleCatalog.FreePlayCollections;
            for (int i = 0; i < collections.Count; i++)
            {
                ImportClaimIfPresent(
                    data,
                    "completion_hint_reward_freeplay_v1_" +
                    collections[i].PackId);
            }

            foreach (string composite in dailyKeys)
            {
                string[] parts = composite.Split('|');
                if (parts.Length != 2 ||
                    !DateTime.TryParseExact(
                        parts[0],
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime date))
                {
                    continue;
                }

                ImportClaimIfPresent(
                    data,
                    "completion_hint_reward_daily_v1_" +
                    date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            }
        }

        private static void ImportClaimIfPresent(
            SaveData data,
            string claimId)
        {
            if (PlayerPrefs.GetInt(claimId, 0) == 1 &&
                !data.economy.claimedRewardIds.Contains(claimId))
            {
                data.economy.claimedRewardIds.Add(claimId);
            }
        }

        private static void ImportStreak(SaveData data)
        {
            data.streak.count = Mathf.Max(
                0,
                PlayerPrefs.GetInt("daily_play_streak_count", 0));
            data.streak.lastCompletionDate = Mathf.Max(
                0,
                PlayerPrefs.GetInt("daily_play_streak_last_date", 0));
        }

        private static void ImportAchievements(
            SaveData data,
            HashSet<string> puzzleIds)
        {
            data.achievements.lifetimePuzzleCount = Mathf.Max(
                0,
                PlayerPrefs.GetInt("achievement_lifetime_puzzles_v1", 0));
            data.achievements.highestTimeTrialScore = Mathf.Max(
                0,
                PlayerPrefs.GetInt(
                    "achievement_time_trial_high_score_v1",
                    0));
            data.achievements.timeTrialModeMask = PlayerPrefs.GetInt(
                "achievement_time_trial_modes_v1",
                0);
            data.achievements.tripleThreatObserved =
                PlayerPrefs.GetInt("achievement_triple_threat_v1", 0) == 1;

            int generation = Mathf.Max(
                0,
                PlayerPrefs.GetInt(
                    "achievement_lifetime_count_generation_v1",
                    0));
            string countedPrefix =
                "achievement_lifetime_counted_puzzle_v1_" + generation + "_";

            foreach (string puzzleId in puzzleIds)
            {
                if (PlayerPrefs.GetInt(countedPrefix + puzzleId, 0) == 1)
                    data.achievements.countedPuzzleIds.Add(puzzleId);
            }

            for (int i = 0; i < AchievementIds.Length; i++)
            {
                string id = AchievementIds[i];
                if (PlayerPrefs.GetInt(
                        "achievement_unlocked_v1_" + id,
                        0) == 1)
                {
                    data.achievements.unlockedIds.Add(id);
                }
            }
        }

        private static bool TryParseDailyId(
            string puzzleId,
            out DateTime date,
            out string difficulty)
        {
            date = default;
            difficulty = string.Empty;

            if (string.IsNullOrEmpty(puzzleId) ||
                !puzzleId.StartsWith("Daily_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = puzzleId.Split('_');
            if (parts.Length < 5 ||
                !int.TryParse(parts[1], out int year) ||
                !int.TryParse(parts[2], out int month) ||
                !int.TryParse(parts[3], out int day))
            {
                return false;
            }

            try
            {
                date = new DateTime(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            string rawDifficulty = parts[4];
            if (rawDifficulty.Equals("easy", StringComparison.OrdinalIgnoreCase))
                difficulty = "Easy";
            else if (rawDifficulty.Equals("medium", StringComparison.OrdinalIgnoreCase))
                difficulty = "Medium";
            else if (rawDifficulty.Equals("hard", StringComparison.OrdinalIgnoreCase))
                difficulty = "Hard";
            else
                return false;

            return true;
        }

        private static string NormalizePuzzleId(string puzzleId)
        {
            if (string.IsNullOrWhiteSpace(puzzleId))
                return string.Empty;

            string normalized = puzzleId.Trim();
            int pipe = normalized.LastIndexOf('|');
            if (pipe >= 0 && pipe < normalized.Length - 1)
                normalized = normalized.Substring(pipe + 1);

            return normalized;
        }
    }
}
