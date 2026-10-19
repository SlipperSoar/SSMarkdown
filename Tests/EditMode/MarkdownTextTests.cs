using System.Linq;
using NUnit.Framework;
using SS.UIComponent;
using UnityEngine;
using UnityEngine.UI;

namespace SS.UIComponent.Tests
{
    public sealed class MarkdownTextTests
    {
        private GameObject m_Canvas;
        private MarkdownText m_View;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = new GameObject("Test Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = m_Canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.GetComponent<CanvasScaler>().scaleFactor = 1f;

            var go = new GameObject("Markdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(MarkdownText));
            go.transform.SetParent(m_Canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(600f, 320f);

            m_View = go.GetComponent<MarkdownText>();
            m_View.font = MarkdownText.GetDefaultFont();
            m_View.fontSize = 16;
            m_View.color = Color.black;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Canvas != null)
                Object.DestroyImmediate(m_Canvas);
        }

        [Test]
        public void Builds_a_non_empty_mesh()
        {
            m_View.text = "# Title\n\nHello **bold** and `code`.";
            Canvas.ForceUpdateCanvases();

            Assert.Greater(m_View.document.Blocks.Count, 0);
            Assert.Greater(m_View.contentHeight, 0f);
            Assert.Greater(m_View.preferredHeight, 0f);

            Mesh mesh = m_View.GetComponent<CanvasRenderer>().GetMesh();
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
        }

        [Test]
        public void Changing_text_changes_content_height()
        {
            m_View.text = "short";
            float shortHeight = m_View.preferredHeight;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < 12; i++)
                builder.Append("A line of text.\n\n");
            m_View.text = builder.ToString();
            float longHeight = m_View.preferredHeight;

            Assert.Greater(longHeight, shortHeight);
        }

        [Test]
        public void Link_can_be_found_in_local_space()
        {
            m_View.text = "[link](https://example.com)";
            m_View.padding = Vector4.zero;
            Canvas.ForceUpdateCanvases();

            bool found = false;
            string url = null;
            for (float x = -300f; x <= 300f && !found; x += 2f)
            {
                for (float y = -160f; y <= 160f && !found; y += 2f)
                {
                    if (m_View.TryGetLinkAtLocalPosition(new Vector2(x, y), out url))
                        found = true;
                }
            }

            Assert.IsTrue(found, "Expected to find a link hit box.");
            Assert.AreEqual("https://example.com", url);
        }

        [Test]
        public void Code_block_handles_angle_brackets()
        {
            m_View.text = "```csharp\nvar view = gameObject.AddComponent<MarkdownText>();\nif (a < b) { }\n```";
            Canvas.ForceUpdateCanvases();

            Mesh mesh = m_View.GetComponent<CanvasRenderer>().GetMesh();
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
            Assert.Greater(m_View.contentHeight, 0f);
        }

        [Test]
        public void Theme_can_be_switched()
        {
            m_View.UseDarkTheme();
            m_View.text = "> quote\n\n`code`";
            Canvas.ForceUpdateCanvases();
            Assert.Greater(m_View.contentHeight, 0f);
            Assert.Greater(m_View.theme.backgroundColor.a, 0f);
            Assert.Greater(m_View.color.grayscale, 0.5f, "Dark theme should switch body text to a light color.");

            m_View.UseLightTheme();
            Assert.AreEqual(0f, m_View.theme.backgroundColor.a, 0.001f);
            Assert.Less(m_View.color.grayscale, 0.5f, "Light theme should switch body text to a dark color.");
        }

        [Test]
        public void Renders_tables()
        {
            m_View.text = "| Name | Score |\n| :--- | ---: |\n| **Alice** | [link](https://example.com) |";
            Canvas.ForceUpdateCanvases();

            MarkdownBlock block = m_View.document.Blocks[0];
            Assert.AreEqual(MarkdownBlockType.Table, block.Type);
            Assert.AreEqual(2, block.Table.ColumnCount);
            Assert.Greater(m_View.contentHeight, 0f);

            Mesh mesh = m_View.GetComponent<CanvasRenderer>().GetMesh();
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
        }

        [Test]
        public void Renders_images_with_a_custom_loader()
        {
            var texture = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            texture.name = "SSMarkdownTestImage";
            m_View.imageLoader = (url, onLoaded, onError) => onLoaded(texture);
            m_View.text = "Before\n\n![logo](test://logo)\n\nAfter";
            Canvas.ForceUpdateCanvases();
            m_View.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

            MarkdownBlock image = System.Array.Find(m_View.document.Blocks.ToArray(), b => b.Type == MarkdownBlockType.Image);
            Assert.IsNotNull(image);
            Assert.AreEqual("logo", image.ImageAlt);

            RawImage raw = m_View.GetComponentInChildren<RawImage>(true);
            Assert.IsNotNull(raw);
            Assert.AreSame(texture, raw.texture);

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Failed_images_show_a_fallback()
        {
            m_View.imageLoader = (url, onLoaded, onError) => onError("not found");
            m_View.text = "![missing](test://missing)";
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(MarkdownBlockType.Image, m_View.document.Blocks[0].Type);
            Assert.Greater(m_View.contentHeight, 0f);

            Mesh mesh = m_View.GetComponent<CanvasRenderer>().GetMesh();
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
        }

        [Test]
        public void Renders_footnotes()
        {
            m_View.text = "A note[^n].\n\n[^n]: Footnote text.";
            Canvas.ForceUpdateCanvases();

            Assert.Greater(m_View.contentHeight, 0f);
            Assert.IsTrue(m_View.document.Blocks.Any(b => b.Type == MarkdownBlockType.Footnote));
            Assert.IsTrue(m_View.document.Blocks[0].Inlines.Any(
                s => (s.Style & MarkdownInlineStyle.Footnote) != 0));
        }

        [Test]
        public void Renders_task_lists()
        {
            m_View.text = "- [ ] Todo\n- [x] Done";
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(MarkdownTaskState.Unchecked, m_View.document.Blocks[0].TaskState);
            Assert.AreEqual(MarkdownTaskState.Checked, m_View.document.Blocks[1].TaskState);
            Assert.Greater(m_View.contentHeight, 0f);

            Mesh mesh = m_View.GetComponent<CanvasRenderer>().GetMesh();
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
        }

        [Test]
        public void Accepts_custom_syntax_highlighters()
        {
            m_View.codeHighlighter = new TestHighlighter();
            m_View.text = "```csharp\nvar x = 1; // <color=#fff>\n```";
            Canvas.ForceUpdateCanvases();

            Assert.Greater(m_View.contentHeight, 0f);
            Mesh mesh = m_View.GetComponent<CanvasRenderer>().GetMesh();
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
        }

        private sealed class TestHighlighter : IMarkdownSyntaxHighlighter
        {
            public System.Collections.Generic.IReadOnlyList<MarkdownHighlightSpan> Highlight(string language, string code)
            {
                return new[]
                {
                    new MarkdownHighlightSpan(0, 3, Color.blue),
                    new MarkdownHighlightSpan(code.Length - 1, 1, Color.red)
                };
            }
        }

        [Test]
        public void Empty_text_has_zero_height()
        {
            m_View.text = string.Empty;
            Assert.AreEqual(0f, m_View.preferredHeight, 0.001f);
        }
    }
}
