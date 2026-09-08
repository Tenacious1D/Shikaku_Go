using System;
using Shikaku.Store;
using UnityEngine;
using Shikaku.SaveSystem;

namespace Shikaku.Services
{
    public readonly struct DailyStreakUpdate
    {
        public DailyStreakUpdate(
            bool advanced,
            int previousStreak,
            int currentStreak,
            int hintReward)
        {
            Advanced = advanced;
            PreviousStreak = previousStreak;
            CurrentStreak = currentStreak;
            HintReward = hintReward;
        }

        public bool Advanced { get; }
        public int PreviousStreak { get; }
        public int CurrentStreak { get; }
        public int HintReward { get; }

        public static DailyStreakUpdate NoChange(int currentStreak) =>
            new DailyStreakUpdate(
                false,
                currentStreak,
                currentStreak,
                0);
    }

    public readonly struct DailyStreakRestoreOffer
    {
        public DailyStreakRestoreOffer(
            bool isEligible,
            int currentStreak,
            int missedDays)
        {
            IsEligible = isEligible;
            CurrentStreak = currentStreak;
            MissedDays = missedDays;
        }

        public bool IsEligible { get; }
        public int CurrentStreak { get; }
        public int MissedDays { get; }

        public static DailyStreakRestoreOffer None(int currentStreak) =>
            new DailyStreakRestoreOffer(false, currentStreak, 0);
    }

    /// <summary>
    /// Tracks the first puzzle completion on each local calendar day.
    /// This is intentionally independent of the Daily Puzzle calendar.
    /// </summary>
    public static class DailyStreakService
    {
        private const string CurrentStreakKey =
            "daily_play_streak_count";
        private const string LastCompletionDateKey =
            "daily_play_streak_last_date";

        public static event Action PuzzleCompleted;

        public static int CurrentStreak =>
            NormalizeAndReadStreak(DateTime.Today);

        public static bool HasCompletedToday =>
            TryReadLastCompletionDate(out DateTime lastDate) &&
            lastDate.Date >= DateTime.Today;

        public static DailyStreakRestoreOffer GetRestoreOffer()
        {
            DateTime today = DateTime.Today;
            int storedStreak = NormalizeAndReadStreak(today);

            if (storedStreak <= 0 ||
                IsRestorationGrantedForDate(today) ||
                !TryReadLastCompletionDate(out DateTime lastDate))
            {
                return DailyStreakRestoreOffer.None(storedStreak);
            }

            int daysSinceCompletion =
                (today - lastDate.Date).Days;
            int missedDays = daysSinceCompletion - 1;

            if (missedDays < 1 || missedDays > 2)
                return DailyStreakRestoreOffer.None(storedStreak);

            return new DailyStreakRestoreOffer(
                true,
                storedStreak,
                missedDays);
        }

        public static bool TryGrantRestoration()
        {
            DailyStreakRestoreOffer offer = GetRestoreOffer();
            if (!offer.IsEligible)
                return false;

            SaveManager.SetDailyStreakRestorationGrantedDate(
                EncodeDate(DateTime.Today));

            Debug.Log(
                $"DailyStreakService: Restored the " +
                $"{offer.CurrentStreak}-day streak after " +
                $"{offer.MissedDays} missed day(s). Complete a puzzle " +
                "today to continue it.");
            return true;
        }

        public static bool TryRestoreWithHint()
        {
            DailyStreakRestoreOffer offer = GetRestoreOffer();
            if (!offer.IsEligible || HintWallet.Balance <= 0)
                return false;

            using (SaveManager.BeginBatch())
            {
                if (!HintWallet.TrySpendHint())
                    return false;

                SaveManager.SetDailyStreakRestorationGrantedDate(
                    EncodeDate(DateTime.Today));
            }

            Debug.Log(
                $"DailyStreakService: Used one hint to restore the " +
                $"{offer.CurrentStreak}-day streak.");
            return true;
        }

        public static bool DeclineRestoration()
        {
            DailyStreakRestoreOffer offer = GetRestoreOffer();
            if (!offer.IsEligible)
                return false;

            SaveManager.SetDailyStreak(0, 0);
            Debug.Log(
                $"DailyStreakService: The {offer.CurrentStreak}-day " +
                "streak was not restored.");
            return true;
        }

