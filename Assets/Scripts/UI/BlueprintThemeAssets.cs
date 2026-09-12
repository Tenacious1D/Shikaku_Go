using UnityEngine;

namespace Shikaku.UI
{
    [CreateAssetMenu(
        fileName = "BlueprintThemeAssets",
        menuName = "Shikaku/Blueprint Workshop Theme")]
    public sealed class BlueprintThemeAssets : ScriptableObject
    {
        [Header("Drafting Paper")]
        public Texture2D whiteprintPaper;
        public Texture2D blueprintPaper;

        [Header("Room Hatches")]
        [Tooltip("Concrete, wood, tile, steel, insulation, and survey hatch.")]
        public Texture2D[] roomHatches = new Texture2D[6];

        [Header("Stamps")]
        public Sprite inspectionStamp;
        public Sprite approvedStamp;

        [Header("Rivet")]
        public Sprite rivetWelcome;
        public Sprite rivetTeach;
        public Sprite rivetInspect;
        public Sprite rivetHint;
        public Sprite rivetCelebrate;
        public Sprite rivetConcerned;

        [Header("Room Drawing")]
        public Color whiteprintInk = new Color32(18, 79, 132, 255);
        public Color blueprintInk = new Color32(231, 245, 248, 255);
        public Color whiteprintWall = new Color32(16, 71, 121, 255);
        public Color blueprintWall = new Color32(220, 244, 248, 255);
        [Range(0f, 0.4f)] public float whiteprintHatchOpacity = 0.13f;
        [Range(0f, 0.4f)] public float blueprintHatchOpacity = 0.12f;
        [Min(0f)] public float wallInkDuration = 0.14f;

        [Header("Solid Room Fill")]
        public Color whiteprintRoomBase = new Color32(226, 241, 244, 255);
        public Color blueprintRoomBase = new Color32(8, 35, 58, 255);
        [Range(0f, 1f)] public float whiteprintPaletteStrength = 0.46f;
        [Range(0f, 1f)] public float blueprintPaletteStrength = 0.55f;
        public Color whiteprintInvalidFill = new Color32(246, 215, 214, 255);
        public Color blueprintInvalidFill = new Color32(77, 35, 43, 255);
        public Color selectedRoomOutline = new Color32(244, 188, 43, 255);

        private static BlueprintThemeAssets _runtimeFallback;

        public static BlueprintThemeAssets Resolve(BlueprintThemeAssets assigned)
        {
            if (assigned != null)
                return assigned;

            BlueprintThemeAssets resource =
                Resources.Load<BlueprintThemeAssets>("UI/BlueprintThemeAssets");
            if (resource != null)
                return resource;

            if (_runtimeFallback == null)
            {
                _runtimeFallback = CreateInstance<BlueprintThemeAssets>();
                _runtimeFallback.name = "Blueprint Runtime Defaults";
                _runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _runtimeFallback;
        }

        public Texture2D GetRoomHatch(int index)
        {
            if (roomHatches == null || roomHatches.Length == 0)
                return null;

            int resolved = (index & int.MaxValue) % roomHatches.Length;
            return roomHatches[resolved];
        }

        public Color GetPatternColor(bool dark)
        {
            Color color = dark ? blueprintInk : whiteprintInk;
            color.a = dark ? blueprintHatchOpacity : whiteprintHatchOpacity;
            return color;
        }

        public Color GetWallColor(bool dark)
        {
            return dark ? blueprintWall : whiteprintWall;
        }

        public Color GetRoomFill(Color paletteColor, bool dark, bool valid)
        {
            if (!valid)
                return dark ? blueprintInvalidFill : whiteprintInvalidFill;

            Color baseColor = dark ? blueprintRoomBase : whiteprintRoomBase;
            float strength = dark
                ? blueprintPaletteStrength
                : whiteprintPaletteStrength;
            Color result = Color.Lerp(baseColor, paletteColor, strength);
            result.a = 1f;
            return result;
        }

        public Color GetRoomOutline(
            Color paletteColor,
            bool dark,
            bool valid,
            bool selected)
        {
            if (!valid)
                return new Color32(204, 62, 67, 255);
            if (selected)
                return selectedRoomOutline;

            Color ink = GetWallColor(dark);
            return dark
                ? Color.Lerp(paletteColor, ink, 0.64f)
                : Color.Lerp(paletteColor, ink, 0.72f);
        }

        public int GetStablePaletteIndex(
            Shikaku.Logic.ShikakuRegion region,
            int paletteColorCount)
        {
            if (region == null || paletteColorCount <= 0)
                return 1;

            // The first entry is neutral and entries 10+ repeat the same hue
            // families. Shuffle across the eight distinct construction colors.
            if (paletteColorCount < 2)
                return 1;
            int distinctColorCount = Mathf.Min(8, paletteColorCount - 1);
            uint seed = GetStableRegionSeed(region);
            return 2 + (int)(seed % (uint)distinctColorCount);
        }

        public int GetStableHatchIndex(Shikaku.Logic.ShikakuRegion region)
        {
            if (region == null)
                return 0;

            uint seed = GetStableRegionSeed(region) ^ 0x9E3779B9u;
            return (int)(seed % 6u);
        }

        private static uint GetStableRegionSeed(
            Shikaku.Logic.ShikakuRegion region)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)(region.X + 1)) * 16777619u;
                hash = (hash ^ (uint)(region.Y + 1)) * 16777619u;
                hash = (hash ^ (uint)region.Width) * 16777619u;
                hash = (hash ^ (uint)region.Height) * 16777619u;
                hash = (hash ^ (uint)region.ClueValue) * 16777619u;

                // Avalanche the low bits before using a small palette modulus.
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash;
            }
        }
    }
}
