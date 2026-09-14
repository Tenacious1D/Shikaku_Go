using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Shikaku.Menu
{
    public sealed class TimeTrialScreenController : IDisposable
    {
        private readonly Action _returnHome;
        private readonly VisualElement _screen;
        private readonly Button _backButton;
        private readonly Button _quickButton;
        private readonly Button _classicButton;
        private readonly Button _expertButton;
        private readonly Label _quickBestLabel;
        private readonly Label _classicBestLabel;
        private readonly Label _expertBestLabel;

        private bool _isVisible;
        private bool _isStarting;

        public TimeTrialScreenController(
            VisualElement documentRoot,
            Action returnHome)
        {
            _returnHome = returnHome;
            _screen = RequireElement<VisualElement>(
                documentRoot,
                "time-trial-screen");
            _backButton = RequireElement<Button>(
                documentRoot,
                "time-trial-back-button");
            _quickButton = RequireElement<Button>(
                documentRoot,
                "time-trial-quick-button");
            _classicButton = RequireElement<Button>(
                documentRoot,
                "time-trial-classic-button");
            _expertButton = RequireElement<Button>(
                documentRoot,
                "time-trial-expert-button");
            _quickBestLabel = RequireElement<Label>(
                documentRoot,
                "time-trial-quick-best");
            _classicBestLabel = RequireElement<Label>(
                documentRoot,
                "time-trial-classic-best");
            _expertBestLabel = RequireElement<Label>(
                documentRoot,
                "time-trial-expert-best");

            _backButton.BringToFront();
            _backButton.clicked += ReturnHome;
            _quickButton.clicked += StartQuick;
            _classicButton.clicked += StartClassic;
            _expertButton.clicked += StartExpert;

            Hide();
        }

        public void Show()
        {
            _isVisible = true;
            _isStarting = false;
            _screen.style.display = DisplayStyle.Flex;
            SetModeButtonsEnabled(true);
            RefreshBestScores();
        }

        public void Hide()
        {
            _isVisible = false;
            _screen.style.display = DisplayStyle.None;
        }

        public bool HandleBack()
        {
            if (!_isVisible || _isStarting)
                return false;

            ReturnHome();
            return true;
        }

        public void Dispose()
        {
            _backButton.clicked -= ReturnHome;
            _quickButton.clicked -= StartQuick;
            _classicButton.clicked -= StartClassic;
            _expertButton.clicked -= StartExpert;
        }

        private static T RequireElement<T>(
            VisualElement root,
            string name)
            where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"Time Trial UI element '{name}' was not found.");
            }

            return element;
        }

        private void ReturnHome()
        {
            _returnHome?.Invoke();
        }

        private void RefreshBestScores()
        {
            RefreshBestScore(_quickBestLabel, 4);
            RefreshBestScore(_classicBestLabel, 5);
            RefreshBestScore(_expertBestLabel, 6);
        }

        private static void RefreshBestScore(Label label, int size)
        {
            int best = GameSession.GetTimeTrialBestSquares(size);
            label.text = best > 0
                ? $"{best} {(best == 1 ? "SQUARE" : "SQUARES")}"
                : "--";
        }

        private void StartQuick() => StartFixedTimeTrial(4, 60);
        private void StartClassic() => StartFixedTimeTrial(5, 90);
        private void StartExpert() => StartFixedTimeTrial(6, 120);

        private void StartFixedTimeTrial(int size, int seconds)
        {
            if (_isStarting)
                return;

            string packPath = $"TimeTrial/TimeTrial_{size}x{size}";
            var puzzleIds = PuzzleCatalog.GetPackPuzzleIds(packPath);

            if (puzzleIds == null || puzzleIds.Count == 0)
            {
                Debug.LogError(
                    $"No {size}x{size} puzzles found at {packPath}.");
                return;
            }

            string chosenPuzzleId =
                GameSession.PickTimeTrialPuzzleAvoidingRecent(
                    size,
                    puzzleIds);

            if (string.IsNullOrEmpty(chosenPuzzleId))
            {
                Debug.LogError(
                    $"Could not select a {size}x{size} Time Trial puzzle.");
                return;
            }

            _isStarting = true;
            SetModeButtonsEnabled(false);

            GameSession.Mode = MenuMode.TimeTrial;
            GameSession.Size = size;
            GameSession.PackPath = packPath;
            GameSession.LevelIndex = 1;
            GameSession.TimeLimitSeconds = seconds;
            GameSession.TimeTrialSolvedCount = 0;
            GameSession.TimeTrialCompletedSquares = 0;
            GameSession.SetPuzzle(chosenPuzzleId);

            PlayerPrefs.Save();
            EnterGameplay();
        }

        private void SetModeButtonsEnabled(bool enabled)
        {
            _quickButton.SetEnabled(enabled);
            _classicButton.SetEnabled(enabled);
            _expertButton.SetEnabled(enabled);
        }

        private static void EnterGameplay()
        {
            SceneManager.LoadScene("Gameplay");
        }
    }
}