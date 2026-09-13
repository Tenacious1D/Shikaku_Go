using System.Collections.Generic;
using UnityEngine;

namespace Shikaku.UI.Buildings
{
    public static class BuildingGeometry
    {
        public const float FloorHeight = 0.85f;
        public const float FoundationHeight = 0.16f;
        public const float RoofHeight = 0.13f;
        public const float ConstructionLift = 0.75f;

        public static Vector2 Project(float x, float depth, float elevation)
            => new Vector2((x - depth) * 0.8660254f, (x + depth) * 0.5f - elevation);

        // Keep the original grid origin; mask cutouts never recenter a floor.
        public static Rect Footprint(AdventureBuildingData.Floor floor)
            => new Rect(-floor.Width * 0.5f, -floor.Depth * 0.5f, floor.Width, floor.Depth);

        public readonly struct Patch
        {
            public readonly int FloorIndex; // -1 is the ground-floor foundation.
            public readonly Rect Area;
            public readonly Rect Cell;
            public readonly bool Front;
            public readonly bool Side;
            public Patch(int floorIndex, Rect area, Rect cell, bool front, bool side)
            { FloorIndex = floorIndex; Area = area; Cell = cell; Front = front; Side = side; }
        }

        public static List<Patch> BuildPatches(AdventureBuildingData data)
        {
            var patches = new List<Patch>();
            for (int i = -1; i < data.Floors.Count; i++)
            {
                var floor = data.Floors[Mathf.Max(0, i)];
                Rect footprint = Footprint(floor);
                for (int y = 0; y < floor.Depth; y++)
                for (int x = 0; x < floor.Width; x++)
                {
                    if (!floor.Contains(x, y)) continue;
                    var cell = new Rect(footprint.xMin + x, footprint.yMin + y, 1, 1);
                    // Half-unit columns align even and odd grid sizes exactly.
                    // Sorting columns back-to-front, then elevation, handles
                    // concave outlines, holes and changing footprints without
                    // painting a far upper floor over a nearer lower wall.
                    for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                        patches.Add(new Patch(i,
                            new Rect(cell.x + dx * 0.5f, cell.y + dy * 0.5f, 0.5f, 0.5f), cell,
                            dy == 1 && !floor.Contains(x, y + 1),
                            dx == 1 && !floor.Contains(x + 1, y)));
                }
            }
            patches.Sort((a, b) =>
            {
                int depth = (a.Area.x + a.Area.y).CompareTo(b.Area.x + b.Area.y);
                if (depth != 0) return depth;
                int x = a.Area.x.CompareTo(b.Area.x);
                return x != 0 ? x : a.FloorIndex.CompareTo(b.FloorIndex);
            });
            return patches;
        }

        public static Rect Bounds(AdventureBuildingData data)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (var patch in data.Patches)
            {
                float bottom = patch.FloorIndex < 0 ? 0 : FoundationHeight + patch.FloorIndex * FloorHeight;
                float top = patch.FloorIndex < 0 ? FoundationHeight :
                    bottom + FloorHeight + RoofHeight + ConstructionLift + 0.2f;
                for (int c = 0; c < 4; c++)
                {
                    float x = (c & 1) == 0 ? patch.Area.xMin : patch.Area.xMax;
                    float y = (c & 2) == 0 ? patch.Area.yMin : patch.Area.yMax;
                    min = Vector2.Min(min, Project(x, y, top));
                    max = Vector2.Max(max, Project(x, y, bottom));
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
