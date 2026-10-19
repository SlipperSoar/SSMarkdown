using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SS.UIComponent
{
    [Serializable]
    public sealed class MarkdownLinkEvent : UnityEvent<string>
    {
    }

    /// <summary>
    /// Runtime Markdown renderer for Unity UI. Assign <see cref="text"/> (or a
    /// TextAsset) exactly like a UGUI Text and the component parses and lays out
    /// headings, paragraphs, emphasis, links, lists, quotes and code blocks.
    /// </summary>
    [AddComponentMenu("UI/Markdown Text", 12)]
    [DefaultExecutionOrder(1000)]
    public sealed partial class MarkdownText : MaskableGraphic, ILayoutElement, IPointerClickHandler
    {
        [SerializeField, TextArea(4, 24)] private string m_Text = string.Empty;
        [SerializeField] private Font m_Font;
        [SerializeField, Min(1)] private int m_FontSize = 16;
        [SerializeField] private TextAnchor m_Alignment = TextAnchor.UpperLeft;
        [SerializeField] private Vector4 m_Padding = new Vector4(2f, 2f, 2f, 2f);
        [SerializeField] private MarkdownTheme m_Theme = new MarkdownTheme();
        [SerializeField] private bool m_EnableLinks = true;
        [SerializeField, Min(0f)] private float m_MaxImageWidth = 0f;
        [SerializeField, Min(0f)] private float m_MaxImageHeight = 480f;
        [SerializeField] private MarkdownLinkEvent m_OnLinkClicked = new MarkdownLinkEvent();
        [SerializeField] private MarkdownLinkEvent m_OnFootnoteClicked = new MarkdownLinkEvent();

        [NonSerialized] private MarkdownDocument m_Document;
        [NonSerialized] private bool m_DocumentDirty = true;
        [NonSerialized] private LayoutResult m_Layout;
        [NonSerialized] private bool m_LayoutDirty = true;
        [NonSerialized] private float m_LastLayoutWidth = float.NaN;
        [NonSerialized] private float m_LastLayoutHeight = float.NaN;
        [NonSerialized] private float m_LastPixelsPerUnit = float.NaN;
        [NonSerialized] private Color m_LastColor;
        [NonSerialized] private Vector2 m_LastPivot;
        [NonSerialized] private Font m_LastFont;
        [NonSerialized] private int m_LastFontSize = -1;
        [NonSerialized] private TextAnchor m_LastAlignment = (TextAnchor)(-1);
        [NonSerialized] private Vector4 m_LastPadding;
        [NonSerialized] private bool m_LastEnableLinks;

        [NonSerialized] private readonly TextGenerator m_Generator = new TextGenerator();
        [NonSerialized] private readonly TextGenerator m_MarkerGenerator = new TextGenerator();
        [NonSerialized] private readonly UIVertex[] m_TempVerts = new UIVertex[4];
        [NonSerialized] private MarkdownDecorationLayer m_Decoration;
        [NonSerialized] private List<MarkdownSolidRect> m_PendingSolids;
        [NonSerialized] private bool m_DisableFontTextureRebuiltCallback;
        [NonSerialized] private bool m_BuildingLayout;
        [NonSerialized] private bool m_SyncingDecoration;

        private static Font s_DefaultFont;

        /// <summary>Raised when the user clicks a link. The argument is the destination URL.</summary>
        public MarkdownLinkEvent onLinkClicked
        {
            get => m_OnLinkClicked;
            set => m_OnLinkClicked = value ?? new MarkdownLinkEvent();
        }

        /// <summary>Raised when a footnote reference is clicked. The argument is the footnote id.</summary>
        public MarkdownLinkEvent onFootnoteClicked
        {
            get => m_OnFootnoteClicked;
            set => m_OnFootnoteClicked = value ?? new MarkdownLinkEvent();
        }

        /// <summary>Markdown source. Assigning a new value triggers a reparse and relayout.</summary>
        public string text
        {
            get => m_Text ?? string.Empty;
            set
            {
                string next = value ?? string.Empty;
                if (string.Equals(m_Text, next, StringComparison.Ordinal))
                    return;

                m_Text = next;
                m_DocumentDirty = true;
                MarkDirty();
            }
        }

        public Font font
        {
            get => m_Font;
            set
            {
                if (m_Font == value)
                    return;
                m_Font = value;
                MarkDirty();
            }
        }

        public int fontSize
        {
            get => m_FontSize;
            set
            {
                int next = Mathf.Max(1, value);
                if (m_FontSize == next)
                    return;
                m_FontSize = next;
                MarkDirty();
            }
        }

        /// <summary>Horizontal alignment of each block; vertical alignment of the whole document.</summary>
        public TextAnchor alignment
        {
            get => m_Alignment;
            set
            {
                if (m_Alignment == value)
                    return;
                m_Alignment = value;
                MarkDirty();
            }
        }

        /// <summary>Left, top, right and bottom padding in UI units.</summary>
        public Vector4 padding
        {
            get => m_Padding;
            set
            {
                if (m_Padding == value)
                    return;
                m_Padding = value;
                MarkDirty();
            }
        }

        public MarkdownTheme theme
        {
            get
            {
                if (m_Theme == null)
                    m_Theme = new MarkdownTheme();
                return m_Theme;
            }
            set
            {
                m_Theme = value ?? new MarkdownTheme();
                MarkDirty();
            }
        }

        public bool enableLinks
        {
            get => m_EnableLinks;
            set
            {
                if (m_EnableLinks == value)
                    return;
                m_EnableLinks = value;
                MarkDirty();
            }
        }

        /// <summary>Maximum rendered image width. 0 means the available content width.</summary>
        public float maxImageWidth
        {
            get => m_MaxImageWidth;
            set
            {
                float next = Mathf.Max(0f, value);
                if (Mathf.Approximately(m_MaxImageWidth, next))
                    return;
                m_MaxImageWidth = next;
                MarkDirty();
            }
        }

        /// <summary>Maximum rendered image height. 0 means unlimited.</summary>
        public float maxImageHeight
        {
            get => m_MaxImageHeight;
            set
            {
                float next = Mathf.Max(0f, value);
                if (Mathf.Approximately(m_MaxImageHeight, next))
                    return;
                m_MaxImageHeight = next;
                MarkDirty();
            }
        }

        /// <summary>The currently cached parsed document. Accessing this parses on demand.</summary>
        public MarkdownDocument document
        {
            get
            {
                if (m_DocumentDirty || m_Document == null)
                {
                    m_Document = new MarkdownParser().Parse(m_Text);
                    m_DocumentDirty = false;
                }
                return m_Document;
            }
        }

        /// <summary>Total laid-out document height in UI units, including padding.</summary>
        public float contentHeight
        {
            get
            {
                LayoutResult layout = EnsureLayout(rectTransform.rect.width, 0f);
                return layout != null ? layout.Height : 0f;
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (m_Font != null && m_Font.material != null && m_Font.material.mainTexture != null)
                    return m_Font.material.mainTexture;
                return base.mainTexture;
            }
        }

        /// <summary>Assign Markdown text. Alias for the <see cref="text"/> property.</summary>
        public void SetMarkdown(string markdown)
        {
            text = markdown;
        }

        /// <summary>Assign the contents of a TextAsset.</summary>
        public void SetMarkdown(TextAsset asset)
        {
            text = asset != null ? asset.text : string.Empty;
        }

        /// <summary>Replace the current theme and rebuild.</summary>
        public void ApplyTheme(MarkdownTheme value)
        {
            if (value == null)
                return;

            Color body = value.bodyColor;
            body.a *= color.a;
            theme.CopyFrom(value);
            color = body;
            MarkDirty();
        }

        /// <summary>Apply the built-in light documentation theme.</summary>
        public void UseLightTheme()
        {
            ApplyTheme(MarkdownTheme.CreateLight());
        }

        /// <summary>Apply the built-in dark documentation theme.</summary>
        public void UseDarkTheme()
        {
            ApplyTheme(MarkdownTheme.CreateDark());
        }

        /// <summary>Force a reparse and relayout (for example after editing a custom theme in place).</summary>
        public void Refresh()
        {
            m_DocumentDirty = true;
            MarkDirty();
        }

        /// <summary>Return the link URL under a screen point, or null when there is none.</summary>
        public string GetLinkAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
        {
            if (!m_EnableLinks)
                return null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, eventCamera, out Vector2 local))
                return null;

            return TryGetLinkAtLocalPosition(local, out string url) ? url : null;
        }

        /// <summary>Return the link URL under a point in this RectTransform's local space.</summary>
        public bool TryGetLinkAtLocalPosition(Vector2 localPoint, out string url)
        {
            url = null;
            if (!m_EnableLinks)
                return false;

            LayoutResult layout = EnsureLayout(rectTransform.rect.width, rectTransform.rect.height);
            if (layout == null || layout.Links.Count == 0)
                return false;

            foreach (KeyValuePair<string, LinkRegion> pair in layout.Links)
            {
                List<Rect> rects = pair.Value.Rects;
                for (int i = 0; i < rects.Count; i++)
                {
                    if (rects[i].Contains(localPoint))
                    {
                        url = pair.Key;
                        return true;
                    }
                }
            }

            return false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!m_EnableLinks || eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return;

            string url = GetLinkAtScreenPosition(eventData.position, eventData.pressEventCamera);
            if (string.IsNullOrEmpty(url))
                return;

            if (url.StartsWith("#footnote-", StringComparison.Ordinal))
                m_OnFootnoteClicked.Invoke(url.Substring("#footnote-".Length));
            else
                m_OnLinkClicked.Invoke(url);
        }

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }

        public float minWidth => 0f;

        /// <summary>
        /// Markdown documents are width-driven. A horizontal layout group should
        /// give this graphic the available width rather than trying to measure a
        /// single unwrapped line.
        /// </summary>
        public float preferredWidth => 0f;

        public float flexibleWidth => 1f;

        public float minHeight => 0f;

        public float preferredHeight
        {
            get
            {
                LayoutResult layout = EnsureLayout(rectTransform.rect.width, 0f);
                return layout != null ? layout.Height : 0f;
            }
        }

        public float flexibleHeight => -1f;

        public int layoutPriority => 0;

        protected override void Awake()
        {
            base.Awake();
            EnsureFont();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureFont();
            EnsureDecorationLayer();
            SetImagesVisible(true);
            m_DocumentDirty = true;
            m_LayoutDirty = true;
            Font.textureRebuilt += OnFontTextureRebuilt;
            MarkDirty();
        }

        protected override void OnDisable()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            if (m_Decoration != null)
                m_Decoration.enabled = false;
            SetImagesVisible(false);
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            DisposeImageResources();
            // The decoration layer is a sibling rather than a child. It watches
            // its owner reference and destroys itself, which avoids double
            // destruction while a parent Canvas is being torn down.
            base.OnDestroy();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkDirty();
        }

        private void LateUpdate()
        {
            // ScrollRect moves the content RectTransform every frame. The
            // decoration layer is a sibling (so it can render behind the font
            // layer), therefore it has to follow the content transform.
            if (m_Decoration == null)
                EnsureDecorationLayer();

            if (m_Decoration != null)
            {
                SyncDecorationLayer();
                if (m_PendingSolids != null)
                {
                    m_Decoration.SetQuads(m_PendingSolids);
                    m_Decoration.enabled = isActiveAndEnabled;
                    m_PendingSolids = null;
                }
            }

            SyncImagePlacements();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (!CanvasUpdateRegistry.IsRebuildingGraphics() && !CanvasUpdateRegistry.IsRebuildingLayout())
            {
                if (m_Decoration != null)
                {
                    if (Application.isPlaying)
                        Destroy(m_Decoration.gameObject);
                    else
                        DestroyImmediate(m_Decoration.gameObject);
                    m_Decoration = null;
                }
                EnsureDecorationLayer();
            }
            MarkDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (m_Font == null)
            {
                PushDecorations(null);
                return;
            }

            Rect rect = rectTransform.rect;
            LayoutResult layout = EnsureLayout(rect.width, rect.height);
            if (layout == null)
                return;

            for (int i = 0; i < layout.Glyphs.Count; i++)
            {
                GlyphQuad glyph = layout.Glyphs[i];
                m_TempVerts[0] = glyph.V0;
                m_TempVerts[1] = glyph.V1;
                m_TempVerts[2] = glyph.V2;
                m_TempVerts[3] = glyph.V3;
                vh.AddUIVertexQuad(m_TempVerts);
            }
        }

        protected override void Reset()
        {
            base.Reset();
            m_Font = GetDefaultFont();
            m_Theme = new MarkdownTheme();
            m_Padding = new Vector4(2f, 2f, 2f, 2f);
            color = new Color32(36, 41, 47, 255);
            MarkDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_FontSize = Mathf.Max(1, m_FontSize);
            if (m_Theme == null)
                m_Theme = new MarkdownTheme();
            m_Theme.Validate();
            m_DocumentDirty = true;
            MarkDirty();
        }
