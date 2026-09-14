using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI.Buildings
{
    // Five plots form a neighborhood, rather than five separate street blocks.
    // Coordinates are design-space ground anchors, matching BuildingGeometry's projection.
    public sealed class AdventureCityMap : VisualElement
    {
        private static readonly Vector2[] Anchors = {
            new Vector2(150, 720), new Vector2(355, 838), new Vector2(645, 720),
            new Vector2(215, 355), new Vector2(420, 473)
        };
        private readonly List<Plot> _plots = new List<Plot>();
        private readonly AdventureCityScenery _scenery = new AdventureCityScenery();
        private readonly int _chapterCount;
        private sealed class Plot
        {
            public int Index;
            public AdventureBuildingData Data;
            public VisualElement Node;
            public BuildingView Building;
            public Button Button;
            public Label Address;
        }
        private float ViewScale => contentRect.width / 790f;
        private int Districts => Mathf.Max(1, Mathf.CeilToInt(_chapterCount / 5f));
        private float MapHeight => (Districts * 920 + 30) * ViewScale;

        public AdventureCityMap(int chapterCount)
        {
            _chapterCount = Mathf.Max(0, chapterCount);
            AddToClassList("adventure-city-map");
            style.width = Length.Percent(100);
            style.position = Position.Relative;
            style.flexShrink = 0;
            Add(_scenery);
            RegisterCallback<CustomStyleResolvedEvent>(evt => _scenery.SetPalette(evt.customStyle));
            RegisterCallback<GeometryChangedEvent>(_ => LayoutPlots());
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
            // Keep the controller's Button and callbacks. Its transparent hit area covers
            // the building; only the little address plaque has a visible background.
            var address = new Label(button.text) { pickingMode = PickingMode.Ignore };
            address.AddToClassList("city-address");
            button.tooltip = !string.IsNullOrEmpty(button.tooltip) ? button.tooltip : button.text;
            button.text = string.Empty;
            button.AddToClassList("city-chapter-button");
            button.Add(address);
            node.Add(button);
            Add(node);
            _plots.Add(new Plot { Index = index, Data = data, Node = node, Building = building, Button = button, Address = address });
            LayoutPlots();
            return building;
        }

        private void LayoutPlots()
        {
            if (contentRect.width <= 0) return;
            float s = ViewScale;
            if (!Mathf.Approximately(resolvedStyle.height, MapHeight)) style.height = MapHeight;
            _scenery.SetLayout(Districts, s);
            foreach (var plot in _plots)
            {
                int district = plot.Index / 5;
                Vector2 anchor = Anchors[plot.Index % 5];
                if ((district & 1) != 0) anchor.x = 790 - anchor.x;
                anchor.y += (Districts - 1 - district) * 920;
                Vector2 offset = plot.Data?.Definition != null ? plot.Data.Definition.mapOffset : Vector2.zero;
                anchor += new Vector2(Mathf.Clamp(offset.x, -2, 2), Mathf.Clamp(offset.y, -4, 4));
                float badgeHeight = Mathf.Max(40, 38 * s);
                plot.Node.style.left = (anchor.x - 100) * s;
                plot.Node.style.top = (anchor.y - 275) * s;
                plot.Node.style.width = 200 * s;
                plot.Node.style.height = 275 * s + badgeHeight;
                plot.Building.style.height = 275 * s;
                plot.Building.style.width = Length.Percent(100);
                plot.Button.style.position = Position.Absolute;
                plot.Button.style.left = 0; plot.Button.style.right = 0;
                plot.Button.style.top = 0; plot.Button.style.bottom = 0;
                plot.Address.style.height = badgeHeight;
                bool complete = plot.Button.ClassListContains("city-chapter-complete");
                plot.Address.style.width = Mathf.Min(190 * s, Mathf.Max(complete ? 48 : 80, (complete ? 72 : 124) * s));
                plot.Address.style.fontSize = Mathf.Max(12, 18 * s);
            }
            // Back buildings first. Progress changes do not move plots or change hit ordering.
            _plots.Sort((a, b) => a.Node.style.top.value.value.CompareTo(b.Node.style.top.value.value));
            foreach (var plot in _plots) plot.Node.BringToFront();
        }
    }
}