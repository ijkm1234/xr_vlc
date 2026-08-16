using System;
using UnityEngine;

namespace XRVLC
{
    /// <summary>
    /// 视频屏幕管理器 (GameObject)，负责调度背景板和实际的渲染表面。
    /// PlaybackService 与此对象交互。
    /// </summary>
    public class VideoScreen : MonoBehaviour
    {
        private const float UnderlaySurfaceOffsetMeters = -0.001f;
        private const float ScreenSubtitleForwardOffsetMeters = -3f;
        private const float ImmersiveSubtitleDistanceMeters = 5f;
        private const float ImmersiveSubtitleDownOffsetMeters = 3f;
        private const float ImmersiveSubtitleWidthMeters = 5.2f;
        private const int CylinderAlphaHoleSegments = 32;
        private const int FlatVisibilityGridSize = 3;
        private const int CylinderVisibilityColumns = 33;
        private const int CylinderVisibilityRows = 3;

        [Header("Scene References")]
        [Tooltip("视频层的挂载锚点 (VideoAnchor)")]
        public Transform videoAnchor;
        
        [Tooltip("背景板实体，用于平面视频的画框")]
        public Transform backgroundBoard;
        
        [Tooltip("玩家头部相机 (Main Camera)")]
        public Transform playerHeadCamera;

        [Header("Flat Zoom")]
        [Tooltip("平面/曲面视频的最小缩放倍率。")]
        public float minFlatZoomScale = 0.6f;

        [Tooltip("平面/曲面视频的最大缩放倍率。")]
        public float maxFlatZoomScale = 3.0f;

        [Tooltip("把旧的距离输入换算为平面 zoom 的速度。")]
        public float flatZoomScalePerMeter = 0.1f;

        public Transform SubtitleAnchor => videoAnchor != null ? videoAnchor : transform;
        public VideoProjection CurrentProjection => _currentProjection;
        public float FlatZoomScale => _flatZoomScale;
        public bool IsImmersiveProjection =>
            _currentProjection == VideoProjection.Sphere360 ||
            _currentProjection == VideoProjection.Sphere180 ||
            _currentProjection == VideoProjection.Fisheye180;
        public float SubtitleAnchorSurfaceOffsetMeters =>
            _currentProjection == VideoProjection.Cylinder ? GetCurrentCylinderVisualRadius() : 0f;
        public Vector2 SubtitleReferenceSizeMeters
        {
            get
            {
                if ((_currentProjection == VideoProjection.Flat || _currentProjection == VideoProjection.Cylinder)
                    && _lastVisibleWindowSize.x > 0.001f
                    && _lastVisibleWindowSize.y > 0.001f)
                    return _lastVisibleWindowSize;

                Transform reference = videoAnchor != null ? videoAnchor : backgroundBoard;
                if (reference == null) return new Vector2(16f, 9f);

                float width = GetWorldAxisLength(reference, Vector3.right);
                float height = GetWorldAxisLength(reference, Vector3.up);
                if (width <= 0.001f || height <= 0.001f)
                    return new Vector2(16f, 9f);

                return new Vector2(width, height);
            }
        }

        private IRenderSurface _renderSurface;
        private FlatSubtitleOverlaySurface _flatSubtitleOverlaySurface;
        private uint _flatSubtitleSurfaceWidth;
        private uint _flatSubtitleSurfaceHeight;
        private uint _flatSubtitleContentWidth;
        private uint _flatSubtitleContentHeight;
        private bool _flatSubtitleRenderOutsideScreen;
        private bool _hasFlatSubtitleLayerGeometry;
        private VideoProjection _currentProjection = VideoProjection.Flat;
        private StereoMode _currentStereo = StereoMode.Mono;
        private FlatVideoCurveMode _currentCurveMode = FlatVideoCurveMode.None;
        private VideoScaleMode _videoScaleMode = VideoScaleMode.Fit;
        private VideoAspectRatio _videoAspectRatio = VideoAspectRatio.Source;
        private VideoScreenTransformService _transformService;
        private float _flatZoomScale = 1f;
        private uint _lastVideoWidth;
        private uint _lastVideoHeight;
        private float _lastBackgroundRatio;
        private Vector2 _lastVisibleWindowSize = Vector2.one;

        private Camera _mainCamera;
        private Renderer _flatHoleRenderer;
        private bool _flatHoleRendererWasEnabled;
        private bool _hasFlatHoleRendererState;
        private MeshFilter _flatVideoHoleMeshFilter;
        private Mesh _flatAlphaHoleMesh;
        private Mesh _cylinderAlphaHoleMesh;
        private bool _passthroughBackgroundEnabled;

        private void Awake()
        {
            if (videoAnchor == null) videoAnchor = transform;
            _renderSurface = videoAnchor.GetComponentInChildren<IRenderSurface>();
            if (_renderSurface == null)
                Debug.LogError("[VideoScreen] 在 VideoAnchor 下未找到实现了 IRenderSurface 的组件！");

            _mainCamera = playerHeadCamera != null
                ? playerHeadCamera.GetComponent<Camera>()
                : Camera.main;

            ApplyFlatUnderlayAlphaHoleBackground();

            _transformService = new VideoScreenTransformService(
                transform,
                videoAnchor,
                GetViewerTransform,
                () => _currentProjection,
                ApplyFlatZoomDelta);
        }

