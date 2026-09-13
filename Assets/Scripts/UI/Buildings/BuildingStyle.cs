using UnityEngine;

namespace Shikaku.UI.Buildings
{
    [CreateAssetMenu(menuName = "Shikaku/Adventure/Building Style")]
    public sealed class BuildingStyle : ScriptableObject
    {
        public BuildingPalette palette = new BuildingPalette();
    }

    [System.Serializable]
    public sealed class BuildingPalette
    {
        public BuildingArchitecture architecture = BuildingArchitecture.Classic;
        public BuildingPalette Copy() => (BuildingPalette)MemberwiseClone();
        public Color foundation = new Color32(74, 109, 133, 255);
        public Color blueprint = new Color32(121, 215, 237, 255);
        public Color wall = new Color32(222, 179, 124, 255);
        public Color sideWall = new Color32(169, 121, 84, 255);
        public Color window = new Color32(62, 104, 132, 255);
        public Color edge = new Color32(111, 77, 58, 255);
        public Color terrace = new Color32(239, 214, 162, 255);
        public Color roof = new Color32(75, 142, 139, 255);
        [Tooltip("Optional unit textures. Drawn onto projected quads; no pre-isometric art needed. Colors tint textures.")]
        public Texture2D foundationTile;
        public Texture2D wallModule;
        public Texture2D windowModule;
        public Texture2D edgeModule;
        public Texture2D terraceTile;
        public Texture2D roofTile;
        public Texture2D roofDecoration;
    }
}
