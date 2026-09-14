using System;
using System.Collections.Generic;
using System.Globalization;
using Shikaku.Logic;
using Shikaku.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Shikaku.Menu
{
    public sealed class DailyScreenController : IDisposable
    {
        private const int EasyAvailability = 1;
        private const int MediumAvailability = 2;
        private const int HardAvailability = 4;
        private const int AllDifficultiesAvailable = 7;
        private readonly Action _returnHome;
        private readonly VisualElement _screen;
        private readonly Label _todayDateLabel;
        private readonly Label _todayStatusLabel;
        private readonly Label _todayWorkOrderNumberLabel;
        private readonly Label _monthTitleLabel;
        private readonly Label _monthReferenceLabel;
        private readonly Label _monthCompletedLabel;
        private readonly VisualElement _calendarGrid;
        private readonly Label _streakCountLabel;
        private readonly Button _backButton;
        private readonly Button _playTodayButton;
        private readonly Button _previousMonthButton;
        private readonly Button _nextMonthButton;
        private readonly VisualElement _difficultyModal;
        private readonly Button _modalDismissButton;
        private readonly Button _modalCloseButton;
        private readonly Label _modalTitleLabel;
        private readonly Label _modalDateLabel;
        private readonly Button _easyButton;
        private readonly Button _mediumButton;
        private readonly Button _hardButton;
        private readonly HashSet<DateTime> _availableDailyDates;
        private readonly DateTime _firstAvailableDate;

        private DateTime _selectedDate;
        private DateTime _visibleMonth;
        private bool _isVisible;
        private bool _isModalOpen;

        public DailyScreenController(VisualElement documentRoot, Action returnHome)
        {
            _returnHome = returnHome;
            DateTime firstAvailableDate;
            _availableDailyDates = LoadAvailableDailyDates(
                out firstAvailableDate);
            _firstAvailableDate = firstAvailableDate;

            _screen = RequireElement<VisualElement>(documentRoot, "daily-screen");
            _todayDateLabel = RequireElement<Label>(documentRoot, "daily-today-date");
            _todayStatusLabel = RequireElement<Label>(documentRoot, "daily-today-status");
            _todayWorkOrderNumberLabel = RequireElement<Label>(documentRoot, "daily-work-order-number");
            _monthTitleLabel = RequireElement<Label>(documentRoot, "daily-month-title");
            _monthReferenceLabel = RequireElement<Label>(documentRoot, "daily-month-reference");
            _monthCompletedLabel = RequireElement<Label>(documentRoot, "daily-month-completed");
            _calendarGrid = RequireElement<VisualElement>(documentRoot, "daily-calendar-grid");
            _streakCountLabel = RequireElement<Label>(
                documentRoot, "daily-streak-count");
            _backButton = RequireElement<Button>(documentRoot, "daily-back-button");
            _playTodayButton = RequireElement<Button>(documentRoot, "daily-play-today-button");
            _previousMonthButton = RequireElement<Button>(documentRoot, "daily-previous-month-button");
            _nextMonthButton = RequireElement<Button>(documentRoot, "daily-next-month-button");
            _difficultyModal = RequireElement<VisualElement>(documentRoot, "daily-difficulty-modal");
            _modalDismissButton = RequireElement<Button>(documentRoot, "daily-modal-dismiss-button");
            _modalCloseButton = RequireElement<Button>(documentRoot, "daily-modal-close-button");
            _modalTitleLabel = RequireElement<Label>(documentRoot, "daily-modal-title");
            _modalDateLabel = RequireElement<Label>(documentRoot, "daily-modal-date");
            _easyButton = RequireElement<Button>(documentRoot, "daily-easy-button");
            _mediumButton = RequireElement<Button>(documentRoot, "daily-medium-button");
            _hardButton = RequireElement<Button>(documentRoot, "daily-hard-button");

            // Keep the full-screen content from intercepting the back button, while
            // preserving the difficulty modal as the topmost input layer.
            _backButton.BringToFront();
            _difficultyModal.BringToFront();
            _backButton.clicked += ReturnHome;
            _playTodayButton.clicked += OpenToday;
            _previousMonthButton.clicked += ShowPreviousMonth;
            _nextMonthButton.clicked += ShowNextMonth;
            _modalDismissButton.clicked += CloseModal;
            _modalCloseButton.clicked += CloseModal;
            _easyButton.clicked += StartEasy;
            _mediumButton.clicked += StartMedium;
            _hardButton.clicked += StartHard;

            Hide();
        }

        public void Show()
        {
            _isVisible = true;
            _screen.style.display = DisplayStyle.Flex;
            _todayDateLabel.text = DateTime.Today
                .ToString("MMM d, yyyy");
            _streakCountLabel.text =
                DailyStreakService.CurrentStreak.ToString();
            _todayWorkOrderNumberLabel.text =
                DateTime.Today.Day.ToString("00");

            bool todaySelectable = IsDailyDateSelectable(DateTime.Today);
            bool todayApproved = todaySelectable &&
                Progression.IsDailyDateFullyCompleted(DateTime.Today);
            _todayStatusLabel.text = todayApproved
                ? "Complete · Play again"
                : todaySelectable
                    ? "Choose your difficulty"
                    : "No puzzle available today";
            _playTodayButton.SetEnabled(todaySelectable);
            _playTodayButton.EnableInClassList(
                "daily-work-order-approved",
                todayApproved);
            _visibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            CloseModal();
            GenerateCalendar();
        }

        public void Hide()
        {
            _isVisible = false;
            _screen.style.display = DisplayStyle.None;
            CloseModal();
        }

        public bool HandleBack()
        {
            if (!_isVisible)
                return false;

            if (_isModalOpen)
            {
                CloseModal();
                return true;
            }

            ReturnHome();
            return true;
        }

        public void Dispose()
        {
            _backButton.clicked -= ReturnHome;
            _playTodayButton.clicked -= OpenToday;
            _previousMonthButton.clicked -= ShowPreviousMonth;
            _nextMonthButton.clicked -= ShowNextMonth;
            _modalDismissButton.clicked -= CloseModal;
            _modalCloseButton.clicked -= CloseModal;
            _easyButton.clicked -= StartEasy;
            _mediumButton.clicked -= StartMedium;
            _hardButton.clicked -= StartHard;
        }

        private static T RequireElement<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException($"Daily UI element '{name}' was not found.");

            return element;
        }

        private void ReturnHome()
        {
            CloseModal();
            _returnHome?.Invoke();
        }

        private void OpenToday()
        {
            if (!IsDailyDateSelectable(DateTime.Today))
                return;

            OpenDifficultyModal(DateTime.Today);
        }

        private void ShowPreviousMonth()
        {
            DateTime firstAvailableMonth = GetFirstAvailableMonth();
            if (_visibleMonth <= firstAvailableMonth)
                return;

            _visibleMonth = _visibleMonth.AddMonths(-1);
            if (_visibleMonth < firstAvailableMonth)
                _visibleMonth = firstAvailableMonth;

            GenerateCalendar();
        }

        private void ShowNextMonth()
        {
            DateTime currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            if (_visibleMonth >= currentMonth)
                return;

            _visibleMonth = _visibleMonth.AddMonths(1);
            GenerateCalendar();
        }

        private void GenerateCalendar()
        {
            DateTime firstAvailableMonth = GetFirstAvailableMonth();
            if (_visibleMonth < firstAvailableMonth)
                _visibleMonth = firstAvailableMonth;

            _calendarGrid.Clear();
            _monthTitleLabel.text = _visibleMonth
                .ToString("MMMM yyyy");
            _monthReferenceLabel.text =
                $"LOG {_visibleMonth:yyyy-MM}";

            DateTime today = DateTime.Today;
            DateTime firstDay = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
            int leadingBlanks = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_visibleMonth.Year, _visibleMonth.Month);

            int rowCount = Mathf.CeilToInt((leadingBlanks + daysInMonth) / 7f);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = new VisualElement();
                row.AddToClassList("daily-calendar-row");

                for (int columnIndex = 0; columnIndex < 7; columnIndex++)
                {
                    int slotIndex = rowIndex * 7 + columnIndex;
                    int day = slotIndex - leadingBlanks + 1;

                    if (day < 1 || day > daysInMonth)
                    {
                        var blank = new VisualElement();
                        blank.AddToClassList("daily-calendar-cell");
                        blank.AddToClassList("daily-calendar-blank");
                        row.Add(blank);
                        continue;
                    }

                    DateTime date = new DateTime(_visibleMonth.Year, _visibleMonth.Month, day);
                    row.Add(CreateDayButton(date, today));
                }

                _calendarGrid.Add(row);
            }

            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
            _previousMonthButton.SetEnabled(
                _visibleMonth > firstAvailableMonth);
            _nextMonthButton.SetEnabled(_visibleMonth < currentMonth);
            RefreshMonthCompletion(today, daysInMonth);
        }

        private void RefreshMonthCompletion(DateTime today, int daysInMonth)
        {
            int availableCount = 0;
            int approvedCount = 0;

            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime date = new DateTime(
                    _visibleMonth.Year,
                    _visibleMonth.Month,
                    day);
                if (date > today || !_availableDailyDates.Contains(date))
                    continue;

                availableCount++;
                if (Progression.IsDailyDateFullyCompleted(date))
                    approvedCount++;
            }

            _monthCompletedLabel.text =
                $"{approvedCount} / {availableCount}";
        }

        private static Button CreateBaseDayButton(DateTime date)
        {
            var button = new Button
            {
                name = $"daily-day-{date:yyyy-MM-dd}"
            };
            button.AddToClassList("daily-calendar-cell");
            button.AddToClassList("daily-day-button");

            var dayNumber = new Label(date.Day.ToString());
            dayNumber.AddToClassList("daily-day-number");
            button.Add(dayNumber);
            return button;
        }

        private Button CreateDayButton(DateTime date, DateTime today)
        {
            Button button = CreateBaseDayButton(date);

            bool hasPuzzles = _availableDailyDates.Contains(date.Date);
            bool selectable = date <= today && hasPuzzles;

            bool easyCompleted =
                hasPuzzles &&
                Progression.IsDailyCompleted(date, "Easy");

            bool mediumCompleted =
                hasPuzzles &&
                Progression.IsDailyCompleted(date, "Medium");

            bool hardCompleted =
                hasPuzzles &&
                Progression.IsDailyCompleted(date, "Hard");

            bool allCompleted =
                easyCompleted &&
                mediumCompleted &&
                hardCompleted;

            if (!selectable)
            {
                button.AddToClassList(
                    date > today
                        ? "daily-day-future"
                        : "daily-day-unavailable");
                button.SetEnabled(false);
            }
            else
            {
                DateTime clickedDate = date;
                button.clicked += () => OpenDifficultyModal(clickedDate);
            }

            if (date == today && hasPuzzles)
                button.AddToClassList("daily-day-today");

            if (hasPuzzles)
            {
                var statusRow = new VisualElement();
                statusRow.AddToClassList("daily-day-status-row");
                AddDifficultyMark(statusRow, "E", easyCompleted);
                AddDifficultyMark(statusRow, "M", mediumCompleted);
                AddDifficultyMark(statusRow, "H", hardCompleted);
                button.Add(statusRow);
            }

            if (allCompleted)
            {
                button.AddToClassList("daily-day-completed");
                var approvedMark = new Label("APPROVED");
                approvedMark.AddToClassList("daily-day-approved");
                button.Add(approvedMark);
            }
            return button;
        }

        private static void AddDifficultyMark(
            VisualElement row,
            string label,
            bool completed)
        {
            var mark = new Label(label);
            mark.AddToClassList("daily-day-status");
            mark.AddToClassList(
                completed
                    ? "daily-day-status-complete"
                    : "daily-day-status-pending");
            row.Add(mark);
        }

        private void OpenDifficultyModal(DateTime date)
        {
            if (!IsDailyDateSelectable(date))
                return;

            _selectedDate = date.Date;
            _modalTitleLabel.text = _selectedDate == DateTime.Today
                ? "Today's puzzles"
                : "Daily puzzles";
            _modalDateLabel.text = _selectedDate
                .ToString("MMMM d, yyyy");

            RefreshDifficultyButton(_easyButton, "Easy");
            RefreshDifficultyButton(_mediumButton, "Medium");
            RefreshDifficultyButton(_hardButton, "Hard");

            _isModalOpen = true;
            _difficultyModal.style.display = DisplayStyle.Flex;
        }

        private void CloseModal()
        {
            _isModalOpen = false;
            _difficultyModal.style.display = DisplayStyle.None;
        }

        private void RefreshDifficultyButton(Button button, string difficulty)
        {
            bool completed = Progression.IsDailyCompleted(
                _selectedDate,
                difficulty);
            Label statusLabel = button.Q<Label>(
                className: "daily-difficulty-status");
            Label actionLabel = button.Q<Label>(
                className: "daily-difficulty-action");

            if (statusLabel != null)
                statusLabel.text = completed ? "Completed" : "Ready to play";
            if (actionLabel != null)
                actionLabel.text = completed ? "Replay" : "Play";

            button.EnableInClassList(
                "daily-difficulty-complete",
                completed);
        }

        private void StartEasy() => StartDailyPuzzle("Easy");
        private void StartMedium() => StartDailyPuzzle("Medium");
        private void StartHard() => StartDailyPuzzle("Hard");

        private void StartDailyPuzzle(string difficulty)
        {
            if (!IsDailyDateSelectable(_selectedDate))
            {
                Debug.LogWarning(
                    $"No daily puzzles are available for " +
                    $"{_selectedDate:yyyy-MM-dd}.");
                return;
            }

            string puzzleId = $"Daily_{_selectedDate:yyyy_MM_dd}_{difficulty}";
            string packPath = $"Daily/{_selectedDate:yyyy}/Daily_{_selectedDate:yyyy}_{difficulty}";

            TextAsset jsonFile = Resources.Load<TextAsset>($"Puzzles/{packPath}");
            if (jsonFile == null)
            {
                Debug.LogError($"Daily puzzle JSON not found at Resources/Puzzles/{packPath}.json");
                return;
            }

            GameSession.Mode = MenuMode.Daily;
            GameSession.DailyKey = GameSession.DateKey(_selectedDate);
            GameSession.PackPath = packPath;
            GameSession.LevelIndex = _selectedDate.Day;
            GameSession.SetPuzzle(puzzleId);

            PlayerPrefs.Save();
            SceneManager.LoadScene("Gameplay");
        }

        private bool IsDailyDateSelectable(DateTime date)
        {
            return date.Date <= DateTime.Today &&
                _availableDailyDates.Contains(date.Date);
        }

        private DateTime GetFirstAvailableMonth()
        {
            return new DateTime(
                _firstAvailableDate.Year,
                _firstAvailableDate.Month,
                1);
        }

        private static HashSet<DateTime> LoadAvailableDailyDates(
            out DateTime firstAvailableDate)
        {
            var availabilityByDate = new Dictionary<DateTime, int>();
            TextAsset[] assets =
                Resources.LoadAll<TextAsset>("Puzzles/Daily");

            for (int assetIndex = 0;
                 assetIndex < assets.Length;
                 assetIndex++)
            {
                TextAsset asset = assets[assetIndex];
                if (asset == null ||
                    asset.text.IndexOf(
                        "puzzles",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                PuzzlePackData pack;
                try
                {
                    pack = JsonUtility.FromJson<PuzzlePackData>(
                        asset.text);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Could not read daily puzzle pack " +
                        $"'{asset.name}': {exception.Message}");
                    continue;
                }

                if (pack?.puzzles == null)
                    continue;

                for (int puzzleIndex = 0;
                     puzzleIndex < pack.puzzles.Length;
                     puzzleIndex++)
                {
                    PuzzleEntry puzzle = pack.puzzles[puzzleIndex];
                    if (!TryReadDailyPuzzleAvailability(
                            puzzle?.id,
                            out DateTime date,
                            out int difficultyAvailability))
                    {
                        continue;
                    }

                    availabilityByDate.TryGetValue(
                        date,
                        out int currentAvailability);
                    availabilityByDate[date] =
                        currentAvailability | difficultyAvailability;
                }
            }

            var availableDates = new HashSet<DateTime>();
            firstAvailableDate = DateTime.Today;
            bool foundFirstDate = false;

            foreach (KeyValuePair<DateTime, int> pair in availabilityByDate)
            {
                if (pair.Value != AllDifficultiesAvailable)
                    continue;

                availableDates.Add(pair.Key);
                if (!foundFirstDate || pair.Key < firstAvailableDate)
                {
                    firstAvailableDate = pair.Key;
                    foundFirstDate = true;
                }
            }

            if (!foundFirstDate)
            {
                Debug.LogWarning(
                    "No dates with Easy, Medium, and Hard daily puzzles " +
                    "were found.");
            }

            return availableDates;
        }

        private static bool TryReadDailyPuzzleAvailability(
            string puzzleId,
            out DateTime date,
            out int difficultyAvailability)
        {
            date = default;
            difficultyAvailability = 0;
            if (string.IsNullOrWhiteSpace(puzzleId))
                return false;

            string[] parts = puzzleId.Split('_');
            if (parts.Length != 5 ||
                !parts[0].Equals(
                    "Daily",
                    StringComparison.OrdinalIgnoreCase) ||
                !DateTime.TryParseExact(
                    $"{parts[1]}_{parts[2]}_{parts[3]}",
                    "yyyy_MM_dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out date))
            {
                return false;
            }

            if (parts[4].Equals(
                    "Easy",
                    StringComparison.OrdinalIgnoreCase))
            {
                difficultyAvailability = EasyAvailability;
            }
            else if (parts[4].Equals(
                         "Medium",
                         StringComparison.OrdinalIgnoreCase))
            {
                difficultyAvailability = MediumAvailability;
            }
            else if (parts[4].Equals(
                         "Hard",
                         StringComparison.OrdinalIgnoreCase))
            {
                difficultyAvailability = HardAvailability;
            }

            return difficultyAvailability != 0;
        }
    }
}
