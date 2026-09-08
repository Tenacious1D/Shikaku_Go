using System;
using System.Collections.Generic;

namespace Shikaku.SaveSystem
{
    [Serializable]
    public sealed class SaveData
    {
        public int schemaVersion = 1;
        public long generation;
        public string lastSavedAtUtc = string.Empty;
        public string storyCurrentPack = string.Empty;
        public bool tutorialCompleted;
        public List<PuzzleSaveRecord> puzzles = new List<PuzzleSaveRecord>();
        public List<SizeProgressRecord> sizeProgress = new List<SizeProgressRecord>();
        public List<PackProgressRecord> packProgress = new List<PackProgressRecord>();
        public List<DailyCompletionRecord> dailyCompletions = new List<DailyCompletionRecord>();
        public List<TimeTrialScoreRecord> timeTrialScores = new List<TimeTrialScoreRecord>();
        public EconomySaveData economy = new EconomySaveData();
        public StreakSaveData streak = new StreakSaveData();
        public AchievementSaveData achievements = new AchievementSaveData();
    }

    [Serializable]
    public sealed class PuzzleSaveRecord
    {
        public string puzzleId = string.Empty;
        public bool completed;
        public float bestTimeSeconds = -1f;
        public bool bestTimeUsedHint;
    }

    [Serializable]
    public sealed class SizeProgressRecord
    {
        public int size;
        public int unlockedLevel = 1;
    }

    [Serializable]
    public sealed class PackProgressRecord
    {
        public string packPath = string.Empty;
        public int unlockedLevel = 1;
        public int lastPlayedLevel = 1;
    }

    [Serializable]
    public sealed class DailyCompletionRecord
    {
        public string date = string.Empty;
        public string difficulty = string.Empty;
    }

    [Serializable]
    public sealed class TimeTrialScoreRecord
    {
        public int size;
        public int bestSquares;
    }

    [Serializable]
    public sealed class EconomySaveData
    {
        public int hintBalance;
        public List<string> claimedRewardIds = new List<string>();
        public List<string> deliveredPurchaseIds = new List<string>();
    }

    [Serializable]
    public sealed class StreakSaveData
    {
        public int count;
        public int lastCompletionDate;
        public int restorationGrantedDate;
    }

    [Serializable]
    public sealed class AchievementSaveData
    {
        public int lifetimePuzzleCount;
        public List<string> countedPuzzleIds = new List<string>();
        public int highestTimeTrialScore;
        public int timeTrialModeMask;
        public bool tripleThreatObserved;
        public List<string> unlockedIds = new List<string>();
    }
}
