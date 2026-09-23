#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Shikaku.Logic;
using Shikaku.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Shikaku.Tests
{
    public class FloorplanFurnitureTests
    {
        private static ShikakuRegion Commit(PuzzleModel model, int x, int y, int w, int h)
        {
            model.TryCommitRectangle(x, y, w, h);
            model.TryGetRegion(model.GetRegionIdAt(y * model.Width + x), out var room);
            return room;
        }

        [Test]
        public void EveryCluePosition_KeepsAllFootprintsInsideWallsAndOutsideClueCell()
        {
            var items = new List<FloorplanFurniturePlacement>();
            int furnished = 0;
            for (int w = 1; w <= 8; w++) for (int h = 1; h <= 8; h++)
                for (int clueIndex = 0; clueIndex < w * h; clueIndex++)
                {
                    var givens = new int[w * h]; givens[clueIndex] = w * h;
                    var model = new PuzzleModel(w, h, givens);
                    var room = Commit(model, 0, 0, w, h);
                    var clue = FloorplanFurnitureLayout.ClueBounds(model, room);
                    foreach (float pixels in new[] { 16f, 28f, 64f, 160f })
                    {
                        FloorplanFurnitureLayout.Build(model, room, pixels, items);
                        furnished += items.Count;
                        for (int i = 0; i < items.Count; i++)
                        {
                            Rect b = items[i].Bounds;
                            Assert.That(b.xMin, Is.GreaterThanOrEqualTo(.159f));
                            Assert.That(b.yMin, Is.GreaterThanOrEqualTo(.159f));
                            Assert.That(b.xMax, Is.LessThanOrEqualTo(w - .159f));
                            Assert.That(b.yMax, Is.LessThanOrEqualTo(h - .159f));
                            Assert.That(b.Overlaps(clue), Is.False, $"Clue {clueIndex} in {w}x{h}");
                            for (int j = 0; j < i; j++)
                                Assert.That(b.Overlaps(items[j].Bounds), Is.False);
                        }
                    }
                }
            Assert.That(furnished, Is.GreaterThan(1000), "The safety checks must exercise actual furnished rooms.");
        }

        [Test]
        public void RecommitSameGeometry_DoesNotRerollFurniture()
        {
            var model = new PuzzleModel(3, 3, new[] { 0,0,0,0,9,0,0,0,0 });
            var first = new List<FloorplanFurniturePlacement>();
            var second = new List<FloorplanFurniturePlacement>();
            var room = Commit(model, 0, 0, 3, 3);
            int oldId = room.Id;
            FloorplanFurnitureLayout.Build(model, room, 80, first);
            model.TryRemoveRegionAt(0);
            room = Commit(model, 0, 0, 3, 3);
            Assert.That(room.Id, Is.Not.EqualTo(oldId));
            FloorplanFurnitureLayout.Build(model, room, 80, second);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SmallerCells_ShrinkMainPropAndRemoveAccessories()
        {
            var model = new PuzzleModel(4, 4, new[] {16,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0});
            var room = Commit(model,0,0,4,4);
            var items = new List<FloorplanFurniturePlacement>();
            FloorplanFurnitureLayout.Build(model,room,100,items);
            Assert.That(items.Count, Is.GreaterThan(1));
            Rect main = items[0].Bounds;
            FloorplanFurnitureLayout.Build(model,room,24,items);
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Bounds, Is.EqualTo(main));
            Assert.That(items[0].Bounds.width * 24, Is.LessThan(main.width * 100));
            FloorplanFurnitureLayout.Build(model,room,8,items);
            Assert.That(items, Is.Empty);
        }

        [Test]
        public void InvalidAndSingleCellRooms_RemainUnfurnished()
        {
            var model = new PuzzleModel(3, 2, new[] {6,0,0,0,0,0});
            var items = new List<FloorplanFurniturePlacement>();
            FloorplanFurnitureLayout.Build(model,Commit(model,0,0,2,2),80,items);
            Assert.That(items, Is.Empty);
            model = new PuzzleModel(1,1,new[]{1});
            FloorplanFurnitureLayout.Build(model,Commit(model,0,0,1,1),80,items);
            Assert.That(items, Is.Empty);
        }

        [Test]
        public void Catalog_AllFurnitureFamiliesAppearWithoutReadingSolutionData()
        {
            var observed = new HashSet<FloorplanFurnitureKind>();
            var items = new List<FloorplanFurniturePlacement>();
            for (int w = 2; w <= 8; w++) for (int h = 2; h <= 8; h++)
                for (int clue = 0; clue < w * h; clue++)
                {
                    var given = new int[w * h]; given[clue] = w * h;
                    var model = new PuzzleModel(w,h,given);
                    FloorplanFurnitureLayout.Build(model,Commit(model,0,0,w,h),80,items);
                    foreach(var item in items) observed.Add(item.Kind);
                }
            foreach(var kind in new[] { FloorplanFurnitureKind.Bed, FloorplanFurnitureKind.Sofa,
                FloorplanFurnitureKind.Desk, FloorplanFurnitureKind.DiningSet, FloorplanFurnitureKind.KitchenCounter,
                FloorplanFurnitureKind.Bathtub, FloorplanFurnitureKind.Basin, FloorplanFurnitureKind.Toilet,
                FloorplanFurnitureKind.Bookcase, FloorplanFurnitureKind.Washer, FloorplanFurnitureKind.Armchair,
                FloorplanFurnitureKind.Crib, FloorplanFurnitureKind.ChangingTable, FloorplanFurnitureKind.RockingChair,
                FloorplanFurnitureKind.GrandfatherClock, FloorplanFurnitureKind.TvConsole,
                FloorplanFurnitureKind.Wardrobe, FloorplanFurnitureKind.ToyChest, FloorplanFurnitureKind.BreakfastBar,
                FloorplanFurnitureKind.DoubleWorkstation, FloorplanFurnitureKind.FilingCabinet, FloorplanFurnitureKind.PrinterStand,
                FloorplanFurnitureKind.UprightPiano, FloorplanFurnitureKind.RecordCabinet,
                FloorplanFurnitureKind.BoxShelves, FloorplanFurnitureKind.CoatRack, FloorplanFurnitureKind.LaundryHamper, FloorplanFurnitureKind.TallBookshelf })
                Assert.That(observed.Contains(kind), Is.True, $"Missing template coverage: {kind}");
        }

        [TestCase(1,5)]
        [TestCase(5,1)]
        public void NarrowRooms_OrientFurnitureAlongTheLongWall(int w, int h)
        {
            var given = new int[w*h];given[0]=w*h;
            var model = new PuzzleModel(w,h,given);
            var items = new List<FloorplanFurniturePlacement>();
            FloorplanFurnitureLayout.Build(model,Commit(model,0,0,w,h),80,items);
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Bounds.height > items[0].Bounds.width, Is.EqualTo(h > w));
            CollectionAssert.Contains(new[] { FloorplanFurnitureKind.Cabinet,
                FloorplanFurnitureKind.Bookcase, FloorplanFurnitureKind.Bench, FloorplanFurnitureKind.GrandfatherClock,
                FloorplanFurnitureKind.TvConsole, FloorplanFurnitureKind.BreakfastBar, FloorplanFurnitureKind.Wardrobe,
                FloorplanFurnitureKind.BoxShelves, FloorplanFurnitureKind.CoatRack, FloorplanFurnitureKind.LaundryHamper, FloorplanFurnitureKind.FilledBookshelf }, items[0].Kind);
        }

        [Test]
        public void Nursery_RequiresLargeRoomAndSpaceForSupportingFurniture()
        {
            var items = new List<FloorplanFurniturePlacement>();
            int nurseries = 0;
            for(int w=1;w<=8;w++) for(int h=1;h<=8;h++) for(int c=0;c<w*h;c++)
            {
                var given=new int[w*h];given[c]=w*h;
                var model=new PuzzleModel(w,h,given);
                var room=Commit(model,0,0,w,h);
                FloorplanFurnitureLayout.Build(model,room,80,items);
                if(items.Count==0 || items[0].Kind!=FloorplanFurnitureKind.Crib) continue;
                nurseries++;
                Assert.That(w*h, Is.GreaterThanOrEqualTo(12));
                Assert.That(Mathf.Min(w,h), Is.GreaterThanOrEqualTo(3));
                Assert.That(items.Count, Is.InRange(2,3));
                for(int i=1;i<items.Count;i++)
                    CollectionAssert.Contains(new[]{FloorplanFurnitureKind.ChangingTable,
                        FloorplanFurnitureKind.RockingChair,FloorplanFurnitureKind.ToyChest},items[i].Kind);
                var crib=items[0];
                FloorplanFurnitureLayout.Build(model,room,24,items);
                Assert.That(items.Count, Is.EqualTo(1));
                Assert.That(items[0], Is.EqualTo(crib), "LOD must not reroll the room family or resize the crib.");
            }
            Assert.That(nurseries, Is.GreaterThan(10));
        }

        [Test]
        public void NarrowCatalog_IncludesClockTvAndBreakfastBar()
        {
            var seen=new HashSet<FloorplanFurnitureKind>();
            var items=new List<FloorplanFurniturePlacement>();
            for(int length=2;length<=12;length++) for(int c=0;c<length;c++)
            {
                var given=new int[length];given[c]=length;
                var model=new PuzzleModel(1,length,given);
                FloorplanFurnitureLayout.Build(model,Commit(model,0,0,1,length),80,items);
                if(items.Count>0) seen.Add(items[0].Kind);
            }
            foreach(var kind in new[]{FloorplanFurnitureKind.GrandfatherClock,FloorplanFurnitureKind.TvConsole,
                FloorplanFurnitureKind.BreakfastBar,FloorplanFurnitureKind.FilledBookshelf}) Assert.That(seen.Contains(kind),Is.True,kind.ToString());
        }
        [Test]
        public void DoubleOfficeAndMusicRooms_RespectTheirSizeRequirements()
        {
            var items=new List<FloorplanFurniturePlacement>();
            int offices=0,musicRooms=0;
            for(int w=1;w<=8;w++) for(int h=1;h<=8;h++) for(int c=0;c<w*h;c++)
            {
                var given=new int[w*h];given[c]=w*h;
                var model=new PuzzleModel(w,h,given);
                FloorplanFurnitureLayout.Build(model,Commit(model,0,0,w,h),80,items);
                if(items.Count==0) continue;
                if(items[0].Kind==FloorplanFurnitureKind.DoubleWorkstation)
                {
                    offices++;
                    Assert.That(w*h,Is.GreaterThanOrEqualTo(10));
                    Assert.That(Mathf.Min(w,h),Is.GreaterThanOrEqualTo(2));
                }
                if(items[0].Kind==FloorplanFurnitureKind.UprightPiano)
                {
                    musicRooms++;
                    Assert.That(w*h,Is.GreaterThanOrEqualTo(6));
                    Assert.That(Mathf.Min(w,h),Is.GreaterThanOrEqualTo(2));
                }
            }
            Assert.That(offices,Is.GreaterThan(0));
            Assert.That(musicRooms,Is.GreaterThan(0));
        }
        [Test]
        public void Libraries_KeepUprightShelvesAdjacentAlignedAndStableAtLowDetail()
        {
            var items=new List<FloorplanFurniturePlacement>();
            int libraries=0;
            for(int w=2;w<=8;w++) for(int h=2;h<=8;h++) for(int c=0;c<w*h;c++)
            {
                var given=new int[w*h];given[c]=w*h;
                var model=new PuzzleModel(w,h,given);
                var room=Commit(model,0,0,w,h);
                FloorplanFurnitureLayout.Build(model,room,80,items);
                if(items.Count==0 || items[0].Kind!=FloorplanFurnitureKind.TallBookshelf) continue;
                libraries++;
                Assert.That(w*h,Is.GreaterThanOrEqualTo(12));
                Assert.That(Mathf.Min(w,h),Is.GreaterThanOrEqualTo(3));
                Assert.That(items.Count,Is.InRange(3,5));
                var bank=items.ToArray();
                for(int i=0;i<items.Count;i++)
                {
                    Assert.That(items[i].Kind,Is.EqualTo(FloorplanFurnitureKind.TallBookshelf));
                    Assert.That(items[i].QuarterTurn,Is.False,"Bookcases must stand upright.");
                    Assert.That(items[i].Bounds.height,Is.GreaterThan(items[i].Bounds.width));
                    Assert.That(items[i].Bounds.y,Is.EqualTo(items[0].Bounds.y).Within(.0001f));
                    Assert.That(items[i].Bounds.size,Is.EqualTo(items[0].Bounds.size));
                    if(i>0) Assert.That(items[i].Bounds.xMin-items[i-1].Bounds.xMax,
                        Is.InRange(.031f,.045f),"Cases must sit directly next to one another.");
                }
                FloorplanFurnitureLayout.Build(model,room,24,items);
                CollectionAssert.AreEqual(bank,items,"Dense boards must preserve the whole shelf bank.");
            }
            Assert.That(libraries,Is.GreaterThan(10));
        }
        [Test]
        public void EveryFurnitureMesh_StaysInItsFootprintInBothOrientations()
        {
            var go = new GameObject("Furniture footprint",typeof(RectTransform),typeof(CanvasRenderer),typeof(FloorplanFurnitureGraphic));
            try
            {
                var graphic = go.GetComponent<FloorplanFurnitureGraphic>();
                var type = typeof(FloorplanFurnitureGraphic);
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                using(var mesh = new VertexHelper())
                {
                    type.GetField("_mesh",flags).SetValue(graphic,mesh);
                    type.GetField("_prop",flags).SetValue(graphic,new Rect(0,0,120,80));
                    foreach(FloorplanFurnitureKind kind in System.Enum.GetValues(typeof(FloorplanFurnitureKind)))
                        foreach(bool rotated in new[]{false,true}) foreach(bool detail in new[]{false,true})
                        {
                            mesh.Clear();
                            type.GetField("_quarterTurn",flags).SetValue(graphic,rotated);
                            type.GetField("_detail",flags).SetValue(graphic,detail);
                            type.GetMethod("Draw",flags).Invoke(graphic,new object[]{kind});
                            Assert.That(mesh.currentVertCount, Is.InRange(1,256), $"Mesh budget: {kind}");
                            for(int i=0;i<mesh.currentVertCount;i++)
                            {
                                UIVertex vertex=default;mesh.PopulateUIVertex(ref vertex,i);
                                Assert.That(vertex.position.x, Is.InRange(-.001f,120.001f), $"{kind} x");
                                Assert.That(vertex.position.y, Is.InRange(-80.001f,.001f), $"{kind} y");
                            }
                        }
                    type.GetField("_mesh",flags).SetValue(graphic,null);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void GraphicToggle_DoesNotChangePuzzleAndNeverInterceptsTouches()
        {
            var model = new PuzzleModel(3,3,new[]{9,0,0,0,0,0,0,0,0});
            var room = Commit(model,0,0,3,3);
            var go = new GameObject("Furniture test",typeof(RectTransform),typeof(CanvasRenderer),typeof(FloorplanFurnitureGraphic));
            try
            {
                var graphic = go.GetComponent<FloorplanFurnitureGraphic>();
                graphic.SetRoom(model,room,false,true);
                Assert.That(graphic.enabled, Is.True);
                Assert.That(graphic.raycastTarget, Is.False);
                graphic.SetRoom(model,room,true,false);
                Assert.That(graphic.enabled, Is.False);
                graphic.SetRoom(model,room,true,true);
                Assert.That(graphic.enabled, Is.True);
                Assert.That(model.AssignedCellCount, Is.EqualTo(9));
                Assert.That(model.RegionCount, Is.EqualTo(1));
                Assert.That(model.SelectedRegionId, Is.EqualTo(room.Id));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif