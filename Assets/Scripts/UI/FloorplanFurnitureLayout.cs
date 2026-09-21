using System.Collections.Generic;
using Shikaku.Logic;
using UnityEngine;

namespace Shikaku.UI
{
    public enum FloorplanFurnitureKind
    {
        Bed, Sofa, Desk, Cabinet, Armchair, Table, Plant,
        DiningSet, KitchenCounter, Bathtub, Basin, Toilet, Bookcase, Washer, Bench
    }

    public readonly struct FloorplanFurniturePlacement
    {
        public readonly FloorplanFurnitureKind Kind;
        // Room-local cell units, origin at top left. Includes every face and shadow.
        public readonly Rect Bounds;
        public readonly bool QuarterTurn;
        public FloorplanFurniturePlacement(FloorplanFurnitureKind kind, Rect bounds, bool quarterTurn = false)
        { Kind = kind; Bounds = bounds; QuarterTurn = quarterTurn; }
    }

    /// <summary>Deterministic room templates using only committed player regions and public clues.</summary>
    public static class FloorplanFurnitureLayout
    {
        private readonly struct RoomTemplate
        {
            public readonly FloorplanFurnitureKind Primary, Secondary, Accent;
            public readonly Vector2 Size, SecondarySize;
            public readonly int MinimumArea;
            public RoomTemplate(FloorplanFurnitureKind primary, float width, float height,
                FloorplanFurnitureKind secondary, float secondaryWidth, float secondaryHeight,
                FloorplanFurnitureKind accent = FloorplanFurnitureKind.Plant, int minimumArea = 4)
            {
                Primary = primary; Size = new Vector2(width, height);
                Secondary = secondary; SecondarySize = new Vector2(secondaryWidth, secondaryHeight);
                Accent = accent; MinimumArea = minimumArea;
            }
        }

        // Bedroom, lounge, office, dining, kitchen, bathroom, reading room, laundry, storage.
        // Each family supplies related objects instead of randomly mixing unrelated props.
        private static readonly RoomTemplate[] Templates = {
            new(FloorplanFurnitureKind.Bed, 1.45f, 2.05f, FloorplanFurnitureKind.Cabinet, .55f, .55f, minimumArea: 6),
            new(FloorplanFurnitureKind.Sofa, 1.85f, 1.12f, FloorplanFurnitureKind.Table, .65f, .65f),
            new(FloorplanFurnitureKind.Desk, 1.65f, 1.35f, FloorplanFurnitureKind.Bookcase, 1.0f, .46f),
            new(FloorplanFurnitureKind.DiningSet, 1.85f, 1.75f, FloorplanFurnitureKind.Cabinet, 1.0f, .48f, minimumArea: 6),
            new(FloorplanFurnitureKind.KitchenCounter, 2.0f, .85f, FloorplanFurnitureKind.Cabinet, .65f, .65f),
            new(FloorplanFurnitureKind.Bathtub, 1.0f, 1.9f, FloorplanFurnitureKind.Basin, .62f, .7f, FloorplanFurnitureKind.Toilet, 6),
            new(FloorplanFurnitureKind.Armchair, 1.12f, 1.2f, FloorplanFurnitureKind.Bookcase, 1.05f, .48f, FloorplanFurnitureKind.Table),
            new(FloorplanFurnitureKind.Washer, .95f, 1.05f, FloorplanFurnitureKind.Basin, .62f, .7f, FloorplanFurnitureKind.Cabinet),
            new(FloorplanFurnitureKind.Bookcase, 1.8f, .65f, FloorplanFurnitureKind.Cabinet, .75f, .55f)
        };
        private static readonly RoomTemplate[] NarrowTemplates = {
            new(FloorplanFurnitureKind.Cabinet, 1.5f, .56f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.Bookcase, 1.6f, .55f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.Bench, 1.8f, .62f, FloorplanFurnitureKind.Plant, .48f, .48f)
        };
        public const float WallInset = .16f;
        public const float MinimumCellPixels = 12f;
        public const float AccessoryCellPixels = 30f;
        private const float Clearance = .08f;

        public static Rect ClueBounds(PuzzleModel model, ShikakuRegion room)
        {
            for (int y = 0; y < room.Height; y++)
                for (int x = 0; x < room.Width; x++)
                    if (model.GivenNumber[(room.Y + y) * model.Width + room.X + x] > 0)
                        return new Rect(x, y, 1, 1);
            return new Rect();
        }

