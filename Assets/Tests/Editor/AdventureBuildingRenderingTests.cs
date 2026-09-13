#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using Shikaku.Logic;
using Shikaku.UI.Buildings;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shikaku.Tests
{
    public class AdventureBuildingRenderingTests
    {

        private GameObject _host;
        private PanelSettings _settings;
        private RenderTexture _target;
        private AdventureBuildingPreview _window;

        [TearDown]
        public void Cleanup()
        {
            if (_window != null) _window.Close();
            if (_host != null) Object.DestroyImmediate(_host);
            if (_settings != null) Object.DestroyImmediate(_settings);
            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
            }

        }

        [UnityTest]
        public IEnumerator SharedViewsRenderAndConstructionCompletesOnAttachedPanel()
        {
            // Isolate from gameplay services and player saves.

            var settings = _settings = ScriptableObject.CreateInstance<PanelSettings>();
            var target = _target = new RenderTexture(1200, 760, 24);
            target.Create();
            settings.targetTexture = target;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.94f, 0.91f, 0.83f);
            var host = _host = new GameObject("Building rendering test");
            host.hideFlags = HideFlags.HideAndDontSave;
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var root = document.rootVisualElement;
            root.style.width = 1200;
            root.style.height = 760;
            root.style.flexDirection = FlexDirection.Row;
            var pack = new PuzzlePackData { puzzles = new[]
            {
                new PuzzleEntry { id = "one", width = 7, height = 7, mask = "1111111/1111111/0111110/0111110/0111110/1111111/1111111" },
                new PuzzleEntry { id = "two", width = 7, height = 6 },
                new PuzzleEntry { id = "three", width = 6, height = 6 },
                new PuzzleEntry { id = "four", width = 6, height = 5 },
                new PuzzleEntry { id = "five", width = 5, height = 5, mask = "11111/11011/10001/11011/11111" },
                new PuzzleEntry { id = "six", width = 8, height = 4, mask = "11111111/00111100/00111100/11111111" }
            }};
            var data = AdventureBuildingData.FromPack("render", pack);
            BuildingView AddPreview(string title, int progress, float height)
            {
                var column = new VisualElement();
                column.style.width = 300;
                column.style.alignItems = Align.Center;
                var label = new Label(title);
                label.style.fontSize = 18;
                label.style.color = Color.black;
                column.Add(label);
                var view = new BuildingView();
                view.style.width = 290;
                view.style.height = height;
                view.SetBuilding(data);
                view.SetProgress(progress);
                column.Add(view);
                root.Add(column);
                return view;
            }
            var foundation = AddPreview("Unstarted: 7 x 7 pad", 0, 340);
            var map = AddPreview("Map: five floors", 5, 340);
            var solved = AddPreview("Solved: five floors", 5, 510);
            var complete = AddPreview("Overhang + roof", 6, 340);
            int constructed = 0;
            int chapterComplete = 0;
            solved.FloorConstructed += _ => constructed++;
            solved.ChapterCompleted += () => chapterComplete++;
            for (int i = 0; i < 10; i++) yield return null;

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(folder);
            Capture(target, Path.Combine(folder, "AdventureBuildings-before.png"));
            // Runtime panels render in Edit Mode, but their schedulers require a
            // runtime player loop. An editor panel supplies the scheduler clock
            // here without invoking unrelated game startup services.
            var window = _window = ScriptableObject.CreateInstance<AdventureBuildingPreview>();
            window.ShowUtility();
            window.rootVisualElement.Clear();
            var solvedParent = solved.parent;
            window.rootVisualElement.Add(solved);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(solved.panel, Is.Not.Null);
            Assert.That(solved.AnimateFloor(5), Is.True);
            Assert.That(solved.CompletedFloors, Is.EqualTo(5));
            float deadline = Time.realtimeSinceStartup + 5;
            while (solved.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(solved.IsAnimating, Is.False);
            Assert.That(solved.IsComplete, Is.True);
            Assert.That(constructed, Is.EqualTo(1));
            Assert.That(chapterComplete, Is.EqualTo(1));
            Assert.That(solved.AnimateFloor(5), Is.False);
            solvedParent.Add(solved);
            window.Close();
            map.SetProgress(6);
            for (int i = 0; i < 3; i++) yield return null;
            Capture(target, Path.Combine(folder, "AdventureBuildings-after.png"));
            Assert.That(foundation.AnimateFloor(0), Is.True);
            foundation.RemoveFromHierarchy();
            Assert.That(foundation.IsAnimating, Is.False);
            Assert.That(foundation.CompletedFloors, Is.EqualTo(1));

        }

        private static void Capture(RenderTexture target, string path)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            var pixels = image.GetPixels32();
            Color32 background = pixels[0];
            int foreground = 0;
            foreach (var pixel in pixels)
            {
                if (Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) +
                    Mathf.Abs(pixel.b - background.b) > 30) foreground++;
            }
            File.WriteAllBytes(path, image.EncodeToPNG());
            Assert.That(foreground, Is.GreaterThan(pixels.Length / 40),
                "The capture must contain building geometry, not only a blank panel or labels.");
            Object.DestroyImmediate(image);
            RenderTexture.active = previous;
        }
    }
}
#endif
