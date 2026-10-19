using System.Collections.Generic;
using UnityEngine;

namespace SS.UIComponent
{
    /// <summary>
    /// A foreground-color range returned by a syntax highlighter. Offsets are
    /// UTF-16 indices into the code block string.
    /// </summary>
    public readonly struct MarkdownHighlightSpan
    {
        public MarkdownHighlightSpan(int start, int length, Color color)
        {
            Start = start;
            Length = length;
            Color = color;
        }

        public int Start { get; }
        public int Length { get; }
        public Color Color { get; }
        public int End => Start + Length;
    }

    /// <summary>
    /// Optional syntax-highlighting contract for fenced code blocks. SSMarkdown
    /// deliberately ships no language implementations; assign an implementation
    /// to <see cref="MarkdownText.codeHighlighter"/>.
    /// </summary>
    public interface IMarkdownSyntaxHighlighter
    {
        /// <summary>
        /// Return foreground color spans for the code. Return null or an empty
        /// list to use the theme's normal code color.
        /// </summary>
        /// <param name="language">Info string after the opening fence, for example "csharp".</param>
        /// <param name="code">Code text without fences.</param>
        IReadOnlyList<MarkdownHighlightSpan> Highlight(string language, string code);
    }
}
