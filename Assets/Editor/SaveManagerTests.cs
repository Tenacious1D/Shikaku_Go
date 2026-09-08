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
