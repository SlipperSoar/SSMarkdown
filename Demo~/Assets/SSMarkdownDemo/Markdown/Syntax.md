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
- GFM tables with left, center and right alignment
- Thematic breaks with `---`, `***` or `___`

## Inline elements

- **Bold** with `**` or `__`
- *Italic* with `*` or `_`
- ~~Strikethrough~~ with `~~`
- `Inline code` with backticks
- [Links](https://example.com) with an optional title
- Reference links such as `[text][id]` and `[id]`
- Footnotes such as `a note[^1]`
- Autolinks such as <https://unity.com>
- Bare URLs such as https://docs.unity3d.com
- Backslash escapes and common HTML entities

## Not implemented

Task lists, raw HTML styling and syntax highlighting are intentionally out of scope.

## References and footnotes

```markdown
Read [the docs][docs] and a note[^1].

[docs]: https://example.com/docs
[^1]: Footnotes are rendered at the end of the document.
```

## Images

Images are loaded asynchronously and shown inside the document. Local paths first try `Resources`, and `ab://` addresses load a texture from an AssetBundle.

```markdown
![Resources logo](SSMarkdownLogo)
![Remote image](https://example.com/image.png)
![Bundle image](ab://ui#Logo)
```

A failed load is replaced with an inline error box.

## Table example

| Feature | Ready | Example |
| :--- | :---: | ---: |
| Bold | Yes | `**text**` |
| Links | Yes | `[text](url)` |
| Tables | Yes | this one |

## Theme notes

The built-in LegacyRuntime font does not contain CJK glyphs. Import a font that covers your language and assign it to the `font` field for Chinese, Japanese or Korean documents.

```text
# Heading

Paragraph with **bold**, *italic*, `code` and a [link](https://example.com).

> Quote

- List item
```
