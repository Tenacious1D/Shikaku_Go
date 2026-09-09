#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Shikaku.UI;
using UnityEditor;
using UnityEngine;

namespace Shikaku.EditorTools
{
    public static class BlueprintThemeAssetBuilder
    {
        private const string ThemePath =
            "Assets/Resources/UI/BlueprintThemeAssets.asset";
        private const string BlueprintFolder =
            "Assets/Art/UI/Blueprint";
        private const string HatchFolder =
            BlueprintFolder + "/Hatches";
        private const string BackgroundFolder =
            BlueprintFolder + "/Backgrounds";
        private const string RivetPath =
            BlueprintFolder + "/Rivet/rivet-sprite-sheet.png";

        [MenuItem("Tools/Shikaku Go/Blueprint Workshop/Build Theme Assets")]
        public static void BuildThemeAssets()
        {
            EnsureFolder("Assets/Resources/UI");
            EnsureFolder(HatchFolder);
            EnsureFolder(BackgroundFolder);

            string whiteprintPath =
                BackgroundFolder + "/whiteprint_grid.png";
            string blueprintPath =
                BackgroundFolder + "/blueprint_grid.png";
            WriteDraftingPaper(whiteprintPath, false);
            WriteDraftingPaper(blueprintPath, true);

            var hatches = new Texture2D[6];
            for (int index = 0; index < hatches.Length; index++)
            {
                string path = $"{HatchFolder}/room-hatch-{index + 1}.png";
                WriteHatch(path, index);
                hatches[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            BlueprintThemeAssets theme =
                AssetDatabase.LoadAssetAtPath<BlueprintThemeAssets>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<BlueprintThemeAssets>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.whiteprintPaper =
                AssetDatabase.LoadAssetAtPath<Texture2D>(whiteprintPath);
            theme.blueprintPaper =
                AssetDatabase.LoadAssetAtPath<Texture2D>(blueprintPath);
            theme.roomHatches = hatches;
            theme.inspectionStamp = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Icons/icon_daily.png");
            theme.approvedStamp = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/checkmark.png");

            Dictionary<string, Sprite> poses = LoadRivetPoses();
            theme.rivetWelcome = GetPose(poses, "rivet_welcome");
            theme.rivetTeach = GetPose(poses, "rivet_teach");
            theme.rivetInspect = GetPose(poses, "rivet_inspect");
            theme.rivetHint = GetPose(poses, "rivet_hint");
            theme.rivetCelebrate = GetPose(poses, "rivet_celebrate");
            theme.rivetConcerned = GetPose(poses, "rivet_concerned");

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(
                "Assets/UI/Themes/BlueprintTheme.uss",
                ImportAssetOptions.ForceUpdate);
            Debug.Log($"Built Blueprint Workshop theme asset at {ThemePath}.");
        }

        private static Dictionary<string, Sprite> LoadRivetPoses()
        {
            var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(RivetPath);
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is Sprite sprite)
                    result[sprite.name] = sprite;
            }
            return result;
        }

        private static Sprite GetPose(
            IReadOnlyDictionary<string, Sprite> poses,
            string name)
        {
            if (!poses.TryGetValue(name, out Sprite sprite))
                throw new InvalidOperationException(
                    $"Rivet pose '{name}' is missing from {RivetPath}.");
            return sprite;
        }

        private static void WriteDraftingPaper(string assetPath, bool dark)
        {
            const int size = 256;
            var pixels = new Color32[size * size];
            Color32 baseColor = dark
                ? new Color32(8, 31, 58, 255)
                : new Color32(246, 248, 244, 255);
            Color32 minorLine = dark
                ? new Color32(16, 57, 94, 255)
                : new Color32(211, 225, 234, 255);
            Color32 majorLine = dark
                ? new Color32(31, 107, 154, 255)
                : new Color32(148, 184, 207, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool major = x % 64 == 0 || y % 64 == 0;
                    bool minor = x % 16 == 0 || y % 16 == 0;
                    Color32 color = major ? majorLine : minor ? minorLine : baseColor;
                    if (!major && !minor)
                    {
                        int grain = ((x * 13 + y * 17 + x * y) % 5) - 2;
                        color = new Color32(
                            ClampByte(color.r + grain),
                            ClampByte(color.g + grain),
                            ClampByte(color.b + grain),
                            255);
                    }
                    pixels[y * size + x] = color;
                }
            }

            WriteTexture(assetPath, size, pixels, false);
        }

        private static void WriteHatch(string assetPath, int hatchIndex)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 ink = new Color32(255, 255, 255, 255);
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = clear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool mark;
                    switch (hatchIndex)
                    {
                        case 0: // concrete stipple
                            int noise = (x * 17 + y * 31 + x * y * 3) & 31;
                            mark = noise == 0 || noise == 11;
                            break;
                        case 1: // wood floor boards
                            int boardRow = y / 12;
                            mark = y % 12 == 0 ||
                                (boardRow % 2 == 0 && x % 32 == 0) ||
                                (boardRow % 2 == 1 && x % 32 == 16);
                            break;
                        case 2: // ceramic tile grid
                            mark = x % 16 == 0 || y % 16 == 0;
                            break;
                        case 3: // steel diagonal hatch
                            mark = (x + y) % 12 == 0;
                            break;
                        case 4: // insulation zigzag
                            int segment = x % 16;
                            int ridge = segment < 8 ? segment : 15 - segment;
                            mark = Mathf.Abs((y % 16) - ridge * 2) < 2;
                            break;
                        default: // survey crosshatch
                            mark = (x + y) % 16 == 0 ||
                                (x - y + size) % 16 == 0;
                            break;
                    }

                    if (mark)
                        pixels[y * size + x] = ink;
                }
            }

            WriteTexture(assetPath, size, pixels, true);
        }

        private static void WriteTexture(
            string assetPath,
            int size,
            Color32[] pixels,
            bool alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaSource = alpha
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = alpha;
                importer.SaveAndReimport();
            }
        }

        private static byte ClampByte(int value)
        {
            return (byte)Mathf.Clamp(value, 0, 255);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
#endif

