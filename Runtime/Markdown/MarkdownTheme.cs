using System;
using UnityEngine;

namespace SS.UIComponent
{
    /// <summary>
    /// Serialized visual settings for <see cref="MarkdownText"/>. The theme is a
    /// plain serializable class so it can be edited inline and shared by copying
    /// it between components.
    /// </summary>
    [Serializable]
    public sealed class MarkdownTheme
    {
        [Header("Surface")]
        [Tooltip("Optional page fill drawn behind the document. Use alpha 0 for a transparent MarkdownText.")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0f);

        [Header("Text Colors")]
        [Tooltip("Body and list text color applied by ApplyTheme, UseLightTheme and UseDarkTheme. MarkdownText.color remains the source of truth at runtime.")]
        public Color bodyColor = new Color32(36, 41, 47, 255);
        public Color headingColor = new Color32(13, 20, 30, 255);
        public Color linkColor = new Color32(9, 105, 218, 255);
        public Color linkUnderlineColor = new Color32(9, 105, 218, 150);
        public Color codeColor = new Color32(163, 58, 91, 255);
        public Color codeBlockTextColor = new Color32(36, 41, 47, 255);
        public Color quoteColor = new Color32(87, 96, 106, 255);

        [Header("Decorations")]
        public Color inlineCodeBackgroundColor = new Color32(175, 184, 193, 45);
        public Color codeBlockBackgroundColor = new Color32(175, 184, 193, 35);
        public Color codeBlockAccentColor = new Color32(9, 105, 218, 160);
        public Color quoteBarColor = new Color32(175, 184, 193, 210);
        public Color ruleColor = new Color32(175, 184, 193, 160);
        public Color listMarkerColor = new Color32(87, 96, 106, 255);
        public Color strikeThroughColor = new Color32(87, 96, 106, 200);

        [Header("Metrics")]
        [Min(0.5f)] public float lineSpacing = 1.18f;
        [Min(0f)] public float paragraphSpacing = 10f;
        [Min(0f)] public float headingTopSpacing = 18f;
        [Min(0f)] public float headingBottomSpacing = 7f;
        [Min(0f)] public float listSpacing = 3f;
        [Min(0f)] public float listIndent = 20f;
        [Min(0f)] public float listMarkerGap = 8f;
        [Min(0f)] public float quoteIndent = 16f;
        [Min(0f)] public float quoteBarWidth = 3f;
        [Min(0f)] public float codePadding = 10f;
        [Min(0f)] public float codeBlockSpacing = 10f;
        [Min(0f)] public float ruleSpacing = 14f;
        [Min(1f)] public float ruleThickness = 1f;
        [Min(1f)] public float bulletSize = 4f;
        [Range(0.5f, 1.5f)] public float codeFontScale = 0.9f;
        [Range(0.5f, 2f)] public float quoteFontScale = 1f;

        [Header("Behavior")]
        public bool underlineLinks = true;
        public bool drawInlineCodeBackground = true;
        public bool drawCodeBlockBackground = true;
        public bool drawHeadingRules = true;
        public bool drawBullets = true;
        public float[] headingScales = { 1.9f, 1.55f, 1.3f, 1.15f, 1.05f, 1f };

        /// <summary>Create a theme matching a light documentation surface.</summary>
        public static MarkdownTheme CreateLight()
        {
            return new MarkdownTheme();
        }

        /// <summary>Create a theme matching a dark documentation surface.</summary>
        public static MarkdownTheme CreateDark()
        {
            var theme = new MarkdownTheme
            {
                backgroundColor = new Color32(13, 17, 23, 255),
                bodyColor = new Color32(230, 237, 243, 255),
                headingColor = new Color32(240, 246, 252, 255),
                linkColor = new Color32(88, 166, 255, 255),
                linkUnderlineColor = new Color32(88, 166, 255, 150),
                codeColor = new Color32(255, 123, 114, 255),
                codeBlockTextColor = new Color32(230, 237, 243, 255),
                quoteColor = new Color32(139, 148, 158, 255),
                inlineCodeBackgroundColor = new Color32(110, 118, 129, 60),
                codeBlockBackgroundColor = new Color32(110, 118, 129, 38),
                codeBlockAccentColor = new Color32(88, 166, 255, 180),
                quoteBarColor = new Color32(110, 118, 129, 190),
                ruleColor = new Color32(110, 118, 129, 150),
                listMarkerColor = new Color32(139, 148, 158, 255),
                strikeThroughColor = new Color32(139, 148, 158, 220)
            };
            return theme;
        }

