using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SS.UIComponent
{
    internal struct MarkdownSolidRect
    {
        public MarkdownSolidRect(Rect rect, Color color)
        {
            Rect = rect;
            Color = color;
        }

        public Rect Rect;
        public Color Color;
    }

    /// <summary>
    /// Draws the solid decoration layer (code backgrounds, quote bars, rules,
    /// underlines) behind a <see cref="MarkdownText"/>. A Graphic can only use one
    /// texture, so this separate sibling layer keeps the font atlas untouched and
    /// lets the text layer render with the normal UGUI font material.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class MarkdownDecorationLayer : MaskableGraphic
    {
        [SerializeField, HideInInspector] private MarkdownText m_Owner;

        private List<MarkdownSolidRect> m_Quads;

        public override Texture mainTexture => Texture2D.whiteTexture;

        internal MarkdownText Owner => m_Owner;

        private void Update()
        {
            if (m_Owner != null)
                return;

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        internal void Initialize(MarkdownText owner)
        {
            m_Owner = owner;
            raycastTarget = false;
            color = Color.white;
        }

        internal void SetQuads(List<MarkdownSolidRect> quads)
        {
            m_Quads = quads;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (m_Quads == null || m_Quads.Count == 0)
                return;

            for (int i = 0; i < m_Quads.Count; i++)
            {
                MarkdownSolidRect quad = m_Quads[i];
                if (quad.Rect.width <= 0f || quad.Rect.height <= 0f)
                    continue;

                var vertex = UIVertex.simpleVert;
                vertex.color = quad.Color;

                vertex.position = new Vector3(quad.Rect.xMin, quad.Rect.yMin);
                vh.AddVert(vertex);
                vertex.position = new Vector3(quad.Rect.xMin, quad.Rect.yMax);
                vh.AddVert(vertex);
                vertex.position = new Vector3(quad.Rect.xMax, quad.Rect.yMax);
                vh.AddVert(vertex);
                vertex.position = new Vector3(quad.Rect.xMax, quad.Rect.yMin);
                vh.AddVert(vertex);

                int start = vh.currentVertCount - 4;
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start + 2, start + 3, start);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
        }
#endif
    }
}
