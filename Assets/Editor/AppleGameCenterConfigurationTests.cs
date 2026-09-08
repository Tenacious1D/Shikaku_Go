using System;
using System.Collections.Generic;
using Shikaku.Achievements;
using NUnit.Framework;

namespace Shikaku.EditorTests
{
    public sealed class AppleGameCenterConfigurationTests
    {
        private const string Prefix =
            "com.smoothbraingames.shikakugo.achievement.";

        [Test]
        public void EveryAchievementHasAUniqueValidAppleIdentifier()
        {
            var identifiers =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (AchievementId achievement in
                     Enum.GetValues(typeof(AchievementId)))
            {
                string identifier =
                    AchievementPlatformIds.GetAppleId(achievement);

                Assert.That(
                    AchievementPlatformIds.IsConfigured(identifier),
                    Is.True,
                    $"Missing Apple ID for {achievement}.");
                Assert.That(
                    identifier,
                    Does.StartWith(Prefix),
                    $"Unexpected Apple ID namespace for {achievement}.");
                Assert.That(
                    identifier.Length,
                    Is.LessThanOrEqualTo(100),
                    $"Apple ID is too long for {achievement}.");
                Assert.That(
                    identifiers.Add(identifier),
                    Is.True,
                    $"Duplicate Apple ID for {achievement}.");
            }

            Assert.That(identifiers, Has.Count.EqualTo(10));
        }

        [TestCase(
            AchievementId.AdventureBegins,
            Prefix + "adventure_begins")]
        [TestCase(
            AchievementId.SeasonedExplorer,
            Prefix + "seasoned_explorer")]
        [TestCase(
            AchievementId.FreeThinker,
            Prefix + "free_thinker")]
        [TestCase(
            AchievementId.PackItUp,
            Prefix + "pack_it_up")]
        [TestCase(
            AchievementId.TripleThreat,
            Prefix + "triple_threat")]
        [TestCase(
            AchievementId.PerfectWeek,
            Prefix + "perfect_week")]
        [TestCase(
            AchievementId.AgainstTheClock,
            Prefix + "against_the_clock")]
        [TestCase(
            AchievementId.Clockwork,
            Prefix + "clockwork")]
        [TestCase(
            AchievementId.CenturyClub,
            Prefix + "century_club")]
        [TestCase(
            AchievementId.ShikakuMaster,
            Prefix + "shikaku_master")]
        public void AppleIdentifierMatchesCatalog(
            AchievementId achievement,
            string expected)
        {
            Assert.That(
                AchievementPlatformIds.GetAppleId(achievement),
                Is.EqualTo(expected));
        }
    }
}
