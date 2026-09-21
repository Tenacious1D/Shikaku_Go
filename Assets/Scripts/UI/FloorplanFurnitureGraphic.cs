using System.Collections.Generic;
using Shikaku.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace Shikaku.UI
{
    /// <summary>A pooled, noninteractive room mesh. All furniture is made from flat colored faces.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorplanFurnitureGraphic : MaskableGraphic
    {
        private readonly List<FloorplanFurniturePlacement> _placements = new(3);
        private PuzzleModel _model;
        private ShikakuRegion _room;
        private bool _dark, _quarterTurn, _detail;
        private VertexHelper _mesh;
        private Rect _prop;
        private Color _wood, _fabric, _cream, _leaf, _shadow;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void SetRoom(PuzzleModel model, ShikakuRegion room, bool dark, bool visible)
        {
            bool show = visible && room != null && room.IsValid;
            if (enabled != show) enabled = show;
            if (_model == model && _room == room && _dark == dark) return;
            _model = model; _room = room; _dark = dark;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_room == null || _model == null || !enabled) return;
            Rect rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;
            float cellWidth = rect.width / _room.Width, cellHeight = rect.height / _room.Height;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(Vector3.zero));
            float pixelsX = Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera,
                rectTransform.TransformPoint(new Vector3(cellWidth, 0))));
            float pixelsY = Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera,
                rectTransform.TransformPoint(new Vector3(0, cellHeight))));
            FloorplanFurnitureLayout.Build(_model, _room, Mathf.Min(pixelsX, pixelsY), _placements);
            _detail = Mathf.Min(pixelsX, pixelsY) >= 36f;
            _mesh = mesh;
            _wood = _dark ? RGB(167, 127, 82) : RGB(191, 150, 94);
            _fabric = _dark ? RGB(100, 161, 172) : RGB(90, 151, 168);
            _cream = _dark ? RGB(211, 212, 193) : RGB(246, 240, 214);
            _leaf = _dark ? RGB(108, 163, 113) : RGB(116, 163, 104);
            _shadow = new Color(0.08f, .15f, .17f, _dark ? .26f : .13f);
            foreach (var item in _placements)
            {
                // _prop uses top-down local coordinates. Each primitive stays inside this footprint.
                _prop = new Rect(rect.xMin + item.Bounds.x * cellWidth,
                    rect.yMax - item.Bounds.y * cellHeight,
                    item.Bounds.width * cellWidth, item.Bounds.height * cellHeight);
                _quarterTurn = item.QuarterTurn;
                Draw(item.Kind);
            }
            _mesh = null;
        }

        private static Color RGB(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        private static Color Shade(Color color, float amount) => new Color(color.r * amount, color.g * amount, color.b * amount, color.a);

        private void Draw(FloorplanFurnitureKind kind)
        {
            switch (kind)
            {
                case FloorplanFurnitureKind.Bed:
                    Quad(.08f, .10f, .88f, .88f, _shadow);
                    Block(.05f, .04f, .88f, .89f, .07f, _wood);
                    Block(.09f, .06f, .80f, .84f, .05f, _cream);
                    Quad(.12f, .13f, .32f, .21f, Shade(_cream, .87f));
                    Quad(.54f, .13f, .32f, .21f, Shade(_cream, .87f));
                    SoftQuad(.12f, .10f, .32f, .21f, _dark ? RGB(233, 233, 215) : RGB(255, 252, 240));
                    SoftQuad(.54f, .10f, .32f, .21f, _dark ? RGB(233, 233, 215) : RGB(255, 252, 240));
                    Block(.09f, .38f, .80f, .52f, .06f, _leaf);
                    Quad(.09f, .38f, .80f, .08f, Shade(_leaf, 1.08f));
                    break;
                case FloorplanFurnitureKind.Sofa:
                    Quad(.05f, .12f, .92f, .85f, _shadow);
                    Color sofa = _dark ? RGB(190, 117, 84) : RGB(199, 121, 83);
                    Block(.05f, .07f, .88f, .81f, .12f, sofa);
                    Block(.10f, .05f, .78f, .26f, .06f, Shade(sofa, 1.08f));
                    Block(.15f, .34f, .31f, .40f, .06f, Shade(sofa, 1.13f));
                    Block(.50f, .34f, .31f, .40f, .06f, Shade(sofa, 1.10f));
                    Block(.04f, .22f, .11f, .63f, .10f, Shade(sofa, 1.04f));
                    Block(.82f, .22f, .11f, .63f, .10f, Shade(sofa, 1.04f));
                    break;
                case FloorplanFurnitureKind.Desk:
                    Quad(.04f, .09f, .92f, .52f, _shadow);
                    Quad(.49f, .60f, .43f, .36f, _shadow);
                    Block(.03f, .05f, .90f, .47f, .07f, _wood);
                    // A screen, base and sheet stay legible as three uncomplicated shapes.
                    Quad(.37f, .13f, .30f, .18f, Shade(_fabric, .52f));
                    Quad(.48f, .31f, .09f, .04f, Shade(_fabric, .70f));
                    Quad(.13f, .23f, .16f, .16f, _cream);
                    Quad(.59f, .71f, .06f, .21f, Shade(_fabric, .65f));
                    Quad(.44f, .85f, .36f, .04f, Shade(_fabric, .65f));
                    Block(.42f, .56f, .40f, .28f, .05f, _fabric);
                    Block(.42f, .77f, .40f, .11f, .03f, Shade(_fabric, 1.12f));
                    break;
                case FloorplanFurnitureKind.Armchair:
                    Quad(.12f, .12f, .84f, .84f, _shadow);
                    Block(.08f, .08f, .79f, .80f, .12f, _fabric);
                    Block(.13f, .05f, .69f, .26f, .06f, Shade(_fabric, 1.14f));
                    Block(.21f, .36f, .53f, .37f, .07f, Shade(_fabric, 1.12f));
                    Block(.06f, .25f, .16f, .57f, .08f, _fabric);
                    Block(.73f, .25f, .16f, .57f, .08f, _fabric);
                    break;
                case FloorplanFurnitureKind.Cabinet:
                    Quad(.08f, .15f, .88f, .81f, _shadow);
                    Block(.04f, .05f, .87f, .80f, .15f, _wood);
                    break;
                case FloorplanFurnitureKind.Table:
                    Ellipse(.09f, .27f, .87f, .68f, _shadow);
                    Quad(.21f, .61f, .08f, .22f, Shade(_wood, .7f));
                    Quad(.73f, .61f, .08f, .22f, Shade(_wood, .7f));
                    Ellipse(.05f, .14f, .87f, .65f, Shade(_fabric, .78f));
                    Ellipse(.05f, .06f, .87f, .65f, _fabric);
                    break;
                case FloorplanFurnitureKind.DiningSet:
                    Quad(.08f, .09f, .87f, .86f, _shadow);
                    // Four chairs and one tabletop are one bounded object, including pull-out space.
                    Block(.08f, .34f, .19f, .29f, .06f, _fabric);
                    Block(.73f, .34f, .19f, .29f, .06f, _fabric);
                    Block(.37f, .05f, .27f, .18f, .05f, _fabric);
                    Block(.37f, .76f, .27f, .18f, .05f, _fabric);
                    Block(.23f, .21f, .54f, .57f, .08f, _wood);
                    if (_detail)
                    {
                        Ellipse(.33f, .36f, .12f, .15f, _cream);
                        Ellipse(.55f, .36f, .12f, .15f, _cream);
                    }
                    break;
                case FloorplanFurnitureKind.KitchenCounter:
                    Quad(.07f, .15f, .89f, .81f, _shadow);
                    Block(.04f, .06f, .88f, .80f, .16f, _wood);
                    SoftQuad(.04f, .06f, .88f, .58f, _cream);
                    SoftQuad(.12f, .15f, .29f, .36f, Shade(_fabric, .78f));
                    SoftQuad(.16f, .19f, .21f, .27f, _fabric);
                    SoftQuad(.57f, .14f, .27f, .38f, Shade(_fabric, .40f));
                    if (_detail)
                    {
                        Ellipse(.61f, .18f, .08f, .11f, Shade(_fabric, 1.35f));
                        Ellipse(.72f, .33f, .08f, .11f, Shade(_fabric, 1.35f));
                        Quad(.25f, .07f, .04f, .14f, Shade(_fabric, .6f));
                        Quad(.46f, .70f, .02f, .10f, Shade(_wood, .7f));
                    }
                    break;
                case FloorplanFurnitureKind.Bathtub:
                    SoftQuad(.13f, .08f, .78f, .88f, _shadow);
                    Block(.08f, .04f, .78f, .87f, .06f, _cream);
                    SoftQuad(.18f, .14f, .58f, .64f, Shade(_fabric, .8f));
                    SoftQuad(.23f, .18f, .48f, .55f, Shade(_fabric, 1.2f));
                    if (_detail)
                    {
                        Quad(.43f, .07f, .07f, .15f, Shade(_fabric, .6f));
                        Ellipse(.45f, .66f, .05f, .025f, Shade(_fabric, .7f));
                    }
                    break;
                case FloorplanFurnitureKind.Basin:
                    Quad(.11f, .15f, .82f, .81f, _shadow);
                    Block(.07f, .05f, .80f, .82f, .13f, _wood);
                    SoftQuad(.07f, .05f, .80f, .62f, _cream);
                    Ellipse(.18f, .16f, .58f, .39f, Shade(_fabric, .85f));
                    Ellipse(.24f, .20f, .46f, .28f, _fabric);
                    Quad(.44f, .06f, .05f, .19f, Shade(_fabric, .6f));
                    break;
                case FloorplanFurnitureKind.Toilet:
                    Ellipse(.15f, .30f, .72f, .65f, _shadow);
                    Block(.15f, .07f, .66f, .26f, .07f, _cream);
                    Ellipse(.21f, .25f, .55f, .60f, Shade(_cream, .82f));
                    Ellipse(.17f, .23f, .63f, .55f, _cream);
                    Ellipse(.30f, .33f, .36f, .32f, _fabric);
                    break;
                case FloorplanFurnitureKind.Bookcase:
                    Quad(.08f, .13f, .88f, .84f, _shadow);
                    Block(.04f, .06f, .87f, .79f, .12f, _wood);
                    Quad(.10f, .17f, .75f, .44f, Shade(_wood, .62f));
                    Quad(.13f, .19f, .13f, .38f, _fabric);
                    Quad(.28f, .23f, .11f, .34f, _cream);
                    Quad(.41f, .18f, .14f, .39f, _leaf);
                    Quad(.57f, .22f, .11f, .35f, RGB(199, 121, 83));
                    Quad(.70f, .19f, .12f, .38f, _fabric);
                    break;
                case FloorplanFurnitureKind.Washer:
                    Quad(.12f, .13f, .81f, .83f, _shadow);
                    Block(.08f, .06f, .79f, .83f, .10f, _cream);
                    SoftQuad(.15f, .12f, .65f, .14f, Shade(_fabric, .7f));
                    Ellipse(.21f, .32f, .52f, .40f, Shade(_fabric, .65f));
                    Ellipse(.27f, .37f, .40f, .29f, _fabric);
                    if (_detail) Ellipse(.65f, .145f, .08f, .07f, _cream);
                    break;
                case FloorplanFurnitureKind.Bench:
                    Quad(.09f, .20f, .84f, .75f, _shadow);
                    Quad(.17f, .64f, .08f, .25f, Shade(_wood, .62f));
                    Quad(.74f, .64f, .08f, .25f, Shade(_wood, .62f));
                    Block(.06f, .13f, .86f, .59f, .12f, _wood);
                    if (_detail) Quad(.10f, .34f, .78f, .035f, Shade(_wood, .76f));
                    break;
                case FloorplanFurnitureKind.Plant:
                    Ellipse(.19f, .47f, .70f, .50f, _shadow);
                    Ellipse(.20f, .41f, .60f, .48f, Shade(_wood, .70f));
                    Ellipse(.20f, .34f, .60f, .44f, _wood);
                    Ellipse(.28f, .06f, .54f, .53f, Shade(_leaf, .80f));
                    Ellipse(.08f, .23f, .52f, .50f, _leaf);
                    Ellipse(.42f, .27f, .48f, .46f, Shade(_leaf, 1.12f));
                    break;
            }
        }

        private void Block(float x, float y, float width, float height, float depth, Color top)
        {
            SoftQuad(x, y + depth, width, height - depth, Shade(top, .76f));
            SoftQuad(x, y, width, height - depth, top);
        }

        private Vector3 Point(float x, float y)
        {
            if (_quarterTurn) { float oldX = x; x = 1f - y; y = oldX; }
            return new Vector3(_prop.x + x * _prop.width, _prop.y - y * _prop.height);
        }
        private void Quad(float x, float y, float width, float height, Color color)
        {
            int first = _mesh.currentVertCount;
            _mesh.AddVert(Point(x, y), color, Vector2.zero);
            _mesh.AddVert(Point(x + width, y), color, Vector2.zero);
            _mesh.AddVert(Point(x + width, y + height), color, Vector2.zero);
            _mesh.AddVert(Point(x, y + height), color, Vector2.zero);
            _mesh.AddTriangle(first, first + 1, first + 2);
            _mesh.AddTriangle(first, first + 2, first + 3);
        }

        // Eight corners soften silhouettes without sprites, textures or per-prop GameObjects.
        private void SoftQuad(float x, float y, float width, float height, Color color)
        {
            float logicalWidth = _quarterTurn ? _prop.height : _prop.width;
            float logicalHeight = _quarterTurn ? _prop.width : _prop.height;
            float radius = Mathf.Min(width * logicalWidth, height * logicalHeight) * .065f;
            float rx = radius / logicalWidth, ry = radius / logicalHeight;
            int first = _mesh.currentVertCount;
            _mesh.AddVert(Point(x + rx, y), color, Vector2.zero);
            _mesh.AddVert(Point(x + width - rx, y), color, Vector2.zero);
            _mesh.AddVert(Point(x + width, y + ry), color, Vector2.zero);
            _mesh.AddVert(Point(x + width, y + height - ry), color, Vector2.zero);
            _mesh.AddVert(Point(x + width - rx, y + height), color, Vector2.zero);
            _mesh.AddVert(Point(x + rx, y + height), color, Vector2.zero);
            _mesh.AddVert(Point(x, y + height - ry), color, Vector2.zero);
            _mesh.AddVert(Point(x, y + ry), color, Vector2.zero);
            for (int i = 1; i < 7; i++) _mesh.AddTriangle(first, first + i, first + i + 1);
        }

        private void Ellipse(float x, float y, float width, float height, Color color)
        {
            const int segments = 12;
            int first = _mesh.currentVertCount;
            _mesh.AddVert(Point(x + width * .5f, y + height * .5f), color, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                _mesh.AddVert(Point(x + width * (.5f + .5f * Mathf.Cos(angle)),
                    y + height * (.5f + .5f * Mathf.Sin(angle))), color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                _mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
        }
    }
}