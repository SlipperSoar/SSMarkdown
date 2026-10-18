using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SS.UIComponent.Editor
{
    [CustomEditor(typeof(MarkdownText))]
    [CanEditMultipleObjects]
    public sealed class MarkdownTextEditor : GraphicEditor
    {
        private SerializedProperty m_Text;
        private SerializedProperty m_Font;
        private SerializedProperty m_FontSize;
        private SerializedProperty m_Alignment;
        private SerializedProperty m_Padding;
        private SerializedProperty m_Theme;
        private SerializedProperty m_EnableLinks;
        private SerializedProperty m_OnLinkClicked;
        private bool m_ShowTheme = true;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_Text = serializedObject.FindProperty("m_Text");
            m_Font = serializedObject.FindProperty("m_Font");
            m_FontSize = serializedObject.FindProperty("m_FontSize");
            m_Alignment = serializedObject.FindProperty("m_Alignment");
            m_Padding = serializedObject.FindProperty("m_Padding");
            m_Theme = serializedObject.FindProperty("m_Theme");
            m_EnableLinks = serializedObject.FindProperty("m_EnableLinks");
            m_OnLinkClicked = serializedObject.FindProperty("m_OnLinkClicked");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Text, new GUIContent("Markdown"));
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Typography", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_Font);
            EditorGUILayout.PropertyField(m_FontSize);
            EditorGUILayout.PropertyField(m_Alignment);
            EditorGUILayout.PropertyField(m_Padding);

            EditorGUILayout.Space(4f);
            m_ShowTheme = EditorGUILayout.Foldout(m_ShowTheme, "Theme", true, EditorStyles.foldoutHeader);
            if (m_ShowTheme)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_Theme, new GUIContent("Style"), true);
                EditorGUI.indentLevel--;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Light"))
                        ApplyTheme(false);
                    if (GUILayout.Button("Dark"))
                        ApplyTheme(true);
                    if (GUILayout.Button("Refresh"))
                        RefreshTargets();
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Links", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_EnableLinks);
            EditorGUILayout.PropertyField(m_OnLinkClicked);

            EditorGUILayout.Space(8f);
            AppearanceControlsGUI();
            RaycastControlsGUI();
            MaskableControlsGUI();

            if (targets.Length == 1)
            {
                var markdown = (MarkdownText)target;
                int blocks = markdown.document.Blocks.Count;
                string summary = string.Format(
                    "{0} blocks  ·  {1} characters  ·  {2:0.#} px content height",
                    blocks,
                    markdown.text.Length,
                    markdown.contentHeight);
                EditorGUILayout.HelpBox(summary, MessageType.Info);

                if (markdown.font == null)
                {
                    EditorGUILayout.HelpBox(
                        "No font assigned. MarkdownText will try to use Unity's built-in LegacyRuntime font at runtime.",
                        MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyTheme(bool dark)
        {
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targets.Length; i++)
            {
                var markdown = targets[i] as MarkdownText;
                if (markdown == null)
                    continue;

                Undo.RecordObject(markdown, dark ? "Apply Dark Markdown Theme" : "Apply Light Markdown Theme");
                if (dark)
                    markdown.UseDarkTheme();
                else
                    markdown.UseLightTheme();
                EditorUtility.SetDirty(markdown);
            }
        }

        private void RefreshTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                var markdown = targets[i] as MarkdownText;
                if (markdown != null)
                    markdown.Refresh();
            }
        }
    }

    internal static class MarkdownTextMenu
    {
        [MenuItem("GameObject/UI/Markdown Text", false, 2000)]
        private static void CreateMarkdownText(MenuCommand command)
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 800f);
                scaler.matchWidthOrHeight = 0.5f;

                if (Object.FindObjectOfType<EventSystem>() == null)
                {
                    var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                    Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
                }
            }

            var go = new GameObject("Markdown Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(MarkdownText));
            GameObject context = command.context as GameObject;
            if (context == null || context.GetComponent<RectTransform>() == null)
                context = canvas.gameObject;
            GameObjectUtility.SetParentAndAlign(go, context);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(560f, 360f);
            rect.anchoredPosition = Vector2.zero;

            var markdown = go.GetComponent<MarkdownText>();
            markdown.font = MarkdownText.GetDefaultFont();
            markdown.fontSize = 16;
            markdown.color = new Color32(36, 41, 47, 255);
            markdown.text = "# Markdown Text\n\nStart writing **Markdown** here.\n\n- Supports lists\n- Clickable [links](https://example.com)\n- Inline `code`";

            Undo.RegisterCreatedObjectUndo(go, "Create Markdown Text");
            Selection.activeGameObject = go;
        }

        private static Canvas FindCanvas()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                Canvas selectedCanvas = selected.GetComponentInParent<Canvas>();
                if (selectedCanvas != null)
                    return selectedCanvas;
            }

            return Object.FindObjectOfType<Canvas>();
        }
    }
}
