using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Shikaku.SaveSystem
{
    /// <summary>
    /// Owns durable, cloud-eligible player data. Unfinished puzzle boards and
    /// device-specific preferences deliberately do not belong in this file.
    /// </summary>
    public static class SaveManager
    {
        public const int CurrentSchemaVersion = 1;

        private const string SaveFileName = "shikaku_save.json";
        private const string MigrationMarker = "save_manager_migrated_v1";
        private static readonly object Sync = new object();

        private static SaveData _data;
        private static bool _initialized;
        private static bool _applicationHookRegistered;
        private static bool _dirty;
        private static int _batchDepth;
#if UNITY_EDITOR
        private static string _savePathOverride;
        private static bool _skipLegacyImportForTests;
#endif

        public static string SavePath =>
#if UNITY_EDITOR
            !string.IsNullOrEmpty(_savePathOverride)
                ? _savePathOverride
                :
#endif
            Path.Combine(Application.persistentDataPath, SaveFileName);

        public static string BackupPath => SavePath + ".bak";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        public static IDisposable BeginBatch()
        {
            EnsureInitialized();
            lock (Sync)
            {
                _batchDepth++;
            }

            return new SaveBatch();
        }

        public static void Flush()
        {
            EnsureInitialized();
            lock (Sync)
            {
                if (_dirty)
                    WriteNow();
            }
        }

        public static bool IsPuzzleCompleted(string puzzleId)
        {
            string id = NormalizePuzzleId(puzzleId);
            if (string.IsNullOrEmpty(id))
                return false;

            EnsureInitialized();
            lock (Sync)
            {
                PuzzleSaveRecord record = FindPuzzle(id);
                return record != null && record.completed;
            }
        }

        public static void MarkPuzzleCompleted(string puzzleId)
        {
            string id = NormalizePuzzleId(puzzleId);
            if (string.IsNullOrEmpty(id))
                return;

            EnsureInitialized();
            lock (Sync)
            {
                PuzzleSaveRecord record = GetOrCreatePuzzle(id);
                if (record.completed)
                    return;

                record.completed = true;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static bool TryGetBestTime(
    string puzzleId,
    out float seconds)
        {
            return TryGetBestTime(
                puzzleId,
                out seconds,
                out _);
        }

        public static bool TryGetBestTime(
            string puzzleId,
            out float seconds,
            out bool usedHint)
        {
            seconds = 0f;
            usedHint = false;

            string id = NormalizePuzzleId(puzzleId);
            if (string.IsNullOrEmpty(id))
                return false;

            EnsureInitialized();

            lock (Sync)
            {
                PuzzleSaveRecord record = FindPuzzle(id);

                if (record == null ||
                    record.bestTimeSeconds < 0f)
                {
                    return false;
                }

                seconds = Mathf.Max(
                    0f,
                    record.bestTimeSeconds);

                usedHint = record.bestTimeUsedHint;

                return true;
            }
        }

        public static bool SaveBestTimeIfBetter(
            string puzzleId,
            float seconds)
        {
            // Preserve old callers by treating them as unassisted.
            return SaveBestTimeIfBetter(
                puzzleId,
                seconds,
                false);
        }

        public static bool SaveBestTimeIfBetter(
            string puzzleId,
            float seconds,
            bool usedHint)
        {
            string id = NormalizePuzzleId(puzzleId);

            if (string.IsNullOrEmpty(id) ||
                seconds < 0f ||
                float.IsNaN(seconds) ||
                float.IsInfinity(seconds))
            {
                return false;
            }

            EnsureInitialized();

            lock (Sync)
            {
                PuzzleSaveRecord record =
                    GetOrCreatePuzzle(id);

                bool hasExistingBest =
                    record.bestTimeSeconds >= 0f;

                if (hasExistingBest)
                {
                    bool existingUsedHint =
                        record.bestTimeUsedHint;

                    // An assisted solve can NEVER replace
                    // an existing unassisted best.
                    if (!existingUsedHint && usedHint)
                        return false;

                    // If both runs have the same hint status,
                    // only save the faster time.
                    if (existingUsedHint == usedHint &&
                        seconds >= record.bestTimeSeconds)
                    {
                        return false;
                    }

                    // If the existing best used a hint and this
                    // new solve did not, allow the unassisted
                    // solve to replace it regardless of time.
                }

                record.bestTimeSeconds = seconds;
                record.bestTimeUsedHint = usedHint;

                MarkDirtyAndSaveIfNeeded();

                return true;
            }
        }

        public static int GetUnlockedLevel(int size)
        {
            if (size <= 0)
                return 1;

            EnsureInitialized();
            lock (Sync)
            {
                SizeProgressRecord record = FindSizeProgress(size);
                return record == null ? 1 : Mathf.Max(1, record.unlockedLevel);
            }
        }

        public static void SetUnlockedLevel(int size, int unlockedLevel)
        {
            if (size <= 0)
                return;

            EnsureInitialized();
            lock (Sync)
            {
                int value = Mathf.Max(1, unlockedLevel);
                SizeProgressRecord record = GetOrCreateSizeProgress(size);
                if (record.unlockedLevel == value)
                    return;

                record.unlockedLevel = value;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static int GetUnlockedLevelForPack(string packPath)
        {
            if (string.IsNullOrEmpty(packPath))
                return 1;

            EnsureInitialized();
            lock (Sync)
            {
                PackProgressRecord record = FindPackProgress(packPath);
                return record == null ? 1 : Mathf.Max(1, record.unlockedLevel);
            }
        }

        public static void SetUnlockedLevelForPack(
            string packPath,
            int unlockedLevel)
        {
            if (string.IsNullOrEmpty(packPath))
                return;

            EnsureInitialized();
            lock (Sync)
            {
                int value = Mathf.Max(1, unlockedLevel);
                PackProgressRecord record = GetOrCreatePackProgress(packPath);
                if (record.unlockedLevel == value)
                    return;

                record.unlockedLevel = value;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static string GetStoryCurrentPack()
        {
            EnsureInitialized();
            lock (Sync)
            {
                return _data.storyCurrentPack ?? string.Empty;
            }
        }

        public static void SetStoryCurrentPack(string packPath)
        {
            string value = packPath ?? string.Empty;
            EnsureInitialized();
            lock (Sync)
            {
                if (string.Equals(
                        _data.storyCurrentPack,
                        value,
                        StringComparison.Ordinal))
                {
                    return;
                }

                _data.storyCurrentPack = value;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static int GetLastPlayedLevelForPack(string packPath)
        {
            if (string.IsNullOrEmpty(packPath))
                return 1;

            EnsureInitialized();
            lock (Sync)
            {
                PackProgressRecord record = FindPackProgress(packPath);
                return record == null ? 1 : Mathf.Max(1, record.lastPlayedLevel);
            }
        }

        public static void SetLastPlayedLevelForPack(
            string packPath,
            int levelIndex)
        {
            if (string.IsNullOrEmpty(packPath))
                return;

            EnsureInitialized();
            lock (Sync)
            {
                int value = Mathf.Max(1, levelIndex);
                PackProgressRecord record = GetOrCreatePackProgress(packPath);
                if (record.lastPlayedLevel == value)
                    return;

                record.lastPlayedLevel = value;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static bool IsDailyCompleted(DateTime date, string difficulty)
        {
            string normalizedDifficulty = NormalizeDifficulty(difficulty);
            if (string.IsNullOrEmpty(normalizedDifficulty))
                return false;

            string dateKey = date.Date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            EnsureInitialized();
            lock (Sync)
            {
                return FindDailyCompletion(dateKey, normalizedDifficulty) != null;
            }
        }

        public static void SetDailyCompleted(
            DateTime date,
            string difficulty,
            bool completed)
        {
            string normalizedDifficulty = NormalizeDifficulty(difficulty);
            if (string.IsNullOrEmpty(normalizedDifficulty))
                return;

            string dateKey = date.Date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            EnsureInitialized();
            lock (Sync)
            {
                DailyCompletionRecord existing =
                    FindDailyCompletion(dateKey, normalizedDifficulty);

                if (completed)
                {
                    if (existing != null)
                        return;

                    _data.dailyCompletions.Add(new DailyCompletionRecord
                    {
                        date = dateKey,
                        difficulty = normalizedDifficulty
                    });
                }
                else
                {
                    if (existing == null)
                        return;

                    _data.dailyCompletions.Remove(existing);
                }

                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static int GetTimeTrialBestSquares(int size)
        {
            if (size <= 0)
                return 0;

            EnsureInitialized();
            lock (Sync)
            {
                TimeTrialScoreRecord record = FindTimeTrialScore(size);
                return record == null ? 0 : Mathf.Max(0, record.bestSquares);
            }
        }

        public static bool SaveTimeTrialBestSquaresIfHigher(int size, int score)
        {
            if (size <= 0 || score <= 0)
                return false;

            EnsureInitialized();
            lock (Sync)
            {
                TimeTrialScoreRecord record = GetOrCreateTimeTrialScore(size);
                if (score <= record.bestSquares)
                    return false;

                record.bestSquares = score;
                MarkDirtyAndSaveIfNeeded();
                return true;
            }
        }

        public static int HintBalance
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                {
                    return Mathf.Max(0, _data.economy.hintBalance);
                }
            }
        }

        public static void AddHints(int amount)
        {
            if (amount <= 0)
                return;

            EnsureInitialized();
            lock (Sync)
            {
                _data.economy.hintBalance =
                    Mathf.Max(0, _data.economy.hintBalance) + amount;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static bool TrySpendHint()
        {
            EnsureInitialized();
            lock (Sync)
            {
                if (_data.economy.hintBalance <= 0)
                    return false;

                _data.economy.hintBalance--;
                MarkDirtyAndSaveIfNeeded();
                return true;
            }
        }

        public static bool HasDeliveredHintPurchase(string deliveryId)
        {
            if (string.IsNullOrEmpty(deliveryId))
                return false;

            EnsureInitialized();
            lock (Sync)
            {
                return _data.economy.deliveredPurchaseIds.Contains(deliveryId);
            }
        }

        public static void RememberHintPurchaseDelivery(string deliveryId)
        {
            if (string.IsNullOrEmpty(deliveryId))
                return;

            EnsureInitialized();
            lock (Sync)
            {
                if (_data.economy.deliveredPurchaseIds.Contains(deliveryId))
                    return;

                _data.economy.deliveredPurchaseIds.Add(deliveryId);
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static bool TryDeliverPurchasedHints(
            string deliveryId,
            int amount)
        {
            if (string.IsNullOrEmpty(deliveryId) || amount <= 0)
                return false;

            EnsureInitialized();
            lock (Sync)
            {
                if (_data.economy.deliveredPurchaseIds.Contains(deliveryId))
                    return false;

                _data.economy.deliveredPurchaseIds.Add(deliveryId);
                _data.economy.hintBalance =
                    Mathf.Max(0, _data.economy.hintBalance) + amount;
                MarkDirtyAndSaveIfNeeded();
                return true;
            }
        }

        public static bool HasClaimedReward(string rewardId)
        {
            if (string.IsNullOrEmpty(rewardId))
                return false;

            EnsureInitialized();
            lock (Sync)
            {
                return _data.economy.claimedRewardIds.Contains(rewardId);
            }
        }

        public static bool TryClaimHintReward(string rewardId, int amount)
        {
            if (string.IsNullOrEmpty(rewardId) || amount <= 0)
                return false;

            EnsureInitialized();
            lock (Sync)
            {
                if (_data.economy.claimedRewardIds.Contains(rewardId))
                    return false;

                _data.economy.claimedRewardIds.Add(rewardId);
                _data.economy.hintBalance =
                    Mathf.Max(0, _data.economy.hintBalance) + amount;
                MarkDirtyAndSaveIfNeeded();
                return true;
            }
        }

        public static int DailyStreakCount
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return Mathf.Max(0, _data.streak.count);
            }
        }

        public static int DailyStreakLastCompletionDate
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return Mathf.Max(0, _data.streak.lastCompletionDate);
            }
        }

        public static int DailyStreakRestorationGrantedDate
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                {
                    return Mathf.Max(
                        0,
                        _data.streak.restorationGrantedDate);
                }
            }
        }

        public static void SetDailyStreak(int count, int lastCompletionDate)
        {
            EnsureInitialized();
            lock (Sync)
            {
                int normalizedCount = Mathf.Max(0, count);
                int normalizedDate = Mathf.Max(0, lastCompletionDate);
                if (_data.streak.count == normalizedCount &&
                    _data.streak.lastCompletionDate == normalizedDate &&
                    _data.streak.restorationGrantedDate == 0)
                {
                    return;
                }

                _data.streak.count = normalizedCount;
                _data.streak.lastCompletionDate = normalizedDate;
                _data.streak.restorationGrantedDate = 0;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static void SetDailyStreakRestorationGrantedDate(
            int restorationGrantedDate)
        {
            EnsureInitialized();
            lock (Sync)
            {
                int normalizedDate = Mathf.Max(
                    0,
                    restorationGrantedDate);
                if (_data.streak.restorationGrantedDate == normalizedDate)
                    return;

                _data.streak.restorationGrantedDate = normalizedDate;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static int LifetimePuzzleCount
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return Mathf.Max(0, _data.achievements.lifetimePuzzleCount);
            }
        }

        public static int RecordLifetimePuzzle(string puzzleId)
        {
            string id = NormalizePuzzleId(puzzleId);
            EnsureInitialized();
            lock (Sync)
            {
                if (string.IsNullOrEmpty(id) ||
                    _data.achievements.countedPuzzleIds.Contains(id))
                {
                    return Mathf.Max(
                        0,
                        _data.achievements.lifetimePuzzleCount);
                }

                _data.achievements.countedPuzzleIds.Add(id);
                _data.achievements.lifetimePuzzleCount =
                    Mathf.Max(0, _data.achievements.lifetimePuzzleCount) + 1;
                MarkDirtyAndSaveIfNeeded();
                return _data.achievements.lifetimePuzzleCount;
            }
        }

        public static int HighestAchievementTimeTrialScore
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return Mathf.Max(
                        0,
                        _data.achievements.highestTimeTrialScore);
            }
        }

        public static void SetHighestAchievementTimeTrialScore(int score)
        {
            score = Mathf.Max(0, score);
            EnsureInitialized();
            lock (Sync)
            {
                if (score <= _data.achievements.highestTimeTrialScore)
                    return;

                _data.achievements.highestTimeTrialScore = score;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static int AchievementTimeTrialModeMask
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return _data.achievements.timeTrialModeMask;
            }
        }

        public static void AddAchievementTimeTrialMode(int bit)
        {
            if (bit == 0)
                return;

            EnsureInitialized();
            lock (Sync)
            {
                int updated = _data.achievements.timeTrialModeMask | bit;
                if (updated == _data.achievements.timeTrialModeMask)
                    return;

                _data.achievements.timeTrialModeMask = updated;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static bool TripleThreatObserved
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return _data.achievements.tripleThreatObserved;
            }
        }

        public static void MarkTripleThreatObserved()
        {
            EnsureInitialized();
            lock (Sync)
            {
                if (_data.achievements.tripleThreatObserved)
                    return;

                _data.achievements.tripleThreatObserved = true;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static bool IsAchievementUnlocked(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId))
                return false;

            EnsureInitialized();
            lock (Sync)
                return _data.achievements.unlockedIds.Contains(achievementId);
        }

        public static bool UnlockAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId))
                return false;

            EnsureInitialized();
            lock (Sync)
            {
                if (_data.achievements.unlockedIds.Contains(achievementId))
                    return false;

                _data.achievements.unlockedIds.Add(achievementId);
                MarkDirtyAndSaveIfNeeded();
                return true;
            }
        }

        public static bool TutorialCompleted
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return _data.tutorialCompleted;
            }
        }

        public static void MarkTutorialCompleted()
        {
            EnsureInitialized();
            lock (Sync)
            {
                if (_data.tutorialCompleted)
                    return;

                _data.tutorialCompleted = true;
                MarkDirtyAndSaveIfNeeded();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ResetAchievementDataForTesting()
        {
            EnsureInitialized();
            lock (Sync)
            {
                _data.achievements = new AchievementSaveData();
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static void ResetHintBalanceForTesting()
        {
            EnsureInitialized();
            lock (Sync)
            {
                if (_data.economy.hintBalance == 0)
                    return;

                _data.economy.hintBalance = 0;
                MarkDirtyAndSaveIfNeeded();
            }
        }

        public static void ResetDailyStreakForTesting()
        {
            SetDailyStreak(0, 0);
        }
#endif

#if UNITY_EDITOR
        public static void LoadIsolatedSaveForTesting(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "A test save path is required.",
                    nameof(path));
            }

            lock (Sync)
            {
                if (_initialized && _dirty)
                    WriteNow();

                _data = null;
                _initialized = false;
                _dirty = false;
                _batchDepth = 0;
                _savePathOverride = path;
                _skipLegacyImportForTests = true;
            }

            EnsureInitialized();
        }

        public static void ClearIsolatedSaveForTesting()
        {
            lock (Sync)
            {
                if (_initialized && _dirty)
                    WriteNow();

                _data = null;
                _initialized = false;
                _dirty = false;
                _batchDepth = 0;
                _savePathOverride = null;
                _skipLegacyImportForTests = false;
            }
        }
#endif

        private static void EnsureInitialized()
        {
            lock (Sync)
            {
                if (_initialized)
                    return;

                RegisterApplicationHook();

                if (TryLoad(SavePath, out SaveData loaded))
                {
                    _data = loaded;
                    NormalizeLoadedData();
                    _initialized = true;
                    return;
                }

                if (TryLoad(BackupPath, out loaded))
                {
                    _data = loaded;
                    NormalizeLoadedData();
                    _initialized = true;

                    try
                    {
                        File.Copy(BackupPath, SavePath, true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "SaveManager recovered the backup but could not " +
                            "restore the primary save: " + exception.Message);
                    }

                    return;
                }

                _data = new SaveData();
                bool shouldImportLegacy =
                    PlayerPrefs.GetInt(MigrationMarker, 0) == 0;
#if UNITY_EDITOR
                shouldImportLegacy &= !_skipLegacyImportForTests;
#endif
                if (shouldImportLegacy)
                    LegacySaveImporter.ImportInto(_data);

                NormalizeLoadedData();
                _initialized = true;
                _dirty = true;

                if (WriteNow())
                {
                    PlayerPrefs.SetInt(MigrationMarker, 1);
                    PlayerPrefs.Save();
                }
            }
        }

        private static void RegisterApplicationHook()
        {
            if (_applicationHookRegistered)
                return;

            _applicationHookRegistered = true;
            Application.quitting += Flush;
        }

        private static bool TryLoad(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                SaveData parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null || parsed.schemaVersion <= 0 ||
                    parsed.schemaVersion > CurrentSchemaVersion)
                {
                    return false;
                }

                data = parsed;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"SaveManager could not read '{path}': {exception.Message}");
                return false;
            }
        }

        private static bool WriteNow()
        {
            string temporaryPath = SavePath + ".tmp";

            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                _data.schemaVersion = CurrentSchemaVersion;
                _data.generation++;
                _data.lastSavedAtUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture);

                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(
                    temporaryPath,
                    json,
                    new UTF8Encoding(false));

                if (File.Exists(SavePath))
                    File.Copy(SavePath, BackupPath, true);

                File.Copy(temporaryPath, SavePath, true);
                File.Delete(temporaryPath);
                _dirty = false;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "SaveManager could not write the local save: " +
                    exception.Message);
                _dirty = true;
                return false;
            }
        }

        private static void MarkDirtyAndSaveIfNeeded()
        {
            _dirty = true;
            if (_batchDepth == 0)
                WriteNow();
        }

        private static void EndBatch()
        {
            lock (Sync)
            {
                _batchDepth = Mathf.Max(0, _batchDepth - 1);
                if (_batchDepth == 0 && _dirty)
                    WriteNow();
            }
        }

        private static void NormalizeLoadedData()
        {
            _data.schemaVersion = CurrentSchemaVersion;
            _data.storyCurrentPack ??= string.Empty;
            _data.puzzles ??= new System.Collections.Generic.List<PuzzleSaveRecord>();
            _data.sizeProgress ??= new System.Collections.Generic.List<SizeProgressRecord>();
            _data.packProgress ??= new System.Collections.Generic.List<PackProgressRecord>();
            _data.dailyCompletions ??= new System.Collections.Generic.List<DailyCompletionRecord>();
            _data.timeTrialScores ??= new System.Collections.Generic.List<TimeTrialScoreRecord>();
            _data.economy ??= new EconomySaveData();
            _data.economy.claimedRewardIds ??= new System.Collections.Generic.List<string>();
            _data.economy.deliveredPurchaseIds ??= new System.Collections.Generic.List<string>();
            _data.economy.hintBalance = Mathf.Max(0, _data.economy.hintBalance);
            _data.streak ??= new StreakSaveData();
            _data.streak.count = Mathf.Max(0, _data.streak.count);
            _data.streak.lastCompletionDate = Mathf.Max(0, _data.streak.lastCompletionDate);
            _data.streak.restorationGrantedDate = Mathf.Max(
                0,
                _data.streak.restorationGrantedDate);
            _data.achievements ??= new AchievementSaveData();
            _data.achievements.countedPuzzleIds ??= new System.Collections.Generic.List<string>();
            _data.achievements.unlockedIds ??= new System.Collections.Generic.List<string>();
            _data.achievements.lifetimePuzzleCount = Mathf.Max(
                _data.achievements.countedPuzzleIds.Count,
                Mathf.Max(0, _data.achievements.lifetimePuzzleCount));
            _data.achievements.highestTimeTrialScore = Mathf.Max(
                0,
                _data.achievements.highestTimeTrialScore);
        }

        private static PuzzleSaveRecord FindPuzzle(string id)
        {
            for (int i = 0; i < _data.puzzles.Count; i++)
            {
                if (string.Equals(
                        _data.puzzles[i].puzzleId,
                        id,
                        StringComparison.Ordinal))
                {
                    return _data.puzzles[i];
                }
            }

            return null;
        }

        private static PuzzleSaveRecord GetOrCreatePuzzle(string id)
        {
            PuzzleSaveRecord record = FindPuzzle(id);
            if (record != null)
                return record;

            record = new PuzzleSaveRecord { puzzleId = id };
            _data.puzzles.Add(record);
            return record;
        }

        private static SizeProgressRecord FindSizeProgress(int size)
        {
            for (int i = 0; i < _data.sizeProgress.Count; i++)
            {
                if (_data.sizeProgress[i].size == size)
                    return _data.sizeProgress[i];
            }

            return null;
        }

        private static SizeProgressRecord GetOrCreateSizeProgress(int size)
        {
            SizeProgressRecord record = FindSizeProgress(size);
            if (record != null)
                return record;

            record = new SizeProgressRecord { size = size };
            _data.sizeProgress.Add(record);
            return record;
        }

        private static PackProgressRecord FindPackProgress(string packPath)
        {
            for (int i = 0; i < _data.packProgress.Count; i++)
            {
                if (string.Equals(
                        _data.packProgress[i].packPath,
                        packPath,
                        StringComparison.Ordinal))
                {
                    return _data.packProgress[i];
                }
            }

            return null;
        }

        private static PackProgressRecord GetOrCreatePackProgress(
            string packPath)
        {
            PackProgressRecord record = FindPackProgress(packPath);
            if (record != null)
                return record;

            record = new PackProgressRecord { packPath = packPath };
            _data.packProgress.Add(record);
            return record;
        }

        private static DailyCompletionRecord FindDailyCompletion(
            string date,
            string difficulty)
        {
            for (int i = 0; i < _data.dailyCompletions.Count; i++)
            {
                DailyCompletionRecord record = _data.dailyCompletions[i];
                if (string.Equals(record.date, date, StringComparison.Ordinal) &&
                    string.Equals(
                        record.difficulty,
                        difficulty,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        private static TimeTrialScoreRecord FindTimeTrialScore(int size)
        {
            for (int i = 0; i < _data.timeTrialScores.Count; i++)
            {
                if (_data.timeTrialScores[i].size == size)
                    return _data.timeTrialScores[i];
            }

            return null;
        }

        private static TimeTrialScoreRecord GetOrCreateTimeTrialScore(int size)
        {
            TimeTrialScoreRecord record = FindTimeTrialScore(size);
            if (record != null)
                return record;

            record = new TimeTrialScoreRecord { size = size };
            _data.timeTrialScores.Add(record);
            return record;
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

        private static string NormalizeDifficulty(string difficulty)
        {
            if (string.IsNullOrWhiteSpace(difficulty))
                return string.Empty;

            string value = difficulty.Trim();
            if (value.Equals("easy", StringComparison.OrdinalIgnoreCase))
                return "Easy";
            if (value.Equals("medium", StringComparison.OrdinalIgnoreCase))
                return "Medium";
            if (value.Equals("hard", StringComparison.OrdinalIgnoreCase))
                return "Hard";

            return value;
        }

        private sealed class SaveBatch : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                EndBatch();
            }
        }
    }
}
