using System;
using System.Runtime.CompilerServices;
using Shikaku.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI
{
    /// <summary>Illustrated blueprint accents and brief, event-driven character moments.</summary>
    public static class BlueprintCharacter
    {
        private static readonly ConditionalWeakTable<VisualElement, Arrival> Arrivals = new();
        private static readonly string[] Expressions = {
            "rivet-welcome", "rivet-teach", "rivet-inspect", "rivet-hint", "rivet-celebrate", "rivet-concerned"
        };

        public static void Install(VisualElement root)
        {
            if (root == null) return;
            if (!root.ClassListContains("character-installed"))
            {
                root.AddToClassList("character-installed");
                bool listening = false;
                Action refresh = () => root.EnableInClassList("character-reduced-motion", AppSettings.ReduceMotion);
                Action attach = () => {
                    if (!listening) { AppSettings.Changed += refresh; listening = true; }
                    refresh();
                };
                root.RegisterCallback<AttachToPanelEvent>(_ => attach());
                root.RegisterCallback<DetachFromPanelEvent>(_ => {
                    AppSettings.Changed -= refresh; listening = false;
                });
                if (root.panel != null) attach(); else refresh();
            }

            var home = root.Q("safe-area");
            if (home != null && home.ClassListContains("modern-ui"))
            {
                AddHomeArt(home, "adventure-button", "city");
                AddHomeArt(home, "free-play-button", "plans");
                AddHomeArt(home, "time-trial-button", "timer");
                AddHomeArt(home, "daily-button", "calendar");
            }
            Header(root.Q("daily-screen"), "blueprint-document-header", "calendar");
            Header(root.Q("time-trial-screen"), "blueprint-document-header", "timer");
            Header(root.Q("free-play-screen"), "free-play-drawing-header", "plans");
            Header(root.Q("settings-screen"), "settings-title-block", "tools");
            Header(root.Q("shop-screen"), "shop-title-block", "idea");

            var solved = root.Q("gameplay-solved-modal");
            var title = solved?.Q<Label>("gameplay-solved-title");
            if (title != null && solved.Q("character-approval") == null)
            {
                var approval = new VisualElement { name = "character-approval", pickingMode = PickingMode.Ignore };
                approval.AddToClassList("character-approval");
                var check = new Sketch("check");
                check.AddToClassList("character-approval-mark");
                approval.Add(check);
                var label = new Label("Nicely drawn!") { name = "character-approval-copy" };
                approval.Add(label);
                title.parent.Insert(title.parent.IndexOf(title) + 1, approval);
            }
        }

        private static void AddHomeArt(VisualElement home, string name, string kind)
        {
            var icon = home.Q<Button>(name)?.Q(className: "menu-button-icon");
            if (icon == null || icon.ClassListContains("character-home-art")) return;
            icon.AddToClassList("character-home-art");
            icon.Add(new Sketch(kind));
        }

        private static void Header(VisualElement screen, string headerClass, string kind)
        {
            if (screen == null || !screen.ClassListContains("modern-ui")) return;
            screen.Query<VisualElement>(className: headerClass).ForEach(header => {
                if (header.Q(className: "character-header-art") != null) return;
                var art = new Sketch(kind);
                art.AddToClassList("character-header-art");
                header.Add(art);
                var title = header.Q<Label>(className: "blueprint-document-title") ??
                    header.Q<Label>(className: "free-play-context-title") ?? header.Q<Label>(className: "free-play-drawing-title") ??
                    header.Q<Label>(className: "settings-title") ?? header.Q<Label>(className: "shop-title");
                title?.AddToClassList("character-page-title");
            });
        }

        public static void Tutorial(VisualElement root, int step)
        {
            var mascot = root?.Q("gameplay-tutorial-card")?.Q(className: "rivet");
            if (mascot == null) return;
            string expression = step == 0 ? "rivet-welcome" : step >= 6 ? "rivet-celebrate" :
                step == 3 || step == 4 ? "rivet-inspect" : step == 5 ? "rivet-hint" : "rivet-teach";
            foreach (string name in Expressions) mascot.EnableInClassList(name, name == expression);
            Arrivals.GetValue(mascot, e => new Arrival(e)).Play(false);
        }

        public static void Result(VisualElement modal, bool personalBest, bool timeTrial, bool tutorial)
        {
            if (modal == null) return;
            bool building = modal.ClassListContains("gameplay-solved-with-building");
            modal.EnableInClassList("character-personal-best", personalBest);
            var approval = modal.Q("character-approval");
            if (approval != null)
            {
                approval.style.display = building ? DisplayStyle.None : DisplayStyle.Flex;
                approval.Q<Label>("character-approval-copy").text = personalBest ? "New personal best!" :
                    tutorial ? "Ready to build!" : timeTrial ? "Draft complete" : "Nicely drawn!";
                var mark = approval.Q<Sketch>();
                if (!building) mark?.Reveal();
            }
            var mascot = modal.Q(className: "rivet-celebrate");
            if (!building && mascot != null)
                Arrivals.GetValue(mascot, e => new Arrival(e)).Play(true);
        }

        private sealed class Arrival
        {
            private readonly VisualElement _element;
            private IVisualElementScheduledItem _tick;
            private float _start;
            private bool _celebrate;
            public Arrival(VisualElement element)
            {
                _element = element;
                element.RegisterCallback<DetachFromPanelEvent>(_ => Finish());
            }
            public void Play(bool celebrate)
            {
                Finish();
                if (AppSettings.ReduceMotion) return;
                _celebrate = celebrate;
                _start = Time.realtimeSinceStartup;
                _tick = _element.schedule.Execute(Tick).Every(16);
            }
            private void Tick()
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - _start) / 0.48f);
                if (AppSettings.ReduceMotion || t >= 1) { Finish(); return; }
                float wave = Mathf.Sin(t * Mathf.PI);
                float lift = _celebrate ? -18 * wave : 8 * (1 - t);
                _element.style.translate = new Translate(0, lift);
                float scale = 1 + (_celebrate ? 0.09f : -0.06f) * wave;
                _element.style.scale = new Scale(new Vector3(scale, scale, 1));
            }
            private void Finish()
            {
                _tick?.Pause(); _tick = null;
                _element.style.translate = StyleKeyword.Null;
                _element.style.scale = StyleKeyword.Null;
            }
        }

        // All motifs are crisp retained vectors. Coordinates share a 200 x 160 drawing sheet.
        public sealed class Sketch : VisualElement
        {
            private static readonly CustomStyleProperty<Color> InkProperty = new("--character-ink");
            private static readonly CustomStyleProperty<Color> AccentProperty = new("--character-accent");
            private static readonly CustomStyleProperty<Color> PaperProperty = new("--character-paper");
            private Color _ink = new Color32(41, 101, 135, 255);
            private Color _accent = new Color32(205, 152, 64, 255);
            private Color _paper = new Color32(232, 241, 243, 255);
            private readonly string _kind;
            private float _progress = 1;
            private IVisualElementScheduledItem _reveal;
            public Sketch(string kind)
            {
                _kind = kind;
                AddToClassList("character-sketch");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                RegisterCallback<CustomStyleResolvedEvent>(evt => {
                    if (evt.customStyle.TryGetValue(InkProperty, out Color ink)) _ink = ink;
                    if (evt.customStyle.TryGetValue(AccentProperty, out Color accent)) _accent = accent;
                    if (evt.customStyle.TryGetValue(PaperProperty, out Color paper)) _paper = paper;
                    MarkDirtyRepaint();
                });
                RegisterCallback<DetachFromPanelEvent>(_ => FinishReveal());
            }
            public void Reveal()
            {
                FinishReveal();
                if (AppSettings.ReduceMotion) return;
                _progress = 0;
                float start = Time.realtimeSinceStartup;
                _reveal = schedule.Execute(() => {
                    _progress = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.42f);
                    if (AppSettings.ReduceMotion || _progress >= 1) FinishReveal();
                    MarkDirtyRepaint();
                }).Every(16);
            }
            private void FinishReveal()
            {
                _reveal?.Pause(); _reveal = null; _progress = 1; MarkDirtyRepaint();
            }
            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float scale = Mathf.Min(contentRect.width / 200, contentRect.height / 160);
                Vector2 offset = new((contentRect.width - 200 * scale) / 2, (contentRect.height - 160 * scale) / 2);
                Vector2 Point(float x, float y) => offset + new Vector2(x, y) * scale;
                void Path(Color color, params Vector2[] points) {
                    p.lineWidth = 3 * scale; p.strokeColor = color; p.BeginPath();
                    for (int i = 0; i < points.Length; i++) {
                        var v = Point(points[i].x, points[i].y);
                        if (i == 0) p.MoveTo(v); else p.LineTo(v);
                    }
                    p.Stroke();
                }
                void Box(float x, float y, float w, float h, Color color) {
                    p.fillColor = color; p.BeginPath(); p.MoveTo(Point(x,y));
                    p.LineTo(Point(x+w,y)); p.LineTo(Point(x+w,y+h)); p.LineTo(Point(x,y+h));
                    p.ClosePath(); p.Fill();
                }
                void Frame(float x, float y, float w, float h) =>
                    Path(_ink, new(x,y), new(x+w,y), new(x+w,y+h), new(x,y+h), new(x,y));
                void Circle(float x, float y, float radius) {
                    var pts = new Vector2[41];
                    for (int i=0;i<=40;i++) { float a=i*Mathf.PI*2/40; pts[i]=new(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius); }
                    Path(_ink,pts);
                }

                if (_kind == "check") {
                    var a = new Vector2(44,82); var b = new Vector2(82,118); var c = new Vector2(160,40);
                    if (_progress < 0.35f) Path(_accent,a,Vector2.Lerp(a,b,_progress/0.35f));
                    else Path(_accent,a,b,Vector2.Lerp(b,c,(_progress-0.35f)/0.65f));
                    return;
                }
                // Short registration marks frame the drawing without adding another full grid.
                Path(_accent,new(10,35),new(10,15),new(30,15));
                Path(_accent,new(170,145),new(190,145),new(190,125));
                if (_kind == "plans") {
                    Box(47,20,126,106,_paper); Frame(47,20,126,106);
                    Box(29,36,126,106,_paper); Frame(29,36,126,106);
                    Box(32,39,54,48,new Color(_accent.r,_accent.g,_accent.b,0.24f));
                    Path(_ink,new(88,36),new(88,142)); Path(_ink,new(29,90),new(155,90));
                    Path(_ink,new(118,90),new(118,142));
                    Path(_accent,new(40,151),new(144,151));
                } else if (_kind == "timer") {
                    Box(88,13,28,10,_accent); Path(_ink,new(102,23),new(102,34));
                    Circle(102,88,53); Circle(102,88,45);
                    for(int i=0;i<12;i++) { float a=i*Mathf.PI/6;
                        Path(_ink,new(102+Mathf.Sin(a)*39,88+Mathf.Cos(a)*39),new(102+Mathf.Sin(a)*44,88+Mathf.Cos(a)*44)); }
                    Path(_accent,new(102,52),new(102,88),new(129,102));
                    Path(_ink,new(140,44),new(153,30)); Path(_accent,new(145,26),new(159,39));
                } else if (_kind == "calendar") {
                    Box(31,34,136,105,_paper);Frame(31,34,136,105);
                    Box(33,36,132,26,_accent); Path(_ink,new(65,22),new(65,45));Path(_ink,new(133,22),new(133,45));
                    for(int y=0;y<2;y++) for(int x=0;x<3;x++) Box(49+x*38,77+y*31,15,12,_ink);
                    Box(113,100,44,33,_paper);Path(_accent,new(116,114),new(127,125),new(150,101));
                } else if (_kind == "city") {
                    Box(30,70,43,65,_paper);Frame(30,70,43,65);
                    Box(78,30,48,105,_paper);Frame(78,30,48,105);
                    Box(131,89,40,46,_paper);Frame(131,89,40,46);
                    for(int y=0;y<4;y++)for(int x=0;x<2;x++)Box(88+x*19,43+y*21,9,11,y==3?_accent:_ink);
                    for(int y=0;y<2;y++)for(int x=0;x<2;x++)Box(39+x*18,81+y*22,8,10,_accent);
                    Path(_ink,new(19,138),new(181,138));Path(_accent,new(69,19),new(134,19));
                } else if (_kind == "idea") {
                    Circle(100,66,34);Path(_ink,new(78,91),new(82,119),new(118,119),new(122,91));
                    Path(_accent,new(87,130),new(113,130));Path(_accent,new(93,140),new(107,140));
                    Path(_accent,new(88,68),new(100,83),new(113,67));Path(_accent,new(100,83),new(100,112));
                    for(int i=0;i<5;i++){float a=(i*45+180)*Mathf.Deg2Rad;
                        Path(_accent,new(100+Mathf.Cos(a)*46,66+Mathf.Sin(a)*46),new(100+Mathf.Cos(a)*58,66+Mathf.Sin(a)*58));}
                } else {
                    Box(31,99,139,30,_paper);Frame(31,99,139,30);
                    for(int i=0;i<8;i++)Path(_ink,new(43+i*16,99),new(43+i*16,i%2==0?116:109));
                    Path(_accent,new(50,83),new(139,24),new(151,42),new(62,101),new(42,103),new(50,83));
                    Path(_ink,new(129,31),new(141,49));
                }
            }
        }
    }
}
