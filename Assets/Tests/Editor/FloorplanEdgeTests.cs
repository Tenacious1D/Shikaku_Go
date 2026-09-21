#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shikaku.Logic;
using Shikaku.UI;

namespace Shikaku.Tests
{
    public class FloorplanEdgeTests
    {
        private static List<FloorplanEdge> Edges(PuzzleModel model)
        {
            var result = new List<FloorplanEdge>();
            FloorplanEdges.Build(model, result);
            Assert.That(result.Select(e => (e.X, e.Y, e.Horizontal)).Distinct().Count(),
                Is.EqualTo(result.Count), "No physical edge may be emitted twice.");
            return result;
        }

        [TestCase(1, 1)]
        [TestCase(1, 9)]
        [TestCase(13, 12)]
        public void EmptyBoard_OnlyDrawsItsPerimeter(int width, int height)
        {
            var model = new PuzzleModel(width, height, new int[width * height]);
            var edges = Edges(model);
            Assert.That(edges.Count, Is.EqualTo(2 * (width + height)));
            Assert.That(edges.All(e => e.Perimeter), Is.True);
        }

        [Test]
        public void EqualAreaNeighbors_HaveOneSharedPartition()
        {
            var model = new PuzzleModel(4, 2, new[] {4,0,4,0,0,0,0,0});
            model.TryCommitRectangle(0,0,2,2);
            model.TryCommitRectangle(2,0,2,2);
            var interior = Edges(model).Where(e => !e.Perimeter).ToArray();
            Assert.That(interior.Length, Is.EqualTo(2));
            Assert.That(interior.All(e => !e.Horizontal && e.X == 2), Is.True);
            Assert.That(interior.All(e => e.FirstRegion != e.SecondRegion), Is.True);
        }

        [Test]
        public void MaskedCourtyard_HasItsOwnBoundaryWithoutFillingTheHole()
        {
            var model = new PuzzleModel(3,3,new int[9]);
            model.LoadPuzzle(3,3,new int[9],new[]{true,true,true,true,false,true,true,true,true});
            var edges = Edges(model);
            Assert.That(edges.Count, Is.EqualTo(16));
            Assert.That(edges.All(e => e.Perimeter), Is.True);
            Assert.That(edges.Count(e => e.Horizontal && e.X == 1 && (e.Y == 1 || e.Y == 2)), Is.EqualTo(2));
            Assert.That(edges.Count(e => !e.Horizontal && e.Y == 1 && (e.X == 1 || e.X == 2)), Is.EqualTo(2));
        }

        [Test]
        public void TJunction_PartitionsStopAtTheirOwningRooms()
        {
            var model = new PuzzleModel(3,2,new[]{2,2,0,0,2,0});
            model.TryCommitRectangle(0,0,1,2);
            model.TryCommitRectangle(1,0,2,1);
            model.TryCommitRectangle(1,1,2,1);
            var inside = Edges(model).Where(e => !e.Perimeter).ToArray();
            Assert.That(inside.Length, Is.EqualTo(4));
            Assert.That(inside.Any(e => e.Horizontal && e.X == 0), Is.False);
        }

        [Test]
        public void RemoveAndRestart_DoNotLeaveGhostPartitions()
        {
            var model = new PuzzleModel(4,2,new[]{4,0,4,0,0,0,0,0});
            model.TryCommitRectangle(0,0,2,2);
            model.TryCommitRectangle(2,0,2,2);
            model.TryRemoveRegionAt(0);
            Assert.That(Edges(model).Count(e => !e.Perimeter), Is.EqualTo(2));
            model.ClearRegions();
            Assert.That(Edges(model).All(e => e.Perimeter), Is.True);
        }

        [Test]
        public void GeometryIncludesInvalidRegionAndDoesNotMutateSelection()
        {
            var model = new PuzzleModel(3,2,new[]{6,0,0,0,0,0});
            model.TryCommitRectangle(0,0,1,2);
            int selected = model.SelectedRegionId;
            Assert.That(model.IsRegionValidAt(0), Is.False);
            Assert.That(Edges(model).Count(e => !e.Perimeter), Is.EqualTo(2));
            Assert.That(model.SelectedRegionId, Is.EqualTo(selected));
            Assert.That(model.AssignedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void EmptyMask_ProducesNoPhantomRectangle()
        {
            var model = new PuzzleModel(2,2,new int[4]);
            model.LoadPuzzle(2,2,new int[4],new bool[4]);
            Assert.That(Edges(model), Is.Empty);
        }
    }
}
#endif
