# Changelog

All notable changes to this project are documented here.

## [1.0.0] - 2026-10-18

### Added

- Runtime UGUI `MarkdownText` component with `text`, `SetMarkdown(string)` and `SetMarkdown(TextAsset)` APIs.
- Markdown parser for headings, paragraphs, emphasis, strikethrough, inline code, links, autolinks, lists, quotes, fenced/indented code blocks, thematic breaks, escapes and entities.
- Block layout using `TextGenerator`, with Light/Dark themes and configurable spacing.
- Clickable link hit testing and `onLinkClicked` event.
- Hidden decoration layer for code backgrounds, quote bars, heading rules, link underlines and list bullets.
- `ContentSizeFitter` / `ScrollRect` integration.
- Custom inspector, creation menu and generated demo scene.
- Unity EditMode tests and a dependency-free .NET parser test harness.
