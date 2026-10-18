using System.Collections;
using SS.UIComponent;
using UnityEngine;
using UnityEngine.UI;

namespace SSMarkdown.Demo
{
    /// <summary>Small showcase controller for the SSMarkdown demo scene.</summary>
    public sealed class MarkdownDemoController : MonoBehaviour
    {
        [Header("Document")]
        [SerializeField] private MarkdownText m_Markdown;
        [SerializeField] private ScrollRect m_Scroll;
        [SerializeField] private TextAsset m_Overview;
        [SerializeField] private TextAsset m_Syntax;

        [Header("Chrome")]
        [SerializeField] private Image m_Background;
        [SerializeField] private Text m_Title;
        [SerializeField] private Text m_Subtitle;
        [SerializeField] private Text m_Status;
        [SerializeField] private Image[] m_ButtonBackgrounds;
        [SerializeField] private Text[] m_ButtonLabels;

        [Header("Buttons")]
        [SerializeField] private Button m_OverviewButton;
        [SerializeField] private Button m_SyntaxButton;
        [SerializeField] private Button m_ThemeButton;
        [SerializeField] private Button m_DecreaseButton;
        [SerializeField] private Button m_IncreaseButton;

        [Header("Colors")]
        [SerializeField] private Color m_LightBackground = new Color32(246, 243, 236, 255);
        [SerializeField] private Color m_DarkBackground = new Color32(13, 17, 23, 255);
        [SerializeField] private Color m_LightText = new Color32(36, 41, 47, 255);
        [SerializeField] private Color m_DarkText = new Color32(230, 237, 243, 255);

        private bool m_Dark;

        private void Start()
        {
            if (m_OverviewButton != null) m_OverviewButton.onClick.AddListener(ShowOverview);
            if (m_SyntaxButton != null) m_SyntaxButton.onClick.AddListener(ShowSyntax);
            if (m_ThemeButton != null) m_ThemeButton.onClick.AddListener(ToggleTheme);
            if (m_DecreaseButton != null) m_DecreaseButton.onClick.AddListener(DecreaseFont);
            if (m_IncreaseButton != null) m_IncreaseButton.onClick.AddListener(IncreaseFont);

            if (m_Markdown != null)
            {
                m_Markdown.onLinkClicked.AddListener(OnLinkClicked);
                m_Markdown.UseLightTheme();
            }

            ApplyTheme(false);
            ShowOverview();
        }

        private void OnDestroy()
        {
            if (m_Markdown != null)
                m_Markdown.onLinkClicked.RemoveListener(OnLinkClicked);
        }

        public void ShowOverview()
        {
            LoadDocument(m_Overview, "Overview");
        }

        public void ShowSyntax()
        {
            LoadDocument(m_Syntax, "Syntax");
        }

        public void ToggleTheme()
        {
            ApplyTheme(!m_Dark);
        }

        public void DecreaseFont()
        {
            ChangeFontSize(-1);
        }

        public void IncreaseFont()
        {
            ChangeFontSize(1);
        }

        private void LoadDocument(TextAsset asset, string label)
        {
            if (m_Markdown == null)
                return;

            m_Markdown.SetMarkdown(asset);
            SetStatus("Document: " + label + "  |  Click a link to raise onLinkClicked.");
            StopAllCoroutines();
            StartCoroutine(ScrollToTop());
        }

        private void ChangeFontSize(int delta)
        {
            if (m_Markdown == null)
                return;

            m_Markdown.fontSize = Mathf.Clamp(m_Markdown.fontSize + delta, 13, 24);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)m_Markdown.transform);
            SetStatus("Font size: " + m_Markdown.fontSize);
        }

        private void ApplyTheme(bool dark)
        {
            m_Dark = dark;

            if (m_Background != null)
                m_Background.color = dark ? m_DarkBackground : m_LightBackground;

            Color textColor = dark ? m_DarkText : m_LightText;
            Color secondary = dark ? new Color32(139, 148, 158, 255) : new Color32(87, 96, 106, 255);
            Color buttonColor = dark ? new Color32(255, 255, 255, 20) : new Color32(15, 23, 42, 15);

            if (m_Title != null) m_Title.color = textColor;
            if (m_Subtitle != null) m_Subtitle.color = secondary;
            if (m_Status != null) m_Status.color = secondary;

            if (m_ButtonBackgrounds != null)
            {
                for (int i = 0; i < m_ButtonBackgrounds.Length; i++)
                {
                    if (m_ButtonBackgrounds[i] != null)
                        m_ButtonBackgrounds[i].color = buttonColor;
                }
            }

            if (m_ButtonLabels != null)
            {
                for (int i = 0; i < m_ButtonLabels.Length; i++)
                {
                    if (m_ButtonLabels[i] != null)
                        m_ButtonLabels[i].color = textColor;
                }
            }

            if (m_Markdown != null)
            {
                m_Markdown.color = textColor;
                if (dark)
                    m_Markdown.UseDarkTheme();
                else
                    m_Markdown.UseLightTheme();
            }
        }

        private void OnLinkClicked(string url)
        {
            SetStatus("onLinkClicked: " + url);
            Debug.Log("[SSMarkdown Demo] Link clicked: " + url);
        }

        private void SetStatus(string message)
        {
            if (m_Status != null)
                m_Status.text = message;
        }

        private IEnumerator ScrollToTop()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (m_Scroll != null)
                m_Scroll.verticalNormalizedPosition = 1f;
        }
    }
}
