using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Shikaku.Logic;
using Shikaku.Settings;

namespace Shikaku.UI
{
    public class CellView : MonoBehaviour,
        IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler, ICancelHandler
    {
        [Header("Refs")]
        [SerializeField] private Image bg;
        [SerializeField] private TMP_Text label;         // anchor label (big, centered)
        [SerializeField] private TMP_Text colorLabel;    // drawn-cell label (small, corner)

        [Header("Tile Surface")]
        [SerializeField] private Sprite bevelSprite;
        [SerializeField] private Color blankCellColor =
            new Color32(238, 247, 248, 255);
        [SerializeField] private Color darkBlankCellColor =
            new Color32(10, 39, 65, 255);
        [SerializeField, Range(0f, 1f)] private float bevelHighlightBlend = 0.42f;
        [SerializeField] private Color tileShadowColor =
            new Color32(20, 65, 94, 72);
        [SerializeField] private Color darkTileShadowColor =
            new Color32(0, 0, 0, 135);
        [SerializeField] private Vector2 tileShadowOffset =
            new Vector2(0f, -3f);
        [SerializeField, Min(1f)] private float baseBorderThickness = 2f;

        private Image _bevelOverlay;

        [Header("Color Label Scaling")]
        [SerializeField] private float colorLabelSizeFactor = 0.28f;  // 28% of cell width
        [SerializeField] private float colorLabelMinSize = 16f;
        [SerializeField] private float colorLabelMaxSize = 48f;

        [Header("Anchor Label Style")]
        [SerializeField] private Color anchorLabelColor =
            new Color32(235, 247, 249, 255);
        [SerializeField] private Color anchorOutlineColor =
            new Color32(6, 36, 61, 190);
        [SerializeField] private float anchorOutlineWidth = 0.055f;
        [SerializeField] private float anchorFontSize = 70f;
        [SerializeField] private bool anchorBold = true;
        [Range(-1f, 1f)]
        [SerializeField] private float anchorFaceDilate = 0.05f; // gently thickens the glyph

        [Header("Borders (children Images under Borders)")]
        [SerializeField] private Image top;
        [SerializeField] private Image right;
        [SerializeField] private Image bottom;
        [SerializeField] private Image left;

        [Header("Border Style")]
        [SerializeField] private bool showCompleteOutline = false;
        [SerializeField] private Color borderColor =
            new Color32(35, 91, 126, 190);
        [SerializeField] private Color selectedCellBorderColor =
            new Color32(238, 250, 252, 255);
        [SerializeField, Min(1f)] private float selectedCellBorderThickness = 4f;
        [SerializeField] private Color invalidRegionBorderColor =
            new Color32(210, 69, 69, 255);
        [SerializeField] private Color previewFillColor =
            new Color32(76, 181, 204, 150);
        [SerializeField] private Color previewBorderColor =
            new Color32(30, 119, 145, 255);
        [SerializeField] private Color invalidPreviewBorderColor =
            new Color32(220, 75, 75, 255);
        [SerializeField, Min(1f)] private float previewBorderThickness = 6f;
        [SerializeField] private float hintBorderThickness = 7f;
        [SerializeField] private Color tutorialHighlightColor =
            new Color32(244, 188, 43, 255);
        [SerializeField, Min(1f)] private float tutorialHighlightThickness = 7f;

        private bool _tutorialHighlighted;

        private float _baseTopH, _baseBottomH, _baseLeftW, _baseRightW;
        private bool _cachedBorderSizes;

        [Header("Double Tap")]
        [SerializeField] private float doubleTapThreshold = 0.35f;

        [Header("Complete FX (children under Cell)")]
        [SerializeField] private Image completeSheen;
        [SerializeField, Range(0f, 0.9f)] private float completeSheenAlpha = 0f;
        [SerializeField, Range(0f, 0.5f)] private float completeSheenPulseAlpha = 0.18f;
        [SerializeField] private float completeSheenPulseDuration = 0.22f;
        [SerializeField, Min(2)] private int hintedCompleteSheenPulseCount = 3;
        [SerializeField, Min(0f)] private float hintedCompleteSheenPulseGap = 0.15f;
        [SerializeField] private RawImage completePattern;
        [SerializeField] private Texture2D completePaperTexture;
        [SerializeField] private bool showCompletePaper = true;
        [SerializeField, Range(0f, 0.25f)] private float completePaperAlpha = 0.1f;
        [SerializeField] private bool showCompletePattern = false;
        [SerializeField, Range(0f, 0.1f)] private float completePatternAlpha = 0.02f;
        // How many times the pattern tiles within one cell
        [SerializeField, Range(1f, 16f)] private float patternTiling = 6f;

        [Header("Press Visuals")]
        [SerializeField] private float blankTapAnimDuration = 0.18f;
        [SerializeField] private float blankTapDownScale = 0.94f;
        [SerializeField] private float blankTapDownY = -2f;

        [Header("Illegal Move Feedback")]
        [SerializeField] private Color illegalMoveColor =
            new Color(1f, 0.15f, 0.15f, 1f);

        [SerializeField] private float illegalMoveFlashDuration = 0.2f;

        private RectTransform _bgRect;
        private Vector2 _bgBaseAnchoredPos;
        private Vector3 _bgBaseScale;
        private Coroutine _blankTapRoutine;
        private Coroutine _illegalMoveRoutine;

        private static Texture2D s_completePatternTex;
        private static Sprite s_completeSheenSprite;
        private Coroutine _completeSheenRoutine;
        private bool _showRestingCompleteSheen;
        private bool _wasCompleteLastRender;
        private bool _hasRenderedOnce;

        private int _index;
        private PuzzleModel _model;
        private BoardController _board;
        private bool _isDarkTheme;

        private static bool _isPointerDown;
        private static int _activePointerId = int.MinValue;
        private static float _lastTapTime = -999f;
        private static int _lastTapCell = -1;

        private Material _anchorLabelMatInstance;

        private void EnsureCompletePatternTexture()
        {
            if (s_completePatternTex != null) return;

            const int size = 16;
            s_completePatternTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            s_completePatternTex.name = "CompletePatternTex";
            s_completePatternTex.wrapMode = TextureWrapMode.Repeat;
            s_completePatternTex.filterMode = FilterMode.Bilinear;

            // Transparent background
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    s_completePatternTex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));

