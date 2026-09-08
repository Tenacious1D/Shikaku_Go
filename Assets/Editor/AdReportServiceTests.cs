using System;
using System.Collections.Generic;
using Shikaku.Ads;
using NUnit.Framework;

namespace Shikaku.EditorTests
{
    public sealed class AdReportServiceTests
    {
        [Test]
        public void ProductionSupportDestinationsAreConfigured()
        {
            Assert.That(
                AdReportService.SupportEmail,
                Is.EqualTo("support@smoothbraingames.com"));
            Assert.That(
                AdReportService.SupportPageUrl,
                Is.EqualTo("https://smoothbraingames.com/#support"));
        }

        [Test]
        public void ReportIncludesCreativeLookupDetailsAndScreenshotPrompt()
        {
            var ads = new List<AdReportEntry>
            {
                CreateEntry("creative-123", "auction-456")
            };

            string report = AdReportService.BuildReportBody(
                ads,
                "1.0.0",
                "IPhonePlayer",
                "iPhone",
                "iOS");

            Assert.That(report, Does.Contain("attach a screenshot"));
            Assert.That(report, Does.Contain("creative-123"));
            Assert.That(report, Does.Contain("auction-456"));
            Assert.That(report, Does.Contain("hint"));
            Assert.That(report, Does.Contain("AdMob"));
        }

        [Test]
        public void ReportExplicitlyExcludesAdvertisingIdentifiers()
        {
            string report = AdReportService.BuildReportBody(
                Array.Empty<AdReportEntry>(),
                "1.0.0",
                "IPhonePlayer",
                "iPhone",
                "iOS");

            Assert.That(
                report,
                Does.Contain("no advertising ID is included"));
            Assert.That(report, Does.Not.Contain("IDFA:"));
            Assert.That(report, Does.Not.Contain("GAID:"));
        }

        [Test]
        public void MailToUrlUsesProductionSupportEmailAndEscapesBody()
        {
            string url = AdReportService.CreateMailToUrl(
                "Problem ad\nCreative: 123");

            Assert.That(
                url,
                Does.StartWith(
                    "mailto:support@smoothbraingames.com?subject="));
            Assert.That(url, Does.Contain("&body="));
            Assert.That(url, Does.Not.Contain("\n"));
        }

        [Test]
        public void ReportValuesCannotInjectAdditionalLines()
        {
            var entry = new AdReportEntry(
                DateTime.UtcNow,
                "rewarded\nFake field: value",
                "hint",
                "AdMob",
                "instance",
                "creative",
                "ad",
                "auction",
                "unit");

            string report = AdReportService.BuildReportBody(
                new[] { entry },
                "1.0.0",
                "iOS",
                "iPhone",
                "iOS");

            Assert.That(
                report,
                Does.Contain("rewarded Fake field: value"));
            Assert.That(
                report,
                Does.Not.Contain("Format: rewarded\n"));
        }

        private static AdReportEntry CreateEntry(
            string creativeId,
            string auctionId)
        {
            return new AdReportEntry(
                new DateTime(
                    2026,
                    8,
                    26,
                    1,
                    2,
                    3,
                    DateTimeKind.Utc),
                "rewarded",
                "hint",
                "AdMob",
                "AdMob instance",
                creativeId,
                "ad-789",
                auctionId,
                "unit-012");
        }
    }
}
