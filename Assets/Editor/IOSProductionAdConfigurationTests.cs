#if UNITY_EDITOR && UNITY_IOS
using System.Linq;
using Shikaku.EditorTools;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

namespace Shikaku.EditorTests
{
    public sealed class IOSProductionAdConfigurationTests
    {
        [Test]
        public void ProductionIOSAdConfigurationIsComplete()
        {
            Assert.That(
                IOSAdsReleaseConfiguration.CollectErrors(),
                Is.Empty);
        }

        [Test]
        public void ProductionIdentifiersUseLevelPlayFormats()
        {
            Assert.That(
                IOSAdsReleaseConfiguration.IsValidAppKey(
                    IOSAdsReleaseConfiguration.IOSAppKey),
                Is.True);
            Assert.That(
                IOSAdsReleaseConfiguration.IsValidAdUnitId(
                    IOSAdsReleaseConfiguration.IOSRewardedAdUnitId),
                Is.True);
            Assert.That(
                IOSAdsReleaseConfiguration.IsValidAdUnitId(
                    IOSAdsReleaseConfiguration.IOSBannerAdUnitId),
                Is.True);
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("27CBD222D", false)]
        [TestCase("too-short", false)]
        [TestCase("27cbd222d", true)]
        public void AppKeyValidationRejectsUnsafeValues(
            string value,
            bool expected)
        {
            Assert.That(
                IOSAdsReleaseConfiguration.IsValidAppKey(value),
                Is.EqualTo(expected));
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("NIM37AHD2Q6ZH5NX", false)]
        [TestCase("too-short", false)]
        [TestCase("nim37ahd2q6zh5nx", true)]
        public void AdUnitValidationRejectsUnsafeValues(
            string value,
            bool expected)
        {
            Assert.That(
                IOSAdsReleaseConfiguration.IsValidAdUnitId(value),
                Is.EqualTo(expected));
        }

        [Test]
        public void PostprocessorAddsProductionAttributionSettingsOnce()
        {
            var plist = new PlistDocument();
            plist.Create();

            IOSAdsPostprocessor.ApplyProductionPlistSettings(plist);
            IOSAdsPostprocessor.ApplyProductionPlistSettings(plist);

            Assert.That(
                plist.root.values[
                        "NSAdvertisingAttributionReportEndpoint"]
                    .AsString(),
                Is.EqualTo(
                    IOSAdsReleaseConfiguration
                        .AttributionReportEndpoint));

            var networkItems = plist.root.values["SKAdNetworkItems"]
                .AsArray();
            int ironSourceEntries = networkItems.values.Count(element =>
                element.AsDict()
                    .values["SKAdNetworkIdentifier"]
                    .AsString() ==
                IOSAdsReleaseConfiguration.IronSourceSKAdNetworkId);
            Assert.That(ironSourceEntries, Is.EqualTo(1));
        }
    }
}
#endif
