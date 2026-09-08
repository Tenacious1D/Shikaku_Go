using NUnit.Framework;

namespace Shikaku.EditorTools
{
    public sealed class FirebaseIOSConfigurationTests
    {
        [Test]
        public void PlistMatchesTheRegisteredShikakuAppleApp()
        {
            Assert.That(
                FirebaseIOSConfiguration.CollectErrors(),
                Is.Empty);
        }

        [Test]
        public void RegisteredBundleMatchesTheIOSReleaseBundle()
        {
            Assert.That(
                FirebaseIOSConfiguration.ExpectedBundleId,
                Is.EqualTo("com.smoothbraingames.shikakugo"));
        }

        [Test]
        public void FirebaseAppleAppUsesTheProductionProject()
        {
            Assert.That(
                FirebaseIOSConfiguration.ExpectedProjectId,
                Is.EqualTo("REPLACE_WITH_SHIKAKU_FIREBASE_PROJECT_ID"));
            Assert.That(
                FirebaseIOSConfiguration.ExpectedGoogleAppId,
                Does.StartWith("1:REPLACE_WITH_SHIKAKU_FIREBASE_SENDER_ID:ios:"));
        }
    }
}
