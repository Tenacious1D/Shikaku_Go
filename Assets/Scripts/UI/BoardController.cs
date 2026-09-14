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
        BeginRegion,
        UpdateRegion,
        CommitRegion,
        OverrideRegion,
        RemoveRegion,
        InvalidRegion,

        // Retained so the legacy tutorial UI continues to compile while its
        // Fillomino lesson is replaced.
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
            new Color32(225, 240, 243, 248);
        [SerializeField] private Color boardEdgeColor =
            new Color32(76, 130, 163, 255);
        [SerializeField] private Color boardShadowColor =
            new Color32(76, 130, 163, 255);
        [SerializeField] private Color darkBoardTrayColor =
            new Color32(8, 35, 60, 255);
        [SerializeField] private Color darkBoardEdgeColor =
            new Color32(55, 123, 158, 255);
        [SerializeField] private Color darkBoardShadowColor =
            new Color32(55, 123, 158, 255);

        private RectTransform _boardTray;
        private AspectRatioFitter _boardAspectRatioFitter;
        private static Sprite _hudPanelSprite;

        [Header("Palette")]
        [SerializeField] private PuzzlePalette palette;
        [SerializeField] private BlueprintThemeAssets blueprintTheme;
        public PuzzlePalette Palette => palette;
        public Color EmptyCellColor(bool dark) =>
            BlueprintThemeAssets.Resolve(blueprintTheme).GetBoardSurfaceColor(dark);

        [Header("Puzzle Size")]
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;

        [Header("Layout")]
        [SerializeField] private float spacing = 2f;
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
        private BlueprintRoomDecorationLayer _blueprintRooms;
        private readonly Vector3[] _tutorialCellWorldCorners = new Vector3[4];
        private System.Func<BoardInputAction, int, bool>
            _tutorialInputFilter;
        private readonly HashSet<int> _tutorialHighlights = new HashSet<int>();

        // Prevent spam resizing loops
        private bool _gridBuilt = false;
        private bool _useEdgeToEdgeBoardSlot;

        private SolutionRegion[] _solutionRegions;
        private bool _dragActive;
        private int _dragPointerId = int.MinValue;
        private int _dragStartIndex = -1;
        private int _dragCurrentIndex = -1;

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

        private static PuzzleData CreateDemoFallbackPuzzle()
        {
            return new PuzzleData
            {
                id = "shikaku_demo_4x4",
                width = 4,
                height = 4,
                givens = new[]
                {
                    new Given { x = 1, y = 0, v = 4 },
                    new Given { x = 2, y = 1, v = 4 },
                    new Given { x = 0, y = 2, v = 4 },
                    new Given { x = 3, y = 3, v = 4 }
                },
                solution = null
            };
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
            blueprintTheme = BlueprintThemeAssets.Resolve(blueprintTheme);

            PuzzleData data = LoadPuzzleDataSmart(puzzleId);

            if (data == null)
            {
                Debug.LogWarning(
                    $"Could not load '{puzzleId}'. Using the built-in Shikaku demo puzzle.");
                data = CreateDemoFallbackPuzzle();
            }

            puzzleId = PuzzleProgressStore.NormalizePuzzleId(data.id);
            GameSession.SetPuzzle(puzzleId);
            width = data.width;
            height = data.height;
            _model = new PuzzleModel(width, height, null);
            BuildCellExistsFromMask(data);
            _model.LoadPuzzle(data);
            _solutionRegions = data.solution != null ? data.solution.regions : null;

            _model.CellChanged += OnCellChanged;
            _model.SelectionChanged += RenderAll;
            _model.BoardReset += OnBoardReset;
            _model.BoardChanged += RenderAll;
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
            trayImage.color = BlueprintThemeAssets.Resolve(blueprintTheme).GetBoardGridColor(ThemeManager.IsDark);
            trayImage.raycastTarget = false;

            Outline trayEdge = _boardTray.GetComponent<Outline>();
            if (trayEdge == null)
                trayEdge = _boardTray.gameObject.AddComponent<Outline>();

            trayEdge.effectColor = trayImage.color;
            trayEdge.effectDistance = new Vector2(1f, -1f);
            trayEdge.useGraphicAlpha = true;

            Shadow trayShadow = _boardTray.GetComponent<Shadow>();
            trayShadow.effectColor = ThemeManager.IsDark
                ? darkBoardShadowColor
                : boardShadowColor;
            trayShadow.effectDistance = Vector2.zero;
            trayShadow.enabled = false;
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

            _blueprintRooms =
                BlueprintRoomDecorationLayer.Create(boardPanel);
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
            // Keep small puzzles comfortably sized on the available canvas too.
            desiredLongSide = Mathf.Max(desiredLongSide,
                Mathf.Min(maximumLongSide, Mathf.Min(availableWidth, availableHeight) * 0.92f));
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

            RefreshBlueprintRooms();
        }

        private void RefreshBlueprintRooms()
        {
            if (_blueprintRooms == null || _model == null ||
                _cells == null)
            {
                return;
            }

            bool hasPreview = TryGetDraftVisual(out BlueprintRoomVisualDescriptor preview);
            _blueprintRooms.Refresh(
                _model,
                _cells,
                palette,
                blueprintTheme,
                ThemeManager.IsDark,
                hasPreview,
                preview);
        }

        private bool TryGetDraftVisual(
            out BlueprintRoomVisualDescriptor descriptor)
        {
            descriptor = default;
            if (!_dragActive)
                return false;

            ShikakuRegionEvaluation evaluation =
                _model.EvaluateRegionCandidate(
                    _dragStartIndex,
                    _dragCurrentIndex);
            if (evaluation.Width <= 0 || evaluation.Height <= 0)
                return false;

            descriptor = new BlueprintRoomVisualDescriptor(
                -1,
                evaluation.X,
                evaluation.Y,
                evaluation.Width,
                evaluation.Height,
                evaluation.ClueValue,
                5,
                evaluation.IsRuleValid
                    ? BlueprintRoomVisualState.Preview
                    : BlueprintRoomVisualState.Invalid);
            return true;
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
                CancelRegionDrag();
            }
        }

        public void SetTutorialInputFilter(
            System.Func<BoardInputAction, int, bool> filter)
        {
            _tutorialInputFilter = filter;
            CellView.CancelPointerInput();
            CancelRegionDrag();
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

        public void OnPointerDownCell(int cellIndex, int pointerId = -1)
        {
            if (_inputLocked || !CellExists(cellIndex) || _dragActive)
                return;
            if (!AllowsInput(BoardInputAction.BeginRegion, cellIndex))
                return;

            _dragActive = true;
            _dragPointerId = pointerId;
            _dragStartIndex = cellIndex;
            _dragCurrentIndex = cellIndex;
            InputPerformed?.Invoke(BoardInputAction.BeginRegion, cellIndex);
            RenderAll();
        }

        public void OnPointerEnterCell(int cellIndex, int pointerId = -1)
        {
            if (_inputLocked || !_dragActive || pointerId != _dragPointerId ||
                !CellExists(cellIndex) || cellIndex == _dragCurrentIndex)
            {
                return;
            }

            _dragCurrentIndex = cellIndex;
            InputPerformed?.Invoke(BoardInputAction.UpdateRegion, cellIndex);
            RenderAll();
        }

        public void OnPointerUpCell(
            int pointerId,
            Vector2 screenPosition,
            Camera eventCamera,
            int releaseCellIndex)
        {
            if (!_dragActive || pointerId != _dragPointerId)
                return;
            if (!CellExists(releaseCellIndex) ||
                (boardPanel != null && !RectTransformUtility.RectangleContainsScreenPoint(
                    boardPanel, screenPosition, eventCamera)))
            {
                CancelRegionDrag();
                return;
            }

            _dragCurrentIndex = releaseCellIndex;
            int startIndex = _dragStartIndex;
            int endIndex = _dragCurrentIndex;
            bool wasTap = startIndex == endIndex;
            _dragActive = false;
            _dragPointerId = int.MinValue;
            _dragStartIndex = -1;
            _dragCurrentIndex = -1;

            // A tap on an existing rectangle selects it. It never replaces the
            // rectangle with a 1x1 region.
            if (wasTap && _model.GetRegionIdAt(startIndex) >= 0)
            {
                _model.SelectRegionAt(startIndex);
                InputPerformed?.Invoke(BoardInputAction.Select, startIndex);
                RenderAll();
                return;
            }

            ShikakuRegionEvaluation evaluation =
                _model.EvaluateRegionCandidate(startIndex, endIndex);
            if (!evaluation.IsRuleValid)
            {
                RenderAll();
                ShowIllegalMoveFeedback(endIndex);
                InputPerformed?.Invoke(BoardInputAction.InvalidRegion, endIndex);
                return;
            }

            int overlappedRegionCount = _model.CountOverlappedRegions(startIndex, endIndex);
            BoardInputAction action = overlappedRegionCount > 0
                ? BoardInputAction.OverrideRegion
                : BoardInputAction.CommitRegion;
            if (!AllowsInput(action, endIndex))
            {
                RenderAll();
                return;
            }

            if (_model.TryCommitRegion(startIndex, endIndex))
            {
                if (sfx != null)
                    sfx.PlayDraw(isDrag: !wasTap);
                InputPerformed?.Invoke(action, endIndex);
                CheckSolved();
            }
            RenderAll();
        }

        public void CancelRegionDrag()
        {
            if (!_dragActive)
                return;
            _dragActive = false;
            _dragPointerId = int.MinValue;
            _dragStartIndex = -1;
            _dragCurrentIndex = -1;
            RenderAll();
        }

        public void OnDoubleTapCell(int cellIndex)
        {
            CancelRegionDrag();
            if (_inputLocked || !CellExists(cellIndex) ||
                _model.GetRegionIdAt(cellIndex) < 0 ||
                !AllowsInput(BoardInputAction.RemoveRegion, cellIndex))
            {
                return;
            }

            if (_model.TryRemoveRegionAt(cellIndex))
            {
                if (sfx != null)
                    sfx.PlayErase();
                InputPerformed?.Invoke(BoardInputAction.RemoveRegion, cellIndex);
                CheckSolved();
            }
        }
        public int[] GetFirstSolutionRegionCells()
        {
            SolutionRegion region =
                _solutionRegions != null && _solutionRegions.Length > 0
                    ? _solutionRegions[0]
                    : null;
            if (region == null || region.width <= 0 || region.height <= 0)
            {
                for (int index = 0; index < width * height; index++)
                {
                    if (IsAnchorCell(index))
                        return new[] { index };
                }

                return null;
            }

            var cells = new int[region.width * region.height];
            int target = 0;
            for (int y = region.y; y < region.y + region.height; y++)
            {
                for (int x = region.x; x < region.x + region.width; x++)
                    cells[target++] = y * width + x;
            }

            return cells;
        }

        public int SelectedCellIndex => _model.SelectedCellIndex;
        public int SelectedComponentId => _model.SelectedRegionId;

        public bool IsAnchorCell(int idx) => _model.IsAnchorCell(idx);
        public int GivenNumberAt(int idx) => _model.GivenNumber[idx];
        public int ValueAt(int idx) => _model.Value[idx];
        public int RegionIdAt(int idx) => _model.GetRegionIdAt(idx);
        public int RegionPaletteIndexAt(int idx)
        {
            ShikakuRegion region = _model.GetRegionAt(idx);
            int paletteCount = palette != null && palette.numberColors != null
                ? palette.numberColors.Length - 1
                : 0;
            return BlueprintThemeAssets.Resolve(blueprintTheme)
                .GetStablePaletteIndex(region, paletteCount);
        }

        public Color RegionFillColorAt(int idx, bool dark)
        {
            bool valid = _model.IsRegionValidAt(idx);
            Color identity = palette != null
                ? palette.GetColorForNumber(RegionPaletteIndexAt(idx))
                : new Color32(45, 145, 180, 255);
            return BlueprintThemeAssets.Resolve(blueprintTheme)
                .GetRoomFill(identity, dark, valid);
        }
        public bool IsCellAssigned(int idx) => _model.GetRegionIdAt(idx) >= 0;
        public bool IsRegionValidAt(int idx) => _model.IsRegionValidAt(idx);
        public bool IsRegionSelectedAt(int idx) => _model.IsRegionSelectedAt(idx);
        public bool IsHintLockedRegionAt(int idx) => _model.IsHintLockedCell(idx);

        public bool IsDraftCell(int index)
        {
            if (!_dragActive || index < 0 || index >= width * height)
                return false;
            int startX = _dragStartIndex % width;
            int startY = _dragStartIndex / width;
            int endX = _dragCurrentIndex % width;
            int endY = _dragCurrentIndex / width;
            int x = index % width;
            int y = index / width;
            return x >= Mathf.Min(startX, endX) && x <= Mathf.Max(startX, endX) &&
                   y >= Mathf.Min(startY, endY) && y <= Mathf.Max(startY, endY);
        }

        public bool IsDraftNeighbor(int index, int dx, int dy)
        {
            int x = index % width + dx;
            int y = index / width + dy;
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;
            return IsDraftCell(y * width + x);
        }

        public bool DraftIsGeometricallyValid => _dragActive &&
            _model.EvaluateRegionCandidate(
                _dragStartIndex,
                _dragCurrentIndex).IsRuleValid;

        public bool OverwriteTutorialCell(
            int sourceCellIndex,
            int targetCellIndex,
            bool playFeedback = true,
            bool checkSolved = true)
        {
            // The old tutorial painted individual cells. Shikaku moves are
            // rectangles, so this compatibility entry point is intentionally inert.
            return false;
        }

        public bool RestoreTutorialCell(
            int sourceCellIndex,
            int targetCellIndex,
            bool playFeedback = true,
            bool checkSolved = true)
        {
            return false;
        }
        public bool ResetTutorialPuzzleState()
        {
            if (string.IsNullOrEmpty(puzzleId))
                return false;

            _tutorialInputFilter = null;
            _tutorialHighlights.Clear();
            _inputLocked = false;
            CellView.CancelPointerInput();
            CancelRegionDrag();
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
                    if (_model.GetRegionIdAt(i) < 0)
                        return false;
                }

                return foundPlayableCell;
            }
        }

        private void LoadPuzzleById(string id)
        {
            CancelRegionDrag();
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

            _solutionRegions = data.solution != null ? data.solution.regions : null;

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
                _blueprintRooms?.PlaySolveSweep();

                // TIME TRIAL: no solved panel, immediately load next puzzle
                if (Shikaku.Menu.GameSession.Mode == Shikaku.Menu.MenuMode.TimeTrial)
                {
                    if (sfx != null) sfx.PlaySolved();

                    Shikaku.Menu.GameSession.TimeTrialSolvedCount =
                        Shikaku.Menu.GameSession.TimeTrialSolvedCount + 1;

                    Shikaku.Menu.GameSession.TimeTrialCompletedSquares =
                        Shikaku.Menu.GameSession.TimeTrialCompletedSquares + _model.PlayableCellCount;

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
                if (_model.GetRegionIdAt(i) < 0) continue;

                if (_model.IsRegionValidAt(i))
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

                if (_model.GetRegionIdAt(i) >= 0)
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
            if (_inputLocked || _model == null || _solutionRegions == null ||
                _solutionRegions.Length == 0)
            {
                Debug.Log("No canonical Shikaku solution regions are available for hints.");
                return;
            }

            CancelRegionDrag();
            SolutionRegion target = FindLargestUnsolvedSolutionRectangle();
            if (target == null)
            {
                Debug.Log("No unsolved Shikaku solution rectangle found.");
                return;
            }

            if (!_model.TryCommitSolutionRegion(target))
                return;

            RenderAll();
            int hintedCell = target.y * width + target.x;
            int hintedRegionId = _model.GetRegionIdAt(hintedCell);
            _blueprintRooms?.PlayHintPulse(hintedRegionId);

            if (sfx != null)
                sfx.PlayDraw(isDrag: false);
            CheckSolved();
        }

        private SolutionRegion FindLargestUnsolvedSolutionRectangle()
        {
            SolutionRegion best = null;
            int bestArea = -1;
            for (int i = 0; i < _solutionRegions.Length; i++)
            {
                SolutionRegion region = _solutionRegions[i];
                if (region == null || _model.IsSolutionRegionAlreadyCorrect(region))
                    continue;
                if (!_model.CanCommitRectangle(region.x, region.y, region.width, region.height))
                    continue;

                int area = region.width * region.height;
                if (area > bestArea)
                {
                    best = region;
                    bestArea = area;
                }
            }
            return best;
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

            RefreshBlueprintRooms();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelRegionDrag();
                CellView.CancelPointerInput();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                CancelRegionDrag();
                CellView.CancelPointerInput();
            }
        }

        private void OnDestroy()
        {
            CancelRegionDrag();
            if (_model != null)
                _model.BoardChanged -= RenderAll;
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
