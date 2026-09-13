#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Shikaku.Logic;
using Shikaku.Menu;
using Shikaku.UI.Buildings;
using UnityEngine;

namespace Shikaku.Tests
{
    public class AdventureBuildingTests
    {
        private static AdventureBuildingData MixedChapter()
        {
            return AdventureBuildingData.FromPack("test", new PuzzlePackData
            {
                width = 7, height = 7,
                puzzles = new[]
                {
                    new PuzzleEntry { id = "first" },
                    new PuzzleEntry { id = "narrow", width = 5, height = 4 },
                    new PuzzleEntry { id = "overhang", width = 8, height = 6 }
                }
            });
        }

        [Test]
        public void FloorsPreservePuzzleOrderAndIndependentDimensionOverrides()
        {
            var data = AdventureBuildingData.FromPack("test", new PuzzlePackData
            {
                width = 7, height = 6,
                puzzles = new[]
                {
                    new PuzzleEntry { id = "z", height = 4 },
                    new PuzzleEntry { id = "a", width = 5 },
                    new PuzzleEntry { id = "b", width = 8, height = 3 }
                }
            });
            Assert.That(data.Floors[0].PuzzleId, Is.EqualTo("z"));
            Assert.That(data.Floors[0].Width, Is.EqualTo(7));
            Assert.That(data.Floors[0].Depth, Is.EqualTo(4));
            Assert.That(data.Floors[1].Width, Is.EqualTo(5));
            Assert.That(data.Floors[1].Depth, Is.EqualTo(6));
            Assert.That(data.Floors[2].Width, Is.EqualTo(8));
            Assert.That(data.FindFloor("legacy|a"), Is.EqualTo(1));
        }

        [Test]
        public void CenteredFootprintsSupportSetbacksAndOverhangs()
        {
            var data = MixedChapter();
            Rect ground = BuildingGeometry.Footprint(data.Floors[0]);
            Rect narrow = BuildingGeometry.Footprint(data.Floors[1]);
            Rect overhang = BuildingGeometry.Footprint(data.Floors[2]);
            Assert.That(ground.size, Is.EqualTo(new Vector2(7, 7)));
            Assert.That(narrow.size, Is.EqualTo(new Vector2(5, 4)));
            Assert.That(narrow.xMin, Is.GreaterThan(ground.xMin));
            Assert.That(narrow.yMin, Is.GreaterThan(ground.yMin));
            Assert.That(overhang.xMax, Is.GreaterThan(ground.xMax));
            Assert.That(overhang.center, Is.EqualTo(ground.center));
        }

        [Test]
        public void BoundsIncludeFinalOverhangAndConstructionLift()
        {
            var data = MixedChapter();
            Rect bounds = BuildingGeometry.Bounds(data);
            for (int i = 0; i < data.Floors.Count; i++)
            {
                Rect r = BuildingGeometry.Footprint(data.Floors[i]);
                float top = BuildingGeometry.FoundationHeight + (i + 1) * BuildingGeometry.FloorHeight +
                    BuildingGeometry.RoofHeight + BuildingGeometry.ConstructionLift;
                Vector2 point = BuildingGeometry.Project(r.xMax, r.yMin, top);
                Assert.That(point.x, Is.InRange(bounds.xMin, bounds.xMax));
                Assert.That(point.y, Is.InRange(bounds.yMin, bounds.yMax));
            }
        }

        [Test]
        public void InvalidFirstFloorIsRejectedInsteadOfInventingFoundation()
        {
            Assert.Throws<ArgumentException>(() => AdventureBuildingData.FromPack("bad",
                new PuzzlePackData { puzzles = new[] { new PuzzleEntry { id = "bad" } } }));
            Assert.Throws<ArgumentException>(() => AdventureBuildingData.FromPack("bad",
                new PuzzlePackData { width = 5, height = 5, puzzles = new[]
                { new PuzzleEntry { id = "duplicate" }, new PuzzleEntry { id = "duplicate" } } }));
        }

        [Test]
        public void ExactPuzzleCompletionDoesNotConstructWrongFloorWhenSaveHasGaps()
        {
            var view = new BuildingView();
            view.SetBuilding(MixedChapter());
            view.SetCompletedFloors(new[] { false, true, false });
            Assert.That(view.AnimateFloor(1), Is.False, "Replaying a completed floor is idempotent.");
            Assert.That(view.AnimateFloor(0), Is.True);
            view.FinishConstruction();
            Assert.That(view.CompletedFloors, Is.EqualTo(2));
            Assert.That(view.IsComplete, Is.False);
            Assert.That(view.AnimateFloor(2), Is.True);
            view.FinishConstruction();
            Assert.That(view.IsComplete, Is.True);
        }

        [Test]
        public void FastDismissalFinishesPresentationAndRebindingResetsAnimation()
        {
            var view = new BuildingView();
            view.SetBuilding(MixedChapter());
            Assert.That(view.CompletedFloors, Is.Zero);
            view.AnimateFloor(0);
            Assert.That(view.CompletedFloors, Is.Zero, "Pre-solve state must remain visible before animation.");
            view.FinishConstruction();
            Assert.That(view.CompletedFloors, Is.EqualTo(1));
            Assert.That(view.IsAnimating, Is.False);
            view.AnimateFloor(1);
            view.SetBuilding(MixedChapter());
            Assert.That(view.CompletedFloors, Is.Zero);
            Assert.That(view.IsAnimating, Is.False);
            view.SetProgress(100);
            Assert.That(view.IsComplete, Is.True);
            view.SetProgress(-10);
            Assert.That(view.CompletedFloors, Is.Zero);
        }

