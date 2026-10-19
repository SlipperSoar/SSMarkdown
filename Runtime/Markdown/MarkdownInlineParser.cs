using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SS.UIComponent
{
    /// <summary>Parses the inline (span-level) portion of a Markdown block.</summary>
    internal static class MarkdownInlineParser
    {
        private static readonly Regex s_Email = new Regex(
            @"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$",
            RegexOptions.CultureInvariant);

        private static readonly Regex s_HtmlTag = new Regex(
            @"^/?[A-Za-z][A-Za-z0-9-]*(?:\s[^<>]*)?/?$",
            RegexOptions.CultureInvariant);

        public static List<MarkdownInline> Parse(string text, MarkdownInlineContext context = null)
        {
            var output = new List<MarkdownInline>();
            if (string.IsNullOrEmpty(text))
                return output;

            ParseRange(text, 0, text.Length, MarkdownInlineStyle.None, null, true, output, context);
            return output;
        }

        private static void ParseRange(
            string text,
            int start,
            int end,
            MarkdownInlineStyle style,
            string url,
            bool allowLinks,
            List<MarkdownInline> output,
            MarkdownInlineContext context)
        {
            var buffer = new StringBuilder();
            int i = start;

            while (i < end)
            {
                char c = text[i];

                if (c == '\\')
                {
                    if (i + 1 < end && IsEscapable(text[i + 1]))
                    {
                        buffer.Append(text[i + 1]);
                        i += 2;
                        continue;
                    }

                    buffer.Append(c);
                    i++;
                    continue;
                }

                if (c == '`')
                {
                    Flush(buffer, style, url, false, output);
                    int run = CountRun(text, i, end, '`');
                    int close = FindCodeSpanClose(text, i + run, end, run);
                    if (close >= 0)
                    {
                        string code = text.Substring(i + run, close - (i + run)).Replace('\n', ' ');
                        if (code.Length >= 2 && code[0] == ' ' && code[code.Length - 1] == ' ' && !IsAllSpaces(code))
                            code = code.Substring(1, code.Length - 2);
                        AddSpan(output, code, style | MarkdownInlineStyle.Code, url, false);
                        i = close + run;
                    }
                    else
                    {
                        buffer.Append('`', run);
                        i += run;
                    }
                    continue;
                }

                if (c == '!' && i + 1 < end && text[i + 1] == '[' && allowLinks)
                {
                    if (TryParseLink(text, i + 1, end, out int labelStart, out int labelEnd, out int afterLink, out string imageUrl) ||
                        TryParseReferenceLink(text, i + 1, end, context, out labelStart, out labelEnd, out afterLink, out imageUrl))
                    {
                        Flush(buffer, style, url, false, output);
                        int firstImageSpan = output.Count;
                        ParseRange(text, labelStart, labelEnd, style | MarkdownInlineStyle.Link, imageUrl, false, output, context);
                        if (labelEnd == labelStart && !string.IsNullOrEmpty(imageUrl))
                            AddSpan(output, imageUrl, style | MarkdownInlineStyle.Link, imageUrl, true);

                        for (int k = firstImageSpan; k < output.Count; k++)
                            output[k].IsImage = true;

                        i = afterLink;
                        continue;
                    }
                }

                if (c == '[' && allowLinks)
                {
                    if (c == '[' && i + 1 < end && text[i + 1] == '^' && context != null)
                    {
                        int footnoteClose = FindClosingBracket(text, i, end);
                        if (footnoteClose > i + 2)
                        {
                            string footnoteId = MarkdownLinkReference.NormalizeLabel(
                                text.Substring(i + 2, footnoteClose - i - 2));
                            if (context.TryGetFootnoteNumber(footnoteId, out int footnoteNumber))
                            {
                                Flush(buffer, style, url, false, output);
                                AddSpan(
                                    output,
                                    "[" + footnoteNumber + "]",
                                    style | MarkdownInlineStyle.Footnote,
                                    "#footnote-" + footnoteId,
                                    false);
                                i = footnoteClose + 1;
                                continue;
                            }
                        }
                    }

                    if (TryParseLink(text, i, end, out int labelStart, out int labelEnd, out int afterLink, out string linkUrl) ||
                        TryParseReferenceLink(text, i, end, context, out labelStart, out labelEnd, out afterLink, out linkUrl))
                    {
                        Flush(buffer, style, url, false, output);
                        ParseRange(text, labelStart, labelEnd, style | MarkdownInlineStyle.Link, linkUrl, false, output, context);
                        if (labelEnd == labelStart && !string.IsNullOrEmpty(linkUrl))
                            AddSpan(output, linkUrl, style | MarkdownInlineStyle.Link, linkUrl, false);
                        i = afterLink;
                        continue;
                    }

                    buffer.Append(c);
                    i++;
                    continue;
                }

                if (c == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close > i + 1 && close < end && text.IndexOf('\n', i + 1, close - i - 1) < 0)
                    {
                        string inner = text.Substring(i + 1, close - i - 1).Trim();
                        Flush(buffer, style, url, false, output);

                        if (inner.Equals("br", StringComparison.OrdinalIgnoreCase) ||
                            inner.Equals("br/", StringComparison.OrdinalIgnoreCase) ||
                            inner.Equals("/br", StringComparison.OrdinalIgnoreCase))
                        {
                            buffer.Append('\n');
                        }
                        else if (IsUrlOrEmail(inner))
                        {
                            if (allowLinks)
                            {
                                string display = inner.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                                    ? inner.Substring("mailto:".Length)
                                    : inner;
                                AddSpan(output, display, style | MarkdownInlineStyle.Link, inner, false);
                            }
                            else
                            {
                                buffer.Append(inner);
                            }
                        }
                        else if (s_HtmlTag.IsMatch(inner))
                        {
                            // Raw HTML is intentionally ignored instead of being
                            // rendered as literal angle-bracket soup.
                        }
                        else
                        {
                            buffer.Append('<').Append(inner).Append('>');
                        }

                        i = close + 1;
                        continue;
                    }
                }

                if (c == '~' && i + 1 < end && text[i + 1] == '~')
                {
                    int close = FindDelimiter(text, i + 2, end, '~', 2, false);
                    if (close > i + 2)
                    {
                        Flush(buffer, style, url, false, output);
                        ParseRange(text, i + 2, close, style | MarkdownInlineStyle.Strikethrough, url, allowLinks, output, context);
                        i = close + 2;
                        continue;
                    }
                }

                if ((c == '*' || c == '_') && CanOpenDelimiter(text, i, end, c))
                {
                    int run = CountRun(text, i, end, c);
                    int used = run >= 3 ? 3 : run >= 2 ? 2 : 1;
                    MarkdownInlineStyle emphasisStyle = used == 3
                        ? MarkdownInlineStyle.Bold | MarkdownInlineStyle.Italic
                        : used == 2
                            ? MarkdownInlineStyle.Bold
                            : MarkdownInlineStyle.Italic;

                    int close = FindDelimiter(text, i + used, end, c, used, c == '_');
                    if (close > i + used)
                    {
                        Flush(buffer, style, url, false, output);
                        ParseRange(text, i + used, close, style | emphasisStyle, url, allowLinks, output, context);
                        i = close + used;
                        continue;
                    }
                }

                if (allowLinks && IsBareUrlStart(text, i, end))
                {
                    int boundary = i;
                    int urlEnd = FindBareUrlEnd(text, i, end);
                    if (urlEnd > boundary + 7)
                    {
                        string bare = TrimUrl(text.Substring(boundary, urlEnd - boundary));
                        if (bare.Length > 7)
                        {
                            Flush(buffer, style, url, false, output);
                            AddSpan(output, bare, style | MarkdownInlineStyle.Link, bare, false);
                            i = boundary + bare.Length;
                            continue;
                        }
                    }
                }

                if (c == '&' && TryDecodeEntity(text, i, end, out string decoded, out int entityEnd))
                {
                    buffer.Append(decoded);
                    i = entityEnd;
                    continue;
                }

                buffer.Append(c);
                i++;
            }

            Flush(buffer, style, url, false, output);
        }

        private static void Flush(
            StringBuilder buffer,
            MarkdownInlineStyle style,
            string url,
            bool isImage,
            List<MarkdownInline> output)
        {
            if (buffer.Length == 0)
                return;

            AddSpan(output, buffer.ToString(), style, url, isImage);
            buffer.Clear();
        }

        private static void AddSpan(
            List<MarkdownInline> output,
            string value,
            MarkdownInlineStyle style,
            string url,
            bool isImage)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (output.Count > 0)
            {
                MarkdownInline last = output[output.Count - 1];
                if (last.Style == style && string.Equals(last.Url, url, StringComparison.Ordinal) && last.IsImage == isImage)
                {
                    last.Append(value);
                    return;
                }
            }

            output.Add(new MarkdownInline(value, style, url) { IsImage = isImage });
        }

        private static int FindCodeSpanClose(string text, int start, int end, int runLength)
        {
            int i = start;
            while (i < end)
            {
                if (text[i] == '`')
                {
                    int run = CountRun(text, i, end, '`');
                    if (run == runLength)
                        return i;
                    i += run;
                }
                else
                {
                    i++;
                }
            }
            return -1;
        }

        private static int FindDelimiter(
            string text,
            int start,
            int end,
            char delimiter,
            int requiredRun,
            bool avoidIntraword)
        {
            int i = start;
            while (i < end)
            {
                if (text[i] == '\\')
                {
                    i += 2;
                    continue;
                }

                if (text[i] != delimiter)
                {
                    i++;
                    continue;
                }

                int run = CountRun(text, i, end, delimiter);
                if (run < requiredRun)
                {
                    i += run;
                    continue;
                }

                // A single emphasis delimiter cannot be closed by the middle of
                // a strong delimiter run. Odd runs are allowed so that the inner
                // emphasis of "***" can close before the outer strong consumes
                // the remaining two characters.
                if (requiredRun == 1 && (run & 1) == 0)
                {
                    i += run;
                    continue;
                }

                int closeStart = requiredRun == 1
                    ? i + run - 1
                    : requiredRun == 2
                        ? i + run - 2
                        : i;

                if (closeStart <= start || char.IsWhiteSpace(text[closeStart - 1]))
                {
                    i += run;
                    continue;
                }

                if (avoidIntraword)
                {
                    bool leftWord = closeStart > 0 && char.IsLetterOrDigit(text[closeStart - 1]);
                    int right = closeStart + requiredRun;
                    bool rightWord = right < end && char.IsLetterOrDigit(text[right]);
                    if (leftWord && rightWord)
                    {
                        i += run;
                        continue;
                    }
                }

                return closeStart;
            }

            return -1;
        }

        private static bool CanOpenDelimiter(string text, int index, int end, char delimiter)
        {
            int run = CountRun(text, index, end, delimiter);
            if (run <= 0)
                return false;

            int next = index + run;
            if (next >= end || char.IsWhiteSpace(text[next]))
                return false;

            if (delimiter == '_')
            {
                bool leftWord = index > 0 && char.IsLetterOrDigit(text[index - 1]);
                bool rightWord = next < end && char.IsLetterOrDigit(text[next]);
                if (leftWord && rightWord)
                    return false;
            }

            return true;
        }

        private static int CountRun(string text, int start, int end, char c)
        {
            int i = start;
            while (i < end && text[i] == c)
                i++;
            return i - start;
        }

        private static bool IsEscapable(char c)
        {
            switch (c)
            {
                case '\\':
                case '`':
                case '*':
                case '_':
                case '{':
                case '}':
                case '[':
                case ']':
                case '(':
                case ')':
                case '#':
                case '+':
                case '-':
                case '.':
                case '!':
                case '|':
                case '>':
                case '~':
                case '<':
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseReferenceLink(
            string text,
            int openBracket,
            int end,
            MarkdownInlineContext context,
            out int labelStart,
            out int labelEnd,
            out int after,
            out string url)
        {
            labelStart = 0;
            labelEnd = 0;
            after = 0;
            url = null;

            if (context == null || openBracket >= end || text[openBracket] != '[')
                return false;

            int closeBracket = FindClosingBracket(text, openBracket, end);
            if (closeBracket < 0)
                return false;

            int next = closeBracket + 1;
            string referenceLabel;
            if (next < end && text[next] == '[')
            {
                int closeReference = FindClosingBracket(text, next, end);
                if (closeReference < 0)
                    return false;

                referenceLabel = text.Substring(next + 1, closeReference - next - 1);
                if (referenceLabel.Length == 0)
                    referenceLabel = text.Substring(openBracket + 1, closeBracket - openBracket - 1);
                after = closeReference + 1;
            }
            else
            {
                referenceLabel = text.Substring(openBracket + 1, closeBracket - openBracket - 1);
                after = closeBracket + 1;
            }

            if (!context.TryGetReference(referenceLabel, out MarkdownLinkReference reference))
                return false;

            labelStart = openBracket + 1;
            labelEnd = closeBracket;
            url = reference.Url;
            return true;
        }

        private static int FindClosingBracket(string text, int openBracket, int end)
        {
            int depth = 0;
            for (int i = openBracket; i < end; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        private static bool TryParseLink(
            string text,
            int openBracket,
            int end,
            out int labelStart,
            out int labelEnd,
            out int after,
            out string url)
        {
            labelStart = 0;
            labelEnd = 0;
            after = 0;
            url = null;

            if (openBracket >= end || text[openBracket] != '[')
                return false;

            int depth = 0;
            int closeBracket = -1;
            for (int i = openBracket; i < end; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeBracket = i;
                        break;
                    }
                }
            }

            if (closeBracket < 0 || closeBracket + 1 >= end || text[closeBracket + 1] != '(')
                return false;

            int closeParen = FindClosingParen(text, closeBracket + 2, end);
            if (closeParen < 0)
                return false;

            string inside = text.Substring(closeBracket + 2, closeParen - closeBracket - 2).Trim();
            if (inside.Length == 0)
            {
                url = string.Empty;
            }
            else if (inside[0] == '<')
            {
                int angle = inside.IndexOf('>');
                if (angle < 0)
                    return false;
                url = inside.Substring(1, angle - 1);
            }
            else
            {
                int whitespace = -1;
                for (int i = 0; i < inside.Length; i++)
                {
                    if (char.IsWhiteSpace(inside[i]))
                    {
                        whitespace = i;
                        break;
                    }
                }

                url = whitespace >= 0 ? inside.Substring(0, whitespace) : inside;
            }

            url = Unescape(url).Trim();
            labelStart = openBracket + 1;
            labelEnd = closeBracket;
            after = closeParen + 1;
            return true;
        }

        private static int FindClosingParen(string text, int start, int end)
        {
            int depth = 1;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return value;

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length && IsEscapable(value[i + 1]))
                {
                    sb.Append(value[i + 1]);
                    i++;
                }
                else
                {
                    sb.Append(value[i]);
                }
            }
            return sb.ToString();
        }

        private static bool IsUrlOrEmail(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || s_Email.IsMatch(value);
        }

        private static bool IsBareUrlStart(string text, int index, int end)
        {
            if (index > 0)
            {
                char previous = text[index - 1];
                if (!char.IsWhiteSpace(previous) && previous != '(' && previous != '[' && previous != '>' && previous != '"')
                    return false;
            }

            if (index + 7 <= end && string.Compare(text, index, "http://", 0, 7, StringComparison.OrdinalIgnoreCase) == 0)
                return true;
            if (index + 8 <= end && string.Compare(text, index, "https://", 0, 8, StringComparison.OrdinalIgnoreCase) == 0)
                return true;
            return false;
        }

        private static int FindBareUrlEnd(string text, int start, int end)
        {
            int i = start;
            while (i < end)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == '<' || c == '>' || c == '"' || c == '\'')
                    break;
                i++;
            }
            return i;
        }

        private static string TrimUrl(string value)
        {
            int end = value.Length;
            while (end > 0)
            {
                char c = value[end - 1];
                if (c == '.' || c == ',' || c == ';' || c == ':' || c == '!' || c == '?' || c == '\'' || c == '"')
                {
                    end--;
                    continue;
                }

                if (c == ')' && CountChar(value, '(', end) < CountChar(value, ')', end))
                {
                    end--;
                    continue;
                }

                break;
            }
            return value.Substring(0, end);
        }

        private static int CountChar(string value, char c, int end)
        {
            int count = 0;
            for (int i = 0; i < end && i < value.Length; i++)
            {
                if (value[i] == c)
                    count++;
            }
            return count;
        }

        private static bool IsAllSpaces(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != ' ')
                    return false;
            }
            return true;
        }

        private static bool TryDecodeEntity(string text, int start, int end, out string decoded, out int entityEnd)
        {
            decoded = null;
            entityEnd = start;

            int semicolon = text.IndexOf(';', start + 1);
            if (semicolon < 0 || semicolon >= end || semicolon - start > 10)
                return false;

            string entity = text.Substring(start + 1, semicolon - start - 1);
            switch (entity)
            {
                case "amp": decoded = "&"; break;
                case "lt": decoded = "<"; break;
                case "gt": decoded = ">"; break;
                case "quot": decoded = "\""; break;
                case "apos": decoded = "'"; break;
                case "nbsp": decoded = "\u00A0"; break;
                default:
                    if (entity.Length > 1 && (entity[0] == '#'))
                    {
                        int code;
                        bool hex = entity.Length > 2 && (entity[1] == 'x' || entity[1] == 'X');
                        string number = hex ? entity.Substring(2) : entity.Substring(1);
                        try
                        {
                            code = Convert.ToInt32(number, hex ? 16 : 10);
                        }
                        catch (Exception)
                        {
                            return false;
                        }

                        if (code <= 0 || code > 0x10FFFF)
                            return false;

                        decoded = char.ConvertFromUtf32(code);
                    }
                    else
                    {
                        return false;
                    }
                    break;
            }

            entityEnd = semicolon + 1;
            return true;
        }
    }
}
