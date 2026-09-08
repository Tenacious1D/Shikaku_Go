#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Shikaku.Achievements;
using Unity.Notifications;
using Unity.Notifications.iOS;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Owns the stable Unity-side settings for the Shikaku Go iOS release.
    /// Run the menu command after upgrading Unity or changing Player Settings.
    /// Builds also validate these settings before Unity exports Xcode.
    /// </summary>
    public sealed class IOSReleaseConfigurator : IPreprocessBuildWithReport
    {
        private const string BundleIdentifier =
            "com.smoothbraingames.shikakugo";
        private const string Version = "1.0.0";
        private const string FirstBuildNumber = "1";
        private const string MinimumIOSVersion = "15.0";
        private const string AppleTeamId = "TCSF74TDNV";
        private const string GameCenterDefine =
            "SHIKAKU_APPLE_GAME_CENTER";
        private const int Arm64Architecture = 1;

        private static readonly NamedBuildTarget IOSBuildTarget =
            NamedBuildTarget.iOS;

        public int callbackOrder => -1000;

        [MenuItem("Tools/Shikaku Go/iOS/Apply Release Foundation")]
        public static void ApplyReleaseFoundation()
        {
            PlayerSettings.SetApplicationIdentifier(
                IOSBuildTarget,
                BundleIdentifier);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.iOS.buildNumber = FirstBuildNumber;
            PlayerSettings.iOS.targetOSVersionString = MinimumIOSVersion;
            PlayerSettings.iOS.targetDevice =
                iOSTargetDevice.iPhoneAndiPad;

            PlayerSettings.defaultInterfaceOrientation =
                UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(
                IOSBuildTarget,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(
                IOSBuildTarget,
                Arm64Architecture);

            PlayerSettings.iOS.appleDeveloperTeamID = AppleTeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;

            if (NotificationSettings.iOSSettings
                .RequestAuthorizationOnAppLaunch)
            {
                NotificationSettings.iOSSettings
                    .RequestAuthorizationOnAppLaunch = false;
            }

            if (NotificationSettings.iOSSettings
                .DefaultAuthorizationOptions != AuthorizationOption.Alert)
            {
                NotificationSettings.iOSSettings
                    .DefaultAuthorizationOptions = AuthorizationOption.Alert;
            }

            ConfigureIOSDefines();
            AssetDatabase.SaveAssets();

            IReadOnlyList<string> errors = CollectErrors();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "The Shikaku Go iOS release foundation could not be " +
                    "applied:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log(
                "Shikaku Go iOS release foundation applied: " +
                $"{BundleIdentifier}, version {Version} ({FirstBuildNumber}), " +
                "iPhone + iPad, portrait, iOS 15.0+, IL2CPP ARM64, " +
                $"automatic signing with team {AppleTeamId}.");
        }

        [MenuItem("Tools/Shikaku Go/iOS/Validate Release Foundation")]
        public static void ValidateFromMenu()
        {
            IReadOnlyList<string> errors = CollectErrors();
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "iOS Release Foundation",
                    "All foundation settings are valid.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "iOS Release Foundation Needs Attention",
                "- " + string.Join("\n- ", errors),
                "OK");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            IReadOnlyList<string> errors = CollectErrors();
            if (errors.Count == 0)
                return;

            throw new BuildFailedException(
                "Shikaku Go iOS release validation failed:\n- " +
                string.Join("\n- ", errors));
        }

        private static void ConfigureIOSDefines()
        {
            PlayerSettings.GetScriptingDefineSymbols(
                IOSBuildTarget,
                out string[] defines);
            var keptDefines = new List<string>();

            foreach (string define in defines)
            {
                if (string.IsNullOrWhiteSpace(define) ||
                    IsAndroidOnlyDefine(define))
                {
                    continue;
                }

                keptDefines.Add(define.Trim());
            }

            if (!keptDefines.Contains(GameCenterDefine))
                keptDefines.Add(GameCenterDefine);

            PlayerSettings.SetScriptingDefineSymbols(
                IOSBuildTarget,
                keptDefines.ToArray());
        }

        internal static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();

            AddMismatch(
                errors,
                "Bundle identifier",
                BundleIdentifier,
                PlayerSettings.GetApplicationIdentifier(IOSBuildTarget));
            AddMismatch(
                errors,
                "Version",
                Version,
                PlayerSettings.bundleVersion);

            if (!int.TryParse(
                    PlayerSettings.iOS.buildNumber,
                    out int buildNumber) ||
                buildNumber < 1)
            {
                errors.Add("iOS build number must be a positive integer.");
            }

            AddMismatch(
                errors,
                "Minimum iOS version",
                MinimumIOSVersion,
                PlayerSettings.iOS.targetOSVersionString);

            if (PlayerSettings.iOS.targetDevice !=
                iOSTargetDevice.iPhoneAndiPad)
            {
                errors.Add("Target device must be iPhone + iPad.");
            }

            if (PlayerSettings.defaultInterfaceOrientation !=
                UIOrientation.Portrait)
            {
                errors.Add("Default orientation must be Portrait.");
            }

            if (!PlayerSettings.allowedAutorotateToPortrait ||
                PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
                PlayerSettings.allowedAutorotateToLandscapeLeft ||
                PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                errors.Add("Only portrait orientation may be enabled.");
            }

            if (PlayerSettings.GetScriptingBackend(IOSBuildTarget) !=
                ScriptingImplementation.IL2CPP)
            {
                errors.Add("The iOS scripting backend must be IL2CPP.");
            }

            if (PlayerSettings.GetArchitecture(IOSBuildTarget) !=
                Arm64Architecture)
            {
                errors.Add("The iOS architecture must be ARM64.");
            }

            AddMismatch(
                errors,
                "Apple Team ID",
                AppleTeamId,
                PlayerSettings.iOS.appleDeveloperTeamID);

            if (!PlayerSettings.iOS.appleEnableAutomaticSigning)
                errors.Add("Automatic Apple signing must be enabled.");

            if (NotificationSettings.iOSSettings
                .RequestAuthorizationOnAppLaunch)
            {
                errors.Add(
                    "iOS notification authorization must not be " +
                    "requested automatically at app launch.");
            }

            if (NotificationSettings.iOSSettings
                .DefaultAuthorizationOptions !=
                AuthorizationOption.Alert)
            {
                errors.Add(
                    "iOS reminders must request quiet alert-only " +
                    "authorization.");
            }

            bool hasGameCenterDefine = false;
            PlayerSettings.GetScriptingDefineSymbols(
                IOSBuildTarget,
                out string[] iosDefines);

            foreach (string define in iosDefines)
            {
                if (IsAndroidOnlyDefine(define))
                {
                    errors.Add(
                        $"Android-only define '{define}' is set for iOS.");
                }

                if (string.Equals(
                        define,
                        GameCenterDefine,
                        StringComparison.Ordinal))
                {
                    hasGameCenterDefine = true;
                }
            }

            if (!hasGameCenterDefine)
            {
                errors.Add(
                    $"iOS scripting defines must include {GameCenterDefine}.");
            }

            var appleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AchievementId achievement in
                     Enum.GetValues(typeof(AchievementId)))
            {
                string appleId =
                    AchievementPlatformIds.GetAppleId(achievement);
                if (!AchievementPlatformIds.IsConfigured(appleId))
                {
                    errors.Add(
                        $"Apple achievement ID for {achievement} is missing.");
                }
                else if (appleId.Length > 100)
                {
                    errors.Add(
                        $"Apple achievement ID for {achievement} exceeds " +
                        "100 characters.");
                }
                else if (!appleIds.Add(appleId))
                {
                    errors.Add($"Duplicate Apple achievement ID: {appleId}.");
                }
            }

            foreach (string firebaseError in
                     FirebaseIOSConfiguration.CollectErrors())
            {
                errors.Add(firebaseError);
            }

            foreach (string attError in
                     IOSATTReleaseConfiguration.CollectErrors())
            {
                errors.Add(attError);
            }

            return errors;
        }

        private static bool IsAndroidOnlyDefine(string define)
        {
            return string.Equals(
                define,
                "SHIKAKU_GOOGLE_PLAY_GAMES",
                StringComparison.Ordinal);
        }

        private static void AddMismatch(
            ICollection<string> errors,
            string label,
            string expected,
            string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{label} must be '{expected}' (currently '{actual}').");
            }
        }
    }
}
#endif
