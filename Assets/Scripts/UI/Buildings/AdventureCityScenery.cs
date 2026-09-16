using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI.Buildings
{
    // Modular vector scenery: one retained element for all streets and props.
    // All ground pieces use the same 30-degree projection as the building meshes.
    public sealed class AdventureCityScenery : VisualElement
    {
        private static readonly CustomStyleProperty<Color> SidewalkProperty = new CustomStyleProperty<Color>("--city-sidewalk");
        private static readonly CustomStyleProperty<Color> AsphaltProperty = new CustomStyleProperty<Color>("--city-asphalt");
        private static readonly CustomStyleProperty<Color> MarkingProperty = new CustomStyleProperty<Color>("--city-marking");
        private static readonly CustomStyleProperty<Color> PavingProperty = new CustomStyleProperty<Color>("--city-paving");
        private static readonly CustomStyleProperty<Color> LawnProperty = new CustomStyleProperty<Color>("--city-lawn");
        private static readonly CustomStyleProperty<Color> LeafProperty = new CustomStyleProperty<Color>("--city-leaf");
        private static readonly CustomStyleProperty<float> NightProperty = new CustomStyleProperty<float>("--city-night");
        private Color _sidewalk, _asphalt, _marking, _paving, _lawn, _leaf;
        private float _night;
        private int _districts = 1;
        private const float EstablishedBlockOffset = 320f;
        private float _scale = 1, _origin, _sectionOffset;
        private bool _mirror;
        private readonly bool _foreground;
        private Painter2D _p;
        private readonly struct TreeSite
        {
            public readonly Vector2 Position;
            public readonly bool Foreground;
            public TreeSite(float x, float y, bool foreground = false)
            { Position = new Vector2(x, y); Foreground = foreground; }
        }
        private static readonly TreeSite[] EstablishedTreeSites = {
            new TreeSite(87, 279), new TreeSite(119, 259), new TreeSite(92, 350),
            new TreeSite(242, 377), new TreeSite(308, 277), new TreeSite(363, 244),
            new TreeSite(449, 280), new TreeSite(492, 323), new TreeSite(440, 368),
            new TreeSite(299, 334), new TreeSite(67, 652), new TreeSite(78, 723, true),
            new TreeSite(218, 795), new TreeSite(437, 842, true), new TreeSite(738, 667),
            new TreeSite(745, 720, true), new TreeSite(529, 746, true)
        };
        private static readonly TreeSite[] ExpansionTreeSites = {
            new TreeSite(188, 250), new TreeSite(560, 238), new TreeSite(755, 285)
        };

        public AdventureCityScenery(bool foreground = false)
        {
            _foreground = foreground;
            pickingMode = PickingMode.Ignore;
            AddToClassList("city-scenery");
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.width = Length.Percent(100); style.height = Length.Percent(100);
            generateVisualContent += Draw;
        }

        public void SetPalette(ICustomStyle palette)
        {
            palette.TryGetValue(SidewalkProperty, out _sidewalk);
            palette.TryGetValue(AsphaltProperty, out _asphalt);
            palette.TryGetValue(MarkingProperty, out _marking);
            palette.TryGetValue(PavingProperty, out _paving);
            palette.TryGetValue(LawnProperty, out _lawn);
            palette.TryGetValue(LeafProperty, out _leaf);
            palette.TryGetValue(NightProperty, out _night);
            MarkDirtyRepaint();
        }
        public void SetLayout(int districts, float scale)
        {
            if (_districts == districts && Mathf.Approximately(_scale, scale)) return;
            _districts = districts; _scale = scale; MarkDirtyRepaint();
        }

        private Vector2 Screen(Vector2 p) => new Vector2(
            (_mirror ? 790 - p.x : p.x) * _scale,
            (p.y + _origin + _sectionOffset) * _scale);
        private static Vector2 Iso(float x, float depth, float height = 0) => BuildingGeometry.Project(x, depth, height);
        private Color Shade(Color color, float strength) => Color.Lerp(color, new Color(0.06f, 0.12f, 0.14f, 1), strength);

        private void Draw(MeshGenerationContext context)
        {
            if (_scale <= 0) return;
            _p = context.painter2D;
            for (int district = 0; district < _districts; district++)
            {
                _origin = (_districts - 1 - district) * AdventureCityMap.DistrictSpan;
                _mirror = (district & 1) != 0;
                DrawNeighborhood(district);
            }
        }

        private void DrawNeighborhood(int seed)
        {
            if (!_foreground)
            {
                DrawExpansionRoadAndCourts();

                _sectionOffset = EstablishedBlockOffset;
                DrawEstablishedBlock();
                _sectionOffset = 0;
            }

            // Stable, bounded jitter gives each neighborhood its own planting arrangement.
            var random = new System.Random(417 + seed * 7919);
            _sectionOffset = EstablishedBlockOffset;
            DrawTrees(EstablishedTreeSites, random);
            _sectionOffset = 0;
            DrawTrees(ExpansionTreeSites, random);

            if (!_foreground)
            {
                _sectionOffset = EstablishedBlockOffset;
                Planter(new Vector2(353, 515));
                Planter(new Vector2(101, 767));
                Planter(new Vector2(701, 771));
                _sectionOffset = 0;
                Planter(new Vector2(82, 525));
                Planter(new Vector2(720, 560));
            }
        }

        private void DrawExpansionRoadAndCourts()
        {
            // New courts support plots 6-10 around the road sketched above the original block.
            Box(new Vector2(580, 1255), 250, 250, 3, _paving);

            // Six equal square tiles share their edges with the park's center tile.
            Box(new Vector2(390, 850), 220, 220, 3, _paving);
            Box(new Vector2(200, 740), 220, 220, 3, _paving);
            Box(new Vector2(581, 740), 220, 220, 3, _paving);
            Box(new Vector2(200, 520), 220, 220, 3, _paving);
            Box(new Vector2(390, 410), 220, 220, 3, _paving);
            Box(new Vector2(581, 520), 220, 220, 3, _paving);

            // The road center follows the diamond's upper platform edges exactly.
            Vector2 west = new Vector2(-80, 571);
            Vector2 crown = new Vector2(390, 300);
            Vector2 east = new Vector2(870, 577);
            for (int layer = 0; layer < 2; layer++)
            {
                float width = layer == 0 ? 68 : 45;
                Color color = layer == 0 ? _sidewalk : _asphalt;
                Stroke(west, crown, width, color);
                Stroke(crown, east, width, color);
                Circle(crown, width * 0.5f, color);
            }
            Lane(west, crown, 0, 42);
            Lane(crown, east, 42, 0);
            Crosswalk(Vector2.Lerp(west, crown, 0.22f), (crown - west).normalized);
            Crosswalk(Vector2.Lerp(crown, east, 0.72f), (east - crown).normalized);

            Car(new Vector2(92, 466), true, new Color32(188, 139, 102, 255));
            Car(new Vector2(705, 493), false, new Color32(115, 155, 174, 255));
            Lamp(new Vector2(230, 370), new Vector2(0.7f, -0.7f));
            Lamp(new Vector2(535, 350), new Vector2(-0.6f, -0.8f));
            Bench(new Vector2(600, 555));
        }

        private void DrawEstablishedBlock()
        {
            // This is the approved five-building block, preserved intact below the expansion.
            Box(new Vector2(147, 685), 250, 250, 3, _paving);
            Box(new Vector2(364, 810), 250, 250, 3, _paving);
            Box(new Vector2(645, 684), 250, 250, 3, _paving);
            // The park fills the center of the six-building diamond.
            Box(new Vector2(390, 310), 220, 220, 4, _sidewalk);
            Box(new Vector2(390, 307), 190, 190, 3, _lawn);
            Plane(new Vector2(390, 307), 193, 24, _paving);
            Plane(new Vector2(390, 307), 22, 193, _paving);

            Vector2 west = new Vector2(-80, 400), junction = new Vector2(420, 689);
            Vector2 east = new Vector2(870, 429), south = new Vector2(860, 943);
            for (int layer = 0; layer < 2; layer++)
            {
                float width = layer == 0 ? 68 : 45;
                Color color = layer == 0 ? _sidewalk : _asphalt;
                Stroke(west, junction, width, color);
                Stroke(junction, east, width, color);
                Stroke(junction, south, width, color);
                Circle(junction, width * 0.5f, color);
            }
            Lane(west, junction, 0, 55);
            Lane(junction, east, 55, 0);
            Lane(junction, south, 55, 0);
            Crosswalk(Vector2.Lerp(west, junction, 0.87f), (junction - west).normalized);
            Crosswalk(Vector2.Lerp(junction, east, 0.15f), (east - junction).normalized);

            Car(new Vector2(60, 493), false, new Color32(206, 119, 88, 255));
            Car(new Vector2(137, 538), false, new Color32(227, 197, 118, 255));
            Car(new Vector2(738, 521), true, new Color32(112, 165, 181, 255));
            Car(new Vector2(585, 800), false, new Color32(207, 214, 196, 255));

            Fountain(new Vector2(390, 307));
            Bench(new Vector2(350, 365));
            Bench(new Vector2(434, 284));
            Bench(new Vector2(286, 436));
            Lamp(new Vector2(321, 601), new Vector2(-1f, 0.6f));
            Lamp(new Vector2(537, 655), new Vector2(-1f, -0.35f));
            Lamp(new Vector2(675, 875), Vector2.up);
        }

        private void DrawTrees(TreeSite[] sites, System.Random random)
        {
            for (int i = 0; i < sites.Length; i++)
            {
                if (sites[i].Foreground != _foreground) continue;
                Vector2 site = sites[i].Position +
                    new Vector2(random.Next(-5, 6), random.Next(-4, 5));
                Tree(site, 0.78f + (float)random.NextDouble() * 0.35f, i % 4 == 0);
            }
        }

        private void Lane(Vector2 a, Vector2 b, float startGap, float endGap)
        {
            Vector2 direction = (b - a).normalized;
            float length = Vector2.Distance(a, b) - endGap;
            for (float d = startGap + 9; d + 14 < length; d += 35)
                Stroke(a + direction * d, a + direction * (d + 14), 2, _marking);
        }

        private void Crosswalk(Vector2 center, Vector2 direction)
        {
            Vector2 normal = new Vector2(-direction.y, direction.x);
            for (int i = -3; i <= 3; i++)
                Stroke(center + normal * i * 5 - direction * 6, center + normal * i * 5 + direction * 6, 2.8f, _marking);
        }

        private void Car(Vector2 center, bool reverse, Color color)
        {
            float w = reverse ? 16 : 36, d = reverse ? 36 : 16;
            Plane(center + new Vector2(3, 3), w + 7, d + 6, new Color(0, 0, 0, 0.18f));
            Box(center, w, d, 9, Shade(color, _night * 0.15f));
            Box(center + new Vector2(0, -8), reverse ? 13 : 20, reverse ? 20 : 13, 7, color);
            Plane(center + new Vector2(0, -15), reverse ? 11 : 17, reverse ? 17 : 11, new Color32(79, 111, 124, 255));
            Vector2 wheelA = reverse
                ? center + Iso(w * 0.5f, -11)
                : center + Iso(-11, d * 0.5f);
            Vector2 wheelB = reverse
                ? center + Iso(w * 0.5f, 11)
                : center + Iso(11, d * 0.5f);
            Circle(wheelA, 3.3f, new Color32(35, 46, 49, 255));
            Circle(wheelB, 3.3f, new Color32(35, 46, 49, 255));
        }

        private void Tree(Vector2 ground, float size, bool gold)
        {
            Plane(ground + new Vector2(4, 3), 32 * size, 22 * size, new Color(0.02f, 0.08f, 0.06f, 0.15f));
            Stroke(ground, ground + new Vector2(0, -27 * size), 5 * size, new Color32(115, 101, 75, 255));
            Color leaf = gold ? Color.Lerp(_leaf, new Color32(183, 174, 89, 255), 0.55f) : _leaf;
            Circle(ground + new Vector2(0, -34 * size), 19 * size, Shade(leaf, 0.18f));
            Circle(ground + new Vector2(-7 * size, -40 * size), 15 * size, leaf);
            Circle(ground + new Vector2(4 * size, -47 * size), 12 * size, Color.Lerp(leaf, Color.white, 0.12f));
        }

        private void Fountain(Vector2 center)
        {
            Box(center, 50, 50, 7, _sidewalk);
            Plane(center + new Vector2(0, -7), 40, 40, new Color32(101, 171, 179, 255));
            Box(center + new Vector2(0, -8), 8, 8, 15, _paving);
            Circle(center + new Vector2(0, -26), 4, new Color32(192, 231, 220, 255));
        }

        private void Bench(Vector2 center)
        {
            var wood = new Color32(180, 143, 101, 255);
            Box(center + Iso(-12, 0), 3, 10, 6, Shade(wood, 0.45f));
            Box(center + Iso(12, 0), 3, 10, 6, Shade(wood, 0.45f));
            Box(center + new Vector2(0, -6), 34, 10, 3, wood);
            Box(center + Iso(0, -5, 8), 34, 2, 8, wood);
        }

        private void Lamp(Vector2 ground, Vector2 towardRoad)
        {
            Vector2 poleTop = ground + new Vector2(0, -45);
            Vector2 bulb = poleTop + towardRoad.normalized * 11;
            Plane(ground, 10, 10, _sidewalk);
            Stroke(ground, poleTop, 3, Shade(_sidewalk, 0.45f));
            Stroke(poleTop, bulb, 3, _sidewalk);
            if (_night > 0.5f)
            {
                Circle(bulb, 19, new Color(1, 0.78f, 0.38f, 0.07f));
                Circle(bulb, 11, new Color(1, 0.83f, 0.47f, 0.14f));
            }
            Circle(bulb, 4, _night > 0.5f ? new Color32(255, 221, 147, 255) : _paving);
        }

        private void Planter(Vector2 center)
        {
            Box(center, 29, 12, 6, _sidewalk);
            Plane(center + new Vector2(0, -6), 25, 9, _leaf);
            for (int i = -1; i <= 1; i++) Circle(center + Iso(i * 8, 0, 10), 2.5f, new Color32(227, 175, 137, 255));
        }

        private void Plane(Vector2 center, float width, float depth, Color color)
        {
            Quad(center + Iso(-width / 2, -depth / 2), center + Iso(width / 2, -depth / 2),
                center + Iso(width / 2, depth / 2), center + Iso(-width / 2, depth / 2), color);
        }

        private void Box(Vector2 center, float width, float depth, float height, Color color)
        {
            Vector2 left = center + Iso(-width / 2, depth / 2), front = center + Iso(width / 2, depth / 2);
            Vector2 right = center + Iso(width / 2, -depth / 2), up = new Vector2(0, -height);
            Quad(left, front, front + up, left + up, Shade(color, 0.12f));
            Quad(front, right, right + up, front + up, Shade(color, 0.28f));
            Plane(center + up, width, depth, color);
        }

        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            _p.fillColor = color; _p.BeginPath(); _p.MoveTo(Screen(a)); _p.LineTo(Screen(b));
            _p.LineTo(Screen(c)); _p.LineTo(Screen(d)); _p.ClosePath(); _p.Fill();
        }

        private void Stroke(Vector2 a, Vector2 b, float width, Color color)
        {
            _p.strokeColor = color; _p.lineWidth = width * _scale; _p.lineCap = LineCap.Butt;
            _p.BeginPath(); _p.MoveTo(Screen(a)); _p.LineTo(Screen(b)); _p.Stroke();
        }

        private void Circle(Vector2 center, float radius, Color color)
        {
            _p.fillColor = color; _p.BeginPath(); _p.Arc(Screen(center), radius * _scale, 0, 360); _p.ClosePath(); _p.Fill();
        }
    }
}
