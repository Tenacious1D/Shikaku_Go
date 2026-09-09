using System.Collections.Generic;
using Shikaku.Logic;
using Shikaku.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shikaku.UI
{
    internal enum AtlasRegionVisualState
    {
        Preview,
        Committed,
        Invalid
    }

    internal readonly struct AtlasRegionVisualDescriptor
    {
        public readonly int RegionId;
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;
        public readonly int ClueValue;
        public readonly int MotifIndex;
        public readonly AtlasRegionVisualState State;

        public int Area => Width * Height;

        public AtlasRegionVisualDescriptor(
            int regionId,
            int x,
            int y,
            int width,
            int height,
            int clueValue,
            int motifIndex,
            AtlasRegionVisualState state)
        {
            RegionId = regionId;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ClueValue = clueValue;
            MotifIndex = motifIndex;
            State = state;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class AtlasRegionDecorationLayer : MonoBehaviour
    {
        private sealed class DecorationView
        {
            public int RegionId;
            public int SeenGeneration;
            public RectTransform Rect;
            public RawImage Pattern;
            public Color TargetColor;
            public float CreatedAt;
        }

        private const int MotifCount = 6;
        private static Texture2D[] _fallbackMotifs;

        private readonly Dictionary<int, DecorationView> _active =
            new Dictionary<int, DecorationView>(64);
        private readonly Stack<DecorationView> _pool =
            new Stack<DecorationView>(16);
        private readonly List<int> _releaseBuffer = new List<int>(16);
        private readonly List<ShikakuRegion> _regionBuffer =
            new List<ShikakuRegion>(64);

        private RectTransform _root;
        private RectTransform _previewRect;
        private RawImage _previewPattern;
        private TextMeshProUGUI _previewLabel;
        private RectTransform _routeSweep;
        private Image _routeSweepImage;
        private Coroutine _routeSweepRoutine;
        private int _generation;
        private AtlasThemeAssets _theme;
        private bool _dark;

        public static AtlasRegionDecorationLayer Create(
            RectTransform boardPanel)
        {
            if (boardPanel == null)
                return null;

            Transform existing = boardPanel.Find("AtlasRegionDecorations");
            if (existing != null)
            {
                AtlasRegionDecorationLayer layer =
                    existing.GetComponent<AtlasRegionDecorationLayer>();
                if (layer != null)
                    return layer;
            }

            GameObject layerObject = new GameObject(
                "AtlasRegionDecorations",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(LayoutElement),
                typeof(AtlasRegionDecorationLayer));
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

            AtlasRegionDecorationLayer component =
                layerObject.GetComponent<AtlasRegionDecorationLayer>();
            component.Initialize(rect);
            rect.SetAsLastSibling();
            return component;
        }

        private void Initialize(RectTransform root)
        {
            _root = root;
            EnsurePreview();
            EnsureRouteSweep();
        }

        public void Refresh(
            PuzzleModel model,
            CellView[] cells,
            AtlasThemeAssets theme,
            bool dark,
            bool hasPreview,
            AtlasRegionVisualDescriptor preview)
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_root == null || model == null || cells == null)
                return;

            _theme = AtlasThemeAssets.Resolve(theme);
            _dark = dark;
            _generation++;
            model.CopyRegionsTo(_regionBuffer);

            for (int index = 0; index < _regionBuffer.Count; index++)
            {
                ShikakuRegion region = _regionBuffer[index];
                DecorationView view = GetOrCreate(region.Id);
                view.SeenGeneration = _generation;
                int motifIndex = _theme.GetStableMotifIndex(region);
                Texture2D motif = _theme.GetDistrictMotif(motifIndex);
                view.Pattern.texture = motif != null
                    ? motif
                    : GetFallbackMotif(motifIndex);
                view.Pattern.uvRect = new Rect(
                    0f,
                    0f,
                    Mathf.Max(1f, region.Width * 0.7f),
                    Mathf.Max(1f, region.Height * 0.7f));
                view.TargetColor = _theme.GetPatternColor(_dark);
                view.Pattern.color = view.TargetColor;
                view.Pattern.enabled = true;
                SetRegionRect(
                    view.Rect,
                    cells,
                    model.Width,
                    region.X,
                    region.Y,
                    region.Width,
                    region.Height);
            }

            _releaseBuffer.Clear();
            foreach (KeyValuePair<int, DecorationView> pair in _active)
            {
                if (pair.Value.SeenGeneration != _generation)
                    _releaseBuffer.Add(pair.Key);
            }
            for (int index = 0; index < _releaseBuffer.Count; index++)
                Release(_releaseBuffer[index]);

            RefreshPreview(cells, model.Width, hasPreview, preview);
            _root.SetAsLastSibling();
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
            view.Rect.gameObject.SetActive(true);
            view.Rect.localScale = AppSettings.ReduceMotion
                ? Vector3.one
                : new Vector3(0.985f, 0.985f, 1f);
            _active.Add(regionId, view);
            return view;
        }

        private DecorationView CreateView()
        {
            GameObject viewObject = new GameObject(
                "AtlasDistrict",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            viewObject.layer = gameObject.layer;
            viewObject.transform.SetParent(_root, false);
            RawImage pattern = viewObject.GetComponent<RawImage>();
            pattern.raycastTarget = false;
            pattern.maskable = true;
            return new DecorationView
            {
                Rect = viewObject.GetComponent<RectTransform>(),
                Pattern = pattern
            };
        }

        private void Release(int regionId)
        {
            if (!_active.TryGetValue(regionId, out DecorationView view))
                return;

            _active.Remove(regionId);
            view.Rect.gameObject.SetActive(false);
            _pool.Push(view);
        }

        private void EnsurePreview()
        {
            if (_previewRect != null)
                return;

            GameObject previewObject = new GameObject(
                "AtlasDraft",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            previewObject.layer = gameObject.layer;
            previewObject.transform.SetParent(_root, false);
            _previewRect = previewObject.GetComponent<RectTransform>();
            _previewPattern = previewObject.GetComponent<RawImage>();
            _previewPattern.raycastTarget = false;

            GameObject labelObject = new GameObject(
                "AtlasDraftArea",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(_previewRect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -8f);
            labelRect.sizeDelta = new Vector2(250f, 62f);

            _previewLabel = labelObject.GetComponent<TextMeshProUGUI>();
            _previewLabel.raycastTarget = false;
            _previewLabel.alignment = TextAlignmentOptions.Center;
            _previewLabel.fontStyle = FontStyles.Bold;
            _previewLabel.fontSize = 30f;
            _previewLabel.enableAutoSizing = true;
            _previewLabel.fontSizeMin = 18f;
            _previewLabel.fontSizeMax = 30f;
            _previewRect.gameObject.SetActive(false);
        }

        public void PlaySolveSweep()
        {
            EnsureRouteSweep();
            if (_routeSweep == null)
                return;

            if (_routeSweepRoutine != null)
            {
                StopCoroutine(_routeSweepRoutine);
                _routeSweepRoutine = null;
            }

            if (AppSettings.ReduceMotion)
            {
                _routeSweep.gameObject.SetActive(false);
                return;
            }

            _routeSweepRoutine = StartCoroutine(AnimateRouteSweep());
        }

        private void EnsureRouteSweep()
        {
            if (_routeSweep != null || _root == null)
                return;

            GameObject routeObject = new GameObject(
                "AtlasRouteSweep",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            routeObject.layer = gameObject.layer;
            routeObject.transform.SetParent(_root, false);
            _routeSweep = routeObject.GetComponent<RectTransform>();
            _routeSweep.anchorMin = new Vector2(0f, 0.5f);
            _routeSweep.anchorMax = new Vector2(0f, 0.5f);
            _routeSweep.pivot = new Vector2(0.5f, 0.5f);
            _routeSweep.localRotation = Quaternion.Euler(0f, 0f, -9f);
            _routeSweepImage = routeObject.GetComponent<Image>();
            _routeSweepImage.raycastTarget = false;
            _routeSweepImage.color = new Color32(190, 92, 67, 0);
            routeObject.SetActive(false);
        }

        private System.Collections.IEnumerator AnimateRouteSweep()
        {
            _routeSweep.gameObject.SetActive(true);
            _routeSweep.SetAsLastSibling();
            float boardWidth = Mathf.Max(1f, _root.rect.width);
            float boardHeight = Mathf.Max(1f, _root.rect.height);
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
                _routeSweepImage.color = new Color32(
                    190,
                    92,
                    67,
                    (byte)Mathf.RoundToInt(210f * fade));
                yield return null;
            }

            _routeSweep.gameObject.SetActive(false);
            _routeSweepRoutine = null;
        }
        private void RefreshPreview(
            CellView[] cells,
            int boardWidth,
            bool hasPreview,
            AtlasRegionVisualDescriptor preview)
        {
            EnsurePreview();
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
            _previewPattern.texture = GetFallbackMotif(5);
            _previewPattern.uvRect = new Rect(
                0f,
                0f,
                Mathf.Max(1f, preview.Width * 0.7f),
                Mathf.Max(1f, preview.Height * 0.7f));

            bool valid = preview.State != AtlasRegionVisualState.Invalid;
            _previewPattern.color = valid
                ? new Color32(54, 138, 154, 40)
                : new Color32(190, 52, 56, 36);
            _previewLabel.color = valid
                ? (_dark
                    ? new Color32(221, 240, 238, 255)
                    : new Color32(27, 91, 102, 255))
                : new Color32(190, 52, 56, 255);
            _previewLabel.text =
                $"{preview.Width} × {preview.Height} = {preview.Area}";
            _previewRect.SetAsLastSibling();
        }

        private void Update()
        {
            if (_theme == null || AppSettings.ReduceMotion)
                return;

            float duration = Mathf.Max(0.01f, _theme.regionCommitDuration);
            foreach (KeyValuePair<int, DecorationView> pair in _active)
            {
                DecorationView view = pair.Value;
                float t = Mathf.Clamp01(
                    (Time.unscaledTime - view.CreatedAt) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                view.Rect.localScale = Vector3.LerpUnclamped(
                    new Vector3(0.985f, 0.985f, 1f),
                    Vector3.one,
                    eased);
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
            if (first == null || last == null)
            {
                target.gameObject.SetActive(false);
                return;
            }

            Bounds firstBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    _root,
                    first);
            Bounds lastBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    _root,
                    last);
            Vector3 min = Vector3.Min(firstBounds.min, lastBounds.min);
            Vector3 max = Vector3.Max(firstBounds.max, lastBounds.max);

            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.zero;
            target.pivot = Vector2.zero;
            target.anchoredPosition = new Vector2(min.x, min.y);
            target.sizeDelta = new Vector2(max.x - min.x, max.y - min.y);
        }

        private static Texture2D GetFallbackMotif(int motifIndex)
        {
            if (_fallbackMotifs == null)
            {
                _fallbackMotifs = new Texture2D[MotifCount];
                for (int index = 0; index < MotifCount; index++)
                    _fallbackMotifs[index] = BuildMotif(index);
            }

            return _fallbackMotifs[Mathf.Abs(motifIndex) % MotifCount];
        }

        private static Texture2D BuildMotif(int motifIndex)
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
                    switch (motifIndex)
                    {
                        case 0: // contour bands
                            float dx = x - 31.5f;
                            float dy = y - 31.5f;
                            int radius = Mathf.RoundToInt(
                                Mathf.Sqrt(dx * dx + dy * dy));
                            mark = radius % 12 == 0;
                            break;
                        case 1: // orchard dots
                            mark = x % 16 == 8 && y % 16 == 8;
                            break;
                        case 2: // streets
                            mark = x % 24 == 4 || y % 24 == 12;
                            break;
                        case 3: // water lines
                            int wave = 8 + Mathf.RoundToInt(
                                Mathf.Sin(x * Mathf.PI / 16f) * 3f);
                            mark = y % 16 == wave;
                            break;
                        case 4: // trail dashes
                            mark = ((x + y) % 18) < 2 && (x / 8) % 2 == 0;
                            break;
                        default: // survey hatch
                            mark = (x + y) % 16 == 0;
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
                name = $"Pocket Atlas Motif {motifIndex + 1}",
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