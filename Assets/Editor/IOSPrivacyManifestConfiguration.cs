#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Defines and validates Shikaku City's app-owned privacy manifest.
    /// Third-party SDK manifests remain responsible for SDK-specific APIs,
    /// collection, and tracking domains.
    /// </summary>
    public static class IOSPrivacyManifestConfiguration
    {
        public const string ManifestAssetPath =
            "Assets/Editor/IOSPrivacy/PrivacyInfo.xcprivacy";
        public const string ExportFileName = "PrivacyInfo.xcprivacy";

        public const string UserDefaultsCategory =
            "NSPrivacyAccessedAPICategoryUserDefaults";
        public const string UserDefaultsReason = "CA92.1";
        public const string FileTimestampCategory =
            "NSPrivacyAccessedAPICategoryFileTimestamp";
        public const string FileTimestampReason = "C617.1";
        public const string DiskSpaceCategory =
            "NSPrivacyAccessedAPICategoryDiskSpace";
        public const string DiskSpaceReason = "E174.1";

        private const string TrackingKey = "NSPrivacyTracking";
        private const string TrackingDomainsKey =
            "NSPrivacyTrackingDomains";
        private const string CollectedDataTypesKey =
            "NSPrivacyCollectedDataTypes";
        private const string CollectedDataTypeKey =
            "NSPrivacyCollectedDataType";
        private const string CollectedDataLinkedKey =
            "NSPrivacyCollectedDataTypeLinked";
        private const string CollectedDataTrackingKey =
            "NSPrivacyCollectedDataTypeTracking";
        private const string CollectedDataPurposesKey =
            "NSPrivacyCollectedDataTypePurposes";
        private const string AccessedApiTypesKey =
            "NSPrivacyAccessedAPITypes";
        private const string AccessedApiTypeKey =
            "NSPrivacyAccessedAPIType";
        private const string AccessedApiReasonsKey =
            "NSPrivacyAccessedAPITypeReasons";

        private static readonly string[] RequiredSdkManifestPaths =
        {
            "Packages/com.unity.purchasing/Plugins/" +
            "UnityPurchasing/iOS/PrivacyInfo.xcprivacy",
            "Packages/com.unity.services.analytics/Runtime/" +
            "PrivacyInfo.xcprivacy",
            "Packages/com.unity.services.core/Runtime/" +
            "PrivacyInfo.xcprivacy",
            "Packages/com.unity.services.levelplay/Runtime/" +
            "PrivacyInfo.xcprivacy"
        };

        private static readonly DataExpectation[] RequiredData =
        {
            new DataExpectation(
                "NSPrivacyCollectedDataTypeUserID",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAnalytics"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypeDeviceID",
                true,
                true,
                "NSPrivacyCollectedDataTypePurposeAnalytics",
                "NSPrivacyCollectedDataTypePurposeThirdPartyAdvertising"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypeCoarseLocation",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAnalytics",
                "NSPrivacyCollectedDataTypePurposeThirdPartyAdvertising"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypePurchaseHistory",
                true,
                true,
                "NSPrivacyCollectedDataTypePurposeAnalytics",
                "NSPrivacyCollectedDataTypePurposeAppFunctionality"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypeProductInteraction",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAnalytics"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypeAdvertisingData",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAnalytics",
                "NSPrivacyCollectedDataTypePurposeThirdPartyAdvertising"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypeCrashData",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAppFunctionality"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypePerformanceData",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAnalytics"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypeOtherDiagnosticData",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAnalytics",
                "NSPrivacyCollectedDataTypePurposeAppFunctionality"),
            new DataExpectation(
                "NSPrivacyCollectedDataTypeOtherDataTypes",
                true,
                false,
                "NSPrivacyCollectedDataTypePurposeAnalytics")
        };

        private static readonly ApiExpectation[] RequiredApis =
        {
            new ApiExpectation(UserDefaultsCategory, UserDefaultsReason),
            new ApiExpectation(FileTimestampCategory, FileTimestampReason),
            new ApiExpectation(DiskSpaceCategory, DiskSpaceReason)
        };

        public static string AbsoluteManifestPath =>
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    ManifestAssetPath));

        public static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();
            if (!File.Exists(AbsoluteManifestPath))
            {
                errors.Add(
                    $"Missing app privacy manifest at {ManifestAssetPath}.");
                return errors;
            }

            try
            {
                var manifest = new PlistDocument();
                manifest.ReadFromFile(AbsoluteManifestPath);
                ValidateDocument(manifest, errors);
            }
            catch (Exception exception)
            {
                errors.Add(
                    "The app privacy manifest is not a valid property " +
                    $"list: {exception.Message}");
            }

            ValidateSdkManifests(errors);
            return errors;
        }

        internal static void ValidateDocument(
            PlistDocument manifest,
            ICollection<string> errors)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            ValidateTrackingConfiguration(manifest.root, errors);

            PlistElementArray dataTypes = RequireArray(
                manifest.root,
                CollectedDataTypesKey,
                errors);
            if (dataTypes != null)
            {
                foreach (DataExpectation expectation in RequiredData)
                    ValidateDataType(dataTypes, expectation, errors);
            }

            PlistElementArray apiTypes = RequireArray(
                manifest.root,
                AccessedApiTypesKey,
                errors);
            if (apiTypes != null)
            {
                foreach (ApiExpectation expectation in RequiredApis)
                {
                    ValidateRequiredReason(
                        apiTypes,
                        expectation.Type,
                        expectation.Reason,
                        errors);
                }
            }
        }

        private static void ValidateSdkManifests(
            ICollection<string> errors)
        {
            foreach (string path in RequiredSdkManifestPaths)
            {
                if (!string.IsNullOrEmpty(
                        AssetDatabase.AssetPathToGUID(path)))
                {
                    continue;
                }

                errors.Add(
                    "The installed SDK privacy manifest could not be " +
                    $"found at {path}.");
            }
        }

        private static void ValidateDataType(
            PlistElementArray dataTypes,
            DataExpectation expectation,
            ICollection<string> errors)
        {
            PlistElementDict match = FindDictionary(
                dataTypes,
                CollectedDataTypeKey,
                expectation.Type);
            if (match == null)
            {
                errors.Add(
                    $"Privacy manifest is missing {expectation.Type}.");
                return;
            }

            ValidateBoolean(
                match,
                CollectedDataLinkedKey,
                expectation.Linked,
                errors,
                expectation.Type);
            ValidateBoolean(
                match,
                CollectedDataTrackingKey,
                expectation.Tracking,
                errors,
                expectation.Type);

            PlistElementArray purposes = RequireArray(
                match,
                CollectedDataPurposesKey,
                errors,
                expectation.Type);
            if (purposes == null)
                return;

            HashSet<string> actualPurposes = ReadStrings(purposes);
            foreach (string purpose in expectation.Purposes)
            {
                if (!actualPurposes.Contains(purpose))
                {
                    errors.Add(
                        $"{expectation.Type} is missing purpose " +
                        $"{purpose}.");
                }
            }
        }

        private static void ValidateRequiredReason(
            PlistElementArray apiTypes,
            string apiType,
            string reason,
            ICollection<string> errors)
        {
            PlistElementDict match = FindDictionary(
                apiTypes,
                AccessedApiTypeKey,
                apiType);
            if (match == null)
            {
                errors.Add(
                    $"Privacy manifest is missing required API {apiType}.");
                return;
            }

            PlistElementArray reasons = RequireArray(
                match,
                AccessedApiReasonsKey,
                errors,
                apiType);
            if (reasons == null || !ReadStrings(reasons).Contains(reason))
            {
                errors.Add(
                    $"{apiType} must declare approved reason {reason}.");
            }
        }

        private static void ValidateTrackingConfiguration(
            PlistElementDict root,
            ICollection<string> errors)
        {
            bool hasTracking = root.values.TryGetValue(
                TrackingKey,
                out PlistElement trackingElement);
            bool tracking = false;

            if (hasTracking)
            {
                try
                {
                    tracking = trackingElement.AsBoolean();
                }
                catch
                {
                    errors.Add(
                        $"Privacy manifest requires {TrackingKey} " +
                        "to be Boolean.");
                    return;
                }
            }

            bool hasDomains = root.values.TryGetValue(
                TrackingDomainsKey,
                out PlistElement domainsElement);
            PlistElementArray domains = null;

            if (hasDomains)
            {
                try
                {
                    domains = domainsElement.AsArray();
                }
                catch
                {
                    errors.Add(
                        $"Privacy manifest requires {TrackingDomainsKey} " +
                        "to be an array.");
                    return;
                }
            }

            if (tracking)
            {
                if (domains == null || domains.values.Count == 0)
                {
                    errors.Add(
                        "Privacy manifest requires a non-empty " +
                        $"{TrackingDomainsKey} array when " +
                        $"{TrackingKey}=true.");
                    return;
                }

                ValidateTrackingDomains(domains, errors);
                return;
            }

            if (hasDomains)
            {
                errors.Add(
                    $"Privacy manifest must omit {TrackingDomainsKey} " +
                    $"when {TrackingKey} is false or omitted.");
            }
        }

        private static void ValidateTrackingDomains(
            PlistElementArray domains,
            ICollection<string> errors)
        {
            foreach (PlistElement element in domains.values)
            {
                string domain;
                try
                {
                    domain = element.AsString();
                }
                catch
                {
                    errors.Add(
                        $"Privacy manifest {TrackingDomainsKey} entries " +
                        "must be strings.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(domain) ||
                    !string.Equals(
                        domain,
                        domain.Trim(),
                        StringComparison.Ordinal) ||
                    Uri.CheckHostName(domain) == UriHostNameType.Unknown)
                {
                    errors.Add(
                        $"Privacy manifest tracking domain '{domain}' " +
                        "must be a valid domain without a scheme, path, " +
                        "query, whitespace, or trailing slash.");
                }
            }
        }

        private static PlistElementDict FindDictionary(
            PlistElementArray array,
            string key,
            string expectedValue)
        {
            foreach (PlistElement element in array.values)
            {
                PlistElementDict dictionary;
                try
                {
                    dictionary = element.AsDict();
                }
                catch
                {
                    continue;
                }

                if (!dictionary.values.TryGetValue(
                        key,
                        out PlistElement value))
                {
                    continue;
                }

                try
                {
                    if (string.Equals(
                            value.AsString(),
                            expectedValue,
                            StringComparison.Ordinal))
                    {
                        return dictionary;
                    }
                }
                catch
                {
                    // A malformed entry is reported as a missing entry.
                }
            }

            return null;
        }

        private static HashSet<string> ReadStrings(
            PlistElementArray array)
        {
            return new HashSet<string>(
                array.values.Select(value =>
                {
                    try
                    {
                        return value.AsString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }),
                StringComparer.Ordinal);
        }

        private static PlistElementArray RequireArray(
            PlistElementDict dictionary,
            string key,
            ICollection<string> errors,
            string context = null)
        {
            if (!dictionary.values.TryGetValue(
                    key,
                    out PlistElement value))
            {
                errors.Add(FormatContext(context, $"is missing {key}."));
                return null;
            }

            try
            {
                return value.AsArray();
            }
            catch
            {
                errors.Add(
                    FormatContext(context, $"requires {key} to be an array."));
                return null;
            }
        }

        private static void ValidateBoolean(
            PlistElementDict dictionary,
            string key,
            bool expected,
            ICollection<string> errors,
            string context = null)
        {
            if (!dictionary.values.TryGetValue(
                    key,
                    out PlistElement value))
            {
                errors.Add(FormatContext(context, $"is missing {key}."));
                return;
            }

            try
            {
                if (value.AsBoolean() != expected)
                {
                    errors.Add(
                        FormatContext(
                            context,
                            $"requires {key}={expected.ToString().ToLowerInvariant()}."));
                }
            }
            catch
            {
                errors.Add(
                    FormatContext(context, $"requires {key} to be Boolean."));
            }
        }

        private static string FormatContext(
            string context,
            string message)
        {
            return string.IsNullOrEmpty(context)
                ? $"Privacy manifest {message}"
                : $"{context} {message}";
        }

        private sealed class DataExpectation
        {
            public string Type { get; }
            public bool Linked { get; }
            public bool Tracking { get; }
            public IReadOnlyList<string> Purposes { get; }

            public DataExpectation(
                string type,
                bool linked,
                bool tracking,
                params string[] purposes)
            {
                Type = type;
                Linked = linked;
                Tracking = tracking;
                Purposes = purposes;
            }
        }

        private sealed class ApiExpectation
        {
            public string Type { get; }
            public string Reason { get; }

            public ApiExpectation(string type, string reason)
            {
                Type = type;
                Reason = reason;
            }
        }
    }
}
#endif
