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

            if (!root.ClassListContains("modern-layout-installed"))
            {
                root.AddToClassList("modern-layout-installed");
                root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea(root));
            }
            BlueprintCharacter.Install(root);
            FloorplanMenuArt.Install(root);
            ShikakuCityLogo.Install(root);
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

    }
}
