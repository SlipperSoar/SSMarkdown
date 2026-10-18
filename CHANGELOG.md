# Changelog

All notable changes to this project are documented here.

## [1.0.0] - 2026-10-18

### Changed

- Repository root is now a UPM package (`com.ssmarkdown`), with `Runtime`, `Editor` and `Tests` at the top level.
- The demo is a standalone Unity project under `Demo~/` and references the package through `file:../..`.
- Added Git URL and local installation instructions to the README.

### Added

- Runtime UGUI `MarkdownText` component with `text`, `SetMarkdown(string)` and `SetMarkdown(TextAsset)` APIs.
- Markdown parser for headings, paragraphs, emphasis, strikethrough, inline code, links, autolinks, lists, quotes, fenced/indented code blocks, thematic breaks, escapes and entities.
- Block layout using `TextGenerator`, with Light/Dark themes and configurable spacing.
- GFM table parsing and rendering with column alignment, borders, header styling, row striping and inline cell formatting.
- Dark theme paints an opaque page background and applies a light body/list color, so light text stays readable on light parents.
- Clickable link hit testing and `onLinkClicked` event.
- Hidden decoration layer for code backgrounds, quote bars, heading rules, link underlines and list bullets.
- `ContentSizeFitter` / `ScrollRect` integration.
- Custom inspector, creation menu and generated demo scene.
- Unity EditMode tests and a dependency-free .NET parser test harness.
