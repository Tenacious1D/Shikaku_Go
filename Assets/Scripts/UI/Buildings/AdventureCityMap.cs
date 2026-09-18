using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI.Buildings
{
    // Ten plots form a neighborhood, rather than ten separate street blocks.
    // Coordinates are design-space ground anchors, matching BuildingGeometry's projection.
    public sealed class AdventureCityMap : VisualElement
    {
        public const int BuildingsPerDistrict = 10;
        // District scenery intentionally overlaps slightly so the full map reads as one city.
        public const float DistrictSpan = 1040f;
        public const float DistrictCanvasHeight = 1390f;

        private static readonly Vector2[] CityAnchors = {
            // Chapters 1-4 occupy the former 6, 3, 2 and 1 positions respectively.
            new Vector2(580, 1297), new Vector2(645, 1046),
            new Vector2(364, 1172), new Vector2(147, 1047),
            // Chapters 5-10 form a compact diamond around the central park.
            new Vector2(390, 892), new Vector2(200, 782),
            new Vector2(581, 782), new Vector2(200, 562),
            new Vector2(390, 452), new Vector2(581, 562)
        };
        private static readonly Vector2[] NeighborhoodAnchors = {
            // Lower street: 1-2 below the road, 3-5 above it.
            new Vector2(333, 1172), new Vector2(125, 1052),
            new Vector2(561, 1082), new Vector2(353, 962), new Vector2(145, 842),
            // Upper street: 6-8 below the road, 9-10 above it.
            new Vector2(541, 762), new Vector2(333, 642), new Vector2(125, 522),
            new Vector2(508, 492), new Vector2(300, 372)
        };
        private static readonly Vector2[] WarehouseAnchors = {
            // Lower diagonal: 1-2 below the road and 3-4 above it.
            new Vector2(120, 1041), new Vector2(310, 1151),
            new Vector2(290, 920), new Vector2(100, 810),
            // Upper diagonal: 5 and 7 below; 6, 8 and 9 above.
            new Vector2(500, 841), new Vector2(270, 706),
            new Vector2(690, 732), new Vector2(440, 626),
            new Vector2(630, 516),
            // Chapter 10 fills the open lower-right area above the lower road.
            new Vector2(675, 1080)
        };
        private readonly List<Plot> _plots = new List<Plot>();
        private readonly AdventureCityScenery _backgroundScenery = new AdventureCityScenery(false);
        private readonly AdventureCityScenery _foregroundScenery = new AdventureCityScenery(true);
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
        private int Districts => Mathf.Max(1, Mathf.CeilToInt(_chapterCount / (float)BuildingsPerDistrict));
        private float MapHeight =>
            ((Districts - 1) * DistrictSpan + DistrictCanvasHeight) * ViewScale;

        private static int LayoutFamily(int district) => (district / 2) % 3;
        public static bool UsesNeighborhoodLayout(int district) => LayoutFamily(district) == 1;
        public static bool UsesWarehouseLayout(int district) => LayoutFamily(district) == 2;

        public AdventureCityMap(int chapterCount)
        {
            _chapterCount = Mathf.Max(0, chapterCount);
            AddToClassList("adventure-city-map");
            style.width = Length.Percent(100);
            style.position = Position.Relative;
            style.flexShrink = 0;
            Add(_backgroundScenery);
            Add(_foregroundScenery);
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                _backgroundScenery.SetPalette(evt.customStyle);
                _foregroundScenery.SetPalette(evt.customStyle);
            });
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
            _foregroundScenery.BringToFront();
            _plots.Add(new Plot { Index = index, Data = data, Node = node, Building = building, Button = button, Address = address });
            LayoutPlots();
            return building;
        }

        private void LayoutPlots()
        {
            if (contentRect.width <= 0) return;
            float s = ViewScale;
            if (!Mathf.Approximately(resolvedStyle.height, MapHeight)) style.height = MapHeight;
            _backgroundScenery.SetLayout(Districts, s);
            _foregroundScenery.SetLayout(Districts, s);
            foreach (var plot in _plots)
            {
                int district = plot.Index / BuildingsPerDistrict;
                int slot = plot.Index % BuildingsPerDistrict;
                bool neighborhood = UsesNeighborhoodLayout(district);
                bool warehouse = UsesWarehouseLayout(district);
                Vector2 anchor = warehouse ? WarehouseAnchors[slot] :
                    neighborhood ? NeighborhoodAnchors[slot] : CityAnchors[slot];
                if ((district & 1) != 0) anchor.x = 790 - anchor.x;
                anchor.y += (Districts - 1 - district) * DistrictSpan;
                Vector2 offset = plot.Data?.Definition != null ? plot.Data.Definition.mapOffset : Vector2.zero;
                anchor += new Vector2(Mathf.Clamp(offset.x, -2, 2), Mathf.Clamp(offset.y, -4, 4));
                bool fullHeightPlot = !neighborhood && !warehouse && slot >= 1 && slot <= 3;
                float buildingHeight = fullHeightPlot ? 255 : 205;
                float badgeHeight = fullHeightPlot ? Mathf.Max(32, 38 * s) : Mathf.Max(20, 24 * s);
                plot.Node.style.left = (anchor.x - 100) * s;
                plot.Node.style.top = (anchor.y - buildingHeight) * s;
                plot.Node.style.width = 200 * s;
                plot.Node.style.height = buildingHeight * s + badgeHeight;
                plot.Building.style.height = buildingHeight * s;
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
            _foregroundScenery.BringToFront();
        }
    }
}
