using UnityEngine;
using UnityEngine.UI;
using Shikaku.Logic;
using System.Collections.Generic;
using Shikaku.Menu;
using UnityEngine.SceneManagement;
using Shikaku.Settings;

namespace Shikaku.UI
{
    public enum BoardInputAction
    {
        Select,
        DragOverwrite,
        Paint,
        DragPaint,
        EraseCell,
        EraseRegion,
        IllegalMove
    }

    public class BoardController : MonoBehaviour
    {
        public System.Action PuzzleSolved;
        public event System.Action<BoardInputAction, int> InputPerformed;
        public event System.Action BoardTouched;

        [Header("Scene refs")]
        [SerializeField] private RectTransform boardPanel;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private GameObject cellPrefab;

        // NEW: optional, but recommended (drag BoardContainer here)
        // If left null, we fall back to boardPanel.parent.
        [SerializeField] private RectTransform boardContainer;

        [Header("Board Surface")]
        [SerializeField, Min(0f)] private float boardTrayMargin = 6f;
        [SerializeField] private Color boardTrayColor =
            new Color32(224, 216, 194, 245);
        [SerializeField] private Color boardEdgeColor =
            new Color32(190, 180, 156, 255);
        [SerializeField] private Color boardShadowColor =
            new Color32(190, 180, 156, 255);
        [SerializeField] private Color darkBoardTrayColor =
            new Color32(48, 45, 40, 255);
        [SerializeField] private Color darkBoardEdgeColor =
            new Color32(91, 83, 71, 255);
        [SerializeField] private Color darkBoardShadowColor =
            new Color32(91, 83, 71, 255);

        private RectTransform _boardTray;
        private AspectRatioFitter _boardAspectRatioFitter;
        private static Sprite _hudPanelSprite;

        [Header("Palette")]
        [SerializeField] private PuzzlePalette palette;
        public PuzzlePalette Palette => palette;

        [Header("Puzzle Size")]
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;

        [Header("Layout")]
        [SerializeField] private float spacing = 8f;
        [SerializeField] private float padding = 12f;

        [Header("Responsive Board Layout")]
        [SerializeField, Min(1f)] private float minimumBoardLongSide = 650f;
        [SerializeField, Min(0f)] private float boardGrowthPerSizeStep = 95f;
        [SerializeField, Min(1f)] private float maximumBoardLongSide = 1080f;
        [SerializeField, Min(0f)] private float boardSlotMargin = 12f;

        [Header("Puzzle Load")]
        [SerializeField] private string puzzleId = "debug_two4s";


        [Header("Dev Override (optional)")]
        [SerializeField] private bool devOverrideEnabled = false;
        [SerializeField] private string devPuzzleIdOverride = "";

        [Header("Dev Puzzle Order (used when Dev Override is enabled)")]
        [SerializeField] private string[] devPuzzleOrder = { "test_4x4_easy", "debug_two4s" };

        [Header("Audio")]
        [SerializeField] private SfxManager sfx;

        [Header("Hint FX")]
        [SerializeField] private float hintFlashSpeed = 4.5f;   // higher = faster
        [SerializeField] private float hintAlphaMin = 0.35f;    // more transparent
        [SerializeField] private float hintAlphaMax = 0.75f;    // less transparent at peak

        [Header("Input Behavior")]
        [SerializeField] private bool allowDragOverwrite = true;

        [Header("Illegal Move Feedback")]
        [SerializeField] private bool illegalMoveHaptics = true;
        [SerializeField] private float illegalMoveHapticCooldown = 0.2f;

        private float _lastIllegalMoveHapticTime = -999f;



        private int _puzzleIndex = 0;
        private bool _alreadySolved = false;
        private bool _inputLocked;

        private List<string> _sessionOrder;

        private PuzzleModel _model;
        private CellView[] _cells;
        private readonly Vector3[] _tutorialCellWorldCorners = new Vector3[4];
        private System.Func<BoardInputAction, int, bool>
            _tutorialInputFilter;
        private readonly HashSet<int> _tutorialHighlights = new HashSet<int>();

        // Prevent spam resizing loops
        private bool _gridBuilt = false;
        private bool _useEdgeToEdgeBoardSlot;

        private int[] _solutionValues;

        private int _hintIndex = -1;
        private HintKind _hintKind = HintKind.None;
        private int _hintTargetValue = 0;

        private enum HintKind { None, WrongFilled, EmptyCell }

        private bool[] _cellExists;   // true = normal cell, false = hole

        private static PuzzleData LoadPuzzleDataSmart(
            string id,
            string packPathOverride = null)
        {
            if (string.IsNullOrEmpty(id)) return null;

            string entryId = id.Trim();
            string embeddedPackPath = null;

            // Accept the previous combined value for developer convenience.
            int pipe = id.LastIndexOf('|');
            if (pipe > 0 && pipe < id.Length - 1)
            {
                embeddedPackPath = id.Substring(0, pipe);
                entryId = id.Substring(pipe + 1);
            }

            // Keep the old indexed developer format available.
            int hash = id.LastIndexOf('#');
            if (hash > 0 && hash < id.Length - 1)
            {
                string packPath = id.Substring(0, hash);
                string idxStr = id.Substring(hash + 1);

                if (int.TryParse(idxStr, out int idx))
                    return PuzzleLoader.LoadFromResourcesPackIndex(packPath, idx);
            }

            string packPathToUse = !string.IsNullOrEmpty(packPathOverride)
                ? packPathOverride
                : !string.IsNullOrEmpty(embeddedPackPath)
                    ? embeddedPackPath
                    : GameSession.PackPath;

            if (!string.IsNullOrEmpty(packPathToUse))
            {
                PuzzleData packed =
                    PuzzleLoader.LoadFromResourcesPackPuzzleId(
                        packPathToUse,
                        entryId);
                if (packed != null)
                    return packed;
            }

            // Standalone developer puzzle.
            return PuzzleLoader.LoadFromResources(entryId);
        }

        private void BuildCellExistsFromMask(PuzzleData data)
        {
            int total = width * height;
            _cellExists = new bool[total];

            // Default: all cells exist
            for (int i = 0; i < total; i++) _cellExists[i] = true;

            if (data == null) return;
            if (string.IsNullOrWhiteSpace(data.mask)) return;

            // Accept formats like:
            // "11111/11011/11111" or "11111\n11011\n11111"
            // We treat '0' as hole, '1' as exists. Ignore other chars.
            List<char> bits = new List<char>(total);
            foreach (char c in data.mask)
            {
                if (c == '0' || c == '1') bits.Add(c);
            }

            if (bits.Count != total)
            {
                Debug.LogWarning($"Mask size mismatch for {data.id}. Expected {total} bits, got {bits.Count}. Ignoring mask.");
                return;
            }

            for (int i = 0; i < total; i++)
                _cellExists[i] = (bits[i] == '1');
        }

        private bool CellExists(int idx)
        {
            // If no mask loaded yet, assume normal rectangular board
            if (_cellExists == null) return true;
            if (idx < 0 || idx >= _cellExists.Length) return false;
            return _cellExists[idx];
        }

