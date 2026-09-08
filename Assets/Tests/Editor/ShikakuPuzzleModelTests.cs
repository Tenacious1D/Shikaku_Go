#if UNITY_EDITOR
using NUnit.Framework;
using Shikaku.Logic;

namespace Shikaku.Tests
{
    public class ShikakuPuzzleModelTests
    {
        [Test]
        public void CanCommitRegion_DoesNotMutateBoard()
        {
            var model = new PuzzleModel(3, 2, new[] { 0, 0, 0, 0, 0, 6 });

            Assert.That(model.CanCommitRegion(0, 5), Is.True);
            Assert.That(model.AssignedCellCount, Is.Zero);
            Assert.That(model.RegionCount, Is.Zero);
        }

        [Test]
        public void CommitRegion_OverridesWholeTouchedRegionsOnlyAtCommit()
        {
            var model = new PuzzleModel(4, 2, new[] { 4, 0, 4, 0, 0, 0, 0, 0 });
            Assert.That(model.TryCommitRectangle(0, 0, 2, 2), Is.True);
            Assert.That(model.TryCommitRectangle(2, 0, 2, 2), Is.True);
            int leftId = model.GetRegionIdAt(0);
            int rightId = model.GetRegionIdAt(2);

            Assert.That(model.CanCommitRectangle(1, 0, 2, 2), Is.True);
            Assert.That(model.GetRegionIdAt(0), Is.EqualTo(leftId));
            Assert.That(model.GetRegionIdAt(2), Is.EqualTo(rightId));

            Assert.That(model.TryCommitRectangle(1, 0, 2, 2), Is.True);
            Assert.That(model.GetRegionIdAt(0), Is.EqualTo(-1));
            Assert.That(model.GetRegionIdAt(3), Is.EqualTo(-1));
            Assert.That(model.GetRegionIdAt(1), Is.EqualTo(model.GetRegionIdAt(2)));
            Assert.That(model.RegionCount, Is.EqualTo(1));
        }

        [Test]
        public void MaskedDraft_IsRejectedWithoutChangingExistingRegion()
        {
            var mask = new[] { true, true, true, false };
            var model = new PuzzleModel(2, 2, new[] { 2, 0, 0, 0 });
            model.LoadPuzzle(2, 2, new[] { 2, 0, 0, 0 }, mask);
            Assert.That(model.TryCommitRectangle(0, 0, 2, 1), Is.True);
            int committedId = model.GetRegionIdAt(0);

            Assert.That(model.TryCommitRectangle(0, 0, 2, 2), Is.False);
            Assert.That(model.GetRegionIdAt(0), Is.EqualTo(committedId));
            Assert.That(model.GetRegionIdAt(1), Is.EqualTo(committedId));
            Assert.That(model.GetRegionIdAt(3), Is.EqualTo(-2));
        }

        [Test]
        public void AdjacentEqualAreaRectangles_RemainDistinctAndSolve()
        {
            var model = new PuzzleModel(4, 2, new[] { 4, 0, 4, 0, 0, 0, 0, 0 });

            Assert.That(model.TryCommitRectangle(0, 0, 2, 2), Is.True);
            Assert.That(model.TryCommitRectangle(2, 0, 2, 2), Is.True);

            Assert.That(model.GetRegionIdAt(1), Is.Not.EqualTo(model.GetRegionIdAt(2)));
            Assert.That(model.IsRegionValidAt(0), Is.True);
            Assert.That(model.IsRegionValidAt(2), Is.True);
            Assert.That(model.IsSolved(), Is.True);
        }

        [Test]
        public void GeometricButWrongRectangle_CommitsButDoesNotSolve()
        {
            var model = new PuzzleModel(2, 2, new[] { 4, 0, 0, 0 });

            Assert.That(model.TryCommitRectangle(0, 0, 1, 1), Is.True);
            Assert.That(model.IsRegionValidAt(0), Is.False);
            Assert.That(model.IsSolved(), Is.False);
        }

        [Test]
        public void ClassicalFreeplayPack_LoadsCanonicalRectangles()
        {
            PuzzleData puzzle = PuzzleLoader.LoadFromResourcesPackPuzzleId(
                "FreePlay/Classical1/8x8",
                "shikaku_8x8_12345_0000");

            Assert.That(puzzle, Is.Not.Null);
            Assert.That(puzzle.solution, Is.Not.Null);
            Assert.That(puzzle.solution.regionIds, Has.Length.EqualTo(64));
            Assert.That(puzzle.solution.regions, Has.Length.EqualTo(puzzle.givens.Length));
            Assert.That(PuzzleLoader.ValidatePuzzleData(puzzle, out string error),
                Is.True,
                error);
        }
    }
}
#endif