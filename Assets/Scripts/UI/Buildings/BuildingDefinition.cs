using UnityEngine;

namespace Shikaku.UI.Buildings
{
    [CreateAssetMenu(menuName = "Shikaku/Adventure/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        public string buildingId;
        public string displayName;
        [Tooltip("Resources path without Puzzles/ or .json, e.g. Story/Adventure_Chapter01")]
        public string chapterPackPath;
        [Tooltip("Optional style asset overrides automatic architecture. Existing assignments remain supported.")]
        public BuildingStyle style;
        [Tooltip("Change this seed to deliberately reroll the default appearance.")]
        public int appearanceSeed;
        public bool overrideArchitecture;
        public BuildingArchitecture architecture;
        public bool overrideColors;
        public Color wallColor = new Color32(211, 152, 118, 255);
        public Color roofColor = new Color32(77, 109, 118, 255);
        [Tooltip("Optional city plot adjustment in design units. Keep small enough to avoid neighboring plots and roads.")]
        public Vector2 mapOffset;
    }
}