        public static void Build(PuzzleModel model, ShikakuRegion room, float cellPixels,
            List<FloorplanFurniturePlacement> output)
        {
            output.Clear();
            if (model == null || room == null || !room.IsValid || room.Area <= 1 ||
                cellPixels < MinimumCellPixels) return;

            Rect clue = ClueBounds(model, room);
            Rect interior = new Rect(WallInset, WallInset,
                room.Width - 2 * WallInset, room.Height - 2 * WallInset);
            uint seed = Seed(room, clue);
            bool narrow = room.Width == 1 || room.Height == 1;
            RoomTemplate template = NarrowTemplates[seed % (uint)NarrowTemplates.Length];
            if (!narrow)
            {
                // Walk a stable catalog order to find a family appropriate to the room's size.
                int start = (int)(seed % (uint)Templates.Length);
                for (int i = 0; i < Templates.Length; i++)
                {
                    RoomTemplate candidate = Templates[(start + i) % Templates.Length];
                    if (room.Area < candidate.MinimumArea) continue;
                    template = candidate;
                    break;
                }
            }

            float maximumScale = !narrow && room.Area >= 12 ? 1.25f : 1f;
            FindPrimary(template, interior, clue, seed, maximumScale, out var main, out float scale);
            if (scale < .32f || Mathf.Min(main.Bounds.width, main.Bounds.height) * cellPixels < 8f) return;
            output.Add(main);

            // Dense boards keep a single readable silhouette. Never enlarge props to a fixed pixel size.
            if (narrow || cellPixels < AccessoryCellPixels || scale < .55f) return;
            AddAccessory(template.Secondary, template.SecondarySize, interior, clue, seed, cellPixels, output);
            Vector2 accentSize = template.Accent == FloorplanFurnitureKind.Toilet
                ? new Vector2(.54f, .72f) : new Vector2(.48f, .48f);
            AddAccessory(template.Accent, accentSize, interior, clue, seed + 3, cellPixels, output);
        }

        private static void FindPrimary(RoomTemplate template, Rect interior, Rect clue, uint seed,
            float maximumScale, out FloorplanFurniturePlacement best, out float bestScale)
        {
            best = default; bestScale = 0;
            float bestSpace = 0;
            // Four maximal rectangles around the full clue cell, in both furniture orientations.
            for (int i = 0; i < 4; i++)
            {
                Rect space;
                switch ((i + (int)(seed % 4)) % 4)
                {
                    case 0: space = Rect.MinMaxRect(interior.xMin, interior.yMin, clue.xMin - Clearance, interior.yMax); break;
                    case 1: space = Rect.MinMaxRect(clue.xMax + Clearance, interior.yMin, interior.xMax, interior.yMax); break;
                    case 2: space = Rect.MinMaxRect(interior.xMin, interior.yMin, interior.xMax, clue.yMin - Clearance); break;
                    default: space = Rect.MinMaxRect(interior.xMin, clue.yMax + Clearance, interior.xMax, interior.yMax); break;
                }
                if (space.width <= 0 || space.height <= 0) continue;
                for (int orientation = 0; orientation < 2; orientation++)
                {
                    bool rotated = orientation == 1;
                    Vector2 size = rotated ? new Vector2(template.Size.y, template.Size.x) : template.Size;
                    float scale = Mathf.Min(maximumScale, space.width / size.x, space.height / size.y);
                    float area = space.width * space.height;
                    if (scale < bestScale || (Mathf.Approximately(scale, bestScale) && area <= bestSpace)) continue;
                    bestScale = scale; bestSpace = area;
                    Vector2 fitted = size * scale;
                    best = new FloorplanFurniturePlacement(template.Primary,
                        new Rect(space.center - fitted * .5f, fitted), rotated);
                }
            }
        }

        private static void AddAccessory(FloorplanFurnitureKind kind, Vector2 size, Rect interior,
            Rect clue, uint seed, float cellPixels, List<FloorplanFurniturePlacement> output)
        {
            // Keep accessories at a consistent cell-relative size; rotate, but never squeeze them to fit.
            if (Mathf.Min(size.x, size.y) * cellPixels < 12f) return;
            for (int orientation = 0; orientation < 2; orientation++)
            {
                Vector2 fitted = orientation == 0 ? size : new Vector2(size.y, size.x);
                if (interior.width < fitted.x || interior.height < fitted.y) continue;
                for (int i = 0; i < 9; i++)
                {
                    int slot = (i + (int)(seed % 9)) % 9;
                    var bounds = new Rect(Mathf.Lerp(interior.xMin, interior.xMax - fitted.x, (slot % 3) * .5f),
                        Mathf.Lerp(interior.yMin, interior.yMax - fitted.y, (slot / 3) * .5f), fitted.x, fitted.y);
                    var padded = new Rect(bounds.x - Clearance, bounds.y - Clearance,
                        bounds.width + 2 * Clearance, bounds.height + 2 * Clearance);
                    if (padded.Overlaps(clue)) continue;
                    bool overlaps = false;
                    foreach (var item in output) if (padded.Overlaps(item.Bounds)) { overlaps = true; break; }
                    if (overlaps) continue;
                    output.Add(new FloorplanFurniturePlacement(kind, bounds, orientation == 1));
                    return;
                }
            }
        }

        private static uint Seed(ShikakuRegion room, Rect clue)
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)room.X) * 16777619;
                h = (h ^ (uint)room.Y) * 16777619;
                h = (h ^ (uint)room.Width) * 16777619;
                h = (h ^ (uint)room.Height) * 16777619;
                h = (h ^ (uint)(clue.x + clue.y * room.Width)) * 16777619;
                return h ^ (h >> 16);
            }
        }
    }
}