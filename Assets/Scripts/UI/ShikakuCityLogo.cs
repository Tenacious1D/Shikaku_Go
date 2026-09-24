using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI
{
    /// <summary>A crisp city mark built from rectangular puzzle-room silhouettes.</summary>
    public sealed class ShikakuCityLogo : VisualElement
    {
        private static readonly CustomStyleProperty<Color> InkProperty = new("--city-logo-ink");
        private static readonly CustomStyleProperty<Color> PaperProperty = new("--city-logo-paper");
        private Color _ink = new Color32(53,72,77,255), _paper = new Color32(255,253,247,255);
        private Painter2D _painter;
        private float _scale;
        private Vector2 _origin;
        public ShikakuCityLogo()
        {
            name = "shikaku-city-logo";
            AddToClassList("shikaku-city-logo");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(evt => {
                if(evt.customStyle.TryGetValue(InkProperty,out var ink)) _ink=ink;
                if(evt.customStyle.TryGetValue(PaperProperty,out var paper)) _paper=paper;
                MarkDirtyRepaint();
            });
        }

        public static void Install(VisualElement root)
        {
            var home=root.Q("safe-area");
            if(home!=null && home.ClassListContains("modern-ui")) Replace(home.Q("brand-mark"));
            var welcome=root.Q("privacy-welcome-screen");
            if(welcome!=null && welcome.ClassListContains("modern-ui"))
                Replace(welcome.Q(className:"privacy-welcome-brand-mark"));
        }

        private static void Replace(VisualElement host)
        {
            if(host==null || host.Q<ShikakuCityLogo>()!=null) return;
            host.Clear(); host.AddToClassList("city-logo-host"); host.Add(new ShikakuCityLogo());
        }

        private void Draw(MeshGenerationContext context)
        {
            _scale=Mathf.Min(contentRect.width/260f,contentRect.height/170f);
            if(_scale<=0) return;
            _origin=contentRect.center-new Vector2(130,85)*_scale;
            _painter=context.painter2D;
            Color sage=new Color32(158,188,150,255), blue=new Color32(117,175,190,255),
                peach=new Color32(220,154,119,255), gold=new Color32(224,182,103,255);
            // Shared edges connect the rooms into a compact skyline, with small intentional gaps.
            Box(23,92,58,60,sage);
            Box(85,23,61,62,blue);
            Box(85,89,61,63,gold);
            Box(150,59,54,93,peach);
            Box(208,110,29,42,sage);
            Window(36,105,12,14); Window(55,105,12,14);
            Window(36,128,12,14); Window(55,128,12,14);
            Window(98,38,13,15); Window(120,38,13,15);
            Window(98,61,13,12); Window(120,61,13,12);
            // A double-height doorway also reads as an unfilled puzzle rectangle.
            Window(105,114,21,38);
            Window(162,72,12,17); Window(182,72,10,17);
            Window(162,100,12,17); Window(182,100,10,17);
            Window(163,128,29,13);
            Window(217,121,11,18);
            // A quiet drafting baseline anchors the mark without a background grid.
            Line(17,158,243,158,2,_ink);
            _painter=null;
        }
        private Vector2 P(float x,float y)=>_origin+new Vector2(x,y)*_scale;
        private void Box(float x,float y,float width,float height,Color color)
        {
            _painter.fillColor=color;_painter.strokeColor=_ink;_painter.lineWidth=2f*_scale;
            _painter.BeginPath();_painter.MoveTo(P(x,y));_painter.LineTo(P(x+width,y));
            _painter.LineTo(P(x+width,y+height));_painter.LineTo(P(x,y+height));
            _painter.ClosePath();_painter.Fill();_painter.Stroke();
        }
        private void Window(float x,float y,float width,float height)
        {
            _painter.fillColor=_paper;_painter.BeginPath();_painter.MoveTo(P(x,y));
            _painter.LineTo(P(x+width,y));_painter.LineTo(P(x+width,y+height));_painter.LineTo(P(x,y+height));
            _painter.ClosePath();_painter.Fill();
        }
        private void Line(float ax,float ay,float bx,float by,float width,Color color)
        { _painter.strokeColor=color;_painter.lineWidth=width*_scale;_painter.BeginPath();_painter.MoveTo(P(ax,ay));_painter.LineTo(P(bx,by));_painter.Stroke(); }
    }
}