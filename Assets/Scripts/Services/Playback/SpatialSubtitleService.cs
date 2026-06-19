using TMPro;
using UnityEngine;
using XRVLC.Subtitles;

namespace XRVLC.Media
{
    /// <summary>
    /// 将 VLC 播放时字幕 cue 显示在 Unity 空间中。
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class SpatialSubtitleService : MonoBehaviour
    {
        [SerializeField] private TextMeshPro subtitleText;
        [SerializeField] private Transform anchor;
        [SerializeField] private global::XRVLC.VideoScreen videoScreen;
        [SerializeField] private Camera viewerCamera;
        [SerializeField] private Vector3 anchorLocalOffset = new Vector3(0f, 0f, -0.08f);
        [SerializeField] private float fallbackDistanceMeters = 3.6f;
        [SerializeField] private float fallbackVerticalOffsetMeters = -0.85f;
        [SerializeField] private bool followViewerWhenUnanchored = false;
        [SerializeField] private float maxWallClockCueSeconds = 10f;
        [SerializeField] private bool preferSkiaTextureRenderer = true;
        [SerializeField] private int subtitleTextureWidthPx = 4096;
        [SerializeField] private int subtitleTextureMaxHeightPx = 1024;
        [SerializeField] private Material subtitleTextureMaterial;
        [SerializeField] private MeshRenderer subtitleTextureRenderer;
        [SerializeField] private FilterMode subtitleTextureFilterMode = FilterMode.Trilinear;
        [SerializeField] private bool subtitleTextureUseMipMaps = true;
        [SerializeField] private int subtitleTextureAnisoLevel = 4;
        [SerializeField] private float subtitleTextureMipMapBias = -0.25f;
        [SerializeField] private float subtitleWorldWidthMeters = 5.2f;
        [SerializeField] private float subtitleLineHeightScreenFraction = 1f / 15f;
        [SerializeField] private float subtitleBottomMarginLineHeights = 1f;
        [SerializeField] private float subtitleFallbackScreenWidthMeters = 16f;
        [SerializeField] private float subtitleFallbackScreenHeightMeters = 9f;
        [SerializeField] private float subtitleTextBoxLineCapacity = 2.4f;
        [SerializeField] private float subtitleTextureReferenceFontSizePx = 96f;
        [SerializeField] private float subtitleTextureLineHeightPxMultiplier = 1.25f;
        [SerializeField] private bool flipTextureSubtitleVertically = true;
        [SerializeField] private bool flipTextureSubtitleHorizontally = false;
        [SerializeField] private long maxCueLeadMs = 60000;
        [SerializeField] private long cueMediaLengthSlackMs = 10000;

        private bool _visible;
        private float _hideAtRealtime;
        private Texture2D _subtitleTexture;
        private ISubtitleTextureRenderer _textureRenderer;
        private readonly SubtitleCueScheduler _cueScheduler = new SubtitleCueScheduler();

        private void Awake()
        {
            _textureRenderer = new AndroidSkiaSubtitleRenderer();
            TryBindVideoScreenAnchor();
            EnsureText();
            Hide();
            Debug.Log($"[SpatialSubtitleService] Awake. viewerCamera={(viewerCamera != null ? viewerCamera.name : "null")}, anchor={(anchor != null ? anchor.name : "null")}, followViewerWhenUnanchored={followViewerWhenUnanchored}");
        }

        private void OnEnable()
        {
            _cueScheduler.SetPlaybackTime(VlcPlaybackEvents.Snapshot.TimeMs);
            _cueScheduler.SetPlaybackState(VlcPlaybackEvents.Snapshot.Status);
            VlcPlaybackEvents.OnTimeChanged += HandlePlaybackTimeChanged;
            VlcPlaybackEvents.OnStateChanged += HandlePlaybackStateChanged;
            VlcPlaybackEvents.OnSubtitleCue += HandleSubtitleCue;
            Debug.Log("[SpatialSubtitleService] Enabled and subscribed to subtitle cues.");
        }

        private void OnDisable()
        {
            VlcPlaybackEvents.OnTimeChanged -= HandlePlaybackTimeChanged;
            VlcPlaybackEvents.OnStateChanged -= HandlePlaybackStateChanged;
            VlcPlaybackEvents.OnSubtitleCue -= HandleSubtitleCue;
            Debug.Log("[SpatialSubtitleService] Disabled and unsubscribed from subtitle cues.");
        }

        private void LateUpdate()
        {
            if (!_visible) return;

            if (_hideAtRealtime > 0f && !_cueScheduler.IsPaused && Time.unscaledTime >= _hideAtRealtime)
            {
                Hide();
                return;
            }

            UpdatePose();
        }

        public void SetAnchor(Transform subtitleAnchor)
        {
            anchor = subtitleAnchor;
            UpdatePose();
        }

        public void SetVideoScreen(global::XRVLC.VideoScreen screen)
        {
            videoScreen = screen;
            anchor = videoScreen != null ? videoScreen.SubtitleAnchor : null;
            UpdatePose();
        }

        private void HandleSubtitleCue(SubtitleCue cue)
        {
            cue = NormalizeCueTimeline(cue);
            ApplyScheduleResult(_cueScheduler.EnqueueCue(cue));
        }

        private SubtitleCue NormalizeCueTimeline(SubtitleCue cue)
        {
            long playbackTimeMs = VlcPlaybackEvents.Snapshot.TimeMs;
            long mediaLengthMs = VlcPlaybackEvents.Snapshot.LengthMs;
            long maxDurationMs = Mathf.RoundToInt(Mathf.Max(0.25f, maxWallClockCueSeconds) * 1000f);

            SubtitleCue normalized = SubtitleCueTimelineNormalizer.Normalize(
                cue,
                playbackTimeMs,
                mediaLengthMs,
                maxCueLeadMs,
                cueMediaLengthSlackMs,
                maxDurationMs,
                out bool changed);

            if (changed)
            {
                Debug.LogWarning(
                    $"[SpatialSubtitleService] Normalized subtitle cue timeline. originalStartMs={cue.startMs}, originalEndMs={cue.endMs}, playbackTimeMs={playbackTimeMs}, mediaLengthMs={mediaLengthMs}, normalizedStartMs={normalized.startMs}, normalizedEndMs={normalized.endMs}");
            }

            return normalized;
        }

        private void ShowCue(SubtitleCue cue)
        {
            EnsureText();
            if (TryShowTextureCue(cue))
            {
                subtitleText.gameObject.SetActive(false);
            }
            else
            {
                HideTexture();
                ApplyTextLayout();
                subtitleText.text = cue.text;
                subtitleText.gameObject.SetActive(true);
            }

            _visible = true;
            Debug.Log($"[SpatialSubtitleService] Show cue. source={cue.source}, textLength={cue.text.Length}, text={Preview(cue.text)}");

            _hideAtRealtime = 0f;
            if (!SubtitleCueScheduler.HasTiming(cue))
                _hideAtRealtime = Time.unscaledTime + Mathf.Clamp(maxWallClockCueSeconds, 0.25f, maxWallClockCueSeconds);

            UpdatePose();
        }

        private void HandlePlaybackTimeChanged(long timeMs)
        {
            ApplyScheduleResult(_cueScheduler.SetPlaybackTime(timeMs));
        }

        private void HandlePlaybackStateChanged(string state)
        {
            ApplyScheduleResult(_cueScheduler.SetPlaybackState(ParsePlaybackState(state)));
        }

        private void ApplyScheduleResult(SubtitleCueScheduleResult result)
        {
            switch (result.Action)
            {
                case SubtitleCueScheduleAction.Show:
                    ShowCue(result.Cue);
                    break;
                case SubtitleCueScheduleAction.Hide:
                    Debug.Log("[SpatialSubtitleService] Hide cue.");
                    Hide();
                    break;
            }
        }

        private static PlayerStatus ParsePlaybackState(string state)
        {
            switch (state)
            {
                case "Playing":
                    return PlayerStatus.Playing;
                case "Paused":
                    return PlayerStatus.Paused;
                case "Stopped":
                    return PlayerStatus.Stopped;
                case "Ended":
                    return PlayerStatus.Ended;
                case "Error":
                    return PlayerStatus.Error;
                default:
                    return VlcPlaybackEvents.Snapshot.Status;
            }
        }

        private static string Preview(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= 120 ? value : value.Substring(0, 120) + "...";
        }

        private void EnsureText()
        {
            if (subtitleText != null) return;

            GameObject textObject = new GameObject("SpatialSubtitleText");
            textObject.transform.SetParent(transform, false);
            subtitleText = textObject.AddComponent<TextMeshPro>();
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.enableWordWrapping = true;
            subtitleText.fontSize = 0.42f;
            subtitleText.color = Color.white;
            if (Application.isPlaying)
            {
                subtitleText.outlineWidth = 0.18f;
                subtitleText.outlineColor = new Color32(0, 0, 0, 220);
            }
            ApplyTextLayout();
        }

        private bool TryShowTextureCue(SubtitleCue cue)
        {
            if (!preferSkiaTextureRenderer || _textureRenderer == null)
                return false;

            SubtitleBitmap bitmap = _textureRenderer.Render(cue, subtitleTextureWidthPx, subtitleTextureMaxHeightPx);
            if (!bitmap.IsValid)
                return false;

            if (!EnsureTextureView())
                return false;

            bool textureHasMipMaps = _subtitleTexture != null && _subtitleTexture.mipmapCount > 1;
            if (_subtitleTexture == null
                || _subtitleTexture.width != bitmap.Width
                || _subtitleTexture.height != bitmap.Height
                || textureHasMipMaps != subtitleTextureUseMipMaps)
            {
                if (_subtitleTexture != null)
                    DestroyRuntimeObject(_subtitleTexture);

                _subtitleTexture = new Texture2D(
                    bitmap.Width,
                    bitmap.Height,
                    TextureFormat.RGBA32,
                    subtitleTextureUseMipMaps);
            }

            ApplySubtitleTextureSampling();

            byte[] textureData = SubtitleBitmapTransforms.ToUnityTextureRgba(
                bitmap,
                flipTextureSubtitleVertically,
                flipTextureSubtitleHorizontally);
            if (textureData.Length == 0)
                return false;

            _subtitleTexture.SetPixelData(textureData, 0);
            _subtitleTexture.Apply(subtitleTextureUseMipMaps, false);

            subtitleTextureRenderer.sharedMaterial.mainTexture = _subtitleTexture;
            subtitleTextureRenderer.gameObject.SetActive(true);

            float aspect = bitmap.Height > 0 ? (float)bitmap.Width / bitmap.Height : 4.8f;
            float textureWorldHeightMeters = CalculateTextureWorldHeightMeters(bitmap);
            Transform textureTransform = subtitleTextureRenderer.transform;
            textureTransform.localPosition = Vector3.zero;
            textureTransform.localRotation = Quaternion.identity;
            textureTransform.localScale = new Vector3(
                textureWorldHeightMeters * Mathf.Max(aspect, 0.01f),
                textureWorldHeightMeters,
                1f);
            return true;
        }

        private void ApplySubtitleTextureSampling()
        {
            if (_subtitleTexture == null) return;

            _subtitleTexture.wrapMode = TextureWrapMode.Clamp;
            _subtitleTexture.filterMode = subtitleTextureFilterMode;
            _subtitleTexture.anisoLevel = Mathf.Clamp(subtitleTextureAnisoLevel, 1, 16);
            _subtitleTexture.mipMapBias = subtitleTextureMipMapBias;
        }

        private bool EnsureTextureView()
        {
            if (subtitleTextureRenderer != null)
                return subtitleTextureRenderer.sharedMaterial != null;

            Shader shader = subtitleTextureMaterial == null ? Shader.Find("Unlit/Transparent") : null;
            Material material = subtitleTextureMaterial != null
                ? new Material(subtitleTextureMaterial)
                : shader != null
                    ? new Material(shader)
                    : null;

            if (material == null)
            {
                Debug.LogWarning("[SpatialSubtitleService] No transparent material available for texture subtitles; falling back to TMP.");
                return false;
            }

            GameObject textureObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            textureObject.name = "SpatialSubtitleTexture";
            textureObject.transform.SetParent(transform, false);

            Collider textureCollider = textureObject.GetComponent<Collider>();
            if (textureCollider != null)
                DestroyRuntimeObject(textureCollider);

            subtitleTextureRenderer = textureObject.GetComponent<MeshRenderer>();
            subtitleTextureRenderer.sharedMaterial = material;
            textureObject.SetActive(false);
            return true;
        }

        private void Hide()
        {
            _visible = false;
            _hideAtRealtime = 0f;
            if (subtitleText != null)
            {
                subtitleText.text = string.Empty;
                subtitleText.gameObject.SetActive(false);
            }
            HideTexture();
        }

        private void HideTexture()
        {
            if (subtitleTextureRenderer != null)
                subtitleTextureRenderer.gameObject.SetActive(false);
        }

        private void UpdatePose()
        {
            TryBindVideoScreenAnchor();
            Camera targetCamera = viewerCamera != null ? viewerCamera : Camera.main;

            if (anchor != null)
            {
                transform.position = anchor.position + anchor.rotation * CalculateAnchorLocalOffset();
                transform.rotation = anchor.rotation;
                return;
            }

            if (followViewerWhenUnanchored && targetCamera != null)
            {
                Transform cameraTransform = targetCamera.transform;
                transform.position = cameraTransform.position
                    + cameraTransform.forward * fallbackDistanceMeters
                    + cameraTransform.up * fallbackVerticalOffsetMeters;

                Vector3 directionToSubtitle = transform.position - targetCamera.transform.position;
                if (directionToSubtitle.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(directionToSubtitle.normalized, targetCamera.transform.up);
            }
        }

        private void TryBindVideoScreenAnchor()
        {
            if (anchor != null)
                return;

            if (videoScreen == null)
                videoScreen = FindObjectOfType<global::XRVLC.VideoScreen>();

            if (videoScreen != null)
                anchor = videoScreen.SubtitleAnchor;
        }

        private void ApplyTextLayout()
        {
            if (subtitleText == null) return;

            float lineHeightMeters = CalculateSubtitleLineHeightMeters();
            Vector2 screenSize = CalculateSubtitleReferenceSizeMeters();
            float widthMeters = Mathf.Max(subtitleWorldWidthMeters, screenSize.x * 0.9f);

            subtitleText.fontSize = lineHeightMeters;
            subtitleText.rectTransform.sizeDelta = new Vector2(
                widthMeters,
                lineHeightMeters * Mathf.Max(1f, subtitleTextBoxLineCapacity));
        }

        private Vector3 CalculateAnchorLocalOffset()
        {
            if (videoScreen != null && videoScreen.IsImmersiveProjection)
            {
                return new Vector3(
                    anchorLocalOffset.x,
                    fallbackVerticalOffsetMeters + anchorLocalOffset.y,
                    anchorLocalOffset.z);
            }

            Vector2 screenSize = CalculateSubtitleReferenceSizeMeters();
            float lineHeightMeters = CalculateSubtitleLineHeightMeters(screenSize.y);
            float bottomToCenterMeters = lineHeightMeters * Mathf.Max(0.5f, subtitleBottomMarginLineHeights);
            float y = -screenSize.y * 0.5f + bottomToCenterMeters + anchorLocalOffset.y;
            return new Vector3(anchorLocalOffset.x, y, anchorLocalOffset.z);
        }

        private float CalculateSubtitleLineHeightMeters()
        {
            return CalculateSubtitleLineHeightMeters(CalculateSubtitleReferenceSizeMeters().y);
        }

        private float CalculateSubtitleLineHeightMeters(float screenHeightMeters)
        {
            float height = screenHeightMeters > 0.001f ? screenHeightMeters : subtitleFallbackScreenHeightMeters;
            return Mathf.Max(0.05f, height * Mathf.Max(0.01f, subtitleLineHeightScreenFraction));
        }

        private Vector2 CalculateSubtitleReferenceSizeMeters()
        {
            if (videoScreen != null)
            {
                Vector2 size = videoScreen.SubtitleReferenceSizeMeters;
                if (size.x > 0.001f && size.y > 0.001f)
                    return size;
            }

            if (anchor != null)
            {
                float width = anchor.TransformVector(Vector3.right).magnitude;
                float height = anchor.TransformVector(Vector3.up).magnitude;
                if (width > 0.001f && height > 0.001f)
                    return new Vector2(width, height);
            }

            return new Vector2(
                Mathf.Max(0.001f, subtitleFallbackScreenWidthMeters),
                Mathf.Max(0.001f, subtitleFallbackScreenHeightMeters));
        }

        private float CalculateTextureWorldHeightMeters(SubtitleBitmap bitmap)
        {
            float lineHeightMeters = CalculateSubtitleLineHeightMeters();
            float referenceLineHeightPx = Mathf.Max(
                1f,
                subtitleTextureReferenceFontSizePx * Mathf.Max(1f, subtitleTextureLineHeightPxMultiplier));

            return Mathf.Max(lineHeightMeters, lineHeightMeters * bitmap.Height / referenceLineHeightPx);
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