        /// <summary>
        /// 代理到底层渲染表面的重建方法
        /// </summary>
        public void RebuildLayer(bool asHardwareSurface, uint videoWidth = 0, uint videoHeight = 0,
                                 VideoProjection proj = VideoProjection.Flat, StereoMode stereo = StereoMode.Mono,
                                 FlatVideoCurveMode curveMode = FlatVideoCurveMode.None,
                                 bool useTextureAlphaBlending = false)
        {
            if (_renderSurface != null)
            {
                // 先清理可能存在的旧的缩放状态，避免干扰底层的计算
                videoAnchor.localScale = Vector3.one;
                _renderSurface.RebuildLayer(asHardwareSurface, videoWidth, videoHeight, proj, stereo, curveMode, useTextureAlphaBlending);
            }
        }

        public void SetVideoLayout(VideoScaleMode scaleMode, VideoAspectRatio aspectRatio)
        {
            _videoScaleMode = scaleMode;
            _videoAspectRatio = aspectRatio;

            if (_lastVideoWidth > 0 && _lastVideoHeight > 0)
                FitVideoSize(_lastVideoWidth, _lastVideoHeight, _lastBackgroundRatio);
        }

        /// <summary>
        /// 调整视频锚点的缩放以适配视频比例和背景板
        /// </summary>
        public void FitVideoSize(uint videoWidth, uint videoHeight, float backgroundRatio = 0f)
        {
            if (videoWidth == 0 || videoHeight == 0) return;

            _lastVideoWidth = videoWidth;
            _lastVideoHeight = videoHeight;
            _lastBackgroundRatio = backgroundRatio;

            if (_currentProjection == VideoProjection.Sphere360 ||
                _currentProjection == VideoProjection.Sphere180 ||
                _currentProjection == VideoProjection.Fisheye180)
            {
                videoAnchor.localScale = Vector3.one;
                RefreshFlatSubtitleLayerGeometry();
                return;
            }

            float targetRatio = ResolveTargetVideoRatio(videoWidth, videoHeight);
            Vector2 backgroundSize = ResolveBackgroundSize(targetRatio, backgroundRatio);
            VideoLayoutResult layout = CalculateFlatVideoLayout(backgroundSize, targetRatio);
            Vector2 renderSize = layout.RenderSize * _flatZoomScale;
            Vector2 visibleSize = layout.VisibleSize * _flatZoomScale;
            _lastVisibleWindowSize = visibleSize;

            if (_currentProjection == VideoProjection.Cylinder)
            {
                float cylinderRadius = FlatVideoCurveMetrics.GetCylinderRadius(_currentCurveMode) * _flatZoomScale;
                float cylinderCentralAngle = cylinderRadius > 0f ? visibleSize.x / cylinderRadius : 0f;
                videoAnchor.localScale = new Vector3(renderSize.x, renderSize.y, cylinderRadius);
                PositionVideoAnchorForFlatProjection();
                RefreshVideoAlphaHole();
                RefreshFlatSubtitleLayerGeometry();
                Debug.Log($"[VideoScreen] FitVideoSize 曲面: render={renderSize}, visible={visibleSize}, targetRatio={targetRatio}, scaleMode={_videoScaleMode}, aspect={_videoAspectRatio}, radius={cylinderRadius}, centralAngle={cylinderCentralAngle}, PICO scale={videoAnchor.localScale}");
                return;
            }

            videoAnchor.localScale = new Vector3(renderSize.x, renderSize.y, 1f);
            PositionVideoAnchorForFlatProjection();
            RefreshVideoAlphaHole();
            RefreshFlatSubtitleLayerGeometry();
            Debug.Log($"[VideoScreen] FitVideoSize (共同父节点): background={backgroundSize}, render={renderSize}, visible={visibleSize}, targetRatio={targetRatio}, scaleMode={_videoScaleMode}, aspect={_videoAspectRatio}, 调整缩放为 {videoAnchor.localScale}");
        }

        private float ResolveTargetVideoRatio(uint videoWidth, uint videoHeight)
        {
            return _videoAspectRatio switch
            {
                VideoAspectRatio.Ratio16x9 => 16f / 9f,
                VideoAspectRatio.Ratio4x3 => 4f / 3f,
                VideoAspectRatio.Ratio16x10 => 16f / 10f,
                VideoAspectRatio.Ratio221x1 => 2.21f,
                VideoAspectRatio.Ratio235x1 => 2.35f,
                VideoAspectRatio.Ratio239x1 => 2.39f,
                VideoAspectRatio.Ratio5x4 => 5f / 4f,
                _ => videoHeight > 0 ? (float)videoWidth / videoHeight : 16f / 9f
            };
        }

        private Vector2 ResolveBackgroundSize(float targetRatio, float backgroundRatio)
        {
            if (backgroundBoard != null)
            {
                float width = Mathf.Abs(backgroundBoard.localScale.x);
                float height = Mathf.Abs(backgroundBoard.localScale.y);
                if (width > 0.001f && height > 0.001f)
                    return new Vector2(width, height);
            }

            if (backgroundRatio > 0.001f)
                return new Vector2(backgroundRatio, 1f);

            return new Vector2(Mathf.Max(0.001f, targetRatio), 1f);
        }