        /// <summary>Copy every public setting from another theme.</summary>
        public void CopyFrom(MarkdownTheme other)
        {
            if (other == null)
                return;

            backgroundColor = other.backgroundColor;
            bodyColor = other.bodyColor;
            headingColor = other.headingColor;
            linkColor = other.linkColor;
            linkUnderlineColor = other.linkUnderlineColor;
            codeColor = other.codeColor;
            codeBlockTextColor = other.codeBlockTextColor;
            quoteColor = other.quoteColor;
            inlineCodeBackgroundColor = other.inlineCodeBackgroundColor;
            codeBlockBackgroundColor = other.codeBlockBackgroundColor;
            codeBlockAccentColor = other.codeBlockAccentColor;
            quoteBarColor = other.quoteBarColor;
            ruleColor = other.ruleColor;
            listMarkerColor = other.listMarkerColor;
            strikeThroughColor = other.strikeThroughColor;

            lineSpacing = other.lineSpacing;
            paragraphSpacing = other.paragraphSpacing;
            headingTopSpacing = other.headingTopSpacing;
            headingBottomSpacing = other.headingBottomSpacing;
            listSpacing = other.listSpacing;
            listIndent = other.listIndent;
            listMarkerGap = other.listMarkerGap;
            quoteIndent = other.quoteIndent;
            quoteBarWidth = other.quoteBarWidth;
            codePadding = other.codePadding;
            codeBlockSpacing = other.codeBlockSpacing;
            ruleSpacing = other.ruleSpacing;
            ruleThickness = other.ruleThickness;
            bulletSize = other.bulletSize;
            codeFontScale = other.codeFontScale;
            quoteFontScale = other.quoteFontScale;

            underlineLinks = other.underlineLinks;
            drawInlineCodeBackground = other.drawInlineCodeBackground;
            drawCodeBlockBackground = other.drawCodeBlockBackground;
            drawHeadingRules = other.drawHeadingRules;
            drawBullets = other.drawBullets;

            if (other.headingScales != null && other.headingScales.Length == 6)
                headingScales = (float[])other.headingScales.Clone();
        }

        /// <summary>Keep serialized values in a safe range.</summary>
        public void Validate()
        {
            if (headingScales == null || headingScales.Length != 6)
            {
                headingScales = new[] { 1.9f, 1.55f, 1.3f, 1.15f, 1.05f, 1f };
            }

            lineSpacing = Mathf.Max(0.5f, lineSpacing);
            paragraphSpacing = Mathf.Max(0f, paragraphSpacing);
            headingTopSpacing = Mathf.Max(0f, headingTopSpacing);
            headingBottomSpacing = Mathf.Max(0f, headingBottomSpacing);
            listSpacing = Mathf.Max(0f, listSpacing);
            listIndent = Mathf.Max(0f, listIndent);
            listMarkerGap = Mathf.Max(0f, listMarkerGap);
            quoteIndent = Mathf.Max(0f, quoteIndent);
            quoteBarWidth = Mathf.Max(0f, quoteBarWidth);
            codePadding = Mathf.Max(0f, codePadding);
            codeBlockSpacing = Mathf.Max(0f, codeBlockSpacing);
            ruleSpacing = Mathf.Max(0f, ruleSpacing);
            ruleThickness = Mathf.Max(1f, ruleThickness);
            bulletSize = Mathf.Max(1f, bulletSize);
            codeFontScale = Mathf.Clamp(codeFontScale, 0.5f, 1.5f);
            quoteFontScale = Mathf.Clamp(quoteFontScale, 0.5f, 2f);
        }
    }
}
