#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Assigns the approved opaque square artwork to every iOS application
    /// icon slot. Unity produces the required device and App Store sizes.
    /// </summary>
    public static class IOSAppIconConfiguration
    {
        public const string SourceAssetPath =
            "Assets/Art/Logos/shikaku_blueprint_app_icon.png";

        [MenuItem("Tools/Shikaku Go/iOS/Apply App Icon")]
        public static void ApplyIOSAppIcon()
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(
                SourceAssetPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"The iOS app icon source is missing at " +
                    $"{SourceAssetPath}.");
            }

            int[] sizes = PlayerSettings.GetIconSizes(
                NamedBuildTarget.iOS,
                IconKind.Any);
            if (sizes == null || sizes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Unity did not report any iOS app icon slots.");
            }

            var icons = new Texture2D[sizes.Length];
            for (int index = 0; index < icons.Length; index++)
                icons[index] = source;

            PlayerSettings.SetIcons(
                NamedBuildTarget.iOS,
                icons,
                IconKind.Any);
            AssetDatabase.SaveAssets();

            IReadOnlyList<string> errors = CollectErrors();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "The iOS app icon could not be applied:\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log(
                $"Assigned {SourceAssetPath} to all {icons.Length} iOS " +
                "application icon slots.");
        }

        public static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(
                SourceAssetPath);
            if (source == null)
            {
                errors.Add(
                    $"The iOS app icon source is missing at " +
                    $"{SourceAssetPath}.");
                return errors;
            }

            ValidateSourcePng(errors);

            Texture2D[] icons = PlayerSettings.GetIcons(
                NamedBuildTarget.iOS,
                IconKind.Any);
            if (icons == null || icons.Length == 0)
            {
                errors.Add("Unity reports no iOS application icon slots.");
                return errors;
            }

            for (int index = 0; index < icons.Length; index++)
            {
                if (icons[index] == null)
                {
                    errors.Add(
                        $"iOS application icon slot {index + 1} of " +
                        $"{icons.Length} is empty.");
                    continue;
                }

                string assignedPath = AssetDatabase.GetAssetPath(
                    icons[index]);
                if (!string.Equals(
                        assignedPath,
                        SourceAssetPath,
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        $"iOS application icon slot {index + 1} uses " +
                        $"'{assignedPath}' instead of '{SourceAssetPath}'.");
                }
            }

            return errors;
        }

        private static void ValidateSourcePng(
            ICollection<string> errors)
        {
            string absolutePath = Path.GetFullPath(
                Path.Combine(
                    Directory.GetParent(Application.dataPath)?.FullName ??
                    string.Empty,
                    SourceAssetPath));
            if (!File.Exists(absolutePath))
            {
                errors.Add(
                    $"The iOS icon file is missing at {SourceAssetPath}.");
                return;
            }

            try
            {
                byte[] header = new byte[26];
                using (FileStream stream = File.OpenRead(absolutePath))
                {
                    if (stream.Read(header, 0, header.Length) !=
                        header.Length)
                    {
                        errors.Add("The iOS icon PNG header is incomplete.");
                        return;
                    }
                }

                bool pngSignature =
                    header[0] == 0x89 && header[1] == 0x50 &&
                    header[2] == 0x4e && header[3] == 0x47 &&
                    header[4] == 0x0d && header[5] == 0x0a &&
                    header[6] == 0x1a && header[7] == 0x0a;
                if (!pngSignature)
                {
                    errors.Add("The iOS icon source must be a PNG file.");
                    return;
                }

                int width = ReadBigEndianInt(header, 16);
                int height = ReadBigEndianInt(header, 20);
                if (width != height)
                    errors.Add("The iOS app icon source must be square.");
                if (width < 1024 || height < 1024)
                {
                    errors.Add(
                        "The iOS app icon source must be at least " +
                        "1024 x 1024 pixels.");
                }

                byte colorType = header[25];
                if (colorType == 4 || colorType == 6)
                {
                    errors.Add(
                        "The App Store icon source cannot contain an " +
                        "alpha channel.");
                }
            }
            catch (Exception exception)
            {
                errors.Add(
                    "The iOS app icon could not be inspected: " +
                    exception.Message);
            }
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) |
                   (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }
    }
}
#endif