        private VideoLayoutResult CalculateFlatVideoLayout(Vector2 backgroundSize, float targetRatio)
        {
            float safeTargetRatio = Mathf.Max(0.001f, targetRatio);
            float safeWidth = Mathf.Max(0.001f, backgroundSize.x);
            float safeHeight = Mathf.Max(0.001f, backgroundSize.y);
            Vector2 safeBackgroundSize = new Vector2(safeWidth, safeHeight);

            Vector2 fitSize = FitSizeInside(safeBackgroundSize, safeTargetRatio);
            return _videoScaleMode switch
            {
                VideoScaleMode.Stretch => new VideoLayoutResult(safeBackgroundSize, safeBackgroundSize),
                VideoScaleMode.FillCrop => new VideoLayoutResult(CoverSize(safeBackgroundSize, safeTargetRatio), safeBackgroundSize),
                VideoScaleMode.Fit => new VideoLayoutResult(fitSize, fitSize),
                _ => new VideoLayoutResult(fitSize, fitSize)
            };
        }

        private static Vector2 FitSizeInside(Vector2 bounds, float ratio)
        {
            float boundsRatio = bounds.x / bounds.y;
            if (ratio > boundsRatio)
                return new Vector2(bounds.x, bounds.x / ratio);

            return new Vector2(bounds.y * ratio, bounds.y);
        }

        private static Vector2 CoverSize(Vector2 bounds, float ratio)
        {
            float boundsRatio = bounds.x / bounds.y;
            if (ratio > boundsRatio)
                return new Vector2(bounds.y * ratio, bounds.y);

            return new Vector2(bounds.x, bounds.x / ratio);
        }

        /// <summary>
        /// 把旧的前后距离输入映射为平面/曲面视频的视觉缩放，不改变幕布距离。
        /// </summary>
        public void ApplyFlatZoomDelta(float distanceDeltaMeters)
        {
            if (_currentProjection == VideoProjection.Sphere360 ||
                _currentProjection == VideoProjection.Sphere180 ||
                _currentProjection == VideoProjection.Fisheye180)
                return;

            float minZoom = Mathf.Min(minFlatZoomScale, maxFlatZoomScale);
            float maxZoom = Mathf.Max(minFlatZoomScale, maxFlatZoomScale);
            float zoomDelta = -distanceDeltaMeters * Mathf.Max(0f, flatZoomScalePerMeter);
            float nextZoom = Mathf.Clamp(_flatZoomScale + zoomDelta, minZoom, maxZoom);
            if (Mathf.Approximately(nextZoom, _flatZoomScale))
                return;

            _flatZoomScale = nextZoom;
            if (_lastVideoWidth > 0 && _lastVideoHeight > 0)
                FitVideoSize(_lastVideoWidth, _lastVideoHeight, _lastBackgroundRatio);
        }

        /// <summary>
        /// 设置投影模式并调整场景层级
        /// </summary>
        public void ChangeLayer(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode = FlatVideoCurveMode.None)
        {
            _currentProjection = projection;
            _currentStereo = stereo;
            _currentCurveMode = projection == VideoProjection.Cylinder ? curveMode : FlatVideoCurveMode.None;

            // 1. 调度层级和状态
            switch (projection)
            {
                case VideoProjection.Flat:
                case VideoProjection.Cylinder:
                    SetupFlatMode();
                    break;
                case VideoProjection.Sphere360:
                case VideoProjection.Sphere180:
                case VideoProjection.Fisheye180:
                    SetupImmersiveMode();
                    break;
            }

            // 2. 调度底层渲染表面
            _renderSurface?.ChangeLayer(projection, stereo, curveMode);
        }

        public void SetPassthroughBackgroundEnabled(bool enabled)
        {
            if (_passthroughBackgroundEnabled == enabled)
                return;

            _passthroughBackgroundEnabled = enabled;
            if (_currentProjection == VideoProjection.Flat || _currentProjection == VideoProjection.Cylinder)
                UpdateFlatUnderlayBackground();
        }

        public void EnterPlaybackBackground()
        {
            if (RenderSettings.skybox == null)
                return;

            RenderSettings.skybox = null;
            if (_currentProjection == VideoProjection.Flat || _currentProjection == VideoProjection.Cylinder)
                UpdateFlatUnderlayBackground();
            else
                ApplyTransparentUnderlayBackground();
        }

        private void SetupFlatMode()
        {
            ApplyFlatUnderlayAlphaHoleBackground();

            if (backgroundBoard != null && videoAnchor != null)
            {
                // BackgroundBoard 是不透明黑板，Underlay 模式下会遮住视频，只保留它的尺寸作为布局参考。
                PositionVideoAnchorForFlatProjection();
                videoAnchor.localScale = Vector3.one;
            }

            RefreshVideoAlphaHole();
        }

        private void SetupImmersiveMode()
        {
            ApplyTransparentUnderlayBackground();

            if (backgroundBoard != null && videoAnchor != null)
            {
                videoAnchor.localScale = Vector3.one;
                RecenterImmersiveSphere();
            }
        }