        private void ApplyHoleVisual(GameObject cellGO, bool isHole)
        {
            // Prefer "invisible + no raycasts" over SetActive(false),
            // because GridLayoutGroup will collapse missing children.
            var btn = cellGO.GetComponent<Button>();
            if (btn != null) btn.interactable = !isHole;

            // CanvasGroup lets us disable raycasts cleanly
            var cg = cellGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = cellGO.AddComponent<CanvasGroup>();

            if (isHole)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
            else
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        private void Awake()
        {
            ThemeManager.EnsureInitialized();
            ThemeManager.Changed += OnThemeChanged;

            // Choose which puzzle to load (dev override wins if enabled)
            if (devOverrideEnabled && !string.IsNullOrWhiteSpace(devPuzzleIdOverride))
            {
                GameSession.SetPuzzle(devPuzzleIdOverride.Trim());
                puzzleId = GameSession.GetPuzzleId();
                PlayerPrefs.Save();
            }
            else if (PlayerPrefs.HasKey("puzzleId"))
            {
                puzzleId = GameSession.GetPuzzleId();
            }

            if (boardContainer == null && boardPanel != null)
                boardContainer = boardPanel.parent as RectTransform;

            ConfigureResponsiveBoardPanel();
            EnsureBoardTray();

            PuzzleData data = LoadPuzzleDataSmart(puzzleId);

            if (data == null)
            {
                // fallback to old hardcoded setup if json missing
                int[] given = new int[width * height];
                given[0] = 4;
                given[3] = 4;
                given[10] = 3;
                given[27] = 5;

                _model = new PuzzleModel(width, height, given);
                _cellExists = null; // ✅ ensure no previous mask sticks
            }
            else
            {
                puzzleId = PuzzleProgressStore.NormalizePuzzleId(data.id);
                GameSession.SetPuzzle(puzzleId);
                width = data.width;
                height = data.height;

                _model = new PuzzleModel(width, height, null);
                // ✅ build mask BEFORE model load triggers BoardReset
                BuildCellExistsFromMask(data);
                _model.LoadPuzzle(data);
            }

            _solutionValues = (data.solution != null) ? data.solution.values : null;

            if (_solutionValues != null && _solutionValues.Length != width * height)
            {
                Debug.LogWarning($"Solution length mismatch for {data.id}. Expected {width * height}, got {_solutionValues.Length}. Disabling hints.");
                _solutionValues = null;
            }

            _model.CellChanged += OnCellChanged;
            _model.SelectionChanged += RenderAll;
            _model.BoardReset += OnBoardReset;
            AppSettings.EnsureLoaded();
            AppSettings.Changed += OnAnySettingChanged;

            BuildGrid();
            _gridBuilt = true;

            BuildSessionOrderAndIndex();
            GameAnalytics.PuzzleStarted(width, height);
        }

        public void ContinueAfterSolved()
        {
            if (sfx != null)
                sfx.PlayButton();

            LoadNextPuzzle();
        }

        public void ContinueAfterSolvedSameDailyDifficulty()
        {
            if (sfx != null)
                sfx.PlayButton();

            if (GameSession.Mode != MenuMode.Daily)
                return;

            LoadNextDailySameDifficulty();
        }

        public bool HasNextDailyPuzzleAvailable()
        {
            if (GameSession.Mode != MenuMode.Daily ||
                !TryGetCurrentDailyPuzzle(
                    out System.DateTime currentDate,
                    out string currentDifficulty))
            {
                return false;
            }

            System.DateTime nextDate = currentDate;
            string nextDifficulty;

            switch (currentDifficulty.ToLowerInvariant())
            {
                case "easy":
                    nextDifficulty = "Medium";
                    break;
                case "medium":
                    nextDifficulty = "Hard";
                    break;
                case "hard":
                    nextDate = currentDate.AddDays(1);
                    nextDifficulty = "Easy";
                    break;
                default:
                    return false;
            }

            if (nextDate.Date > System.DateTime.Today)
                return false;

            string nextPuzzleEntryId =
                $"Daily_{nextDate:yyyy_MM_dd}_{nextDifficulty}";
            string nextPackPath =
                $"Daily/{nextDate:yyyy}/Daily_{nextDate:yyyy}_{nextDifficulty}";

            return LoadPuzzleDataSmart(
                nextPuzzleEntryId,
                nextPackPath) != null;
        }

        public void RefreshBoardLayout()
        {
            if (isActiveAndEnabled)
                StartCoroutine(EnsureSizedAndRendered());
        }

        private void BuildSessionOrderAndIndex()
        {
            _sessionOrder = null;

            // If dev override is enabled, we DO NOT use pack order.
            // Next will cycle devPuzzleOrder instead.
            if (devOverrideEnabled)
            {
                _puzzleIndex = System.Array.IndexOf(devPuzzleOrder, puzzleId);
                if (_puzzleIndex < 0) _puzzleIndex = 0;
                return;
            }

            // Normal behavior: if launched from a pack, use that pack's ids.
            var packPath = GameSession.PackPath;
            if (!string.IsNullOrEmpty(packPath))
            {
                var ids = PuzzleCatalog.GetPackPuzzleIds(packPath);
                if (ids != null && ids.Count > 0)
                {
                    _sessionOrder = new List<string>(ids.Count);
                    for (int i = 0; i < ids.Count; i++) _sessionOrder.Add(ids[i]);

                    int idxByLevel = GameSession.LevelIndex - 1;
                    if (idxByLevel >= 0 && idxByLevel < _sessionOrder.Count)
                        _puzzleIndex = idxByLevel;
                    else
                    {
                        int idx = _sessionOrder.IndexOf(puzzleId);
                        _puzzleIndex = (idx >= 0) ? idx : 0;
                    }
                    return;
                }
            }

            // If not in a pack and not dev override, fall back to devPuzzleOrder too (nice for testing).
            _puzzleIndex = System.Array.IndexOf(devPuzzleOrder, puzzleId);
            if (_puzzleIndex < 0) _puzzleIndex = 0;
        }

        private void OnBoardReset()
        {
            if (_cells == null || _cells.Length != width * height)
            {
                BuildGrid();
                _gridBuilt = true;
            }
            else
            {
                // ✅ same grid size: still update visuals based on new mask
                RefreshHoleVisuals();
            }
            StartCoroutine(EnsureSizedAndRendered());
        }

        private System.Collections.IEnumerator Start()
        {
            // IMPORTANT: do NOT size immediately. The layout often has 0x0 here.
            yield return EnsureSizedAndRendered();
        }

        /// <summary>
        /// Wait until the board has real dimensions, force layout rebuild, then size + render.
        /// This fixes the "boardPanel.rect is 0 at start" issue.
        /// </summary>
        private System.Collections.IEnumerator EnsureSizedAndRendered()
        {
            if (!_gridBuilt)
                yield break;

            yield return null;

            const int maxTries = 8;
            for (int i = 0; i < maxTries; i++)
            {
                ForceLayoutNow();

                if (HasValidBoardContainerRect())
                {
                    ApplyResponsiveBoardSizing();
                    ForceLayoutNow();

                    if (HasValidBoardRect())
                        break;
                }

                yield return new WaitForEndOfFrame();
            }

            ForceLayoutNow();
            if (!HasValidBoardContainerRect())
                yield break;

            ApplyResponsiveBoardSizing();
            ForceLayoutNow();

            if (!HasValidBoardRect())
                yield break;

            UpdateBoardTrayLayout();
            RenderAll();
        }

        private void ConfigureResponsiveBoardPanel()
        {
            if (boardPanel == null)
                return;

            _boardAspectRatioFitter = boardPanel.GetComponent<AspectRatioFitter>();
            if (_boardAspectRatioFitter != null)
                _boardAspectRatioFitter.enabled = false;

            boardPanel.anchorMin = new Vector2(0.5f, 0.5f);
            boardPanel.anchorMax = new Vector2(0.5f, 0.5f);
            boardPanel.pivot = new Vector2(0.5f, 0.5f);
            boardPanel.anchoredPosition = Vector2.zero;
        }

        private void ForceLayoutNow()
        {
            // Force UI to compute sizes immediately.
            Canvas.ForceUpdateCanvases();

            if (boardContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(boardContainer);

            if (boardPanel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(boardPanel);
        }

        private bool HasValidBoardContainerRect()
        {
            return boardContainer != null &&
                boardContainer.rect.width > 1f &&
                boardContainer.rect.height > 1f;
        }

        private bool HasValidBoardRect()
        {
            return boardPanel != null &&
                boardPanel.rect.width > 1f &&
                boardPanel.rect.height > 1f;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (grid == null || boardPanel == null)
                return;
            if (_cells == null || _cells.Length == 0)
                return;
            if (!HasValidBoardContainerRect())
                return;

            ApplyResponsiveBoardSizing();
            UpdateBoardTrayLayout();
            RenderAll();
        }

        private void EnsureBoardTray()
        {
            if (boardContainer == null || boardPanel == null)
                return;

            Transform existing = boardContainer.Find("BoardTray");
            if (existing != null)
            {
                _boardTray = existing as RectTransform;

                Transform legacyWell = existing.Find("BoardWell");
                if (legacyWell != null)
                    legacyWell.gameObject.SetActive(false);
            }

            if (_boardTray == null)
            {
                GameObject trayObject = new GameObject(
                    "BoardTray",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Shadow));
                trayObject.layer = boardPanel.gameObject.layer;
                _boardTray = trayObject.GetComponent<RectTransform>();
                _boardTray.SetParent(boardContainer, false);
            }

            Image trayImage = _boardTray.GetComponent<Image>();
            trayImage.sprite = GetHudPanelSprite();
            trayImage.type = Image.Type.Sliced;
            trayImage.color = ThemeManager.IsDark
                ? darkBoardTrayColor
                : boardTrayColor;
            trayImage.raycastTarget = false;

            Outline trayEdge = _boardTray.GetComponent<Outline>();
            if (trayEdge == null)
                trayEdge = _boardTray.gameObject.AddComponent<Outline>();

            trayEdge.effectColor = ThemeManager.IsDark
                ? darkBoardEdgeColor
                : boardEdgeColor;
            trayEdge.effectDistance = new Vector2(2f, -2f);
            trayEdge.useGraphicAlpha = true;

            Shadow trayShadow = _boardTray.GetComponent<Shadow>();
            trayShadow.effectColor = ThemeManager.IsDark
                ? darkBoardShadowColor
                : boardShadowColor;
            trayShadow.effectDistance = new Vector2(0f, -5f);
            trayShadow.useGraphicAlpha = true;


            _boardTray.SetSiblingIndex(boardPanel.GetSiblingIndex());
            boardPanel.SetAsLastSibling();
            UpdateBoardTrayLayout();
        }

        private static Sprite GetHudPanelSprite()
        {
            if (_hudPanelSprite != null)
                return _hudPanelSprite;

            const int size = 64;
            const int radius = 14;
            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Gameplay HUD Style Board Surface",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            float center = size * 0.5f;
            float innerExtent = center - radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(
                        Mathf.Abs(x + 0.5f - center) - innerExtent,
                        0f);
                    float dy = Mathf.Max(
                        Mathf.Abs(y + 0.5f - center) - innerExtent,
                        0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)Mathf.RoundToInt(
                        255f * Mathf.Clamp01(radius + 0.5f - distance));
                    pixels[y * size + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _hudPanelSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius),
                false);
            _hudPanelSprite.name = "Gameplay HUD Style Board Surface";
            _hudPanelSprite.hideFlags = HideFlags.HideAndDontSave;
            return _hudPanelSprite;
        }

        private void UpdateBoardTrayLayout()
        {
            if (_boardTray == null)
                EnsureBoardTray();

            if (_boardTray == null || boardPanel == null)
                return;

            _boardTray.anchorMin = boardPanel.anchorMin;
            _boardTray.anchorMax = boardPanel.anchorMax;
            _boardTray.pivot = boardPanel.pivot;
            _boardTray.anchoredPosition = boardPanel.anchoredPosition;
            _boardTray.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                boardPanel.rect.width + boardTrayMargin * 2f);
            _boardTray.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                boardPanel.rect.height + boardTrayMargin * 2f);
        }

