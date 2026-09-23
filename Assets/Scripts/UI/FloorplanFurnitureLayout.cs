using System.Collections.Generic;
using Shikaku.Logic;
using UnityEngine;

namespace Shikaku.UI
{
    public enum FloorplanFurnitureKind
    {
        Bed, Sofa, Desk, Cabinet, Armchair, Table, Plant,
        DiningSet, KitchenCounter, Bathtub, Basin, Toilet, Bookcase, Washer, Bench,
        Crib, ChangingTable, RockingChair, GrandfatherClock, TvConsole, Wardrobe, ToyChest, BreakfastBar,
        DoubleWorkstation, FilingCabinet, PrinterStand, UprightPiano, RecordCabinet, BoxShelves, CoatRack, LaundryHamper, FilledBookshelf, TallBookshelf
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
            public readonly int MinimumArea, MinimumShortSide;
            public RoomTemplate(FloorplanFurnitureKind primary, float width, float height,
                FloorplanFurnitureKind secondary, float secondaryWidth, float secondaryHeight,
                FloorplanFurnitureKind accent = FloorplanFurnitureKind.Plant, int minimumArea = 4, int minimumShortSide = 1)
            {
                Primary = primary; Size = new Vector2(width, height);
                Secondary = secondary; SecondarySize = new Vector2(secondaryWidth, secondaryHeight);
                Accent = accent; MinimumArea = minimumArea; MinimumShortSide = minimumShortSide;
            }
        }