        private void LateUpdate()
        {
            if (_currentProjection == VideoProjection.Flat || _currentProjection == VideoProjection.Cylinder)
                UpdateFlatUnderlayBackground();
        }

        private void ApplyFlatUnderlayAlphaHoleBackground()
        {
            Camera targetCamera = GetMainCamera();
            if (targetCamera != null)
                UpdateFlatUnderlayBackground();

            if (backgroundBoard != null)
                backgroundBoard.gameObject.SetActive(false);
        }

        private void ApplyTransparentUnderlayBackground()
        {
            // 沉浸模式：相机背景设为 SolidColor alpha=0。
            // PICO compositor 在 alpha=0 处显示 Underlay 视频，alpha=1 处显示 eye buffer。
            // 不透明对象（手柄）由 opaque pass 写入 alpha=1，UI 由透明 blend 写入 alpha≈1。
            Camera targetCamera = GetMainCamera();
            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            if (backgroundBoard != null)
                backgroundBoard.gameObject.SetActive(false);

            RestoreFlatHoleRendererState();
            UnderlayAlphaHoleRegistry.Disable(videoAnchor);
        }

        private void RefreshVideoAlphaHole()
        {
            if (videoAnchor == null)
            {
                UnderlayAlphaHoleRegistry.Disable();
                return;
            }

            UnderlayAlphaHoleRegistry.Disable(videoAnchor);

            if (_currentProjection == VideoProjection.Cylinder)
            {
                RegisterCylinderAlphaHole();
                return;
            }

            RegisterFlatAlphaHole();
        }

        private void RegisterFlatAlphaHole()
        {
            MeshFilter meshFilter = FindVideoScreenMeshFilter();

            if (meshFilter == null)
            {
                _flatVideoHoleMeshFilter = null;
                UnderlayAlphaHoleRegistry.Disable(videoAnchor);
                return;
            }

            _flatVideoHoleMeshFilter = meshFilter;
            CacheAndDisableVideoScreenRenderer(meshFilter);
            UpdateFlatAlphaHoleMesh(_lastVisibleWindowSize.x, _lastVisibleWindowSize.y);
            if (_flatAlphaHoleMesh != null)
                UnderlayAlphaHoleRegistry.SetHole(videoAnchor, _flatAlphaHoleMesh);
        }

        private void RegisterCylinderAlphaHole()
        {
            if (videoAnchor == null)
                return;

            float radius = Mathf.Abs(videoAnchor.localScale.z);
            if (_lastVisibleWindowSize.x <= 0.001f || _lastVisibleWindowSize.y <= 0.001f || radius <= 0.001f)
                return;

            MeshFilter meshFilter = FindVideoScreenMeshFilter();
            _flatVideoHoleMeshFilter = meshFilter;
            CacheAndDisableVideoScreenRenderer(meshFilter);

            UpdateCylinderAlphaHoleMesh(_lastVisibleWindowSize.x, _lastVisibleWindowSize.y, radius);
            if (_cylinderAlphaHoleMesh != null)
                UnderlayAlphaHoleRegistry.SetHole(videoAnchor, _cylinderAlphaHoleMesh);
        }

        private void CacheAndDisableVideoScreenRenderer(MeshFilter meshFilter)
        {
            if (meshFilter == null)
                return;

            Renderer flatHoleRenderer = meshFilter.GetComponent<Renderer>();
            if (_flatHoleRenderer != flatHoleRenderer)
                RestoreFlatHoleRendererState();

            if (flatHoleRenderer == null)
                return;

            if (!_hasFlatHoleRendererState)
            {
                _flatHoleRenderer = flatHoleRenderer;
                _flatHoleRendererWasEnabled = flatHoleRenderer.enabled;
                _hasFlatHoleRendererState = true;
            }

            flatHoleRenderer.enabled = false;
        }

        private void UpdateFlatAlphaHoleMesh(float width, float height)
        {
            if (_flatAlphaHoleMesh == null)
            {
                _flatAlphaHoleMesh = new Mesh
                {
                    name = "FlatAlphaHoleMesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _flatAlphaHoleMesh.MarkDynamic();
            }

            float renderWidth = videoAnchor != null ? Mathf.Abs(videoAnchor.localScale.x) : width;
            float renderHeight = videoAnchor != null ? Mathf.Abs(videoAnchor.localScale.y) : height;
            float halfWidth = renderWidth > 0.001f ? Mathf.Clamp(width / renderWidth * 0.5f, 0f, 0.5f) : 0.5f;
            float halfHeight = renderHeight > 0.001f ? Mathf.Clamp(height / renderHeight * 0.5f, 0f, 0.5f) : 0.5f;

            _flatAlphaHoleMesh.Clear();
            _flatAlphaHoleMesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f)
            };
            _flatAlphaHoleMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            _flatAlphaHoleMesh.RecalculateBounds();
        }

