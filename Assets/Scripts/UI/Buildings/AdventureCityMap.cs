using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI.Buildings
{
    // Streets are one retained drawing behind independent building/button nodes.
    // Plot order is presentation only; Adventure progression remains in Progression.
    public sealed class AdventureCityMap : VisualElement
    {
        private readonly int _chapterCount;
        private readonly List<Plot> _plots = new List<Plot>();
        private sealed class Plot
        {
            public int Index;
            public AdventureBuildingData Data;
            public VisualElement Node;
            public BuildingView Building;
            public Button Button;
        }

        public AdventureCityMap(int chapterCount)
        {
            _chapterCount = chapterCount;
            AddToClassList("adventure-city-map");
            style.width = Length.Percent(100);
            style.position = Position.Relative;
            style.flexShrink = 0;
            generateVisualContent += DrawCity;
            RegisterCallback<GeometryChangedEvent>(_ => LayoutPlots());
            style.height = MapHeight(1);
        }

        private float ViewScale => Mathf.Clamp(contentRect.width / 790f, 0.55f, 1.2f);
        private float MapHeight(float scale)
        {
            int last = Mathf.Max(0, _chapterCount - 1);
            return (380 + (last / 2) * 620 + (last % 2 == 1 ? 230 : 0) + 380) * scale;
        }

        public BuildingView AddChapter(int index, AdventureBuildingData data, Button button)
        {
            var node = new VisualElement { pickingMode = PickingMode.Ignore };
            node.AddToClassList("city-building-node");
            node.style.position = Position.Absolute;
            var building = new BuildingView();
            building.AddToClassList("city-building-view");
            building.SetBuilding(data);
            if (data != null) building.SetCompletedFloors(data.ReadCompletion());
            node.Add(building);
            node.Add(button);
            Add(node);
            _plots.Add(new Plot { Index = index, Data = data, Node = node, Building = building, Button = button });
            LayoutPlots();
            return building;
        }

        private void LayoutPlots()
        {
            if (contentRect.width <= 0) return;
            float scale = ViewScale;
            float height = MapHeight(scale);
            if (!Mathf.Approximately(resolvedStyle.height, height)) style.height = height;
            foreach (var plot in _plots)
            {
                int band = plot.Index / 2;
                bool right = plot.Index % 2 == 1;
                // Different plot offsets make small neighborhood blocks, not a card chain.
                float cx = contentRect.width * (right ? (band % 2 == 0 ? 0.77f : 0.74f) :
                    (band % 2 == 0 ? 0.23f : 0.26f));
                float ground = height - (380 + band * 620 + (right ? 230 : 0)) * scale;
                Vector2 offset = plot.Data?.Definition != null ? plot.Data.Definition.mapOffset : Vector2.zero;
                cx += Mathf.Clamp(offset.x, -32, 32) * scale;
                ground += Mathf.Clamp(offset.y, -40, 40) * scale;
                float width = contentRect.width * 0.44f;
                plot.Node.style.left = cx - width * 0.5f;
                plot.Node.style.top = ground - 310 * scale;
                plot.Node.style.width = width;
                plot.Building.style.height = 310 * scale;
                plot.Building.style.width = Length.Percent(100);
                plot.Button.style.height = 82 * scale;
                plot.Button.style.fontSize = 25 * scale;
            }
            MarkDirtyRepaint();
        }

        private void DrawCity(MeshGenerationContext context)
        {
            if (contentRect.width <= 0) return;
            var p = context.painter2D;
            float w = contentRect.width, h = contentRect.height, s = ViewScale;
            int bands = Mathf.CeilToInt(_chapterCount / 2f);
            var main = new List<Vector2> { new Vector2(w * 0.50f, h + 30) };
            for (int band = 0; band < bands; band++)
            {
                float y = h - (band * 620 + 50) * s;
                main.Add(new Vector2(w * 0.50f, y));
                main.Add(new Vector2(w * 0.57f, y - 185 * s));
                main.Add(new Vector2(w * 0.43f, y - 405 * s));
                main.Add(new Vector2(w * 0.50f, y - 620 * s));
            }
            main.Add(new Vector2(w * 0.50f, -35));
            // Sidewalk/curb, then asphalt. Repeated branches form intersections.
            Road(p, main, 78 * s, new Color32(112, 142, 145, 255));
            Road(p, main, 55 * s, new Color32(49, 72, 85, 255));
            for (int band = 0; band < bands; band++)
            {
                float y = h - (band * 620 + 48) * s;
                var street = new List<Vector2>
                {
                    new Vector2(-25, y - w * 0.28f),
                    new Vector2(w * 0.50f, y),
                    new Vector2(w + 25, y - w * 0.28f)
                };
                Road(p, street, 64 * s, new Color32(112, 142, 145, 255));
                Road(p, street, 43 * s, new Color32(49, 72, 85, 255));
                Dashes(p, street, s);
                // Small planted traffic islands in the spaces between plots.
                Park(p, new Vector2(w * 0.10f, y - 330 * s), s);
                Park(p, new Vector2(w * 0.89f, y - 460 * s), s);
            }
            Dashes(p, main, s);
        }

        private static void Road(Painter2D p, List<Vector2> points, float width, Color color)
        {
            p.strokeColor = color; p.lineWidth = width; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            p.BeginPath(); p.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++) p.LineTo(points[i]);
            p.Stroke();
        }

        private static void Dashes(Painter2D p, List<Vector2> points, float scale)
        {
            p.strokeColor = new Color32(214, 187, 118, 255);
            p.lineWidth = 2 * scale;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = points[i - 1], delta = points[i] - a;
                float length = delta.magnitude;
                Vector2 direction = delta.normalized;
                for (float d = 14 * scale; d + 14 * scale < length; d += 34 * scale)
                {
                    p.BeginPath(); p.MoveTo(a + direction * d);
                    p.LineTo(a + direction * (d + 14 * scale)); p.Stroke();
                }
            }
        }

        private static void Park(Painter2D p, Vector2 center, float scale)
        {
            p.fillColor = new Color32(50, 93, 80, 255);
            p.BeginPath(); p.MoveTo(center + new Vector2(-32, 0) * scale);
            p.LineTo(center + new Vector2(0, -18) * scale);
            p.LineTo(center + new Vector2(32, 0) * scale);
            p.LineTo(center + new Vector2(0, 18) * scale); p.ClosePath(); p.Fill();
            p.fillColor = new Color32(91, 145, 111, 255);
            p.BeginPath(); p.MoveTo(center + new Vector2(-15, -3) * scale);
            p.LineTo(center + new Vector2(0, -35) * scale);
            p.LineTo(center + new Vector2(15, -3) * scale); p.ClosePath(); p.Fill();
        }
    }
}
