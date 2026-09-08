using System.Reflection;
using Shikaku.Store;
using NUnit.Framework;
using UnityEngine;

namespace Shikaku.EditorTests
{
    public sealed class StoreRatingServiceTests
    {
        private const string AppleWriteReviewUrl =
            "https://apps.apple.com/app/idREPLACE_WITH_SHIKAKU_APPLE_APP_ID" +
            "?action=write-review";
        private const string GooglePlayListingUrl =
            "https://play.google.com/store/apps/details?id=" +
            "com.smoothbraingames.shikakugo";

        [Test]
        public void IPhoneUsesAppleWriteReviewUrl()
        {
            string url = ResolveStoreReviewUrl(
                RuntimePlatform.IPhonePlayer);

            Assert.That(url, Is.EqualTo(AppleWriteReviewUrl));
            Assert.That(url, Does.Not.Contain("play.google.com"));
        }

        [Test]
        public void AndroidKeepsGooglePlayListingUrl()
        {
            string url = ResolveStoreReviewUrl(RuntimePlatform.Android);

            Assert.That(url, Is.EqualTo(GooglePlayListingUrl));
        }

        [Test]
        public void OnlyIPhoneUsesNativeReviewPrompt()
        {
            MethodInfo method = GetPrivateMethod(
                "UsesNativeReviewPrompt");

            Assert.That(
                method.Invoke(
                    null,
                    new object[] { RuntimePlatform.IPhonePlayer }),
                Is.EqualTo(true));
            Assert.That(
                method.Invoke(
                    null,
                    new object[] { RuntimePlatform.Android }),
                Is.EqualTo(false));
        }

        [Test]
        public void UnsupportedPlatformsDoNotReceiveAnotherStoresUrl()
        {
            Assert.That(
                ResolveStoreReviewUrl(RuntimePlatform.WindowsPlayer),
                Is.Null);
        }

        private static string ResolveStoreReviewUrl(
            RuntimePlatform platform)
        {
            return (string)GetPrivateMethod("GetStoreReviewUrl").Invoke(
                null,
                new object[] { platform });
        }

        private static MethodInfo GetPrivateMethod(string name)
        {
            MethodInfo method = typeof(StoreRatingService).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