        private void BuildGrid()
        {
            if (grid == null || boardPanel == null || cellPrefab == null) return;

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = width;

            for (int i = boardPanel.childCount - 1; i >= 0; i--)
                Destroy(boardPanel.GetChild(i).gameObject);

            _cells = new CellView[width * height];

            for (int i = 0; i < width * height; i++)
            {
                var go = Instantiate(cellPrefab, boardPanel);

                bool isHole = !CellExists(i);
                ApplyHoleVisual(go, isHole);

                var view = go.GetComponent<CellView>();
                view.Init(i, _model, this);
                view.SetTutorialHighlight(
                    _tutorialHighlights.Contains(i));
                _cells[i] = view;
            }
        }

        private void RefreshHoleVisuals()
        {
            if (_cells == null) return;

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == null) continue;

                bool isHole = !CellExists(i);
                ApplyHoleVisual(_cells[i].gameObject, isHole);
            }
        }

        public void SetEdgeToEdgeBoardLayout(bool enabled)
        {
            if (_useEdgeToEdgeBoardSlot == enabled)
                return;

            _useEdgeToEdgeBoardSlot = enabled;
            RefreshBoardLayout();
        }

        private float EffectiveBoardSlotMargin =>
            _useEdgeToEdgeBoardSlot ? 0f : boardSlotMargin;

        public Vector2 GetPreferredBoardSlotSize(
            float maximumWidth,
            float maximumHeight)
        {
            if (!TryCalculateResponsiveBoardLayout(
                    maximumWidth,
                    maximumHeight,
                    out Vector2 boardSize,
                    out _,
                    out _))
            {
                return Vector2.zero;
            }

            float outerInset = Mathf.Max(
                0f,
                EffectiveBoardSlotMargin + boardTrayMargin);

            return boardSize +
                new Vector2(outerInset * 2f, outerInset * 2f);
        }

        private bool TryCalculateResponsiveBoardLayout(
            float containerWidth,
            float containerHeight,
            out Vector2 boardSize,
            out float cellSize,
            out float adaptiveSpacing)
        {
            boardSize = Vector2.zero;
            cellSize = 0f;
            adaptiveSpacing = 0f;

            int columns = Mathf.Max(width, 1);
            int rows = Mathf.Max(height, 1);
            float outerInset = Mathf.Max(
                0f,
                EffectiveBoardSlotMargin + boardTrayMargin);
            float availableWidth = containerWidth - outerInset * 2f;
            float availableHeight = containerHeight - outerInset * 2f;
            if (availableWidth <= 1f || availableHeight <= 1f)
                return false;

            float approximateCellFit = Mathf.Min(
                availableWidth / columns,
                availableHeight / rows);
            adaptiveSpacing = Mathf.Clamp(
                approximateCellFit / 32f,
                0f,
                spacing);

            float horizontalFixedSize =
                padding * 2f + adaptiveSpacing * (columns - 1);
            float verticalFixedSize =
                padding * 2f + adaptiveSpacing * (rows - 1);
            float fitCellSize = Mathf.Min(
                (availableWidth - horizontalFixedSize) / columns,
                (availableHeight - verticalFixedSize) / rows);
            if (fitCellSize < 1f)
                return false;

            int longSideCellCount = Mathf.Max(columns, rows);
            float minimumLongSide = Mathf.Min(
                minimumBoardLongSide,
                maximumBoardLongSide);
            float maximumLongSide = Mathf.Max(
                minimumBoardLongSide,
                maximumBoardLongSide);
            int growthSteps = Mathf.Max(0, longSideCellCount - 3);
            float desiredLongSide = Mathf.Clamp(
                minimumLongSide + boardGrowthPerSizeStep * growthSteps,
                minimumLongSide,
                maximumLongSide);
            float desiredCellSize =
                (desiredLongSide - padding * 2f -
                    adaptiveSpacing * (longSideCellCount - 1)) /
                longSideCellCount;

            cellSize = Mathf.Max(
                1f,
                Mathf.Min(desiredCellSize, fitCellSize));

            boardSize = new Vector2(
                horizontalFixedSize + cellSize * columns,
                verticalFixedSize + cellSize * rows);
            return true;
        }

        private void ApplyResponsiveBoardSizing()
        {
            if (grid == null || boardPanel == null ||
                !HasValidBoardContainerRect())
            {
                return;
            }

            if (!TryCalculateResponsiveBoardLayout(
                    boardContainer.rect.width,
                    boardContainer.rect.height,
                    out Vector2 boardSize,
                    out float cellSize,
                    out float adaptiveSpacing))
            {
                return;
            }

            boardPanel.anchorMin = new Vector2(0.5f, 0.5f);
            boardPanel.anchorMax = new Vector2(0.5f, 0.5f);
            boardPanel.pivot = new Vector2(0.5f, 0.5f);
            boardPanel.anchoredPosition = Vector2.zero;
            boardPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                boardSize.x);
            boardPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                boardSize.y);

            grid.spacing = new Vector2(
                adaptiveSpacing,
                adaptiveSpacing);
            int roundedPadding = Mathf.RoundToInt(padding);
            grid.padding = new RectOffset(
                roundedPadding,
                roundedPadding,
                roundedPadding,
                roundedPadding);
            grid.cellSize = new Vector2(cellSize, cellSize);
        }
        private void RenderAll()
        {
            if (_cells == null) return;
            for (int i = 0; i < _cells.Length; i++)
                _cells[i].Render();
        }

        private void OnCellChanged(int cellIndex)
        {
            if (_cells == null || cellIndex < 0 || cellIndex >= _cells.Length) return;
            _cells[cellIndex].Render();
        }

        public void ClearSelection() => _model?.ClearSelection();

        public void NotifyBoardTouched()
        {
            if (!_inputLocked)
                BoardTouched?.Invoke();
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;

            if (locked)
            {
                _model?.ClearSelection();
                CellView.CancelPointerInput();
            }
        }

        public void SetTutorialInputFilter(
            System.Func<BoardInputAction, int, bool> filter)
        {
            _tutorialInputFilter = filter;
            CellView.CancelPointerInput();
            _model?.ClearSelection();
        }

        public void ClearTutorialInputFilter()
        {
            SetTutorialInputFilter(null);
        }

        public void SetTutorialHighlights(params int[] indices)
        {
            _tutorialHighlights.Clear();

            if (indices != null)
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    int index = indices[i];
                    if (index >= 0 && index < width * height)
                        _tutorialHighlights.Add(index);
                }
            }

            if (_cells == null)
                return;

            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i]?.SetTutorialHighlight(
                    _tutorialHighlights.Contains(i));
            }
        }

        private bool AllowsInput(BoardInputAction action, int cellIndex)
        {
            return _tutorialInputFilter == null ||
                   _tutorialInputFilter(action, cellIndex);
        }

        public void OnPointerDownCell(int cellIndex)
        {

            if (_inputLocked || !CellExists(cellIndex))
                return;

            if (_model.Value[cellIndex] != 0)
            {
                // Some filled regions have no meaningful interaction:
                // - regions completed entirely by the original givens
                // - regions completed by a hint
                if (!_model.CanSelectCell(cellIndex))
                    return;

                if (!AllowsInput(BoardInputAction.Select, cellIndex))
                    return;

                _model.SelectCell(cellIndex);

                InputPerformed?.Invoke(
                    BoardInputAction.Select,
                    cellIndex);

                return;
            }

            if (_model.SelectedCellIndex == -1)
                return;

            if (_model.CanPaintCell(cellIndex))
            {
                if (!AllowsInput(BoardInputAction.Paint, cellIndex))
                    return;

                _model.TryApplyToCell(cellIndex);

                if (sfx != null)
                    sfx.PlayDraw(isDrag: false);

                InputPerformed?.Invoke(
                    BoardInputAction.Paint,
                    cellIndex);
                CheckSolved();
            }
            else
            {
                bool overflowed =
                    _model.WouldPaintOverflow(cellIndex);

                // Preserve the current behavior of clearing selection.
                _model.ClearSelection();

                if (overflowed)
                {
                    if (!AllowsInput(BoardInputAction.IllegalMove, cellIndex))
                        return;

                    ShowIllegalMoveFeedback(cellIndex);
                    InputPerformed?.Invoke(
                        BoardInputAction.IllegalMove,
                        cellIndex);
                }
            }
        }

        public void OnPointerEnterCell(int cellIndex)
        {
            if (_inputLocked || !CellExists(cellIndex))
                return;

            if (_model.SelectedCellIndex == -1)
                return;

            if (allowDragOverwrite &&
                _model.IsOverwriteCandidate(cellIndex))
            {
                PerformDragOverwrite(cellIndex);
                return;
            }

            if (!_model.CanPaintCell(cellIndex))
            {
                if (_model.WouldPaintOverflow(cellIndex))
                {
                    if (!AllowsInput(BoardInputAction.IllegalMove, cellIndex))
                        return;

                    ShowIllegalMoveFeedback(cellIndex);
                    InputPerformed?.Invoke(
                        BoardInputAction.IllegalMove,
                        cellIndex);
                }

                return;
            }

            if (!AllowsInput(BoardInputAction.DragPaint, cellIndex))
                return;

            _model.TryApplyToCell(cellIndex);

            if (sfx != null)
                sfx.PlayDraw(isDrag: true);

            InputPerformed?.Invoke(
                BoardInputAction.DragPaint,
                cellIndex);
            CheckSolved();
        }

        private bool PerformDragOverwrite(int cellIndex)
        {
            if (_inputLocked || !allowDragOverwrite ||
                !CellExists(cellIndex))
            {
                return false;
            }

            if (_model.CanOverwriteCell(cellIndex))
            {
                if (!AllowsInput(BoardInputAction.DragOverwrite, cellIndex) ||
                    !_model.TryOverwriteCell(cellIndex))
                {
                    return false;
                }

                if (sfx != null)
                    sfx.PlayDraw(isDrag: true);

                InputPerformed?.Invoke(
                    BoardInputAction.DragOverwrite,
                    cellIndex);
                CheckSolved();
                return true;
            }

            if (!_model.WouldOverwriteOverflow(cellIndex))
                return false;

            // Reject the mutation while preserving the selected region, then
            // flash the filled target before restoring its original color.

            if (!AllowsInput(BoardInputAction.IllegalMove, cellIndex))
                return false;

            ShowIllegalMoveFeedback(cellIndex);
            InputPerformed?.Invoke(
                BoardInputAction.IllegalMove,
                cellIndex);
            return true;
        }

        public void OnDoubleTapCell(int cellIndex)
        {
            if (_inputLocked || !CellExists(cellIndex)) return;
            if (_model.Value[cellIndex] == 0) return;

            // Hint-completed regions and regions already complete entirely
            // from givens have no editable content.
            if (_model.IsHintLockedCell(cellIndex) ||
                _model.IsGivenOnlyCompleteComponentAt(cellIndex))
            {
                return;
            }

            BoardInputAction action = _model.IsAnchorCell(cellIndex)
                        ? BoardInputAction.EraseRegion
                : BoardInputAction.EraseCell;

            if (!AllowsInput(action, cellIndex))
                return;

            bool erasedSomething;

            if (_model.IsAnchorCell(cellIndex))
            {
                erasedSomething = _model.TryEraseRegionFromAnchor(cellIndex);
            }
            else
            {
                _model.TryRemoveFromCell(cellIndex);
                erasedSomething = true;
            }

            if (erasedSomething)
            {
                if (sfx != null)
                    sfx.PlayErase();

                InputPerformed?.Invoke(action, cellIndex);
                CheckSolved();
            }
        }

        public int SelectedCellIndex => _model.SelectedCellIndex;
        public int SelectedComponentId => _model.SelectedComponentId;

        public bool IsAnchorCell(int idx) => _model.IsAnchorCell(idx);
        public int GivenNumberAt(int idx) => _model.GivenNumber[idx];

        public int ValueAt(int idx) => _model.Value[idx];

        public bool OverwriteTutorialCell(
            int sourceCellIndex,
            int targetCellIndex,
            bool playFeedback = true,
            bool checkSolved = true)
        {
            if (_model == null)
                return false;

            if (!_model.CanSelectCell(sourceCellIndex))
                return false;

            int sourceValue = _model.Value[sourceCellIndex];
            _model.SelectCell(sourceCellIndex);

            if (!_model.CanOverwriteCell(targetCellIndex))
            {
                _model.ClearSelection();
                return false;
            }

            bool overwritten =
                _model.TryOverwriteCell(targetCellIndex);

            if (!overwritten)
                return false;

            if (playFeedback && sfx != null)
                sfx.PlayDraw(isDrag: true);

            if (checkSolved)
                CheckSolved();

            return _model.Value[targetCellIndex] == sourceValue;
        }

        public bool RestoreTutorialCell(
            int sourceCellIndex,
            int targetCellIndex,
            bool playFeedback = true,
            bool checkSolved = true)
        {
            if (_model == null)
                return false;

            // Select the existing incomplete region.
            if (!_model.CanSelectCell(sourceCellIndex))
                return false;

            _model.SelectCell(sourceCellIndex);

            // Make sure the erased square can actually be restored.
            if (!_model.CanPaintCell(targetCellIndex))
            {
                _model.ClearSelection();
                return false;
            }

            // Restore the erased square without treating it as player input.
            _model.TryApplyToCell(targetCellIndex);

            if (playFeedback && sfx != null)
                sfx.PlayDraw(isDrag: false);

            if (checkSolved)
                CheckSolved();

            return _model.Value[targetCellIndex] != 0;
        }

        public bool ResetTutorialPuzzleState()
        {
            if (string.IsNullOrEmpty(puzzleId))
                return false;

            _tutorialInputFilter = null;
            _tutorialHighlights.Clear();
            _inputLocked = false;
            CellView.CancelPointerInput();
            LoadPuzzleById(puzzleId);

            return _model != null;
        }

        public bool TryGetCellScreenRect(int cellIndex, out Rect screenRect)
        {
            screenRect = default;

            if (_cells == null ||
                cellIndex < 0 ||
                cellIndex >= _cells.Length ||
                _cells[cellIndex] == null)
            {
                return false;
            }

            RectTransform cellRect =
                _cells[cellIndex].transform as RectTransform;
            if (cellRect == null)
                return false;

            cellRect.GetWorldCorners(_tutorialCellWorldCorners);

            Canvas canvas = cellRect.GetComponentInParent<Canvas>();
            Camera camera =
                canvas != null &&
                canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;

            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(
                camera,
                _tutorialCellWorldCorners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(
                camera,
                _tutorialCellWorldCorners[2]);

            screenRect = Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y));

            return screenRect.width > 1f && screenRect.height > 1f;
        }

        public int ComponentIdAt(int idx) => _model.GetComponentIdAt(idx);
        public int ComponentSizeAt(int idx) => _model.GetComponentSizeAt(idx);
        public bool IsComponentCompleteAt(int idx) => _model.IsComponentCompleteAt(idx);

        public int Width => width;
        public int Height => height;
        public bool IsPlayableCell(int index) => CellExists(index);
        public bool InputLocked => _inputLocked;

        public bool IsPuzzleSolved =>
            _model != null && _model.IsSolved();

        public bool IsBoardCompletelyFilled
        {
            get
            {
                if (_model == null || width <= 0 || height <= 0)
                    return false;

                bool foundPlayableCell = false;
                int total = width * height;
                for (int i = 0; i < total; i++)
                {
                    if (!CellExists(i))
                        continue;

                    foundPlayableCell = true;
                    if (_model.Value[i] == 0)
                        return false;
                }

                return foundPlayableCell;
            }
        }

        private void LoadPuzzleById(string id)
        {
            PuzzleData data = LoadPuzzleDataSmart(id);
            if (data == null) return;

            puzzleId = PuzzleProgressStore.NormalizePuzzleId(data.id);
            GameSession.SetPuzzle(puzzleId);
            width = data.width;
            height = data.height;

            // ✅ build mask BEFORE model load triggers BoardReset -> BuildGrid()
            BuildCellExistsFromMask(data);

            _model.LoadPuzzle(data);
            _alreadySolved = false;
            _inputLocked = false;

            // ✅ make sure visuals match the new mask even if grid didn't rebuild
            RefreshHoleVisuals();

            _solutionValues = (data.solution != null) ? data.solution.values : null;

            if (_solutionValues != null && _solutionValues.Length != width * height)
            {
                Debug.LogWarning($"Solution length mismatch for {data.id}. Expected {width * height}, got {_solutionValues.Length}. Disabling hints.");
                _solutionValues = null;
            }

            // Rebuild size after load (since board rect may change)
            StartCoroutine(EnsureSizedAndRendered());
            GameAnalytics.PuzzleStarted(width, height);
        }

        private void LoadNextPuzzle()
        {
            // Daily puzzles have their own sequence:
            // Easy -> Medium -> Hard -> next day's Easy.
            if (GameSession.Mode == MenuMode.Daily)
            {
                LoadNextDailyPuzzle();
                return;
            }

            // Time Trial: next should be another random puzzle (no pack end -> menu)
            if (GameSession.Mode == MenuMode.TimeTrial)
            {

                string nextId = PickRandomTimeTrialPuzzleId();
                if (string.IsNullOrEmpty(nextId)) return;

                GameSession.SetPuzzle(nextId);
                PlayerPrefs.Save();

                LoadPuzzleById(nextId);
                FindObjectOfType<GameplayHUD>()?.ForceRefreshHeader();
                FindObjectOfType<GameplayHUD>()?.ResumeAfterSolvedPanel(); // you'll add this below
                return;
            }


            if (_sessionOrder != null && _sessionOrder.Count > 0)
            {
                int nextIndex = _puzzleIndex + 1;

                if (nextIndex >= _sessionOrder.Count)
                {
                    if (GameSession.Mode == MenuMode.Story)
                    {
                        if (TryLoadNextStoryChapter())
                            return;

                        PlayerPrefs.SetString(
                            HomeScreenController.StartScreenKey,
                            HomeScreenController.AdventureScreenId);
                        PlayerPrefs.SetString(
                            HomeScreenController.AdventurePackKey,
                            GameSession.PackPath);
                        PlayerPrefs.Save();
                        SceneManager.LoadScene("HomeUI");
                        return;
                    }

                    if (GameSession.Mode == MenuMode.FreePlay)
                    {
                        PlayerPrefs.SetString(
                            HomeScreenController.StartScreenKey,
                            HomeScreenController.FreePlayScreenId);
                        PlayerPrefs.SetString(
                            HomeScreenController.FreePlayPackKey,
                            GameSession.PackPath);
                        PlayerPrefs.Save();
                        SceneManager.LoadScene("HomeUI");
                        return;
                    }

                    SceneManager.LoadScene("HomeUI");
                    return;
                }

                _puzzleIndex = nextIndex;
                string nextId = _sessionOrder[_puzzleIndex];

                GameSession.LevelIndex = _puzzleIndex + 1;
                GameSession.SetPuzzle(nextId);
                PlayerPrefs.Save();

                LoadPuzzleById(nextId);

                // ✅ NEW: refresh header after puzzle changes
                FindObjectOfType<GameplayHUD>()?.ForceRefreshHeader();
                return;
            }

            if (devPuzzleOrder == null || devPuzzleOrder.Length == 0) return;
            _puzzleIndex = (_puzzleIndex + 1) % devPuzzleOrder.Length;

            string devId = devPuzzleOrder[_puzzleIndex];
            GameSession.LevelIndex = _puzzleIndex + 1;     // ✅ also update level index for dev path
            GameSession.SetPuzzle(devId);                  // ✅ keep session consistent
            PlayerPrefs.Save();

            LoadPuzzleById(devId);

            // ✅ refresh header here too
            FindObjectOfType<GameplayHUD>()?.ForceRefreshHeader();
        }

        private void LoadNextDailyPuzzle()
        {
            if (!TryGetCurrentDailyPuzzle(
                    out System.DateTime currentDate,
                    out string currentDifficulty))
            {
                Debug.LogError(
                    "Could not determine the current Daily puzzle date " +
                    "and difficulty from: " +
                    GameSession.GetPuzzleId());

                ReturnToDailyPanel();
                return;
            }

            System.DateTime nextDate = currentDate;
            string nextDifficulty;

            switch (currentDifficulty.ToLowerInvariant())
            {
                case "easy":
                    nextDifficulty = "Medium";
                    break;

                case "medium":
                    nextDifficulty = "Hard";
                    break;

                case "hard":
                    nextDate = currentDate.AddDays(1);
                    nextDifficulty = "Easy";
                    break;

                default:
                    Debug.LogError(
                        $"Unknown Daily difficulty: {currentDifficulty}");

                    ReturnToDailyPanel();
                    return;
            }

            // Never allow Next to open a future Daily puzzle.
            if (nextDate.Date > System.DateTime.Today)
            {
                ReturnToDailyPanel();
                return;
            }

            string nextPuzzleEntryId =
                $"Daily_{nextDate:yyyy_MM_dd}_{nextDifficulty}";

            string nextPackPath =
                $"Daily/{nextDate:yyyy}/Daily_{nextDate:yyyy}_{nextDifficulty}";

            // Make sure the next puzzle exists before changing the session.
            PuzzleData nextPuzzleData =
                LoadPuzzleDataSmart(nextPuzzleEntryId, nextPackPath);

            if (nextPuzzleData == null)
            {
                Debug.LogWarning(
                    $"Next Daily puzzle could not be loaded: " +
                    $"{nextPuzzleEntryId}");

                ReturnToDailyPanel();
                return;
            }


            GameSession.DailyKey =
                GameSession.DateKey(nextDate);

            GameSession.PackPath =
                nextPackPath;

            GameSession.LevelIndex =
                nextDate.Day;

            GameSession.SetPuzzle(
                nextPuzzleEntryId);

            PlayerPrefs.Save();

            LoadPuzzleById(nextPuzzleEntryId);

            GameplayHUD hud =
                FindObjectOfType<GameplayHUD>();

            if (hud != null)
            {
                hud.ForceRefreshHeader();
                hud.ResumeAfterSolvedPanel();
            }
        }

        private void LoadNextDailySameDifficulty()
        {
            if (!TryGetCurrentDailyPuzzle(
                    out System.DateTime currentDate,
                    out string currentDifficulty))
            {
                Debug.LogError(
                    "Could not determine the current Daily puzzle " +
                    "for same-difficulty navigation.");

                ReturnToDailyPanel();
                return;
            }

            // Stay on the same difficulty, but advance one day.
            System.DateTime nextDate =
                currentDate.AddDays(1);

            string nextDifficulty =
                currentDifficulty;

            // Future Daily puzzles cannot be opened.
            if (nextDate.Date > System.DateTime.Today)
            {
                ReturnToDailyPanel();
                return;
            }

            string nextPuzzleEntryId =
                $"Daily_{nextDate:yyyy_MM_dd}_{nextDifficulty}";

            string nextPackPath =
                $"Daily/{nextDate:yyyy}/Daily_{nextDate:yyyy}_{nextDifficulty}";

            // Make sure the puzzle exists before changing session data.
            PuzzleData nextPuzzleData =
                LoadPuzzleDataSmart(nextPuzzleEntryId, nextPackPath);

            if (nextPuzzleData == null)
            {
                Debug.LogWarning(
                    $"Next Daily {nextDifficulty} puzzle could not be loaded: " +
                    nextPuzzleEntryId);

                ReturnToDailyPanel();
                return;
            }

            GameSession.DailyKey =
                GameSession.DateKey(nextDate);

            GameSession.PackPath =
                nextPackPath;

            GameSession.LevelIndex =
                nextDate.Day;

            GameSession.SetPuzzle(
                nextPuzzleEntryId);

            PlayerPrefs.Save();

            LoadPuzzleById(
                nextPuzzleEntryId);

            GameplayHUD hud =
                FindObjectOfType<GameplayHUD>();

            if (hud != null)
            {
                hud.ForceRefreshHeader();
                hud.ResumeAfterSolvedPanel();
            }
        }

        private void ReturnToDailyPanel()
        {

            PlayerPrefs.DeleteKey("menu_return");
            PlayerPrefs.DeleteKey("menu_return_pack");
            PlayerPrefs.SetString(
                HomeScreenController.StartScreenKey,
                HomeScreenController.DailyScreenId);

            PlayerPrefs.Save();

            SceneManager.LoadScene("HomeUI");
        }

        private bool TryGetCurrentDailyPuzzle(
            out System.DateTime date,
            out string difficulty)
        {
            date = default;
            difficulty = "";

            string sessionPuzzleId =
                GameSession.GetPuzzleId();

            if (string.IsNullOrEmpty(sessionPuzzleId))
                return false;

            // Remove the pack path, leaving:
            // Daily_2026_06_02_Easy
            string puzzleEntryId = sessionPuzzleId;

            int pipeIndex = puzzleEntryId.LastIndexOf('|');

            if (pipeIndex >= 0 &&
                pipeIndex < puzzleEntryId.Length - 1)
            {
                puzzleEntryId =
                    puzzleEntryId.Substring(pipeIndex + 1);
            }

            string[] parts =
                puzzleEntryId.Split('_');

            if (parts.Length < 5 ||
                !parts[0].Equals(
                    "Daily",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int year) ||
                !int.TryParse(parts[2], out int month) ||
                !int.TryParse(parts[3], out int day))
            {
                return false;
            }

            try
            {
                date = new System.DateTime(
                    year,
                    month,
                    day);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return false;
            }

            difficulty = parts[4];

            return
                difficulty.Equals(
                    "Easy",
                    System.StringComparison.OrdinalIgnoreCase) ||

                difficulty.Equals(
                    "Medium",
                    System.StringComparison.OrdinalIgnoreCase) ||

                difficulty.Equals(
                    "Hard",
                    System.StringComparison.OrdinalIgnoreCase);
        }

        private void CheckSolved()
        {
            if (_model != null && _model.IsSolved())
            {
                if (_alreadySolved) return;
                _alreadySolved = true;

                // TIME TRIAL: no solved panel, immediately load next puzzle
                if (Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.TimeTrial)
                {
                    if (sfx != null) sfx.PlaySolved();

                    Shikaku.Menu.GameSession.TimeTrialSolvedCount =
                        Shikaku.Menu.GameSession.TimeTrialSolvedCount + 1;

                    Shikaku.Menu.GameSession.TimeTrialCompletedSquares =
                        Shikaku.Menu.GameSession.TimeTrialCompletedSquares + (width * height);

                    PlayerPrefs.Save();

                    PuzzleSolved?.Invoke();
                    _model.ClearSelection();

                    LoadRandomTimeTrialPuzzle();

                    _alreadySolved = false;
                    return;
                }

                // NORMAL MODES: show solved panel

                if (sfx != null) sfx.PlaySolved();

                PuzzleSolved?.Invoke();
                _model.ClearSelection();
            }
        }

        public int CountCompletedSquaresForTimeTrial()
        {
            if (_model == null || width <= 0 || height <= 0) return 0;

            int total = width * height;
            int completed = 0;

            for (int i = 0; i < total; i++)
            {
                if (!CellExists(i)) continue;
                if (_model.Value[i] == 0) continue;

                if (_model.IsComponentCompleteAt(i) && _model.ComponentHasAnchorAt(i))
                    completed++;
            }

            return completed;
        }

        public void RestartPuzzle()
        {
            if (_inputLocked)
                return;

            bool erasedSomething = HasPlayerPlacedCells();


            LoadPuzzleById(puzzleId);

            if (erasedSomething && sfx != null)
                sfx.PlayErase();
        }

        public bool HasPlayerPlacedCells()
        {
            return CountPlayerPlacedCells() > 0;
        }

        public int CountPlayerPlacedCells()
        {
            if (_model == null)
                return 0;

            int total = width * height;
            int count = 0;

            for (int i = 0; i < total; i++)
            {
                if (!CellExists(i))
                    continue;

                // A value that is not an original anchor was placed by the player.
                if (_model.Value[i] != 0 &&
                    !_model.IsAnchorCell(i))
                {
                    count++;
                }
            }

            return count;
        }

        public void BeginStroke()
        {
            if (_inputLocked)
                return;

            if (sfx != null) sfx.BeginStroke();
        }


        public void ShowHint()
        {
            if (_inputLocked)
                return;

            if (_solutionValues == null ||
                _solutionValues.Length != width * height)
            {
                Debug.Log("No solution data available for hints.");
                return;
            }

            if (_model == null)
                return;

            // Clear any old hint visual state.
            _hintIndex = -1;
            _hintKind = HintKind.None;
            _hintTargetValue = 0;

            List<int> target =
    FindLargestUnsolvedSolutionPolyomino();

            if (target == null || target.Count == 0)
            {
                Debug.Log(
                    "No unsolved solution polyomino found."
                );

                return;
            }

            // Complete the selected solution region.
            _model.ForceSolveCellsFromSolution(
                target,
                _solutionValues
            );

            // Render the completed region normally first.
            RenderAll();

            // Restart the completion pulse with the longer hint duration
            // on exactly the cells completed by this hint.
            for (int i = 0; i < target.Count; i++)
            {
                int cellIndex = target[i];

                if (cellIndex < 0 || cellIndex >= _cells.Length)
                    continue;

                CellView cell = _cells[cellIndex];

                if (cell != null)
                    cell.PlayHintCompletionPulse();
            }

            // Play only when a region was actually completed.
            if (sfx != null)
            {
                sfx.PlayDraw(isDrag: false);
            }

            CheckSolved();
        }

        private List<int> FindLargestUnsolvedSolutionPolyomino()
        {
            int total = width * height;

            bool[] visited = new bool[total];

            List<int> best = null;
            int bestSize = 0;

            for (int i = 0; i < total; i++)
            {
                if (visited[i]) continue;
                if (!CellExists(i)) continue;

                int solValue = _solutionValues[i];

                if (solValue <= 0)
                {
                    visited[i] = true;
                    continue;
                }

                List<int> region =
                    FloodSolutionRegion(i, solValue, visited);

                // Include regions of size 2 and larger.
                if (region.Count < 2) continue;

                // Skip regions that are already completely correct.
                if (IsSolutionRegionAlreadyCorrect(region, solValue))
                    continue;

                // Keep the largest incomplete or incorrect region found.
                if (region.Count <= bestSize) continue;

                best = region;
                bestSize = region.Count;
            }

            return best;
        }

        private List<int> FloodSolutionRegion(int startIndex, int solValue, bool[] visited)
        {
            List<int> cells = new List<int>();
            Queue<int> q = new Queue<int>();

            visited[startIndex] = true;
            q.Enqueue(startIndex);

            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                cells.Add(cur);

                int x = cur % width;
                int y = cur / width;

                TryQueueSolutionNeighbor(cur - 1, x > 0, solValue, visited, q);
                TryQueueSolutionNeighbor(cur + 1, x < width - 1, solValue, visited, q);
                TryQueueSolutionNeighbor(cur - width, y > 0, solValue, visited, q);
                TryQueueSolutionNeighbor(cur + width, y < height - 1, solValue, visited, q);
            }

            return cells;
        }

        private void TryQueueSolutionNeighbor(
    int idx,
    bool inside,
    int solValue,
    bool[] visited,
    Queue<int> q)
        {
            if (!inside) return;
            if (idx < 0 || idx >= width * height) return;
            if (visited[idx]) return;
            if (!CellExists(idx)) return;
            if (_solutionValues[idx] != solValue) return;

            visited[idx] = true;
            q.Enqueue(idx);
        }

        private bool IsSolutionRegionAlreadyCorrect(List<int> region, int solValue)
        {
            if (region == null || region.Count == 0) return true;

            bool[] inRegion = new bool[width * height];

            for (int i = 0; i < region.Count; i++)
            {
                int idx = region[i];
                inRegion[idx] = true;

                if (_model.Value[idx] != solValue)
                    return false;
            }

            // Also make sure there is not an extra same-value square touching this region.
            // Example: solved 3-region has a wrong extra 3 attached to it.
            for (int i = 0; i < region.Count; i++)
            {
                int idx = region[i];

                int x = idx % width;
                int y = idx / width;

                if (HasWrongSameValueNeighbor(idx - 1, x > 0, solValue, inRegion)) return false;
                if (HasWrongSameValueNeighbor(idx + 1, x < width - 1, solValue, inRegion)) return false;
                if (HasWrongSameValueNeighbor(idx - width, y > 0, solValue, inRegion)) return false;
                if (HasWrongSameValueNeighbor(idx + width, y < height - 1, solValue, inRegion)) return false;
            }

            return true;
        }

        private bool HasWrongSameValueNeighbor(
    int idx,
    bool inside,
    int solValue,
    bool[] inRegion)
        {
            if (!inside) return false;
            if (idx < 0 || idx >= width * height) return false;
            if (!CellExists(idx)) return false;
            if (inRegion[idx]) return false;

            return _model.Value[idx] == solValue;
        }


        // Expose hint state for CellView
        public bool IsHintCell(int idx) => idx == _hintIndex;
        public bool HintIsWrongFilled => _hintKind == HintKind.WrongFilled;
        public bool HintIsEmptyCell => _hintKind == HintKind.EmptyCell;
        public int HintTargetValue => _hintTargetValue;

        public float GetHintPulse01()
        {
            // 0..1..0..1 smooth pulse
            //return 0.5f + 0.5f * Mathf.Sin(Time.time * hintFlashSpeed * Mathf.PI * 2f);
            return Mathf.PingPong(Time.time * hintFlashSpeed, 1f);
        }

        public float GetHintAlpha()
        {
            float t = GetHintPulse01();
            return Mathf.Lerp(hintAlphaMin, hintAlphaMax, t);
        }



        private bool HasOrthogonalNeighborInComponent(int index, int compId)
        {
            if (!CellExists(index)) return false;

            int x = index % width;
            int y = index / width;

            // Up
            if (y > 0)
            {
                int n = (y - 1) * width + x;
                if (CellExists(n) && ComponentIdAt(n) == compId) return true;
            }

            // Right
            if (x < width - 1)
            {
                int n = y * width + (x + 1);
                if (CellExists(n) && ComponentIdAt(n) == compId) return true;
            }

            // Down
            if (y < height - 1)
            {
                int n = (y + 1) * width + x;
                if (CellExists(n) && ComponentIdAt(n) == compId) return true;
            }

            // Left
            if (x > 0)
            {
                int n = y * width + (x - 1);
                if (CellExists(n) && ComponentIdAt(n) == compId) return true;
            }

            return false;
        }


        public void LoadRandomTimeTrialPuzzle()
        {

            string nextId = PickRandomTimeTrialPuzzleId();
            if (string.IsNullOrEmpty(nextId))
            {
                Debug.LogError("TimeTrial: couldn't pick a random puzzle id.");
                return;
            }

            // In Time Trial, "level" means current puzzle number in this run.
            // solved count is already incremented before this method is called.
            GameSession.LevelIndex = GameSession.TimeTrialSolvedCount + 1;

            GameSession.SetPuzzle(nextId);
            PlayerPrefs.Save();

            LoadPuzzleById(nextId);
            FindObjectOfType<GameplayHUD>()?.ForceRefreshHeader();
        }

        private string PickRandomTimeTrialPuzzleId()
        {
            int size = GameSession.Size;

            if (size <= 0)
            {
                Debug.LogError("TimeTrial: GameSession.Size is missing.");
                return null;
            }

            string packPath = $"TimeTrial/TimeTrial_{size}x{size}";

            var ids = PuzzleCatalog.GetPackPuzzleIds(packPath);

            if (ids == null || ids.Count == 0)
            {
                Debug.LogError(
                    $"TimeTrial: no puzzle ids found for pack path '{packPath}'.");

                return null;
            }

            return GameSession.PickTimeTrialPuzzleAvoidingRecent(size, ids);
        }

        private void OnAnySettingChanged()
        {
            RenderAll();
        }

        private void OnThemeChanged()
        {
            EnsureBoardTray();

            if (_cells == null)
                return;

            bool isDark = ThemeManager.IsDark;
            foreach (CellView cell in _cells)
                cell?.SetDarkTheme(isDark);
        }

        private void OnDestroy()
        {
            ThemeManager.Changed -= OnThemeChanged;
            AppSettings.Changed -= OnAnySettingChanged;
        }

        private bool TryLoadNextStoryChapter()
        {
            var storyPacks = PuzzleCatalog.StoryPacks;

            if (storyPacks == null || storyPacks.Count == 0)
            {
                Debug.LogError("TryLoadNextStoryChapter: No story packs found.");
                return false;
            }

            string currentPackPath = GameSession.PackPath;

            if (string.IsNullOrEmpty(currentPackPath))
            {
                Debug.LogError("TryLoadNextStoryChapter: GameSession.PackPath is empty.");
                return false;
            }

            int currentPackIndex = -1;

            for (int i = 0; i < storyPacks.Count; i++)
            {
                Debug.Log($"Story pack {i}: {storyPacks[i].PackPath}");

                if (storyPacks[i].PackPath == currentPackPath)
                {
                    currentPackIndex = i;
                    break;
                }
            }

            if (currentPackIndex < 0)
            {
                Debug.LogError(
                    $"TryLoadNextStoryChapter: Current pack not found in StoryPacks. Current='{currentPackPath}'");

                return false;
            }

            int nextPackIndex = currentPackIndex + 1;

            if (nextPackIndex >= storyPacks.Count)
            {
                Debug.Log("TryLoadNextStoryChapter: No more story chapters.");
                return false;
            }

            string nextPackPath = storyPacks[nextPackIndex].PackPath;
            var nextIds = PuzzleCatalog.GetPackPuzzleIds(nextPackPath);

            if (nextIds == null || nextIds.Count == 0)
            {
                Debug.LogError(
                    $"TryLoadNextStoryChapter: Next story pack has no puzzles. Next='{nextPackPath}'");

                return false;
            }

            string nextPuzzleId = nextIds[0];

            Debug.Log(
                $"Adventure advancing from '{currentPackPath}' to '{nextPackPath}', puzzle '{nextPuzzleId}'.");


            _sessionOrder = new List<string>(nextIds);
            _puzzleIndex = 0;

            GameSession.Mode = MenuMode.Story;
            GameSession.PackPath = nextPackPath;
            GameSession.LevelIndex = 1;
            GameSession.SetPuzzle(nextPuzzleId);

            Progression.SetStoryCurrentPack(nextPackPath);
            Progression.SetLastPlayedLevelForPack(nextPackPath, 1);
            Progression.SetUnlockedLevelForPack(nextPackPath, 1);

            PlayerPrefs.Save();

            LoadPuzzleById(nextPuzzleId);

            GameplayHUD hud = FindObjectOfType<GameplayHUD>();

            if (hud != null)
            {
                hud.ForceRefreshHeader();
                hud.ResumeAfterSolvedPanel();
            }

            return true;
        }

        private void ShowIllegalMoveFeedback(int cellIndex)
        {
            if (_cells != null &&
                cellIndex >= 0 &&
                cellIndex < _cells.Length &&
                _cells[cellIndex] != null)
            {
                _cells[cellIndex].PlayIllegalMoveFeedback();
            }

            if (!illegalMoveHaptics ||
    !AppSettings.VibrationEnabled)
            {
                return;
            }

            if (Time.unscaledTime - _lastIllegalMoveHapticTime
                < illegalMoveHapticCooldown)
            {
                return;
            }

            _lastIllegalMoveHapticTime = Time.unscaledTime;

#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