        private void UpdateCylinderAlphaHoleMesh(float width, float height, float radius)
        {
            if (_cylinderAlphaHoleMesh == null)
            {
                _cylinderAlphaHoleMesh = new Mesh
                {
                    name = "CylinderAlphaHoleMesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _cylinderAlphaHoleMesh.MarkDynamic();
            }

            int segments = CylinderAlphaHoleSegments;
            int vertexCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[segments * 6];
            float centralAngle = FlatVideoCurveMetrics.GetCylinderCentralAngle(width, _currentCurveMode);
            float halfAngle = centralAngle * 0.5f;
            float renderWidth = videoAnchor != null ? Mathf.Abs(videoAnchor.localScale.x) : width;
            float renderHeight = videoAnchor != null ? Mathf.Abs(videoAnchor.localScale.y) : height;
            float localHalfHeight = renderHeight > 0.001f ? Mathf.Clamp(height / renderHeight * 0.5f, 0f, 0.5f) : 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float normalized = segments > 0 ? (float)i / segments : 0f;
                float theta = Mathf.Lerp(-halfAngle, halfAngle, normalized);
                float localX = renderWidth > 0.001f ? Mathf.Sin(theta) * radius / renderWidth : 0f;
                float localZ = Mathf.Cos(theta);
                int vertexIndex = i * 2;
                vertices[vertexIndex] = new Vector3(localX, -localHalfHeight, localZ);
                vertices[vertexIndex + 1] = new Vector3(localX, localHalfHeight, localZ);
            }

            for (int i = 0; i < segments; i++)
            {
                int vertexIndex = i * 2;
                int triangleIndex = i * 6;
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex + 2;
                triangles[triangleIndex + 3] = vertexIndex + 2;
                triangles[triangleIndex + 4] = vertexIndex + 1;
                triangles[triangleIndex + 5] = vertexIndex + 3;
            }

            _cylinderAlphaHoleMesh.Clear();
            _cylinderAlphaHoleMesh.vertices = vertices;
            _cylinderAlphaHoleMesh.triangles = triangles;
            _cylinderAlphaHoleMesh.RecalculateBounds();
        }

        private void UpdateFlatUnderlayBackground()
        {
            Camera targetCamera = GetMainCamera();
            if (targetCamera == null)
                return;

            if (_passthroughBackgroundEnabled)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                return;
            }

