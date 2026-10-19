using System;
using System.Collections.Generic;
using System.Text;

namespace SS.UIComponent
{
    /// <summary>The kind of block-level element produced by the Markdown parser.</summary>
    public enum MarkdownBlockType
    {
        Paragraph,
        Heading,
        CodeBlock,
        BlockQuote,
        ListItem,
        ThematicBreak,
        Table,
        Image,
        Footnote
    }

    /// <summary>Inline styling flags. A single inline can combine several flags.</summary>
    [Flags]
    public enum MarkdownInlineStyle
    {
        None = 0,
        Bold = 1 << 0,
        Italic = 1 << 1,
        Code = 1 << 2,
        Link = 1 << 3,
        Strikethrough = 1 << 4,
        Footnote = 1 << 5
    }

    /// <summary>A run of inline text sharing the same style and link target.</summary>
    public sealed class MarkdownInline
    {
        private readonly StringBuilder m_Text = new StringBuilder();

        public MarkdownInline(string text, MarkdownInlineStyle style, string url = null)
        {
            m_Text.Append(text ?? string.Empty);
            Style = style;
            Url = url;
        }

        /// <summary>Visible text of the run. Escapes have already been resolved.</summary>
        public string Text => m_Text.ToString();

        public MarkdownInlineStyle Style { get; }

        /// <summary>Destination of a link, or null when the run is not a link.</summary>
        public string Url { get; }

        public bool IsImage { get; internal set; }

        internal void Append(string text)
        {
            m_Text.Append(text);
        }
    }

    /// <summary>A reference-style link definition such as <c>[docs]: https://...</c>.</summary>
    public sealed class MarkdownLinkReference
    {
        public MarkdownLinkReference(string label, string url, string title = null)
        {
            Label = label;
            Url = url;
            Title = title;
        }

        public string Label { get; }
        public string Url { get; }
        public string Title { get; }

        public static string NormalizeLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;

            var sb = new StringBuilder(label.Length);
            bool pendingSpace = false;
            for (int i = 0; i < label.Length; i++)
            {
                char c = label[i];
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }

    /// <summary>A footnote definition parsed from <c>[^id]: ...</c>.</summary>
    public sealed class MarkdownFootnote
    {
        public MarkdownFootnote(string id, int number, string text)
        {
            Id = id;
            Number = number;
            Text = text;
        }

        public string Id { get; }
        public int Number { get; }
        public string Text { get; }
    }

    internal sealed class MarkdownInlineContext
    {
        private readonly IReadOnlyDictionary<string, MarkdownLinkReference> m_References;
        private readonly IReadOnlyDictionary<string, int> m_FootnoteNumbers;

        public MarkdownInlineContext(
            IReadOnlyDictionary<string, MarkdownLinkReference> references,
            IReadOnlyDictionary<string, int> footnoteNumbers)
        {
            m_References = references;
            m_FootnoteNumbers = footnoteNumbers;
        }

        public bool TryGetReference(string label, out MarkdownLinkReference reference)
        {
            reference = null;
            return m_References != null &&
                   m_References.TryGetValue(MarkdownLinkReference.NormalizeLabel(label), out reference);
        }

        public bool TryGetFootnoteNumber(string id, out int number)
        {
            number = 0;
            return m_FootnoteNumbers != null &&
                   m_FootnoteNumbers.TryGetValue(MarkdownLinkReference.NormalizeLabel(id), out number);
        }
    }

    /// <summary>Column alignment read from a GFM table delimiter row.</summary>
    public enum MarkdownTableAlignment
    {
        Default,
        Left,
        Center,
        Right
    }

    /// <summary>A single header or body cell in a Markdown table.</summary>
    public sealed class MarkdownTableCell
    {
        public List<MarkdownInline> Inlines { get; } = new List<MarkdownInline>();

