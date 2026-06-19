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
            _currentProjection == VideoProjection.Sphere360 || _currentProjection == VideoProjection.Sphere180;
        public Vector2 SubtitleReferenceSizeMeters
        {
            get
            {
                Transform reference = backgroundBoard != null ? backgroundBoard : videoAnchor;
                if (reference == null) return new Vector2(16f, 9f);

                float width = GetWorldAxisLength(reference, Vector3.right);
                float height = GetWorldAxisLength(reference, Vector3.up);
                if (width <= 0.001f || height <= 0.001f)
                    return new Vector2(16f, 9f);

                return new Vector2(width, height);
            }
        }

        private IRenderSurface _renderSurface;
        private VideoProjection _currentProjection = VideoProjection.Flat;
        private FlatVideoCurveMode _currentCurveMode = FlatVideoCurveMode.None;
        private VideoScreenTransformService _transformService;
        private float _flatZoomScale = 1f;
        private uint _lastVideoWidth;
        private uint _lastVideoHeight;
        private float _lastBackgroundRatio;

        private Camera _mainCamera;

        private void Awake()
        {
            if (videoAnchor == null) videoAnchor = transform;
            _renderSurface = videoAnchor.GetComponentInChildren<IRenderSurface>();
            if (_renderSurface == null)
                Debug.LogError("[VideoScreen] 在 VideoAnchor 下未找到实现了 IRenderSurface 的组件！");

            _mainCamera = playerHeadCamera != null
                ? playerHeadCamera.GetComponent<Camera>()
                : Camera.main;

            ApplyTransparentUnderlayBackground();

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
                                 FlatVideoCurveMode curveMode = FlatVideoCurveMode.None)
        {
            if (_renderSurface != null)
            {
                // 先清理可能存在的旧的缩放状态，避免干扰底层的计算
                videoAnchor.localScale = Vector3.one;
                _renderSurface.RebuildLayer(asHardwareSurface, videoWidth, videoHeight, proj, stereo, curveMode);
            }
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
                _currentProjection == VideoProjection.Sphere180)
            {
                videoAnchor.localScale = Vector3.one;
                return;
            }

            float videoRatio = (float)videoWidth / videoHeight;
            
            // 获取背景板的宽高比
            if (backgroundBoard != null)
            {
                // 现在 backgroundBoard 和 videoAnchor 是兄弟节点（平级），
                // 它们都挂在 VideoScreenManager (缩放通常为 1:1:1) 下。
                // 我们可以直接使用 localScale 来获取背景板的比例和尺寸。
                backgroundRatio = backgroundBoard.localScale.x / backgroundBoard.localScale.y;
            }

            Vector2 targetSize;
            if (backgroundRatio <= 0f)
            {
                targetSize = new Vector2(videoRatio, 1f);
            }
            else
            {
                // Fit-to-Contain: 确保画面完整塞入背景板
                // 因为现在是兄弟节点，不受父级变形影响，我们可以直接以背景板的物理尺寸（localScale）为基准
                // 来设置视频锚点的绝对大小。

                if (videoRatio > backgroundRatio)
                {
                    // 视频比背景更宽，X满载(对齐背景板的宽度)，Y按视频比例缩小
                    targetSize = new Vector2(
                        backgroundBoard.localScale.x, 
                        backgroundBoard.localScale.x / videoRatio);
                }
                else
                {
                    // 视频比背景更窄，Y满载(对齐背景板的高度)，X按视频比例缩小
                    targetSize = new Vector2(
                        backgroundBoard.localScale.y * videoRatio, 
                        backgroundBoard.localScale.y);
                }
            }

            targetSize *= _flatZoomScale;

            if (_currentProjection == VideoProjection.Cylinder)
            {
                float cylinderRadius = FlatVideoCurveMetrics.GetCylinderRadius(_currentCurveMode) * _flatZoomScale;
                float cylinderCentralAngle = cylinderRadius > 0f ? targetSize.x / cylinderRadius : 0f;
                videoAnchor.localScale = new Vector3(targetSize.x, targetSize.y, cylinderRadius);
                PositionVideoAnchorForFlatProjection();
                Debug.Log($"[VideoScreen] FitVideoSize 曲面: 弧长尺寸 {targetSize}, radius={cylinderRadius}, centralAngle={cylinderCentralAngle}, PICO scale={videoAnchor.localScale}");
                return;
            }

            videoAnchor.localScale = new Vector3(targetSize.x, targetSize.y, 1f);
            PositionVideoAnchorForFlatProjection();
            Debug.Log($"[VideoScreen] FitVideoSize (共同父节点): 目标比例 {backgroundRatio}, 视频比例 {videoRatio}, 调整缩放为 {videoAnchor.localScale}");
        }

        /// <summary>
        /// 把旧的前后距离输入映射为平面/曲面视频的视觉缩放，不改变幕布距离。
        /// </summary>
        public void ApplyFlatZoomDelta(float distanceDeltaMeters)
        {
            if (_currentProjection == VideoProjection.Sphere360 ||
                _currentProjection == VideoProjection.Sphere180)
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
        public void SetGeometry(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode = FlatVideoCurveMode.None)
        {
            _currentProjection = projection;
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
                    SetupImmersiveMode();
                    break;
            }

            // 2. 调度底层渲染表面
            _renderSurface?.SetGeometry(projection, stereo, curveMode);
        }

        private void SetupFlatMode()
        {
            ApplyTransparentUnderlayBackground();

            if (backgroundBoard != null && videoAnchor != null)
            {
                // BackgroundBoard 是不透明黑板，Underlay 模式下会遮住视频，只保留它的尺寸作为布局参考。
                PositionVideoAnchorForFlatProjection();
                videoAnchor.localScale = Vector3.one;
            }
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

        private void ApplyTransparentUnderlayBackground()
        {
            // 沉浸模式：相机背景设为 SolidColor alpha=0。
            // PICO compositor 在 alpha=0 处显示 Underlay 视频，alpha=1 处显示 eye buffer。
            // 不透明对象（手柄）由 opaque pass 写入 alpha=1，UI 由透明 blend 写入 alpha≈1。
            if (_mainCamera != null)
            {
                _mainCamera.clearFlags = CameraClearFlags.SolidColor;
                _mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            if (backgroundBoard != null)
                backgroundBoard.gameObject.SetActive(false);
        }

        /// <summary>
        /// 视角复位：将全景/鱼眼球体中心对齐到玩家头部
        /// </summary>
        public void RecenterImmersiveSphere()
        {
            if (videoAnchor == null || playerHeadCamera == null) return;
            if (_currentProjection == VideoProjection.Flat || _currentProjection == VideoProjection.Cylinder) return;

            videoAnchor.position = playerHeadCamera.position;
            float playerYaw = playerHeadCamera.eulerAngles.y;
            videoAnchor.rotation = Quaternion.Euler(0, playerYaw, 0);

            Debug.Log($"[VideoScreen] 视角已复位，新球心位置: {videoAnchor.position}");
        }

        /// <summary>
        /// 将摇杆前后输入转为平面/曲面视频缩放。
        /// </summary>
        public void AddViewerDistanceOffset(float deltaMeters)
        {
            _transformService?.AddViewerDistanceOffset(deltaMeters);
        }

        /// <summary>
        /// 记录 Grip 拖动开始时的手柄射线方向和幕布位置。
        /// </summary>
        public void BeginControllerMove(Vector3 controllerRayDirection)
        {
            _transformService?.BeginControllerMove(controllerRayDirection);
        }

        /// <summary>
        /// 根据手柄射线方向相对拖动起点的变化更新幕布位置。
        /// </summary>
        public void UpdateControllerMove(Vector3 controllerRayDirection)
        {
            _transformService?.UpdateControllerMove(controllerRayDirection);
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
                _currentProjection == VideoProjection.Sphere180)
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
        public void DestroyLayer() => _renderSurface?.DestroyLayer();

        private static float GetWorldAxisLength(Transform reference, Vector3 localAxis)
        {
            return reference != null ? reference.TransformVector(localAxis).magnitude : 0f;
        }
    }
}
