using System;
using System.Collections.Generic;
using Shikaku.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Shikaku.Menu
{
    public sealed class FreePlayScreenController : IDisposable
    {
        private const int ColumnsPerRow = 5;

        private enum FreePlayView
        {
            Packs,
            Sizes,
            Levels
        }

        private readonly Action _returnHome;
        private readonly VisualElement _screen;
        private readonly VisualElement _packView;
        private readonly VisualElement _sizeView;
        private readonly VisualElement _levelView;
        private readonly Button _backButton;
        private readonly Label _sizePackTitle;
        private readonly Label _levelTitleLabel;
        private readonly Label _levelPackLabel;
        private readonly Label _levelProgressLabel;
        private readonly Button _continueButton;
        private readonly Label _continueLabel;
        private readonly ScrollView _packScrollView;
        private readonly ScrollView _sizeScrollView;
        private readonly ScrollView _levelScrollView;
        private readonly VisualElement _packListRoot;
        private readonly VisualElement _sizeListRoot;
        private readonly VisualElement _levelGridRoot;

        private PuzzleCatalog.FreePlayCollectionInfo _selectedCollection;
        private string _selectedPackPath;
        private string _continuePuzzleId;
        private int _selectedSize;
        private int _continueLevelIndex;
        private VisualElement _nextLevelButton;
        private FreePlayView _currentView;
        private bool _isVisible;
        private bool _isLoading;

        public FreePlayScreenController(VisualElement documentRoot, Action returnHome)
        {
            _returnHome = returnHome;
            _screen = RequireElement<VisualElement>(documentRoot, "free-play-screen");
            _packView = RequireElement<VisualElement>(documentRoot, "free-play-pack-view");
            _sizeView = RequireElement<VisualElement>(documentRoot, "free-play-size-view");
            _levelView = RequireElement<VisualElement>(documentRoot, "free-play-level-view");
            _backButton = RequireElement<Button>(documentRoot, "free-play-back-button");
            _sizePackTitle = RequireElement<Label>(documentRoot, "free-play-size-pack-title");
            _levelTitleLabel = RequireElement<Label>(documentRoot, "free-play-level-title");
            _levelPackLabel = RequireElement<Label>(documentRoot, "free-play-level-pack-label");
            _levelProgressLabel = RequireElement<Label>(documentRoot, "free-play-level-progress");
            _continueButton = RequireElement<Button>(documentRoot, "free-play-continue-button");
            _continueLabel = RequireElement<Label>(documentRoot, "free-play-continue-label");
            _packScrollView = RequireElement<ScrollView>(documentRoot, "free-play-pack-scroll");
            _sizeScrollView = RequireElement<ScrollView>(documentRoot, "free-play-size-scroll");
            _levelScrollView = RequireElement<ScrollView>(documentRoot, "free-play-level-scroll");
            _packListRoot = RequireElement<VisualElement>(documentRoot, "free-play-pack-list");
            _sizeListRoot = RequireElement<VisualElement>(documentRoot, "free-play-size-list");
            _levelGridRoot = RequireElement<VisualElement>(documentRoot, "free-play-level-grid");

            HideScrollbars(_packScrollView);
            HideScrollbars(_sizeScrollView);
            HideScrollbars(_levelScrollView);

            _backButton.BringToFront();
            _backButton.clicked += GoBack;
            _continueButton.clicked += ContinueLevel;

            Hide();
        }

        public void Show(string requestedPackPath = null)
        {
            _isVisible = true;
            _isLoading = false;
            _screen.style.display = DisplayStyle.Flex;

            if (!string.IsNullOrEmpty(requestedPackPath) &&
                PuzzleCatalog.TryResolveFreePlayPackPath(
                    requestedPackPath,
                    out PuzzleCatalog.FreePlayCollectionInfo collection,
                    out PuzzleCatalog.PackInfo sizePack))
            {
                _selectedCollection = collection;
                ShowLevelSelection(sizePack.Size, sizePack.PackPath);
                return;
            }

            ShowPackSelection();
        }

        public void Hide()
        {
            _isVisible = false;
            _screen.style.display = DisplayStyle.None;
        }

        public bool HandleBack()
        {
            if (!_isVisible || _isLoading)
                return false;

            GoBack();
            return true;
        }

        public void Dispose()
        {
            _backButton.clicked -= GoBack;
            _continueButton.clicked -= ContinueLevel;
            _packListRoot.Clear();
            _sizeListRoot.Clear();
            _levelGridRoot.Clear();
        }

        private static T RequireElement<T>(VisualElement root, string name)
            where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException(
                    $"Free Play UI element '{name}' was not found."
                );

            return element;
        }

        private static void HideScrollbars(ScrollView scrollView)
        {
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        private void GoBack()
        {
            if (_currentView == FreePlayView.Levels)
            {
                ShowSizeSelection();
                return;
            }

            if (_currentView == FreePlayView.Sizes)
            {
                ShowPackSelection();
                return;
            }

            _returnHome?.Invoke();
        }

        private void ShowPackSelection()
        {
            _currentView = FreePlayView.Packs;
            _selectedCollection = null;
            _selectedPackPath = null;
            _selectedSize = 0;

            _sizeView.style.display = DisplayStyle.None;
            _levelView.style.display = DisplayStyle.None;
            _packView.style.display = DisplayStyle.Flex;

            BuildPackList();
            _packScrollView.schedule.Execute(
                () => _packScrollView.scrollOffset = Vector2.zero);
        }

        private void BuildPackList()
        {
            _packListRoot.Clear();
            IReadOnlyList<PuzzleCatalog.FreePlayCollectionInfo> collections =
                PuzzleCatalog.FreePlayCollections;

            for (int i = 0; i < collections.Count; i++)
                _packListRoot.Add(CreatePackButton(collections[i], i + 1));
        }

        private Button CreatePackButton(
            PuzzleCatalog.FreePlayCollectionInfo collection,
            int collectionNumber)
        {
            var button = new Button
            {
                name = $"free-play-pack-{collection.PackId}-button"
            };
            button.AddToClassList("free-play-pack-button");

            var tab = new VisualElement();
            tab.AddToClassList("free-play-pack-tab");
            var tabLabel = new Label($"SET A-{collectionNumber:00}");
            tabLabel.AddToClassList("free-play-pack-tab-label");
            tab.Add(tabLabel);
            button.Add(tab);

            VisualElement preview = CreatePlanPreview(collectionNumber);
            button.Add(preview);

            var copy = new VisualElement();
            copy.AddToClassList("free-play-pack-copy");

            var caption = new Label("PROJECT BINDER");
            caption.AddToClassList("free-play-pack-caption");
            copy.Add(caption);

            var name = new Label(collection.DisplayName);
            name.AddToClassList("free-play-pack-name");
            copy.Add(name);

            int completed = CountCompleted(collection);
            var progress = new Label(
                $"{completed:00} / {collection.TotalPuzzleCount:00} APPROVED");
            progress.AddToClassList("free-play-pack-progress");
            copy.Add(progress);
            copy.Add(CreateProgressBar(
                completed,
                collection.TotalPuzzleCount,
                "free-play-pack-progress"));
            button.Add(copy);

            var open = new Label("OPEN  \u203A");
            open.AddToClassList("free-play-pack-arrow");
            button.Add(open);

            button.clicked += () => OpenCollection(collection);
            return button;
        }

        private static VisualElement CreatePlanPreview(int seed)
        {
            var preview = new VisualElement();
            preview.AddToClassList("free-play-plan-preview");
            preview.AddToClassList(
                $"free-play-plan-preview-{((seed - 1) % 3) + 1}");

            string[] wallClasses =
            {
                "free-play-plan-wall-v1",
                "free-play-plan-wall-v2",
                "free-play-plan-wall-h1",
                "free-play-plan-wall-h2"
            };

            for (int i = 0; i < wallClasses.Length; i++)
            {
                var wall = new VisualElement();
                wall.AddToClassList("free-play-plan-wall");
                wall.AddToClassList(wallClasses[i]);
                preview.Add(wall);
            }

            var markerOne = new Label("4");
            markerOne.AddToClassList("free-play-plan-marker");
            markerOne.AddToClassList("free-play-plan-marker-one");
            preview.Add(markerOne);

            var markerTwo = new Label("6");
            markerTwo.AddToClassList("free-play-plan-marker");
            markerTwo.AddToClassList("free-play-plan-marker-two");
            preview.Add(markerTwo);

            return preview;
        }

        private static VisualElement CreateProgressBar(
            int completed,
            int total,
            string classPrefix)
        {
            var track = new VisualElement();
            track.AddToClassList($"{classPrefix}-track");

            var fill = new VisualElement();
            fill.AddToClassList($"{classPrefix}-fill");
            float percent = total > 0
                ? Mathf.Clamp01((float)completed / total) * 100f
                : 0f;
            fill.style.width = new Length(percent, LengthUnit.Percent);
            track.Add(fill);
            return track;
        }
        private void OpenCollection(
            PuzzleCatalog.FreePlayCollectionInfo collection)
        {
            _selectedCollection = collection;
            ShowSizeSelection();
        }

        private void ShowSizeSelection()
        {
            if (_selectedCollection == null)
            {
                ShowPackSelection();
                return;
            }

            _currentView = FreePlayView.Sizes;
            _selectedPackPath = null;
            _selectedSize = 0;

            _packView.style.display = DisplayStyle.None;
            _levelView.style.display = DisplayStyle.None;
            _sizeView.style.display = DisplayStyle.Flex;
            _sizePackTitle.text = _selectedCollection.DisplayName;

            BuildSizeList();
            _sizeScrollView.schedule.Execute(
                () => _sizeScrollView.scrollOffset = Vector2.zero
            );
        }

        private void BuildSizeList()
        {
            _sizeListRoot.Clear();
            IReadOnlyList<PuzzleCatalog.PackInfo> sizePacks =
                _selectedCollection.SizePacks;

            for (int rowStart = 0; rowStart < sizePacks.Count; rowStart += 2)
            {
                var row = new VisualElement();
                row.AddToClassList("free-play-size-row");
                row.Add(CreateSizeButton(sizePacks[rowStart]));

                if (rowStart + 1 < sizePacks.Count)
                {
                    row.Add(CreateSizeButton(sizePacks[rowStart + 1]));
                }
                else
                {
                    var blank = new VisualElement();
                    blank.AddToClassList("free-play-size-card-blank");
                    row.Add(blank);
                }

                _sizeListRoot.Add(row);
            }
        }

        private Button CreateSizeButton(PuzzleCatalog.PackInfo sizePack)
        {
            var button = new Button
            {
                name = $"free-play-size-{sizePack.Size}-button"
            };
            button.AddToClassList("free-play-size-button");

            var heading = new VisualElement();
            heading.AddToClassList("free-play-size-heading");

            var caption = new Label("FLOOR PLATE");
            caption.AddToClassList("free-play-size-caption");
            heading.Add(caption);

            var sheet = new Label($"FP-{sizePack.Size:00}");
            sheet.AddToClassList("free-play-size-sheet");
            heading.Add(sheet);
            button.Add(heading);

            var preview = new VisualElement();
            preview.AddToClassList("free-play-size-preview");
            for (int i = 1; i < 4; i++)
            {
                var vertical = new VisualElement();
                vertical.AddToClassList("free-play-size-grid-line");
                vertical.AddToClassList("free-play-size-grid-vertical");
                vertical.style.left = new Length(i * 25f, LengthUnit.Percent);
                preview.Add(vertical);

                var horizontal = new VisualElement();
                horizontal.AddToClassList("free-play-size-grid-line");
                horizontal.AddToClassList("free-play-size-grid-horizontal");
                horizontal.style.top = new Length(i * 25f, LengthUnit.Percent);
                preview.Add(horizontal);
            }

            var badge = new Label($"{sizePack.Size}\u00D7{sizePack.Size}");
            badge.AddToClassList("free-play-size-badge");
            preview.Add(badge);
            button.Add(preview);

            int completed = CountCompleted(
                PuzzleCatalog.GetPackPuzzleIds(sizePack.PackPath));
            var progress = new Label(
                $"{completed:00} / {sizePack.Count:00} APPROVED");
            progress.AddToClassList("free-play-size-progress");
            button.Add(progress);
            button.Add(CreateProgressBar(
                completed,
                sizePack.Count,
                "free-play-size-progress"));

            button.clicked += () =>
                ShowLevelSelection(sizePack.Size, sizePack.PackPath);
            return button;
        }
        private void ShowLevelSelection(int size, string packPath)
        {
            IReadOnlyList<string> puzzleIds =
                PuzzleCatalog.GetPackPuzzleIds(packPath);

            if (puzzleIds == null || puzzleIds.Count == 0)
            {
                Debug.LogError($"Free Play pack '{packPath}' has no puzzles.");
                ShowSizeSelection();
                return;
            }

            _currentView = FreePlayView.Levels;
            _selectedSize = size;
            _selectedPackPath = packPath;

            _packView.style.display = DisplayStyle.None;
            _sizeView.style.display = DisplayStyle.None;
            _levelView.style.display = DisplayStyle.Flex;
            _levelPackLabel.text = _selectedCollection != null
                ? _selectedCollection.DisplayName
                : "Free Play";


            int completed = CountCompleted(puzzleIds);
            int nextPuzzleIndex = FindNextIncomplete(puzzleIds);
            bool allCompleted = nextPuzzleIndex < 0;
            if (allCompleted)
                nextPuzzleIndex = 0;

            _continueLevelIndex = nextPuzzleIndex + 1;
            _continuePuzzleId = puzzleIds[nextPuzzleIndex];

            _levelTitleLabel.text = $"{size}\u00D7{size}";
            _levelProgressLabel.text =
                $"{completed:00} / {puzzleIds.Count:00} APPROVED";
            _continueLabel.text = allCompleted
                ? "REOPEN PLAN 01"
                : $"RESUME PLAN {_continueLevelIndex:00}";

            BuildLevelGrid(puzzleIds, allCompleted ? -1 : nextPuzzleIndex);
            _levelScrollView.schedule.Execute(() =>
            {
                if (_nextLevelButton != null)
                    _levelScrollView.ScrollTo(_nextLevelButton);
                else
                    _levelScrollView.scrollOffset = Vector2.zero;
            });
        }

        private void BuildLevelGrid(
            IReadOnlyList<string> puzzleIds,
            int nextPuzzleIndex)
        {
            _levelGridRoot.Clear();
            _nextLevelButton = null;

            int unlockedLevel = nextPuzzleIndex < 0
                ? puzzleIds.Count
                : Mathf.Clamp(nextPuzzleIndex + 1, 1, puzzleIds.Count);

            var gridPanel = new VisualElement();
            gridPanel.AddToClassList("free-play-level-grid-panel");

            for (int rowStart = 0;
                 rowStart < puzzleIds.Count;
                 rowStart += ColumnsPerRow)
            {
                var row = new VisualElement();
                row.AddToClassList("free-play-level-row");

                for (int column = 0; column < ColumnsPerRow; column++)
                {
                    int puzzleIndex = rowStart + column;
                    if (puzzleIndex >= puzzleIds.Count)
                    {
                        var blank = new VisualElement();
                        blank.AddToClassList("free-play-level-cell");
                        blank.AddToClassList("free-play-level-blank");
                        row.Add(blank);
                        continue;
                    }

                    int levelIndex = puzzleIndex + 1;
                    string puzzleId = puzzleIds[puzzleIndex];
                    bool completed = GameplayHUD.IsCompleted(puzzleId);
                    bool unlocked = levelIndex <= unlockedLevel;
                    bool isNext = puzzleIndex == nextPuzzleIndex &&
                        !completed &&
                        unlocked;

                    Button levelButton = CreateLevelButton(
                        levelIndex,
                        puzzleId,
                        completed,
                        unlocked,
                        isNext
                    );

                    if (isNext)
                        _nextLevelButton = levelButton;

                    row.Add(levelButton);
                }

                gridPanel.Add(row);
            }

            _levelGridRoot.Add(gridPanel);
        }
        private Button CreateLevelButton(
            int levelIndex,
            string puzzleId,
            bool completed,
            bool unlocked,
            bool isNext)
        {
            var button = new Button
            {
                name = $"free-play-level-{levelIndex}-button"
            };

            button.AddToClassList("free-play-level-cell");
            button.AddToClassList("free-play-level-button");

            var titleStrip = new VisualElement();
            titleStrip.AddToClassList("free-play-level-title-strip");

            var planLabel = new Label("PLAN");
            planLabel.AddToClassList("free-play-level-plan-label");
            titleStrip.Add(planLabel);

            var sheetLabel = new Label($"A-{levelIndex:00}");
            sheetLabel.AddToClassList("free-play-level-sheet-label");
            titleStrip.Add(sheetLabel);
            button.Add(titleStrip);

            var numberLabel = new Label(levelIndex.ToString("00"));
            numberLabel.AddToClassList("free-play-level-number");
            button.Add(numberLabel);

            if (isNext)
            {
                button.AddToClassList("free-play-level-next");

                var nextLabel = new Label("NEXT");
                nextLabel.AddToClassList("free-play-level-next-label");
                button.Add(nextLabel);

                var topCorner = new VisualElement();
                topCorner.AddToClassList("free-play-level-corner");
                topCorner.AddToClassList("free-play-level-corner-top");
                button.Add(topCorner);

                var bottomCorner = new VisualElement();
                bottomCorner.AddToClassList("free-play-level-corner");
                bottomCorner.AddToClassList("free-play-level-corner-bottom");
                button.Add(bottomCorner);
            }

            if (completed)
            {
                button.AddToClassList("free-play-level-completed");
                var stamp = new Label("APPROVED");
                stamp.AddToClassList("free-play-level-approved-stamp");
                button.Add(stamp);
            }

            if (!unlocked)
            {
                button.AddToClassList("free-play-level-locked");

                var constructionLine = new VisualElement();
                constructionLine.AddToClassList("free-play-level-lock-line");
                button.Add(constructionLine);

                var lockedLabel = new Label("HOLD");
                lockedLabel.AddToClassList("free-play-level-locked-label");
                button.Add(lockedLabel);
            }
            else
            {
                if (!completed && !isNext)
                {
                    var readyLabel = new Label("READY");
                    readyLabel.AddToClassList("free-play-level-ready-label");
                    button.Add(readyLabel);
                }

                button.clicked += () => StartLevel(levelIndex, puzzleId);
            }

            button.SetEnabled(unlocked);
            return button;
        }
        private void ContinueLevel()
        {
            if (_continueLevelIndex <= 0 ||
                string.IsNullOrEmpty(_continuePuzzleId))
            {
                return;
            }

            StartLevel(_continueLevelIndex, _continuePuzzleId);
        }

        private static int FindNextIncomplete(
            IReadOnlyList<string> puzzleIds)
        {
            for (int i = 0; i < puzzleIds.Count; i++)
            {
                if (!GameplayHUD.IsCompleted(puzzleIds[i]))
                    return i;
            }

            return -1;
        }
        private void StartLevel(int levelIndex, string puzzleId)
        {
            if (_isLoading || string.IsNullOrEmpty(_selectedPackPath))
                return;

            _isLoading = true;

            GameSession.Mode = MenuMode.FreePlay;
            GameSession.LevelIndex = levelIndex;
            GameSession.Size = _selectedSize;
            GameSession.PackPath = _selectedPackPath;
            GameSession.SetPuzzle(puzzleId);

            PlayerPrefs.Save();
            SceneManager.LoadScene("Gameplay");
        }

        private static int CountCompleted(IReadOnlyList<string> puzzleIds)
        {
            if (puzzleIds == null)
                return 0;

            int completed = 0;
            for (int i = 0; i < puzzleIds.Count; i++)
            {
                if (GameplayHUD.IsCompleted(puzzleIds[i]))
                    completed++;
            }

            return completed;
        }

        private static int CountCompleted(
            PuzzleCatalog.FreePlayCollectionInfo collection)
        {
            int completed = 0;
            for (int i = 0; i < collection.SizePacks.Count; i++)
            {
                completed += CountCompleted(PuzzleCatalog.GetPackPuzzleIds(
                    collection.SizePacks[i].PackPath));
            }

            return completed;
        }
    }
}
