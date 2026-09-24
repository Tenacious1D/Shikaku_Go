using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Services.LevelPlay;
using UnityEngine;
using UnityEngine.Networking;

namespace Shikaku.Ads
{
    public sealed class AdReportEntry
    {
        public DateTime CapturedAtUtc { get; }
        public string Format { get; }
        public string Placement { get; }
        public string Network { get; }
        public string Instance { get; }
        public string CreativeId { get; }
        public string AdId { get; }
        public string AuctionId { get; }
        public string AdUnitId { get; }

        public AdReportEntry(
            DateTime capturedAtUtc,
            string format,
            string placement,
            string network,
            string instance,
            string creativeId,
            string adId,
            string auctionId,
            string adUnitId)
        {
            CapturedAtUtc = capturedAtUtc.Kind == DateTimeKind.Utc
                ? capturedAtUtc
                : capturedAtUtc.ToUniversalTime();
            Format = AdReportService.CleanValue(format);
            Placement = AdReportService.CleanValue(placement);
            Network = AdReportService.CleanValue(network);
            Instance = AdReportService.CleanValue(instance);
            CreativeId = AdReportService.CleanValue(creativeId);
            AdId = AdReportService.CleanValue(adId);
            AuctionId = AdReportService.CleanValue(auctionId);
            AdUnitId = AdReportService.CleanValue(adUnitId);
        }

        internal static AdReportEntry FromLevelPlay(
            DateTime capturedAtUtc,
            string fallbackFormat,
            string fallbackPlacement,
            LevelPlayAdInfo adInfo)
        {
            return new AdReportEntry(
                capturedAtUtc,
                FirstAvailable(adInfo?.AdFormat, fallbackFormat),
                FirstAvailable(
                    adInfo?.PlacementName,
                    fallbackPlacement),
                adInfo?.AdNetwork,
                adInfo?.InstanceName,
                adInfo?.CreativeId,
                adInfo?.AdId,
                adInfo?.AuctionId,
                adInfo?.AdUnitId);
        }

