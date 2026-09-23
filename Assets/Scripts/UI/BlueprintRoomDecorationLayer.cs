using System.Collections;
using System.Collections.Generic;
using Shikaku.Logic;
using Shikaku.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shikaku.UI
{
    internal enum BlueprintRoomVisualState
    {
        Preview,
        Committed,
        Invalid
    }

    internal readonly struct BlueprintRoomVisualDescriptor
    {
        public readonly int RegionId;
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;
        public readonly int ClueValue;
        public readonly int PaletteIndex;
        public readonly int HatchIndex;
        public readonly BlueprintRoomVisualState State;

        public int Area => Width * Height;

        public BlueprintRoomVisualDescriptor(
            int regionId,
            int x,
            int y,
            int width,
            int height,
            int clueValue,
            int hatchIndex,
            BlueprintRoomVisualState state,
            int paletteIndex = 1)
        {
            RegionId = regionId;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ClueValue = clueValue;
            PaletteIndex = paletteIndex;
            HatchIndex = hatchIndex;
            State = state;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class BlueprintRoomDecorationLayer : MonoBehaviour
    {
        private sealed class DecorationView
        {
            public int RegionId;
            public int SeenGeneration;
            public RectTransform Rect;
            public CanvasGroup Canvas;
            public Image Fill;
            public RawImage Pattern;
            public FloorplanFurnitureGraphic Furniture;
            public CanvasGroup FurnitureCanvas;

            public Color WallColor;
            public float CreatedAt;
            public Coroutine HintRoutine;
        }

        private const int HatchCount = 6;
        private static Texture2D[] _fallbackHatches;

        private readonly Dictionary<int, DecorationView> _active =
            new Dictionary<int, DecorationView>(64);
        private readonly Stack<DecorationView> _pool =
            new Stack<DecorationView>(16);
        private readonly List<int> _releaseBuffer = new List<int>(16);
        private readonly List<ShikakuRegion> _regionBuffer =
            new List<ShikakuRegion>(64);

        private FloorplanWallGraphic _walls;
        private RectTransform _furnitureRoot;
        private RectTransform _fillRoot;
        private RectTransform _feedbackRoot;
        private RectTransform _previewRect;
        private RawImage _previewPattern;
        private TextMeshProUGUI _previewLabel;
        private RectTransform _routeSweep;
        private Image _routeSweepImage;
        private Image[] _previewWalls;
        private Image[] _previewCornerMarks;
        private Coroutine _routeSweepRoutine;
        private RectTransform _completionRoot;
        private CanvasGroup _completionCanvas;
        private Image _completionPlate;
        private TextMeshProUGUI _completionLabel;
        private Image[] _completionWalls;
        private Coroutine _completionRoutine;
        private int _generation;
        private BlueprintThemeAssets _theme;
        private bool _dark;
        private int _lastPreviewWidth = -1;
        private int _lastPreviewHeight = -1;
        private bool _lastPreviewValid;

        public static BlueprintRoomDecorationLayer Create(
            RectTransform boardPanel)
        {
            if (boardPanel == null)
                return null;

            Transform existing = boardPanel.Find("BlueprintRooms");
            BlueprintRoomDecorationLayer layer = existing != null
                ? existing.GetComponent<BlueprintRoomDecorationLayer>()
                : null;

            if (layer == null)
            {
                GameObject fillObject = CreateLayerObject(
                    boardPanel,
                    "BlueprintRooms",
                    true);
                layer = fillObject.AddComponent<BlueprintRoomDecorationLayer>();
                existing = fillObject.transform;
            }

            RectTransform fillRoot = existing as RectTransform;
            RectTransform feedbackRoot = EnsureFeedbackRoot(boardPanel);
            layer.Initialize(fillRoot, feedbackRoot);
            fillRoot.SetAsFirstSibling();
            feedbackRoot.SetAsLastSibling();
            return layer;
        }

        private static GameObject CreateLayerObject(
            RectTransform boardPanel,
            string objectName,
            bool clipped)
        {
            GameObject layerObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(LayoutElement));
            layerObject.layer = boardPanel.gameObject.layer;
            layerObject.transform.SetParent(boardPanel, false);

            RectTransform rect = layerObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            LayoutElement layout = layerObject.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;

            CanvasGroup canvasGroup = layerObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (clipped)
                layerObject.AddComponent<RectMask2D>();

            return layerObject;
        }

        private static RectTransform EnsureFeedbackRoot(RectTransform boardPanel)
        {
            Transform existing = boardPanel.Find("BlueprintRoomFeedback");
            if (existing != null)
            {
                if (existing.GetComponent<RectMask2D>() == null)
                    existing.gameObject.AddComponent<RectMask2D>();
                return existing as RectTransform;
            }

            return CreateLayerObject(
                boardPanel,
                "BlueprintRoomFeedback",
                true).GetComponent<RectTransform>();
        }

        private void Initialize(
            RectTransform fillRoot,
            RectTransform feedbackRoot)
        {
            _fillRoot = fillRoot;
            _feedbackRoot = feedbackRoot;
            if (_walls == null)
            {
                var wallObject = new GameObject("FloorplanWalls", typeof(RectTransform), typeof(CanvasRenderer), typeof(FloorplanWallGraphic));
                wallObject.layer = gameObject.layer;
                wallObject.transform.SetParent(_feedbackRoot, false);
                var rect = (RectTransform)wallObject.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _walls = wallObject.GetComponent<FloorplanWallGraphic>();
                wallObject.transform.SetAsFirstSibling();
            }
            if (_furnitureRoot == null)
            {
                _furnitureRoot = CreateLayerObject(_feedbackRoot, "FloorplanFurniture", false)
                    .GetComponent<RectTransform>();
                _furnitureRoot.SetAsFirstSibling();
            }
            EnsurePreview();
            EnsureRouteSweep();
            EnsureCompletionPresentation();
        }

        public void Refresh(
            PuzzleModel model,
            CellView[] cells,
            PuzzlePalette palette,
            BlueprintThemeAssets theme,
            bool dark,
            bool hasPreview,
            BlueprintRoomVisualDescriptor preview,
            bool showFurniture = true)
        {
            if (_fillRoot == null)
                _fillRoot = transform as RectTransform;
            if (_feedbackRoot == null && _fillRoot != null)
                _feedbackRoot = EnsureFeedbackRoot(_fillRoot.parent as RectTransform);
            if (_fillRoot == null || _feedbackRoot == null ||
                model == null || cells == null)
            {
                return;
            }

            _theme = BlueprintThemeAssets.Resolve(theme);
            _dark = dark;
            _generation++;
            model.CopyRegionsTo(_regionBuffer);


            for (int index = 0; index < _regionBuffer.Count; index++)
            {
                ShikakuRegion region = _regionBuffer[index];
                DecorationView view = GetOrCreate(region.Id);
                view.SeenGeneration = _generation;

                bool selected = model.SelectedRegionId == region.Id;

                view.Fill.color = _theme.GetFloorplanRoomFill(region, _dark);
                // Solid floors keep furniture silhouettes readable in both themes.
                view.Pattern.enabled = false;
                view.Furniture.SetRoom(model, region, _dark, showFurniture);
                view.WallColor = !region.IsValid ? _theme.invalidInk :
                    selected ? _theme.GetFloorplanSelectionColor(_dark) : _theme.GetFloorplanWallColor(_dark);
                SetRegionRect(
                    view.Rect,
                    cells,
                    model.Width,
                    region.X,
                    region.Y,
                    region.Width,
                    region.Height);
                SetRegionRect(view.Furniture.rectTransform, cells, model.Width,
                    region.X, region.Y, region.Width, region.Height);
            }

            _releaseBuffer.Clear();
            foreach (KeyValuePair<int, DecorationView> pair in _active)
            {
                if (pair.Value.SeenGeneration != _generation)
                    _releaseBuffer.Add(pair.Key);
            }
            for (int index = 0; index < _releaseBuffer.Count; index++)
                Release(_releaseBuffer[index]);

            _walls.SetBoard(model, cells, _theme, _dark);
            RefreshPreview(cells, model.Width, hasPreview, preview);
            _fillRoot.SetAsFirstSibling();
            _feedbackRoot.SetAsLastSibling();
        }

        private DecorationView GetOrCreate(int regionId)
        {
            if (_active.TryGetValue(regionId, out DecorationView existing))
                return existing;

            DecorationView view = _pool.Count > 0
                ? _pool.Pop()
                : CreateView();
            view.RegionId = regionId;
            view.CreatedAt = Time.unscaledTime;
            view.Canvas.alpha = AppSettings.ReduceMotion ? 1f : 0f;
            view.FurnitureCanvas.alpha = view.Canvas.alpha;
            view.Furniture.SetReveal(AppSettings.ReduceMotion ? 1f : 0f);
            view.Rect.gameObject.SetActive(true);
            view.Furniture.gameObject.SetActive(true);
            view.Rect.localScale = Vector3.one;
            _active.Add(regionId, view);
            return view;
        }

        private DecorationView CreateView()
        {
            GameObject viewObject = new GameObject(
                "BlueprintRoom",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            viewObject.layer = gameObject.layer;
            viewObject.transform.SetParent(_fillRoot, false);

            Image fill = viewObject.GetComponent<Image>();
            fill.raycastTarget = false;
            fill.maskable = true;

            RectTransform roomRect = viewObject.GetComponent<RectTransform>();
            RawImage pattern = CreatePattern(roomRect);
            var furnitureObject = new GameObject("RoomFurniture", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(CanvasGroup), typeof(FloorplanFurnitureGraphic));
            furnitureObject.layer = gameObject.layer;
            furnitureObject.transform.SetParent(_furnitureRoot, false);
            var furnitureRect = (RectTransform)furnitureObject.transform;
            furnitureRect.anchorMin = Vector2.zero;
            furnitureRect.anchorMax = Vector2.one;
            furnitureRect.offsetMin = furnitureRect.offsetMax = Vector2.zero;
            return new DecorationView
            {
                Rect = roomRect,
                Canvas = viewObject.GetComponent<CanvasGroup>(),
                Fill = fill,
                Pattern = pattern,
                Furniture = furnitureObject.GetComponent<FloorplanFurnitureGraphic>(),
                FurnitureCanvas = furnitureObject.GetComponent<CanvasGroup>()
            };
        }

        private RawImage CreatePattern(RectTransform parent)
        {
            GameObject patternObject = new GameObject(
                "RoomPattern",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            patternObject.layer = gameObject.layer;
            patternObject.transform.SetParent(parent, false);
            RectTransform patternRect = patternObject.GetComponent<RectTransform>();
            patternRect.anchorMin = Vector2.zero;
            patternRect.anchorMax = Vector2.one;
            patternRect.offsetMin = Vector2.zero;
            patternRect.offsetMax = Vector2.zero;
            RawImage pattern = patternObject.GetComponent<RawImage>();
            pattern.raycastTarget = false;
            pattern.maskable = true;
            pattern.enabled = false;
            return pattern;
        }

        private Image[] CreateWalls(
            RectTransform parent,
            string prefix,
            float thickness)
        {
            return new[]
            {
                CreateWall(parent, prefix + "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness)),
                CreateWall(parent, prefix + "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness)),
                CreateWall(parent, prefix + "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, 0f)),
                CreateWall(parent, prefix + "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, 0f))
            };
        }

        private Image CreateWall(
            RectTransform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta)
        {
            GameObject wallObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            wallObject.layer = gameObject.layer;
            wallObject.transform.SetParent(parent, false);
            RectTransform wallRect = wallObject.GetComponent<RectTransform>();
            wallRect.anchorMin = anchorMin;
            wallRect.anchorMax = anchorMax;
            wallRect.pivot = new Vector2(0.5f, 0.5f);
            wallRect.anchoredPosition = Vector2.zero;
            wallRect.sizeDelta = sizeDelta;
            Image wall = wallObject.GetComponent<Image>();
            wall.raycastTarget = false;
            return wall;
        }

        private void ApplyRoomWalls(DecorationView view, Color outline)
        {
            _walls?.SetHintColor(view.RegionId, outline);
        }

        private void Release(int regionId)
        {
            if (!_active.TryGetValue(regionId, out DecorationView view))
                return;

            _active.Remove(regionId);
            _walls?.ClearHintColor(regionId);
            if (view.HintRoutine != null)
            {
                StopCoroutine(view.HintRoutine);
                view.HintRoutine = null;
            }
            view.Rect.gameObject.SetActive(false);
            view.Furniture.gameObject.SetActive(false);
            _pool.Push(view);
        }

        private void EnsurePreview()
        {
            if (_previewRect != null || _feedbackRoot == null)
                return;

            GameObject previewObject = new GameObject(
                "BlueprintDraft",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            previewObject.layer = gameObject.layer;
            previewObject.transform.SetParent(_feedbackRoot, false);
            _previewRect = previewObject.GetComponent<RectTransform>();
            _previewPattern = previewObject.GetComponent<RawImage>();
            _previewPattern.raycastTarget = false;
            _previewPattern.maskable = true;
            _previewWalls = CreateWalls(_previewRect, "DraftWall", 4f);
            _previewCornerMarks = CreateCornerMarks(_previewRect);

            GameObject labelObject = new GameObject(
                "BlueprintDimensionLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(_previewRect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -6f);
            labelRect.sizeDelta = new Vector2(-10f, 50f);

            _previewLabel = labelObject.GetComponent<TextMeshProUGUI>();
            _previewLabel.raycastTarget = false;
            _previewLabel.alignment = TextAlignmentOptions.Center;
            _previewLabel.fontStyle = FontStyles.Bold;
            _previewLabel.fontSize = 30f;
            _previewLabel.enableAutoSizing = true;
            _previewLabel.fontSizeMin = 12f;
            _previewLabel.fontSizeMax = 28f;
            _previewRect.gameObject.SetActive(false);
        }

        public void PlayHintPulse(int regionId)
        {
            if (!_active.TryGetValue(regionId, out DecorationView view))
                return;

            if (view.HintRoutine != null)
                StopCoroutine(view.HintRoutine);
            view.HintRoutine = StartCoroutine(AnimateHintPulse(view));
        }

        private IEnumerator AnimateHintPulse(DecorationView view)
        {
            if (AppSettings.ReduceMotion)
            {
                ApplyRoomWalls(view, _theme.GetFloorplanSelectionColor(_dark));
                yield return new WaitForSecondsRealtime(0.35f);
                _walls?.ClearHintColor(view.RegionId);
                view.HintRoutine = null;
                yield break;
            }

            Color pulseColor = new Color32(255, 207, 57, 255);
            const int pulseCount = 3;
            const float pulseDuration = 0.28f;
            for (int pulse = 0; pulse < pulseCount; pulse++)
            {
                float elapsed = 0f;
                while (elapsed < pulseDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float amount = Mathf.Sin(
                        Mathf.Clamp01(elapsed / pulseDuration) * Mathf.PI);
                    ApplyRoomWalls(
                        view,
                        Color.Lerp(view.WallColor, pulseColor, amount));
                    yield return null;
                }
            }

            _walls?.ClearHintColor(view.RegionId);
            view.HintRoutine = null;
        }

        public void PlaySolveSweep(string completionLabel)
        {
            EnsureRouteSweep();
            EnsureCompletionPresentation();
            if (_routeSweep == null || _completionRoot == null)
                return;

            if (_routeSweepRoutine != null)
            {
                StopCoroutine(_routeSweepRoutine);
                _routeSweepRoutine = null;
            }

            if (_completionRoutine != null)
                StopCoroutine(_completionRoutine);

            _completionLabel.text = string.IsNullOrWhiteSpace(completionLabel)
                ? "FLOOR PLAN COMPLETE"
                : completionLabel;
            ApplyCompletionTheme();
            _completionRoutine = StartCoroutine(AnimateCompletionPresentation());
            if (!AppSettings.ReduceMotion)
                _routeSweepRoutine = StartCoroutine(AnimateRouteSweep());
        }

        private Image[] CreateCornerMarks(RectTransform parent)
        {
            var marks = new List<Image>(8);
            for (int y = 0; y <= 1; y++)
            {
                for (int x = 0; x <= 1; x++)
                {
                    Vector2 anchor = new Vector2(x, y);
                    marks.Add(CreateCornerMark(
                        parent,
                        $"DraftCorner{x}{y}H",
                        anchor,
                        new Vector2(18f, 3f)));
                    marks.Add(CreateCornerMark(
                        parent,
                        $"DraftCorner{x}{y}V",
                        anchor,
                        new Vector2(3f, 18f)));
                }
            }
            return marks.ToArray();
        }

        private Image CreateCornerMark(
            RectTransform parent,
            string objectName,
            Vector2 anchor,
            Vector2 size)
        {
            GameObject markObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            markObject.layer = gameObject.layer;
            markObject.transform.SetParent(parent, false);
            RectTransform rect = markObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = markObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private void EnsureRouteSweep()
        {
            if (_routeSweep != null || _feedbackRoot == null)
                return;

            GameObject routeObject = new GameObject(
                "BlueprintReviewSweep",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            routeObject.layer = gameObject.layer;
            routeObject.transform.SetParent(_feedbackRoot, false);
            _routeSweep = routeObject.GetComponent<RectTransform>();
            _routeSweep.anchorMin = new Vector2(0f, 0.5f);
            _routeSweep.anchorMax = new Vector2(0f, 0.5f);
            _routeSweep.pivot = new Vector2(0.5f, 0.5f);
            _routeSweep.localRotation = Quaternion.Euler(0f, 0f, -9f);
            _routeSweepImage = routeObject.GetComponent<Image>();
            _routeSweepImage.raycastTarget = false;
            _routeSweepImage.color = new Color32(43, 188, 221, 0);
            routeObject.SetActive(false);
        }

        private void EnsureCompletionPresentation()
        {
            if (_completionRoot != null || _feedbackRoot == null)
                return;

            GameObject rootObject = new GameObject(
                "BlueprintApproval",
                typeof(RectTransform),
                typeof(CanvasGroup));
            rootObject.layer = gameObject.layer;
            rootObject.transform.SetParent(_feedbackRoot, false);
            _completionRoot = rootObject.GetComponent<RectTransform>();
            _completionRoot.anchorMin = Vector2.zero;
            _completionRoot.anchorMax = Vector2.one;
            _completionRoot.offsetMin = new Vector2(2f, 2f);
            _completionRoot.offsetMax = new Vector2(-2f, -2f);
            _completionCanvas = rootObject.GetComponent<CanvasGroup>();
            _completionCanvas.interactable = false;
            _completionCanvas.blocksRaycasts = false;
            _completionWalls = CreateWalls(
                _completionRoot,
                "ApprovalFrame",
                6f);

            GameObject plateObject = new GameObject(
                "ApprovalPlate",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            plateObject.layer = gameObject.layer;
            plateObject.transform.SetParent(_completionRoot, false);
            RectTransform plateRect =
                plateObject.GetComponent<RectTransform>();
            plateRect.anchorMin = new Vector2(0.12f, 0.5f);
            plateRect.anchorMax = new Vector2(0.88f, 0.5f);
            plateRect.pivot = new Vector2(0.5f, 0.5f);
            plateRect.anchoredPosition = Vector2.zero;
            plateRect.sizeDelta = new Vector2(0f, 68f);
            _completionPlate = plateObject.GetComponent<Image>();
            _completionPlate.raycastTarget = false;

            GameObject labelObject = new GameObject(
                "ApprovalLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(plateRect, false);
            RectTransform labelRect =
                labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 4f);
            labelRect.offsetMax = new Vector2(-12f, -4f);
            _completionLabel = labelObject.GetComponent<TextMeshProUGUI>();
            _completionLabel.raycastTarget = false;
            _completionLabel.alignment = TextAlignmentOptions.Center;
            _completionLabel.fontStyle = FontStyles.Bold;
            _completionLabel.enableAutoSizing = true;
            _completionLabel.fontSizeMin = 14f;
            _completionLabel.fontSizeMax = 31f;
            _completionRoot.gameObject.SetActive(false);
        }

        private void ApplyCompletionTheme()
        {
            Color success = _dark
                ? Color.Lerp(_theme.successInk, Color.white, 0.25f)
                : _theme.successInk;
            for (int index = 0; index < _completionWalls.Length; index++)
                _completionWalls[index].color = success;
            _completionLabel.color = success;
            Color plate = _theme.GetBoardSurfaceColor(_dark);
            plate.a = _dark ? 0.94f : 0.96f;
            _completionPlate.color = plate;
        }

        private IEnumerator AnimateCompletionPresentation()
        {
            _completionRoot.gameObject.SetActive(true);
            _completionRoot.SetAsLastSibling();
            float duration = AppSettings.ReduceMotion ? 0.22f : 0.72f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float fadeIn = Mathf.SmoothStep(0f, 1f, t / 0.18f);
                float fadeOut = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((t - 0.72f) / 0.28f));
                _completionCanvas.alpha = Mathf.Min(fadeIn, fadeOut);
                yield return null;
            }

            _completionRoot.gameObject.SetActive(false);
            _completionRoutine = null;
        }

        private IEnumerator AnimateRouteSweep()
        {
            _routeSweep.gameObject.SetActive(true);
            _routeSweep.SetAsLastSibling();
            float boardWidth = Mathf.Max(1f, _feedbackRoot.rect.width);
            float boardHeight = Mathf.Max(1f, _feedbackRoot.rect.height);
            _routeSweep.sizeDelta = new Vector2(12f, boardHeight * 1.35f);
            const float duration = 0.42f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                _routeSweep.anchoredPosition = new Vector2(
                    Mathf.Lerp(-24f, boardWidth + 24f, eased),
                    0f);
                float fade = Mathf.Sin(t * Mathf.PI);
                Color sweepColor = _theme.GetInteractionColor(_dark);
                sweepColor.a = 0.82f * fade;
                _routeSweepImage.color = sweepColor;
                yield return null;
            }

            _routeSweep.gameObject.SetActive(false);
            _routeSweepRoutine = null;
        }

        private void RefreshPreview(
            CellView[] cells,
            int boardWidth,
            bool hasPreview,
            BlueprintRoomVisualDescriptor preview)
        {
            EnsurePreview();
            if (_previewRect == null)
                return;
            if (!hasPreview)
            {
                _previewRect.gameObject.SetActive(false);
                return;
            }

            _previewRect.gameObject.SetActive(true);
            SetRegionRect(
                _previewRect,
                cells,
                boardWidth,
                preview.X,
                preview.Y,
                preview.Width,
                preview.Height);
            _previewPattern.texture = GetFallbackHatch(5);
            _previewPattern.uvRect = new Rect(
                0f,
                0f,
                Mathf.Max(1f, preview.Width * 0.7f),
                Mathf.Max(1f, preview.Height * 0.7f));

            bool valid = preview.State != BlueprintRoomVisualState.Invalid;
            Color previewFill = valid
                ? _theme.GetInteractionColor(_dark)
                : _theme.invalidInk;
            previewFill.a = valid ? 0.20f : 0.18f;
            _previewPattern.color = previewFill;
            Color previewWallColor = valid
                ? _theme.GetInteractionColor(_dark)
                : _theme.invalidInk;
            for (int wallIndex = 0; wallIndex < _previewWalls.Length; wallIndex++)
                _previewWalls[wallIndex].color = previewWallColor;
            for (int markIndex = 0; markIndex < _previewCornerMarks.Length; markIndex++)
                _previewCornerMarks[markIndex].color = previewWallColor;
            _previewLabel.color = valid
                ? _theme.GetInteractionColor(_dark)
                : _theme.invalidInk;
            if (_lastPreviewWidth != preview.Width ||
                _lastPreviewHeight != preview.Height ||
                _lastPreviewValid != valid)
            {
                _previewLabel.text =
                    $"{preview.Width} × {preview.Height} = {preview.Area}";
                _lastPreviewWidth = preview.Width;
                _lastPreviewHeight = preview.Height;
                _lastPreviewValid = valid;
            }
            _previewRect.SetAsLastSibling();
        }

        private void Update()
        {
            if (_theme == null)
                return;

            float duration = Mathf.Max(0.01f, _theme.wallInkDuration);
            foreach (KeyValuePair<int, DecorationView> pair in _active)
            {
                DecorationView view = pair.Value;
                float t = Mathf.Clamp01(
                    (Time.unscaledTime - view.CreatedAt) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                view.Rect.localScale = Vector3.one;
                view.Canvas.alpha = AppSettings.ReduceMotion ? 1f : eased;
                float furnitureReveal = FloorplanFurnitureGraphic.RevealProgress(
                    Time.unscaledTime - view.CreatedAt, AppSettings.ReduceMotion);
                view.FurnitureCanvas.alpha = furnitureReveal;
                view.Furniture.SetReveal(furnitureReveal);
            }
        }

        private void SetRegionRect(
            RectTransform target,
            CellView[] cells,
            int boardWidth,
            int x,
            int y,
            int width,
            int height)
        {
            int firstIndex = y * boardWidth + x;
            int lastIndex =
                (y + height - 1) * boardWidth + (x + width - 1);
            if (firstIndex < 0 || lastIndex < 0 ||
                firstIndex >= cells.Length || lastIndex >= cells.Length ||
                cells[firstIndex] == null || cells[lastIndex] == null)
            {
                target.gameObject.SetActive(false);
                return;
            }

            RectTransform first = cells[firstIndex].transform as RectTransform;
            RectTransform last = cells[lastIndex].transform as RectTransform;
            RectTransform coordinateRoot = target.parent as RectTransform;
            if (first == null || last == null || coordinateRoot == null)
            {
                target.gameObject.SetActive(false);
                return;
            }

            Bounds firstBounds = CalculateRectBounds(coordinateRoot, first);
            Bounds lastBounds = CalculateRectBounds(coordinateRoot, last);
            ApplyLocalBounds(target, coordinateRoot, firstBounds, lastBounds);
        }

        internal static Bounds CalculateRectBounds(
            RectTransform root,
            RectTransform rect)
        {
            if (root == null || rect == null)
                return default;

            Rect localRect = rect.rect;
            Matrix4x4 rectToRoot =
                root.worldToLocalMatrix * rect.localToWorldMatrix;
            Vector3 firstCorner = rectToRoot.MultiplyPoint3x4(
                new Vector3(localRect.xMin, localRect.yMin, 0f));
            var bounds = new Bounds(firstCorner, Vector3.zero);
            bounds.Encapsulate(rectToRoot.MultiplyPoint3x4(
                new Vector3(localRect.xMin, localRect.yMax, 0f)));
            bounds.Encapsulate(rectToRoot.MultiplyPoint3x4(
                new Vector3(localRect.xMax, localRect.yMax, 0f)));
            bounds.Encapsulate(rectToRoot.MultiplyPoint3x4(
                new Vector3(localRect.xMax, localRect.yMin, 0f)));

            return bounds;
        }

        internal static void ApplyLocalBounds(
            RectTransform target,
            RectTransform root,
            Bounds firstBounds,
            Bounds lastBounds)
        {
            if (target == null || root == null)
                return;

            Vector3 min = Vector3.Min(firstBounds.min, lastBounds.min);
            Vector3 max = Vector3.Max(firstBounds.max, lastBounds.max);
            Vector3 center = (min + max) * 0.5f;

            target.anchorMin = root.pivot;
            target.anchorMax = root.pivot;
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = new Vector2(center.x, center.y);
            target.sizeDelta = new Vector2(max.x - min.x, max.y - min.y);
        }

        private static Texture2D GetFallbackHatch(int hatchIndex)
        {
            if (_fallbackHatches == null)
            {
                _fallbackHatches = new Texture2D[HatchCount];
                for (int index = 0; index < HatchCount; index++)
                    _fallbackHatches[index] = BuildHatch(index);
            }

            return _fallbackHatches[Mathf.Abs(hatchIndex) % HatchCount];
        }

        private static Texture2D BuildHatch(int hatchIndex)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            var ink = new Color32(255, 255, 255, 255);
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = clear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool mark;
                    switch (hatchIndex)
                    {
                        case 0:
                            int noise = (x * 17 + y * 31 + x * y * 3) & 31;
                            mark = noise == 0 || noise == 11;
                            break;
                        case 1:
                            int boardRow = y / 12;
                            mark = y % 12 == 0 ||
                                (boardRow % 2 == 0 && x % 32 == 0) ||
                                (boardRow % 2 == 1 && x % 32 == 16);
                            break;
                        case 2:
                            mark = x % 16 == 0 || y % 16 == 0;
                            break;
                        case 3:
                            mark = (x + y) % 12 == 0;
                            break;
                        case 4:
                            int segment = x % 16;
                            int ridge = segment < 8 ? segment : 15 - segment;
                            mark = Mathf.Abs((y % 16) - ridge * 2) < 2;
                            break;
                        default:
                            mark = (x + y) % 16 == 0 ||
                                (x - y + size) % 16 == 0;
                            break;
                    }

                    if (mark)
                        pixels[y * size + x] = ink;
                }
            }

            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = $"Blueprint Hatch {hatchIndex + 1}",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
