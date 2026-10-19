using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SS.UIComponent
{
    /// <summary>
    /// A small, dependency-free Markdown parser tuned for runtime UI text.
    /// It supports the common block and inline constructs used by documentation,
    /// without taking a dependency on Unity or third-party packages.
    /// </summary>
    public sealed class MarkdownParser
    {
        private static readonly Regex s_AtxHeading = new Regex(
            @"^(?<indent> {0,3})(?<hashes>#{1,6})(?<space>[ \t]+|$)(?<text>.*)$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_SetextUnderline = new Regex(
            @"^ {0,3}(?<mark>=+|-+)[ \t]*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_Fence = new Regex(
            @"^(?<indent> {0,3})(?<fence>`{3,}|~{3,})(?<info>.*)$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_BlockQuote = new Regex(
            @"^ {0,3}>[ \t]?(?<text>.*)$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_ListMarker = new Regex(
            @"^(?<indent> *)(?<marker>[-+*]|\d{1,9}[.)])(?:(?<spaces>[ \t]+)(?<content>.*)|$)",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_ClosingHashes = new Regex(
            @"[ \t]+#+[ \t]*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_TableDelimiterCell = new Regex(
            @"^:?-{3,}:?$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_LinkReference = new Regex(
            @"^ {0,3}\[(?<label>[^\]]+)\]:[ \t]*(?<dest><[^>\n]*>|[^\s]+)(?:[ \t]+(?<title>""[^""]*""|'[^']*'|\([^)]*\)))?[ \t]*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_FootnoteDefinition = new Regex(
            @"^ {0,3}\[\^(?<id>[^\]]+)\]:[ \t]*(?<text>.*)$",
            RegexOptions.CultureInvariant);

        private Dictionary<string, MarkdownLinkReference> m_LinkReferences;
        private List<MarkdownFootnote> m_Footnotes;
        private Dictionary<string, int> m_FootnoteNumbers;
        private MarkdownInlineContext m_InlineContext;

        /// <summary>Parse a Markdown string into a block tree.</summary>
        public MarkdownDocument Parse(string markdown)
        {
            string source = markdown ?? string.Empty;
            if (source.Length > 0 && source[0] == '\uFEFF')
                source = source.Substring(1);

            source = source.Replace("\r\n", "\n").Replace('\r', '\n');
            source = ExpandTabs(source);

            var lines = new List<string>(source.Split('\n'));
            ExtractDefinitions(lines);
            m_InlineContext = new MarkdownInlineContext(m_LinkReferences, m_FootnoteNumbers);

            var blocks = new List<MarkdownBlock>();
            ParseLines(lines, 0, blocks);
            AppendFootnotes(blocks);

            return new MarkdownDocument(source, blocks, m_LinkReferences, m_Footnotes);
        }

        /// <summary>Static convenience wrapper.</summary>
        public static MarkdownDocument ParseText(string markdown)
        {
            return new MarkdownParser().Parse(markdown);
        }

        private void ExtractDefinitions(List<string> lines)
        {
            m_LinkReferences = new Dictionary<string, MarkdownLinkReference>(StringComparer.Ordinal);
            m_Footnotes = new List<MarkdownFootnote>();
            m_FootnoteNumbers = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < lines.Count; i++)
            {
                Match footnote = s_FootnoteDefinition.Match(lines[i]);
                if (footnote.Success)
                {
                    string id = MarkdownLinkReference.NormalizeLabel(footnote.Groups["id"].Value);
                    if (id.Length == 0 || m_FootnoteNumbers.ContainsKey(id))
                        continue;

                    var text = new StringBuilder(footnote.Groups["text"].Value.TrimEnd());
                    lines[i] = string.Empty;

                    int j = i + 1;
                    while (j < lines.Count)
                    {
                        if (IsBlank(lines[j]))
                        {
                            int k = j + 1;
                            while (k < lines.Count && IsBlank(lines[k]))
                                k++;
                            if (k < lines.Count && GetIndent(lines[k]) >= 2)
                            {
                                text.Append('\n');
                                lines[j] = string.Empty;
                                j++;
                                continue;
                            }
                            break;
                        }

                        if (GetIndent(lines[j]) < 2)
                            break;

                        text.Append('\n').Append(RemoveIndent(lines[j], 2).TrimEnd());
                        lines[j] = string.Empty;
                        j++;
                    }

                    int number = m_Footnotes.Count + 1;
                    m_Footnotes.Add(new MarkdownFootnote(id, number, text.ToString().Trim()));
                    m_FootnoteNumbers[id] = number;
                    i = j - 1;
                    continue;
                }

                Match reference = s_LinkReference.Match(lines[i]);
                if (!reference.Success)
                    continue;

                string label = reference.Groups["label"].Value;
                if (label.StartsWith("^", StringComparison.Ordinal))
                    continue;

                label = MarkdownLinkReference.NormalizeLabel(label);
                if (label.Length == 0 || m_LinkReferences.ContainsKey(label))
                    continue;

                string destination = reference.Groups["dest"].Value;
                if (destination.Length >= 2 && destination[0] == '<' && destination[destination.Length - 1] == '>')
                    destination = destination.Substring(1, destination.Length - 2);

                string title = reference.Groups["title"].Value;
                if (title.Length >= 2)
                    title = title.Substring(1, title.Length - 2);

                m_LinkReferences[label] = new MarkdownLinkReference(
                    label,
                    UnescapeReference(destination),
                    UnescapeReference(title));
                lines[i] = string.Empty;
            }
        }

        private static string UnescapeReference(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return value;

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    sb.Append(value[++i]);
                }
                else
                {
                    sb.Append(value[i]);
                }
            }
            return sb.ToString();
        }

        private void AppendFootnotes(List<MarkdownBlock> blocks)
        {
            if (m_Footnotes == null || m_Footnotes.Count == 0)
                return;

            blocks.Add(NewBlock(MarkdownBlockType.ThematicBreak, 0));
            for (int i = 0; i < m_Footnotes.Count; i++)
            {
                MarkdownFootnote footnote = m_Footnotes[i];
                var block = NewBlock(MarkdownBlockType.Footnote, 0);
                block.FootnoteId = footnote.Id;
                block.FootnoteNumber = footnote.Number;
                AddInlines(block, footnote.Text);
                blocks.Add(block);
            }
        }

        private void ParseLines(IReadOnlyList<string> lines, int quoteDepth, List<MarkdownBlock> output)
        {
            int i = 0;
            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line))
                {
                    i++;
                    continue;
                }

                // Fenced code block.
                if (TryGetFence(line, out string fence, out char fenceChar, out int fenceIndent, out string info))
                {
                    i = ParseFencedCode(lines, i, fence.Length, fenceChar, fenceIndent, info, quoteDepth, output);
                    continue;
                }

                // ATX heading.
                Match heading = s_AtxHeading.Match(line);
                if (heading.Success)
                {
                    string text = s_ClosingHashes.Replace(heading.Groups["text"].Value, string.Empty).Trim();
                    var block = NewBlock(MarkdownBlockType.Heading, quoteDepth);
                    block.Level = heading.Groups["hashes"].Value.Length;
                    AddInlines(block, text);
                    output.Add(block);
                    i++;
                    continue;
                }

                // GFM table. The header line and delimiter row must agree on the
                // number of cells; otherwise the delimiter is treated as a rule.
                if (TryParseTable(lines, i, quoteDepth, output, out int tableEnd))
                {
                    i = tableEnd;
                    continue;
                }

                // Setext heading. This intentionally runs before thematic breaks:
                // "Title" followed by "---" is a heading, not a rule.
                if (i + 1 < lines.Count && !IsBlank(lines[i]) && TryGetSetextLevel(lines[i + 1], out int setextLevel))
                {
                    var block = NewBlock(MarkdownBlockType.Heading, quoteDepth);
                    block.Level = setextLevel;
                    AddInlines(block, line.Trim());
                    output.Add(block);
                    i += 2;
                    continue;
                }

                // Thematic break.
                if (IsThematicBreak(line))
                {
                    output.Add(NewBlock(MarkdownBlockType.ThematicBreak, quoteDepth));
                    i++;
                    continue;
                }

                // Block quote. Consecutive ">" lines are parsed recursively so that
                // nested quotes, lists and code blocks inside quotes work naturally.
                if (s_BlockQuote.IsMatch(line))
                {
                    i = ParseBlockQuote(lines, i, quoteDepth, output);
                    continue;
                }

                // List item.
                if (TryGetListMarker(line, out ListMarker marker))
                {
                    i = ParseListItem(lines, i, marker, quoteDepth, output);
                    continue;
                }

                // Indented code block (four spaces). Paragraph continuation is
                // handled by the paragraph branch, so this only starts after a
                // blank line or another block.
                if (GetIndent(line) >= 4)
                {
                    i = ParseIndentedCode(lines, i, quoteDepth, output);
                    continue;
                }

                i = ParseParagraph(lines, i, quoteDepth, output);
            }
        }

        private bool TryParseTable(
            IReadOnlyList<string> lines,
            int start,
            int quoteDepth,
            List<MarkdownBlock> output,
            out int next)
        {
            next = start;
            if (start + 1 >= lines.Count)
                return false;

            string headerLine = lines[start];
            if (!ContainsUnescapedPipe(headerLine))
                return false;

            List<string> headerCells = SplitTableRow(headerLine);
            List<string> delimiterCells = SplitTableRow(lines[start + 1]);
            if (headerCells.Count == 0 || delimiterCells.Count != headerCells.Count)
                return false;

            var alignments = new List<MarkdownTableAlignment>(delimiterCells.Count);
            for (int c = 0; c < delimiterCells.Count; c++)
            {
                string delimiter = delimiterCells[c].Trim();
                if (!s_TableDelimiterCell.IsMatch(delimiter))
                    return false;

                bool left = delimiter[0] == ':';
                bool right = delimiter[delimiter.Length - 1] == ':';
                alignments.Add(left && right
                    ? MarkdownTableAlignment.Center
                    : left
                        ? MarkdownTableAlignment.Left
                        : right
                            ? MarkdownTableAlignment.Right
                            : MarkdownTableAlignment.Default);
            }

            var table = new MarkdownTable();
            for (int c = 0; c < alignments.Count; c++)
            {
                table.Alignments.Add(alignments[c]);
                table.Header.Add(ParseTableCell(c < headerCells.Count ? headerCells[c] : string.Empty));
            }

            int i = start + 2;
            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line) || !ContainsUnescapedPipe(line))
                    break;

                List<string> cells = SplitTableRow(line);
                if (cells.Count == 0)
                    break;

                var row = new List<MarkdownTableCell>(table.ColumnCount);
                for (int c = 0; c < table.ColumnCount; c++)
                    row.Add(ParseTableCell(c < cells.Count ? cells[c] : string.Empty));
                table.Rows.Add(row);
                i++;
            }

            var block = NewBlock(MarkdownBlockType.Table, quoteDepth);
            block.Table = table;
            output.Add(block);
            next = i;
            return true;
        }

        private static bool LooksLikeTableStart(IReadOnlyList<string> lines, int index)
        {
            if (index + 1 >= lines.Count)
                return false;

            string header = lines[index];
            if (!ContainsUnescapedPipe(header))
                return false;

            List<string> headerCells = SplitTableRow(header);
            List<string> delimiterCells = SplitTableRow(lines[index + 1]);
            if (headerCells.Count == 0 || delimiterCells.Count != headerCells.Count)
                return false;

            for (int i = 0; i < delimiterCells.Count; i++)
            {
                if (!s_TableDelimiterCell.IsMatch(delimiterCells[i].Trim()))
                    return false;
            }

            return true;
        }

        private MarkdownTableCell ParseTableCell(string text)
        {
            var cell = new MarkdownTableCell();
            var inlines = MarkdownInlineParser.Parse(text.Trim(), m_InlineContext);
            for (int i = 0; i < inlines.Count; i++)
                cell.Inlines.Add(inlines[i]);
            return cell;
        }

        private static List<string> SplitTableRow(string line)
        {
            var cells = new List<string>();
            string text = line.Trim();
            if (text.Length == 0)
                return cells;

            if (text[0] == '|')
                text = text.Substring(1);
            if (text.Length > 0 && text[text.Length - 1] == '|' && (text.Length < 2 || text[text.Length - 2] != '\\'))
                text = text.Substring(0, text.Length - 1);

            var cell = new StringBuilder();
            int codeTicks = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < text.Length && text[i + 1] == '|')
                {
                    cell.Append('|');
                    i++;
                    continue;
                }

                if (c == '`')
                {
                    int run = 1;
                    while (i + run < text.Length && text[i + run] == '`')
                        run++;

                    if (codeTicks == 0)
                        codeTicks = run;
                    else if (run >= codeTicks)
                        codeTicks = 0;

                    cell.Append('`', run);
                    i += run - 1;
                    continue;
                }

                if (c == '|' && codeTicks == 0)
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Clear();
                    continue;
                }

                cell.Append(c);
            }

            cells.Add(cell.ToString().Trim());
            return cells;
        }

        private static bool ContainsUnescapedPipe(string line)
        {
            int codeTicks = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length)
                {
                    i++;
                    continue;
                }

                if (c == '`')
                {
                    int run = 1;
                    while (i + run < line.Length && line[i + run] == '`')
                        run++;

                    if (codeTicks == 0)
                        codeTicks = run;
                    else if (run >= codeTicks)
                        codeTicks = 0;

                    i += run - 1;
                    continue;
                }

                if (c == '|' && codeTicks == 0)
                    return true;
            }
            return false;
        }

        private int ParseFencedCode(
            IReadOnlyList<string> lines,
            int start,
            int fenceLength,
            char fenceChar,
            int fenceIndent,
            string info,
            int quoteDepth,
            List<MarkdownBlock> output)
        {
            var code = new StringBuilder();
            int i = start + 1;
            bool closed = false;

            while (i < lines.Count)
            {
                if (IsFenceClose(lines[i], fenceChar, fenceLength))
                {
                    closed = true;
                    i++;
                    break;
                }

                string codeLine = RemoveIndent(lines[i], fenceIndent);
                if (code.Length > 0)
                    code.Append('\n');
                code.Append(codeLine);
                i++;
            }

            var block = NewBlock(MarkdownBlockType.CodeBlock, quoteDepth);
            block.Code = code.ToString();
            block.Info = (info ?? string.Empty).Trim();
            output.Add(block);

            // An unclosed fence consumes the rest of the document, which mirrors
            // CommonMark and avoids silently dropping the remaining source.
            return closed ? i : lines.Count;
        }

        private int ParseBlockQuote(
            IReadOnlyList<string> lines,
            int start,
            int quoteDepth,
            List<MarkdownBlock> output)
        {
            var quoted = new List<string>();
            int i = start;

            while (i < lines.Count)
            {
                Match match = s_BlockQuote.Match(lines[i]);
                if (match.Success)
                {
                    quoted.Add(match.Groups["text"].Value);
                    i++;
                    continue;
                }

                // A blank line between two quoted lines belongs to the quote.
                if (IsBlank(lines[i]) && i + 1 < lines.Count && s_BlockQuote.IsMatch(lines[i + 1]))
                {
                    quoted.Add(string.Empty);
                    i++;
                    continue;
                }

                break;
            }

            int before = output.Count;
            ParseLines(quoted, quoteDepth + 1, output);

            // Empty quote: keep the block so an empty ">" line still advances the
            // layout in a predictable way.
            if (output.Count == before)
                output.Add(NewBlock(MarkdownBlockType.BlockQuote, quoteDepth + 1));

            return i;
        }

        private int ParseListItem(
            IReadOnlyList<string> lines,
            int start,
            ListMarker marker,
            int quoteDepth,
            List<MarkdownBlock> output)
        {
            var itemLines = new List<string> { marker.Content ?? string.Empty };
            int contentIndent = marker.Indent + marker.Marker.Length + marker.Spaces;
            int i = start + 1;

            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line))
                    break;

                // Siblings end this item; indented nested lists are emitted by the
                // outer loop as separate blocks with a greater ListDepth.
                if (TryGetListMarker(line, out ListMarker nested))
                    break;

                int indent = GetIndent(line);

                if (indent > marker.Indent)
                {
                    // Continuation of the item, including wrapped source lines.
                    itemLines.Add(RemoveIndent(line, Math.Min(contentIndent, indent)));
                    i++;
                    continue;
                }

                if (IsBlockStart(line))
                    break;

                // Lazy continuation: a plain line immediately below an item belongs
                // to the item even when it is not indented.
                itemLines.Add(line.TrimStart());
                i++;
            }

            string text = JoinParagraphLines(itemLines);
            var block = NewBlock(MarkdownBlockType.ListItem, quoteDepth);
            block.ListDepth = Math.Min(6, marker.Indent / 2);
            block.IsOrderedList = marker.IsOrdered;
            block.ListNumber = marker.Number;
            AddInlines(block, text);
            output.Add(block);

            return i;
        }

        private int ParseIndentedCode(
            IReadOnlyList<string> lines,
            int start,
            int quoteDepth,
            List<MarkdownBlock> output)
        {
            var code = new StringBuilder();
            int i = start;

            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line))
                {
                    if (code.Length > 0)
                        code.Append('\n');
                    i++;
                    continue;
                }

                if (GetIndent(line) < 4)
                    break;

                if (code.Length > 0)
                    code.Append('\n');
                code.Append(RemoveIndent(line, 4));
                i++;
            }

            var block = NewBlock(MarkdownBlockType.CodeBlock, quoteDepth);
            block.Code = code.ToString().TrimEnd('\n');
            block.Info = string.Empty;
            output.Add(block);
            return i;
        }

        private int ParseParagraph(
            IReadOnlyList<string> lines,
            int start,
            int quoteDepth,
            List<MarkdownBlock> output)
        {
            var paragraph = new List<string>();
            int i = start;

            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line))
                    break;

                if (paragraph.Count > 0 && IsBlockStart(line))
                    break;

                if (paragraph.Count > 0 && LooksLikeTableStart(lines, i))
                    break;

                // A setext underline terminates the paragraph and is consumed by
                // the heading branch on the next iteration only when this is the
                // first line. For an inner paragraph line it is a thematic break.
                if (paragraph.Count > 0 && TryGetSetextLevel(line, out _))
                    break;

                paragraph.Add(line);
                i++;
            }

            if (paragraph.Count == 0)
            {
                // Defensive: always make progress even on an unexpected input.
                i = Math.Max(i, start + 1);
                return i;
            }

            string text = JoinParagraphLines(paragraph);
            if (!string.IsNullOrWhiteSpace(text))
                AddParagraphBlocks(text, quoteDepth, output);

            return i;
        }

        private void AddParagraphBlocks(string text, int quoteDepth, List<MarkdownBlock> output)
        {
            List<MarkdownInline> inlines = MarkdownInlineParser.Parse(text, m_InlineContext);
            var textRun = new List<MarkdownInline>();

            int index = 0;
            while (index < inlines.Count)
            {
                MarkdownInline inline = inlines[index];
                if (!inline.IsImage || string.IsNullOrEmpty(inline.Url))
                {
                    textRun.Add(inline);
                    index++;
                    continue;
                }

                FlushTextRun(textRun, quoteDepth, output);

                string url = inline.Url;
                var alt = new StringBuilder();
                while (index < inlines.Count &&
                       inlines[index].IsImage &&
                       string.Equals(inlines[index].Url, url, StringComparison.Ordinal))
                {
                    alt.Append(inlines[index].Text);
                    index++;
                }

                var image = NewBlock(MarkdownBlockType.Image, quoteDepth);
                image.ImageUrl = url;
                image.ImageAlt = alt.ToString();
                output.Add(image);
            }

            FlushTextRun(textRun, quoteDepth, output);
        }

        private void FlushTextRun(List<MarkdownInline> textRun, int quoteDepth, List<MarkdownBlock> output)
        {
            if (textRun.Count == 0)
                return;

            int first = 0;
            while (first < textRun.Count && textRun[first].Text.Length == 0)
                first++;
            int last = textRun.Count - 1;
            while (last >= first && textRun[last].Text.Length == 0)
                last--;

            if (first > last)
            {
                textRun.Clear();
                return;
            }

            if (first == last)
            {
                textRun[first] = Trimmed(textRun[first], true, true);
            }
            else
            {
                textRun[first] = Trimmed(textRun[first], true, false);
                textRun[last] = Trimmed(textRun[last], false, true);
            }

            var block = NewBlock(MarkdownBlockType.Paragraph, quoteDepth);
            for (int i = 0; i < textRun.Count; i++)
                block.Inlines.Add(textRun[i]);
            textRun.Clear();

            if (!string.IsNullOrWhiteSpace(block.PlainText))
                output.Add(block);
        }

        private static MarkdownInline Trimmed(MarkdownInline inline, bool trimStart, bool trimEnd)
        {
            string text = inline.Text;
            string trimmed = trimStart ? text.TrimStart() : text;
            if (trimEnd)
                trimmed = trimmed.TrimEnd();
            if (string.Equals(text, trimmed, StringComparison.Ordinal))
                return inline;
            return new MarkdownInline(trimmed, inline.Style, inline.Url);
        }

        private static MarkdownBlock NewBlock(MarkdownBlockType type, int quoteDepth)
        {
            return new MarkdownBlock(type)
            {
                QuoteDepth = quoteDepth
            };
        }

        private void AddInlines(MarkdownBlock block, string text)
        {
            var inlines = MarkdownInlineParser.Parse(text, m_InlineContext);
            for (int i = 0; i < inlines.Count; i++)
                block.Inlines.Add(inlines[i]);
        }

        private static string JoinParagraphLines(IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
                return string.Empty;

            var result = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();
                bool last = i == lines.Count - 1;

                if (!last)
                {
                    bool hardBreak = EndsWithHardBreak(raw);
                    if (hardBreak && trimmed.EndsWith("\\", StringComparison.Ordinal))
                        trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();

                    result.Append(trimmed);

                    if (hardBreak)
                        result.Append('\n');
                    else if (result.Length > 0 && !char.IsWhiteSpace(result[result.Length - 1]))
                        result.Append(' ');
                }
                else
                {
                    result.Append(trimmed);
                }
            }

            return result.ToString();
        }

        private static bool EndsWithHardBreak(string line)
        {
            int end = line.Length - 1;
            if (end < 0)
                return false;
            if (line[end] == '\\')
                return true;

            int spaces = 0;
            while (end >= 0 && line[end] == ' ')
            {
                spaces++;
                end--;
            }
            return spaces >= 2;
        }

        private static bool TryGetFence(
            string line,
            out string fence,
            out char fenceChar,
            out int indent,
            out string info)
        {
            Match match = s_Fence.Match(line);
            if (!match.Success)
            {
                fence = null;
                fenceChar = '\0';
                indent = 0;
                info = null;
                return false;
            }

            fence = match.Groups["fence"].Value;
            fenceChar = fence[0];
            indent = match.Groups["indent"].Length;
            info = match.Groups["info"].Value;

            // A backtick fence's info string may not contain backticks.
            if (fenceChar == '`' && info.IndexOf('`') >= 0)
                return false;

            return true;
        }

        private static bool IsFenceClose(string line, char fenceChar, int minLength)
        {
            int indent = 0;
            while (indent < line.Length && indent < 4 && line[indent] == ' ')
                indent++;

            if (indent > 3)
                return false;

            int run = 0;
            int i = indent;
            while (i < line.Length && line[i] == fenceChar)
            {
                run++;
                i++;
            }

            if (run < minLength)
                return false;

            while (i < line.Length)
            {
                if (line[i] != ' ' && line[i] != '\t')
                    return false;
                i++;
            }

            return true;
        }

        private static bool TryGetSetextLevel(string line, out int level)
        {
            Match match = s_SetextUnderline.Match(line);
            if (!match.Success)
            {
                level = 0;
                return false;
            }

            level = match.Groups["mark"].Value[0] == '=' ? 1 : 2;
            return true;
        }

        private static bool IsThematicBreak(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length < 3)
                return false;

            char marker = trimmed[0];
            if (marker != '*' && marker != '-' && marker != '_')
                return false;

            int count = 0;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == marker)
                {
                    count++;
                    continue;
                }

                if (c == ' ' || c == '\t')
                    continue;

                return false;
            }

            return count >= 3;
        }

        private static bool TryGetListMarker(string line, out ListMarker marker)
        {
            Match match = s_ListMarker.Match(line);
            if (!match.Success)
            {
                marker = default;
                return false;
            }

            string indentText = match.Groups["indent"].Value;
            string markerText = match.Groups["marker"].Value;
            string spacesText = match.Groups["spaces"].Value;
            int indent = indentText.Length;

            // Very deep indentation is more likely to be code or malformed input.
            if (indent > 24)
            {
                marker = default;
                return false;
            }

            bool ordered = char.IsDigit(markerText[0]);
            int number = 0;
            if (ordered)
            {
                string digits = markerText.Substring(0, markerText.Length - 1);
                int.TryParse(digits, out number);
            }

            marker = new ListMarker
            {
                Indent = indent,
                Marker = markerText,
                Spaces = Math.Max(1, spacesText.Length),
                Content = match.Groups["content"].Value,
                IsOrdered = ordered,
                Number = number
            };
            return true;
        }

        private static bool IsBlockStart(string line)
        {
            if (IsBlank(line))
                return true;

            if (TryGetFence(line, out _, out _, out _, out _))
                return true;

            if (s_AtxHeading.IsMatch(line))
                return true;

            if (IsThematicBreak(line))
                return true;

            if (s_BlockQuote.IsMatch(line))
                return true;

            if (TryGetListMarker(line, out _))
                return true;

            return false;
        }

        private static bool IsBlank(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (!char.IsWhiteSpace(line[i]))
                    return false;
            }
            return true;
        }

        private static int GetIndent(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ')
                i++;
            return i;
        }

        private static string RemoveIndent(string line, int count)
        {
            if (count <= 0)
                return line;

            int i = 0;
            while (i < line.Length && i < count && line[i] == ' ')
                i++;
            return line.Substring(i);
        }

        private static string ExpandTabs(string text)
        {
            if (text.IndexOf('\t') < 0)
                return text;

            var sb = new StringBuilder(text.Length + 16);
            int column = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    sb.Append(c);
                    column = 0;
                }
                else if (c == '\t')
                {
                    int spaces = 4 - (column % 4);
                    sb.Append(' ', spaces);
                    column += spaces;
                }
                else
                {
                    sb.Append(c);
                    column++;
                }
            }
            return sb.ToString();
        }

        private struct ListMarker
        {
            public int Indent;
            public string Marker;
            public int Spaces;
            public string Content;
            public bool IsOrdered;
            public int Number;
        }
    }
}