            // Sparse diagonal dots
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (((x + y) % 8) == 0)
                        s_completePatternTex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                }
            }

            s_completePatternTex.Apply();
        }

        private void EnsureCompleteSheenSprite()
        {
            if (s_completeSheenSprite != null) return;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CompleteSheenSoft";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;

                    // One broad, feathered highlight. The slight curve keeps it
                    // from reading as a stripe or a disabled-state pattern.
                    float curve = 0.70f - (u * 0.34f) +
                                  (Mathf.Pow(u - 0.5f, 2f) * 0.18f);
                    float distance = Mathf.Abs(v - curve);
                    float band = 1f - Mathf.SmoothStep(0.10f, 0.48f, distance);

                    // Favor the upper-left like light falling across a raised
                    // paper tile, while leaving most of the face visible.
                    float falloff = Mathf.Lerp(0.68f, 1f, 1f - u);
                    float alpha = Mathf.Clamp01(band * falloff);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            s_completeSheenSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            s_completeSheenSprite.name = "CompleteSheenSoftSprite";
        }

        private void ApplyCompleteFx(bool enabled)
        {
            _showRestingCompleteSheen = enabled;

            if (!enabled)
            {
                if (_completeSheenRoutine != null)
                {
                    StopCoroutine(_completeSheenRoutine);
                    _completeSheenRoutine = null;
                }

                if (completeSheen != null)
                    completeSheen.enabled = false;

                if (completePattern != null)
                    completePattern.enabled = false;

                return;
            }

            // The curved sheen is animation-only. The subtle paper grain is
            // the resting completion cue.
            if (completeSheen != null && _completeSheenRoutine == null)
                completeSheen.enabled = false;

            if (completePattern == null)
                return;

            if (showCompletePaper && completePaperTexture != null)
            {
                int boardWidth = Mathf.Max(1, _board.Width);
                int boardHeight = Mathf.Max(1, _board.Height);
                int x = _index % boardWidth;
                int y = _index / boardWidth;

                // Each cell reveals its corresponding piece of one board-wide
                // paper sheet, so the texture flows continuously across a region.
                completePattern.enabled = true;
                completePattern.texture = completePaperTexture;
                completePattern.uvRect = new Rect(
                    (float)x / boardWidth,
                    1f - ((float)y + 1f) / boardHeight,
                    1f / boardWidth,
                    1f / boardHeight);
                completePattern.color =
                    new Color(1f, 1f, 1f, completePaperAlpha);
                return;
            }

            // Preserve the original generated pattern as a disabled fallback
            // until the paper treatment is approved.
            if (showCompletePattern)
            {
                float cellPx = 96f;
                var rt = transform as RectTransform;
                if (rt != null)
                {
                    float width = rt.rect.width * rt.lossyScale.x;
                    float height = rt.rect.height * rt.lossyScale.y;
                    cellPx = Mathf.Min(width, height);
                }

                float scale = Mathf.InverseLerp(35f, 120f, cellPx);
                float tiling = Mathf.Lerp(1.25f, patternTiling, scale);
                float patternAlpha =
                    Mathf.Lerp(0.05f, completePatternAlpha, scale);

                EnsureCompletePatternTexture();
                completePattern.enabled = true;
                completePattern.texture = s_completePatternTex;
                completePattern.uvRect =
                    new Rect(0f, 0f, tiling, tiling);
                completePattern.color =
                    new Color(1f, 1f, 1f, patternAlpha);
                return;
            }

            completePattern.enabled = false;
        }

        private System.Collections.IEnumerator PlayCompleteSheenPulse(
    int pulseCount = 1,
    float gapDuration = 0f)
        {
            if (completeSheen == null)
                yield break;

            EnsureCompleteSheenSprite();
            completeSheen.sprite = s_completeSheenSprite;
            completeSheen.type = Image.Type.Simple;

            // Reset opacity before enabling the delayed cell. Otherwise the
            // overlay can briefly display its prefab color at nearly full alpha.
            float baseSheenA = completeSheenAlpha;
            Color c = completeSheen.color;
            c.r = 1f;
            c.g = 1f;
            c.b = 1f;
            c.a = baseSheenA;
            completeSheen.color = c;
            completeSheen.enabled = true;

            float half = completeSheenPulseDuration * 0.5f;
            pulseCount = Mathf.Max(1, pulseCount);

            for (int pulse = 0; pulse < pulseCount; pulse++)
            {
                float elapsed = 0f;

                // Flash to peak brightness.
                while (elapsed < half)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(elapsed / half);

                    c.a = Mathf.Lerp(
                        baseSheenA,
                        completeSheenPulseAlpha,
                        k
                    );

                    completeSheen.color = c;
                    yield return null;
                }

                // Return to the normal completed-region brightness.
                elapsed = 0f;

                while (elapsed < half)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(elapsed / half);

                    c.a = Mathf.Lerp(
                        completeSheenPulseAlpha,
                        baseSheenA,
                        k
                    );

                    completeSheen.color = c;
                    yield return null;
                }

                // Pause at resting brightness between hint flashes.
                if (pulse < pulseCount - 1 && gapDuration > 0f)
                {
                    c.a = baseSheenA;
                    completeSheen.color = c;

                    yield return new WaitForSecondsRealtime(
                        gapDuration
                    );
                }
            }

            c.a = baseSheenA;
            completeSheen.color = c;
            completeSheen.enabled =
                _showRestingCompleteSheen && baseSheenA > 0.001f;
            _completeSheenRoutine = null;
        }

        public void PlayHintCompletionPulse()
        {
            if (completeSheen == null)
                return;

            if (_completeSheenRoutine != null)
                StopCoroutine(_completeSheenRoutine);

            _completeSheenRoutine = StartCoroutine(
                PlayCompleteSheenPulse(
                    hintedCompleteSheenPulseCount,
                    hintedCompleteSheenPulseGap
                )
            );
        }

        public void Init(int index, PuzzleModel model, BoardController board)
        {
            _index = index;
            _model = model;
            _board = board;
            _isDarkTheme = ThemeManager.IsDark;

            // Tile visual used for bevel flip + tap animation
            if (!bg)
                bg = transform.Find("TileVisual")?.GetComponent<Image>();

            if (bg != null)
            {
                _bgRect = bg.rectTransform;
                _bgBaseAnchoredPos = _bgRect.anchoredPosition;
                _bgBaseScale = _bgRect.localScale;
                _bgRect.localScale = _bgBaseScale;
                _bgRect.anchoredPosition = _bgBaseAnchoredPos;
                _bgRect.localEulerAngles = Vector3.zero;
            }
            else
            {
                Debug.LogError($"CellView on {name} could not find TileVisual Image.");
            }

            ConfigureTileSurface();

            if (!label) label = GetComponentInChildren<TMP_Text>();

            if (label) label.rectTransform.localScale = Vector3.one;

            // Make the label take the whole cell so large font sizes actually render
            if (label != null)
            {
                var rt = label.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // Auto-find the small color label by name (child under the cell)
            if (colorLabel == null)
                colorLabel = transform.Find("ColorLabel")?.GetComponent<TMP_Text>();

            if (colorLabel != null)
            {
                colorLabel.raycastTarget = false;

                // Center the color label
                var rt = colorLabel.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(120f, 120f);
                colorLabel.alignment = TextAlignmentOptions.Center;
            }

            // Auto-find Borders by name
            if (!top || !right || !bottom || !left)
            {
                var borders = transform.Find("Borders");
                if (borders != null)
                {
                    if (!top) top = borders.Find("Top")?.GetComponent<Image>();
                    if (!right) right = borders.Find("Right")?.GetComponent<Image>();
                    if (!bottom) bottom = borders.Find("Bottom")?.GetComponent<Image>();
                    if (!left) left = borders.Find("Left")?.GetComponent<Image>();
                }
            }

            ApplyBaseBorderThickness();

            // Auto-find completion FX by name (sheen only)
            if (completeSheen == null)
                completeSheen = transform.Find("CompleteSheen")?.GetComponent<Image>();

            if (completeSheen != null)
            {
                completeSheen.raycastTarget = false;
                EnsureCompleteSheenSprite();
                completeSheen.sprite = s_completeSheenSprite;
                completeSheen.type = Image.Type.Simple;
            }

            if (completePattern == null)
                completePattern = transform.Find("CompletePattern")?.GetComponent<RawImage>();

            // Don't let overlays block input
            if (completeSheen != null) completeSheen.raycastTarget = false;
            if (completePattern != null) completePattern.raycastTarget = false;

            // Reset transient animation state
            _wasCompleteLastRender = false;
            _hasRenderedOnce = false;
            if (_completeSheenRoutine != null)
            {
                StopCoroutine(_completeSheenRoutine);
                _completeSheenRoutine = null;
            }
            if (_blankTapRoutine != null)
            {
                StopCoroutine(_blankTapRoutine);
                _blankTapRoutine = null;
            }

            // Start disabled
            ApplyCompleteFx(false);

            DisableRaycast(top);
            DisableRaycast(right);
            DisableRaycast(bottom);
            DisableRaycast(left);

            // Ensure consistent draw state on start
            HardDisableAllBorders();
        }

        private void ConfigureTileSurface()
        {
            if (bg == null)
                return;

            bg.type = Image.Type.Simple;

            Shadow shadow = bg.GetComponent<Shadow>();
            if (shadow == null)
                shadow = bg.gameObject.AddComponent<Shadow>();

            shadow.effectColor = _isDarkTheme
                ? darkTileShadowColor
                : tileShadowColor;
            shadow.effectDistance = tileShadowOffset;
            shadow.useGraphicAlpha = true;

            Transform existing = bg.transform.Find("BevelOverlay");
            if (existing != null)
                _bevelOverlay = existing.GetComponent<Image>();

            if (_bevelOverlay == null)
            {
                GameObject overlayObject = new GameObject(
                    "BevelOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                overlayObject.layer = gameObject.layer;
                overlayObject.transform.SetParent(bg.transform, false);
                _bevelOverlay = overlayObject.GetComponent<Image>();
            }

            RectTransform overlayRect = _bevelOverlay.rectTransform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.localScale = Vector3.one;

            _bevelOverlay.sprite = bevelSprite;
            _bevelOverlay.type = Image.Type.Simple;
            ApplyBevelTint(bg.color);
            _bevelOverlay.raycastTarget = false;
            _bevelOverlay.enabled = bevelSprite != null;
            _bevelOverlay.transform.SetAsLastSibling();

            bg.SetVerticesDirty();
        }

        private void ApplyTileFill(Color fillColor)
        {
            if (bg == null)
                return;

            bg.color = fillColor;
            ApplyBevelTint(fillColor);
        }

        private void ApplyBevelTint(Color tileColor)
        {
            if (_bevelOverlay == null)
                return;

            tileColor.a = 1f;
            Color warmHighlight = _isDarkTheme
                ? new Color32(87, 147, 177, 255)
                : new Color32(250, 255, 255, 255);
            float highlightBlend = _isDarkTheme
                ? bevelHighlightBlend * 0.45f
                : bevelHighlightBlend;

            // Tint the bevel toward the current paper palette. Dark mode uses
            // a gentler lift so empty cells stay charcoal instead of turning gray.
            _bevelOverlay.color =
                Color.Lerp(tileColor, warmHighlight, highlightBlend);
        }

        private void ApplyBaseBorderThickness()
        {
            if (top != null)
            {
                Vector2 size = top.rectTransform.sizeDelta;
                size.y = baseBorderThickness;
                top.rectTransform.sizeDelta = size;
            }

            if (bottom != null)
            {
                Vector2 size = bottom.rectTransform.sizeDelta;
                size.y = baseBorderThickness;
                bottom.rectTransform.sizeDelta = size;
            }

            if (left != null)
            {
                Vector2 size = left.rectTransform.sizeDelta;
                size.x = baseBorderThickness;
                left.rectTransform.sizeDelta = size;
            }

            if (right != null)
            {
                Vector2 size = right.rectTransform.sizeDelta;
                size.x = baseBorderThickness;
                right.rectTransform.sizeDelta = size;
            }

            _cachedBorderSizes = false;
        }

        private void ApplyAnchorLabelStyle()
        {
            if (label == null) return;

            label.enableAutoSizing = false;
            label.fontSizeMin = anchorFontSize;
            label.fontSizeMax = anchorFontSize;

            label.fontSize = anchorFontSize;
            label.fontStyle = anchorBold ? FontStyles.Bold : FontStyles.Normal;
            label.color = anchorLabelColor;

            var rt = label.rectTransform;
            rt.localScale = Vector3.one;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (_anchorLabelMatInstance == null)
            {
                var shared = label.fontSharedMaterial;
                if (shared == null) return;
                _anchorLabelMatInstance = Instantiate(shared);
            }
            if (label.fontMaterial != _anchorLabelMatInstance)
                label.fontMaterial = _anchorLabelMatInstance;

            _anchorLabelMatInstance.SetFloat(ShaderUtilities.ID_OutlineWidth, anchorOutlineWidth);
            _anchorLabelMatInstance.SetColor(ShaderUtilities.ID_OutlineColor, anchorOutlineColor);
            _anchorLabelMatInstance.SetFloat(ShaderUtilities.ID_FaceDilate, anchorFaceDilate);

            label.ForceMeshUpdate();
        }

        private void DisableRaycast(Image img)
        {
            if (img != null) img.raycastTarget = false;
        }

        private void HardDisableAllBorders()
        {
            if (top) top.enabled = false;
            if (right) right.enabled = false;
            if (bottom) bottom.enabled = false;
            if (left) left.enabled = false;
        }

        private void ApplyPressedVisual(bool pressed)
        {
            if (bg == null) return;

            if (_bgRect == null)
                _bgRect = bg.rectTransform;

            _bgRect.localScale = _bgBaseScale;
            _bgRect.anchoredPosition = _bgBaseAnchoredPos;
        }

        private System.Collections.IEnumerator PlayBlankTapAnimation()
        {
            if (bg == null) yield break;

            if (_bgRect == null)
                _bgRect = bg.rectTransform;

            _bgRect.localScale = _bgBaseScale;
            _bgRect.anchoredPosition = _bgBaseAnchoredPos;

            float half = blankTapAnimDuration * 0.5f;
            float t = 0f;

            Vector3 downScale = _bgBaseScale * blankTapDownScale;
            Vector2 downPos = _bgBaseAnchoredPos + new Vector2(0f, blankTapDownY);

            // press down
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                _bgRect.localScale = Vector3.Lerp(_bgBaseScale, downScale, k);
                _bgRect.anchoredPosition = Vector2.Lerp(_bgBaseAnchoredPos, downPos, k);
                yield return null;
            }

            // release
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                _bgRect.localScale = Vector3.Lerp(downScale, _bgBaseScale, k);
                _bgRect.anchoredPosition = Vector2.Lerp(downPos, _bgBaseAnchoredPos, k);
                yield return null;
            }

            _bgRect.localScale = _bgBaseScale;
            _bgRect.anchoredPosition = _bgBaseAnchoredPos;
            _blankTapRoutine = null;
        }

        public void SetDarkTheme(bool isDark)
        {
            _isDarkTheme = isDark;

            if (bg != null)
            {
                Shadow shadow = bg.GetComponent<Shadow>();
                if (shadow != null)
                {
                    shadow.effectColor = _isDarkTheme
                        ? darkTileShadowColor
                        : tileShadowColor;
                }
            }

            if (_board != null)
                Render();
        }

        public void SetTutorialHighlight(bool highlighted)
        {
            _tutorialHighlighted = highlighted;
            Render();
        }

        public void Render()
        {
            HardDisableAllBorders();
            SetHintThickness(false);

            bool isClue = _board.IsAnchorCell(_index);
            int regionId = _board.RegionIdAt(_index);
            bool isAssigned = regionId >= 0;
            bool isSelected = isAssigned && _board.IsRegionSelectedAt(_index);
            bool isValid = isAssigned && _board.IsRegionValidAt(_index);
            bool isDraft = _board.IsDraftCell(_index);

            if (label != null)
            {
                label.text = "";
                label.alpha = 0f;
            }
            if (colorLabel != null)
            {
                colorLabel.text = "";
                colorLabel.alpha = 0f;
            }

            Color fill = _isDarkTheme ? darkBlankCellColor : blankCellColor;
            if (isAssigned)
            {
                Color blueprintBase = _isDarkTheme
                    ? new Color32(18, 61, 91, 255)
                    : new Color32(218, 238, 242, 255);
                Color identity = _board.Palette != null
                    ? _board.Palette.GetColorForNumber(
                        _board.RegionPaletteIndexAt(_index))
                    : (Color)new Color32(45, 145, 180, 255);
                float identityBlend = _isDarkTheme ? 0.08f : 0.12f;
                fill = Color.Lerp(blueprintBase, identity, identityBlend);
                fill.a = 1f;
            }
            if (isDraft)
            {
                float blend = Mathf.Clamp01(previewFillColor.a);
                fill = Color.Lerp(fill, new Color(
                    previewFillColor.r,
                    previewFillColor.g,
                    previewFillColor.b,
                    1f), blend);
            }

            ApplyTileFill(fill);
            ApplyPressedVisual(isAssigned);

            if (isClue && label != null)
            {
                ApplyAnchorLabelStyle();
                label.text = _board.GivenNumberAt(_index).ToString();
                label.alpha = 1f;
                float luminance = (fill.r * 0.299f) + (fill.g * 0.587f) + (fill.b * 0.114f);
                label.color = luminance > 0.58f
                    ? new Color32(47, 42, 35, 255)
                    : new Color32(238, 247, 248, 255);
            }

            bool showCompleteFx = isValid && !isSelected && !isDraft;
            ApplyCompleteFx(showCompleteFx);
            if (_hasRenderedOnce && isValid && !_wasCompleteLastRender)
            {
                if (_completeSheenRoutine != null)
                    StopCoroutine(_completeSheenRoutine);
                _completeSheenRoutine = StartCoroutine(PlayCompleteSheenPulse());
            }
            _wasCompleteLastRender = isValid;
            _hasRenderedOnce = true;

            if (isDraft)
                DrawDraftOutlineStrict();
            else if (isAssigned)
                DrawRegionOutlineStrict(regionId, isSelected, isValid);

            ApplyWrongHintBorderIfNeeded();
            ApplyTutorialHighlight();
        }
        private void ApplyTutorialHighlight()
        {
            if (!_tutorialHighlighted)
                return;

            SetBorderThickness(tutorialHighlightThickness);
            Enable(top); Enable(right); Enable(bottom); Enable(left);
            ApplyBorderColor(tutorialHighlightColor);
        }

        private void DrawRegionOutlineStrict(int regionId, bool selected, bool valid)
        {
            SetBorderThickness(selected ? selectedCellBorderThickness : baseBorderThickness);
            if (!NeighborInSameRegion(regionId, _index, 0, -1)) Enable(top);
            if (!NeighborInSameRegion(regionId, _index, 1, 0)) Enable(right);
            if (!NeighborInSameRegion(regionId, _index, 0, 1)) Enable(bottom);
            if (!NeighborInSameRegion(regionId, _index, -1, 0)) Enable(left);
            ApplyBorderColor(!valid
                ? invalidRegionBorderColor
                : selected ? selectedCellBorderColor : borderColor);
        }

        private void DrawDraftOutlineStrict()
        {
            SetBorderThickness(previewBorderThickness);
            if (!_board.IsDraftNeighbor(_index, 0, -1)) Enable(top);
            if (!_board.IsDraftNeighbor(_index, 1, 0)) Enable(right);
            if (!_board.IsDraftNeighbor(_index, 0, 1)) Enable(bottom);
            if (!_board.IsDraftNeighbor(_index, -1, 0)) Enable(left);
            ApplyBorderColor(_board.DraftIsGeometricallyValid
                ? previewBorderColor
                : invalidPreviewBorderColor);
        }
        private void SetBorderThickness(float thickness)
        {
            if (top != null)
            {
                Vector2 size = top.rectTransform.sizeDelta;
                size.y = thickness;
                top.rectTransform.sizeDelta = size;
            }

            if (bottom != null)
            {
                Vector2 size = bottom.rectTransform.sizeDelta;
                size.y = thickness;
                bottom.rectTransform.sizeDelta = size;
            }

            if (left != null)
            {
                Vector2 size = left.rectTransform.sizeDelta;
                size.x = thickness;
                left.rectTransform.sizeDelta = size;
            }

            if (right != null)
            {
                Vector2 size = right.rectTransform.sizeDelta;
                size.x = thickness;
                right.rectTransform.sizeDelta = size;
            }
        }

        private void Enable(Image img)
        {
            if (img != null) img.enabled = true;
        }

        private void ApplyBorderColor(Color c)
        {
            if (top && top.enabled) top.color = c;
            if (right && right.enabled) right.color = c;
            if (bottom && bottom.enabled) bottom.color = c;
            if (left && left.enabled) left.color = c;
        }

        private bool NeighborInSameRegion(int compId, int index, int dx, int dy)
        {
            int w = _board.Width;
            int h = _board.Height;

            int x = index % w;
            int y = index / w;

            int nx = x + dx;
            int ny = y + dy;

            if (nx < 0 || nx >= w || ny < 0 || ny >= h) return false;

            int nIndex = ny * w + nx;

            // Empty cells have compId -1, so this naturally returns false on edges to empty
            return _board.RegionIdAt(nIndex) == compId;
        }

        // Input is transactional: pointer movement only updates the preview.
        public static void CancelPointerInput()
        {
            _isPointerDown = false;
            _activePointerId = int.MinValue;
            _lastTapCell = -1;
            _lastTapTime = -999f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_board == null || _board.InputLocked ||
                (_isPointerDown && eventData.pointerId != _activePointerId))
                return;

            _board.NotifyBoardTouched();
            bool isAssigned = _board.IsCellAssigned(_index);
            float now = Time.unscaledTime;
            bool isDoubleTap = isAssigned && _lastTapCell == _index &&
                               now - _lastTapTime <= doubleTapThreshold;
            if (isDoubleTap)
            {
                _board.OnDoubleTapCell(_index);
                CancelPointerInput();
                return;
            }

            _lastTapCell = _index;
            _lastTapTime = now;
            _isPointerDown = true;
            _activePointerId = eventData.pointerId;
            _board.BeginStroke();
            _board.OnPointerDownCell(_index, eventData.pointerId);

            if (!isAssigned)
            {
                if (_blankTapRoutine != null)
                    StopCoroutine(_blankTapRoutine);
                _blankTapRoutine = StartCoroutine(PlayBlankTapAnimation());
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_board == null || _board.InputLocked || !_isPointerDown ||
                eventData.pointerId != _activePointerId)
                return;
            _board.OnPointerEnterCell(_index, eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPointerDown || eventData.pointerId != _activePointerId)
                return;
            GameObject releaseObject = eventData.pointerCurrentRaycast.gameObject;
            CellView releaseCell = releaseObject != null
                ? releaseObject.GetComponentInParent<CellView>()
                : null;
            int releaseCellIndex = releaseCell != null && releaseCell._board == _board
                ? releaseCell._index
                : -1;
            _board?.OnPointerUpCell(
                eventData.pointerId,
                eventData.position,
                eventData.pressEventCamera,
                releaseCellIndex);
            _isPointerDown = false;
            _activePointerId = int.MinValue;
        }

        public void OnCancel(BaseEventData eventData)
        {
            _board?.CancelRegionDrag();
            CancelPointerInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _board?.CancelRegionDrag();
                CancelPointerInput();
            }
        }
        private void Update()
        {
            if (_board == null) return;
            if (!_board.IsHintCell(_index)) return;

            // Re-render just this cell so flash animates without rebuilding whole board
            Render();
        }

        private void ApplyWrongHintBorderIfNeeded()
        {
            if (_board == null) return;

            bool isWrongHint = _board.IsHintCell(_index) && _board.HintIsWrongFilled;

            if (!isWrongHint)
            {
                // Render already restored the base thickness before drawing
                // selection, so leave a selected outline at its stronger size.
                return;
            }

            // Wrong-filled: thick flashing red outline
            SetHintThickness(true);

            float a = _board.GetHintAlpha();
            var c = Color.red;
            c.a = a;

            Enable(top); Enable(right); Enable(bottom); Enable(left);
            ApplyBorderColor(c);
        }

        private Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private void CacheBorderSizesIfNeeded()
        {
            if (_cachedBorderSizes) return;
            _cachedBorderSizes = true;

            if (top != null) _baseTopH = top.rectTransform.sizeDelta.y;
            if (bottom != null) _baseBottomH = bottom.rectTransform.sizeDelta.y;
            if (left != null) _baseLeftW = left.rectTransform.sizeDelta.x;
            if (right != null) _baseRightW = right.rectTransform.sizeDelta.x;
        }

        private void SetHintThickness(bool on)
        {
            CacheBorderSizesIfNeeded();

            if (top != null)
            {
                var sd = top.rectTransform.sizeDelta;
                sd.y = on ? hintBorderThickness : _baseTopH;
                top.rectTransform.sizeDelta = sd;
            }
            if (bottom != null)
            {
                var sd = bottom.rectTransform.sizeDelta;
                sd.y = on ? hintBorderThickness : _baseBottomH;
                bottom.rectTransform.sizeDelta = sd;
            }
            if (left != null)
            {
                var sd = left.rectTransform.sizeDelta;
                sd.x = on ? hintBorderThickness : _baseLeftW;
                left.rectTransform.sizeDelta = sd;
            }
            if (right != null)
            {
                var sd = right.rectTransform.sizeDelta;
                sd.x = on ? hintBorderThickness : _baseRightW;
                right.rectTransform.sizeDelta = sd;
            }
        }

        private void UpdateColorLabelFontSize()
        {
            if (colorLabel == null) return;

            var rt = (RectTransform)transform;
            float cellW = rt.rect.width;
            if (cellW <= 0.01f) return;

            float target = cellW * colorLabelSizeFactor;
            colorLabel.fontSize = Mathf.Clamp(target, colorLabelMinSize, colorLabelMaxSize);
        }

        public void PlayIllegalMoveFeedback()
        {
            if (_illegalMoveRoutine != null)
                StopCoroutine(_illegalMoveRoutine);

            _illegalMoveRoutine =
                StartCoroutine(PlayIllegalMoveFlash());
        }

        private System.Collections.IEnumerator PlayIllegalMoveFlash()
        {
            if (bg == null)
                yield break;

            ApplyTileFill(illegalMoveColor);

            float elapsed = 0f;

            while (elapsed < illegalMoveFlashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Restore whatever color the cell should normally have.
            Render();

            _illegalMoveRoutine = null;
        }
    }
}
