#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shikaku.EditorTools
{
    public static class IOSATTReleaseConfiguration
    {
        public const string GoogleMobileAdsSettingsPath =
            "Assets/GoogleMobileAds/Resources/" +
            "GoogleMobileAdsSettings.asset";
        public const string NativeBridgePath =
            "Assets/Plugins/iOS/" +
            "ShikakuAppTrackingTransparency.mm";
        public const string IOSAdMobAppId =
            "REPLACE_WITH_SHIKAKU_IOS_ADMOB_APP_ID";
        public const string TrackingUsageDescription =
            "Allowing tracking helps us show more relevant ads and " +
            "measure ad performance. You can continue playing if you decline.";

        public static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();
            ScriptableObject settings =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    GoogleMobileAdsSettingsPath);

            if (settings == null)
            {
                errors.Add(
                    "Google Mobile Ads settings are missing for iOS.");
            }
            else
            {
                var serializedSettings =
                    new SerializedObject(settings);
                AddMismatch(
                    errors,
                    serializedSettings,
                    "adMobIOSAppId",
                    "iOS AdMob app ID",
                    IOSAdMobAppId);
                AddMismatch(
                    errors,
                    serializedSettings,
                    "userTrackingUsageDescription",
                    "ATT purpose description",
                    TrackingUsageDescription);
            }

            string projectRoot =
                Directory.GetParent(Application.dataPath).FullName;
            string nativeBridge =
                Path.Combine(projectRoot, NativeBridgePath);
            if (!File.Exists(nativeBridge))
            {
                errors.Add(
                    $"ATT native bridge is missing at {NativeBridgePath}.");
            }

            return errors;
        }

        private static void AddMismatch(
            ICollection<string> errors,
            SerializedObject settings,
            string propertyName,
            string label,
            string expected)
        {
            SerializedProperty property =
                settings.FindProperty(propertyName);

            if (property == null)
            {
                errors.Add(
                    $"{label} setting '{propertyName}' was not found.");
                return;
            }

            string actual = property.stringValue?.Trim();
            if (actual != expected)
            {
                errors.Add(
                    $"{label} must be '{expected}' " +
                    $"(currently '{actual}').");
            }
        }
    }
}
#endif
