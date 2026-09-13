using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI.Buildings
{
    // One retained UI element per building, no GameObjects or idle Update loop.
    // Both map and solved modal use exactly this mesh and art pipeline.
    public sealed class BuildingView : VisualElement
    {
        private readonly struct Quad
        {
            public readonly Vector2 A, B, C, D;
            public readonly Color Color;
            public readonly Texture2D Texture;
            public readonly Rect UV;
            public Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, Texture2D texture, Rect? uv = null)
            { A = a; B = b; C = c; D = d; Color = color; Texture = texture; UV = uv ?? new Rect(0, 0, 1, 1); }
        }


        private readonly List<Quad> _quads = new List<Quad>(512);
        private AdventureBuildingData _data;
        private bool[] _completed = Array.Empty<bool>();
        private Rect _bounds;
        private IVisualElementScheduledItem _animation;
        private int _animatedFloor = -1;
        private float _animationStart;
        private float _animationProgress = 1;
        private float _scale;
        private Vector2 _offset;
        public event Action<int> FloorConstructed;
        public event Action ChapterCompleted;
        public int CompletedFloors
        {
            get
            {
                int count = 0;
                foreach (bool completed in _completed) if (completed) count++;
                return count;
            }
        }
        public bool IsAnimating => _animation != null;
        public bool IsComplete => _completed.Length > 0 && CompletedFloors == _completed.Length;
        private BuildingPalette Palette => _data.Appearance;

        public BuildingView()
        {
            AddToClassList("building-view");
            pickingMode = PickingMode.Ignore;
            style.flexShrink = 0;
            generateVisualContent += Draw;
            RegisterCallback<DetachFromPanelEvent>(_ => FinishConstruction());
        }

        public void SetBuilding(AdventureBuildingData data)
        {
            FinishConstruction();
            _data = data;
            _completed = data == null ? Array.Empty<bool>() : new bool[data.Floors.Count];
            if (data != null) _bounds = BuildingGeometry.Bounds(data);
            Refresh();
        }

        public void SetProgress(int completedFloors)
        {
            FinishConstruction();
            for (int i = 0; i < _completed.Length; i++)
                _completed[i] = i < Mathf.Clamp(completedFloors, 0, _completed.Length);
            Refresh();
        }

        public void SetCompletedFloors(IReadOnlyList<bool> completion)
        {
            if (completion == null || completion.Count != _completed.Length)
                throw new ArgumentException("Completion must match the chapter floor sequence.");
            FinishConstruction();
            for (int i = 0; i < _completed.Length; i++) _completed[i] = completion[i];
            Refresh();
        }

        // Call with the exact newly completed puzzle index, not total - 1.
        // Replays are deliberately idempotent.
        public bool AnimateFloor(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex >= _completed.Length || _completed[floorIndex] || floorIndex == _animatedFloor)
                return false;
            FinishConstruction();
            _animatedFloor = floorIndex;
            _animationProgress = 0;
            _animationStart = -1;
            _animation = schedule.Execute(TickConstruction).Every(16).StartingIn(220);
            Refresh();
            return true;
        }

        private void TickConstruction()
        {
            if (_animationStart < 0) _animationStart = Time.realtimeSinceStartup;
            _animationProgress = Mathf.Clamp01((Time.realtimeSinceStartup - _animationStart) / 0.65f);
            if (_animationProgress >= 1)
            {
                int floor = _animatedFloor;
                FinishConstruction();
                FloorConstructed?.Invoke(floor);
                if (IsComplete) ChapterCompleted?.Invoke();
            }
            else Refresh();
        }

        // Finishes only the presentation; completion is already saved by HUD.
        // Safe for fast Next/Exit, dismissal, reuse, and panel detachment.
        public void FinishConstruction()
        {
            _animation?.Pause();
            _animation = null;
            if (_animatedFloor >= 0 && _animatedFloor < _completed.Length)
                _completed[_animatedFloor] = true;
            _animatedFloor = -1;
            _animationProgress = 1;
            Refresh();
        }

        public void Refresh() => MarkDirtyRepaint();

        private void Draw(MeshGenerationContext context)
        {
            if (_data == null || contentRect.width <= 0 || contentRect.height <= 0) return;
            const float padding = 10;
            _scale = Mathf.Min(Mathf.Max(1, contentRect.width - padding * 2) / _bounds.width,
                Mathf.Max(1, contentRect.height - padding * 2) / _bounds.height);
            _offset = new Vector2(contentRect.center.x - _bounds.center.x * _scale,
                contentRect.yMax - padding - _bounds.yMax * _scale);
            _quads.Clear();
            var p = Palette;
            bool complete = IsComplete;
            foreach (var patch in _data.Patches)
            {
                int i = patch.FloorIndex;
                bool foundation = i < 0;
                bool animated = i == _animatedFloor && _animationStart >= 0;
                if (!foundation && !_completed[i] && !animated) continue;
                float lift = animated
                    ? BuildingGeometry.ConstructionLift * Mathf.Pow(1 - _animationProgress, 3) +
                        Mathf.Sin(_animationProgress * Mathf.PI * 3) * (1 - _animationProgress) * 0.07f : 0;
                float alpha = animated ? Mathf.Clamp01(_animationProgress * 5) : 1;
                float bottom = foundation ? 0 : BuildingGeometry.FoundationHeight + i * BuildingGeometry.FloorHeight + lift;
                float top = foundation ? BuildingGeometry.FoundationHeight : bottom + BuildingGeometry.FloorHeight;
                Color front = foundation ? p.foundation : p.wall;
                Color side = foundation ? p.foundation : p.sideWall;
                Texture2D wall = foundation ? p.foundationTile : p.wallModule;
                DrawPatchWalls(patch, bottom, top, front, side, wall, alpha);
                if (!foundation) WindowsAndEdges(patch, bottom, top, alpha);

                // Skip covered terraces. Alignment uses world coordinates even
                // when the next floor has different dimensions or a different mask.
                int above = foundation ? 0 : i + 1;
                bool covered = above < _completed.Length && _completed[above] &&
                    _data.Floors[above].ContainsWorld(patch.Area.center.x, patch.Area.center.y);
                if (!covered)
                {
                    Top(patch.Area, top, foundation ? p.foundation : p.terrace,
                        foundation ? p.foundationTile : p.terraceTile, alpha, TileUV(patch));
                    if (foundation && !_completed[0]) BlueprintGrid(patch, top + 0.001f, p.blueprint);
                }
                if (!foundation && complete && i == _data.Floors.Count - 1)
                {
                    float roofHeight = p.architecture == BuildingArchitecture.ArtDeco ? 0.25f :
                        p.architecture == BuildingArchitecture.Warehouse ? 0.09f : BuildingGeometry.RoofHeight;
                    DrawPatchWalls(patch, top, top + roofHeight, p.roof, p.roof, p.edgeModule, 1);
                    Top(patch.Area, top + roofHeight, p.roof, p.roofTile, 1, TileUV(patch));
                    if (p.architecture == BuildingArchitecture.Modern || p.architecture == BuildingArchitecture.ArtDeco)
                        RoofRim(patch, _data.Floors[i], top + roofHeight + 0.005f, p.terrace);
                    // Decorate an occupied unit, never span a courtyard or notch.
                    if (p.roofDecoration != null && patch.Cell.Contains(Vector2.zero))
                        Top(patch.Area, top + roofHeight + 0.01f, Color.white, p.roofDecoration, 1, TileUV(patch));
                }
            }
            Flush(context);
        }

        private static Rect TileUV(BuildingGeometry.Patch patch) => new Rect(
            patch.Area.xMin - patch.Cell.xMin, patch.Area.yMin - patch.Cell.yMin, 0.5f, 0.5f);

        private void DrawPatchWalls(BuildingGeometry.Patch patch, float bottom, float top,
            Color front, Color side, Texture2D texture, float alpha)
        {
            if (patch.Front) FrontSpan(patch, patch.Cell.xMin, patch.Cell.xMax, bottom, top, front, texture, alpha);
            if (patch.Side) SideSpan(patch, patch.Cell.yMin, patch.Cell.yMax, bottom, top, side, texture, alpha);
        }

        private void WindowsAndEdges(BuildingGeometry.Patch patch, float bottom, float top, float alpha)
        {
            var p = Palette;
            float inset = p.architecture == BuildingArchitecture.Modern ? 0.06f :
                p.architecture == BuildingArchitecture.Warehouse ? 0.30f : 0.20f;
            float low = bottom + (p.architecture == BuildingArchitecture.ArtDeco ? 0.12f : 0.23f);
            float high = top - (p.architecture == BuildingArchitecture.Warehouse ? 0.29f : 0.13f);
            if (patch.Front)
            {
                FrontSpan(patch, patch.Cell.xMin + inset, patch.Cell.xMax - inset, low, high, p.window, p.windowModule, alpha);
                FrontSpan(patch, patch.Cell.xMin, patch.Cell.xMin + 0.055f, bottom, top, p.edge, p.edgeModule, alpha);
                FrontSpan(patch, patch.Cell.xMin, patch.Cell.xMax, top - 0.06f, top, p.edge, p.edgeModule, alpha);
                if (p.architecture == BuildingArchitecture.Classic)
                    FrontSpan(patch, patch.Cell.xMin + 0.47f, patch.Cell.xMin + 0.53f, low, high, p.terrace, null, alpha);
                if (p.architecture == BuildingArchitecture.ArtDeco)
                    FrontSpan(patch, patch.Cell.xMin + 0.08f, patch.Cell.xMin + 0.14f, bottom, top, p.terrace, null, alpha);
                if (p.architecture == BuildingArchitecture.Warehouse)
                    FrontSpan(patch, patch.Cell.xMin, patch.Cell.xMax, bottom + 0.10f, bottom + 0.14f, p.edge, null, alpha);
            }
            if (patch.Side)
            {
                SideSpan(patch, patch.Cell.yMin + inset, patch.Cell.yMax - inset, low, high, p.window, p.windowModule, alpha);
                SideSpan(patch, patch.Cell.yMax - 0.055f, patch.Cell.yMax, bottom, top, p.edge, p.edgeModule, alpha);
                SideSpan(patch, patch.Cell.yMin, patch.Cell.yMax, top - 0.06f, top, p.edge, p.edgeModule, alpha);
                if (p.architecture == BuildingArchitecture.Classic)
                    SideSpan(patch, patch.Cell.yMin + 0.47f, patch.Cell.yMin + 0.53f, low, high, p.terrace, null, alpha);
                if (p.architecture == BuildingArchitecture.ArtDeco)
                    SideSpan(patch, patch.Cell.yMax - 0.14f, patch.Cell.yMax - 0.08f, bottom, top, p.terrace, null, alpha);
                if (p.architecture == BuildingArchitecture.Warehouse)
                    SideSpan(patch, patch.Cell.yMin, patch.Cell.yMax, bottom + 0.10f, bottom + 0.14f, p.edge, null, alpha);
            }
        }

        private void FrontSpan(BuildingGeometry.Patch patch, float start, float end,
            float bottom, float top, Color color, Texture2D texture, float alpha)
        {
            float a = Mathf.Max(start, patch.Area.xMin), b = Mathf.Min(end, patch.Area.xMax);
            if (b <= a) return;
            Face(a, patch.Area.yMax, b, patch.Area.yMax, bottom, top, color, texture, alpha,
                new Rect((a - start) / (end - start), 0, (b - a) / (end - start), 1));
        }

        private void SideSpan(BuildingGeometry.Patch patch, float start, float end,
            float bottom, float top, Color color, Texture2D texture, float alpha)
        {
            float a = Mathf.Max(start, patch.Area.yMin), b = Mathf.Min(end, patch.Area.yMax);
            if (b <= a) return;
            Face(patch.Area.xMax, b, patch.Area.xMax, a, bottom, top, color, texture, alpha,
                new Rect((end - b) / (end - start), 0, (b - a) / (end - start), 1));
        }

        private void BlueprintGrid(BuildingGeometry.Patch patch, float z, Color color)
        {
            if (patch.Area.xMin == patch.Cell.xMin) Top(new Rect(patch.Area.xMin, patch.Area.yMin, 0.025f, 0.5f), z, color, null, 1);
            if (patch.Area.yMin == patch.Cell.yMin) Top(new Rect(patch.Area.xMin, patch.Area.yMin, 0.5f, 0.025f), z, color, null, 1);
            if (patch.Front) Top(new Rect(patch.Area.xMin, patch.Area.yMax - 0.025f, 0.5f, 0.025f), z, color, null, 1);
            if (patch.Side) Top(new Rect(patch.Area.xMax - 0.025f, patch.Area.yMin, 0.025f, 0.5f), z, color, null, 1);
        }

        private void RoofRim(BuildingGeometry.Patch patch, AdventureBuildingData.Floor floor, float z, Color color)
        {
            int x = Mathf.FloorToInt(patch.Cell.xMin + floor.Width * 0.5f);
            int y = Mathf.FloorToInt(patch.Cell.yMin + floor.Depth * 0.5f);
            if (patch.Front) Top(new Rect(patch.Area.xMin, patch.Area.yMax - 0.06f, 0.5f, 0.06f), z, color, null, 1);
            if (patch.Side) Top(new Rect(patch.Area.xMax - 0.06f, patch.Area.yMin, 0.06f, 0.5f), z, color, null, 1);
            if (patch.Area.xMin == patch.Cell.xMin && !floor.Contains(x - 1, y))
                Top(new Rect(patch.Area.xMin, patch.Area.yMin, 0.06f, 0.5f), z, color, null, 1);
            if (patch.Area.yMin == patch.Cell.yMin && !floor.Contains(x, y - 1))
                Top(new Rect(patch.Area.xMin, patch.Area.yMin, 0.5f, 0.06f), z, color, null, 1);
        }

        private Vector2 Point(float x, float y, float z)
            => _offset + BuildingGeometry.Project(x, y, z) * _scale;

        private void Top(Rect r, float z, Color color, Texture2D texture, float alpha, Rect? uv = null)
        {
            color.a *= alpha;
            _quads.Add(new Quad(Point(r.xMin, r.yMin, z), Point(r.xMax, r.yMin, z),
                Point(r.xMax, r.yMax, z), Point(r.xMin, r.yMax, z), color, texture, uv));
        }

        private void Face(float x0, float y0, float x1, float y1,
            float bottom, float top, Color color, Texture2D texture, float alpha, Rect? uv = null)
        {
            color.a *= alpha;
            _quads.Add(new Quad(Point(x0, y0, top), Point(x1, y1, top),
                Point(x1, y1, bottom), Point(x0, y0, bottom), color, texture, uv));
        }

        private void Flush(MeshGenerationContext context)
        {
            // Batch consecutive modules sharing a texture, preserving painter order.
            // Placeholder art uses a single mesh, not an element per window/tile.
            for (int start = 0; start < _quads.Count;)
            {
                Texture2D texture = _quads[start].Texture;
                int end = start + 1;
                while (end < _quads.Count && end - start < 16000 && _quads[end].Texture == texture) end++;
                var mesh = context.Allocate((end - start) * 4, (end - start) * 6, texture);

                for (int i = start; i < end; i++)
                {
                    Quad q = _quads[i];
                    Rect uv = q.UV;
                    Vertex(mesh, q.A, q.Color, new Vector2(uv.xMin, uv.yMax));
                    Vertex(mesh, q.B, q.Color, new Vector2(uv.xMax, uv.yMax));
                    Vertex(mesh, q.C, q.Color, new Vector2(uv.xMax, uv.yMin));
                    Vertex(mesh, q.D, q.Color, new Vector2(uv.xMin, uv.yMin));
                    ushort n = (ushort)((i - start) * 4);
                    mesh.SetNextIndex(n); mesh.SetNextIndex((ushort)(n + 1)); mesh.SetNextIndex((ushort)(n + 2));
                    mesh.SetNextIndex((ushort)(n + 2)); mesh.SetNextIndex((ushort)(n + 3)); mesh.SetNextIndex(n);
                }
                start = end;
            }
        }

        private static void Vertex(MeshWriteData mesh, Vector2 point, Color color, Vector2 uv)
        {
            mesh.SetNextVertex(new UnityEngine.UIElements.Vertex
            {
                position = new Vector3(point.x, point.y, UnityEngine.UIElements.Vertex.nearZ),
                tint = color, uv = uv
            });
        }
    }
}
