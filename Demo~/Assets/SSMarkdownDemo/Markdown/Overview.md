# SSMarkdown

A **runtime Markdown renderer** for *Unity UGUI*. Assign a Markdown string or a `TextAsset` to the component exactly like `UnityEngine.UI.Text`, and SSMarkdown parses, lays out and draws the document into the Canvas.

It does not depend on TextMeshPro or any third-party package.

## Features

1. Pure UGUI `MaskableGraphic` rendering
2. Runtime parsing with no editor-only dependencies
3. Rich text styling through Unity's `TextGenerator`
4. Clickable links with `onLinkClicked`
5. Works in ScrollRects and layout groups

> The component handles the common documentation subset of Markdown.
> Unsupported constructs simply fall back to readable plain text.

## Assigning a document

```csharp
using SS.UIComponent;
using UnityEngine;

public class ReadmePanel : MonoBehaviour
{
    [SerializeField] private MarkdownText m_View;

    public void Show(string markdown)
    {
        m_View.text = markdown;
    }

    public void ShowAsset(TextAsset asset)
    {
        m_View.SetMarkdown(asset);
    }
}
```

## Lists

- Inline **bold**, *italic*, ~~strikethrough~~ and `code`
- Links such as [the Unity manual](https://docs.unity3d.com/Manual/)
- Nested items
  - with the same word-wrapping behavior
  - as every other block

1. Ordered lists keep their source numbers
2. The marker column is measured from the font

## Quotes

> Good runtime documentation should be readable without leaving the game.
> Links, code and emphasis all remain interactive inside a quote.

## Code

```csharp
var view = gameObject.AddComponent<MarkdownText>();
view.text = "# Hello\n\nThis is **Markdown**.";
view.onLinkClicked.AddListener(url => Debug.Log(url));
```

## Links and images

Visit <https://unity.com> or click [the API documentation](https://example.com/docs).

Image syntax is supported as its alt text: ![SSMarkdown logo](logo.png).

The end.
