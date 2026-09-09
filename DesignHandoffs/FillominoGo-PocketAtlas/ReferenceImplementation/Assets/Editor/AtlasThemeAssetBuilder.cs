#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Shikaku.UI;
using UnityEditor;
using UnityEngine;

namespace Shikaku.EditorTools
{
    public static class AtlasThemeAssetBuilder
    {
        private const string ThemePath =
            "Assets/Resources/UI/AtlasThemeAssets.asset";
        private const string MotifFolder =
            "Assets/Art/UI/Atlas/Motifs";
        private const string PipPath =
            "Assets/Art/UI/Atlas/Pip/pip-sprite-sheet.png";

        [MenuItem("Tools/Shikaku Go/Pocket Atlas/Build Theme Assets")]
        public static void BuildThemeAssets()
        {
            EnsureFolder("Assets/Resources/UI");
            EnsureFolder(MotifFolder);

            var motifs = new Texture2D[6];
            for (int index = 0; index < motifs.Length; index++)
            {
                string path = $"{MotifFolder}/atlas-motif-{index + 1}.png";
                WriteMotif(path, index);
                motifs[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            AtlasThemeAssets theme =
                AssetDatabase.LoadAssetAtPath<AtlasThemeAssets>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<AtlasThemeAssets>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.lightPaper = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/UI/Backgrounds/shikaku_warm_stone_paper_seamless.png");
            theme.darkPaper = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/UI/Backgrounds/shikaku_dark_paper_seamless.png");
            theme.districtMotifs = motifs;
            theme.compassStamp = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Icons/icon_daily.png");
            theme.approvedStamp = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/checkmark.png");

            Dictionary<string, Sprite> poses = LoadPipPoses();
            theme.pipWelcome = GetPose(poses, "pip_welcome");
            theme.pipTeach = GetPose(poses, "pip_teach");
            theme.pipInspect = GetPose(poses, "pip_inspect");
            theme.pipHint = GetPose(poses, "pip_hint");
            theme.pipCelebrate = GetPose(poses, "pip_celebrate");
            theme.pipConcerned = GetPose(poses, "pip_concerned");

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built Pocket Atlas theme asset at {ThemePath}.");
        }

        private static Dictionary<string, Sprite> LoadPipPoses()
        {
            var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(PipPath);
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
                throw new InvalidOperationException($"Pip pose '{name}' is missing.");
            return sprite;
        }

        private static void WriteMotif(string assetPath, int motifIndex)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
                    switch (motifIndex)
                    {
                        case 0:
                            float dx = x - 31.5f;
                            float dy = y - 31.5f;
                            mark = Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dy * dy)) % 12 == 0;
                            break;
                        case 1:
                            mark = x % 16 == 8 && y % 16 == 8;
                            break;
                        case 2:
                            mark = x % 24 == 4 || y % 24 == 12;
                            break;
                        case 3:
                            int wave = 8 + Mathf.RoundToInt(
                                Mathf.Sin(x * Mathf.PI / 16f) * 3f);
                            mark = y % 16 == wave;
                            break;
                        case 4:
                            mark = ((x + y) % 18) < 2 && (x / 8) % 2 == 0;
                            break;
                        default:
                            mark = (x + y) % 16 == 0;
                            break;
                    }
                    if (mark)
                        pixels[y * size + x] = ink;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            string absolute = Path.GetFullPath(assetPath);
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
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
