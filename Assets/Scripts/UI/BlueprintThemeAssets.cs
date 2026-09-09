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

        public int GetStableHatchIndex(Shikaku.Logic.ShikakuRegion region)
        {
            if (region == null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + region.X;
                hash = hash * 31 + region.Y;
                hash = hash * 31 + region.Width;
                hash = hash * 31 + region.Height;
                hash = hash * 31 + region.ClueValue;
                return (hash & int.MaxValue) % 6;
            }
        }
    }
}
