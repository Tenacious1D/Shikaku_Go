#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Shikaku.Store;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Shikaku.EditorTools
{
    internal static class IOSPurchasingReleaseConfiguration
    {
        internal const string BundleIdentifier =
            "com.smoothbraingames.shikakugo";
        internal const string MinimumIOSVersion = "15.0";
        internal const string MinimumPurchasingVersion = "5.4.0";

        private static readonly Dictionary<string, ExpectedProduct>
            ExpectedProducts =
                new Dictionary<string, ExpectedProduct>(
                    StringComparer.Ordinal)
                {
                    {
                        StoreProductCatalog.RemoveAdsId,
                        new ExpectedProduct(
                            ProductType.NonConsumable,
                            0,
                            removesAds: true,
                            grantsBonusHints: false,
                            restorable: true)
                    },
                    {
                        StoreProductCatalog.RemoveAdsBundleId,
                        new ExpectedProduct(
                            ProductType.NonConsumable,
                            30,
                            removesAds: true,
                            grantsBonusHints: true,
                            restorable: true)
                    },
                    {
                        StoreProductCatalog.Hints10Id,
                        new ExpectedProduct(
                            ProductType.Consumable,
                            10,
                            removesAds: false,
                            grantsBonusHints: false,
                            restorable: false)
                    },
                    {
                        StoreProductCatalog.Hints25Id,
                        new ExpectedProduct(
                            ProductType.Consumable,
                            25,
                            removesAds: false,
                            grantsBonusHints: false,
                            restorable: false)
                    },
                    {
                        StoreProductCatalog.Hints60Id,
                        new ExpectedProduct(
                            ProductType.Consumable,
                            60,
                            removesAds: false,
                            grantsBonusHints: false,
                            restorable: false)
                    }
                };

        internal static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();
            ValidatePlayerSettings(errors);
            ValidatePackageVersion(errors);
            ValidateCatalog(errors);
            ValidatePlayerFacingControls(errors);
            ValidatePrivacyManifest(errors);
            return errors;
        }

        private static void ValidatePlayerSettings(
            ICollection<string> errors)
        {
            string identifier = PlayerSettings.GetApplicationIdentifier(
                NamedBuildTarget.iOS);
            if (!string.Equals(
                    identifier,
                    BundleIdentifier,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"The iOS bundle identifier must be " +
                    $"'{BundleIdentifier}' (currently '{identifier}').");
            }

            if (!Version.TryParse(
                    PlayerSettings.iOS.targetOSVersionString,
                    out Version currentMinimum) ||
                !Version.TryParse(
                    MinimumIOSVersion,
                    out Version requiredMinimum) ||
                currentMinimum < requiredMinimum)
            {
                errors.Add(
                    $"The minimum iOS version must be " +
                    $"{MinimumIOSVersion} or newer for StoreKit 2.");
            }
        }

        private static void ValidatePackageVersion(
            ICollection<string> errors)
        {
            string manifestPath = ProjectPath("Packages/manifest.json");
            if (!File.Exists(manifestPath))
            {
                errors.Add("Packages/manifest.json is missing.");
                return;
            }

            string contents = File.ReadAllText(manifestPath);
            Match match = Regex.Match(
                contents,
                "\\\"com\\.unity\\.purchasing\\\"\\s*:\\s*" +
                "\\\"(?<version>[0-9]+\\.[0-9]+\\.[0-9]+)\\\"");

            if (!match.Success)
            {
                errors.Add(
                    "The com.unity.purchasing package is not pinned to a " +
                    "semantic version in Packages/manifest.json.");
                return;
            }

            Version current = new Version(
                match.Groups["version"].Value);
            Version minimum = new Version(MinimumPurchasingVersion);
            if (current < minimum)
            {
                errors.Add(
                    $"Unity IAP {MinimumPurchasingVersion}+ is required " +
                    $"(currently {current}).");
            }
        }

        private static void ValidateCatalog(
            ICollection<string> errors)
        {
            if (StoreProductCatalog.Products.Count !=
                ExpectedProducts.Count)
            {
                errors.Add(
                    $"The runtime catalog must contain exactly " +
                    $"{ExpectedProducts.Count} products.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (StoreProductSpec product in
                     StoreProductCatalog.Products)
            {
                if (!seen.Add(product.Id))
                {
                    errors.Add(
                        $"Duplicate store product ID: {product.Id}.");
                    continue;
                }

                if (!ExpectedProducts.TryGetValue(
                        product.Id,
                        out ExpectedProduct expected))
                {
                    errors.Add(
                        $"Unexpected store product ID: {product.Id}.");
                    continue;
                }

                if (product.Type != expected.Type ||
                    product.HintAmount != expected.HintAmount ||
                    product.RemovesAds != expected.RemovesAds ||
                    product.GrantsBonusHints !=
                    expected.GrantsBonusHints ||
                    product.Restorable != expected.Restorable)
                {
                    errors.Add(
                        $"Runtime configuration for '{product.Id}' does " +
                        "not match the approved App Store catalog.");
                }
            }

            foreach (string expectedId in ExpectedProducts.Keys)
            {
                if (!seen.Contains(expectedId))
                    errors.Add($"Store product '{expectedId}' is missing.");
            }
        }

        private static void ValidatePlayerFacingControls(
            ICollection<string> errors)
        {
            string uxmlPath = ProjectPath(
                "Assets/UI/Screens/Home/HomeScreen.uxml");
            if (!File.Exists(uxmlPath))
            {
                errors.Add("The home/shop UXML file is missing.");
                return;
            }

            string contents = File.ReadAllText(uxmlPath);
            RequireText(
                errors,
                contents,
                "settings-restore-purchases-button",
                "The visible Restore Purchases button is missing.");
            RequireText(
                errors,
                contents,
                "settings-restore-purchases-status",
                "The Restore Purchases status label is missing.");
            RequireText(
                errors,
                contents,
                "shop-purchase-status",
                "The shop purchase status label is missing.");
        }

        private static void ValidatePrivacyManifest(
            ICollection<string> errors)
        {
            string manifestPath = ProjectPath(
                "Assets/Editor/IOSPrivacy/PrivacyInfo.xcprivacy");
            if (!File.Exists(manifestPath))
            {
                errors.Add("The app privacy manifest is missing.");
                return;
            }

            RequireText(
                errors,
                File.ReadAllText(manifestPath),
                "NSPrivacyCollectedDataTypePurchaseHistory",
                "The privacy manifest must declare purchase history.");
        }

        private static void RequireText(
            ICollection<string> errors,
            string contents,
            string requiredText,
            string error)
        {
            if (contents.IndexOf(
                    requiredText,
                    StringComparison.Ordinal) < 0)
            {
                errors.Add(error);
            }
        }

        private static string ProjectPath(string relativePath)
        {
            string projectDirectory = Directory.GetParent(
                Application.dataPath)?.FullName;
            return Path.GetFullPath(
                Path.Combine(projectDirectory ?? string.Empty, relativePath));
        }

        private readonly struct ExpectedProduct
        {
            internal ExpectedProduct(
                ProductType type,
                int hintAmount,
                bool removesAds,
                bool grantsBonusHints,
                bool restorable)
            {
                Type = type;
                HintAmount = hintAmount;
                RemovesAds = removesAds;
                GrantsBonusHints = grantsBonusHints;
                Restorable = restorable;
            }

            internal ProductType Type { get; }
            internal int HintAmount { get; }
            internal bool RemovesAds { get; }
            internal bool GrantsBonusHints { get; }
            internal bool Restorable { get; }
        }
    }

    public sealed class IOSPurchasingBuildValidator :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -840;

        [MenuItem(
            "Tools/Shikaku Go/iOS/Validate In-App Purchases")]
        public static void ValidateFromMenu()
        {
            IReadOnlyList<string> errors =
                IOSPurchasingReleaseConfiguration.CollectErrors();
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "iOS In-App Purchases",
                    "The iOS purchase catalog and player-facing controls " +
                    "are valid.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "iOS In-App Purchases Need Attention",
                "- " + string.Join("\n- ", errors),
                "OK");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            IReadOnlyList<string> errors =
                IOSPurchasingReleaseConfiguration.CollectErrors();
            if (errors.Count == 0)
                return;

            throw new BuildFailedException(
                "iOS in-app purchase validation failed:\n- " +
                string.Join("\n- ", errors));
        }
    }
}
#endif

#if UNITY_EDITOR && UNITY_IOS
namespace Shikaku.EditorTools
{
    using System.IO;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEditor.iOS.Xcode;

    /// <summary>
    /// Runs after Unity IAP's postprocessor and fails the export if StoreKit
    /// or the In-App Purchase Xcode capability was not installed.
    /// </summary>
    public sealed class IOSPurchasingPostprocessor :
        IPostprocessBuildWithReport
    {
        public int callbackOrder => 1100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            string projectPath = PBXProject.GetPBXProjectPath(
                report.summary.outputPath);
            if (!File.Exists(projectPath))
            {
                throw new BuildFailedException(
                    "The exported Xcode project is missing.");
            }

            string project = File.ReadAllText(projectPath);
            if (!project.Contains("StoreKit.framework"))
            {
                throw new BuildFailedException(
                    "Unity IAP did not add StoreKit.framework to the " +
                    "exported Xcode project.");
            }

            if (!project.Contains("com.apple.InAppPurchase"))
            {
                throw new BuildFailedException(
                    "Unity IAP did not add the In-App Purchase capability " +
                    "to the exported Xcode project.");
            }
        }
    }
}
#endif
