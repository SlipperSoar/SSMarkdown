using System.Collections.Generic;
using System.IO;
using SS.UIComponent;
using SSMarkdown.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SSMarkdown.Demo.Editor
{
    /// <summary>Creates the showcase scene so the repository has no hand-authored scene YAML drift.</summary>
    public static class DemoSceneBuilder
    {
        public const string ScenePath = "Assets/SSMarkdownDemo/Scenes/MarkdownDemo.unity";
        private const string OverviewPath = "Assets/SSMarkdownDemo/Markdown/Overview.md";
        private const string SyntaxPath = "Assets/SSMarkdownDemo/Markdown/Syntax.md";

        [MenuItem("SSMarkdown/Rebuild Demo Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/SSMarkdownDemo/Scenes");
            AssetDatabase.Refresh();

            TextAsset overview = AssetDatabase.LoadAssetAtPath<TextAsset>(OverviewPath);
            TextAsset syntax = AssetDatabase.LoadAssetAtPath<TextAsset>(SyntaxPath);
            Font font = MarkdownText.GetDefaultFont();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(246, 243, 236, 255);
            camera.cullingMask = 0;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            CreateEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvasGo.transform, new Color32(246, 243, 236, 255));
            Stretch(background.rectTransform);

            Text title = CreateText(
                "Title",
                canvasGo.transform,
                "SSMarkdown",
                font,
                30,
                FontStyle.Bold,
                new Color32(13, 20, 30, 255),
                TextAnchor.UpperLeft);
            SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -26f), new Vector2(700f, 42f));

            Text subtitle = CreateText(
                "Subtitle",
                canvasGo.transform,
                "Runtime Markdown for Unity UGUI",
                font,
                14,
                FontStyle.Normal,
                new Color32(87, 96, 106, 255),
                TextAnchor.UpperLeft);
            SetAnchored(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -68f), new Vector2(700f, 24f));

            Text badge = CreateText(
                "Badge",
                canvasGo.transform,
                "UGUI  ·  RUNTIME",
                font,
                11,
                FontStyle.Bold,
                new Color32(87, 96, 106, 255),
                TextAnchor.UpperRight);
            SetAnchored(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -40f), new Vector2(300f, 22f));

            Image headerRule = CreateImage("Header Rule", canvasGo.transform, new Color32(215, 210, 198, 255));
            var headerRuleRect = headerRule.rectTransform;
            headerRuleRect.anchorMin = new Vector2(0f, 1f);
            headerRuleRect.anchorMax = new Vector2(1f, 1f);
            headerRuleRect.pivot = new Vector2(0.5f, 1f);
            headerRuleRect.offsetMin = new Vector2(48f, -101f);
            headerRuleRect.offsetMax = new Vector2(-48f, -100f);

            var scrollGo = new GameObject("Scroll View", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(canvasGo.transform, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(48f, 82f);
            scrollRect.offsetMax = new Vector2(-48f, -116f);

            Image scrollHitArea = CreateImage("Viewport", scrollGo.transform, new Color(1f, 1f, 1f, 0.001f));
            var viewport = scrollHitArea.rectTransform;
            Stretch(viewport);
            scrollHitArea.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Markdown Content", typeof(RectTransform), typeof(CanvasRenderer), typeof(MarkdownText), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var markdown = contentGo.GetComponent<MarkdownText>();
            markdown.font = font;
            markdown.fontSize = 17;
            markdown.color = new Color32(36, 41, 47, 255);
            markdown.padding = new Vector4(6f, 4f, 6f, 48f);
            markdown.text = overview != null ? overview.text : "# SSMarkdown";

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 28f;

            Image footerRule = CreateImage("Footer Rule", canvasGo.transform, new Color32(215, 210, 198, 255));
            var footerRuleRect = footerRule.rectTransform;
            footerRuleRect.anchorMin = new Vector2(0f, 0f);
            footerRuleRect.anchorMax = new Vector2(1f, 0f);
            footerRuleRect.pivot = new Vector2(0.5f, 0f);
            footerRuleRect.offsetMin = new Vector2(48f, 71f);
            footerRuleRect.offsetMax = new Vector2(-48f, 72f);

            Text status = CreateText(
                "Status",
                canvasGo.transform,
                "Ready.",
                font,
                12,
                FontStyle.Normal,
                new Color32(87, 96, 106, 255),
                TextAnchor.MiddleLeft);
            var statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.offsetMin = new Vector2(48f, 16f);
            statusRect.offsetMax = new Vector2(-460f, 56f);

            var buttonBackgrounds = new List<Image>();
            var buttonLabels = new List<Text>();

            Button overviewButton = CreateButton(
                canvasGo.transform, "Overview Button", "Overview", font,
                new Vector2(-344f, 36f), new Vector2(92f, 32f),
                buttonBackgrounds, buttonLabels);
            Button syntaxButton = CreateButton(
                canvasGo.transform, "Syntax Button", "Syntax", font,
                new Vector2(-244f, 36f), new Vector2(92f, 32f),
                buttonBackgrounds, buttonLabels);
            Button themeButton = CreateButton(
                canvasGo.transform, "Theme Button", "Theme", font,
                new Vector2(-144f, 36f), new Vector2(92f, 32f),
                buttonBackgrounds, buttonLabels);
            Button decreaseButton = CreateButton(
                canvasGo.transform, "Decrease Font", "A-", font,
                new Vector2(-84f, 36f), new Vector2(52f, 32f),
                buttonBackgrounds, buttonLabels);
            Button increaseButton = CreateButton(
                canvasGo.transform, "Increase Font", "A+", font,
                new Vector2(-24f, 36f), new Vector2(52f, 32f),
                buttonBackgrounds, buttonLabels);

            var controllerGo = new GameObject("Demo Controller", typeof(MarkdownDemoController));
            controllerGo.transform.SetParent(canvasGo.transform, false);
            var controller = controllerGo.GetComponent<MarkdownDemoController>();

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("m_Markdown").objectReferenceValue = markdown;
            serializedController.FindProperty("m_Scroll").objectReferenceValue = scroll;
            serializedController.FindProperty("m_Overview").objectReferenceValue = overview;
            serializedController.FindProperty("m_Syntax").objectReferenceValue = syntax;
            serializedController.FindProperty("m_Background").objectReferenceValue = background;
            serializedController.FindProperty("m_Title").objectReferenceValue = title;
            serializedController.FindProperty("m_Subtitle").objectReferenceValue = subtitle;
            serializedController.FindProperty("m_Status").objectReferenceValue = status;
            serializedController.FindProperty("m_OverviewButton").objectReferenceValue = overviewButton;
            serializedController.FindProperty("m_SyntaxButton").objectReferenceValue = syntaxButton;
            serializedController.FindProperty("m_ThemeButton").objectReferenceValue = themeButton;
            serializedController.FindProperty("m_DecreaseButton").objectReferenceValue = decreaseButton;
            serializedController.FindProperty("m_IncreaseButton").objectReferenceValue = increaseButton;

            SerializedProperty backgrounds = serializedController.FindProperty("m_ButtonBackgrounds");
            backgrounds.arraySize = buttonBackgrounds.Count;
            for (int i = 0; i < buttonBackgrounds.Count; i++)
                backgrounds.GetArrayElementAtIndex(i).objectReferenceValue = buttonBackgrounds[i];

            SerializedProperty labels = serializedController.FindProperty("m_ButtonLabels");
            labels.arraySize = buttonLabels.Count;
            for (int i = 0; i < buttonLabels.Count; i++)
                labels.GetArrayElementAtIndex(i).objectReferenceValue = buttonLabels[i];

            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = contentGo;
            Debug.Log("[SSMarkdown] Demo scene created at " + ScenePath);
        }

        private static void CreateEventSystem()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemGo.transform.position = Vector3.zero;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Font font,
            Vector2 anchoredPosition,
            Vector2 size,
            List<Image> backgrounds,
            List<Text> labels)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color32(15, 23, 42, 15);
            backgrounds.Add(image);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(245, 245, 245, 255);
            colors.pressedColor = new Color32(225, 225, 225, 255);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = CreateText(
                "Label",
                go.transform,
                label,
                font,
                13,
                FontStyle.Normal,
                new Color32(36, 41, 47, 255),
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 4f, 2f);
            labels.Add(text);

            return button;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            Font font,
            int size,
            FontStyle style,
            Color color,
            TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void SetAnchored(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
