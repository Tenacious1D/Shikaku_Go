using UnityEngine;

namespace Shikaku.Privacy
{
    public static class PrivacyOnboardingState
    {
        private const string WelcomeVersionPlayerPrefsKey =
            "privacy_welcome_version";

        public const int CurrentWelcomeVersion = 1;

        public static bool HasAcknowledgedCurrentVersion =>
            PlayerPrefs.GetInt(WelcomeVersionPlayerPrefsKey, 0) >=
            CurrentWelcomeVersion;

        public static void AcknowledgeCurrentVersion()
        {
            PlayerPrefs.SetInt(
                WelcomeVersionPlayerPrefsKey,
                CurrentWelcomeVersion);
            PlayerPrefs.Save();
        }
    }
}
