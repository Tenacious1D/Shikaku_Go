#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Consolidates every Windows-verifiable iOS release requirement. It is
    /// both a menu audit and the final pre-export build gate.
    /// </summary>
    public sealed class IOSFinalWindowsReleaseAudit :
        IPreprocessBuildWithReport
    {
        private const string HomeScene =
            "Assets/Resources/Scenes/HomeUI.unity";
        private const string GameplayScene =
            "Assets/Resources/Scenes/Gameplay.unity";
        private const string ExpectedCompanyName = "Smooth Brain Games";
        private const string ExpectedProductName = "Shikaku Go";
        private const string AppleGameCenterDefine =
            "SHIKAKU_APPLE_GAME_CENTER";
        private const string LevelPlayDefine =
            "LEVELPLAY_DEPENDENCIES_INSTALLED";
        private const string GooglePlayGamesDefine =
            "SHIKAKU_GOOGLE_PLAY_GAMES";

        public int callbackOrder => -700;

        [MenuItem(
            "Tools/Shikaku Go/iOS/Run Final Windows Release Audit")]
        public static void RunFromMenu()
        {
            IReadOnlyList<string> errors = CollectErrors();
            string report = BuildReportText(errors);
            Debug.Log(report);

            EditorUtility.DisplayDialog(
                errors.Count == 0
                    ? "Final Windows iOS Audit Passed"
                    : "Final Windows iOS Audit Needs Attention",
                report,
                "OK");

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Final Windows iOS release audit failed:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        public static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();

            AddErrors(
                errors,
                "Release foundation",
                IOSReleaseConfigurator.CollectErrors());
            AddErrors(
                errors,
                "Production ads",
                IOSAdsReleaseConfiguration.CollectErrors());
            AddErrors(
                errors,
                "Privacy manifest",
                IOSPrivacyManifestConfiguration.CollectErrors());
            AddErrors(
                errors,
                "In-app purchases",
                IOSPurchasingReleaseConfiguration.CollectErrors());
            AddErrors(
                errors,
                "App icon",
                IOSAppIconConfiguration.CollectErrors());

            ValidateProjectIdentity(errors);
            ValidateBuildScenes(errors);
            ValidatePlatformDefines(errors);
            ValidateReleaseMode(errors);
            ValidateStaticReleaseAssets(errors);

            return errors
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            IReadOnlyList<string> errors = CollectErrors();
            if (errors.Count == 0)
                return;

            throw new BuildFailedException(
                "Final Windows iOS release audit failed:\n- " +
                string.Join("\n- ", errors));
        }

        private static void ValidateProjectIdentity(
            ICollection<string> errors)
        {
            if (!string.Equals(
                    PlayerSettings.companyName,
                    ExpectedCompanyName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Project identity: company name must be " +
                    $"'{ExpectedCompanyName}'.");
            }

            if (!string.Equals(
                    PlayerSettings.productName,
                    ExpectedProductName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Project identity: product name must be " +
                    $"'{ExpectedProductName}'.");
            }

            if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.DeviceSDK)
            {
                errors.Add(
                    "Project identity: the final iOS target must use the " +
                    "Device SDK, not the Simulator SDK.");
            }

        }

        private static void ValidateBuildScenes(
            ICollection<string> errors)
        {
            EditorBuildSettingsScene[] enabled =
                EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .ToArray();

            string[] expected = { HomeScene, GameplayScene };
            if (enabled.Length != expected.Length)
            {
                errors.Add(
                    "Build scenes: exactly HomeUI and Gameplay must be " +
                    "enabled for release.");
            }

            int sharedCount = Math.Min(enabled.Length, expected.Length);
            for (int index = 0; index < sharedCount; index++)
            {
                if (!string.Equals(
                        enabled[index].path,
                        expected[index],
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Build scenes: scene {index} must be " +
                        $"'{expected[index]}' (currently " +
                        $"'{enabled[index].path}').");
                }
            }

            foreach (string scene in expected)
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(scene)))
                    errors.Add($"Build scenes: missing '{scene}'.");
            }
        }

        private static void ValidatePlatformDefines(
            ICollection<string> errors)
        {
            PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.iOS,
                out string[] defines);
            var set = new HashSet<string>(
                defines,
                StringComparer.Ordinal);

            if (!set.Contains(AppleGameCenterDefine))
            {
                errors.Add(
                    $"Platform defines: iOS requires " +
                    $"{AppleGameCenterDefine}.");
            }

            if (!set.Contains(LevelPlayDefine))
            {
                errors.Add(
                    $"Platform defines: iOS mediation requires " +
                    $"{LevelPlayDefine}.");
            }

            if (set.Contains(GooglePlayGamesDefine))
            {
                errors.Add(
                    $"Platform defines: Android-only " +
                    $"{GooglePlayGamesDefine} cannot be enabled for iOS.");
            }
        }

        private static void ValidateReleaseMode(
            ICollection<string> errors)
        {
            if (EditorUserBuildSettings.development)
            {
                errors.Add(
                    "Release mode: Development Build must be off for the " +
                    "App Store archive.");
            }

            if (EditorUserBuildSettings.connectProfiler)
            {
                errors.Add(
                    "Release mode: Autoconnect Profiler must be off for " +
                    "the App Store archive.");
            }

            if (EditorUserBuildSettings.allowDebugging)
            {
                errors.Add(
                    "Release mode: Script Debugging must be off for the " +
                    "App Store archive.");
            }
        }

        private static void ValidateStaticReleaseAssets(
            ICollection<string> errors)
        {
            RequireAsset(
                errors,
                FirebaseIOSConfiguration.AssetPath,
                "Firebase Apple configuration");
            RequireAsset(
                errors,
                IOSATTReleaseConfiguration.NativeBridgePath,
                "ATT native bridge");
            RequireAsset(
                errors,
                IOSPrivacyManifestConfiguration.ManifestAssetPath,
                "app privacy manifest");

            RequireSourceText(
                errors,
                "Assets/Scripts/Store/StoreRatingService.cs",
                "REPLACE_WITH_SHIKAKU_APPLE_APP_ID",
                "App Store numeric Apple ID");
            RequireSourceText(
                errors,
                "Assets/Scripts/Ads/AdReportService.cs",
                "support@smoothbraingames.com",
                "support email");
            RequireSourceText(
                errors,
                "Assets/Scripts/Privacy/LegalLinks.cs",
                "https://smoothbraingames.com/privacy",
                "privacy policy URL");
            RequireSourceText(
                errors,
                "Assets/Scripts/Privacy/LegalLinks.cs",
                "https://smoothbraingames.com/terms",
                "terms URL");
        }

        private static void RequireAsset(
            ICollection<string> errors,
            string assetPath,
            string label)
        {
            if (string.IsNullOrEmpty(
                    AssetDatabase.AssetPathToGUID(assetPath)))
            {
                errors.Add(
                    $"Static assets: {label} is missing at " +
                    $"'{assetPath}'.");
            }
        }

        private static void RequireSourceText(
            ICollection<string> errors,
            string assetPath,
            string requiredText,
            string label)
        {
            string projectRoot = Directory.GetParent(
                Application.dataPath)?.FullName ?? string.Empty;
            string absolutePath = Path.GetFullPath(
                Path.Combine(projectRoot, assetPath));

            if (!File.Exists(absolutePath))
            {
                errors.Add(
                    $"Static assets: source file '{assetPath}' is missing.");
                return;
            }

            string source = File.ReadAllText(absolutePath);
            if (source.IndexOf(
                    requiredText,
                    StringComparison.Ordinal) < 0)
            {
                errors.Add(
                    $"Static assets: {label} is missing or incorrect in " +
                    $"'{assetPath}'.");
            }
        }

        private static void AddErrors(
            ICollection<string> destination,
            string category,
            IEnumerable<string> source)
        {
            foreach (string error in source)
                destination.Add($"{category}: {error}");
        }

        private static string BuildReportText(
            IReadOnlyList<string> errors)
        {
            if (errors.Count > 0)
            {
                return "The Windows-verifiable iOS release audit found " +
                       $"{errors.Count} problem(s):\n\n- " +
                       string.Join("\n- ", errors);
            }

            return "All Windows-verifiable iOS release checks passed. " +
                   "The remaining work requires Xcode, signed devices, " +
                   "Apple dashboards, Sandbox/TestFlight, and an archive " +
                   "validation on the Mac.";
        }
    }
}
#endif
