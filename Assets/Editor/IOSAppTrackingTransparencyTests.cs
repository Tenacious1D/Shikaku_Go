using Shikaku.EditorTools;
using Shikaku.Privacy;
using NUnit.Framework;

namespace Shikaku.EditorTests
{
    public sealed class IOSAppTrackingTransparencyTests
    {
        [Test]
        public void IOSAdAndATTSettingsMatchProduction()
        {
            Assert.That(
                IOSATTReleaseConfiguration.CollectErrors(),
                Is.Empty);
        }

        [TestCase(IOSATTAuthorizationStatus.NotDetermined, true)]
        [TestCase(IOSATTAuthorizationStatus.Restricted, false)]
        [TestCase(IOSATTAuthorizationStatus.Denied, false)]
        [TestCase(IOSATTAuthorizationStatus.Authorized, false)]
        [TestCase(IOSATTAuthorizationStatus.Unavailable, false)]
        public void PromptIsOnlyRequiredWhileUndecided(
            IOSATTAuthorizationStatus status,
            bool expected)
        {
            Assert.That(
                IOSAppTrackingTransparency.RequiresSystemPrompt(status),
                Is.EqualTo(expected));
        }

        [TestCase(IOSATTAuthorizationStatus.Authorized, true)]
        [TestCase(IOSATTAuthorizationStatus.NotDetermined, false)]
        [TestCase(IOSATTAuthorizationStatus.Restricted, false)]
        [TestCase(IOSATTAuthorizationStatus.Denied, false)]
        [TestCase(IOSATTAuthorizationStatus.Unavailable, false)]
        public void TrackingRequiresExplicitAuthorization(
            IOSATTAuthorizationStatus status,
            bool expected)
        {
            Assert.That(
                IOSAppTrackingTransparency.AllowsTracking(status),
                Is.EqualTo(expected));
        }
    }
}
