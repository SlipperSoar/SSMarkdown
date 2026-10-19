using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SS.UIComponent
{
    /// <summary>Maps generated rich-text characters back to their inline spans.</summary>
    internal sealed class MarkdownRichText
    {
        private readonly List<Range> m_Ranges;

        public MarkdownRichText(string markup, List<Range> ranges, List<int> visibleSpanIndices)
        {
            Markup = markup;
            m_Ranges = ranges;
            VisibleSpanIndices = visibleSpanIndices;
        }

        public string Markup { get; }

        /// <summary>Span index for every visible character, in visual order.</summary>
        public List<int> VisibleSpanIndices { get; }

        public int ResolveSpanIndex(int markupIndex, int visibleOrdinal)
        {
            int byMarkup = FindRangeIndex(markupIndex);
            if (byMarkup >= 0)
                return byMarkup;

            if (visibleOrdinal >= 0 && visibleOrdinal < VisibleSpanIndices.Count)
                return VisibleSpanIndices[visibleOrdinal];

            return -1;
        }

        private int FindRangeIndex(int markupIndex)
        {
            int low = 0;
            int high = m_Ranges.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                Range range = m_Ranges[mid];
                if (markupIndex < range.Start)
                {
                    high = mid - 1;
                }
                else if (markupIndex >= range.End)
                {
                    low = mid + 1;
                }
                else
                {
                    return range.SpanIndex;
                }
            }
            return -1;
        }

        internal readonly struct Range
        {
            public Range(int start, int end, int spanIndex)
            {
                Start = start;
                End = end;
                SpanIndex = spanIndex;
            }

            public int Start { get; }
            public int End { get; }
            public int SpanIndex { get; }
        }
    }

    internal static class MarkdownRichTextBuilder
    {
        public static MarkdownRichText Build(
            MarkdownBlock block,
            MarkdownTheme theme,
            bool forceBold,
            Color linkColor,
            Color codeColor)
        {
            return Build(block.Inlines, theme, forceBold, linkColor, codeColor);
        }

        public static MarkdownRichText Build(
            IReadOnlyList<MarkdownInline> inlines,
            MarkdownTheme theme,
            bool forceBold,
            Color linkColor,
            Color codeColor)
        {
            int capacity = 32;
            for (int i = 0; i < inlines.Count; i++)
                capacity += inlines[i].Text.Length + 16;

            var markup = new StringBuilder(capacity);
            var ranges = new List<MarkdownRichText.Range>(inlines.Count);
            var visible = new List<int>();

            for (int i = 0; i < inlines.Count; i++)
            {
                MarkdownInline inline = inlines[i];
                if (inline.Text.Length == 0)
                    continue;

                MarkdownInlineStyle style = inline.Style;
                if (forceBold)
                    style |= MarkdownInlineStyle.Bold;

                Color? color = null;
                if ((style & MarkdownInlineStyle.Code) != 0)
                    color = codeColor;
                else if ((style & (MarkdownInlineStyle.Link | MarkdownInlineStyle.Footnote)) != 0)
                    color = linkColor;

                bool bold = (style & MarkdownInlineStyle.Bold) != 0;
                bool italic = (style & MarkdownInlineStyle.Italic) != 0;

                if (color.HasValue)
                    markup.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(color.Value)).Append('>');
                if (bold)
                    markup.Append("<b>");
                if (italic)
                    markup.Append("<i>");

                int start = markup.Length;
                AppendEscaped(markup, inline.Text, visible, i, true);
                int end = markup.Length;

                if (italic)
                    markup.Append("</i>");
                if (bold)
                    markup.Append("</b>");
                if (color.HasValue)
                    markup.Append("</color>");

                ranges.Add(new MarkdownRichText.Range(start, end, i));
            }

            return new MarkdownRichText(markup.ToString(), ranges, visible);
        }

        public static MarkdownRichText BuildPlainText(string text, bool protectRichTextTags = true)
        {
            var markup = new StringBuilder((text ?? string.Empty).Length + 16);
            var visible = new List<int>();
            AppendEscaped(markup, text ?? string.Empty, visible, 0, protectRichTextTags);
            var ranges = new List<MarkdownRichText.Range>
            {
                new MarkdownRichText.Range(0, markup.Length, 0)
            };
            return new MarkdownRichText(markup.ToString(), ranges, visible);
        }

        public static MarkdownRichText BuildHighlightedText(
            string text,
            IReadOnlyList<MarkdownHighlightSpan> spans,
            Color baseColor)
        {
            text = text ?? string.Empty;
            var colors = new Color[text.Length];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = baseColor;

            if (spans != null)
            {
                for (int s = 0; s < spans.Count; s++)
                {
                    MarkdownHighlightSpan span = spans[s];
                    int start = Mathf.Clamp(span.Start, 0, text.Length);
                    int end = Mathf.Clamp(span.End, start, text.Length);
                    for (int i = start; i < end; i++)
                        colors[i] = span.Color;
                }
            }

            var markup = new StringBuilder(text.Length + 32);
            var visible = new List<int>(text.Length);
            var ranges = new List<MarkdownRichText.Range>();

            int index = 0;
            while (index < text.Length)
            {
                Color color = colors[index];
                int end = index + 1;
                while (end < text.Length && colors[end] == color)
                    end++;

                int start = markup.Length;
                markup.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(color)).Append('>');
                AppendEscaped(markup, text.Substring(index, end - index), visible, 0, true);
                markup.Append("</color>");
                ranges.Add(new MarkdownRichText.Range(start, markup.Length, 0));
                index = end;
            }

            if (ranges.Count == 0)
                ranges.Add(new MarkdownRichText.Range(0, 0, 0));

            return new MarkdownRichText(markup.ToString(), ranges, visible);
        }

        private static void AppendEscaped(
            StringBuilder markup,
            string text,
            List<int> visible,
            int spanIndex,
            bool protectRichTextTags)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    markup.Append('<');
                    if (protectRichTextTags && LooksLikeRichTextTag(text, i))
                        markup.Append('\u200B');
                    visible.Add(spanIndex);
                }
                else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    markup.Append(c).Append(text[i + 1]);
                    visible.Add(spanIndex);
                    i++;
                }
                else
                {
                    markup.Append(c);
                    visible.Add(spanIndex);
                }
            }
        }

        private static bool LooksLikeRichTextTag(string text, int openBracket)
        {
            int close = text.IndexOf('>', openBracket + 1);
            if (close < 0 || close - openBracket > 32)
                return false;

            int start = openBracket + 1;
            if (start < close && text[start] == '/')
                start++;

            int end = start;
            while (end < close && (char.IsLetterOrDigit(text[end]) || text[end] == '-' || text[end] == '_'))
                end++;

            string name = text.Substring(start, end - start).ToLowerInvariant();
            switch (name)
            {
                case "b":
                case "i":
                case "size":
                case "color":
                case "material":
                case "quad":
                    return true;
                default:
                    return false;
            }
        }
    }
}
