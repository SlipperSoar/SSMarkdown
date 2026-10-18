# Markdown syntax

SSMarkdown implements the documentation-oriented subset below. Anything outside it is rendered as normal text rather than throwing at runtime.

## Block elements

- `#` through `######` ATX headings
- Setext headings with `===` or `---`
- Paragraphs and hard line breaks
- Fenced code blocks with backticks or tildes
- Indented code blocks
- Block quotes, including nested quotes
- Ordered and unordered lists, including nested items
- Thematic breaks with `---`, `***` or `___`

## Inline elements

- **Bold** with `**` or `__`
- *Italic* with `*` or `_`
- ~~Strikethrough~~ with `~~`
- `Inline code` with backticks
- [Links](https://example.com) with an optional title
- Autolinks such as <https://unity.com>
- Bare URLs such as https://docs.unity3d.com
- Backslash escapes and common HTML entities

## Not implemented

Tables, footnotes, reference-style links, task lists, raw HTML styling and syntax highlighting are intentionally out of scope. Image syntax displays its alt text as a link target.

## Theme notes

The built-in LegacyRuntime font does not contain CJK glyphs. Import a font that covers your language and assign it to the `font` field for Chinese, Japanese or Korean documents.

```text
# Heading

Paragraph with **bold**, *italic*, `code` and a [link](https://example.com).

> Quote

- List item
```
