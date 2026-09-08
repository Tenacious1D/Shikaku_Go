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

            var emblem = new Label($"C{collectionNumber}");
            emblem.AddToClassList("free-play-pack-emblem");
            emblem.AddToClassList("free-play-pack-emblem-label");
            button.Add(emblem);

            var copy = new VisualElement();
            copy.AddToClassList("free-play-pack-copy");

            var name = new Label(collection.DisplayName);
            name.AddToClassList("free-play-pack-name");
            copy.Add(name);

            int completed = CountCompleted(collection);
            var progress = new Label(
                $"{completed} / {collection.TotalPuzzleCount} complete");
            progress.AddToClassList("free-play-pack-progress");
            copy.Add(progress);
            button.Add(copy);

            var arrow = new Label("\u203A");
            arrow.AddToClassList("free-play-pack-arrow");
            button.Add(arrow);

            button.clicked += () => OpenCollection(collection);
            return button;
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
            for (int i = 0; i < _selectedCollection.SizePacks.Count; i++)
                _sizeListRoot.Add(CreateSizeButton(
                    _selectedCollection.SizePacks[i]));
        }

        private Button CreateSizeButton(PuzzleCatalog.PackInfo sizePack)
        {
            var button = new Button
            {
                name = $"free-play-size-{sizePack.Size}-button"
            };
            button.AddToClassList("free-play-size-button");
            button.AddToClassList(ToneClassForSize(sizePack.Size));

            var badge = new Label($"{sizePack.Size}\u00D7{sizePack.Size}");
            badge.AddToClassList("free-play-size-badge");
            button.Add(badge);

            int completed = CountCompleted(
                PuzzleCatalog.GetPackPuzzleIds(sizePack.PackPath));
            var progress = new Label(
                $"{completed} / {sizePack.Count} complete");
            progress.AddToClassList("free-play-size-progress");
            button.Add(progress);

            var arrow = new Label("\u203A");
            arrow.AddToClassList("free-play-size-arrow");
            button.Add(arrow);

            button.clicked += () =>
                ShowLevelSelection(sizePack.Size, sizePack.PackPath);
            return button;
        }

        private static string ToneClassForSize(int size)
        {
            if (size <= 5)
                return "free-play-size-green";

            if (size <= 7)
                return "free-play-size-blue";

            return "free-play-size-red";
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
            ApplyLevelTone(size);

            int completed = CountCompleted(puzzleIds);
            int nextPuzzleIndex = FindNextIncomplete(puzzleIds);
            bool allCompleted = nextPuzzleIndex < 0;
            if (allCompleted)
                nextPuzzleIndex = 0;

            _continueLevelIndex = nextPuzzleIndex + 1;
            _continuePuzzleId = puzzleIds[nextPuzzleIndex];

            _levelTitleLabel.text = $"{size}\u00D7{size}";
            _levelProgressLabel.text =
                $"{completed} of {puzzleIds.Count} completed";
            _continueLabel.text = allCompleted
                ? "Replay Puzzle 1"
                : $"Continue \u2022 Puzzle {_continueLevelIndex}";

            BuildLevelGrid(puzzleIds, allCompleted ? -1 : nextPuzzleIndex);
            _levelScrollView.schedule.Execute(() =>
            {
                if (_nextLevelButton != null)
                    _levelScrollView.ScrollTo(_nextLevelButton);
                else
                    _levelScrollView.scrollOffset = Vector2.zero;
            });
        }

        private void ApplyLevelTone(int size)
        {
            _levelView.RemoveFromClassList("free-play-level-green");
            _levelView.RemoveFromClassList("free-play-level-blue");
            _levelView.RemoveFromClassList("free-play-level-red");

            if (size <= 5)
                _levelView.AddToClassList("free-play-level-green");
            else if (size <= 7)
                _levelView.AddToClassList("free-play-level-blue");
            else
                _levelView.AddToClassList("free-play-level-red");
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

            var numberLabel = new Label(levelIndex.ToString());
            numberLabel.AddToClassList("free-play-level-number");
            button.Add(numberLabel);

            if (isNext)
            {
                button.AddToClassList("free-play-level-next");
                var nextLabel = new Label("NEXT");
                nextLabel.AddToClassList("free-play-level-next-label");
                button.Add(nextLabel);
            }
            if (completed)
            {
                button.AddToClassList("free-play-level-completed");
                var checkmark = new Label("\u2713");
                checkmark.AddToClassList("free-play-level-checkmark");
                button.Add(checkmark);
            }

            if (!unlocked)
            {
                button.AddToClassList("free-play-level-locked");
                var lockedLabel = new Label("LOCKED");
                lockedLabel.AddToClassList("free-play-level-locked-label");
                button.Add(lockedLabel);
            }
            else
            {
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