            if (RenderSettings.skybox != null)
            {
                targetCamera.clearFlags = CameraClearFlags.Skybox;
                targetCamera.backgroundColor = Color.black;
                return;
            }

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            bool screenVisible = IsHardwareSurfaceReady() && IsVideoScreenInCameraView(targetCamera);
            targetCamera.backgroundColor = screenVisible
                ? new Color(0f, 0f, 0f, 0f)
                : new Color(0.24f, 0.24f, 0.24f, 1f);
        }

        private bool IsVideoScreenInCameraView(Camera targetCamera)
        {
            if (targetCamera == null)
                return false;

            return _currentProjection == VideoProjection.Cylinder
                ? IsCylinderVideoScreenInCameraView(targetCamera)
                : IsFlatVideoScreenInCameraView(targetCamera);
        }

        private bool IsFlatVideoScreenInCameraView(Camera targetCamera)
        {
            MeshFilter meshFilter = _flatVideoHoleMeshFilter != null
                ? _flatVideoHoleMeshFilter
                : FindVideoScreenMeshFilter();
            Mesh visibilityMesh = _flatAlphaHoleMesh != null
                ? _flatAlphaHoleMesh
                : (meshFilter != null ? meshFilter.sharedMesh : null);
            Transform visibilityTransform = _flatAlphaHoleMesh != null && videoAnchor != null
                ? videoAnchor
                : (meshFilter != null ? meshFilter.transform : null);
            if (visibilityMesh == null || visibilityTransform == null)
                return false;

            Bounds localBounds = visibilityMesh.bounds;
            for (int x = 0; x < FlatVisibilityGridSize; x++)
            {
                float normalizedX = FlatVisibilityGridSize > 1 ? (float)x / (FlatVisibilityGridSize - 1) : 0.5f;
                for (int y = 0; y < FlatVisibilityGridSize; y++)
                {
                    float normalizedY = FlatVisibilityGridSize > 1 ? (float)y / (FlatVisibilityGridSize - 1) : 0.5f;
                    Vector3 localPoint = new Vector3(
                        Mathf.Lerp(localBounds.min.x, localBounds.max.x, normalizedX),
                        Mathf.Lerp(localBounds.min.y, localBounds.max.y, normalizedY),
                        localBounds.center.z);

                    if (IsWorldPointInCameraViewport(targetCamera, visibilityTransform.TransformPoint(localPoint)))
                        return true;
                }
            }

            return false;
        }

        private bool IsCylinderVideoScreenInCameraView(Camera targetCamera)
        {
            if (videoAnchor == null)
                return false;

            float renderWidth = Mathf.Abs(videoAnchor.localScale.x);
            float renderHeight = Mathf.Abs(videoAnchor.localScale.y);
            float width = _lastVisibleWindowSize.x > 0.001f ? _lastVisibleWindowSize.x : renderWidth;
            float height = _lastVisibleWindowSize.y > 0.001f ? _lastVisibleWindowSize.y : renderHeight;
            float radius = Mathf.Abs(videoAnchor.localScale.z);
            if (width <= 0.001f || height <= 0.001f || renderWidth <= 0.001f || renderHeight <= 0.001f || radius <= 0.001f)
                return false;

            float centralAngle = FlatVideoCurveMetrics.GetCylinderCentralAngle(width, _currentCurveMode);
            float halfAngle = centralAngle * 0.5f;
            float localHalfHeight = Mathf.Clamp(height / renderHeight * 0.5f, 0f, 0.5f);
            for (int column = 0; column < CylinderVisibilityColumns; column++)
            {
                float normalizedColumn = CylinderVisibilityColumns > 1
                    ? (float)column / (CylinderVisibilityColumns - 1)
                    : 0.5f;
                float theta = Mathf.Lerp(-halfAngle, halfAngle, normalizedColumn);
                float localX = Mathf.Sin(theta) * radius / renderWidth;
                float localZ = Mathf.Cos(theta);

                for (int row = 0; row < CylinderVisibilityRows; row++)
                {
                    float normalizedRow = CylinderVisibilityRows > 1
                        ? (float)row / (CylinderVisibilityRows - 1)
                        : 0.5f;
                    float localY = Mathf.Lerp(-localHalfHeight, localHalfHeight, normalizedRow);
                    Vector3 worldPoint = videoAnchor.TransformPoint(new Vector3(localX, localY, localZ));
                    if (IsWorldPointInCameraViewport(targetCamera, worldPoint))
                        return true;
                }
            }

            return false;
        }

        private static bool IsWorldPointInCameraViewport(Camera targetCamera, Vector3 worldPoint)
        {
            Vector3 viewportPoint = targetCamera.WorldToViewportPoint(worldPoint);
            return viewportPoint.z > targetCamera.nearClipPlane
                && viewportPoint.x >= 0f
                && viewportPoint.x <= 1f
                && viewportPoint.y >= 0f
                && viewportPoint.y <= 1f;
        }

        private MeshFilter FindVideoScreenMeshFilter()
        {
            if (_renderSurface is Component renderSurfaceComponent)
            {
                MeshFilter meshFilter = renderSurfaceComponent.GetComponent<MeshFilter>();
                if (meshFilter != null)
                    return meshFilter;
            }

            return videoAnchor != null ? videoAnchor.GetComponentInChildren<MeshFilter>(true) : null;
        }

        private Camera GetMainCamera()
        {
            if (_mainCamera != null)
                return _mainCamera;

            _mainCamera = playerHeadCamera != null
                ? playerHeadCamera.GetComponent<Camera>()
                : Camera.main;
            return _mainCamera;
        }

        private void OnDisable()
        {
            UnderlayAlphaHoleRegistry.Disable(videoAnchor != null ? videoAnchor : transform);
            RestoreFlatHoleRendererState();
        }

        private void OnDestroy()
        {
            if (_flatAlphaHoleMesh != null)
            {
                Destroy(_flatAlphaHoleMesh);
                _flatAlphaHoleMesh = null;
            }

            if (_cylinderAlphaHoleMesh != null)
            {
                Destroy(_cylinderAlphaHoleMesh);
                _cylinderAlphaHoleMesh = null;
            }
        }

        private void RestoreFlatHoleRendererState()
        {
            if (_hasFlatHoleRendererState && _flatHoleRenderer != null)
                _flatHoleRenderer.enabled = _flatHoleRendererWasEnabled;

            _flatHoleRenderer = null;
            _flatHoleRendererWasEnabled = false;
            _hasFlatHoleRendererState = false;
        }

        /// <summary>
        /// 视角复位：将全景/鱼眼球体中心对齐到玩家头部，朝向恢复为世界 +Z。
        /// </summary>
        public void RecenterImmersiveSphere()
        {
            if (videoAnchor == null || playerHeadCamera == null) return;
            if (_currentProjection == VideoProjection.Flat || _currentProjection == VideoProjection.Cylinder) return;

            videoAnchor.position = playerHeadCamera.position;
            videoAnchor.rotation = Quaternion.identity;
            _transformService?.ResetImmersiveDistanceOffset();

            Debug.Log($"[VideoScreen] 视角已复位，新球心位置: {videoAnchor.position}，朝向: 世界 +Z");
        }

        /// <summary>
        /// 将摇杆前后输入转为平面/曲面视频缩放，或 180 半球径向移动。
        /// </summary>
        public void AddViewerDistanceOffset(float deltaMeters)
        {
            _transformService?.AddViewerDistanceOffset(deltaMeters);
            RefreshFlatSubtitleLayerGeometry();
        }

        /// <summary>
        /// 记录 Grip 拖动开始时的手柄射线方向和幕布位置。
        /// </summary>
        public void BeginControllerMove(Vector3 controllerRayDirection)
        {
            _transformService?.BeginControllerMove(controllerRayDirection);
        }

        /// <summary>
        /// 根据手柄射线方向相对拖动起点的变化更新幕布位置或全景朝向。
        /// </summary>
        public void UpdateControllerMove(Vector3 controllerRayDirection)
        {
            _transformService?.UpdateControllerMove(controllerRayDirection);
            RefreshFlatSubtitleLayerGeometry();
        }

        /// <summary>
        /// 结束 Grip 拖动并保留幕布当前位置。
        /// </summary>
        public void EndControllerMove()
        {
            _transformService?.EndControllerMove();
        }

        /// <summary>
        /// 恢复幕布默认位置；平面/曲面视频同时恢复默认缩放。
        /// </summary>
        public void ResetTransformToDefault()
        {
            if (_currentProjection == VideoProjection.Sphere360 ||
                _currentProjection == VideoProjection.Sphere180 ||
                _currentProjection == VideoProjection.Fisheye180)
            {
                RecenterImmersiveSphere();
                return;
            }

            _flatZoomScale = 1f;
            _transformService?.ResetToDefault();

            if (_lastVideoWidth > 0 && _lastVideoHeight > 0)
                FitVideoSize(_lastVideoWidth, _lastVideoHeight, _lastBackgroundRatio);
        }

        /// <summary>
        /// 获取用于朝向和前后偏移的玩家视角 Transform。
        /// </summary>
        private Transform GetViewerTransform()
        {
            if (playerHeadCamera != null) return playerHeadCamera;
            return Camera.main != null ? Camera.main.transform : null;
        }

        private void PositionVideoAnchorForFlatProjection()
        {
            if (backgroundBoard == null || videoAnchor == null)
                return;

            videoAnchor.SetPositionAndRotation(backgroundBoard.position, backgroundBoard.rotation);

            float forwardOffsetMeters = UnderlaySurfaceOffsetMeters;
            if (_currentProjection == VideoProjection.Cylinder)
                forwardOffsetMeters -= GetCurrentCylinderVisualRadius();

            videoAnchor.position += videoAnchor.forward * forwardOffsetMeters;
        }

        private float GetCurrentCylinderVisualRadius()
        {
            if (_currentProjection != VideoProjection.Cylinder)
                return 0f;

            return FlatVideoCurveMetrics.GetCylinderRadius(_currentCurveMode) * Mathf.Max(0f, _flatZoomScale);
        }

        // --- 底层渲染表面状态代理 ---
        public bool IsHardwareSurfaceReady() => _renderSurface != null && _renderSurface.IsHardwareSurfaceReady();
        public IntPtr GetHardwareSurfaceHandle() => _renderSurface != null ? _renderSurface.GetHardwareSurfaceHandle() : IntPtr.Zero;
        public void DestroyLayer()
        {
            DestroyFlatSubtitleLayer();
            _renderSurface?.DestroyLayer();
            if (RenderSettings.skybox == null && !_passthroughBackgroundEnabled)
            {
                Camera targetCamera = GetMainCamera();
                if (targetCamera != null)
                {
                    targetCamera.clearFlags = CameraClearFlags.SolidColor;
                    targetCamera.backgroundColor = new Color(0.24f, 0.24f, 0.24f, 1f);
                }
            }
        }

        public bool RebuildFlatSubtitleLayer(uint surfaceWidth, uint surfaceHeight, uint contentWidth, uint contentHeight, bool renderOutsideScreen = false)
        {
            Debug.Log(
                $"[VideoScreen] Flat subtitle layer rebuild entry: projection={_currentProjection}, " +
                $"videoAnchorNull={videoAnchor == null}, surfaceSize={surfaceWidth}x{surfaceHeight}, contentSize={contentWidth}x{contentHeight}, " +
                $"stereo={_currentStereo}, outside={renderOutsideScreen}, " +
                $"anchorScale={(videoAnchor != null ? videoAnchor.localScale.ToString() : "null")}");

            if (videoAnchor == null)
            {
                Debug.LogWarning(
                    $"[VideoScreen] Flat subtitle layer rebuild skipped: projection={_currentProjection}, " +
                    $"videoAnchorNull={videoAnchor == null}");
                DestroyFlatSubtitleLayer();
                return false;
            }

            if (_flatSubtitleOverlaySurface == null)
            {
                GameObject layerObject = new GameObject("FlatSubtitleOverlaySurface");
                layerObject.transform.SetParent(transform, false);
                _flatSubtitleOverlaySurface = layerObject.AddComponent<FlatSubtitleOverlaySurface>();
                Debug.Log("[VideoScreen] Flat subtitle layer object created under VideoScreen");
            }

            _flatSubtitleSurfaceWidth = surfaceWidth;
            _flatSubtitleSurfaceHeight = surfaceHeight;
            _flatSubtitleContentWidth = contentWidth;
            _flatSubtitleContentHeight = contentHeight;
            _flatSubtitleRenderOutsideScreen = renderOutsideScreen;
            _hasFlatSubtitleLayerGeometry = true;

            SubtitleLayerGeometry geometry = CalculateSubtitleLayerGeometry(contentWidth, contentHeight, renderOutsideScreen);
            _flatSubtitleOverlaySurface.SetWorldGeometry(geometry.Center, geometry.Rotation, geometry.SizeMeters);
            _flatSubtitleOverlaySurface.RebuildLayer(surfaceWidth, surfaceHeight, _currentStereo);
            Debug.Log(
                $"[VideoScreen] Flat subtitle layer rebuild dispatched: ready={_flatSubtitleOverlaySurface.IsHardwareSurfaceReady()}, " +
                $"surface={_flatSubtitleOverlaySurface.GetHardwareSurfaceHandle()}, center={geometry.Center}, " +
                $"sizeMeters={geometry.SizeMeters}, rotation={geometry.Rotation.eulerAngles}");
            return true;
        }

        public bool IsFlatSubtitleSurfaceReady()
        {
            return _flatSubtitleOverlaySurface != null && _flatSubtitleOverlaySurface.IsHardwareSurfaceReady();
        }

        public IntPtr GetFlatSubtitleSurfaceHandle()
        {
            return _flatSubtitleOverlaySurface != null ? _flatSubtitleOverlaySurface.GetHardwareSurfaceHandle() : IntPtr.Zero;
        }

        public void DestroyFlatSubtitleLayer()
        {
            if (_flatSubtitleOverlaySurface == null) return;

            Debug.Log(
                $"[VideoScreen] Destroying flat subtitle layer: surface={_flatSubtitleOverlaySurface.GetHardwareSurfaceHandle()}");
            GameObject layerObject = _flatSubtitleOverlaySurface.gameObject;
            _flatSubtitleOverlaySurface.DestroyLayer();
            _flatSubtitleOverlaySurface = null;
            _flatSubtitleSurfaceWidth = 0;
            _flatSubtitleSurfaceHeight = 0;
            _flatSubtitleContentWidth = 0;
            _flatSubtitleContentHeight = 0;
            _flatSubtitleRenderOutsideScreen = false;
            _hasFlatSubtitleLayerGeometry = false;
            Destroy(layerObject);
        }

        private void RefreshFlatSubtitleLayerGeometry()
        {
            if (_flatSubtitleOverlaySurface == null || !_hasFlatSubtitleLayerGeometry || videoAnchor == null)
                return;

            SubtitleLayerGeometry geometry = CalculateSubtitleLayerGeometry(
                _flatSubtitleContentWidth,
                _flatSubtitleContentHeight,
                _flatSubtitleRenderOutsideScreen);
            _flatSubtitleOverlaySurface.SetWorldGeometry(geometry.Center, geometry.Rotation, geometry.SizeMeters);
        }

        private SubtitleLayerGeometry CalculateSubtitleLayerGeometry(uint contentWidth, uint contentHeight, bool renderOutsideScreen)
        {
            Quaternion rotation = videoAnchor.rotation;
            Vector3 center;
            Vector2 size;

            if (IsImmersiveProjection)
            {
                float height = contentWidth > 0
                    ? ImmersiveSubtitleWidthMeters * contentHeight / contentWidth
                    : ImmersiveSubtitleWidthMeters;
                size = new Vector2(ImmersiveSubtitleWidthMeters, Mathf.Max(0.001f, height));
                center = videoAnchor.position + videoAnchor.forward * ImmersiveSubtitleDistanceMeters;
                center -= videoAnchor.up * ImmersiveSubtitleDownOffsetMeters;
                return new SubtitleLayerGeometry(center, rotation, size);
            }

            size = SubtitleReferenceSizeMeters;
            center = videoAnchor.position + videoAnchor.forward * (SubtitleAnchorSurfaceOffsetMeters + ScreenSubtitleForwardOffsetMeters);

            if (renderOutsideScreen)
            {
                center -= videoAnchor.up * size.y;
            }
            else
            {
                size = ScaleSubtitleSizeByCameraDistanceRatio(size, center);
            }

            return new SubtitleLayerGeometry(center, rotation, size);
        }

        private Vector2 ScaleSubtitleSizeByCameraDistanceRatio(Vector2 referenceSize, Vector3 subtitleSurfaceCenter)
        {
            if (videoAnchor == null)
                return referenceSize;

            Transform viewer = GetViewerTransform();
            if (viewer == null)
                return referenceSize;

            Vector3 videoSurfaceCenter = videoAnchor.position + videoAnchor.forward * SubtitleAnchorSurfaceOffsetMeters;
            float videoSurfaceDistance = Vector3.Distance(viewer.position, videoSurfaceCenter);
            float subtitleSurfaceDistance = Vector3.Distance(viewer.position, subtitleSurfaceCenter);
            if (videoSurfaceDistance <= 0.001f || subtitleSurfaceDistance <= 0.001f)
                return referenceSize;

            float distanceRatio = subtitleSurfaceDistance / videoSurfaceDistance;
            return referenceSize * distanceRatio;
        }

        private static float GetWorldAxisLength(Transform reference, Vector3 localAxis)
        {
            return reference != null ? reference.TransformVector(localAxis).magnitude : 0f;
        }

        private readonly struct VideoLayoutResult
        {
            public VideoLayoutResult(Vector2 renderSize, Vector2 visibleSize)
            {
                RenderSize = renderSize;
                VisibleSize = visibleSize;
            }

            public Vector2 RenderSize { get; }
            public Vector2 VisibleSize { get; }
        }

        private readonly struct SubtitleLayerGeometry
        {
            public SubtitleLayerGeometry(Vector3 center, Quaternion rotation, Vector2 sizeMeters)
            {
                Center = center;
                Rotation = rotation;
                SizeMeters = sizeMeters;
            }

            public Vector3 Center { get; }
            public Quaternion Rotation { get; }
            public Vector2 SizeMeters { get; }
        }
    }
}
