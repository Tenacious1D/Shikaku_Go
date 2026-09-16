#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Shikaku.Menu;
using Shikaku.SaveSystem;
using Shikaku.UI.Buildings;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shikaku.Tests
{
    public sealed class AdventureBuildingCityTests
    {
        private GameObject _host;
        private PanelSettings _settings;
        private RenderTexture _target;
        private AdventureScreenController _controller;

        [TearDown]
        public void Cleanup()
        {
            _controller?.Dispose();
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
            SaveManager.ClearIsolatedSaveForTesting();
        }

        [UnityTest]
        public IEnumerator RealAdventureScreenUsesFreestandingNodesAndRendersCity()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(folder);
            SaveManager.LoadIsolatedSaveForTesting(Path.Combine(folder, "city-test-" + Guid.NewGuid().ToString("N") + ".json"));
            var packs = PuzzleCatalog.StoryPacks;
            for (int i = 0; i < packs.Count; i++)
            {
                var ids = PuzzleCatalog.GetPackPuzzleIds(packs[i].PackPath);
                for (int j = 0; j < (i < 4 ? ids.Count : 3); j++) PuzzleProgressStore.MarkCompleted(ids[j]);
            }
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _target = new RenderTexture(900, 1600, 24);
            _target.Create();
            _settings.targetTexture = _target;
            _settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _settings.clearColor = true;
            _settings.colorClearValue = new Color32(22, 43, 55, 255);
            _host = new GameObject("City map test") { hideFlags = HideFlags.HideAndDontSave };
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = _settings;
            var root = document.rootVisualElement;
            root.style.width = 900; root.style.height = 1600;
            root.AddToClassList("theme-dark");
            foreach (string path in new[] { "Assets/UI/Screens/Home/HomeScreen.uss",
                "Assets/UI/Screens/Adventure/AdventureScreen.uss", "Assets/UI/Themes/DarkTheme.uss",
                "Assets/UI/Themes/BlueprintTheme.uss" })
                root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path));
            var home = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Home/HomeScreen.uxml").CloneTree();
            var adventure = home.Q<VisualElement>("adventure-screen");
            adventure.RemoveFromHierarchy();
            root.Add(adventure);
            _controller = new AdventureScreenController(root, () => {});
            _controller.Show(packs[0].PackPath);
            var scroll = root.Q<ScrollView>("adventure-map-scroll");
            for (int i = 0; i < 12; i++) yield return null;
            scroll.scrollOffset = Vector2.zero;
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(root.Query<BuildingView>().ToList().Count, Is.EqualTo(packs.Count));
            foreach (var view in root.Query<BuildingView>().ToList())
                Assert.That(view.parent, Is.Not.InstanceOf<Button>());
            Assert.That(root.Query(className: "city-chapter-button").ToList().Count, Is.EqualTo(packs.Count));
            Capture("AdventureCity-top.png", folder);
            scroll.scrollOffset = new Vector2(0, AdventureCityMap.DistrictSpan);
            for (int i = 0; i < 4; i++) yield return null;
            Capture("AdventureCity-lower.png", folder);
            scroll.scrollOffset = new Vector2(0, AdventureCityMap.DistrictSpan + 420);
            for (int i = 0; i < 4; i++) yield return null;
            Capture("AdventureCity-district-bottom.png", folder);
            // Exercise a narrower portrait viewport with the real theme/UXML.
            _target.Release(); UnityEngine.Object.DestroyImmediate(_target);
            _target = new RenderTexture(720, 1280, 24); _target.Create();
            _settings.targetTexture = _target;
            root.style.width = 720; root.style.height = 1280;
            scroll.scrollOffset = Vector2.zero;
            for (int i = 0; i < 8; i++) yield return null;
            Capture("AdventureCity-portrait.png", folder);
            var map = root.Q<AdventureCityMap>();
            int expectedDistricts = Mathf.Max(1,
                Mathf.CeilToInt(packs.Count / (float)AdventureCityMap.BuildingsPerDistrict));
            float expectedMapHeight =
                (expectedDistricts * AdventureCityMap.DistrictSpan + 30) * map.contentRect.width / 790f;
            Assert.That(map.resolvedStyle.height, Is.EqualTo(expectedMapHeight).Within(2),
                "Each ten-chapter district should retain its authored height.");
            AssertNoPlotOverlaps(map);
            var originalViews = map.Query<BuildingView>().ToList();
            Assert.That(map.Query<AdventureCityScenery>().ToList().Count, Is.EqualTo(2), "Scenery should use retained rear and foreground layers.");
            foreach (var view in originalViews)
            {
                var target = view.parent.Q<Button>();
                Assert.That(target.resolvedStyle.backgroundColor.a, Is.Zero, "The hit area must not become a card.");
                Assert.That(target.worldBound.Contains(view.worldBound.center), Is.True);
                Vector2 pickPoint = new Vector2(view.worldBound.center.x, view.worldBound.yMax - 20);
                if (root.worldBound.Contains(pickPoint))
                    Assert.That(map.panel.Pick(pickPoint), Is.SameAs(target), "Tapping a visible building should hit its chapter button.");
            }
            Color darkBackground = map.resolvedStyle.backgroundColor;
            var streetProperty = new CustomStyleProperty<Color>("--city-asphalt");
            Assert.That(map.customStyle.TryGetValue(streetProperty, out Color darkStreet), Is.True);
            // Switch the existing map in place, exactly as ThemeManager's ancestor classes do.
            root.RemoveFromClassList("theme-dark");
            root.AddToClassList("theme-light");
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(map.resolvedStyle.backgroundColor.grayscale, Is.GreaterThan(darkBackground.grayscale + 0.4f));
            Assert.That(map.customStyle.TryGetValue(streetProperty, out Color lightStreet), Is.True);
            Assert.That(lightStreet, Is.Not.EqualTo(darkStreet));
            CollectionAssert.AreEqual(originalViews, map.Query<BuildingView>().ToList(), "Theme changes must reuse buildings and progress.");
            Capture("AdventureCity-light.png", folder);
            AssertStreetColor(ReadStreetPixel(map), lightStreet);
            root.RemoveFromClassList("theme-light");
            root.AddToClassList("theme-dark");
            for (int i = 0; i < 8; i++) yield return null;
            AssertStreetColor(ReadStreetPixel(map), darkStreet);
            var firstChapter = root.Q<Button>("adventure-chapter-1-button");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = firstChapter; firstChapter.SendEvent(submit); }
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(root.Q("adventure-map-view").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "The original chapter selector must still open.");
            // Many future chapters remain lightweight and stay within the map width.
            _controller.Dispose(); _controller = null;
            root.Clear();
            var city = new AdventureCityMap(40);
            root.Add(city);
            for (int i = 0; i < 40; i++)
                city.AddChapter(i, AdventureBuildingData.Load(packs[i % packs.Count].PackPath), new Button { text = $"Chapter {i + 1}" });
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(city.Query<BuildingView>().ToList().Count, Is.EqualTo(40));
            AssertNoPlotOverlaps(city);
            root.style.width = 390;
            for (int i = 0; i < 4; i++) yield return null;
            AssertNoPlotOverlaps(city);
            var narrowViews = city.Query<BuildingView>().ToList();
            Assert.That(city.Query<AdventureCityScenery>().ToList().Count, Is.EqualTo(2));
            Assert.That(narrowViews[0].worldBound.width, Is.GreaterThan(90), "Building touch areas must remain generous on a narrow viewport.");
            foreach (var view in narrowViews)
            {
                Assert.That(view.worldBound.xMin, Is.GreaterThanOrEqualTo(city.worldBound.xMin - 1));
                Assert.That(view.worldBound.xMax, Is.LessThanOrEqualTo(city.worldBound.xMax + 1));
            }
        }

        private static void AssertNoPlotOverlaps(AdventureCityMap city)
        {
            var plots = city.Query(className: "city-building-node").ToList();
            for (int i = 0; i < plots.Count; i++)
            {
                Assert.That(plots[i].worldBound.yMin, Is.GreaterThanOrEqualTo(city.worldBound.yMin));
                Assert.That(plots[i].worldBound.yMax, Is.LessThanOrEqualTo(city.worldBound.yMax));
                for (int j = i + 1; j < plots.Count; j++)
                {
                    Rect a = plots[i].worldBound;
                    Rect b = plots[j].worldBound;
                    if (!a.Overlaps(b)) continue;

                    // A shallow overlap is intentional where the upper court meets the road-side
                    // plots. Perspective sorting keeps the nearer building and its hit target on top.
                    float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    float maxPerspectiveOverlap = 33f * city.contentRect.width / 790f;
                    Assert.That(Mathf.Min(overlapX, overlapY), Is.LessThanOrEqualTo(maxPerspectiveOverlap),
                        $"Building and button plots {i} {a} and {j} {b} overlap too deeply.");
                }
            }
        }

        private static void AssertStreetColor(Color32 actual, Color expected)
        {
            Color32 color = expected;
            // Painter2D's render-target color conversion can round a channel by one byte.
            Assert.That((int)actual.r, Is.EqualTo((int)color.r).Within(1), "Street red channel must follow the active palette.");
            Assert.That((int)actual.g, Is.EqualTo((int)color.g).Within(1), "Street green channel must follow the active palette.");
            Assert.That((int)actual.b, Is.EqualTo((int)color.b).Within(1), "Street blue channel must follow the active palette.");
        }

        private Color32 ReadStreetPixel(AdventureCityMap city)
        {
            var previous = RenderTexture.active;
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = _target;
                // Inside asphalt, off the center marking and clear of a junction.
                int x = Mathf.RoundToInt(city.worldBound.xMin + 15 * city.contentRect.width / 790);
                int y = Mathf.RoundToInt(city.worldBound.yMin + 785 * city.contentRect.width / 790);
                pixel.ReadPixels(new Rect(x, _target.height - 1 - y, 1, 1), 0, 0);
                pixel.Apply();
                return pixel.GetPixels32()[0];
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixel); }
        }

        private void Capture(string name, string folder)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = _target;
                image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG());
                int bright = 0;
                foreach (var pixel in image.GetPixels32()) if (pixel.r > 130 && pixel.g > 110) bright++;
                Assert.That(bright, Is.GreaterThan(3000), "Buildings, labels and streets must actually render.");
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
#endif
