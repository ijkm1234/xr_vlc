using System;
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

        private PXR_CompositionLayer _compLayer;
        private IntPtr _hardwareSurfaceHandle = IntPtr.Zero;

        void Awake()
        {
            _compLayer = GetComponent<PXR_CompositionLayer>();
            BindCompositionLayerPose();
        }

        public void RebuildLayer(bool asHardwareSurface, uint videoWidth = 0, uint videoHeight = 0,
                                 VideoProjection proj = VideoProjection.Flat, StereoMode stereo = StereoMode.Mono,
                                 FlatVideoCurveMode curveMode = FlatVideoCurveMode.None)
        {
            if (_compLayer == null) return;
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
            bool isImmersive = proj is VideoProjection.Sphere360 or VideoProjection.Sphere180;

            _compLayer.overlayShape = proj switch
            {
                VideoProjection.Cylinder => PXR_CompositionLayer.OverlayShape.Cylinder,
                VideoProjection.Sphere360 or VideoProjection.Sphere180 => PXR_CompositionLayer.OverlayShape.Equirect,
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

            Debug.Log($"[PicoRenderSurface] RebuildLayer — proj={proj}, isImmersive={isImmersive}, overlayType={_compLayer.overlayType}, shape={_compLayer.overlayShape}");

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
            _compLayer.InitializeBuffer();

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

            _compLayer.enabled = true;
            _compLayer.UpdateCoords();
        }

        public void SetGeometry(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode = FlatVideoCurveMode.None)
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

            if (projection == VideoProjection.Sphere360 || projection == VideoProjection.Sphere180)
                _compLayer.radius = ImmersiveSphereRadius;
        }

        private void OnSurfaceCreated()
        {
            if (_compLayer != null && _compLayer.isExternalAndroidSurface)
            {
                _hardwareSurfaceHandle = _compLayer.externalAndroidSurfaceObject;
                Debug.Log($"[PicoRenderSurface] 硬件 Surface 已生成: {_hardwareSurfaceHandle}");
            }
        }

        public bool IsHardwareSurfaceReady()
        {
            if (_compLayer == null) return false;

            if (_hardwareSurfaceHandle == IntPtr.Zero && _compLayer.externalAndroidSurfaceObject != IntPtr.Zero)
            {
                _hardwareSurfaceHandle = _compLayer.externalAndroidSurfaceObject;
            }
            return _hardwareSurfaceHandle != IntPtr.Zero;
        }

        public IntPtr GetHardwareSurfaceHandle() => _hardwareSurfaceHandle;

        public void DestroyLayer()
        {
            if (_compLayer != null)
            {
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
