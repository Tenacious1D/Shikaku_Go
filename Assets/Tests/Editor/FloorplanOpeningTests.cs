#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shikaku.Logic;
using Shikaku.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Shikaku.Tests
{
    public class FloorplanOpeningTests
    {
        private readonly FloorplanOpeningLayout _layout = new();
        private List<FloorplanOpening> Build(PuzzleModel model)
        {
            var edges=new List<FloorplanEdge>(); FloorplanEdges.Build(model,edges);
            var result=new List<FloorplanOpening>(); _layout.Build(model,edges,result); return result;
        }

        [Test]
        public void DoorSwings_ClearCluesAndFurnitureAcrossRoomShapesAndCluePositions()
        {
            int doors=0;
            var furniture=new List<FloorplanFurniturePlacement>();
            for(int w=2;w<=5;w++) for(int h=2;h<=5;h++)
            for(int clue=0;clue<w*h;clue++)
            {
                int width=w*2; var given=new int[width*h];
                given[(clue/w)*width+clue%w]=w*h;
                int other=w*h-1-clue; given[(other/w)*width+w+other%w]=w*h;
                var model=new PuzzleModel(width,h,given);
                model.TryCommitRectangle(0,0,w,h); model.TryCommitRectangle(w,0,w,h);
                var openings=Build(model);
                Assert.That(openings.Count(o=>o.Kind!=FloorplanOpeningKind.Window),Is.LessThanOrEqualTo(1));
                foreach(var opening in openings.Where(o=>o.Kind!=FloorplanOpeningKind.Window))
                {
                    doors++;
                    var edge=opening.Edge;
                    Assert.That(model.TryGetRegion(edge.FirstRegion,out var first)&&first.IsValid,Is.True);
                    Assert.That(model.TryGetRegion(edge.SecondRegion,out var second)&&second.IsValid,Is.True);
                    var room=opening.Inward>0?second:first;
                    Rect local=new Rect(opening.SwingBounds.position-new Vector2(room.X,room.Y),opening.SwingBounds.size);
                    Assert.That(local.xMin,Is.GreaterThanOrEqualTo(-.001f));
                    Assert.That(local.yMin,Is.GreaterThanOrEqualTo(-.001f));
                    Assert.That(local.xMax,Is.LessThanOrEqualTo(room.Width+.001f));
                    Assert.That(local.yMax,Is.LessThanOrEqualTo(room.Height+.001f));
                    Assert.That(local.Overlaps(FloorplanFurnitureLayout.ClueBounds(model,room)),Is.False);
                    FloorplanFurnitureLayout.Build(model,room,float.PositiveInfinity,furniture);
                    foreach(var item in furniture) Assert.That(local.Overlaps(item.Bounds),Is.False);
                }
            }
            Assert.That(doors,Is.GreaterThan(30));
        }

        [Test]
        public void Windows_OnlyUseExteriorWallsOfValidRoomsAndSkipClueCells()
        {
            var mask=Enumerable.Repeat(true,16).ToArray();mask[5]=false;
            var given=new int[16];given[0]=4;given[8]=4;given[3]=4;
            var model=new PuzzleModel(4,4,given);model.LoadPuzzle(4,4,given,mask);
            model.TryCommitRectangle(0,0,4,1); // Invalid: two clues.
            model.TryCommitRectangle(0,2,2,2);
            var openings=Build(model);
            Assert.That(openings.Any(o=>o.Kind==FloorplanOpeningKind.Window),Is.True);
            foreach(var opening in openings)
            {
                Assert.That(opening.Kind,Is.EqualTo(FloorplanOpeningKind.Window));
                var e=opening.Edge;
                Assert.That(e.Horizontal?e.Y==0||e.Y==4:e.X==0||e.X==4,Is.True);
                int id=e.FirstRegion>=0?e.FirstRegion:e.SecondRegion;
                Assert.That(model.TryGetRegion(id,out var room)&&room.IsValid,Is.True);
            }
            model.ClearRegions(); Assert.That(Build(model),Is.Empty);
        }

        [Test]
        public void RecommitOrder_DoesNotRerollOpeningsOrChangeSelection()
        {
            var given=new int[24];given[0]=12;given[3]=12;
            var model=new PuzzleModel(6,4,given);
            model.TryCommitRectangle(0,0,3,4);model.TryCommitRectangle(3,0,3,4);
            int selected=model.SelectedRegionId;
            var first=Build(model).Select(o=>(o.Edge.X,o.Edge.Y,o.Edge.Horizontal,o.Kind,o.Inward)).ToArray();
            Assert.That(model.SelectedRegionId,Is.EqualTo(selected));
            model.ClearRegions();model.TryCommitRectangle(3,0,3,4);model.TryCommitRectangle(0,0,3,4);
            var second=Build(model).Select(o=>(o.Edge.X,o.Edge.Y,o.Edge.Horizontal,o.Kind,o.Inward)).ToArray();
            CollectionAssert.AreEqual(first,second);
            model.TryRemoveRegionAt(0);
            Assert.That(Build(model).Any(o=>o.Kind!=FloorplanOpeningKind.Window),Is.False);
        }

        [Test]
        public void CrowdedRooms_UseSlidingDoorsWithoutChangingFurniture()
        {
            var given=new int[36];given[0]=9;given[3]=6;given[15]=3;
            given[18]=6;given[20]=6;given[22]=6;
            var model=new PuzzleModel(6,6,given);
            model.TryCommitRectangle(0,0,3,3);model.TryCommitRectangle(3,0,3,2);
            model.TryCommitRectangle(3,2,3,1);model.TryCommitRectangle(0,3,2,3);
            model.TryCommitRectangle(2,3,2,3);model.TryCommitRectangle(4,3,2,3);
            var openings=Build(model);
            Assert.That(openings.Count(o=>o.Kind==FloorplanOpeningKind.SlidingDoor),Is.GreaterThanOrEqualTo(3));
        }
        [TestCase(true, 1)] [TestCase(true, -1)]
        [TestCase(false, 1)] [TestCase(false, -1)]
        public void DoorMeshes_FitReservedSwingAndPreserveThreshold(bool horizontal,int inward)
        {
            var go=new GameObject("Opening mesh",typeof(RectTransform),typeof(CanvasRenderer),typeof(FloorplanWallGraphic));
            try
            {
                var wall=go.GetComponent<FloorplanWallGraphic>(); var type=typeof(FloorplanWallGraphic);
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                type.GetField("_x",flags).SetValue(wall,new float[]{0,100,200});
                type.GetField("_y",flags).SetValue(wall,new float[]{0,-100,-200});
                type.GetField("_thickness",flags).SetValue(wall,4f);
                type.GetField("_cellPixels",flags).SetValue(wall,100f);
                var opening=new FloorplanOpening(new FloorplanEdge(1,1,horizontal,0,1),FloorplanOpeningKind.Door,inward,default);
                using(var mesh=new VertexHelper())
                {
                    type.GetMethod("DrawOpening",flags).Invoke(wall,new object[]{mesh,opening,
                        horizontal?100f:98f,horizontal?-102f:-200f,horizontal?200f:102f,horizontal?-98f:-100f,Color.black});
                    Assert.That(mesh.currentVertCount,Is.EqualTo(48)); // Threshold, leaf, ten arc segments.
                    for(int i=0;i<mesh.currentVertCount;i++)
                    {
                        UIVertex vertex=default;mesh.PopulateUIVertex(ref vertex,i);
                        float along=horizontal?vertex.position.x-100:-vertex.position.y-100;
                        float depth=horizontal?(-vertex.position.y-100)*inward:(vertex.position.x-100)*inward;
                        Assert.That(along,Is.InRange(20f,80f));
                        Assert.That(depth,Is.InRange(-2f,58f));
                    }
                    type.GetField("_cellPixels",flags).SetValue(wall,30f);mesh.Clear();
                    type.GetMethod("DrawOpening",flags).Invoke(wall,new object[]{mesh,opening,
                        horizontal?100f:98f,horizontal?-102f:-200f,horizontal?200f:102f,horizontal?-98f:-100f,Color.black});
                    Assert.That(mesh.currentVertCount,Is.EqualTo(8),"Small cells retain the leaf and threshold without the arc.");
                }
                Assert.That(wall.raycastTarget,Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Reveal_IsBoundedMonotonicAndImmediateWithReducedMotion()
        {
            float previous=0;
            for(int i=0;i<=60;i++)
            {
                float t=i*.01f, value=FloorplanFurnitureGraphic.RevealProgress(t,false);
                Assert.That(value,Is.InRange(previous,1f));previous=value;
                Assert.That(FloorplanFurnitureGraphic.RevealProgress(t,true),Is.EqualTo(1f));
            }
            Assert.That(FloorplanFurnitureGraphic.RevealProgress(0,false),Is.Zero);
            Assert.That(FloorplanFurnitureGraphic.RevealProgress(.30f,false),Is.EqualTo(1f));
        }
    }
}
#endif