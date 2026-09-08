using System;
using UnityEngine;

#if UNITY_ANDROID && SHIKAKU_GOOGLE_PLAY_GAMES
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

#if UNITY_IOS && SHIKAKU_APPLE_GAME_CENTER
using UnityEngine.SocialPlatforms.GameCenter;
#endif

namespace Shikaku.Achievements
{
    internal enum AchievementPlatform
    {
        None,
        GooglePlay,
        AppleGameCenter
    }

    internal interface IAchievementPlatformAdapter
    {
        AchievementPlatform Platform { get; }
        bool IsConfigured { get; }
        bool IsAuthenticated { get; }
        void Authenticate(bool userInitiated, Action<bool> completed);
        void Unlock(string platformId, Action<bool> completed);
        void SetStepsAtLeast(
            string platformId,
            int steps,
            int requiredSteps,
            Action<bool> completed);
        void ShowAchievements();
    }

    internal static class AchievementPlatformFactory
    {
        public static IAchievementPlatformAdapter Create()
        {
#if UNITY_ANDROID && SHIKAKU_GOOGLE_PLAY_GAMES
            return new GooglePlayAchievementAdapter();
#elif UNITY_IOS && SHIKAKU_APPLE_GAME_CENTER
            return new AppleGameCenterAchievementAdapter();
#else
            return new UnconfiguredAchievementAdapter();
#endif
        }
    }

    internal sealed class UnconfiguredAchievementAdapter :
        IAchievementPlatformAdapter
    {
        public AchievementPlatform Platform => AchievementPlatform.None;
        public bool IsConfigured => false;
        public bool IsAuthenticated => false;

        public void Authenticate(bool userInitiated, Action<bool> completed) =>
            completed?.Invoke(false);

        public void Unlock(string platformId, Action<bool> completed) =>
            completed?.Invoke(false);

        public void SetStepsAtLeast(
            string platformId,
            int steps,
            int requiredSteps,
            Action<bool> completed) =>
            completed?.Invoke(false);

        public void ShowAchievements()
        {
            Debug.Log(
                "Achievements are running locally. Add the platform IDs and " +
                "enable the Google Play Games or Apple Game Center adapter " +
                "before opening the native achievements screen.");
        }
    }

#if UNITY_ANDROID && SHIKAKU_GOOGLE_PLAY_GAMES
    /// <summary>
    /// Install the official Google Play Games plugin, replace the Google IDs,
    /// then add SHIKAKU_GOOGLE_PLAY_GAMES to Android Scripting Define Symbols.
    /// </summary>
    internal sealed class GooglePlayAchievementAdapter :
        IAchievementPlatformAdapter
    {
        private bool _authenticated;

        public AchievementPlatform Platform => AchievementPlatform.GooglePlay;
        public bool IsConfigured => true;
        public bool IsAuthenticated => _authenticated;

        public void Authenticate(bool userInitiated, Action<bool> completed)
        {
            PlayGamesPlatform.Activate();

            Action<SignInStatus> handleResult = status =>
            {
                _authenticated = status == SignInStatus.Success;
                completed?.Invoke(_authenticated);
            };

            if (userInitiated)
                PlayGamesPlatform.Instance.ManuallyAuthenticate(handleResult);
            else
                PlayGamesPlatform.Instance.Authenticate(handleResult);
        }

        public void Unlock(string platformId, Action<bool> completed)
        {
            Social.ReportProgress(platformId, 100d, completed);
        }

        public void SetStepsAtLeast(
            string platformId,
            int steps,
            int requiredSteps,
            Action<bool> completed)
        {
            PlayGamesPlatform.Instance.SetStepsAtLeast(
                platformId,
                Mathf.Clamp(steps, 0, requiredSteps),
                completed);
        }

        public void ShowAchievements() =>
            PlayGamesPlatform.Instance.ShowAchievementsUI();
    }
#endif

#if UNITY_IOS && SHIKAKU_APPLE_GAME_CENTER
    /// <summary>
    /// Configure the matching IDs in App Store Connect. The iOS release tools
    /// add the Game Center capability and scripting define automatically.
    /// </summary>
    internal sealed class AppleGameCenterAchievementAdapter :
        IAchievementPlatformAdapter
    {
        private bool _authenticated;

        public AchievementPlatform Platform =>
            AchievementPlatform.AppleGameCenter;
        public bool IsConfigured => true;
        public bool IsAuthenticated => _authenticated;

        public void Authenticate(bool userInitiated, Action<bool> completed)
        {
            GameCenterPlatform.ShowDefaultAchievementCompletionBanner(true);
            Social.localUser.Authenticate(success =>
            {
                _authenticated = success;
                completed?.Invoke(success);
            });
        }

        public void Unlock(string platformId, Action<bool> completed)
        {
            Social.ReportProgress(platformId, 100d, completed);
        }

        public void SetStepsAtLeast(
            string platformId,
            int steps,
            int requiredSteps,
            Action<bool> completed)
        {
            double percent = requiredSteps <= 0
                ? 0d
                : 100d * Mathf.Clamp(steps, 0, requiredSteps) /
                  requiredSteps;
            Social.ReportProgress(platformId, percent, completed);
        }

        public void ShowAchievements() => Social.ShowAchievementsUI();
    }
#endif
}
