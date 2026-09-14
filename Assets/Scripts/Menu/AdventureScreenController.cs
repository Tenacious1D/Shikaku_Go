using System;
using System.Collections.Generic;
using Shikaku.Ads;
using Shikaku.UI;
using Shikaku.UI.Buildings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Shikaku.Menu
{
    /// <summary>
    /// Controls the Adventure chapter map and the per-chapter level selector.
    /// Both views live inside HomeUI so returning from Gameplay does not require
    /// another scene or another progression system.
    /// </summary>
    public sealed class AdventureScreenController : IDisposable
    {
        private const int LevelColumns = 5;
        private const float MapFocusViewportPosition = 0.68f;
        private const float MapFocusTrailingSpaceRatio = 0.32f;

        private readonly Action _showHome;
        private readonly VisualElement _documentRoot;
        private readonly VisualElement _screen;
        private readonly VisualElement _mapView;
        private readonly VisualElement _levelView;
        private readonly VisualElement _mapRoot;
        private readonly VisualElement _levelGridRoot;
        private readonly ScrollView _mapScroll;
        private readonly ScrollView _levelScroll;
        private readonly Button _backButton;
        private readonly Button _continueButton;
        private readonly Label _mapProgress;
        private readonly Label _levelTitle;
        private readonly Label _levelProgress;
        private readonly Label _continueLabel;

        private int _selectedChapterIndex = -1;
        private string _selectedPackPath;
        private int _continueLevelIndex;
        private string _continuePuzzleId;
        private Button _nextLevelButton;
        private bool _isVisible;
        private bool _showingLevels;
        private bool _isLoading;

        public AdventureScreenController(VisualElement root, Action showHome)
        {
            _showHome = showHome;
            _documentRoot = root;
            _screen = root.Q<VisualElement>("adventure-screen");
            _mapView = root.Q<VisualElement>("adventure-map-view");
            _levelView = root.Q<VisualElement>("adventure-level-view");
            _mapRoot = root.Q<VisualElement>("adventure-map-root");
            _levelGridRoot = root.Q<VisualElement>("adventure-level-grid");
            _mapScroll = root.Q<ScrollView>("adventure-map-scroll");
            _levelScroll = root.Q<ScrollView>("adventure-level-scroll");
            _backButton = root.Q<Button>("adventure-back-button");
            _continueButton = root.Q<Button>("adventure-continue-button");
            _mapProgress = root.Q<Label>("adventure-map-progress");
            _levelTitle = root.Q<Label>("adventure-level-title");
            _levelProgress = root.Q<Label>("adventure-level-progress");
            _continueLabel = root.Q<Label>("adventure-continue-label");

            if (_screen == null || _mapView == null || _levelView == null ||
                _mapRoot == null || _levelGridRoot == null ||
                _mapScroll == null || _levelScroll == null)
            {
                Debug.LogError(
                    "AdventureScreenController could not find all required Adventure UI elements.");
                return;
            }

            if (_backButton != null)
                _backButton.clicked += OnBackPressed;

            if (_continueButton != null)
                _continueButton.clicked += ContinueChapter;

            AdsManager.BannerPresentationChanged +=
                OnBannerPresentationChanged;
            _screen.RegisterCallback<GeometryChangedEvent>(
                OnScreenGeometryChanged);
            ApplyBannerInset();
        }

        public bool IsVisible => _isVisible;

        public void Show(string requestedPackPath = null)
        {
            if (_screen == null)
                return;

            _isLoading = false;
            _isVisible = true;
            _screen.style.display = DisplayStyle.Flex;
            ShowMap(requestedPackPath);
        }

        public void Hide()
        {
            _isVisible = false;
            _showingLevels = false;

            if (_screen != null)
                _screen.style.display = DisplayStyle.None;
        }

        public bool HandleBack()
        {
            if (!IsVisible)
                return false;

            if (_showingLevels)
            {
                ShowMap(_selectedPackPath);
                return true;
            }

            Hide();
            _showHome?.Invoke();
            return true;
        }

        public void Dispose()
        {
            AdsManager.BannerPresentationChanged -=
                OnBannerPresentationChanged;
            if (_screen != null)
            {
                _screen.UnregisterCallback<GeometryChangedEvent>(
                    OnScreenGeometryChanged);
            }

            if (_backButton != null)
                _backButton.clicked -= OnBackPressed;

            if (_continueButton != null)
                _continueButton.clicked -= ContinueChapter;
        }

        private void OnBannerPresentationChanged(bool isPresented)
        {
            ApplyBannerInset();
        }

        private void OnScreenGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyBannerInset();
        }

        private void ApplyBannerInset()
        {
            if (_mapScroll == null || _levelScroll == null)
                return;

            float inset = AdsManager.GetBannerContentInset(
                _documentRoot,
                includeBottomSafeArea: true);
            _mapScroll.style.marginBottom = inset;
            _levelScroll.style.marginBottom = inset;
        }

        private void OnBackPressed()
        {
            HandleBack();
        }

        private void ShowMap(string focusPackPath = null)
        {
            IReadOnlyList<PuzzleCatalog.PackInfo> packs = PuzzleCatalog.StoryPacks;

            _selectedChapterIndex = FindPackIndex(packs, focusPackPath);
            _selectedPackPath = _selectedChapterIndex >= 0
                ? packs[_selectedChapterIndex].PackPath
                : null;

            _showingLevels = false;
            _levelView.style.display = DisplayStyle.None;
            _mapView.style.display = DisplayStyle.Flex;

            BuildMap(packs, focusPackPath);
        }

        private void BuildMap(
            IReadOnlyList<PuzzleCatalog.PackInfo> packs,
            string focusPackPath)
        {
            _mapRoot.Clear();

            if (packs == null || packs.Count == 0)
            {
                _mapProgress.text = "No chapters available";
                return;
            }

            int completedChapters = 0;
            for (int i = 0; i < packs.Count; i++)
            {
                IReadOnlyList<string> ids =
                    PuzzleCatalog.GetPackPuzzleIds(packs[i].PackPath);

                if (ids.Count > 0 && CountCompleted(ids) == ids.Count)
                    completedChapters++;
            }

            _mapProgress.text =
                $"{completedChapters} of {packs.Count} chapters completed";

            Progression.TryGetStoryContinue(
                out string continuePackPath,
                out _);

            var city = new AdventureCityMap(packs.Count);
            _mapRoot.Add(city);
            Button focusButton = null;

            // Reverse visual order: the highest chapter is above Chapter 1.
            for (int chapterIndex = packs.Count - 1;
                 chapterIndex >= 0;
                 chapterIndex--)
            {
                PuzzleCatalog.PackInfo pack = packs[chapterIndex];
                IReadOnlyList<string> puzzleIds =
                    PuzzleCatalog.GetPackPuzzleIds(pack.PackPath);

                int completed = CountCompleted(puzzleIds);
                bool complete =
                    puzzleIds.Count > 0 && completed == puzzleIds.Count;
                bool unlocked =
                    Progression.IsStoryChapterUnlocked(pack.PackPath);
                bool current =
                    unlocked &&
                    string.Equals(
                        continuePackPath,
                        pack.PackPath,
                        StringComparison.Ordinal);

                int capturedChapterIndex = chapterIndex;
                Button chapterButton = CreateChapterButton(
                    chapterIndex,
                    puzzleIds.Count,
                    completed,
                    unlocked,
                    complete,
                    current);

                var buildingData = AdventureBuildingData.Load(pack.PackPath);
                city.AddChapter(chapterIndex, buildingData, chapterButton);
                if (!string.IsNullOrWhiteSpace(buildingData?.Definition?.displayName))
                    chapterButton.tooltip = buildingData.Definition.displayName + " — " + chapterButton.tooltip;
                if (unlocked)
                {
                    chapterButton.clicked +=
                        () => ShowLevels(capturedChapterIndex);
                }
                else
                {
                    chapterButton.clicked +=
                        () => ShowLockedFeedback(chapterButton);
                }



                if (focusButton == null &&
                    (string.Equals(
                         focusPackPath,
                         pack.PackPath,
                         StringComparison.Ordinal) ||
                     (string.IsNullOrEmpty(focusPackPath) && current)))
                {
                    focusButton = chapterButton;
                }

            }

            if (focusButton == null)
            {
                int highest =
                    Progression.GetHighestUnlockedStoryChapterIndex();
                focusButton = _mapRoot.Q<Button>(
                    $"adventure-chapter-{highest + 1}-button");
            }

            Button target = focusButton;
            ScheduleMapFocus(target);
        }

        private void ScheduleMapFocus(Button target)
        {
            if (target == null)
                return;

            _mapScroll.schedule.Execute(() =>
            {
                float viewportHeight =
                    _mapScroll.contentViewport.worldBound.height;
                if (viewportHeight > 1f)
                {
                    _mapRoot.style.marginBottom =
                        viewportHeight * MapFocusTrailingSpaceRatio;
                }

                _mapScroll.schedule.Execute(
                    () => PositionMapFocus(target));
            });
        }

        private void PositionMapFocus(Button target)
        {
            if (target == null)
                return;

            Rect viewportBounds = _mapScroll.contentViewport.worldBound;
            Rect contentBounds = _mapScroll.contentContainer.worldBound;
            if (viewportBounds.height <= 1f || contentBounds.height <= 1f)
            {
                _mapScroll.ScrollTo(target);
                return;
            }

            float targetCenterInContent =
                target.worldBound.center.y - contentBounds.yMin;
            float desiredOffsetY =
                targetCenterInContent -
                viewportBounds.height * MapFocusViewportPosition;

            _mapScroll.scrollOffset = new Vector2(
                _mapScroll.scrollOffset.x,
                Mathf.Max(0f, desiredOffsetY));
        }

        private static Button CreateChapterButton(
            int chapterIndex, int total, int completed, bool unlocked, bool complete, bool current)
        {
            int chapterNumber = chapterIndex + 1;
            var button = new Button { name = $"adventure-chapter-{chapterNumber}-button" };
            button.AddToClassList("city-chapter-button");
            button.AddToClassList(complete ? "city-chapter-complete" :
                current ? "city-chapter-current" : unlocked ? "city-chapter-unlocked" : "city-chapter-locked");
            string state = complete ? "✓ " : current ? "› " : !unlocked ? "• " : "";
            button.tooltip = $"Chapter {chapterNumber}: " + (unlocked ? $"{completed} of {total} floors completed" : "Locked");
            button.text = complete ? $"✓ {chapterNumber}" : $"{state}{chapterNumber}  ·  " +
                (unlocked ? $"{completed}/{total}" : "Locked");
            return button;
        }

        private static void ShowLockedFeedback(Button button)
        {
            if (button == null)
                return;

            button.RemoveFromClassList("adventure-chapter-denied");
            button.AddToClassList("adventure-chapter-denied");
            button.schedule.Execute(
                () => button.RemoveFromClassList("adventure-chapter-denied"))
                .StartingIn(110);
        }
        private void ShowLevels(int chapterIndex)
        {
            IReadOnlyList<PuzzleCatalog.PackInfo> packs = PuzzleCatalog.StoryPacks;
            if (packs == null ||
                chapterIndex < 0 ||
                chapterIndex >= packs.Count)
            {
                return;
            }

            PuzzleCatalog.PackInfo pack = packs[chapterIndex];
            if (!Progression.IsStoryChapterUnlocked(pack.PackPath))
                return;

            IReadOnlyList<string> puzzleIds =
                PuzzleCatalog.GetPackPuzzleIds(pack.PackPath);

            if (puzzleIds == null || puzzleIds.Count == 0)
                return;

            _selectedChapterIndex = chapterIndex;
            _selectedPackPath = pack.PackPath;
            _showingLevels = true;
            _mapView.style.display = DisplayStyle.None;
            _levelView.style.display = DisplayStyle.Flex;

            int completed = CountCompleted(puzzleIds);
            int nextLevel = Progression.GetNextStoryLevel(pack.PackPath);
            bool allComplete = nextLevel < 1;

            _continueLevelIndex = allComplete ? 1 : nextLevel;
            _continuePuzzleId =
                puzzleIds[Mathf.Clamp(_continueLevelIndex - 1, 0, puzzleIds.Count - 1)];

            _levelTitle.text = $"Chapter {chapterIndex + 1}";
            _levelProgress.text =
                $"{completed} of {puzzleIds.Count} completed";
            _continueLabel.text = allComplete
                ? "Replay Puzzle 1"
                : $"Continue • Puzzle {_continueLevelIndex}";

            BuildLevelGrid(puzzleIds, nextLevel);

            _levelScroll.schedule.Execute(() =>
            {
                if (_nextLevelButton != null)
                    _levelScroll.ScrollTo(_nextLevelButton);
                else
                    _levelScroll.scrollOffset = Vector2.zero;
            });
        }

        private void BuildLevelGrid(
            IReadOnlyList<string> puzzleIds,
            int nextLevel)
        {
            _levelGridRoot.Clear();
            _nextLevelButton = null;

            VisualElement panel = new VisualElement();
            panel.AddToClassList("adventure-level-grid-panel");

            for (int rowStart = 0;
                 rowStart < puzzleIds.Count;
                 rowStart += LevelColumns)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("adventure-level-row");

                for (int column = 0; column < LevelColumns; column++)
                {
                    int puzzleIndex = rowStart + column;
                    if (puzzleIndex >= puzzleIds.Count)
                    {
                        VisualElement blank = new VisualElement();
                        blank.AddToClassList("adventure-level-cell");
                        blank.AddToClassList("adventure-level-blank");
                        row.Add(blank);
                        continue;
                    }

                    int levelIndex = puzzleIndex + 1;
                    string puzzleId = puzzleIds[puzzleIndex];
                    bool completed = GameplayHUD.IsCompleted(puzzleId);
                    bool isNext = levelIndex == nextLevel;
                    bool accessible = completed || isNext;

                    Button levelButton = CreateLevelButton(
                        levelIndex,
                        completed,
                        accessible,
                        isNext);

                    if (accessible)
                    {
                        int capturedLevel = levelIndex;
                        string capturedPuzzleId = puzzleId;
                        levelButton.clicked +=
                            () => StartLevel(capturedLevel, capturedPuzzleId);
                    }

                    if (isNext)
                        _nextLevelButton = levelButton;

                    row.Add(levelButton);
                }

                panel.Add(row);
            }

            _levelGridRoot.Add(panel);
        }

        private static Button CreateLevelButton(
            int levelIndex,
            bool completed,
            bool accessible,
            bool isNext)
        {
            Button button = new Button
            {
                name = $"adventure-level-{levelIndex}-button"
            };

            button.AddToClassList("adventure-level-cell");
            button.AddToClassList("adventure-level-button");

            if (completed)
                button.AddToClassList("adventure-level-completed");
            else if (isNext)
                button.AddToClassList("adventure-level-next");
            else
                button.AddToClassList("adventure-level-locked");

            Label number = new Label(levelIndex.ToString());
            number.AddToClassList("adventure-level-number");
            button.Add(number);

            if (completed)
            {
                Label check = new Label("✓");
                check.AddToClassList("adventure-level-checkmark");
                button.Add(check);
            }
            else if (isNext)
            {
                Label next = new Label("NEXT");
                next.AddToClassList("adventure-level-next-label");
                button.Add(next);
            }
            else
            {
                Label locked = new Label("LOCKED");
                locked.AddToClassList("adventure-level-locked-label");
                button.Add(locked);
            }

            button.SetEnabled(accessible);
            return button;
        }

        private void ContinueChapter()
        {
            if (_continueLevelIndex < 1 ||
                string.IsNullOrEmpty(_continuePuzzleId))
            {
                return;
            }

            StartLevel(_continueLevelIndex, _continuePuzzleId);
        }

        private void StartLevel(int levelIndex, string puzzleId)
        {
            if (_isLoading ||
                string.IsNullOrEmpty(_selectedPackPath) ||
                string.IsNullOrEmpty(puzzleId))
            {
                return;
            }

            bool completed = GameplayHUD.IsCompleted(puzzleId);
            int nextLevel = Progression.GetNextStoryLevel(_selectedPackPath);
            if (!completed && levelIndex != nextLevel)
                return;

            _isLoading = true;

            GameSession.Mode = MenuMode.Story;
            GameSession.LevelIndex = levelIndex;
            GameSession.PackPath = _selectedPackPath;
            GameSession.SetPuzzle(puzzleId);

            Progression.SetStoryCurrentPackIfLater(_selectedPackPath);
            Progression.SetLastPlayedLevelForPack(
                _selectedPackPath,
                levelIndex);

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

        private static int FindPackIndex(
            IReadOnlyList<PuzzleCatalog.PackInfo> packs,
            string packPath)
        {
            if (packs == null || string.IsNullOrEmpty(packPath))
                return -1;

            for (int i = 0; i < packs.Count; i++)
            {
                if (string.Equals(
                    packs[i].PackPath,
                    packPath,
                    StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