        // Room families and size-gated nursery, entertainment, bedroom, and breakfast variants.
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
            new(FloorplanFurnitureKind.Bookcase, 1.8f, .65f, FloorplanFurnitureKind.Cabinet, .75f, .55f),
            new(FloorplanFurnitureKind.Crib, 2.0f, 1.55f, FloorplanFurnitureKind.ChangingTable, .95f, .66f,
                FloorplanFurnitureKind.ToyChest, minimumArea: 12, minimumShortSide: 3),
            new(FloorplanFurnitureKind.Crib, 2.0f, 1.55f, FloorplanFurnitureKind.RockingChair, 1.1f, 1.35f,
                FloorplanFurnitureKind.ToyChest, minimumArea: 12, minimumShortSide: 3),
            new(FloorplanFurnitureKind.TvConsole, 1.9f, .8f, FloorplanFurnitureKind.Sofa, 1.65f, 1.0f,
                FloorplanFurnitureKind.Table, minimumArea: 6),
            new(FloorplanFurnitureKind.Wardrobe, 1.6f, .75f, FloorplanFurnitureKind.Cabinet, .7f, .55f),
            new(FloorplanFurnitureKind.Bed, 1.45f, 2.05f, FloorplanFurnitureKind.Wardrobe, 1.0f, .6f,
                FloorplanFurnitureKind.ToyChest, minimumArea: 8),
            new(FloorplanFurnitureKind.BreakfastBar, 2.2f, .85f, FloorplanFurnitureKind.KitchenCounter, 1.15f, .5f,
                minimumArea: 6),
            new(FloorplanFurnitureKind.GrandfatherClock, .66f, 1.25f, FloorplanFurnitureKind.Cabinet, .8f, .48f),
            new(FloorplanFurnitureKind.DoubleWorkstation, 2.6f, 1.5f, FloorplanFurnitureKind.FilingCabinet, .62f, .85f,
                FloorplanFurnitureKind.PrinterStand, minimumArea: 10, minimumShortSide: 2),
            new(FloorplanFurnitureKind.UprightPiano, 1.95f, 1.55f, FloorplanFurnitureKind.RecordCabinet, 1.05f, .6f,
                minimumArea: 6, minimumShortSide: 2),
            new(FloorplanFurnitureKind.BoxShelves, 1.8f, .85f, FloorplanFurnitureKind.CoatRack, .6f, .9f,
                FloorplanFurnitureKind.LaundryHamper),
            new(FloorplanFurnitureKind.TallBookshelf, .84f, 1.75f, FloorplanFurnitureKind.TallBookshelf, .84f, 1.75f,
                FloorplanFurnitureKind.TallBookshelf, minimumArea: 12, minimumShortSide: 3)
        };
        private static readonly RoomTemplate[] NarrowTemplates = {
            new(FloorplanFurnitureKind.Cabinet, 1.5f, .56f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.Bookcase, 1.6f, .55f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.Bench, 1.8f, .62f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.GrandfatherClock, .66f, 1.25f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.TvConsole, 1.85f, .65f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.BreakfastBar, 2.1f, .65f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.Wardrobe, 1.5f, .6f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.BoxShelves, 1.6f, .65f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.CoatRack, .6f, 1.15f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.LaundryHamper, .62f, .72f, FloorplanFurnitureKind.Plant, .48f, .48f),
            new(FloorplanFurnitureKind.FilledBookshelf, 1.65f, .68f, FloorplanFurnitureKind.Plant, .48f, .48f)
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
                    if (room.Area < candidate.MinimumArea || Mathf.Min(room.Width, room.Height) < candidate.MinimumShortSide) continue;
                    template = candidate;
                    break;
                }
            }

            if (!narrow && template.Primary == FloorplanFurnitureKind.TallBookshelf)
            {
                if (BuildLibrary(interior, clue, seed, cellPixels, output)) return;
                template = Templates[6]; // A reading room when a complete shelf bank cannot fit.
            }
            float maximumScale = !narrow && room.Area >= 12 ? 1.25f : 1f;
            FindPrimary(template, interior, clue, seed, maximumScale, out var main, out float scale);
            if (template.Primary == FloorplanFurnitureKind.Crib)
            {
                // Check nursery capacity in cell units, independent of screen detail level.
                // A nursery needs a full-size crib and at least one supporting piece.
                output.Add(main);
                AddAccessory(template.Secondary, template.SecondarySize, interior, clue, seed, float.PositiveInfinity, output);
                AddAccessory(template.Accent, AccentSize(template.Accent), interior, clue, seed + 3, float.PositiveInfinity, output);
                bool cramped = scale < .85f || output.Count < 2;
                output.Clear();
                if (cramped)
                {
                    template = Templates[6]; // Compact reading-room fallback.
                    FindPrimary(template, interior, clue, seed, maximumScale, out main, out scale);
                }
            }
            if (scale < .32f || Mathf.Min(main.Bounds.width, main.Bounds.height) * cellPixels < 8f) return;
            output.Add(main);

            // Dense boards keep a single readable silhouette. Never enlarge props to a fixed pixel size.
            if (narrow || cellPixels < AccessoryCellPixels || scale < .55f) return;
            AddAccessory(template.Secondary, template.SecondarySize, interior, clue, seed, cellPixels, output);
            Vector2 accentSize = AccentSize(template.Accent);
            AddAccessory(template.Accent, accentSize, interior, clue, seed + 3, cellPixels, output);
        }

        private static bool BuildLibrary(Rect interior, Rect clue, uint seed, float cellPixels,
            List<FloorplanFurniturePlacement> output)
        {
            const float width = .84f, height = 1.75f, gap = .04f;
            // Fit the entire bank at once. All cases have the same baseline, scale and facing;
            // a clue can move the bank, but cannot scatter or rotate individual bookcases.
            for (int count = 5; count >= 3; count--)
            {
                var bank = new RoomTemplate(FloorplanFurnitureKind.TallBookshelf,
                    count * width + (count - 1) * gap, height, FloorplanFurnitureKind.TallBookshelf, width, height);
                FindPrimary(bank, interior, clue, seed, 1.1f, out var row, out float scale, allowRotation: false);
                if (scale < .8f) continue;
                if (width * scale * cellPixels < 8f) return true;
                for (int i = 0; i < count; i++)
                    output.Add(new FloorplanFurniturePlacement(FloorplanFurnitureKind.TallBookshelf,
                        new Rect(row.Bounds.x + i * (width + gap) * scale, row.Bounds.y,
                            width * scale, height * scale)));
                // Dense boards simplify each bookcase's mesh, keeping the bank together.
                return true;
            }
            return false;
        }
        private static Vector2 AccentSize(FloorplanFurnitureKind kind)
        {
            if (kind == FloorplanFurnitureKind.PrinterStand) return new Vector2(.78f, .68f);
            if (kind == FloorplanFurnitureKind.LaundryHamper) return new Vector2(.64f, .64f);
            if (kind == FloorplanFurnitureKind.Toilet) return new Vector2(.54f, .72f);
            if (kind == FloorplanFurnitureKind.ToyChest) return new Vector2(.8f, .58f);
            return new Vector2(.48f, .48f);
        }

        private static void FindPrimary(RoomTemplate template, Rect interior, Rect clue, uint seed,
            float maximumScale, out FloorplanFurniturePlacement best, out float bestScale, bool allowRotation = true)
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
                for (int orientation = 0; orientation < (allowRotation ? 2 : 1); orientation++)
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