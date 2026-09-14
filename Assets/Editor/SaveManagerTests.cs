using System;
using System.IO;
using Shikaku.SaveSystem;
using NUnit.Framework;

namespace Shikaku.EditorTests
{
    public sealed class SaveManagerTests
    {
        private string _testDirectory;
        private string _savePath;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(
                Path.GetTempPath(),
                "ShikakuSaveManagerTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _savePath = Path.Combine(_testDirectory, "save.json");
            SaveManager.LoadIsolatedSaveForTesting(_savePath);
        }

        [TearDown]
        public void TearDown()
        {
            SaveManager.Flush();
            SaveManager.ClearIsolatedSaveForTesting();
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [Test]
        public void DurableOutcomesRoundTripWithoutUnfinishedBoardState()
        {
            SaveManager.MarkPuzzleCompleted("Pack|puzzle_001");
            SaveManager.SaveBestTimeIfBetter("puzzle_001", 42.5f);
            SaveManager.SetUnlockedLevel(4, 7);
            SaveManager.SetUnlockedLevelForPack("Story/Chapter01", 8);
            SaveManager.SetLastPlayedLevelForPack("Story/Chapter01", 6);
            SaveManager.SetStoryCurrentPack("Story/Chapter01");
            SaveManager.SetDailyCompleted(
                new DateTime(2026, 8, 18),
                "Hard",
                true);
            SaveManager.SaveTimeTrialBestSquaresIfHigher(4, 123);
            SaveManager.AddHints(5);
            SaveManager.MarkTutorialCompleted();
            SaveManager.Flush();

            SaveManager.LoadIsolatedSaveForTesting(_savePath);

            Assert.That(SaveManager.IsPuzzleCompleted("puzzle_001"), Is.True);
            Assert.That(
                SaveManager.TryGetBestTime("puzzle_001", out float best),
                Is.True);
            Assert.That(best, Is.EqualTo(42.5f));
            Assert.That(SaveManager.GetUnlockedLevel(4), Is.EqualTo(7));
            Assert.That(
                SaveManager.GetUnlockedLevelForPack("Story/Chapter01"),
                Is.EqualTo(8));
            Assert.That(
                SaveManager.GetLastPlayedLevelForPack("Story/Chapter01"),
                Is.EqualTo(6));
            Assert.That(
                SaveManager.GetStoryCurrentPack(),
                Is.EqualTo("Story/Chapter01"));
            Assert.That(
                SaveManager.IsDailyCompleted(
                    new DateTime(2026, 8, 18),
                    "Hard"),
                Is.True);
            Assert.That(
                SaveManager.GetTimeTrialBestSquares(4),
                Is.EqualTo(123));
            Assert.That(SaveManager.HintBalance, Is.EqualTo(5));
            Assert.That(SaveManager.TutorialCompleted, Is.True);

            string json = File.ReadAllText(_savePath);
            Assert.That(json, Does.Not.Contain("cellValues"));
            Assert.That(json, Does.Not.Contain("hintLocked"));
            Assert.That(json, Does.Not.Contain("elapsedSeconds"));
        }

        [Test]
        public void BestTimesAndEconomyOperationsAreIdempotent()
        {
            Assert.That(
                SaveManager.SaveBestTimeIfBetter("puzzle_002", 20f),
                Is.True);
            Assert.That(
                SaveManager.SaveBestTimeIfBetter("puzzle_002", 25f),
                Is.False);
            Assert.That(
                SaveManager.SaveBestTimeIfBetter("puzzle_002", 15f),
                Is.True);

            Assert.That(
                SaveManager.TryDeliverPurchasedHints("purchase-a", 10),
                Is.True);
            Assert.That(
                SaveManager.TryDeliverPurchasedHints("purchase-a", 10),
                Is.False);
            Assert.That(
                SaveManager.TryClaimHintReward("reward-a", 3),
                Is.True);
            Assert.That(
                SaveManager.TryClaimHintReward("reward-a", 3),
                Is.False);
            Assert.That(SaveManager.TrySpendHint(), Is.True);

            Assert.That(
                SaveManager.TryGetBestTime("puzzle_002", out float best),
                Is.True);
            Assert.That(best, Is.EqualTo(15f));
            Assert.That(SaveManager.HintBalance, Is.EqualTo(12));
        }

        [Test]
        public void PuzzleResetClearsProgressAndBackupButPreservesOtherPlayerData()
        {
            var packs = Shikaku.Menu.PuzzleCatalog.StoryPacks;
            Assert.That(packs.Count, Is.GreaterThan(1));
            string pack = packs[1].PackPath;
            string puzzle = Shikaku.Menu.PuzzleCatalog.GetPackPuzzleIds(pack)[0];
            var day = new DateTime(2026, 9, 12);
            SaveManager.MarkPuzzleCompleted(puzzle);
            SaveManager.SaveBestTimeIfBetter(puzzle, 31f);
            SaveManager.SetUnlockedLevel(5, 12);
            SaveManager.SetUnlockedLevelForPack(pack, 8);
            SaveManager.SetLastPlayedLevelForPack(pack, 7);
            SaveManager.SetStoryCurrentPack(pack);
            SaveManager.SetDailyCompleted(day, "Hard", true);
            SaveManager.SetDailyStreak(4, 20260912);
            SaveManager.SetDailyStreakRestorationGrantedDate(20260913);
            SaveManager.SaveTimeTrialBestSquaresIfHigher(5, 90);
            SaveManager.TryDeliverPurchasedHints("reset-test-purchase", 8);
            SaveManager.TryClaimHintReward("reset-test-reward", 3);
            SaveManager.RecordLifetimePuzzle(puzzle);
            SaveManager.UnlockAchievement("adventurebegins");
            SaveManager.AddAchievementTimeTrialMode(2);
            SaveManager.SetHighestAchievementTimeTrialScore(90);
            SaveManager.MarkTripleThreatObserved();
            SaveManager.MarkTutorialCompleted();
            var before = UnityEngine.JsonUtility.FromJson<SaveData>(File.ReadAllText(_savePath));

            Assert.That(SaveManager.ResetPuzzleProgressForTesting(out string recovery), Is.True);
            Assert.That(File.Exists(recovery), Is.True);
            var snapshot = UnityEngine.JsonUtility.FromJson<SaveData>(File.ReadAllText(recovery));
            Assert.That(snapshot.puzzles.Exists(record => record.puzzleId == puzzle && record.completed), Is.True);
            Assert.That(SaveManager.IsPuzzleCompleted(puzzle), Is.False, "The cached state must reset immediately.");
            Assert.That(SaveManager.TryGetBestTime(puzzle, out _), Is.False);
            Assert.That(SaveManager.GetUnlockedLevel(5), Is.EqualTo(1));
            Assert.That(SaveManager.GetUnlockedLevelForPack(pack), Is.EqualTo(1));
            Assert.That(SaveManager.GetLastPlayedLevelForPack(pack), Is.EqualTo(1));
            Assert.That(SaveManager.GetStoryCurrentPack(), Is.Empty);
            Assert.That(SaveManager.IsDailyCompleted(day, "Hard"), Is.False);
            Assert.That(SaveManager.DailyStreakCount, Is.Zero);
            Assert.That(SaveManager.DailyStreakRestorationGrantedDate, Is.Zero);
            Assert.That(SaveManager.GetTimeTrialBestSquares(5), Is.Zero);
            Assert.That(Shikaku.Menu.Progression.GetHighestUnlockedStoryChapterIndex(), Is.Zero);
            Assert.That(Shikaku.Menu.Progression.GetNextStoryLevel(packs[0].PackPath), Is.EqualTo(1));
            Assert.That(Shikaku.Menu.Progression.IsStoryChapterUnlocked(pack), Is.False);
            var after = UnityEngine.JsonUtility.FromJson<SaveData>(File.ReadAllText(_savePath));
            Assert.That(UnityEngine.JsonUtility.ToJson(after.economy), Is.EqualTo(UnityEngine.JsonUtility.ToJson(before.economy)));
            Assert.That(UnityEngine.JsonUtility.ToJson(after.achievements), Is.EqualTo(UnityEngine.JsonUtility.ToJson(before.achievements)));
            Assert.That(after.tutorialCompleted, Is.True);
            Assert.That(File.ReadAllText(SaveManager.BackupPath), Is.EqualTo(File.ReadAllText(_savePath)));

            SaveManager.LoadIsolatedSaveForTesting(_savePath);
            Assert.That(SaveManager.IsPuzzleCompleted(puzzle), Is.False, "Reset must survive a reload.");
            // Recovery of a missing primary must not bring the old puzzle completion back.
            File.Delete(_savePath);
            SaveManager.LoadIsolatedSaveForTesting(_savePath);
            Assert.That(SaveManager.IsPuzzleCompleted(puzzle), Is.False);
            Assert.That(SaveManager.HintBalance, Is.EqualTo(11));
            Assert.That(SaveManager.HasDeliveredHintPurchase("reset-test-purchase"), Is.True);
            Assert.That(SaveManager.HasClaimedReward("reset-test-reward"), Is.True);
            Assert.That(SaveManager.IsAchievementUnlocked("adventurebegins"), Is.True);
            Assert.That(SaveManager.ResetPuzzleProgressForTesting(out string secondRecovery), Is.True);
            Assert.That(secondRecovery, Is.Not.EqualTo(recovery));
            Assert.That(File.Exists(recovery), Is.True, "Repeated resets must keep the earlier recovery snapshot.");
        }

        [Test]
        public void PuzzleResetRejectsAnActiveSaveBatchWithoutChangingProgress()
        {
            using (SaveManager.BeginBatch())
            {
                SaveManager.MarkPuzzleCompleted("batched-puzzle");
                Assert.Throws<InvalidOperationException>(() => SaveManager.ResetPuzzleProgressForTesting(out _));
                Assert.That(SaveManager.IsPuzzleCompleted("batched-puzzle"), Is.True);
            }
            SaveManager.LoadIsolatedSaveForTesting(_savePath);
            Assert.That(SaveManager.IsPuzzleCompleted("batched-puzzle"), Is.True);
        }
        [Test]
        public void CorruptPrimaryRecoversLastKnownGoodBackup()
        {
            SaveManager.MarkPuzzleCompleted("puzzle_003");
            SaveManager.AddHints(2);
            SaveManager.Flush();

            Assert.That(File.Exists(SaveManager.BackupPath), Is.True);
            File.WriteAllText(_savePath, "{ not valid json");

            SaveManager.LoadIsolatedSaveForTesting(_savePath);

            Assert.That(SaveManager.IsPuzzleCompleted("puzzle_003"), Is.True);
            Assert.That(SaveManager.HintBalance, Is.EqualTo(0));
        }
    }
}
