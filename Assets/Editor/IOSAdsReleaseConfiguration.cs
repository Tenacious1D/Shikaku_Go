#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Owns and validates the production iOS LevelPlay configuration. The
    /// runtime values remain serialized on AdsManager so Android and iOS can
    /// continue to share the same component without sharing identifiers.
    /// </summary>
    public static class IOSAdsReleaseConfiguration
    {
        public const string HomeScenePath =
            "Assets/Resources/Scenes/HomeUI.unity";

        public const string IOSAppKey = "27cbd222d";
        public const string IOSRewardedAdUnitId = "nim37ahd2q6zh5nx";
        public const string IOSBannerAdUnitId = "fexc8v06pu3xv7n7";

        public const string IronSourceSKAdNetworkId =
            "su67r6k2v3.skadnetwork";
        public const string AttributionReportEndpoint =
            "https://postbacks-is.com/";

        private const string LevelPlaySettingsPath =
            "Assets/LevelPlay/Resources/LevelPlayMediationSettings.asset";
        private const string GoogleSKAdNetworkPath =
            "Assets/GoogleMobileAds/Editor/" +
            "GoogleMobileAdsSKAdNetworkItems.xml";

        private static readonly string[] IOSDependencyFiles =
        {
            "Assets/LevelPlay/Editor/IronSourceSDKDependencies.xml",
            "Assets/LevelPlay/Editor/ISAdMobAdapterDependencies.xml",
            "Assets/LevelPlay/Editor/ISUnityAdsAdapterDependencies.xml"
        };

        private static readonly string[] RequiredIOSPods =
        {
            "IronSourceSDK",
            "IronSourceAdMobAdapter",
            "IronSourceUnityAdsAdapter"
        };

        [MenuItem(
            "Tools/Shikaku Go/iOS/Apply Production Ad Configuration")]
        public static void ApplyProductionSettings()
        {
            bool openedForEdit = false;
            Scene scene = GetOrOpenHomeScene(ref openedForEdit);

            try
            {
                List<Component> managers = FindAdsManagers(scene);
                if (managers.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected exactly one AdsManager in {HomeScenePath}, " +
                        $"but found {managers.Count}.");
                }

                var serializedManager = new SerializedObject(managers[0]);
                SetString(
                    serializedManager,
                    "iosAppKey",
                    IOSAppKey);
                SetString(
                    serializedManager,
                    "iosRewardedAdUnitId",
                    IOSRewardedAdUnitId);
                SetString(
                    serializedManager,
                    "iosBannerAdUnitId",
                    IOSBannerAdUnitId);
                SetString(
                    serializedManager,
                    "hintPlacementName",
                    "hint");
                SetString(
                    serializedManager,
                    "bannerPlacementName",
                    "bottom_banner");
                SetBool(serializedManager, "enableBannerAds", true);
                SetBool(
                    serializedManager,
                    "disableAllAdsForTesting",
                    false);
                SetBool(
                    serializedManager,
                    "enableIntegrationTestSuite",
                    false);
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"Could not save {HomeScenePath}.");
                }
            }
            finally
            {
                if (openedForEdit && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.SaveAssets();

            IReadOnlyList<string> errors = CollectErrors();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "The production iOS ad configuration could not be " +
                    "applied:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log(
                "Applied production iOS LevelPlay app key plus rewarded and " +
                "banner ad unit IDs. Test mode remains " +
                "disabled and LevelPlay remains under manual initialization.");
        }

        public static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();

            ValidateIdentifierSyntax(errors);
            ValidateBuildScene(errors);
            ValidateSceneConfiguration(errors);
            ValidateManualInitialization(errors);
            ValidateDependencies(errors);
            ValidateSKAdNetworkSource(errors);

            return errors;
        }

        public static bool IsValidAppKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Regex.IsMatch(value, "^[a-z0-9]{9}$");
        }

        public static bool IsValidAdUnitId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Regex.IsMatch(value, "^[a-z0-9]{16}$");
        }

        private static void ValidateIdentifierSyntax(
            ICollection<string> errors)
        {
            if (!IsValidAppKey(IOSAppKey))
            {
                errors.Add(
                    "The production iOS LevelPlay app key has an invalid " +
                    "format.");
            }

            var adUnitIds = new[]
            {
                IOSRewardedAdUnitId,
                IOSBannerAdUnitId
            };

            if (adUnitIds.Any(id => !IsValidAdUnitId(id)))
            {
                errors.Add(
                    "One or more production iOS LevelPlay ad unit IDs have " +
                    "an invalid format.");
            }

            if (adUnitIds.Distinct(StringComparer.Ordinal).Count() !=
                adUnitIds.Length)
            {
                errors.Add(
                    "The rewarded and banner iOS ad unit " +
                    "IDs must be different.");
            }
        }

        private static void ValidateBuildScene(ICollection<string> errors)
        {
            bool enabled = EditorBuildSettings.scenes.Any(
                scene => scene.enabled &&
                         string.Equals(
                             scene.path,
                             HomeScenePath,
                             StringComparison.Ordinal));

            if (!enabled)
            {
                errors.Add(
                    $"The ad-owning scene {HomeScenePath} must be enabled " +
                    "in Build Settings.");
            }
        }

        private static void ValidateSceneConfiguration(
            ICollection<string> errors)
        {
            bool openedForInspection = false;
            Scene scene = default;

            try
            {
                scene = GetOrOpenHomeScene(ref openedForInspection);
                List<Component> managers = FindAdsManagers(scene);
                if (managers.Count != 1)
                {
                    errors.Add(
                        $"Expected exactly one AdsManager in " +
                        $"{HomeScenePath}, but found {managers.Count}.");
                    return;
                }

                var manager = new SerializedObject(managers[0]);
                AddStringMismatch(
                    errors,
                    manager,
                    "iosAppKey",
                    "iOS LevelPlay app key",
                    IOSAppKey);
                AddStringMismatch(
                    errors,
                    manager,
                    "iosRewardedAdUnitId",
                    "iOS rewarded ad unit ID",
                    IOSRewardedAdUnitId);
                AddStringMismatch(
                    errors,
                    manager,
                    "iosBannerAdUnitId",
                    "iOS banner ad unit ID",
                    IOSBannerAdUnitId);
                AddStringMismatch(
                    errors,
                    manager,
                    "hintPlacementName",
                    "rewarded placement",
                    "hint");
                AddStringMismatch(
                    errors,
                    manager,
                    "bannerPlacementName",
                    "banner placement",
                    "bottom_banner");
                RequireBool(
                    errors,
                    manager,
                    "enableBannerAds",
                    true,
                    "Production iOS banner ads must be enabled.");
                RequireBool(
                    errors,
                    manager,
                    "disableAllAdsForTesting",
                    false,
                    "The production scene cannot disable all ads for " +
                    "testing.");
                RequireBool(
                    errors,
                    manager,
                    "enableIntegrationTestSuite",
                    false,
                    "The production scene cannot enable the LevelPlay " +
                    "integration test suite.");
            }
            catch (Exception exception)
            {
                errors.Add(
                    $"Could not validate iOS ad settings in " +
                    $"{HomeScenePath}: {exception.Message}");
            }
            finally
            {
                if (openedForInspection && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateManualInitialization(
            ICollection<string> errors)
        {
            ScriptableObject settings =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    LevelPlaySettingsPath);
            if (settings == null)
            {
                errors.Add(
                    "LevelPlay mediation settings are missing. They are " +
                    "required to verify that automatic initialization is " +
                    "off.");
                return;
            }

            var serializedSettings = new SerializedObject(settings);
            RequireBool(
                errors,
                serializedSettings,
                "EnableIronsourceSDKInitAPI",
                false,
                "LevelPlay automatic initialization must remain off so " +
                "UMP and ATT finish first.");
            RequireBool(
                errors,
                serializedSettings,
                "EnableAdapterDebug",
                false,
                "LevelPlay adapter debugging must be off for release.");
            RequireBool(
                errors,
                serializedSettings,
                "EnableIntegrationHelper",
                false,
                "LevelPlay Integration Helper must be off for release.");
        }

        private static void ValidateDependencies(
            ICollection<string> errors)
        {
            for (int index = 0; index < IOSDependencyFiles.Length; index++)
            {
                string path = IOSDependencyFiles[index];
                string requiredPod = RequiredIOSPods[index];
                string absolutePath = ToAbsoluteProjectPath(path);

                if (!File.Exists(absolutePath))
                {
                    errors.Add(
                        $"Required iOS mediation dependency file is " +
                        $"missing: {path}.");
                    continue;
                }

                try
                {
                    XDocument document = XDocument.Load(absolutePath);
                    bool containsPod = document
                        .Descendants("iosPod")
                        .Any(element => string.Equals(
                            (string)element.Attribute("name"),
                            requiredPod,
                            StringComparison.Ordinal));
                    if (!containsPod)
                    {
                        errors.Add(
                            $"{path} must declare the {requiredPod} iOS " +
                            "CocoaPod.");
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(
                        $"Could not read {path}: {exception.Message}");
                }
            }
        }

        private static void ValidateSKAdNetworkSource(
            ICollection<string> errors)
        {
            string absolutePath = ToAbsoluteProjectPath(
                GoogleSKAdNetworkPath);
            if (!File.Exists(absolutePath))
            {
                errors.Add(
                    "The Google Mobile Ads SKAdNetwork source list is " +
                    "missing.");
                return;
            }

            try
            {
                XDocument document = XDocument.Load(absolutePath);
                bool containsIronSource = document
                    .Descendants("SKAdNetworkIdentifier")
                    .Any(element => string.Equals(
                        element.Value.Trim(),
                        IronSourceSKAdNetworkId,
                        StringComparison.Ordinal));
                if (!containsIronSource)
                {
                    errors.Add(
                        "The iOS SKAdNetwork list must include the " +
                        $"LevelPlay identifier {IronSourceSKAdNetworkId}.");
                }
            }
            catch (Exception exception)
            {
                errors.Add(
                    $"Could not read {GoogleSKAdNetworkPath}: " +
                    exception.Message);
            }
        }

        private static Scene GetOrOpenHomeScene(ref bool opened)
        {
            Scene loadedScene = SceneManager.GetSceneByPath(HomeScenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                return loadedScene;

            opened = true;
            return EditorSceneManager.OpenScene(
                HomeScenePath,
                OpenSceneMode.Additive);
        }

        private static List<Component> FindAdsManagers(Scene scene)
        {
            var managers = new List<Component>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in
                         root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null &&
                        string.Equals(
                            component.GetType().FullName,
                            "Shikaku.Ads.AdsManager",
                            StringComparison.Ordinal))
                    {
                        managers.Add(component);
                    }
                }
            }

            return managers;
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath);
        }

        private static void SetString(
            SerializedObject target,
            string propertyName,
            string value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"AdsManager property '{propertyName}' was not found.");
            }

            property.stringValue = value;
        }

        private static void SetBool(
            SerializedObject target,
            string propertyName,
            bool value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"AdsManager property '{propertyName}' was not found.");
            }

            property.boolValue = value;
        }

        private static void AddStringMismatch(
            ICollection<string> errors,
            SerializedObject target,
            string propertyName,
            string label,
            string expected)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                errors.Add($"{label} property '{propertyName}' is missing.");
                return;
            }

            string actual = property.stringValue?.Trim();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{label} must be '{expected}' " +
                    $"(currently '{actual}').");
            }
        }

        private static void RequireBool(
            ICollection<string> errors,
            SerializedObject target,
            string propertyName,
            bool expected,
            string errorMessage)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                errors.Add(
                    $"Required setting '{propertyName}' was not found.");
                return;
            }

            if (property.boolValue != expected)
                errors.Add(errorMessage);
        }
    }
}
#endif