        public static DailyStreakUpdate RecordPuzzleCompleted()
        {
            DateTime today = DateTime.Today;
            int storedStreak = NormalizeAndReadStreak(today);

            if (TryReadLastCompletionDate(out DateTime lastDate) &&
                lastDate.Date >= today)
            {
                // The streak has already advanced today. A future saved date
                // is also protected from duplicate rewards after clock changes.
                PuzzleCompleted?.Invoke();
                return DailyStreakUpdate.NoChange(storedStreak);
            }

            int previousStreak =
                TryReadLastCompletionDate(out lastDate) &&
                (lastDate.Date == today.AddDays(-1) ||
                 IsRestorationGrantedForDate(today))
                    ? storedStreak
                    : 0;

            int currentStreak = previousStreak + 1;
            int hintReward = RewardForStreak(currentStreak);

            using (SaveManager.BeginBatch())
            {
                SaveManager.SetDailyStreak(
                    currentStreak,
                    EncodeDate(today));

                // HintWallet raises BalanceChanged, so the gameplay badge
                // refreshes immediately with the streak reward.
                HintWallet.AddHints(hintReward);
            }

            Debug.Log(
                $"DailyStreakService: Advanced to {currentStreak}. " +
                $"Awarded {hintReward} hint(s).");

            PuzzleCompleted?.Invoke();

            return new DailyStreakUpdate(
                true,
                previousStreak,
                currentStreak,
                hintReward);
        }

        public static int RewardForStreak(int streak)
        {
            if (streak > 0 && streak % 28 == 0)
                return 10;

            if (streak > 0 && streak % 7 == 0)
                return 5;

            return 1;
        }

        private static int NormalizeAndReadStreak(DateTime today)
        {
            int storedStreak = Mathf.Max(
                0,
                SaveManager.DailyStreakCount);

            if (storedStreak == 0 ||
                !TryReadLastCompletionDate(out DateTime lastDate))
            {
                if (storedStreak != 0 ||
                    SaveManager.DailyStreakLastCompletionDate != 0 ||
                    SaveManager.DailyStreakRestorationGrantedDate != 0)
                {
                    SaveManager.SetDailyStreak(0, 0);
                }

                return 0;
            }

            if (TryReadRestorationGrantedDate(
                    out DateTime restorationDate))
            {
                if (restorationDate.Date >= today)
                    return storedStreak;

                if (lastDate.Date < restorationDate.Date)
                {
                    SaveManager.SetDailyStreak(0, 0);
                    return 0;
                }
            }

            int missedDays = (today - lastDate.Date).Days - 1;
            if (missedDays > 2)
            {
                SaveManager.SetDailyStreak(0, 0);
                return 0;
            }

            return storedStreak;
        }

        private static bool TryReadLastCompletionDate(
            out DateTime date)
        {
            return TryDecodeDate(
                SaveManager.DailyStreakLastCompletionDate,
                out date);
        }

        private static bool TryReadRestorationGrantedDate(
            out DateTime date)
        {
            return TryDecodeDate(
                SaveManager.DailyStreakRestorationGrantedDate,
                out date);
        }

        private static bool IsRestorationGrantedForDate(DateTime date)
        {
            return TryReadRestorationGrantedDate(
                       out DateTime restorationDate) &&
                   restorationDate.Date >= date.Date;
        }

        private static bool TryDecodeDate(
            int encoded,
            out DateTime date)
        {
            int year = encoded / 10000;
            int month = encoded / 100 % 100;
            int day = encoded % 100;

            try
            {
                date = new DateTime(year, month, day);
                return encoded > 0;
            }
            catch (ArgumentOutOfRangeException)
            {
                date = default;
                return false;
            }
        }

        private static int EncodeDate(DateTime date) =>
            date.Year * 10000 + date.Month * 100 + date.Day;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ResetForTesting()
        {
            SaveManager.ResetDailyStreakForTesting();
        }
#endif
    }
}
