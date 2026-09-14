using UnityEngine;
using UnityEngine.UIElements;

namespace Shikaku.UI
{
    /// <summary>Opt-in blueprint chrome. Never attaches to or restyles Adventure.</summary>
    public static class ModernBlueprintLayout
    {
        public static void Install(VisualElement root)
        {
            if (root == null)
                return;

            foreach (string name in new[] { "safe-area", "daily-screen", "time-trial-screen",
                "free-play-screen", "settings-screen", "shop-screen", "privacy-welcome-screen" })
            {
                var surface = root.Q<VisualElement>(name);
                if (surface != null && surface.ClassListContains("modern-ui") && surface.Q("modern-blueprint-grid") == null)
                    surface.Insert(0, new BlueprintGrid());
            }
            if (!root.ClassListContains("modern-layout-installed"))
            {
                root.AddToClassList("modern-layout-installed");
                root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea(root));
            }
            ApplySafeArea(root);
        }

        public static void ApplySafeArea(VisualElement root)
        {
            float width = root.resolvedStyle.width;
            float height = root.resolvedStyle.height;
            if (width <= 0 || height <= 0 || Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect safe = Screen.safeArea;
            float top = Mathf.Max(64f, (Screen.height - safe.yMax) / Screen.height * height + 20f);
            float bottom = Mathf.Max(40f, safe.yMin / Screen.height * height + 16f);
            float left = Mathf.Max(48f, safe.xMin / Screen.width * width + 16f);
            float right = Mathf.Max(48f, (Screen.width - safe.xMax) / Screen.width * width + 16f);
            // Home owns its safe-area padding. Other pages share a centered content column.
            var home = root.Q<VisualElement>("safe-area");
            if (home != null)
            {
                home.style.paddingTop = top;
                home.style.paddingBottom = bottom;
                home.style.paddingLeft = left;
                home.style.paddingRight = right;
            }
            foreach (string className in new[] { "daily-content", "time-trial-content",
                "free-play-view", "settings-content", "shop-content" })
            {
                root.Query<VisualElement>(className: className).ForEach(content =>
                {
                    content.style.paddingTop = top;
                    content.style.paddingBottom = bottom;
                    content.style.paddingLeft = left;
                    content.style.paddingRight = right;
                });
            }
            float columnOffset = Mathf.Max(0, (width - 1080f) * 0.5f);
            foreach (string name in new[] { "daily-back-button", "time-trial-back-button",
                "free-play-back-button", "settings-back-button", "privacy-preferences-back-button",
                "shop-back-button" })
            {
                var button = root.Q<Button>(name);
                if (button == null) continue;
                button.style.top = top + 8f;
                button.style.left = columnOffset + left;
            }
        }

        // Retained vector lines stay crisp as the panel changes resolution.
        private sealed class BlueprintGrid : VisualElement
        {
            private static readonly CustomStyleProperty<Color> GridColor = new("--bp-grid");
            private Color _line = new Color(0.15f, 0.39f, 0.54f, 0.055f);
            public BlueprintGrid()
            {
                name = "modern-blueprint-grid";
                AddToClassList("modern-blueprint-grid");
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = style.right = style.top = style.bottom = 0;
                generateVisualContent += Draw;
                RegisterCallback<CustomStyleResolvedEvent>(evt =>
                {
                    if (evt.customStyle.TryGetValue(GridColor, out Color color)) _line = color;
                    MarkDirtyRepaint();
                });
            }
            private void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                painter.lineWidth = 1f;
                painter.strokeColor = _line;
                painter.BeginPath();
                const float step = 96f;
                for (float x = step; x < contentRect.width; x += step)
                {
                    painter.MoveTo(new Vector2(x, 0));
                    painter.LineTo(new Vector2(x, contentRect.height));
                }
                for (float y = step; y < contentRect.height; y += step)
                {
                    painter.MoveTo(new Vector2(0, y));
                    painter.LineTo(new Vector2(contentRect.width, y));
                }
                painter.Stroke();
            }
        }
    }
}

