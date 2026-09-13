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
            scroll.scrollOffset = new Vector2(0, 950);
            for (int i = 0; i < 4; i++) yield return null;
            Capture("AdventureCity-lower.png", folder);
            // Exercise a narrower portrait viewport with the real theme/UXML.
            _target.Release(); UnityEngine.Object.DestroyImmediate(_target);
            _target = new RenderTexture(720, 1280, 24); _target.Create();
            _settings.targetTexture = _target;
            root.style.width = 720; root.style.height = 1280;
            scroll.scrollOffset = Vector2.zero;
            for (int i = 0; i < 8; i++) yield return null;
            Capture("AdventureCity-portrait.png", folder);
            // Many future chapters remain lightweight and stay within the map width.
            _controller.Dispose(); _controller = null;
            root.Clear();
            var city = new AdventureCityMap(40);
            root.Add(city);
            for (int i = 0; i < 40; i++)
                city.AddChapter(i, AdventureBuildingData.Load(packs[i % packs.Count].PackPath), new Button { text = $"Chapter {i + 1}" });
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(city.Query<BuildingView>().ToList().Count, Is.EqualTo(40));
            foreach (var view in city.Query<BuildingView>().ToList())
            {
                Assert.That(view.worldBound.xMin, Is.GreaterThanOrEqualTo(city.worldBound.xMin - 1));
                Assert.That(view.worldBound.xMax, Is.LessThanOrEqualTo(city.worldBound.xMax + 1));
            }
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
