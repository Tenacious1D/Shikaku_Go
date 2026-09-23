using System.Collections.Generic;
using Shikaku.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace Shikaku.UI
{
    public readonly struct FloorplanEdge
    {
        public readonly int X, Y, FirstRegion, SecondRegion;
        public readonly bool Horizontal;
        public bool Perimeter => FirstRegion == -2 || SecondRegion == -2;
        public FloorplanEdge(int x, int y, bool horizontal, int firstRegion, int secondRegion)
        { X = x; Y = y; Horizontal = horizontal; FirstRegion = firstRegion; SecondRegion = secondRegion; }
    }

    /// <summary>One entry per physical cell edge. Uses player placements, never solution data.</summary>
    public static class FloorplanEdges
    {
        public static void Build(PuzzleModel model, List<FloorplanEdge> output)
        {
            output.Clear();
            if (model == null) return;
            int Region(int x, int y) => x < 0 || y < 0 || x >= model.Width || y >= model.Height
                ? -2 : model.GetRegionIdAt(y * model.Width + x);
            void Add(int x, int y, bool horizontal, int first, int second)
            {
                // Equal ownership includes unassigned space and pairs of masked cells.
                if (first != second) output.Add(new FloorplanEdge(x, y, horizontal, first, second));
            }
            for (int y = 0; y <= model.Height; y++)
                for (int x = 0; x < model.Width; x++)
                    Add(x, y, true, Region(x, y - 1), Region(x, y));
            for (int x = 0; x <= model.Width; x++)
                for (int y = 0; y < model.Height; y++)
                    Add(x, y, false, Region(x - 1, y), Region(x, y));
        }
    }

    /// <summary>Single noninteractive wall mesh, above room floors and below drag feedback.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorplanWallGraphic : MaskableGraphic
    {
        private readonly List<FloorplanEdge> _edges = new(256);
        private readonly Dictionary<int, Color> _hintColors = new();
        private readonly List<int> _staleHints = new();
        private readonly FloorplanOpeningLayout _openingLayout = new();
        private readonly List<FloorplanOpening> _openings = new(64);
        private readonly Dictionary<(int, int, bool), FloorplanOpening> _openingByEdge = new();
        private float _cellPixels;
        public IReadOnlyList<FloorplanOpening> Openings => _openings;
        private PuzzleModel _model;
        private BlueprintThemeAssets _theme;
        private bool _dark;
        private float[] _x, _y;
        private float _thickness;
        public IReadOnlyList<FloorplanEdge> Edges => _edges;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void SetBoard(PuzzleModel model, CellView[] cells, BlueprintThemeAssets theme, bool dark)
        {
            _model = model; _theme = theme; _dark = dark;
            _edges.Clear(); _openings.Clear(); _openingByEdge.Clear();
            if (model == null || cells == null || cells.Length != model.Width * model.Height ||
                cells.Length == 0 || cells[0] == null)
            { SetVerticesDirty(); return; }
            if (_x == null || _x.Length != model.Width + 1) _x = new float[model.Width + 1];
            if (_y == null || _y.Length != model.Height + 1) _y = new float[model.Height + 1];
            Bounds BoundsAt(int index) => BlueprintRoomDecorationLayer.CalculateRectBounds(
                rectTransform, cells[index].transform as RectTransform);
            Bounds first = BoundsAt(0);
            _x[0] = first.min.x;
            _y[0] = first.max.y;
            for (int x = 1; x < model.Width; x++)
                _x[x] = (BoundsAt(x - 1).max.x + BoundsAt(x).min.x) * 0.5f;
            for (int y = 1; y < model.Height; y++)
                _y[y] = (BoundsAt((y - 1) * model.Width).min.y + BoundsAt(y * model.Width).max.y) * 0.5f;
            _x[model.Width] = BoundsAt(model.Width - 1).max.x;
            _y[model.Height] = BoundsAt((model.Height - 1) * model.Width).min.y;
            _thickness = Mathf.Clamp(Mathf.Min(first.size.x, first.size.y) * 0.055f, 2.4f, 5.5f);
            FloorplanEdges.Build(model, _edges);
            _openingLayout.Build(model, _edges, _openings);
            foreach (var opening in _openings)
                _openingByEdge[(opening.Edge.X, opening.Edge.Y, opening.Edge.Horizontal)] = opening;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(Vector3.zero));
            _cellPixels = Mathf.Min(
                Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(new Vector3(first.size.x, 0)))),
                Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(new Vector3(0, first.size.y)))));
            _staleHints.Clear();
            foreach (int id in _hintColors.Keys)
                if (!model.TryGetRegion(id, out _)) _staleHints.Add(id);
            foreach (int id in _staleHints) _hintColors.Remove(id);
            SetVerticesDirty();
        }

        public void SetHintColor(int regionId, Color color)
        { _hintColors[regionId] = color; SetVerticesDirty(); }
        public void ClearHintColor(int regionId)
        { if (_hintColors.Remove(regionId)) SetVerticesDirty(); }

        private Color EdgeColor(FloorplanEdge edge)
        {
            bool Invalid(int id) => id >= 0 && _model.TryGetRegion(id, out var region) && !region.IsValid;
            if (Invalid(edge.FirstRegion) || Invalid(edge.SecondRegion)) return _theme.invalidInk;
            if (_hintColors.TryGetValue(edge.FirstRegion, out Color first)) return first;
            if (_hintColors.TryGetValue(edge.SecondRegion, out Color second)) return second;
            int selected = _model.SelectedRegionId;
            if (selected >= 0 && (edge.FirstRegion == selected || edge.SecondRegion == selected))
                return _theme.GetFloorplanSelectionColor(_dark);
            return _theme.GetFloorplanWallColor(_dark);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_model == null || _theme == null || _x == null || _y == null) return;
            foreach (var edge in _edges)
            {
                float width = _thickness * (edge.Perimeter ? 1.65f : 1f);
                float left, right, bottom, top;
                if (edge.Horizontal)
                {
                    left = _x[edge.X]; right = _x[edge.X + 1];
                    bottom = _y[edge.Y] - width * 0.5f; top = bottom + width;
                    if (edge.Perimeter)
                    {
                        // Perimeter ink belongs entirely to the playable side, including holes.
                        bottom = _y[edge.Y] - (edge.FirstRegion == -2 ? width : 0);
                        top = bottom + width;
                    }
                }
                else
                {
                    bottom = _y[edge.Y + 1]; top = _y[edge.Y];
                    left = _x[edge.X] - width * 0.5f; right = left + width;
                    if (edge.Perimeter)
                    {
                        left = _x[edge.X] - (edge.FirstRegion == -2 ? 0 : width);
                        right = left + width;
                    }
                }
                Color ink = EdgeColor(edge);
                if (_cellPixels >= FloorplanOpeningLayout.MinimumCellPixels &&
                    _openingByEdge.TryGetValue((edge.X, edge.Y, edge.Horizontal), out var opening))
                {
                    float a = edge.Horizontal ? Mathf.Lerp(left, right, FloorplanOpeningLayout.Start) : Mathf.Lerp(top, bottom, FloorplanOpeningLayout.Start);
                    float b = edge.Horizontal ? Mathf.Lerp(left, right, FloorplanOpeningLayout.End) : Mathf.Lerp(top, bottom, FloorplanOpeningLayout.End);
                    if (edge.Horizontal)
                    {
                        Quad(mesh, left, bottom, a, top, ink);
                        Quad(mesh, b, bottom, right, top, ink);
                    }
                    else
                    {
                        Quad(mesh, left, a, right, top, ink);
                        Quad(mesh, left, bottom, right, b, ink);
                    }
                    DrawOpening(mesh, opening, left, bottom, right, top, ink);
                }
                else Quad(mesh, left, bottom, right, top, ink);
            }
        }
        private void DrawOpening(VertexHelper mesh, FloorplanOpening opening,
            float left, float bottom, float right, float top, Color ink)
        {
            var edge = opening.Edge;
            Vector2 along = edge.Horizontal ? Vector2.right : Vector2.down;
            Vector2 inward = (edge.Horizontal ? Vector2.down : Vector2.right) * opening.Inward;
            Vector2 start = edge.Horizontal ? new Vector2(left, (top + bottom) * .5f) : new Vector2((left + right) * .5f, top);
            float cell = edge.Horizontal ? right - left : top - bottom;
            Vector2 hinge = start + along * (cell * FloorplanOpeningLayout.Start);
            float length = cell * (FloorplanOpeningLayout.End - FloorplanOpeningLayout.Start);
            Vector2 end = hinge + along * length;
            float fine = Mathf.Max(.8f, _thickness * .27f);
            // A thin threshold preserves the exact puzzle boundary through every decorative opening.
            Line(mesh, hinge, end, fine, ink);
            if (opening.Kind == FloorplanOpeningKind.Window)
            {
                float depth = edge.Horizontal ? top - bottom : right - left;
                Color glass = _dark ? new Color32(116, 187, 201, 255) : new Color32(124, 178, 190, 255);
                Line(mesh, hinge, end, depth * .52f, glass);
                Line(mesh, hinge + inward * depth * .38f, end + inward * depth * .38f, fine, ink);
                Line(mesh, hinge - inward * depth * .38f, end - inward * depth * .38f, fine, ink);
                Vector2 center = (hinge + end) * .5f;
                Line(mesh, center - inward * depth * .5f, center + inward * depth * .5f, fine, ink);
            }
            else
            {
                float acrossCell = edge.Horizontal ? _y[edge.Y- (opening.Inward < 0 ? 1 : 0)] - _y[edge.Y + (opening.Inward > 0 ? 1 : 0)]
                    : _x[edge.X + (opening.Inward > 0 ? 1 : 0)] - _x[edge.X - (opening.Inward < 0 ? 1 : 0)];
                if (opening.Kind == FloorplanOpeningKind.SlidingDoor)
                {
                    Vector2 offset = inward * (acrossCell * .065f);
                    // A compact pocket-door leaf parallel to its track, entirely in the wall margin.
                    Line(mesh, hinge + offset, end + offset, fine * 1.6f, ink);
                    Line(mesh, end + offset * .5f, end + offset * 1.3f, fine, ink);
                    return;
                }
                float depth = acrossCell * (FloorplanOpeningLayout.End - FloorplanOpeningLayout.Start);
                Line(mesh, hinge, hinge + inward * depth, fine * 1.6f, ink);
                if (_cellPixels >= 44f)
                {
                    Color arc = ink; arc.a *= .52f;
                    Vector2 previous = end;
                    for (int i = 1; i <= 10; i++)
                    {
                        float angle = i * Mathf.PI * .05f;
                        Vector2 next = hinge + along * (Mathf.Cos(angle) * length) + inward * (Mathf.Sin(angle) * depth);
                        Line(mesh, previous, next, fine, arc); previous = next;
                    }
                }
            }
        }

        private static void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left,bottom),color,Vector2.zero);
            mesh.AddVert(new Vector3(left,top),color,Vector2.zero);
            mesh.AddVert(new Vector3(right,top),color,Vector2.zero);
            mesh.AddVert(new Vector3(right,bottom),color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2); mesh.AddTriangle(first,first+2,first+3);
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = b-a;
            Vector2 normal = new Vector2(-direction.y,direction.x).normalized * width * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero); mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero); mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2); mesh.AddTriangle(first,first+2,first+3);
        }
    }
}