        private static string FirstAvailable(
            string preferred,
            string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred)
                ? fallback
                : preferred;
        }
    }

    /// <summary>
    /// Keeps a short in-memory history of displayed ads and opens an editable
    /// support email containing the identifiers needed to find a problematic
    /// creative in LevelPlay Ad Quality. Nothing is uploaded automatically.
    /// </summary>
    public static class AdReportService
    {
        public const string SupportEmail =
            "support@smoothbraingames.com";
        public const string SupportPageUrl =
            "https://smoothbraingames.com/#support";

        private const int MaximumRecentAds = 5;
        private const int MaximumValueLength = 160;
        private const string EmailSubject =
            "Shikaku City - Inappropriate Ad Report";

        private static readonly object Gate = new object();
        private static readonly List<AdReportEntry> RecentAds =
            new List<AdReportEntry>(MaximumRecentAds);

        public static void RecordDisplayedAd(
            string fallbackFormat,
            string fallbackPlacement,
            LevelPlayAdInfo adInfo)
        {
            AdReportEntry entry = AdReportEntry.FromLevelPlay(
                DateTime.UtcNow,
                fallbackFormat,
                fallbackPlacement,
                adInfo);

            lock (Gate)
            {
                if (RecentAds.Count > 0 &&
                    IsSameImpression(RecentAds[0], entry))
                {
                    return;
                }

                RecentAds.Insert(0, entry);
                if (RecentAds.Count > MaximumRecentAds)
                {
                    RecentAds.RemoveRange(
                        MaximumRecentAds,
                        RecentAds.Count - MaximumRecentAds);
                }
            }

            AdDiagnostics.Record(
                "report",
                $"captured displayed ad metadata: " +
                $"format={entry.Format}, placement={entry.Placement}");
        }

        public static void OpenEmailReport()
        {
            Application.OpenURL(CreateCurrentMailToUrl());
        }

        public static void OpenSupportPage()
        {
            Application.OpenURL(SupportPageUrl);
        }

        public static string CreateCurrentMailToUrl()
        {
            List<AdReportEntry> snapshot;
            lock (Gate)
                snapshot = new List<AdReportEntry>(RecentAds);

            string body = BuildReportBody(
                snapshot,
                Application.version,
                Application.platform.ToString(),
                SystemInfo.deviceModel,
                SystemInfo.operatingSystem);

            return CreateMailToUrl(body);
        }

        public static string CreateMailToUrl(string body)
        {
            string subject = UnityWebRequest.EscapeURL(EmailSubject);
            string escapedBody = UnityWebRequest.EscapeURL(
                body ?? string.Empty);
            return $"mailto:{SupportEmail}?subject={subject}&body=" +
                   escapedBody;
        }

        public static string BuildReportBody(
            IReadOnlyList<AdReportEntry> recentAds,
            string appVersion,
            string platform,
            string deviceModel,
            string operatingSystem)
        {
            var report = new StringBuilder();
            report.AppendLine(
                "Please describe what was inappropriate or misleading:");
            report.AppendLine();
            report.AppendLine();
            report.AppendLine(
                "Please attach a screenshot of the ad if possible.");
            report.AppendLine();
            report.AppendLine(
                "Recent ad details captured by Shikaku City " +
                "(no advertising ID is included):");

            if (recentAds == null || recentAds.Count == 0)
            {
                report.AppendLine(
                    "No recent displayed-ad details were available.");
            }
            else
            {
                int count = Math.Min(recentAds.Count, MaximumRecentAds);
                for (int index = 0; index < count; index++)
                {
                    AdReportEntry ad = recentAds[index];
                    report.AppendLine();
                    report.AppendLine($"Ad {index + 1}:");
                    report.AppendLine(
                        "  Displayed UTC: " +
                        ad.CapturedAtUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture));
                    report.AppendLine($"  Format: {ad.Format}");
                    report.AppendLine(
                        $"  Placement: {ad.Placement}");
                    report.AppendLine($"  Network: {ad.Network}");
                    report.AppendLine($"  Instance: {ad.Instance}");
                    report.AppendLine(
                        $"  Creative ID: {ad.CreativeId}");
                    report.AppendLine($"  Ad ID: {ad.AdId}");
                    report.AppendLine(
                        $"  Auction ID: {ad.AuctionId}");
                    report.AppendLine(
                        $"  Ad unit ID: {ad.AdUnitId}");
                }
            }

            report.AppendLine();
            report.AppendLine("App details:");
            report.AppendLine(
                $"  Version: {CleanValue(appVersion)}");
            report.AppendLine(
                $"  Platform: {CleanValue(platform)}");
            report.AppendLine(
                $"  Device: {CleanValue(deviceModel)}");
            report.AppendLine(
                $"  OS: {CleanValue(operatingSystem)}");
            report.AppendLine();
            report.AppendLine($"Support page: {SupportPageUrl}");
            return report.ToString();
        }

        internal static string CleanValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unavailable";

            string cleaned = value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();
            return cleaned.Length <= MaximumValueLength
                ? cleaned
                : cleaned.Substring(0, MaximumValueLength);
        }

        private static bool IsSameImpression(
            AdReportEntry existing,
            AdReportEntry candidate)
        {
            bool hasAuctionId =
                existing.AuctionId != "Unavailable" &&
                candidate.AuctionId != "Unavailable";
            if (hasAuctionId)
            {
                return string.Equals(
                    existing.AuctionId,
                    candidate.AuctionId,
                    StringComparison.Ordinal) &&
                    string.Equals(
                        existing.AdId,
                        candidate.AdId,
                        StringComparison.Ordinal);
            }

            return string.Equals(
                       existing.AdId,
                       candidate.AdId,
                       StringComparison.Ordinal) &&
                   existing.AdId != "Unavailable" &&
                   string.Equals(
                       existing.Format,
                       candidate.Format,
                       StringComparison.Ordinal);
        }
    }
}
