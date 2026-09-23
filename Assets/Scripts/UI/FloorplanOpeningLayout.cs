using System.Collections.Generic;
using Shikaku.Logic;
using UnityEngine;

namespace Shikaku.UI
{
    public enum FloorplanOpeningKind { Door, Window, SlidingDoor }

    public readonly struct FloorplanOpening
    {
        public readonly FloorplanEdge Edge;
        public readonly FloorplanOpeningKind Kind;
        // +1 means the room below/right of the edge; -1 means above/left.
        public readonly int Inward;
        public readonly Rect SwingBounds;
        public FloorplanOpening(FloorplanEdge edge, FloorplanOpeningKind kind, int inward, Rect swingBounds)
        { Edge = edge; Kind = kind; Inward = inward; SwingBounds = swingBounds; }
    }

    /// <summary>Decorative openings inferred only from valid player placements, never solution data.</summary>
    public sealed class FloorplanOpeningLayout
    {
        public const float Start = .22f, End = .78f;
        public const float MinimumCellPixels = 36f;
        private readonly HashSet<(int, int)> _doors = new();
        private readonly HashSet<(int, int)> _windowSides = new();
        private readonly Dictionary<int, List<FloorplanFurniturePlacement>> _furniture = new();
        private readonly Stack<List<FloorplanFurniturePlacement>> _pool = new();

        public void Build(PuzzleModel model, IReadOnlyList<FloorplanEdge> edges, List<FloorplanOpening> output)
        {
            output.Clear(); _doors.Clear(); _windowSides.Clear();
            foreach (var items in _furniture.Values) { items.Clear(); _pool.Push(items); }
            _furniture.Clear();
            if (model == null) return;
            foreach (var edge in edges)
            {
                bool firstValid = model.TryGetRegion(edge.FirstRegion, out var first) && first.IsValid;
                bool secondValid = model.TryGetRegion(edge.SecondRegion, out var second) && second.IsValid;
                if (firstValid && secondValid)
                {
                    var pair = (Mathf.Min(first.Id, second.Id), Mathf.Max(first.Id, second.Id));
                    if (_doors.Contains(pair)) continue;
                    // Prefer the same physical side regardless of region creation order.
                    if (TryDoor(model, edge, second, 1, out var door) ||
                        TryDoor(model, edge, first, -1, out door) ||
                        TryDoor(model, edge, second, 1, out door, sliding: true) ||
                        TryDoor(model, edge, first, -1, out door, sliding: true))
                    { output.Add(door); _doors.Add(pair); }
                }
                else if (edge.Perimeter && (firstValid || secondValid))
                {
                    // Windows belong only to the exterior, never masked courtyards or unfinished rooms.
                    bool outer = edge.Horizontal ? edge.Y == 0 || edge.Y == model.Height : edge.X == 0 || edge.X == model.Width;
                    if (!outer) continue;
                    var room = firstValid ? first : second;
                    if (room.Area < 2) continue;
                    int side = edge.Horizontal ? (edge.Y == 0 ? 0 : 1) : (edge.X == 0 ? 2 : 3);
                    if (_windowSides.Contains((room.Id, side))) continue;
                    int x = edge.Horizontal ? edge.X : edge.X == 0 ? 0 : edge.X - 1;
                    int y = edge.Horizontal ? edge.Y == 0 ? 0 : edge.Y - 1 : edge.Y;
                    if (model.GivenNumber[y * model.Width + x] > 0) continue;
                    output.Add(new FloorplanOpening(edge, FloorplanOpeningKind.Window, firstValid ? -1 : 1, default));
                    _windowSides.Add((room.Id, side));
                }
            }
        }

        private bool TryDoor(PuzzleModel model, FloorplanEdge edge, ShikakuRegion room, int inward,
            out FloorplanOpening door, bool sliding = false)
        {
            door = default;
            const float radius = End - Start;
            float depth = sliding ? .09f : radius;
            Rect swing = edge.Horizontal
                ? new Rect(edge.X + Start, edge.Y + (inward < 0 ? -depth : 0), radius, depth)
                : new Rect(edge.X + (inward < 0 ? -depth : 0), edge.Y + Start, depth, radius);
            Rect local = new Rect(swing.position - new Vector2(room.X, room.Y), swing.size);
            if (local.Overlaps(FloorplanFurnitureLayout.ClueBounds(model, room))) return false;
            if (!_furniture.TryGetValue(room.Id, out var furniture))
            {
                furniture = _pool.Count > 0 ? _pool.Pop() : new List<FloorplanFurniturePlacement>(5);
                // Reserve all furniture even when the current screen hides accessories or the toggle is off.
                FloorplanFurnitureLayout.Build(model, room, float.PositiveInfinity, furniture);
                _furniture.Add(room.Id, furniture);
            }
            Rect padded = new Rect(local.x - .04f, local.y - .04f, local.width + .08f, local.height + .08f);
            foreach (var item in furniture) if (padded.Overlaps(item.Bounds)) return false;
            door = new FloorplanOpening(edge, sliding ? FloorplanOpeningKind.SlidingDoor : FloorplanOpeningKind.Door, inward, swing);
            return true;
        }
    }
}