#endif

        private void MarkDirty()
        {
            m_LayoutDirty = true;
            m_Layout = null;
            SetVerticesDirty();
            SetLayoutDirty();
        }

        private LayoutResult EnsureLayout(float width, float height)
        {
            float pixelsPerUnit = PixelsPerUnit;
            if (m_Font == null)
                return null;

            if (!m_LayoutDirty &&
                m_Layout != null &&
                Mathf.Approximately(m_LastLayoutWidth, width) &&
                Mathf.Approximately(m_LastLayoutHeight, height) &&
                Mathf.Approximately(m_LastPixelsPerUnit, pixelsPerUnit) &&
                m_LastColor == color &&
                m_LastPivot == rectTransform.pivot &&
                m_LastFont == m_Font &&
                m_LastFontSize == m_FontSize &&
                m_LastAlignment == m_Alignment &&
                m_LastPadding == m_Padding &&
                m_LastEnableLinks == m_EnableLinks)
            {
                return m_Layout;
            }

            m_LastLayoutWidth = width;
            m_LastLayoutHeight = height;
            m_LastPixelsPerUnit = pixelsPerUnit;
            m_LastColor = color;
            m_LastPivot = rectTransform.pivot;
            m_LastFont = m_Font;
            m_LastFontSize = m_FontSize;
            m_LastAlignment = m_Alignment;
            m_LastPadding = m_Padding;
            m_LastEnableLinks = m_EnableLinks;

            m_Layout = BuildLayout(Mathf.Max(1f, width), height, pixelsPerUnit);
            m_LayoutDirty = false;

            PushDecorations(m_Layout);
            QueueImagePlacements(m_Layout.Images);
            return m_Layout;
        }

        private LayoutResult BuildLayout(float width, float height, float pixelsPerUnit)
        {
            var result = new LayoutResult { Width = width };
            theme.Validate();
            MarkdownDocument doc = document;
            if (doc == null || doc.Blocks.Count == 0)
            {
                result.Height = 0f;
                AddDocumentBackground(result, width, height, 0f);
                result.Offset(
                    -rectTransform.pivot.x * width + padding.x,
                    (1f - rectTransform.pivot.y) * height - padding.y);
                return result;
            }

            m_DisableFontTextureRebuiltCallback = true;
            m_BuildingLayout = true;
            try
            {
                float availableWidth = Mathf.Max(1f, width - padding.x - padding.z);
                float y = 0f;

                for (int i = 0; i < doc.Blocks.Count; i++)
                {
                    MarkdownBlock block = doc.Blocks[i];
                    y += GetBlockGap(doc.Blocks, i);
                    float blockHeight = BuildBlock(result, block, y, availableWidth, pixelsPerUnit);
                    if (blockHeight > 0f)
                        y += blockHeight;
                }

                result.Height = y + padding.y + padding.w;

                float extra = height - result.Height;
                float verticalShift = 0f;
                if (extra > 0f)
                {
                    if (m_Alignment == TextAnchor.MiddleLeft || m_Alignment == TextAnchor.MiddleCenter || m_Alignment == TextAnchor.MiddleRight)
                        verticalShift = extra * 0.5f;
                    else if (m_Alignment == TextAnchor.LowerLeft || m_Alignment == TextAnchor.LowerCenter || m_Alignment == TextAnchor.LowerRight)
                        verticalShift = extra;
                }

                AddDocumentBackground(result, width, height, result.Height, verticalShift);

                float originX = -rectTransform.pivot.x * width + padding.x;
                float originY = (1f - rectTransform.pivot.y) * height - padding.y - verticalShift;
                result.Offset(originX, originY);
            }
            finally
            {
                m_DisableFontTextureRebuiltCallback = false;
                m_BuildingLayout = false;
            }

            return result;
        }

        private float BuildBlock(
            LayoutResult result,
            MarkdownBlock block,
            float top,
            float availableWidth,
            float pixelsPerUnit)
        {
            switch (block.Type)
            {
                case MarkdownBlockType.Heading:
                    return BuildHeading(result, block, top, availableWidth, pixelsPerUnit);
                case MarkdownBlockType.CodeBlock:
                    return BuildCodeBlock(result, block, top, availableWidth, pixelsPerUnit);
                case MarkdownBlockType.Table:
                    return BuildTable(result, block, top, availableWidth, pixelsPerUnit);
                case MarkdownBlockType.Image:
                    return BuildImageBlock(result, block, top, availableWidth, pixelsPerUnit);
                case MarkdownBlockType.Footnote:
                    return BuildFlowBlock(result, block, top, availableWidth, pixelsPerUnit, false);
                case MarkdownBlockType.ThematicBreak:
                    AddSolidNormalized(
                        result,
                        new Rect(0f, -top - theme.ruleThickness, availableWidth, theme.ruleThickness),
                        ThemeColor(theme.ruleColor));
                    return theme.ruleThickness;
                case MarkdownBlockType.ListItem:
                    return BuildFlowBlock(result, block, top, availableWidth, pixelsPerUnit, true);
                case MarkdownBlockType.BlockQuote:
                    return BuildFlowBlock(result, block, top, availableWidth, pixelsPerUnit, false);
                default:
                    return BuildFlowBlock(result, block, top, availableWidth, pixelsPerUnit, false);
            }
        }

        private float BuildHeading(
            LayoutResult result,
            MarkdownBlock block,
            float top,
            float availableWidth,
            float pixelsPerUnit)
        {
            int level = Mathf.Clamp(block.Level, 1, 6);
            float scale = theme.headingScales[level - 1];
            int size = Mathf.Max(1, Mathf.RoundToInt(m_FontSize * scale));

            MarkdownRichText rich = MarkdownRichTextBuilder.Build(
                block,
                theme,
                true,
                ThemeColor(theme.linkColor),
                ThemeColor(theme.codeColor));

            TextGenerationSettings settings = CreateSettings(
                size,
                availableWidth,
                ToUpperAnchor(m_Alignment),
                ThemeColor(theme.headingColor));

            m_Generator.Populate(rich.Markup, settings);
            float textHeight = GetTextHeight(m_Generator, pixelsPerUnit);
            if (textHeight <= 0f)
                textHeight = size * theme.lineSpacing;

            AppendGeneratedGlyphs(
                result,
                m_Generator,
                rich,
                block.Inlines,
                0f,
                top,
                pixelsPerUnit,
                true);

            float totalHeight = textHeight;
            if (theme.drawHeadingRules && level <= 2)
            {
                float ruleTop = top + textHeight + 3f;
                AddSolidNormalized(
                    result,
                    new Rect(0f, -ruleTop - theme.ruleThickness, availableWidth, theme.ruleThickness),
                    ThemeColor(theme.ruleColor));
                totalHeight += 3f + theme.ruleThickness;
            }

            AddQuoteBars(result, block, top, totalHeight);
            return totalHeight;
        }

        private float BuildTable(
            LayoutResult result,
            MarkdownBlock block,
            float top,
            float availableWidth,
            float pixelsPerUnit)
        {
            MarkdownTable table = block.Table;
            if (table == null || table.ColumnCount == 0)
                return 0f;

            int columns = table.ColumnCount;
            int size = Mathf.Max(1, m_FontSize);
            float left = block.QuoteDepth * theme.quoteIndent + block.ListDepth * theme.listIndent;
            float tableWidth = Mathf.Max(1f, availableWidth - left);
            float padding = theme.tableCellPadding;

            float[] widths = ComputeTableColumnWidths(table, size, tableWidth, pixelsPerUnit);
            float actualWidth = 0f;
            for (int c = 0; c < columns; c++)
                actualWidth += widths[c];

            float headerHeight = MeasureTableRow(table.Header, widths, size, true, pixelsPerUnit);
            var rowHeights = new float[table.Rows.Count];
            for (int r = 0; r < table.Rows.Count; r++)
                rowHeights[r] = MeasureTableRow(table.Rows[r], widths, size, false, pixelsPerUnit);

            float tableHeight = headerHeight;
            for (int r = 0; r < rowHeights.Length; r++)
                tableHeight += rowHeights[r];

            float headerTop = top;
            AddSolidNormalized(
                result,
                new Rect(left, -headerTop - headerHeight, actualWidth, headerHeight),
                ThemeColor(theme.tableHeaderBackgroundColor));

            float rowTop = headerTop + headerHeight;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                if ((r & 1) == 1)
                {
                    AddSolidNormalized(
                        result,
                        new Rect(left, -rowTop - rowHeights[r], actualWidth, rowHeights[r]),
                        ThemeColor(theme.tableRowBackgroundColor));
                }
                rowTop += rowHeights[r];
            }

            AddTableBorders(result, left, top, actualWidth, tableHeight, widths, headerHeight, rowHeights);

            float columnX = left;
            for (int c = 0; c < columns; c++)
            {
                DrawTableCell(
                    result,
                    table.Header[c],
                    columnX,
                    headerTop,
                    widths[c],
                    headerHeight,
                    size,
                    true,
                    table.Alignments[c],
                    ThemeColor(theme.tableHeaderTextColor),
                    pixelsPerUnit);
                columnX += widths[c];
            }

            rowTop = headerTop + headerHeight;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                columnX = left;
                for (int c = 0; c < columns; c++)
                {
                    DrawTableCell(
                        result,
                        table.Rows[r][c],
                        columnX,
                        rowTop,
                        widths[c],
                        rowHeights[r],
                        size,
                        false,
                        table.Alignments[c],
                        color,
                        pixelsPerUnit);
                    columnX += widths[c];
                }
                rowTop += rowHeights[r];
            }

            AddQuoteBars(result, block, top, tableHeight);
            return tableHeight;
        }

        private float[] ComputeTableColumnWidths(
            MarkdownTable table,
            int fontSize,
            float tableWidth,
            float pixelsPerUnit)
        {
            int columns = table.ColumnCount;
            var preferred = new float[columns];
            float padding = theme.tableCellPadding * 2f;

            for (int c = 0; c < columns; c++)
            {
                float width = theme.tableMinColumnWidth;
                width = Mathf.Max(width, MeasureTableCellWidth(table.Header[c], fontSize, true, pixelsPerUnit) + padding);
                for (int r = 0; r < table.Rows.Count; r++)
                    width = Mathf.Max(width, MeasureTableCellWidth(table.Rows[r][c], fontSize, false, pixelsPerUnit) + padding);
                preferred[c] = Mathf.Min(width, tableWidth);
            }

            float total = 0f;
            for (int c = 0; c < columns; c++)
                total += preferred[c];

            if (total <= 0f)
            {
                for (int c = 0; c < columns; c++)
                    preferred[c] = tableWidth / columns;
                total = tableWidth;
            }

            var widths = new float[columns];
            if (total <= tableWidth)
            {
                float extra = tableWidth - total;
                for (int c = 0; c < columns; c++)
                    widths[c] = preferred[c] + extra * (preferred[c] / total);
                return widths;
            }

            float scale = tableWidth / total;
            for (int c = 0; c < columns; c++)
                widths[c] = Mathf.Max(theme.tableMinColumnWidth, preferred[c] * scale);

            float sum = 0f;
            for (int c = 0; c < columns; c++)
                sum += widths[c];

            if (sum > tableWidth)
            {
                float adjustable = sum - theme.tableMinColumnWidth * columns;
                if (adjustable > 0.001f)
                {
                    float factor = Mathf.Clamp01((sum - tableWidth) / adjustable);
                    for (int c = 0; c < columns; c++)
                        widths[c] -= (widths[c] - theme.tableMinColumnWidth) * factor;
                }
            }
            else if (sum < tableWidth)
            {
                float extra = (tableWidth - sum) / columns;
                for (int c = 0; c < columns; c++)
                    widths[c] += extra;
            }

            return widths;
        }

        private float MeasureTableRow(
            IReadOnlyList<MarkdownTableCell> cells,
            float[] widths,
            int fontSize,
            bool header,
            float pixelsPerUnit)
        {
            float height = fontSize * theme.lineSpacing;
            int count = Mathf.Min(cells.Count, widths.Length);
            for (int c = 0; c < count; c++)
            {
                float width = Mathf.Max(1f, widths[c] - theme.tableCellPadding * 2f);
                MarkdownRichText rich = MarkdownRichTextBuilder.Build(
                    cells[c].Inlines,
                    theme,
                    header,
                    ThemeColor(theme.linkColor),
                    ThemeColor(theme.codeColor));
                TextGenerationSettings settings = CreateSettings(
                    fontSize,
                    width,
                    TextAnchor.UpperLeft,
                    header ? ThemeColor(theme.tableHeaderTextColor) : color);
                m_MarkerGenerator.Populate(rich.Markup, settings);
                float cellHeight = GetTextHeight(m_MarkerGenerator, pixelsPerUnit);
                if (cellHeight <= 0f)
                    cellHeight = fontSize * theme.lineSpacing;
                height = Mathf.Max(height, cellHeight);
            }
            return height + theme.tableCellPadding * 2f;
        }

        private float MeasureTableCellWidth(
            MarkdownTableCell cell,
            int fontSize,
            bool header,
            float pixelsPerUnit)
        {
            MarkdownRichText rich = MarkdownRichTextBuilder.Build(
                cell.Inlines,
                theme,
                header,
                ThemeColor(theme.linkColor),
                ThemeColor(theme.codeColor));
            TextGenerationSettings settings = CreateSettings(
                fontSize,
                0f,
                TextAnchor.UpperLeft,
                header ? ThemeColor(theme.tableHeaderTextColor) : color);
            return m_MarkerGenerator.GetPreferredWidth(rich.Markup, settings) / pixelsPerUnit;
        }

        private void DrawTableCell(
            LayoutResult result,
            MarkdownTableCell cell,
            float columnX,
            float rowTop,
            float columnWidth,
            float rowHeight,
            int fontSize,
            bool header,
            MarkdownTableAlignment tableAlignment,
            Color textColor,
            float pixelsPerUnit)
        {
            MarkdownRichText rich = MarkdownRichTextBuilder.Build(
                cell.Inlines,
                theme,
                header,
                ThemeColor(theme.linkColor),
                ThemeColor(theme.codeColor));

            float innerWidth = Mathf.Max(1f, columnWidth - theme.tableCellPadding * 2f);
            TextAnchor anchor = ToTableAnchor(tableAlignment);
            TextGenerationSettings settings = CreateSettings(fontSize, innerWidth, anchor, textColor);
            m_Generator.Populate(rich.Markup, settings);

            AppendGeneratedGlyphs(
                result,
                m_Generator,
                rich,
                cell.Inlines,
                columnX + theme.tableCellPadding,
                rowTop + theme.tableCellPadding,
                pixelsPerUnit,
                true);
        }

        private void AddTableBorders(
            LayoutResult result,
            float left,
            float top,
            float width,
            float height,
            float[] columnWidths,
            float headerHeight,
            float[] rowHeights)
        {
            Color borderColor = ThemeColor(theme.tableBorderColor);
            if (borderColor.a <= 0f)
                return;

            float thickness = Mathf.Max(1f, theme.tableBorderThickness);
            AddSolidNormalized(result, new Rect(left, -top - thickness, width, thickness), borderColor);
            AddSolidNormalized(result, new Rect(left, -top - height, width, thickness), borderColor);
            AddSolidNormalized(result, new Rect(left, -top - height, thickness, height), borderColor);
            AddSolidNormalized(result, new Rect(left + width - thickness, -top - height, thickness, height), borderColor);

            float rowY = top + headerHeight;
            AddSolidNormalized(result, new Rect(left, -rowY - thickness * 0.5f, width, thickness), borderColor);
            for (int r = 0; r < rowHeights.Length - 1; r++)
            {
                rowY += rowHeights[r];
                AddSolidNormalized(result, new Rect(left, -rowY - thickness * 0.5f, width, thickness), borderColor);
            }

            float columnX = left;
            for (int c = 0; c < columnWidths.Length - 1; c++)
            {
                columnX += columnWidths[c];
                AddSolidNormalized(
                    result,
                    new Rect(columnX - thickness * 0.5f, -top - height, thickness, height),
                    borderColor);
            }
        }

        private TextAnchor ToTableAnchor(MarkdownTableAlignment alignment)
        {
            switch (alignment)
            {
                case MarkdownTableAlignment.Left:
                    return TextAnchor.UpperLeft;
                case MarkdownTableAlignment.Center:
                    return TextAnchor.UpperCenter;
                case MarkdownTableAlignment.Right:
                    return TextAnchor.UpperRight;
                default:
                    return ToUpperAnchor(m_Alignment);
            }
        }

        private float BuildFlowBlock(
            LayoutResult result,
            MarkdownBlock block,
            float top,
            float availableWidth,
            float pixelsPerUnit,
            bool isListItem)
        {
            float left = block.QuoteDepth * theme.quoteIndent + block.ListDepth * theme.listIndent;
            float markerArea = 0f;
            string orderedMarker = null;
            bool isFootnote = block.Type == MarkdownBlockType.Footnote;
            bool isTask = isListItem && block.TaskState != MarkdownTaskState.None;
            int listSize = Mathf.Max(1, Mathf.RoundToInt(m_FontSize * (block.QuoteDepth > 0 ? theme.quoteFontScale : 1f)));

            if (isListItem || isFootnote)
            {
                markerArea = theme.listIndent;
                if (isTask)
                {
                    markerArea = Mathf.Max(markerArea, theme.taskCheckboxSize + theme.listMarkerGap);
                }
                else if (isFootnote)
                {
                    orderedMarker = "[" + block.FootnoteNumber + "]";
                    float markerWidth = MeasureMarkerWidth(orderedMarker, listSize, pixelsPerUnit);
                    markerArea = Mathf.Max(markerArea, markerWidth + theme.listMarkerGap);
                }
                else if (block.IsOrderedList)
                {
                    orderedMarker = block.ListNumber.ToString() + ".";
                    float markerWidth = MeasureMarkerWidth(orderedMarker, listSize, pixelsPerUnit);
                    markerArea = Mathf.Max(markerArea, markerWidth + theme.listMarkerGap);
                }
            }

            float textLeft = left + markerArea;
            float textWidth = Mathf.Max(1f, availableWidth - textLeft);
            int size = listSize;
            Color textColor = block.QuoteDepth > 0 ? ThemeColor(theme.quoteColor) : color;

            MarkdownRichText rich = MarkdownRichTextBuilder.Build(
                block,
                theme,
                false,
                ThemeColor(theme.linkColor),
                ThemeColor(theme.codeColor));

            TextGenerationSettings settings = CreateSettings(
                size,
                textWidth,
                ToUpperAnchor(m_Alignment),
                textColor);

            m_Generator.Populate(rich.Markup, settings);
            float textHeight = GetTextHeight(m_Generator, pixelsPerUnit);
            if (textHeight <= 0f)
                textHeight = size * theme.lineSpacing;

            AppendGeneratedGlyphs(
                result,
                m_Generator,
                rich,
                block.Inlines,
                textLeft,
                top,
                pixelsPerUnit,
                true);

            float totalHeight = textHeight;

            if (isTask)
            {
                float lineHeight = size * theme.lineSpacing;
                DrawTaskCheckbox(result, block, left, top, lineHeight);
            }
            else if ((isListItem || isFootnote) && !string.IsNullOrEmpty(orderedMarker))
            {
                MarkdownRichText markerRich = MarkdownRichTextBuilder.BuildPlainText(orderedMarker);
                TextGenerationSettings markerSettings = CreateSettings(
                    size,
                    Mathf.Max(1f, markerArea),
                    TextAnchor.UpperLeft,
                    ThemeColor(theme.listMarkerColor));
                m_MarkerGenerator.Populate(markerRich.Markup, markerSettings);
                AppendGeneratedGlyphs(
                    result,
                    m_MarkerGenerator,
                    markerRich,
                    null,
                    left,
                    top,
                    pixelsPerUnit,
                    false);
            }
            else if (isListItem && theme.drawBullets)
            {
                float lineHeight = size * theme.lineSpacing;
                float bulletTop = top + lineHeight * 0.62f - theme.bulletSize * 0.5f;
                AddSolidNormalized(
                    result,
                    new Rect(left + theme.listMarkerGap * 0.5f, -bulletTop - theme.bulletSize, theme.bulletSize, theme.bulletSize),
                    ThemeColor(theme.listMarkerColor));
            }

            AddQuoteBars(result, block, top, totalHeight);
            return totalHeight;
        }

        private void DrawTaskCheckbox(
            LayoutResult result,
            MarkdownBlock block,
            float left,
            float top,
            float lineHeight)
        {
            float size = Mathf.Max(8f, theme.taskCheckboxSize);
            float x = left + theme.listMarkerGap * 0.5f;
            float y = top + Mathf.Max(0f, (lineHeight - size) * 0.5f);
            float thickness = Mathf.Max(1f, theme.taskCheckboxBorderThickness);
            Color color = ThemeColor(block.TaskState == MarkdownTaskState.Checked
                ? theme.taskCheckboxCheckedColor
                : theme.taskCheckboxColor);

            AddSolidNormalized(result, new Rect(x, -y - thickness, size, thickness), color);
            AddSolidNormalized(result, new Rect(x, -y - size, size, thickness), color);
            AddSolidNormalized(result, new Rect(x, -y - size, thickness, size), color);
            AddSolidNormalized(result, new Rect(x + size - thickness, -y - size, thickness, size), color);

            if (block.TaskState == MarkdownTaskState.Checked)
            {
                float inset = thickness + 2.5f;
                float inner = Mathf.Max(1f, size - inset * 2f);
                AddSolidNormalized(
                    result,
                    new Rect(x + inset, -y - size + inset, inner, inner),
                    ThemeColor(theme.taskCheckboxFillColor));
            }
        }

        private float BuildCodeBlock(
            LayoutResult result,
            MarkdownBlock block,
            float top,
            float availableWidth,
            float pixelsPerUnit)
        {
            float left = block.QuoteDepth * theme.quoteIndent + block.ListDepth * theme.listIndent;
            float blockWidth = Mathf.Max(1f, availableWidth - left);
            int size = Mathf.Max(1, Mathf.RoundToInt(m_FontSize * theme.codeFontScale));
            float innerWidth = Mathf.Max(1f, blockWidth - theme.codePadding * 2f);
            string code = block.Code ?? string.Empty;
            if (code.EndsWith("\n", StringComparison.Ordinal))
                code = code.Substring(0, code.Length - 1);

            MarkdownRichText rich = MarkdownRichTextBuilder.BuildPlainText(code, false);
            TextGenerationSettings settings = CreateSettings(
                size,
                innerWidth,
                TextAnchor.UpperLeft,
                ThemeColor(theme.codeBlockTextColor),
                false);
            settings.lineSpacing = theme.lineSpacing;

            m_Generator.Populate(rich.Markup, settings);
            float textHeight = GetTextHeight(m_Generator, pixelsPerUnit);
            if (textHeight <= 0f)
                textHeight = size * theme.lineSpacing;

            float blockHeight = textHeight + theme.codePadding * 2f;
            if (theme.drawCodeBlockBackground)
            {
                AddSolidNormalized(
                    result,
                    new Rect(left, -top - blockHeight, blockWidth, blockHeight),
                    ThemeColor(theme.codeBlockBackgroundColor));

                if (theme.codeBlockAccentColor.a > 0f)
                {
                    AddSolidNormalized(
                        result,
                        new Rect(left, -top - blockHeight, Mathf.Max(2f, theme.quoteBarWidth), blockHeight),
                        ThemeColor(theme.codeBlockAccentColor));
                }
            }

            AppendGeneratedGlyphs(
                result,
                m_Generator,
                rich,
                null,
                left + theme.codePadding,
                top + theme.codePadding,
                pixelsPerUnit,
                false);

            AddQuoteBars(result, block, top, blockHeight);
            return blockHeight;
        }

        private void AppendGeneratedGlyphs(
            LayoutResult result,
            TextGenerator generator,
            MarkdownRichText rich,
            IReadOnlyList<MarkdownInline> inlines,
            float left,
            float top,
            float pixelsPerUnit,
            bool decorations)
        {
            IList<UIVertex> verts = generator.verts;
            IList<UICharInfo> chars = generator.characters;
            IList<UILineInfo> lines = generator.lines;
            if (chars.Count == 0 || verts.Count < 4)
                return;

            string markup = rich.Markup;
            int glyphCount = verts.Count / 4;
            int candidateCount = 0;
            int nonSpaceCount = 0;
            for (int i = 0; i < chars.Count; i++)
            {
                if (chars[i].charWidth <= 0f)
                    continue;
                candidateCount++;
                if (!IsLayoutSpace(markup, i))
                    nonSpaceCount++;
            }

            // TextGenerator emits four vertices per glyph but keeps one
            // character entry per input character (including tags). Spaces have a
            // width but normally no glyph, so only include them when the vertex
            // count proves they were emitted.
            bool includeSpaces = candidateCount == glyphCount && nonSpaceCount != glyphCount;

            float unitsPerPixel = 1f / pixelsPerUnit;
            int lineIndex = 0;
            int glyphOrdinal = 0;
            Dictionary<long, SpanLineBounds> bounds = decorations
                ? new Dictionary<long, SpanLineBounds>()
                : null;

            for (int i = 0; i < chars.Count; i++)
            {
                while (lineIndex + 1 < generator.lineCount && i >= lines[lineIndex + 1].startCharIdx)
                    lineIndex++;

                UICharInfo info = chars[i];
                if (info.charWidth <= 0f)
                    continue;

                bool isSpace = IsLayoutSpace(markup, i);
                if (isSpace && !includeSpaces)
                {
                    // Spaces do not create geometry, but their horizontal extent
                    // still belongs to an inline code span or link.
                    if (decorations && bounds != null && inlines != null)
                    {
                        int spaceSpan = rich.ResolveSpanIndex(i, -1);
                        if (spaceSpan >= 0 && spaceSpan < inlines.Count && inlines[spaceSpan] != null)
                        {
                            float lineTopY = -top + lines[lineIndex].topY * unitsPerPixel;
                            float lineBottomY = lineTopY - lines[lineIndex].height * unitsPerPixel;
                            float spaceMinX = left + info.cursorPos.x * unitsPerPixel;
                            float spaceMaxX = spaceMinX + info.charWidth * unitsPerPixel;
                            AccumulateBounds(
                                bounds,
                                lineIndex,
                                spaceSpan,
                                inlines[spaceSpan],
                                spaceMinX,
                                spaceMaxX,
                                lineBottomY,
                                lineTopY);
                        }
                    }

                    continue;
                }

                int vertexIndex = glyphOrdinal * 4;
                glyphOrdinal++;
                if (vertexIndex < 0 || vertexIndex + 3 >= verts.Count)
                    break;

                UIVertex source0 = verts[vertexIndex];
                UIVertex source1 = verts[vertexIndex + 1];
                UIVertex source2 = verts[vertexIndex + 2];
                UIVertex source3 = verts[vertexIndex + 3];

                GlyphQuad glyph = new GlyphQuad
                {
                    V0 = OffsetVertex(source0, left, top, unitsPerPixel),
                    V1 = OffsetVertex(source1, left, top, unitsPerPixel),
                    V2 = OffsetVertex(source2, left, top, unitsPerPixel),
                    V3 = OffsetVertex(source3, left, top, unitsPerPixel)
                };
                result.Glyphs.Add(glyph);

                if (!decorations || inlines == null || inlines.Count == 0)
                    continue;

                int spanIndex = rich.ResolveSpanIndex(i, -1);
                if (spanIndex < 0 || spanIndex >= inlines.Count)
                    continue;

                MarkdownInline inline = inlines[spanIndex];
                if (inline == null)
                    continue;

                float minX = Mathf.Min(Mathf.Min(glyph.V0.position.x, glyph.V1.position.x), Mathf.Min(glyph.V2.position.x, glyph.V3.position.x));
                float maxX = Mathf.Max(Mathf.Max(glyph.V0.position.x, glyph.V1.position.x), Mathf.Max(glyph.V2.position.x, glyph.V3.position.x));
                float minY = Mathf.Min(Mathf.Min(glyph.V0.position.y, glyph.V1.position.y), Mathf.Min(glyph.V2.position.y, glyph.V3.position.y));
                float maxY = Mathf.Max(Mathf.Max(glyph.V0.position.y, glyph.V1.position.y), Mathf.Max(glyph.V2.position.y, glyph.V3.position.y));

                if (maxX <= minX && maxY <= minY)
                    continue;

                AccumulateBounds(bounds, lineIndex, spanIndex, inline, minX, maxX, minY, maxY);
            }

            if (bounds == null)
                return;

            foreach (KeyValuePair<long, SpanLineBounds> pair in bounds)
            {
                SpanLineBounds line = pair.Value;
                MarkdownInline inline = line.Inline;
                float width = Mathf.Max(1f, line.MaxX - line.MinX);
                float height = Mathf.Max(1f, line.MaxY - line.MinY);
                MarkdownInlineStyle style = inline.Style;

                if ((style & MarkdownInlineStyle.Code) != 0 && theme.drawInlineCodeBackground)
                {
                    AddSolidNormalized(
                        result,
                        new Rect(line.MinX - 1.5f, line.MinY - 1f, width + 3f, height + 2f),
                        ThemeColor(theme.inlineCodeBackgroundColor));
                }

                if ((style & MarkdownInlineStyle.Strikethrough) != 0)
                {
                    float y = line.MinY + height * 0.45f;
                    AddSolidNormalized(
                        result,
                        new Rect(line.MinX, y, width, Mathf.Max(1f, theme.ruleThickness)),
                        ThemeColor(theme.strikeThroughColor));
                }

                if ((style & (MarkdownInlineStyle.Link | MarkdownInlineStyle.Footnote)) != 0 &&
                    !string.IsNullOrEmpty(inline.Url) &&
                    m_EnableLinks)
                {
                    if (!result.Links.TryGetValue(inline.Url, out LinkRegion region))
                    {
                        region = new LinkRegion(inline.Url);
                        result.Links.Add(inline.Url, region);
                    }

                    Rect linkRect = new Rect(line.MinX - 1f, line.MinY - 2f, width + 2f, height + 4f);
                    region.Rects.Add(linkRect);

                    if ((style & MarkdownInlineStyle.Link) != 0 && theme.underlineLinks)
                    {
                        AddSolidNormalized(
                            result,
                            new Rect(line.MinX, line.MinY + 0.5f, width, Mathf.Max(1f, theme.ruleThickness)),
                            ThemeColor(theme.linkUnderlineColor));
                    }
                }
            }
        }

        private static bool IsLayoutSpace(string markup, int index)
        {
            return index >= 0 && index < markup.Length && (markup[index] == ' ' || markup[index] == '\t');
        }

        private static void AccumulateBounds(
            Dictionary<long, SpanLineBounds> bounds,
            int lineIndex,
            int spanIndex,
            MarkdownInline inline,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (bounds == null)
                return;

            long key = ((long)lineIndex << 32) | (uint)spanIndex;
            if (!bounds.TryGetValue(key, out SpanLineBounds lineBounds))
            {
                lineBounds = new SpanLineBounds
                {
                    Inline = inline,
                    MinX = minX,
                    MaxX = maxX,
                    MinY = minY,
                    MaxY = maxY
                };
            }
            else
            {
                lineBounds.MinX = Mathf.Min(lineBounds.MinX, minX);
                lineBounds.MaxX = Mathf.Max(lineBounds.MaxX, maxX);
                lineBounds.MinY = Mathf.Min(lineBounds.MinY, minY);
                lineBounds.MaxY = Mathf.Max(lineBounds.MaxY, maxY);
            }
            bounds[key] = lineBounds;
        }

        private void AddQuoteBars(LayoutResult result, MarkdownBlock block, float top, float height)
        {
            if (block.QuoteDepth <= 0 || height <= 0f)
                return;

            for (int depth = 0; depth < block.QuoteDepth; depth++)
            {
                AddSolidNormalized(
                    result,
                    new Rect(
                        depth * theme.quoteIndent,
                        -top - height,
                        Mathf.Max(1f, theme.quoteBarWidth),
                        height),
                    ThemeColor(theme.quoteBarColor));
            }
        }

        private void AddDocumentBackground(
            LayoutResult result,
            float width,
            float height,
            float contentHeight,
            float verticalShift = 0f)
        {
            Color background = ThemeColor(theme.backgroundColor);
            if (background.a <= 0f)
                return;

            float backgroundHeight = Mathf.Max(height, contentHeight);
            if (backgroundHeight <= 0f || width <= 0f)
                return;

            // Coordinates are relative to the padded content origin. Extending
            // left/up by the padding makes the fill cover the whole RectTransform.
            result.Solids.Insert(
                0,
                new MarkdownSolidRect(
                    new Rect(
                        -padding.x,
                        -(backgroundHeight - padding.y) + verticalShift,
                        width,
                        backgroundHeight),
                    background));
        }

        private static void AddSolidNormalized(LayoutResult result, Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            result.Solids.Add(new MarkdownSolidRect(rect, color));
        }

        private static UIVertex OffsetVertex(UIVertex source, float left, float top, float unitsPerPixel)
        {
            UIVertex vertex = source;
            vertex.position = new Vector3(
                left + source.position.x * unitsPerPixel,
                -top + source.position.y * unitsPerPixel,
                0f);
            return vertex;
        }

        private float MeasureMarkerWidth(string marker, int size, float pixelsPerUnit)
        {
            MarkdownRichText rich = MarkdownRichTextBuilder.BuildPlainText(marker);
            TextGenerationSettings settings = CreateSettings(size, 1000f, TextAnchor.UpperLeft, color);
            return m_MarkerGenerator.GetPreferredWidth(rich.Markup, settings) / pixelsPerUnit;
        }

        private TextGenerationSettings CreateSettings(
            int size,
            float width,
            TextAnchor anchor,
            Color textColor,
            bool richText = true)
        {
            var settings = new TextGenerationSettings
            {
                font = m_Font,
                color = textColor,
                fontSize = size,
                scaleFactor = PixelsPerUnit,
                lineSpacing = theme.lineSpacing,
                richText = richText,
                textAnchor = anchor,
                pivot = new Vector2(0f, 1f),
                generationExtents = new Vector2(Mathf.Max(1f, width), 100000f),
                horizontalOverflow = HorizontalWrapMode.Wrap,
                verticalOverflow = VerticalWrapMode.Overflow,
                updateBounds = false,
                resizeTextForBestFit = false,
                fontStyle = FontStyle.Normal,
                alignByGeometry = false
            };
            return settings;
        }

        private float GetTextHeight(TextGenerator generator, float pixelsPerUnit)
        {
            if (generator.lineCount <= 0)
                return 0f;

            IList<UILineInfo> lines = generator.lines;
            if (generator.lineCount == 1)
                return lines[0].height / pixelsPerUnit;

            // line.height does not include the extra spacing introduced by
            // lineSpacing. topY is the actual line origin, so the distance from
            // the first line top to the last line bottom is the real used height.
            float top = lines[0].topY;
            float bottom = lines[generator.lineCount - 1].topY - lines[generator.lineCount - 1].height;
            float height = Mathf.Abs(top - bottom);
            if (height <= 0f)
            {
                height = 0f;
                for (int i = 0; i < generator.lineCount; i++)
                {
                    height += lines[i].height;
                    if (i > 0)
                        height += lines[i - 1].leading;
                }
            }
            return height / pixelsPerUnit;
        }

        private float GetBlockGap(IReadOnlyList<MarkdownBlock> blocks, int index)
        {
            if (index <= 0)
                return 0f;

            MarkdownBlock previous = blocks[index - 1];
            MarkdownBlock current = blocks[index];

            if (current.Type == MarkdownBlockType.Heading)
                return theme.headingTopSpacing;
            if (previous.Type == MarkdownBlockType.Heading)
                return theme.headingBottomSpacing;
            if (current.Type == MarkdownBlockType.ThematicBreak || previous.Type == MarkdownBlockType.ThematicBreak)
                return theme.ruleSpacing;
            if (current.Type == MarkdownBlockType.CodeBlock || previous.Type == MarkdownBlockType.CodeBlock)
                return theme.codeBlockSpacing;
            if (current.Type == MarkdownBlockType.Table || previous.Type == MarkdownBlockType.Table)
                return theme.tableSpacing;
            if (current.Type == MarkdownBlockType.ListItem &&
                previous.Type == MarkdownBlockType.ListItem &&
                current.ListDepth == previous.ListDepth)
            {
                return theme.listSpacing;
            }

            return theme.paragraphSpacing;
        }

        private static TextAnchor ToUpperAnchor(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    return TextAnchor.UpperCenter;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    return TextAnchor.UpperRight;
                default:
                    return TextAnchor.UpperLeft;
            }
        }

        private Color ThemeColor(Color value)
        {
            value.a *= color.a;
            return value;
        }

        private float PixelsPerUnit
        {
            get
            {
                Canvas canvas = this.canvas;
                if (canvas == null)
                    return 1f;
                float scale = canvas.scaleFactor;
                return scale > 0.0001f ? scale : 1f;
            }
        }

        private void PushDecorations(LayoutResult layout)
        {
            List<MarkdownSolidRect> solids = layout != null ? layout.Solids : null;
            m_PendingSolids = solids;

            if (m_Decoration == null)
                return;

            if (CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout())
                return;

            m_Decoration.SetQuads(solids);
            m_Decoration.enabled = isActiveAndEnabled;
            m_PendingSolids = null;
        }

        private MarkdownDecorationLayer EnsureDecorationLayer()
        {
            if (CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout())
                return m_Decoration;

            if (m_Decoration != null)
            {
                SyncDecorationLayer();
                return m_Decoration;
            }

            Transform parent = transform.parent;
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var candidate = parent.GetChild(i).GetComponent<MarkdownDecorationLayer>();
                if (candidate != null && candidate.Owner == this)
                {
                    m_Decoration = candidate;
                    SyncDecorationLayer();
                    return m_Decoration;
                }
            }

            var go = new GameObject("Markdown Text Decorations", typeof(RectTransform), typeof(CanvasRenderer), typeof(MarkdownDecorationLayer));
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            go.transform.SetParent(parent, false);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            m_Decoration = go.GetComponent<MarkdownDecorationLayer>();
            m_Decoration.Initialize(this);
            m_Decoration.enabled = isActiveAndEnabled;
            SyncDecorationLayer();
            return m_Decoration;
        }

        private void SyncDecorationLayer()
        {
            if (m_Decoration == null || m_SyncingDecoration)
                return;

            m_SyncingDecoration = true;
            try
            {
                RectTransform source = rectTransform;
                RectTransform target = m_Decoration.rectTransform;
                if (target.parent != source.parent)
                    target.SetParent(source.parent, false);

                target.anchorMin = source.anchorMin;
                target.anchorMax = source.anchorMax;
                target.pivot = source.pivot;
                target.anchoredPosition3D = source.anchoredPosition3D;
                target.sizeDelta = source.sizeDelta;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;

                int sourceIndex = source.GetSiblingIndex();
                int targetIndex = target.GetSiblingIndex();
                if (targetIndex > sourceIndex)
                    target.SetSiblingIndex(sourceIndex);
                else if (targetIndex < sourceIndex - 1)
                    target.SetSiblingIndex(Mathf.Max(0, sourceIndex - 1));
            }
            finally
            {
                m_SyncingDecoration = false;
            }
        }

        private void OnFontTextureRebuilt(Font rebuiltFont)
        {
            if (m_DisableFontTextureRebuiltCallback || rebuiltFont != m_Font)
                return;
            MarkDirty();
        }

        private void EnsureFont()
        {
            if (m_Font == null)
                m_Font = GetDefaultFont();
        }

        public static Font GetDefaultFont()
        {
            if (s_DefaultFont != null)
                return s_DefaultFont;

            try
            {
                s_DefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                s_DefaultFont = null;
            }

            if (s_DefaultFont == null)
            {
                try
                {
                    s_DefaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (Exception)
                {
                    s_DefaultFont = null;
                }
            }

            return s_DefaultFont;
        }

        private sealed class LayoutResult
        {
            public float Width;
            public float Height;
            public readonly List<GlyphQuad> Glyphs = new List<GlyphQuad>();
            public readonly List<MarkdownSolidRect> Solids = new List<MarkdownSolidRect>();
            public readonly List<MarkdownImagePlacement> Images = new List<MarkdownImagePlacement>();
            public readonly Dictionary<string, LinkRegion> Links = new Dictionary<string, LinkRegion>(StringComparer.Ordinal);

            public void Offset(float x, float y)
            {
                for (int i = 0; i < Glyphs.Count; i++)
                {
                    GlyphQuad glyph = Glyphs[i];
                    glyph.V0.position += new Vector3(x, y, 0f);
                    glyph.V1.position += new Vector3(x, y, 0f);
                    glyph.V2.position += new Vector3(x, y, 0f);
                    glyph.V3.position += new Vector3(x, y, 0f);
                    Glyphs[i] = glyph;
                }

                for (int i = 0; i < Solids.Count; i++)
                {
                    MarkdownSolidRect solid = Solids[i];
                    Rect rect = solid.Rect;
                    rect.x += x;
                    rect.y += y;
                    solid.Rect = rect;
                    Solids[i] = solid;
                }

                foreach (KeyValuePair<string, LinkRegion> pair in Links)
                {
                    List<Rect> rects = pair.Value.Rects;
                    for (int i = 0; i < rects.Count; i++)
                    {
                        Rect rect = rects[i];
                        rect.x += x;
                        rect.y += y;
                        rects[i] = rect;
                    }
                }

                for (int i = 0; i < Images.Count; i++)
                {
                    MarkdownImagePlacement image = Images[i];
                    Rect rect = image.Rect;
                    rect.x += x;
                    rect.y += y;
                    image.Rect = rect;
                }
            }
        }

        private struct GlyphQuad
        {
            public UIVertex V0;
            public UIVertex V1;
            public UIVertex V2;
            public UIVertex V3;
        }

        private sealed class LinkRegion
        {
            public LinkRegion(string url)
            {
                Url = url;
            }

            public string Url { get; }
            public List<Rect> Rects { get; } = new List<Rect>();
        }

        private struct SpanLineBounds
        {
            public MarkdownInline Inline;
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
        }
    }
}
