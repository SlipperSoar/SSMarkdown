using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace SS.UIComponent
{
    /// <summary>
    /// Loads an image URL into a Texture2D. Callbacks must be invoked on the main
    /// thread. Set <see cref="MarkdownText.imageLoader"/> to override the built-in
    /// Resources / AssetBundle / HTTP loader.
    /// </summary>
    public delegate void MarkdownImageLoader(
        string url,
        Action<Texture2D> onLoaded,
        Action<string> onError);

    /// <summary>
    /// Optional owner for the hidden image container. It removes itself if the
    /// MarkdownText that created it is destroyed independently.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class MarkdownImageLayer : MonoBehaviour
    {
        [SerializeField, HideInInspector] private MarkdownText m_Owner;

        internal void Initialize(MarkdownText owner)
        {
            m_Owner = owner;
        }

        private void Update()
        {
            if (m_Owner != null)
                return;

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }

    public sealed partial class MarkdownText
    {
        [NonSerialized] private readonly Dictionary<string, MarkdownImageEntry> m_ImageCache =
            new Dictionary<string, MarkdownImageEntry>(StringComparer.Ordinal);
        [NonSerialized] private readonly Dictionary<string, AssetBundle> m_AssetBundles =
            new Dictionary<string, AssetBundle>(StringComparer.Ordinal);
        [NonSerialized] private readonly List<RawImage> m_ImageWidgets = new List<RawImage>();
        [NonSerialized] private List<MarkdownImagePlacement> m_PendingImagePlacements;
        [NonSerialized] private RectTransform m_ImageContainer;
        [NonSerialized] private MarkdownImageLoader m_ImageLoader;

        /// <summary>
        /// Optional custom image loader. When null, URLs are resolved as HTTP(S),
        /// Resources paths, or ab://bundle#asset AssetBundle references.
        /// </summary>
        public MarkdownImageLoader imageLoader
        {
            get => m_ImageLoader;
            set
            {
                m_ImageLoader = value;
                ClearImageCache();
                MarkDirty();
            }
        }

        /// <summary>Drop loaded image textures and force a reload on the next layout.</summary>
        public void ClearImageCache()
        {
            StopAllCoroutines();
            foreach (KeyValuePair<string, MarkdownImageEntry> pair in m_ImageCache)
            {
                MarkdownImageEntry entry = pair.Value;
                if (entry.OwnsTexture && entry.Texture != null)
                {
                    if (Application.isPlaying)
                        Destroy(entry.Texture);
                    else
                        DestroyImmediate(entry.Texture);
                }
            }
            m_ImageCache.Clear();

            foreach (KeyValuePair<string, AssetBundle> pair in m_AssetBundles)
            {
                if (pair.Value != null)
                    pair.Value.Unload(true);
            }
            m_AssetBundles.Clear();

            m_PendingImagePlacements = null;
            HideExtraImageWidgets(0);
            MarkDirty();
        }

        private void QueueImagePlacements(List<MarkdownImagePlacement> placements)
        {
            m_PendingImagePlacements = placements;
        }

        internal void SetImagesVisible(bool visible)
        {
            if (m_ImageContainer != null)
                m_ImageContainer.gameObject.SetActive(visible);
        }

        private void SyncImagePlacements()
        {
            if (m_PendingImagePlacements == null || !isActiveAndEnabled)
                return;

            EnsureImageContainer();
            if (m_ImageContainer == null)
                return;

            int used = 0;
            for (int i = 0; i < m_PendingImagePlacements.Count; i++)
            {
                MarkdownImagePlacement placement = m_PendingImagePlacements[i];
                if (placement.Texture == null)
                    continue;

                RawImage image = GetImageWidget(used++);
                image.gameObject.SetActive(true);
                image.texture = placement.Texture;
                image.color = Color.white;

                RectTransform rect = image.rectTransform;
                rect.anchorMin = rectTransform.pivot;
                rect.anchorMax = rectTransform.pivot;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = placement.Rect.min;
                rect.sizeDelta = placement.Rect.size;
                rect.localScale = Vector3.one;
            }

            HideExtraImageWidgets(used);
            m_ImageContainer.gameObject.SetActive(used > 0);
            m_PendingImagePlacements = null;
        }

        private void EnsureImageContainer()
        {
            if (m_ImageContainer != null)
            {
                m_ImageContainer.gameObject.SetActive(true);
                return;
            }

            var go = new GameObject("Markdown Images", typeof(RectTransform), typeof(MarkdownImageLayer));
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            go.transform.SetParent(transform, false);

            m_ImageContainer = (RectTransform)go.transform;
            m_ImageContainer.anchorMin = Vector2.zero;
            m_ImageContainer.anchorMax = Vector2.one;
            m_ImageContainer.offsetMin = Vector2.zero;
            m_ImageContainer.offsetMax = Vector2.zero;
            m_ImageContainer.pivot = new Vector2(0.5f, 0.5f);

            var layer = go.GetComponent<MarkdownImageLayer>();
            layer.Initialize(this);
        }

        private RawImage GetImageWidget(int index)
        {
            while (m_ImageWidgets.Count <= index)
            {
                var go = new GameObject("Image " + m_ImageWidgets.Count, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(m_ImageContainer, false);
                var raw = go.GetComponent<RawImage>();
                raw.raycastTarget = false;
                raw.enabled = false;
                m_ImageWidgets.Add(raw);
            }

            RawImage widget = m_ImageWidgets[index];
            widget.enabled = true;
            return widget;
        }

        private void HideExtraImageWidgets(int fromIndex)
        {
            for (int i = fromIndex; i < m_ImageWidgets.Count; i++)
            {
                if (m_ImageWidgets[i] == null)
                    continue;
                m_ImageWidgets[i].gameObject.SetActive(false);
            }
        }

        private float BuildImageBlock(
            LayoutResult result,
            MarkdownBlock block,
            float top,
            float availableWidth,
            float pixelsPerUnit)
        {
            float left = block.QuoteDepth * theme.quoteIndent + block.ListDepth * theme.listIndent;
            float contentWidth = Mathf.Max(1f, availableWidth - left);
            float maxWidth = m_MaxImageWidth > 0f ? Mathf.Min(contentWidth, m_MaxImageWidth) : contentWidth;
            maxWidth = Mathf.Max(1f, maxWidth);

            MarkdownImageEntry entry = RequestImage(block.ImageUrl);
            if (entry != null && entry.State == MarkdownImageState.Loaded && entry.Texture != null)
            {
                float naturalWidth = Mathf.Max(1f, entry.Texture.width);
                float naturalHeight = Mathf.Max(1f, entry.Texture.height);
                float scale = Mathf.Min(1f, maxWidth / naturalWidth);
                if (m_MaxImageHeight > 0f)
                    scale = Mathf.Min(scale, m_MaxImageHeight / naturalHeight);

                float width = naturalWidth * scale;
                float height = naturalHeight * scale;
                float x = left + ImageAlignmentOffset(contentWidth, width);
                float y = top;

                AddSolidNormalized(
                    result,
                    new Rect(x, -y - height, width, height),
                    ThemeColor(theme.imageBackgroundColor));
                AddImageBorder(result, x, y, width, height, ThemeColor(theme.imageBorderColor));

                result.Images.Add(new MarkdownImagePlacement
                {
                    Url = block.ImageUrl,
                    Alt = block.ImageAlt,
                    Rect = new Rect(x, -y - height, width, height),
                    Texture = entry.Texture
                });

                AddQuoteBars(result, block, top, height);
                return height + theme.imageSpacing;
            }

            if (entry != null && entry.State == MarkdownImageState.Loading)
            {
                float width = Mathf.Min(maxWidth, 320f);
                float height = width * 0.5625f;
                if (m_MaxImageHeight > 0f)
                    height = Mathf.Min(height, m_MaxImageHeight);
                height = Mathf.Max(72f, height);
                float x = left + ImageAlignmentOffset(contentWidth, width);

                AddSolidNormalized(
                    result,
                    new Rect(x, -top - height, width, height),
                    ThemeColor(theme.imageBackgroundColor));
                AddImageBorder(result, x, top, width, height, ThemeColor(theme.imageBorderColor));
                DrawImageMessage(
                    result,
                    string.IsNullOrEmpty(block.ImageAlt) ? "Loading image..." : "Loading image: " + block.ImageAlt,
                    x,
                    top,
                    width,
                    height,
                    ThemeColor(theme.imageLoadingTextColor),
                    pixelsPerUnit);

                AddQuoteBars(result, block, top, height);
                return height + theme.imageSpacing;
            }

            string error = "Image failed to load";
            if (!string.IsNullOrEmpty(block.ImageAlt))
                error += ": " + block.ImageAlt;
            if (string.IsNullOrEmpty(block.ImageUrl))
                error = "Image URL is empty";

            float errorWidth = Mathf.Min(maxWidth, 520f);
            float textWidth = Mathf.Max(1f, errorWidth - theme.imagePadding * 2f);
            MarkdownRichText rich = MarkdownRichTextBuilder.BuildPlainText(error);
            TextGenerationSettings settings = CreateSettings(
                Mathf.Max(1, m_FontSize),
                textWidth,
                TextAnchor.UpperCenter,
                ThemeColor(theme.imageErrorColor));
            m_MarkerGenerator.Populate(rich.Markup, settings);
            float textHeight = GetTextHeight(m_MarkerGenerator, pixelsPerUnit);
            if (textHeight <= 0f)
                textHeight = m_FontSize * theme.lineSpacing;

            float errorHeight = Mathf.Max(52f, textHeight + theme.imagePadding * 2f);
            float errorX = left + ImageAlignmentOffset(contentWidth, errorWidth);

            AddSolidNormalized(
                result,
                new Rect(errorX, -top - errorHeight, errorWidth, errorHeight),
                ThemeColor(theme.imageErrorBackgroundColor));
            AddImageBorder(result, errorX, top, errorWidth, errorHeight, ThemeColor(theme.imageErrorColor));

            AppendGeneratedGlyphs(
                result,
                m_MarkerGenerator,
                rich,
                null,
                errorX + theme.imagePadding,
                top + (errorHeight - textHeight) * 0.5f,
                pixelsPerUnit,
                false);

            AddQuoteBars(result, block, top, errorHeight);
            return errorHeight + theme.imageSpacing;
        }

        private void DrawImageMessage(
            LayoutResult result,
            string message,
            float x,
            float top,
            float width,
            float height,
            Color color,
            float pixelsPerUnit)
        {
            MarkdownRichText rich = MarkdownRichTextBuilder.BuildPlainText(message);
            TextGenerationSettings settings = CreateSettings(
                Mathf.Max(1, m_FontSize),
                Mathf.Max(1f, width - theme.imagePadding * 2f),
                TextAnchor.UpperCenter,
                color);
            m_MarkerGenerator.Populate(rich.Markup, settings);
            float textHeight = GetTextHeight(m_MarkerGenerator, pixelsPerUnit);
            if (textHeight <= 0f)
                textHeight = m_FontSize * theme.lineSpacing;

            AppendGeneratedGlyphs(
                result,
                m_MarkerGenerator,
                rich,
                null,
                x + theme.imagePadding,
                top + (height - textHeight) * 0.5f,
                pixelsPerUnit,
                false);
        }

        private void AddImageBorder(LayoutResult result, float x, float top, float width, float height, Color color)
        {
            if (color.a <= 0f)
                return;

            float thickness = Mathf.Max(1f, theme.tableBorderThickness);
            AddSolidNormalized(result, new Rect(x, -top - thickness, width, thickness), color);
            AddSolidNormalized(result, new Rect(x, -top - height, width, thickness), color);
            AddSolidNormalized(result, new Rect(x, -top - height, thickness, height), color);
            AddSolidNormalized(result, new Rect(x + width - thickness, -top - height, thickness, height), color);
        }

        private float ImageAlignmentOffset(float availableWidth, float imageWidth)
        {
            switch (m_Alignment)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    return Mathf.Max(0f, (availableWidth - imageWidth) * 0.5f);
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    return Mathf.Max(0f, availableWidth - imageWidth);
                default:
                    return 0f;
            }
        }

        private MarkdownImageEntry RequestImage(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            if (m_ImageCache.TryGetValue(url, out MarkdownImageEntry existing))
                return existing;

            var entry = new MarkdownImageEntry { Url = url, State = MarkdownImageState.Loading };
            m_ImageCache[url] = entry;

            try
            {
                if (m_ImageLoader != null)
                    m_ImageLoader(url, texture => OnImageLoaded(entry, texture, false), error => OnImageError(entry, error));
                else
                    LoadImageDefault(url, entry);
            }
            catch (Exception exception)
            {
                OnImageError(entry, exception.Message);
            }

            return entry;
        }

        private void LoadImageDefault(string url, MarkdownImageEntry entry)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(LoadWebTexture(url, entry));
                return;
            }

            if (url.StartsWith("ab://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("assetbundle://", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(LoadAssetBundleTexture(url, entry));
                return;
            }

            string path = url.StartsWith("resources://", StringComparison.OrdinalIgnoreCase)
                ? url.Substring("resources://".Length)
                : url;
            path = Uri.UnescapeDataString(path);

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null && HasFileExtension(path))
                texture = Resources.Load<Texture2D>(RemoveFileExtension(path));

            if (texture != null)
                OnImageLoaded(entry, texture, false);
            else
                OnImageError(entry, "Resources texture not found: " + path);
        }

        private IEnumerator LoadWebTexture(string url, MarkdownImageEntry entry)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    OnImageError(entry, request.error);
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    OnImageError(entry, "Downloaded image is null");
                    yield break;
                }

                OnImageLoaded(entry, texture, true);
            }
        }

        private IEnumerator LoadAssetBundleTexture(string url, MarkdownImageEntry entry)
        {
            string scheme = url.StartsWith("assetbundle://", StringComparison.OrdinalIgnoreCase)
                ? "assetbundle://"
                : "ab://";
            string spec = url.Substring(scheme.Length);
            int hash = spec.IndexOf('#');
            string bundlePath = hash >= 0 ? spec.Substring(0, hash) : spec;
            string assetName = hash >= 0 ? spec.Substring(hash + 1) : string.Empty;

            if (string.IsNullOrEmpty(bundlePath))
            {
                OnImageError(entry, "AssetBundle path is empty");
                yield break;
            }

            bundlePath = Uri.UnescapeDataString(bundlePath);
            if (!Path.IsPathRooted(bundlePath))
                bundlePath = Path.Combine(Application.streamingAssetsPath, bundlePath);

            if (!m_AssetBundles.TryGetValue(bundlePath, out AssetBundle bundle) || bundle == null)
            {
                AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(bundlePath);
                yield return request;
                bundle = request.assetBundle;
                if (bundle == null)
                {
                    OnImageError(entry, "Could not load AssetBundle: " + bundlePath);
                    yield break;
                }
                m_AssetBundles[bundlePath] = bundle;
            }

            Texture2D texture;
            if (string.IsNullOrEmpty(assetName))
            {
                Texture2D[] textures = bundle.LoadAllAssets<Texture2D>();
                texture = textures.Length > 0 ? textures[0] : null;
            }
            else
            {
                texture = bundle.LoadAsset<Texture2D>(assetName);
            }

            if (texture == null)
                OnImageError(entry, "AssetBundle has no Texture2D: " + assetName);
            else
                OnImageLoaded(entry, texture, false);
        }

        private void OnImageLoaded(MarkdownImageEntry entry, Texture2D texture, bool ownsTexture)
        {
            if (this == null)
                return;

            if (texture == null)
            {
                OnImageError(entry, "Image texture is null");
                return;
            }

            entry.Texture = texture;
            entry.OwnsTexture = ownsTexture;
            entry.State = MarkdownImageState.Loaded;
            entry.Error = null;
            if (!m_BuildingLayout)
                MarkDirty();
        }

        private void OnImageError(MarkdownImageEntry entry, string error)
        {
            if (this == null)
                return;

            entry.Texture = null;
            entry.State = MarkdownImageState.Failed;
            entry.Error = string.IsNullOrEmpty(error) ? "Image load failed" : error;
            if (!m_BuildingLayout)
                MarkDirty();
        }

        private void DisposeImageResources()
        {
            StopAllCoroutines();

            foreach (KeyValuePair<string, MarkdownImageEntry> pair in m_ImageCache)
            {
                MarkdownImageEntry entry = pair.Value;
                if (entry.OwnsTexture && entry.Texture != null)
                {
                    if (Application.isPlaying)
                        Destroy(entry.Texture);
                    else
                        DestroyImmediate(entry.Texture);
                }
            }
            m_ImageCache.Clear();

            foreach (KeyValuePair<string, AssetBundle> pair in m_AssetBundles)
            {
                if (pair.Value != null)
                    pair.Value.Unload(true);
            }
            m_AssetBundles.Clear();
            m_PendingImagePlacements = null;
        }

        private static bool HasFileExtension(string path)
        {
            int slash = path.LastIndexOf('/');
            int dot = path.LastIndexOf('.');
            return dot > slash + 1 && dot < path.Length - 1;
        }

        private static string RemoveFileExtension(string path)
        {
            int slash = path.LastIndexOf('/');
            int dot = path.LastIndexOf('.');
            return dot > slash ? path.Substring(0, dot) : path;
        }

        private enum MarkdownImageState
        {
            Loading,
            Loaded,
            Failed
        }

        private sealed class MarkdownImageEntry
        {
            public string Url;
            public Texture2D Texture;
            public bool OwnsTexture;
            public MarkdownImageState State;
            public string Error;
        }

        private sealed class MarkdownImagePlacement
        {
            public string Url;
            public string Alt;
            public Rect Rect;
            public Texture2D Texture;
        }
    }
}