        [Test]
        public void ShippedChapterFloorsMatchCatalogAndGameplayLoader()
        {
            foreach (var chapter in PuzzleCatalog.StoryPacks)
            {
                var data = AdventureBuildingData.Load(chapter.PackPath);
                var ids = PuzzleCatalog.GetPackPuzzleIds(chapter.PackPath);
                Assert.That(data, Is.Not.Null);
                Assert.That(data.Floors.Count, Is.EqualTo(ids.Count));
                for (int i = 0; i < ids.Count; i++)
                {
                    var puzzle = PuzzleLoader.LoadFromResourcesPackKey($"{chapter.PackPath}#{i}");
                    Assert.That(data.Floors[i].PuzzleId, Is.EqualTo(ids[i]));
                    Assert.That(data.Floors[i].Width, Is.EqualTo(puzzle.width));
                    Assert.That(data.Floors[i].Depth, Is.EqualTo(puzzle.height));
                }
            }
        }

        [Test]
        public void ChapterFiveMaskControlsFoundationAndEveryFloor()
        {
            var data = AdventureBuildingData.Load("Story/Adventure_Chapter05");
            foreach (var floor in data.Floors)
            {
                Assert.That(floor.OccupiedCellCount, Is.EqualTo(43));
                Assert.That(floor.Contains(0, 2), Is.False);
                Assert.That(floor.Contains(6, 4), Is.False);
                Assert.That(floor.Contains(0, 0), Is.True);
            }
            int foundationPatches = 0, front = 0, side = 0;
            foreach (var patch in data.Patches)
            {
                if (patch.FloorIndex == -1) foundationPatches++;
                if (patch.FloorIndex != 0) continue;
                if (patch.Front) front++;
                if (patch.Side) side++;
                Assert.That(data.Floors[0].ContainsWorld(patch.Area.center.x, patch.Area.center.y), Is.True);
            }
            Assert.That(foundationPatches, Is.EqualTo(43 * 4));
            Assert.That(front, Is.EqualTo(18), "Recessed front boundaries must produce walls.");
            Assert.That(side, Is.EqualTo(14));
        }

        [Test]
        public void MasksInheritOverrideAndPreserveGridOrigin()
        {
            var data = AdventureBuildingData.FromPack("mask", new PuzzlePackData
            {
                width = 3, height = 3, mask = "111/101/111",
                puzzles = new[] { new PuzzleEntry { id = "a" },
                    new PuzzleEntry { id = "b", mask = "001/001/001" } }
            });
            Assert.That(data.Floors[0].OccupiedCellCount, Is.EqualTo(8));
            Assert.That(data.Floors[1].OccupiedCellCount, Is.EqualTo(3));
            Assert.That(data.Floors[1].ContainsWorld(1, 0), Is.True);
            Assert.That(data.Floors[1].ContainsWorld(0, 0), Is.False, "Do not recenter an asymmetric mask.");
            Assert.Throws<ArgumentException>(() => new AdventureBuildingData.Floor("bad", 3, 3, "111"));
            Assert.Throws<ArgumentException>(() => new AdventureBuildingData.Floor("empty", 2, 2, "00/00"));
        }

        [Test]
        public void AppearanceIsStableVariedAndDoesNotMutateCustomAssets()
        {
            var colors = new System.Collections.Generic.HashSet<Color>();
            var architectures = new System.Collections.Generic.HashSet<BuildingArchitecture>();
            foreach (var pack in PuzzleCatalog.StoryPacks)
            {
                var data = AdventureBuildingData.Load(pack.PackPath);
                var again = BuildingAppearance.Resolve(data.StableId, data.Definition);
                Assert.That(again.wall, Is.EqualTo(data.Appearance.wall));
                colors.Add(BuildingAppearance.Resolve(data.StableId, null).wall);
                architectures.Add(data.Appearance.architecture);
            }
            Assert.That(colors.Count, Is.GreaterThanOrEqualTo(Mathf.Min(5, PuzzleCatalog.StoryPacks.Count)));
            Assert.That(architectures.Count, Is.GreaterThanOrEqualTo(2));
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            var style = ScriptableObject.CreateInstance<BuildingStyle>();
            try
            {
                style.palette.architecture = BuildingArchitecture.ArtDeco;
                definition.style = style;
                definition.overrideColors = true;
                definition.wallColor = Color.magenta;
                definition.roofColor = Color.cyan;
                Color original = style.palette.wall;
                var resolved = BuildingAppearance.Resolve("same", definition);
                Assert.That(resolved.wall, Is.EqualTo(Color.magenta));
                Assert.That(resolved.architecture, Is.EqualTo(BuildingArchitecture.ArtDeco));
                Assert.That(style.palette.wall, Is.EqualTo(original));
                definition.overrideArchitecture = true;
                definition.architecture = BuildingArchitecture.Warehouse;
                Assert.That(BuildingAppearance.Resolve("same", definition).architecture, Is.EqualTo(BuildingArchitecture.Warehouse));
                Assert.That(BuildingAppearance.StableHash("same", 0), Is.Not.EqualTo(BuildingAppearance.StableHash("same", 1)));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(style); }
        }
    }
}
#endif
