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
            _edges.Clear();
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
                int start = mesh.currentVertCount;
                Color32 ink = EdgeColor(edge);
                mesh.AddVert(new Vector3(left,bottom),ink,Vector2.zero);
                mesh.AddVert(new Vector3(left,top),ink,Vector2.zero);
                mesh.AddVert(new Vector3(right,top),ink,Vector2.zero);
                mesh.AddVert(new Vector3(right,bottom),ink,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);
                mesh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