        public string PlainText
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < Inlines.Count; i++)
                    sb.Append(Inlines[i].Text);
                return sb.ToString();
            }
        }
    }

    /// <summary>Parsed GFM table data.</summary>
    public sealed class MarkdownTable
    {
        public List<MarkdownTableCell> Header { get; } = new List<MarkdownTableCell>();
        public List<List<MarkdownTableCell>> Rows { get; } = new List<List<MarkdownTableCell>>();
        public List<MarkdownTableAlignment> Alignments { get; } = new List<MarkdownTableAlignment>();

        public int ColumnCount => Alignments.Count;

        public string PlainText
        {
            get
            {
                var sb = new StringBuilder();
                AppendRow(sb, Header);
                for (int i = 0; i < Rows.Count; i++)
                {
                    sb.Append('\n');
                    AppendRow(sb, Rows[i]);
                }
                return sb.ToString();
            }
        }

        private static void AppendRow(StringBuilder sb, List<MarkdownTableCell> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(cells[i].PlainText);
            }
        }
    }

    /// <summary>A block-level Markdown element.</summary>
    public sealed class MarkdownBlock
    {
        public MarkdownBlock(MarkdownBlockType type)
        {
            Type = type;
            Inlines = new List<MarkdownInline>();
        }

        public MarkdownBlockType Type { get; internal set; }

        /// <summary>Heading level (1-6), or 0 for non-heading blocks.</summary>
        public int Level { get; internal set; }

        /// <summary>Number of enclosing block quotes.</summary>
        public int QuoteDepth { get; internal set; }

        /// <summary>Nesting level of a list item (0 for a top-level item).</summary>
        public int ListDepth { get; internal set; }

        /// <summary>True when this item belongs to an ordered list.</summary>
        public bool IsOrderedList { get; internal set; }

        /// <summary>Source number of an ordered list item, otherwise 0.</summary>
        public int ListNumber { get; internal set; }

        /// <summary>Language recorded after the opening fence of a code block.</summary>
        public string Info { get; internal set; }

        /// <summary>Literal source of a code block.</summary>
        public string Code { get; internal set; }

        /// <summary>Parsed data when <see cref="Type"/> is <see cref="MarkdownBlockType.Table"/>.</summary>
        public MarkdownTable Table { get; internal set; }

        /// <summary>Image source when <see cref="Type"/> is <see cref="MarkdownBlockType.Image"/>.</summary>
        public string ImageUrl { get; internal set; }

        /// <summary>Alt text for an image block.</summary>
        public string ImageAlt { get; internal set; }

        /// <summary>Footnote identifier when this is a footnote block.</summary>
        public string FootnoteId { get; internal set; }

        /// <summary>Footnote display number when this is a footnote block.</summary>
        public int FootnoteNumber { get; internal set; }

        public List<MarkdownInline> Inlines { get; }

        public string PlainText
        {
            get
            {
                if (Type == MarkdownBlockType.CodeBlock)
                    return Code ?? string.Empty;
                if (Type == MarkdownBlockType.Table)
                    return Table != null ? Table.PlainText : string.Empty;
                if (Type == MarkdownBlockType.Image)
                    return ImageAlt ?? string.Empty;

                var sb = new StringBuilder();
                for (int i = 0; i < Inlines.Count; i++)
                    sb.Append(Inlines[i].Text);
                return sb.ToString();
            }
        }

        public override string ToString()
        {
            if (Type == MarkdownBlockType.Heading)
                return "Heading " + Level + ": " + PlainText;
            if (Type == MarkdownBlockType.CodeBlock)
                return "Code (" + (Info ?? string.Empty) + ")";
            if (Type == MarkdownBlockType.Image)
                return "Image: " + (ImageUrl ?? string.Empty);
            return Type + ": " + PlainText;
        }
    }

    /// <summary>The immutable result of parsing a Markdown source string.</summary>
    public sealed class MarkdownDocument
    {
        private readonly List<MarkdownBlock> m_Blocks;
        private readonly IReadOnlyDictionary<string, MarkdownLinkReference> m_LinkReferences;
        private readonly IReadOnlyList<MarkdownFootnote> m_Footnotes;

        internal MarkdownDocument(
            string source,
            List<MarkdownBlock> blocks,
            Dictionary<string, MarkdownLinkReference> linkReferences = null,
            List<MarkdownFootnote> footnotes = null)
        {
            Source = source ?? string.Empty;
            m_Blocks = blocks ?? new List<MarkdownBlock>();
            m_LinkReferences = linkReferences ?? new Dictionary<string, MarkdownLinkReference>(StringComparer.Ordinal);
            m_Footnotes = footnotes ?? new List<MarkdownFootnote>();
        }

        public string Source { get; }

        public IReadOnlyList<MarkdownBlock> Blocks => m_Blocks;

        public IReadOnlyDictionary<string, MarkdownLinkReference> LinkReferences => m_LinkReferences;

        public IReadOnlyList<MarkdownFootnote> Footnotes => m_Footnotes;

        public string PlainText
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < m_Blocks.Count; i++)
                {
                    if (i > 0) sb.Append('\n');
                    sb.Append(m_Blocks[i].PlainText);
                }
                return sb.ToString();
            }
        }
    }
}
