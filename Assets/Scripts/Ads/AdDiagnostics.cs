using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Shikaku.Privacy;

namespace Shikaku.Ads
{
    public static class AdDiagnostics
    {
        private const int MaximumEntries = 250;
        private static readonly Queue<string> Entries = new Queue<string>();

        public static void Record(string category, string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string entry = $"{DateTime.UtcNow:O} [{category}] {message}";
            Entries.Enqueue(entry);
            while (Entries.Count > MaximumEntries)
                Entries.Dequeue();

            Debug.Log($"AD-DIAGNOSTICS {entry}");

            try
            {
                File.AppendAllText(LogPath, entry + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"AdDiagnostics could not write its log: {exception.Message}");
            }
#endif
        }

        public static string BuildReport()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var report = new StringBuilder();
            report.AppendLine("Shikaku Go ad diagnostics");
            report.AppendLine($"UTC: {DateTime.UtcNow:O}");
            report.AppendLine($"Version: {Application.version}");
            report.AppendLine($"Platform: {Application.platform}");
            report.AppendLine($"Device: {SystemInfo.deviceModel}");
            report.AppendLine($"OS: {SystemInfo.operatingSystem}");
            report.AppendLine($"Network: {Application.internetReachability}");
            report.AppendLine(
                $"Screen: {Screen.width}x{Screen.height}, SafeArea={Screen.safeArea}");
            report.AppendLine(
                $"Consent: completed={AdConsentManager.HasCompletedConsentFlow}, " +
                $"canRequestAds={AdConsentManager.CanRequestAds}");
            report.AppendLine(
                $"ATT: {IOSAppTrackingTransparency.CurrentStatus}");
            report.AppendLine();
            report.AppendLine(AdsManager.Instance != null
                ? AdsManager.Instance.GetDiagnosticState()
                : "AdsManager: missing");
            report.AppendLine();
            report.AppendLine("Recent events:");
            foreach (string entry in Entries)
                report.AppendLine(entry);
            report.AppendLine();
            report.AppendLine($"Persistent log: {LogPath}");
            return report.ToString();
#else
            return "Ad diagnostics are only available in development builds.";
#endif
        }

        public static void ShowPanel(VisualElement documentRoot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (documentRoot == null)
                return;

            documentRoot.Q<VisualElement>("ad-diagnostics-overlay")?.RemoveFromHierarchy();

            var overlay = new VisualElement { name = "ad-diagnostics-overlay" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.86f);
            overlay.style.paddingLeft = 24;
            overlay.style.paddingRight = 24;
            overlay.style.paddingTop = 45;
            overlay.style.paddingBottom = 45;

            var card = new VisualElement();
            card.style.flexGrow = 1;
            card.style.backgroundColor = new Color(0.12f, 0.12f, 0.11f, 1f);
            card.style.borderTopLeftRadius = 16;
            card.style.borderTopRightRadius = 16;
            card.style.borderBottomLeftRadius = 16;
            card.style.borderBottomRightRadius = 16;
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            card.style.paddingTop = 18;
            card.style.paddingBottom = 18;
            overlay.Add(card);

            var title = new Label("Ad Diagnostics (Development Build)");
            title.style.fontSize = 22;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            card.Add(title);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            var report = new Label(BuildReport());
            report.name = "ad-diagnostics-report";
            report.style.whiteSpace = WhiteSpace.Normal;
            report.style.fontSize = 13;
            report.style.color = new Color(0.9f, 0.9f, 0.86f, 1f);
            scroll.Add(report);
            card.Add(scroll);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 12;
            card.Add(actions);

            AddButton(actions, "Refresh", () => report.text = BuildReport());
            AddButton(actions, "Copy Report", () =>
            {
                GUIUtility.systemCopyBuffer = BuildReport();
                report.text = BuildReport() + "\n\nReport copied to clipboard.";
            });
            AddButton(actions, "Retry Banner", () =>
            {
                AdsManager.Instance?.DebugRetryBanner();
                report.text = BuildReport();
            });
            AddButton(actions, "Retry Rewarded", () =>
            {
                AdsManager.Instance?.DebugRetryRewarded();
                report.text = BuildReport();
            });
            AddButton(actions, "LevelPlay Test Suite", () =>
            {
                AdsManager.Instance?.DebugLaunchTestSuite();
                report.text = BuildReport();
            });
            AddButton(actions, "Close", overlay.RemoveFromHierarchy);

            documentRoot.Add(overlay);
            overlay.BringToFront();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string LogPath =>
            Path.Combine(Application.persistentDataPath, "ad-diagnostics.log");

        private static void AddButton(
            VisualElement parent,
            string text,
            Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.height = 46;
            button.style.minWidth = 125;
            button.style.marginRight = 8;
            button.style.marginBottom = 8;
            parent.Add(button);
        }
#endif
    }
}
