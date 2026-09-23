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
        private readonly List<FloorplanFurniturePlacement> _placements = new(5);
        private PuzzleModel _model;
        private ShikakuRegion _room;
        private bool _dark, _quarterTurn, _detail;
        private VertexHelper _mesh;
        private Rect _prop;
        private float _reveal = 1f;
        private Color _wood, _fabric, _cream, _leaf, _shadow;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public static float RevealProgress(float elapsed, bool reduceMotion)
            => reduceMotion ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .06f) / .24f));

        public void SetReveal(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(_reveal, amount)) return;
            _reveal = amount;
            SetVerticesDirty();
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
                // Settle inside each prop's own footprint so animation cannot drift over a clue.
                float inset = (1f - Mathf.Lerp(.94f, 1f, _reveal)) * .5f;
                _prop = new Rect(_prop.x + _prop.width * inset, _prop.y - _prop.height * inset,
                    _prop.width * (1f - 2f * inset), _prop.height * (1f - 2f * inset));
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
                case FloorplanFurnitureKind.RockingChair:
                    DrawTraditionalRocker();
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
                case FloorplanFurnitureKind.TallBookshelf:
                    DrawTallBookshelf();
                    break;
                case FloorplanFurnitureKind.FilledBookshelf:
                    DrawFilledBookshelf();
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
                case FloorplanFurnitureKind.Crib:
                    DrawTraditionalCrib();
                    break;
                case FloorplanFurnitureKind.ChangingTable:
                    Quad(.10f, .15f, .83f, .80f, _shadow);
                    Block(.05f, .05f, .84f, .80f, .14f, _wood);
                    SoftQuad(.11f, .11f, .71f, .46f, _cream);
                    SoftQuad(.17f, .16f, .57f, .30f, _fabric);
                    if (_detail)
                    {
                        Quad(.22f, .69f, .14f, .025f, Shade(_wood, .6f));
                        Quad(.57f, .69f, .14f, .025f, Shade(_wood, .6f));
                    }
                    break;
                case FloorplanFurnitureKind.GrandfatherClock:
                    SoftQuad(.19f, .08f, .72f, .88f, _shadow);
                    Block(.20f, .07f, .58f, .81f, .05f, _wood);
                    Block(.13f, .05f, .72f, .40f, .04f, Shade(_wood, .82f));
                    Ellipse(.18f, .10f, .62f, .33f, _wood);
                    Ellipse(.23f, .125f, .52f, .28f, _cream);
                    Quad(.48f, .18f, .025f, .10f, Shade(_fabric, .4f));
                    Quad(.49f, .265f, .15f, .02f, Shade(_fabric, .4f));
                    SoftQuad(.31f, .48f, .35f, .31f, Shade(_wood, .53f));
                    if (_detail)
                    {
                        Quad(.48f, .51f, .025f, .16f, _cream);
                        Ellipse(.38f, .66f, .23f, .12f, _wood);
                    }
                    Block(.13f, .84f, .72f, .10f, .03f, Shade(_wood, .82f));
                    break;
                case FloorplanFurnitureKind.TvConsole:
                    Quad(.09f, .17f, .85f, .79f, _shadow);
                    Block(.05f, .44f, .86f, .43f, .12f, _wood);
                    Quad(.44f, .40f, .08f, .20f, Shade(_fabric, .4f));
                    SoftQuad(.33f, .58f, .32f, .055f, Shade(_fabric, .4f));
                    Block(.10f, .06f, .76f, .47f, .04f, Shade(_fabric, .35f));
                    SoftQuad(.13f, .09f, .70f, .36f, Shade(_fabric, .57f));
                    if (_detail)
                    {
                        Quad(.18f, .13f, .23f, .025f, Shade(_fabric, .8f));
                        Quad(.47f, .71f, .02f, .09f, Shade(_wood, .63f));
                    }
                    break;
                case FloorplanFurnitureKind.Wardrobe:
                    Quad(.09f, .15f, .85f, .79f, _shadow);
                    Block(.05f, .05f, .86f, .81f, .13f, _wood);
                    SoftQuad(.09f, .11f, .36f, .56f, Shade(_wood, 1.10f));
                    SoftQuad(.51f, .11f, .36f, .56f, Shade(_wood, 1.06f));
                    Quad(.395f, .33f, .025f, .17f, Shade(_wood, .5f));
                    Quad(.535f, .33f, .025f, .17f, Shade(_wood, .5f));
                    break;
                case FloorplanFurnitureKind.ToyChest:
                    Quad(.11f, .17f, .83f, .77f, _shadow);
                    Block(.07f, .11f, .82f, .75f, .15f, _fabric);
                    Block(.05f, .06f, .86f, .55f, .06f, Shade(_fabric, 1.12f));
                    Quad(.40f, .66f, .16f, .045f, _cream);
                    if (_detail)
                    {
                        Ellipse(.20f, .23f, .17f, .23f, _cream);
                        SoftQuad(.51f, .22f, .17f, .23f, RGB(214, 159, 95));
                    }
                    break;
                case FloorplanFurnitureKind.BreakfastBar:
                    Quad(.08f, .13f, .85f, .81f, _shadow);
                    Block(.05f, .08f, .86f, .48f, .10f, _wood);
                    SoftQuad(.05f, .08f, .86f, .32f, _cream);
                    Quad(.23f, .69f, .035f, .24f, Shade(_wood, .62f));
                    Quad(.69f, .69f, .035f, .24f, Shade(_wood, .62f));
                    Ellipse(.15f, .59f, .21f, .27f, Shade(_fabric, .72f));
                    Ellipse(.61f, .59f, .21f, .27f, Shade(_fabric, .72f));
                    Ellipse(.15f, .55f, .21f, .27f, _fabric);
                    Ellipse(.61f, .55f, .21f, .27f, _fabric);
                    break;
                case FloorplanFurnitureKind.DoubleWorkstation:
                    Quad(.08f,.13f,.87f,.80f,_shadow);
                    Block(.04f,.06f,.88f,.46f,.08f,_wood);
                    for(int station=0;station<2;station++)
                    {
                        float x=.10f+station*.44f;
                        Block(x+.035f,.12f,.27f,.21f,.035f,Shade(_fabric,.42f));
                        Quad(x+.14f,.32f,.06f,.055f,Shade(_fabric,.55f));
                        if(_detail) SoftQuad(x+.05f,.40f,.23f,.045f,_cream);
                        Quad(x+.16f,.66f,.04f,.22f,Shade(_fabric,.55f));
                        Quad(x+.07f,.85f,.23f,.03f,Shade(_fabric,.55f));
                        Block(x+.04f,.59f,.29f,.23f,.04f,_fabric);
                        Block(x+.04f,.77f,.29f,.10f,.025f,Shade(_fabric,1.10f));
                    }
                    break;
                case FloorplanFurnitureKind.FilingCabinet:
                    Quad(.14f,.12f,.76f,.83f,_shadow);
                    Block(.09f,.05f,.76f,.84f,.10f,Shade(_fabric,.8f));
                    for(int drawer=0;drawer<3;drawer++)
                    {
                        float y=.10f+drawer*.22f;
                        SoftQuad(.15f,y,.64f,.19f,_fabric);
                        Quad(.38f,y+.075f,.18f,.025f,_cream);
                    }
                    break;
                case FloorplanFurnitureKind.PrinterStand:
                    Quad(.10f,.17f,.84f,.77f,_shadow);
                    Block(.05f,.40f,.85f,.46f,.12f,_wood);
                    Quad(.28f,.06f,.42f,.24f,_cream);
                    Block(.14f,.22f,.69f,.41f,.09f,Shade(_fabric,.65f));
                    SoftQuad(.20f,.27f,.56f,.20f,Shade(_cream,.88f));
                    Quad(.24f,.53f,.45f,.05f,Shade(_fabric,.35f));
                    Quad(.29f,.57f,.35f,.13f,_cream);
                    break;
                case FloorplanFurnitureKind.UprightPiano:
                    Color piano=Shade(_wood,.57f);
                    Quad(.09f,.12f,.86f,.51f,_shadow);
                    Quad(.32f,.75f,.45f,.19f,_shadow);
                    Block(.05f,.06f,.86f,.50f,.07f,piano);
                    Block(.05f,.04f,.86f,.10f,.025f,Shade(_wood,.78f));
                    SoftQuad(.25f,.17f,.47f,.13f,Shade(piano,1.3f));
                    // Keyboard and bench share one footprint, so the bench always faces the keys.
                    Quad(.11f,.35f,.74f,.18f,_cream);
                    int keys=_detail?14:8;
                    for(int key=1;key<keys;key++)
                    {
                        float x=.11f+.74f*key/keys;
                        if(_detail) Quad(x,.35f,.003f,.18f,piano);
                        if(key%7!=3 && key%7!=0) Quad(x-.013f,.35f,.023f,.10f,Shade(_fabric,.27f));
                    }
                    Quad(.29f,.74f,.035f,.15f,piano);
                    Quad(.70f,.74f,.035f,.15f,piano);
                    Block(.25f,.68f,.51f,.17f,.04f,Shade(_wood,.76f));
                    break;
                case FloorplanFurnitureKind.RecordCabinet:
                    Quad(.09f,.15f,.85f,.80f,_shadow);
                    Block(.05f,.06f,.85f,.80f,.12f,_wood);
                    SoftQuad(.10f,.11f,.52f,.44f,Shade(_wood,.51f));
                    Ellipse(.19f,.14f,.33f,.35f,Shade(_fabric,.3f));
                    Ellipse(.315f,.27f,.07f,.075f,_cream);
                    Stroke(.58f,.16f,.55f,.40f,.018f,_cream);
                    Quad(.67f,.15f,.15f,.40f,Shade(_wood,.62f));
                    if(_detail)
                    {
                        Quad(.69f,.19f,.03f,.32f,_fabric);
                        Quad(.735f,.17f,.03f,.34f,_cream);
                        Quad(.78f,.22f,.025f,.29f,_leaf);
                    }
                    break;
                case FloorplanFurnitureKind.BoxShelves:
                    Quad(.08f,.14f,.88f,.82f,_shadow);
                    Block(.04f,.06f,.88f,.80f,.12f,_wood);
                    Quad(.10f,.15f,.76f,.48f,Shade(_wood,.58f));
                    for(int box=0;box<3;box++)
                    {
                        float x=.12f+box*.25f;
                        Block(x,.21f,.21f,.36f,.045f,box==1?Shade(_fabric,1.07f):Shade(_wood,1.12f));
                        Quad(x+.09f,.21f,.035f,.31f,_cream);
                        if(_detail) Quad(x+.06f,.46f,.10f,.045f,Shade(_cream,.9f));
                    }
                    break;
                case FloorplanFurnitureKind.CoatRack:
                    Ellipse(.17f,.73f,.71f,.22f,_shadow);
                    Ellipse(.13f,.74f,.69f,.16f,Shade(_wood,.7f));
                    Stroke(.47f,.11f,.47f,.80f,.055f,Shade(_wood,.8f));
                    Curve(.47f,.31f,.14f,.25f,.18f,.14f,.034f,_wood,4);
                    Curve(.47f,.28f,.82f,.21f,.79f,.11f,.034f,_wood,4);
                    Stroke(.47f,.24f,.35f,.08f,.029f,_wood);
                    Ellipse(.425f,.05f,.09f,.075f,_wood);
                    Face(new Vector2(.64f,.30f),new Vector2(.78f,.27f),new Vector2(.87f,.63f),new Vector2(.59f,.67f),_fabric);
                    Stroke(.71f,.35f,.74f,.60f,.014f,Shade(_fabric,.65f));
                    break;
                case FloorplanFurnitureKind.LaundryHamper:
                    Ellipse(.12f,.34f,.80f,.60f,_shadow);
                    SoftQuad(.17f,.25f,.64f,.52f,Shade(_wood,.84f));
                    Ellipse(.17f,.55f,.64f,.29f,Shade(_wood,.84f));
                    if(_detail)
                    {
                        for(int strand=0;strand<4;strand++) Quad(.25f+strand*.14f,.36f,.018f,.33f,Shade(_wood,1.18f));
                        Quad(.19f,.50f,.60f,.025f,Shade(_wood,.64f));
                        Quad(.19f,.63f,.60f,.025f,Shade(_wood,.64f));
                    }
                    Ellipse(.13f,.13f,.72f,.40f,_wood);
                    Ellipse(.20f,.18f,.58f,.28f,Shade(_wood,.51f));
                    SoftQuad(.28f,.20f,.31f,.14f,_cream);
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

        private void DrawTraditionalCrib()
        {
            Color timber = Shade(_wood, .80f), edge = Shade(_wood, 1.06f);
            Face(new Vector2(.16f,.43f), new Vector2(.72f,.32f), new Vector2(.95f,.78f), new Vector2(.38f,.93f), _shadow);
            // Open rear railing, then mattress, then the near railing: no solid box sides.
            Stroke(.15f,.20f,.70f,.11f,.040f,timber);
            Stroke(.15f,.42f,.70f,.33f,.040f,timber);
            int count = _detail ? 6 : 4;
            for(int i=1;i<count;i++)
            {
                float t=i/(float)count;
                float x=Mathf.Lerp(.15f,.70f,t), y=Mathf.Lerp(.20f,.11f,t);
                Stroke(x,y,x,y+.22f,.015f,edge);
            }
            Face(new Vector2(.33f,.74f),new Vector2(.85f,.64f),new Vector2(.85f,.73f),new Vector2(.33f,.83f),timber);
            Face(new Vector2(.17f,.43f),new Vector2(.69f,.34f),new Vector2(.85f,.64f),new Vector2(.33f,.74f),_cream);
            Face(new Vector2(.59f,.37f),new Vector2(.68f,.35f),new Vector2(.84f,.63f),new Vector2(.75f,.65f),Shade(_leaf,1.12f));
            Stroke(.15f,.42f,.33f,.79f,.030f,timber);
            Stroke(.70f,.33f,.85f,.69f,.030f,timber);
            Stroke(.33f,.79f,.85f,.69f,.045f,timber);
            for(int i=1;i<count;i++)
            {
                float t=i/(float)count;
                float x=Mathf.Lerp(.33f,.85f,t), y=Mathf.Lerp(.48f,.38f,t);
                Stroke(x,y,x,Mathf.Lerp(.79f,.69f,t),.017f,edge);
                if(_detail && i%2==0) Ellipse(x-.013f,y+.14f,.026f,.035f,timber);
            }
            Stroke(.33f,.48f,.85f,.38f,.043f,edge);
            // Arched end rails and ball finials give the crib its traditional silhouette.
            Curve(.15f,.20f,.18f,.12f,.33f,.48f,.039f,edge);
            Curve(.70f,.11f,.83f,.04f,.85f,.38f,.039f,edge);
            for(int i=1;i<=2;i++)
            {
                float t=i/3f, inverse=1-t;
                Vector2 left=inverse*inverse*new Vector2(.15f,.20f)+2*inverse*t*new Vector2(.18f,.12f)+t*t*new Vector2(.33f,.48f);
                Vector2 right=inverse*inverse*new Vector2(.70f,.11f)+2*inverse*t*new Vector2(.83f,.04f)+t*t*new Vector2(.85f,.38f);
                Stroke(left.x,left.y,left.x,Mathf.Lerp(.42f,.79f,(left.x-.15f)/.18f),.015f,edge);
                Stroke(right.x,right.y,right.x,Mathf.Lerp(.33f,.69f,(right.x-.70f)/.15f),.015f,edge);
            }
            Post(.15f,.20f,.43f,timber);
            Post(.70f,.11f,.41f,timber);
            Post(.33f,.48f,.44f,timber);
            Post(.85f,.38f,.44f,timber);
        }

        private void DrawTraditionalRocker()
        {
            Color timber=Shade(_wood,.79f), edge=Shade(_wood,1.05f);
            Ellipse(.13f,.65f,.80f,.30f,_shadow);
            Vector2 leftBack=new(.26f,.49f), rightBack=new(.68f,.42f);
            Vector2 leftFront=new(.41f,.71f), rightFront=new(.83f,.61f);
            Vector2 leftStart=new(.12f,.52f), leftControl=new(.07f,1.02f), leftEnd=new(.59f,.88f);
            Vector2 rightStart=new(.65f,.43f), rightControl=new(.64f,.93f), rightEnd=new(.94f,.79f);
            Curve(leftStart.x,leftStart.y,leftControl.x,leftControl.y,leftEnd.x,leftEnd.y,.041f,timber,12);
            Curve(rightStart.x,rightStart.y,rightControl.x,rightControl.y,rightEnd.x,rightEnd.y,.041f,timber,12);
            // Feet land on the runners, rather than approximating their curved silhouettes.
            Join(leftBack,Bezier(leftStart,leftControl,leftEnd,.5f),.036f,timber);
            Join(rightBack,Bezier(rightStart,rightControl,rightEnd,.5f),.036f,timber);
            Join(leftFront,Bezier(leftStart,leftControl,leftEnd,5f/6f),.036f,timber);
            Join(rightFront,Bezier(rightStart,rightControl,rightEnd,5f/6f),.036f,timber);
            Vector2 crestLeft=new(.20f,.15f), crestControl=new(.35f,.015f), crestRight=new(.60f,.10f);
            Join(crestLeft,leftBack,.041f,timber);
            Join(crestRight,rightBack,.041f,timber);
            int count=_detail?5:3;
            for(int i=1;i<=count;i++)
            {
                float t=i/(float)(count+1);
                Join(Bezier(crestLeft,crestControl,crestRight,t),Vector2.Lerp(leftBack,rightBack,t),.018f,edge);
            }
            Curve(crestLeft.x,crestLeft.y,crestControl.x,crestControl.y,crestRight.x,crestRight.y,.052f,edge,12);
            Face(leftBack+new Vector2(0,.04f),rightBack+new Vector2(0,.04f),
                rightFront+new Vector2(0,.04f),leftFront+new Vector2(0,.04f),timber);
            Face(leftBack,rightBack,rightFront,leftFront,edge);
            Vector2 leftArm=Vector2.Lerp(crestLeft,leftBack,.5f), rightArm=Vector2.Lerp(crestRight,rightBack,.5f);
            Vector2 leftSeatSupport=Vector2.Lerp(leftBack,leftFront,.82f), rightSeatSupport=Vector2.Lerp(rightBack,rightFront,.82f);
            Vector2 leftTip=leftSeatSupport-new Vector2(0,.18f), rightTip=rightSeatSupport-new Vector2(0,.18f);
            Join(leftTip,leftSeatSupport,.028f,timber);
            Join(rightTip,rightSeatSupport,.028f,timber);
            Curve(leftArm.x,leftArm.y,.30f,.26f,leftTip.x,leftTip.y,.033f,edge);
            Curve(rightArm.x,rightArm.y,.75f,.21f,rightTip.x,rightTip.y,.033f,edge);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
            => (1-t)*(1-t)*a+2*(1-t)*t*control+t*t*b;

        private void Join(Vector2 a, Vector2 b, float width, Color color)
            => Stroke(a.x,a.y,b.x,b.y,width,color);

        private void DrawTallBookshelf()
        {
            // An upright five-tier cabinet, drawn front-on with a shallow right-hand return.
            // Its tall silhouette is native geometry, never a quarter-turn of the low shelf.
            Color frame=Shade(_wood,.86f), recess=Shade(_wood,.48f);
            Quad(.09f,.075f,.87f,.91f,_shadow);
            Face(new(.055f,.055f),new(.855f,.055f),new(.93f,.025f),new(.13f,.025f),Shade(_wood,1.14f));
            Face(new(.855f,.055f),new(.93f,.025f),new(.93f,.945f),new(.855f,.975f),Shade(_wood,.65f));
            Quad(.055f,.055f,.80f,.92f,recess);
            for(int row=0;row<5;row++)
            {
                float bottom=.235f+row*.165f;
                int books=_detail?5:4;
                float step=.66f/books;
                for(int book=0;book<books;book++)
                {
                    int variation=(book+row*2)%5;
                    Color cover=variation==0?_fabric:variation==1?RGB(188,111,87):
                        variation==2?_leaf:variation==3?_cream:RGB(143,127,162);
                    float x=.125f+book*step, height=.105f+.009f*((book+row*3)%4);
                    Quad(x,bottom-height,step-.013f,height,cover);
                    if(_detail && book%2==0)
                        Quad(x+.013f,bottom-.035f,step-.039f,.009f,_cream);
                }
                Quad(.10f,bottom,.71f,.023f,_wood);
                Quad(.10f,bottom+.023f,.71f,.009f,Shade(_wood,.63f));
            }
            Quad(.055f,.055f,.053f,.92f,frame);
            Quad(.802f,.055f,.053f,.92f,frame);
            Quad(.035f,.045f,.84f,.032f,Shade(_wood,1.07f));
            Quad(.035f,.937f,.84f,.038f,frame);
        }
        private void DrawFilledBookshelf()
        {
            // Three packed shelves, a visible top and side, and a recessed wooden back.
            Quad(.10f,.15f,.86f,.80f,_shadow);
            Face(new(.06f,.12f),new(.87f,.12f),new(.94f,.06f),new(.13f,.06f),Shade(_wood,1.12f));
            Face(new(.87f,.12f),new(.94f,.06f),new(.94f,.84f),new(.87f,.91f),Shade(_wood,.70f));
            Quad(.06f,.12f,.81f,.79f,Shade(_wood,.53f));
            for(int row=0;row<3;row++)
            {
                float bottom=.35f+row*.25f;
                int books=_detail?9:6;
                float step=.71f/books;
                for(int book=0;book<books;book++)
                {
                    int variation=(book+row*3)%5;
                    Color color=variation==0?_fabric:variation==1?_cream:variation==2?_leaf:
                        variation==3?RGB(192,112,88):RGB(147,133,170);
                    float x=.11f+book*step, height=.145f+.012f*((book*3+row)%4);
                    Quad(x,bottom-height,step-.009f,height,color);
                    if(_detail && book%2==0) Quad(x+.009f,bottom-.045f,step-.027f,.010f,Shade(_cream,.94f));
                }
                Quad(.08f,bottom,.77f,.034f,_wood);
                Quad(.08f,bottom+.034f,.77f,.012f,Shade(_wood,.73f));
            }
            Quad(.06f,.12f,.045f,.79f,_wood);
            Quad(.825f,.12f,.045f,.79f,_wood);
            Quad(.045f,.105f,.84f,.044f,Shade(_wood,1.06f));
            Quad(.045f,.885f,.84f,.036f,_wood);
        }
        private void Post(float x,float y,float height,Color wood)
        {
            Stroke(x,y,x,y+height,.033f,wood);
            Ellipse(x-.027f,y-.031f,.054f,.067f,Shade(wood,1.15f));
        }

        private void Face(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int first=_mesh.currentVertCount;
            _mesh.AddVert(Point(a.x,a.y),color,Vector2.zero);
            _mesh.AddVert(Point(b.x,b.y),color,Vector2.zero);
            _mesh.AddVert(Point(c.x,c.y),color,Vector2.zero);
            _mesh.AddVert(Point(d.x,d.y),color,Vector2.zero);
            _mesh.AddTriangle(first,first+1,first+2);
            _mesh.AddTriangle(first,first+2,first+3);
        }

        private void Stroke(float ax,float ay,float bx,float by,float width,Color color)
        {
            Vector3 a=Point(ax,ay), b=Point(bx,by), direction=b-a;
            Vector3 normal=new Vector3(-direction.y,direction.x).normalized *
                (Mathf.Min(_prop.width,_prop.height)*width*.5f);
            int first=_mesh.currentVertCount;
            _mesh.AddVert(a-normal,color,Vector2.zero);
            _mesh.AddVert(a+normal,color,Vector2.zero);
            _mesh.AddVert(b+normal,color,Vector2.zero);
            _mesh.AddVert(b-normal,color,Vector2.zero);
            _mesh.AddTriangle(first,first+1,first+2);
            _mesh.AddTriangle(first,first+2,first+3);
        }

        private void Curve(float ax,float ay,float cx,float cy,float bx,float by,float width,Color color,int steps=6)
        {
            Vector3 a=Point(ax,ay), control=Point(cx,cy), b=Point(bx,by);
            float halfWidth=Mathf.Min(_prop.width,_prop.height)*width*.5f;
            int first=_mesh.currentVertCount;
            // Shared edge vertices make a continuous ribbon with no gaps between curve segments.
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps, inverse=1-t;
                Vector3 point=inverse*inverse*a+2*inverse*t*control+t*t*b;
                Vector3 tangent=2*inverse*(control-a)+2*t*(b-control);
                Vector3 normal=new Vector3(-tangent.y,tangent.x).normalized*halfWidth;
                _mesh.AddVert(point-normal,color,Vector2.zero);
                _mesh.AddVert(point+normal,color,Vector2.zero);
                if(i==0) continue;
                int previous=first+(i-1)*2;
                _mesh.AddTriangle(previous,previous+1,previous+3);
                _mesh.AddTriangle(previous,previous+3,previous+2);
            }
        }        private void Block(float x, float y, float width, float height, float depth, Color top)
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