using System;
using Shikaku.Services;
using UnityEngine;

namespace Shikaku.Store
{
    public static class StoreRatingService
    {
        private const string PromptPendingKey =
            "store_rating_prompt_pending";
        private const string PromptShownKey =
            "store_rating_prompt_shown";
        private const string StoreOpenedKey =
            "store_rating_store_opened";
        private const string GooglePlayPackageId =
            "com.smoothbraingames.shikakugo";
        private const string AppleAppStoreId = "REPLACE_WITH_SHIKAKU_APPLE_APP_ID";
        private const string AppleWriteReviewUrl =
            "https://apps.apple.com/app/id" + AppleAppStoreId +
            "?action=write-review";
        private const string GooglePlayListingUrl =
            "https://play.google.com/store/apps/details?id=" +
            GooglePlayPackageId;

        public static bool HasPendingPrompt =>
            PlayerPrefs.GetInt(PromptPendingKey, 0) == 1 &&
            PlayerPrefs.GetInt(PromptShownKey, 0) == 0 &&
            PlayerPrefs.GetInt(StoreOpenedKey, 0) == 0;

        public static bool QueueForStreak(
            DailyStreakUpdate streakUpdate)
        {
            if (!streakUpdate.Advanced ||
                streakUpdate.PreviousStreak != 2 ||
                streakUpdate.CurrentStreak != 3 ||
                PlayerPrefs.GetInt(PromptShownKey, 0) == 1 ||
                PlayerPrefs.GetInt(StoreOpenedKey, 0) == 1)
            {
                return false;
            }

            PlayerPrefs.SetInt(PromptPendingKey, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool MarkPromptShown()
        {
            if (!HasPendingPrompt)
                return false;

            PlayerPrefs.SetInt(PromptShownKey, 1);
            PlayerPrefs.DeleteKey(PromptPendingKey);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Handles an earned iOS review opportunity with Apple's system UI.
        /// Apple disallows custom review prompts, so callers should skip their
        /// custom modal when this method returns true.
        /// </summary>
        public static bool TryRequestPendingNativeReview()
        {
            if (!UsesNativeReviewPrompt(Application.platform) ||
                !MarkPromptShown())
            {
                return false;
            }

#if UNITY_IOS && !UNITY_EDITOR
            if (!UnityEngine.iOS.Device.RequestStoreReview())
            {
                Debug.LogWarning(
                    "StoreRatingService: Apple's native review API " +
                    "was unavailable. The rating opportunity was " +
                    "left to the system rather than showing a custom " +
                    "review prompt.");
            }
#endif

            return true;
        }

        public static void OpenStoreListing()
        {
            PlayerPrefs.SetInt(StoreOpenedKey, 1);
            PlayerPrefs.DeleteKey(PromptPendingKey);
            PlayerPrefs.Save();

            string webUrl = GetStoreReviewUrl(Application.platform);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer =
                    new AndroidJavaClass(
                        "com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>(
                        "currentActivity");
                using var uriClass =
                    new AndroidJavaClass("android.net.Uri");
                using AndroidJavaObject marketUri =
                    uriClass.CallStatic<AndroidJavaObject>(
                        "parse",
                        "market://details?id=" +
                        GooglePlayPackageId);
                using var intent =
                    new AndroidJavaObject(
                        "android.content.Intent",
                        "android.intent.action.VIEW",
                        marketUri);

                using AndroidJavaObject configuredIntent =
                    intent.Call<AndroidJavaObject>(
                        "setPackage",
                        "com.android.vending");
                activity.Call("startActivity", configuredIntent);
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "StoreRatingService: Google Play app could " +
                    $"not be opened. {exception.Message}");
            }
#endif

            if (string.IsNullOrEmpty(webUrl))
            {
                Debug.LogWarning(
                    "StoreRatingService: No rating URL is configured " +
                    $"for {Application.platform}.");
                return;
            }

            Application.OpenURL(webUrl);
        }

        private static bool UsesNativeReviewPrompt(
            RuntimePlatform platform)
        {
            return platform == RuntimePlatform.IPhonePlayer;
        }

        private static string GetStoreReviewUrl(
            RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    return AppleWriteReviewUrl;
                case RuntimePlatform.Android:
                    return GooglePlayListingUrl;
                default:
                    return null;
            }
        }
    }
}