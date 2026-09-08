using System;

namespace Shikaku.Achievements
{
    /// <summary>
    /// Store identifiers must stay synchronized with the achievements created
    /// in Google Play Console and App Store Connect.
    /// </summary>
    public static class AchievementPlatformIds
    {
        public const string GoogleAdventureBegins = "REPLACE_WITH_SHIKAKU_GOOGLE_ADVENTURE_BEGINS";
        public const string GoogleSeasonedExplorer = "REPLACE_WITH_SHIKAKU_GOOGLE_SEASONED_EXPLORER";
        public const string GoogleFreeThinker = "REPLACE_WITH_SHIKAKU_GOOGLE_FREE_THINKER";
        public const string GooglePackItUp = "REPLACE_WITH_SHIKAKU_GOOGLE_PACK_IT_UP";
        public const string GoogleTripleThreat = "REPLACE_WITH_SHIKAKU_GOOGLE_TRIPLE_THREAT";
        public const string GooglePerfectWeek = "REPLACE_WITH_SHIKAKU_GOOGLE_PERFECT_WEEK";
        public const string GoogleAgainstTheClock = "REPLACE_WITH_SHIKAKU_GOOGLE_AGAINST_THE_CLOCK";
        public const string GoogleClockwork = "REPLACE_WITH_SHIKAKU_GOOGLE_CLOCKWORK";
        public const string GoogleCenturyClub = "REPLACE_WITH_SHIKAKU_GOOGLE_CENTURY_CLUB";
        public const string GoogleShikakuMaster = "REPLACE_WITH_SHIKAKU_GOOGLE_SHIKAKU_MASTER";

        private const string ApplePrefix =
            "com.smoothbraingames.shikakugo.achievement.";

        public const string AppleAdventureBegins =
            ApplePrefix + "adventure_begins";
        public const string AppleSeasonedExplorer =
            ApplePrefix + "seasoned_explorer";
        public const string AppleFreeThinker =
            ApplePrefix + "free_thinker";
        public const string ApplePackItUp = ApplePrefix + "pack_it_up";
        public const string AppleTripleThreat =
            ApplePrefix + "triple_threat";
        public const string ApplePerfectWeek = ApplePrefix + "perfect_week";
        public const string AppleAgainstTheClock =
            ApplePrefix + "against_the_clock";
        public const string AppleClockwork = ApplePrefix + "clockwork";
        public const string AppleCenturyClub =
            ApplePrefix + "century_club";
        public const string AppleShikakuMaster =
            ApplePrefix + "shikaku_master";

        public static string GetGoogleId(AchievementId id)
        {
            switch (id)
            {
                case AchievementId.AdventureBegins: return GoogleAdventureBegins;
                case AchievementId.SeasonedExplorer: return GoogleSeasonedExplorer;
                case AchievementId.FreeThinker: return GoogleFreeThinker;
                case AchievementId.PackItUp: return GooglePackItUp;
                case AchievementId.TripleThreat: return GoogleTripleThreat;
                case AchievementId.PerfectWeek: return GooglePerfectWeek;
                case AchievementId.AgainstTheClock: return GoogleAgainstTheClock;
                case AchievementId.Clockwork: return GoogleClockwork;
                case AchievementId.CenturyClub: return GoogleCenturyClub;
                case AchievementId.ShikakuMaster: return GoogleShikakuMaster;
                default: return string.Empty;
            }
        }

        public static string GetAppleId(AchievementId id)
        {
            switch (id)
            {
                case AchievementId.AdventureBegins: return AppleAdventureBegins;
                case AchievementId.SeasonedExplorer: return AppleSeasonedExplorer;
                case AchievementId.FreeThinker: return AppleFreeThinker;
                case AchievementId.PackItUp: return ApplePackItUp;
                case AchievementId.TripleThreat: return AppleTripleThreat;
                case AchievementId.PerfectWeek: return ApplePerfectWeek;
                case AchievementId.AgainstTheClock: return AppleAgainstTheClock;
                case AchievementId.Clockwork: return AppleClockwork;
                case AchievementId.CenturyClub: return AppleCenturyClub;
                case AchievementId.ShikakuMaster: return AppleShikakuMaster;
                default: return string.Empty;
            }
        }

        public static bool IsConfigured(string platformId)
        {
            return !string.IsNullOrWhiteSpace(platformId) &&
                   !platformId.StartsWith("REPLACE_", StringComparison.Ordinal);
        }
    }
}
