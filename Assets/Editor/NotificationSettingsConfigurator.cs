#if UNITY_EDITOR
using Unity.Notifications;
using Unity.Notifications.iOS;
using UnityEditor;
using UnityEngine;

namespace Shikaku.EditorTools
{
    [InitializeOnLoad]
    internal static class NotificationSettingsConfigurator
    {
        private const string IconAssetPath =
            "Assets/Art/UI/Icons/icon_notification.png";
        private const string IconId = "shikaku_notification";
        private const string SessionConfiguredKey =
            "Shikaku.Notifications.Configured.v3";

        static NotificationSettingsConfigurator()
        {
            EditorApplication.delayCall += ConfigureOncePerSession;
        }

        [MenuItem(
            "Tools/Shikaku/Configure Mobile Notifications")]
        private static void ConfigureFromMenu()
        {
            Configure(force: true);
        }

        private static void ConfigureOncePerSession()
        {
            Configure(force: false);
        }

        private static void Configure(bool force)
        {
            if (!force &&
                SessionState.GetBool(
                    SessionConfiguredKey,
                    false))
            {
                return;
            }

            SessionState.SetBool(
                SessionConfiguredKey,
                true);

            // Permission is requested only after the player opts in.
            NotificationSettings.iOSSettings
                .RequestAuthorizationOnAppLaunch = false;
            NotificationSettings.iOSSettings
                .DefaultAuthorizationOptions =
                    AuthorizationOption.Alert;

            var importer =
                AssetImporter.GetAtPath(IconAssetPath)
                    as TextureImporter;

            if (importer != null)
            {
                bool reimport =
                    !importer.alphaIsTransparency ||
                    importer.mipmapEnabled ||
                    !importer.isReadable;

                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = true;

                if (reimport)
                    importer.SaveAndReimport();
            }

            Texture2D icon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    IconAssetPath);

            if (icon == null)
            {
                Debug.LogWarning(
                    "NotificationSettingsConfigurator: " +
                    $"Icon was not found at {IconAssetPath}.");
                return;
            }

            NotificationSettings.AndroidSettings
                .RemoveDrawableResource(IconId);
            NotificationSettings.AndroidSettings
                .AddDrawableResource(
                    IconId,
                    icon,
                    NotificationIconType.Small);

            NotificationSettings.AndroidSettings
                .RescheduleOnDeviceRestart = true;

            // Zero flags means inexact scheduling and adds no exact-alarm
            // permission to the Android manifest.
            NotificationSettings.AndroidSettings
                .ExactSchedulingOption =
                    (AndroidExactSchedulingOption)0;

            AssetDatabase.SaveAssets();

            Debug.Log(
                "Shikaku mobile notifications configured: " +
                "user-initiated quiet iOS alerts plus Android " +
                "inexact reminders, reboot rescheduling, and " +
                "the custom Android small icon.");
        }
    }
}
#endif
