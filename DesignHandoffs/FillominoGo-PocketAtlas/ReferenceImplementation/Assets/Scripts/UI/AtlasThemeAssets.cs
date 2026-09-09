using UnityEngine;

namespace Shikaku.UI
{
    [CreateAssetMenu(
        fileName = "AtlasThemeAssets",
        menuName = "Shikaku/Pocket Atlas Theme")]
    public sealed class AtlasThemeAssets : ScriptableObject
    {
        [Header("Paper")]
        public Texture2D lightPaper;
        public Texture2D darkPaper;

        [Header("District Motifs")]
        [Tooltip("Contours, orchard, streets, water, trails, and survey hatch.")]
        public Texture2D[] districtMotifs = new Texture2D[6];

        [Header("Stamps")]
        public Sprite compassStamp;
        public Sprite approvedStamp;

        [Header("Pip")]
        public Sprite pipWelcome;
        public Sprite pipTeach;
        public Sprite pipInspect;
        public Sprite pipHint;
        public Sprite pipCelebrate;
        public Sprite pipConcerned;

        [Header("Region Decoration")]
        public Color lightPatternInk = new Color32(45, 43, 38, 255);
        public Color darkPatternInk = new Color32(239, 232, 217, 255);
        [Range(0f, 0.4f)] public float lightPatternOpacity = 0.15f;
        [Range(0f, 0.4f)] public float darkPatternOpacity = 0.11f;
        [Min(0f)] public float regionCommitDuration = 0.14f;

        private static AtlasThemeAssets _runtimeFallback;

        public static AtlasThemeAssets Resolve(AtlasThemeAssets assigned)
        {
            if (assigned != null)
                return assigned;

            AtlasThemeAssets resource =
                Resources.Load<AtlasThemeAssets>("UI/AtlasThemeAssets");
            if (resource != null)
                return resource;

            if (_runtimeFallback == null)
            {
                _runtimeFallback = CreateInstance<AtlasThemeAssets>();
                _runtimeFallback.name = "Pocket Atlas Runtime Defaults";
                _runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _runtimeFallback;
        }

        public Texture2D GetDistrictMotif(int index)
        {
            if (districtMotifs == null || districtMotifs.Length == 0)
                return null;

            int resolved = Mathf.Abs(index) % districtMotifs.Length;
            return districtMotifs[resolved];
        }

        public Color GetPatternColor(bool dark)
        {
            Color color = dark ? darkPatternInk : lightPatternInk;
            color.a = dark ? darkPatternOpacity : lightPatternOpacity;
            return color;
        }

        public int GetStableMotifIndex(Shikaku.Logic.ShikakuRegion region)
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
                return Mathf.Abs(hash % 6);
            }
        }
    }
}