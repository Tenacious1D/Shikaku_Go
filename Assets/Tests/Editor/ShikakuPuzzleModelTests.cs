#if UNITY_EDITOR
using NUnit.Framework;
using Shikaku.Logic;
using Shikaku.UI;
using UnityEngine;
using UnityEngine.UI;

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
        public void EvaluateRegionCandidate_ReportsDimensionsAndValidRule()
        {
            var model = new PuzzleModel(
                3,
                2,
                new[] { 0, 0, 0, 0, 0, 6 });

            ShikakuRegionEvaluation result =
                model.EvaluateRegionCandidate(0, 5);

            Assert.That(result.GeometryAllowed, Is.True);
            Assert.That(result.Width, Is.EqualTo(3));
            Assert.That(result.Height, Is.EqualTo(2));
            Assert.That(result.Area, Is.EqualTo(6));
            Assert.That(result.ClueCount, Is.EqualTo(1));
            Assert.That(result.ClueValue, Is.EqualTo(6));
            Assert.That(result.IsRuleValid, Is.True);
        }

        [Test]
        public void EvaluateRegionCandidate_RejectsAreaMismatchAndMultipleClues()
        {
            var areaMismatch = new PuzzleModel(
                2,
                2,
                new[] { 4, 0, 0, 0 });
            var multipleClues = new PuzzleModel(
                3,
                2,
                new[] { 3, 0, 3, 0, 0, 0 });

            ShikakuRegionEvaluation tooSmall =
                areaMismatch.EvaluateRegionCandidate(0, 0);
            ShikakuRegionEvaluation ambiguous =
                multipleClues.EvaluateRegionCandidate(0, 5);

            Assert.That(tooSmall.GeometryAllowed, Is.True);
            Assert.That(tooSmall.ClueCount, Is.EqualTo(1));
            Assert.That(tooSmall.IsRuleValid, Is.False);
            Assert.That(ambiguous.ClueCount, Is.EqualTo(2));
            Assert.That(ambiguous.IsRuleValid, Is.False);
        }
        [Test]
        public void BlueprintRoomDecoration_UsesRootCoordinatesAndClipsOverflow()
        {
            var boardObject = new GameObject(
                "Board",
                typeof(RectTransform));
            var roomObject = new GameObject(
                "Room",
                typeof(RectTransform));

            try
            {
                RectTransform board =
                    boardObject.GetComponent<RectTransform>();
                board.sizeDelta = new Vector2(400f, 300f);
                board.pivot = new Vector2(0.35f, 0.65f);

                RectTransform room = roomObject.GetComponent<RectTransform>();
                room.SetParent(board, false);

                var firstBounds = new Bounds(
                    new Vector3(-150f, -100f, 0f),
                    new Vector3(100f, 100f, 0f));
                var lastBounds = new Bounds(
                    new Vector3(100f, 50f, 0f),
                    new Vector3(100f, 100f, 0f));

                System.Type layerType =
                    typeof(BlueprintThemeAssets).Assembly.GetType(
                        "Shikaku.UI.BlueprintRoomDecorationLayer");
                Assert.That(layerType, Is.Not.Null);

                System.Reflection.MethodInfo applyLocalBounds =
                    layerType.GetMethod(
                        "ApplyLocalBounds",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.NonPublic);
                Assert.That(applyLocalBounds, Is.Not.Null);
                applyLocalBounds.Invoke(
                    null,
                    new object[]
                    {
                        room,
                        board,
                        firstBounds,
                        lastBounds
                    });

                Assert.That(room.anchorMin.x,
                    Is.EqualTo(board.pivot.x).Within(0.001f));
                Assert.That(room.anchorMin.y,
                    Is.EqualTo(board.pivot.y).Within(0.001f));
                Assert.That(room.anchorMax.x,
                    Is.EqualTo(board.pivot.x).Within(0.001f));
                Assert.That(room.anchorMax.y,
                    Is.EqualTo(board.pivot.y).Within(0.001f));
                Assert.That(room.anchoredPosition.x,
                    Is.EqualTo(-25f).Within(0.001f));
                Assert.That(room.anchoredPosition.y,
                    Is.EqualTo(-25f).Within(0.001f));
                Assert.That(room.sizeDelta.x,
                    Is.EqualTo(350f).Within(0.001f));
                Assert.That(room.sizeDelta.y,
                    Is.EqualTo(250f).Within(0.001f));

                System.Reflection.MethodInfo createLayer =
                    layerType.GetMethod(
                        "Create",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public);
                Assert.That(createLayer, Is.Not.Null);
                createLayer.Invoke(null, new object[] { board });

                Transform layer = board.Find("BlueprintRooms");
                Assert.That(layer, Is.Not.Null);
                Assert.That(layer.GetComponent<RectMask2D>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
                if (roomObject != null)
                    Object.DestroyImmediate(roomObject);
            }
        }
        [Test]
        public void BlueprintTheme_LoadsConfiguredArtSet()
        {
            BlueprintThemeAssets theme =
                Resources.Load<BlueprintThemeAssets>("UI/BlueprintThemeAssets");

            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.whiteprintPaper, Is.Not.Null);
            Assert.That(theme.blueprintPaper, Is.Not.Null);
            Assert.That(theme.roomHatches, Has.Length.EqualTo(6));
            Assert.That(theme.roomHatches, Has.All.Not.Null);
            Assert.That(theme.inspectionStamp, Is.Not.Null);
            Assert.That(theme.approvedStamp, Is.Not.Null);
            Assert.That(theme.rivetWelcome, Is.Not.Null);
            Assert.That(theme.rivetTeach, Is.Not.Null);
            Assert.That(theme.rivetInspect, Is.Not.Null);
            Assert.That(theme.rivetHint, Is.Not.Null);
            Assert.That(theme.rivetCelebrate, Is.Not.Null);
            Assert.That(theme.rivetConcerned, Is.Not.Null);
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
