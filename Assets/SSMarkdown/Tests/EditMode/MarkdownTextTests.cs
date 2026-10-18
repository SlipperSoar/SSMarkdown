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

            m_View.UseLightTheme();
            Assert.AreEqual(0f, m_View.theme.backgroundColor.a, 0.001f);
        }

        [Test]
        public void Empty_text_has_zero_height()
        {
            m_View.text = string.Empty;
            Assert.AreEqual(0f, m_View.preferredHeight, 0.001f);
        }
    }
}
