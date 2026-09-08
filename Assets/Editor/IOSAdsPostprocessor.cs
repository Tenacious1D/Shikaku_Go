#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Applies the iOS property-list requirements that belong to the
    /// production LevelPlay integration after Google has added its complete
    /// mediated-network SKAdNetwork list.
    /// </summary>
    public static class IOSAdsPostprocessor
    {
        private const string SKAdNetworkItems = "SKAdNetworkItems";
        private const string SKAdNetworkIdentifier =
            "SKAdNetworkIdentifier";

        [PostProcessBuild(300)]
        public static void ConfigureProductionAds(
            BuildTarget target,
            string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            ApplyProductionPlistSettings(plist);
            plist.WriteToFile(plistPath);

            Debug.Log(
                "Configured iOS LevelPlay attribution reporting and " +
                "SKAdNetwork support.");
        }

        public static void ApplyProductionPlistSettings(
            PlistDocument plist)
        {
            if (plist == null)
                throw new ArgumentNullException(nameof(plist));

            PlistElementDict root = plist.root;
            root.SetString(
                "NSAdvertisingAttributionReportEndpoint",
                IOSAdsReleaseConfiguration.AttributionReportEndpoint);

            PlistElementArray networkItems = GetOrCreateArray(
                root,
                SKAdNetworkItems);
            bool alreadyPresent = networkItems.values.Any(element =>
            {
                try
                {
                    PlistElementDict item = element.AsDict();
                    return item.values.TryGetValue(
                               SKAdNetworkIdentifier,
                               out PlistElement identifier) &&
                           string.Equals(
                               identifier.AsString(),
                               IOSAdsReleaseConfiguration
                                   .IronSourceSKAdNetworkId,
                               StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            });

            if (!alreadyPresent)
            {
                networkItems
                    .AddDict()
                    .SetString(
                        SKAdNetworkIdentifier,
                        IOSAdsReleaseConfiguration
                            .IronSourceSKAdNetworkId);
            }
        }

        private static PlistElementArray GetOrCreateArray(
            PlistElementDict root,
            string key)
        {
            if (!root.values.TryGetValue(key, out PlistElement existing))
                return root.CreateArray(key);

            try
            {
                return existing.AsArray();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Info.plist key '{key}' must be an array.",
                    exception);
            }
        }
    }
}
#endif
