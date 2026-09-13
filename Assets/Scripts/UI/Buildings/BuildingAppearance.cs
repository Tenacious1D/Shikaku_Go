using UnityEngine;

namespace Shikaku.UI.Buildings
{
    public enum BuildingArchitecture { Classic, Modern, Warehouse, ArtDeco }

    public static class BuildingAppearance
    {
        // Explicit stable hash: never string.GetHashCode or UnityEngine.Random.
        public static uint StableHash(string id, int seed = 0)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in id ?? "") { hash ^= c; hash *= 16777619; }
                return hash + (uint)seed * 2654435761u;
            }
        }

        private static readonly Color32[] Walls =
        {
            new Color32(211, 133, 111, 255), new Color32(123, 173, 170, 255),
            new Color32(222, 187, 114, 255), new Color32(151, 158, 197, 255),
            new Color32(164, 188, 140, 255), new Color32(206, 166, 184, 255),
            new Color32(222, 208, 164, 255), new Color32(138, 170, 197, 255)
        };
        private static readonly Color32[] Roofs =
        {
            new Color32(88, 109, 133, 255), new Color32(61, 104, 109, 255),
            new Color32(164, 90, 70, 255), new Color32(76, 90, 130, 255),
            new Color32(102, 121, 86, 255), new Color32(119, 87, 122, 255),
            new Color32(146, 111, 81, 255), new Color32(64, 95, 123, 255)
        };

        public static BuildingPalette Resolve(string stableId, BuildingDefinition definition)
        {
            uint hash = StableHash(stableId, definition != null ? definition.appearanceSeed : 0);
            BuildingPalette result;
            if (definition?.style != null && definition.style.palette != null)
                result = definition.style.palette.Copy();
            else
            {
                int index = (int)(hash % (uint)Walls.Length);
                result = new BuildingPalette { architecture = (BuildingArchitecture)((hash / 7) % 4) };
                ApplyColors(result, Walls[index], Roofs[index]);
            }
            if (definition != null && definition.overrideArchitecture)
            {
                result.architecture = definition.architecture;
                if (definition.style == null)
                    result.window = result.architecture == BuildingArchitecture.Modern
                        ? new Color32(158, 217, 225, 255) : new Color32(43, 75, 96, 255);
            }
            if (definition != null && definition.overrideColors)
                ApplyColors(result, definition.wallColor, definition.roofColor);
            return result;
        }

        private static void ApplyColors(BuildingPalette palette, Color wall, Color roof)
        {
            palette.wall = wall;
            palette.sideWall = Color.Lerp(wall, new Color(0.10f, 0.16f, 0.22f), 0.28f);
            palette.edge = Color.Lerp(wall, Color.black, 0.42f);
            palette.terrace = Color.Lerp(wall, new Color(1, 0.98f, 0.91f), 0.48f);
            palette.roof = roof;
            palette.window = palette.architecture == BuildingArchitecture.Modern
                ? new Color32(158, 217, 225, 255) : new Color32(43, 75, 96, 255);
        }
    }
}
