using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI
{
    /// <summary>Small, decorative furnished plans for menu cards; never represents a puzzle solution.</summary>
    public sealed class FloorplanMenuArt : VisualElement
    {
        private static readonly CustomStyleProperty<Color> Ink = new("--fp-art-ink");
        private static readonly CustomStyleProperty<Color> Sage = new("--fp-sage");
        private static readonly CustomStyleProperty<Color> Blue = new("--fp-blue");
        private static readonly CustomStyleProperty<Color> Peach = new("--fp-peach");
        private Color _ink = new Color32(53,72,77,255), _sage = new Color32(219,233,216,255),
            _blue = new Color32(218,233,243,255), _peach = new Color32(242,221,210,255);
        private readonly int _variant;
        private bool _showFurniture;
        private Painter2D _painter;
        private Vector2 _origin;
        private float _scale;
        private static readonly Color Wood = new Color32(191,150,94,255);
        private static readonly Color Fabric = new Color32(90,151,168,255);
        private static readonly Color Cream = new Color32(246,240,214,255);

        public FloorplanMenuArt(int variant = 0, bool showFurniture = true)
        {
            _variant = variant % 3;
            _showFurniture = showFurniture;
            name = "floorplan-menu-art";
            pickingMode = PickingMode.Ignore;
            AddToClassList("floorplan-menu-art");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(evt => {
                if(evt.customStyle.TryGetValue(Ink,out var ink)) _ink=ink;
                if(evt.customStyle.TryGetValue(Sage,out var sage)) _sage=sage;
                if(evt.customStyle.TryGetValue(Blue,out var blue)) _blue=blue;
                if(evt.customStyle.TryGetValue(Peach,out var peach)) _peach=peach;
                MarkDirtyRepaint();
            });
        }

        public static void Install(VisualElement root)
        {
            foreach(string name in new[]{"safe-area","daily-screen","time-trial-screen","free-play-screen",
                "settings-screen","shop-screen","privacy-welcome-screen"})
            {
                var screen=root.Q(name);
                if(screen==null || !screen.ClassListContains("modern-ui")) continue;
                screen.AddToClassList("floorplan-menu");
                screen.Q("modern-blueprint-grid")?.RemoveFromHierarchy();
            }
            var home=root.Q("safe-area");
            var icon=home?.Q<Button>("free-play-button")?.Q(className:"menu-button-icon");
            if(icon!=null && icon.Q<FloorplanMenuArt>()==null) { icon.Clear(); icon.Add(new FloorplanMenuArt()); }
            var trial=root.Q("time-trial-screen");
            int variant=0;
            trial?.Query<VisualElement>(className:"time-trial-plan-preview").ForEach(e=>DecoratePreview(e,variant++, showFurniture: false));
            // Also handles already-populated views (for retained documents and editor previews).
            var free=root.Q("free-play-screen");
            free?.Query<VisualElement>(className:"free-play-plan-preview").ForEach(e=>DecoratePreview(e));
            free?.Query<VisualElement>(className:"free-play-size-preview").ForEach(e=>DecoratePreview(e));
        }

        public static void DecoratePreview(VisualElement preview, int variant=0, bool showFurniture=true)
        {
            if(preview==null) return;
            var existing=preview.Q<FloorplanMenuArt>();
            if(existing!=null)
            {
                if(existing._showFurniture!=showFurniture) { existing._showFurniture=showFurniture; existing.MarkDirtyRepaint(); }
                return;
            }
            foreach(var child in preview.Children())
                if(!(child is Label) || child.ClassListContains("free-play-plan-marker")) child.style.display=DisplayStyle.None;
            preview.Insert(0,new FloorplanMenuArt(variant,showFurniture));
            preview.AddToClassList("floorplan-illustrated-preview");
        }

        private void Draw(MeshGenerationContext context)
        {
            _painter=context.painter2D;
            _scale=Mathf.Min(contentRect.width/200f,contentRect.height/160f);
            if(_scale<=0) return;
            _origin=contentRect.center-new Vector2(100,80)*_scale;
            Rect(7,7,186,146,_sage);
            Rect(113,7,80,77,_blue); Rect(113,84,80,69,_peach);
            // Strong shared walls and small blue exterior windows echo the puzzle renderer.
            Line(7,7,193,7,3,_ink);Line(193,7,193,153,3,_ink);
            Line(193,153,7,153,3,_ink);Line(7,153,7,7,3,_ink);
            Line(113,7,113,104,2.5f,_ink);Line(113,128,113,153,2.5f,_ink);
            Line(113,84,147,84,2.5f,_ink);Line(170,84,193,84,2.5f,_ink);
            Line(113,104,113,128,.8f,_ink);Line(113,104,92,104,1.4f,_ink);
            Line(147,84,170,84,.8f,_ink);Line(147,84,147,104,1.4f,_ink);
            Line(39,7,76,7,2.3f,Fabric);Line(193,104,193,134,2.3f,Fabric);
            if(!_showFurniture) { _painter=null; return; }
            if(_variant==1) {
                Block(26,32,64,26,5,Wood);Rect(46,36,25,12,Shade(Fabric,.5f));
                Block(47,65,26,24,4,Fabric);Block(24,113,51,18,4,Wood);
                for(int i=0;i<5;i++)Rect(29+i*8,116,6,10,i%2==0?Fabric:Cream);
            } else {
                Block(28,29,58,88,6,Wood);Block(32,31,50,78,4,Cream);
                Rect(36,38,19,17,Cream);Rect(59,38,19,17,Shade(Cream,.90f));
                Block(32,61,50,48,4,new Color32(116,163,104,255));
                Block(87,34,17,18,3,Wood);
            }
            Block(127,24,50,31,5,_variant==2?Fabric:new Color32(199,121,83,255));
            Rect(127,23,50,9,Shade(_variant==2?Fabric:new Color32(199,121,83,255),1.12f));
            Block(143,62,22,12,3,Wood);
            Block(145,109,28,24,4,Wood);
            Block(134,113,8,15,3,Fabric);Block(177,113,8,15,3,Fabric);
            _painter=null;
        }
        private static Color Shade(Color c,float value)=>new(c.r*value,c.g*value,c.b*value,c.a);
        private Vector2 Point(float x,float y)=>_origin+new Vector2(x,y)*_scale;
        private void Rect(float x,float y,float w,float h,Color color)
        {
            _painter.fillColor=color;_painter.BeginPath();_painter.MoveTo(Point(x,y));
            _painter.LineTo(Point(x+w,y));_painter.LineTo(Point(x+w,y+h));_painter.LineTo(Point(x,y+h));
            _painter.ClosePath();_painter.Fill();
        }
        private void Block(float x,float y,float w,float h,float depth,Color color)
        { Rect(x+2,y+3,w,h,new Color(0.08f,.15f,.17f,.12f));Rect(x,y+depth,w,h-depth,Shade(color,.76f));Rect(x,y,w,h-depth,color); }
        private void Line(float ax,float ay,float bx,float by,float width,Color color)
        { _painter.strokeColor=color;_painter.lineWidth=width*_scale;_painter.BeginPath();_painter.MoveTo(Point(ax,ay));_painter.LineTo(Point(bx,by));_painter.Stroke(); }
    }
}