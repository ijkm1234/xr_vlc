using System;
using System.Reflection;
using UnityEngine;
using Unity.XR.PXR;

namespace XRVLC
{
    /// <summary>
    /// 纯粹的渲染表面实现，专门与 PICO PXR_CompositionLayer 对接。
    /// 不包含任何业务层的父子关系或布局逻辑。
    /// </summary>
    [RequireComponent(typeof(PXR_CompositionLayer))]
    public class PicoRenderSurface : MonoBehaviour, IRenderSurface
    {
        private const float ImmersiveSphereRadius = 50f;
        private const string SurfaceDebugTag = "XR_SURFACE_DEBUG";

        private PXR_CompositionLayer _compLayer;
        private IntPtr _hardwareSurfaceHandle = IntPtr.Zero;

        private static void SurfaceDebug(string message)
        {
            Debug.Log($"[{SurfaceDebugTag}] pico_surface {message}");
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass log = new AndroidJavaClass("android.util.Log"))
                    log.CallStatic<int>("e", SurfaceDebugTag, $"pico_surface {message}");
            }
            catch
            {
                // Logging only; keep render surface creation unaffected.
            }
#endif
        }

        void Awake()
        {
            _compLayer = GetComponent<PXR_CompositionLayer>();
            BindCompositionLayerPose();
        }

        public void RebuildLayer(bool asHardwareSurface, uint videoWidth = 0, uint videoHeight = 0,
                                 VideoProjection proj = VideoProjection.Flat, StereoMode stereo = StereoMode.Mono,
                                 FlatVideoCurveMode curveMode = FlatVideoCurveMode.None,
                                 bool useTextureAlphaBlending = false)
        {
            if (_compLayer == null)
            {
                SurfaceDebug("rebuild_layer skipped compLayer=null");
                return;
            }
            SurfaceDebug(
                $"rebuild_layer start asHardwareSurface={asHardwareSurface} requested={videoWidth}x{videoHeight} " +
                $"projection={proj} stereo={stereo} curve={curveMode} alphaBlending={useTextureAlphaBlending} " +
                $"previousHandle={_hardwareSurfaceHandle} " +
                $"previousExternal={_compLayer.externalAndroidSurfaceObject} enabled={_compLayer.enabled}");
            BindCompositionLayerPose();

            // 彻底销毁旧的 Android Surface
            _compLayer.DestroyLayer();
            _compLayer.enabled = false;

            // 清理旧的回调和句柄
            _compLayer.externalAndroidSurfaceObjectCreated -= OnSurfaceCreated;
            _hardwareSurfaceHandle = IntPtr.Zero;

            // 配置为 Android Surface 模式
            _compLayer.externalAndroidSurfaceObject = IntPtr.Zero;
            _compLayer.textureType = PXR_CompositionLayer.TextureType.ExternalSurface;
            _compLayer.isExternalAndroidSurface = true;
            _compLayer.isDynamic = false;

            // ── 必须在 InitializeBuffer() 之前设置，否则 layerShape/layerLayout/overlayType 固化为默认值 ──
            bool isImmersive = proj is VideoProjection.Sphere360 or VideoProjection.Sphere180 or VideoProjection.Fisheye180;

            _compLayer.overlayShape = proj switch
            {
                VideoProjection.Cylinder => PXR_CompositionLayer.OverlayShape.Cylinder,
                VideoProjection.Sphere360 or VideoProjection.Sphere180 or VideoProjection.Fisheye180 => PXR_CompositionLayer.OverlayShape.Equirect,
                _ => PXR_CompositionLayer.OverlayShape.Quad
            };
            _compLayer.externalAndroidSurface3DType = stereo switch
            {
                StereoMode.LeftRight => PXR_CompositionLayer.Surface3DType.LeftRight,
                StereoMode.TopBottom => PXR_CompositionLayer.Surface3DType.TopBottom,
                _ => PXR_CompositionLayer.Surface3DType.Single
            };
            // 统一使用 Underlay：视频在 eye buffer 之下，字幕/UI/手柄由 Unity 正常覆盖。
            _compLayer.overlayType = PXR_CompositionLayer.OverlayType.Underlay;
            _compLayer.layerDepth = 0;
            _compLayer.useTextureAlphaBlending = useTextureAlphaBlending;
            _compLayer.usePremultipliedAlpha = false;
            _compLayer.normalSupersampling = false;
            _compLayer.qualitySupersampling = true;

            Debug.Log($"[PicoRenderSurface] RebuildLayer — proj={proj}, isImmersive={isImmersive}, overlayType={_compLayer.overlayType}, shape={_compLayer.overlayShape}");
            SurfaceDebug(
                $"rebuild_layer configured shape={_compLayer.overlayShape} type={_compLayer.overlayType} " +
                $"surface3D={_compLayer.externalAndroidSurface3DType} alphaBlending={_compLayer.useTextureAlphaBlending} " +
                $"premultipliedAlpha={_compLayer.usePremultipliedAlpha}");

            ApplyProjectionRadius(proj, curveMode);

            // 覆盖分辨率
            if (videoWidth > 0 && videoHeight > 0)
            {
                if (_compLayer.layerTextures[0] != null) Destroy(_compLayer.layerTextures[0]);
                if (_compLayer.layerTextures[1] != null) Destroy(_compLayer.layerTextures[1]);

                var rt = new RenderTexture((int)videoWidth, (int)videoHeight, 0);
                _compLayer.layerTextures[0] = rt;
                _compLayer.layerTextures[1] = rt;

                _compLayer.useImageRect = false;

                Debug.Log($"[PicoRenderSurface] 已请求底层分辨率: {videoWidth}x{videoHeight}");
            }

            // 层在此处创建，shape 和 layerLayout 在此固化
            SurfaceDebug("rebuild_layer before_initialize_buffer");
            _compLayer.InitializeBuffer();
            SurfaceDebug(
                $"rebuild_layer after_initialize_buffer external={_compLayer.externalAndroidSurfaceObject} " +
                $"compositionColorFormat={DescribeCompositionLayerColorFormat(_compLayer)} " +
                $"graphicsDevice={SystemInfo.graphicsDeviceType} colorSpace={QualitySettings.activeColorSpace}");

            // 重置 UV 截取矩阵
            _compLayer.useImageRect = false;
            _compLayer.destinationRect = PXR_CompositionLayer.DestinationRect.Default;
            _compLayer.srcRectLeft  = new Rect(0, 0, 1, 1);
            _compLayer.srcRectRight = new Rect(0, 0, 1, 1);
            _compLayer.imageRectLeft  = new PxrRecti() { x = 0, y = 0, width = 1, height = 1 };
            _compLayer.imageRectRight = new PxrRecti() { x = 0, y = 0, width = 1, height = 1 };
            _compLayer.dstRectLeft  = new Rect(0, 0, 1, 1);
            _compLayer.dstRectRight = new Rect(0, 0, 1, 1);

            _compLayer.externalAndroidSurfaceObjectCreated += OnSurfaceCreated;
            SurfaceDebug("rebuild_layer subscribed_surface_created_callback");

            _compLayer.enabled = true;
            _compLayer.UpdateCoords();
            SurfaceDebug(
                $"rebuild_layer complete enabled={_compLayer.enabled} external={_compLayer.externalAndroidSurfaceObject} " +
                $"ready={IsHardwareSurfaceReady()} handle={_hardwareSurfaceHandle}");
        }

        private static string DescribeCompositionLayerColorFormat(PXR_CompositionLayer layer)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                FieldInfo overlayParamField = layer.GetType().GetField("overlayParam", flags);
                object overlayParam = overlayParamField?.GetValue(layer);
                FieldInfo formatField = overlayParam?.GetType().GetField("format", flags);
                object rawFormat = formatField?.GetValue(overlayParam);
                if (rawFormat == null)
                    return "unavailable";

                ulong format = Convert.ToUInt64(rawFormat);
                string formatName = format switch
                {
                    37 => "VK_FORMAT_R8G8B8A8_UNORM",
                    43 => "VK_FORMAT_R8G8B8A8_SRGB",
                    0x8058 => "GL_RGBA8",
                    0x8C43 => "GL_SRGB8_ALPHA8",
                    _ => "unknown"
                };
                return $"{formatName}({format}/0x{format:X})";
            }
            catch (Exception exception)
            {
                return $"unavailable({exception.GetType().Name})";
            }
        }

        public void ChangeLayer(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode = FlatVideoCurveMode.None)
        {
            if (_compLayer == null) return;
            BindCompositionLayerPose();
            
            // 确保每次切换几何体时重置 UV
            _compLayer.useImageRect = false;
            _compLayer.imageRectLeft = new PxrRecti() { x = 0, y = 0, width = 1, height = 1 };
            _compLayer.imageRectRight = new PxrRecti() { x = 0, y = 0, width = 1, height = 1 };
            _compLayer.srcRectLeft = new Rect(0, 0, 1, 1);
            _compLayer.srcRectRight = new Rect(0, 0, 1, 1);
            _compLayer.dstRectLeft = new Rect(0, 0, 1, 1);
            _compLayer.dstRectRight = new Rect(0, 0, 1, 1);

            // 1. 设置几何体形状 (Shape)
            switch (projection)
            {
                case VideoProjection.Flat:
                    _compLayer.overlayShape = PXR_CompositionLayer.OverlayShape.Quad;
                    break;
                case VideoProjection.Cylinder:
                    _compLayer.overlayShape = PXR_CompositionLayer.OverlayShape.Cylinder;
                    ApplyProjectionRadius(projection, curveMode);
                    break;
                case VideoProjection.Sphere360:
                    _compLayer.overlayShape = PXR_CompositionLayer.OverlayShape.Equirect;
                    ApplyProjectionRadius(projection, curveMode);
                    _compLayer.dstRectLeft = new Rect(0, 0, 1, 1);
                    _compLayer.dstRectRight = new Rect(0, 0, 1, 1);
                    break;
                case VideoProjection.Sphere180:
                case VideoProjection.Fisheye180:
                    _compLayer.overlayShape = PXR_CompositionLayer.OverlayShape.Equirect;
                    ApplyProjectionRadius(projection, curveMode);
                    // 180半球：水平 180度 (width=0.5)
                    _compLayer.dstRectLeft = new Rect(0, 0, 0.5f, 1);
                    _compLayer.dstRectRight = new Rect(0, 0, 0.5f, 1);
                    break;
            }

            // 2. 设置 3D 分屏模式
            switch (stereo)
            {
                case StereoMode.Mono:
                    _compLayer.externalAndroidSurface3DType = PXR_CompositionLayer.Surface3DType.Single;
                    break;
                case StereoMode.LeftRight:
                    _compLayer.externalAndroidSurface3DType = PXR_CompositionLayer.Surface3DType.LeftRight;
                    break;
                case StereoMode.TopBottom:
                    _compLayer.externalAndroidSurface3DType = PXR_CompositionLayer.Surface3DType.TopBottom;
                    break;
            }

            _compLayer.layerDepth = 0;
            _compLayer.UpdateCoords();
        }

        private void BindCompositionLayerPose()
        {
            if (_compLayer == null) return;

            _compLayer.overlayTransform = transform;

            Camera xrCamera = _compLayer.xrRig != null ? _compLayer.xrRig : Camera.main;
            if (xrCamera == null) return;

            _compLayer.xrRig = xrCamera;
            if (_compLayer.overlayEyeCamera == null || _compLayer.overlayEyeCamera.Length < 2)
                _compLayer.overlayEyeCamera = new Camera[2];

            if (_compLayer.overlayEyeCamera[0] == null || _compLayer.overlayEyeCamera[1] == null)
                _compLayer.RefreshCamera(xrCamera, xrCamera);
        }

        private void ApplyProjectionRadius(VideoProjection projection, FlatVideoCurveMode curveMode)
        {
            if (_compLayer == null) return;

            if (projection == VideoProjection.Cylinder)
            {
                _compLayer.radius = FlatVideoCurveMetrics.GetCylinderRadius(curveMode);
                return;
            }

            if (projection == VideoProjection.Sphere360 ||
                projection == VideoProjection.Sphere180 ||
                projection == VideoProjection.Fisheye180)
                _compLayer.radius = ImmersiveSphereRadius;
        }

        private void OnSurfaceCreated()
        {
            if (_compLayer != null && _compLayer.isExternalAndroidSurface)
            {
                _hardwareSurfaceHandle = _compLayer.externalAndroidSurfaceObject;
                Debug.Log($"[PicoRenderSurface] 硬件 Surface 已生成: {_hardwareSurfaceHandle}");
                SurfaceDebug($"surface_created handle={_hardwareSurfaceHandle} external={_compLayer.externalAndroidSurfaceObject}");
            }
        }

        public bool IsHardwareSurfaceReady()
        {
            if (_compLayer == null) return false;

            if (_hardwareSurfaceHandle == IntPtr.Zero && _compLayer.externalAndroidSurfaceObject != IntPtr.Zero)
            {
                _hardwareSurfaceHandle = _compLayer.externalAndroidSurfaceObject;
                SurfaceDebug($"surface_ready cached_from_external handle={_hardwareSurfaceHandle}");
            }
            return _hardwareSurfaceHandle != IntPtr.Zero;
        }

        public IntPtr GetHardwareSurfaceHandle() => _hardwareSurfaceHandle;

        public void DestroyLayer()
        {
            if (_compLayer != null)
            {
                SurfaceDebug(
                    $"destroy_layer handle={_hardwareSurfaceHandle} external={_compLayer.externalAndroidSurfaceObject} " +
                    $"enabled={_compLayer.enabled}");
                _compLayer.DestroyLayer();
                _compLayer.enabled = false;
                _hardwareSurfaceHandle = IntPtr.Zero;
                _compLayer.externalAndroidSurfaceObject = IntPtr.Zero;
            }
        }

        public void SetSoftwareTexture(RenderTexture texture)
        {
            if (_compLayer != null)
            {
                _compLayer.SetTexture(texture, true);
            }
        }

        void OnDestroy()
        {
            if (_compLayer != null)
            {
                _compLayer.externalAndroidSurfaceObjectCreated -= OnSurfaceCreated;
            }
        }
    }
}
