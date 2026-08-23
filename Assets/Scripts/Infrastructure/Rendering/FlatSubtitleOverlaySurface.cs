using System;
using Unity.XR.PXR;
using UnityEngine;

namespace XRVLC
{
    [RequireComponent(typeof(PXR_CompositionLayer))]
    public sealed class FlatSubtitleOverlaySurface : MonoBehaviour
    {
        private const string SurfaceDebugTag = "XR_SURFACE_DEBUG";
        private static Mesh s_SubtitleAlphaHoleMesh;

        private PXR_CompositionLayer _compLayer;
        private IntPtr _hardwareSurfaceHandle = IntPtr.Zero;

        private static void SurfaceDebug(string message)
        {
            Debug.Log($"[{SurfaceDebugTag}] subtitle_surface {message}");
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass log = new AndroidJavaClass("android.util.Log"))
                    log.CallStatic<int>("e", SurfaceDebugTag, $"subtitle_surface {message}");
            }
            catch
            {
                // Diagnostics only.
            }
#endif
        }

        private void Awake()
        {
            _compLayer = GetComponent<PXR_CompositionLayer>();
        }

        public void SetWorldGeometry(Vector3 center, Quaternion rotation, Vector2 sizeMeters)
        {
            transform.SetPositionAndRotation(center, rotation);
            transform.localScale = new Vector3(
                Mathf.Max(0.001f, sizeMeters.x),
                Mathf.Max(0.001f, sizeMeters.y),
                1f);

            if (_compLayer != null)
            {
                _compLayer.overlayTransform = transform;
                _compLayer.UpdateCoords();
                RegisterAlphaHoleIfEnabled();
            }
        }

        public void RebuildLayer(uint surfaceWidth, uint surfaceHeight, StereoMode stereo)
        {
            if (_compLayer == null)
                _compLayer = GetComponent<PXR_CompositionLayer>();
            if (_compLayer == null)
            {
                Debug.LogError("[FlatSubtitleOverlaySurface] RebuildLayer failed: missing PXR_CompositionLayer");
                SurfaceDebug("rebuild_layer failed compLayer=null");
                return;
            }

            Debug.Log(
                $"[FlatSubtitleOverlaySurface] RebuildLayer start: requestedSize={surfaceWidth}x{surfaceHeight}, " +
                $"stereo={stereo}, previousSurface={_hardwareSurfaceHandle}, " +
                $"previousExternalObject={_compLayer.externalAndroidSurfaceObject}");
            SurfaceDebug(
                $"rebuild_layer start requested={surfaceWidth}x{surfaceHeight} stereo={stereo} " +
                $"previousHandle={_hardwareSurfaceHandle} previousExternal={_compLayer.externalAndroidSurfaceObject} " +
                $"enabled={_compLayer.enabled}");

            _compLayer.externalAndroidSurfaceObjectCreated -= OnSurfaceCreated;
            _compLayer.DestroyLayer();
            _compLayer.enabled = false;
            _hardwareSurfaceHandle = IntPtr.Zero;
            _compLayer.externalAndroidSurfaceObject = IntPtr.Zero;

            _compLayer.textureType = PXR_CompositionLayer.TextureType.ExternalSurface;
            _compLayer.isExternalAndroidSurface = true;
            _compLayer.isDynamic = false;
            _compLayer.overlayShape = PXR_CompositionLayer.OverlayShape.Quad;
            _compLayer.overlayType = PXR_CompositionLayer.OverlayType.Underlay;
            _compLayer.layerDepth = 1;
            _compLayer.useTextureAlphaBlending = true;
            _compLayer.usePremultipliedAlpha = false;
            _compLayer.normalSupersampling = false;
            _compLayer.qualitySupersampling = true;
            _compLayer.externalAndroidSurface3DType = stereo switch
            {
                StereoMode.LeftRight => PXR_CompositionLayer.Surface3DType.LeftRight,
                StereoMode.TopBottom => PXR_CompositionLayer.Surface3DType.TopBottom,
                _ => PXR_CompositionLayer.Surface3DType.Single
            };

            if (surfaceWidth > 0 && surfaceHeight > 0)
            {
                if (_compLayer.layerTextures[0] != null) Destroy(_compLayer.layerTextures[0]);
                if (_compLayer.layerTextures[1] != null) Destroy(_compLayer.layerTextures[1]);

                var rt = new RenderTexture((int)surfaceWidth, (int)surfaceHeight, 0);
                _compLayer.layerTextures[0] = rt;
                _compLayer.layerTextures[1] = rt;
            }

            _compLayer.useImageRect = false;
            _compLayer.destinationRect = PXR_CompositionLayer.DestinationRect.Default;
            _compLayer.srcRectLeft = new Rect(0, 0, 1, 1);
            _compLayer.srcRectRight = new Rect(0, 0, 1, 1);
            _compLayer.dstRectLeft = new Rect(0, 0, 1, 1);
            _compLayer.dstRectRight = new Rect(0, 0, 1, 1);
            _compLayer.imageRectLeft = new PxrRecti { x = 0, y = 0, width = 1, height = 1 };
            _compLayer.imageRectRight = new PxrRecti { x = 0, y = 0, width = 1, height = 1 };

            BindCompositionLayerPose();
            _compLayer.externalAndroidSurfaceObjectCreated += OnSurfaceCreated;
            SurfaceDebug("rebuild_layer subscribed_surface_created_callback");
            _compLayer.enabled = true;
            SurfaceDebug("rebuild_layer before_initialize_buffer");
            _compLayer.InitializeBuffer();
            SurfaceDebug($"rebuild_layer after_initialize_buffer external={_compLayer.externalAndroidSurfaceObject}");
            _compLayer.UpdateCoords();
            RegisterAlphaHoleIfEnabled();

            Debug.Log(
                $"[FlatSubtitleOverlaySurface] Rebuilt subtitle overlay surface {surfaceWidth}x{surfaceHeight}, stereo={stereo}, " +
                $"parent={(transform.parent != null ? transform.parent.name : "none")}, localPosition={transform.localPosition}, " +
                $"worldPosition={transform.position}, worldScale={transform.lossyScale}, overlayType={_compLayer.overlayType}, " +
                $"externalObject={_compLayer.externalAndroidSurfaceObject}, ready={IsHardwareSurfaceReady()}");
            SurfaceDebug(
                $"rebuild_layer complete external={_compLayer.externalAndroidSurfaceObject} " +
                $"ready={IsHardwareSurfaceReady()} handle={_hardwareSurfaceHandle}");
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
            if (_compLayer == null) return;

            Debug.Log(
                $"[FlatSubtitleOverlaySurface] DestroyLayer: surface={_hardwareSurfaceHandle}, " +
                $"externalObject={_compLayer.externalAndroidSurfaceObject}");
            SurfaceDebug(
                $"destroy_layer handle={_hardwareSurfaceHandle} external={_compLayer.externalAndroidSurfaceObject} " +
                $"enabled={_compLayer.enabled}");
            _compLayer.externalAndroidSurfaceObjectCreated -= OnSurfaceCreated;
            _compLayer.DestroyLayer();
            _compLayer.enabled = false;
            _compLayer.externalAndroidSurfaceObject = IntPtr.Zero;
            _hardwareSurfaceHandle = IntPtr.Zero;
            UnderlayAlphaHoleRegistry.Disable(transform);
        }

        private void RegisterAlphaHoleIfEnabled()
        {
            if (_compLayer == null || !_compLayer.enabled || !isActiveAndEnabled)
                return;

            UnderlayAlphaHoleRegistry.SetHole(transform, GetSubtitleAlphaHoleMesh());
        }

        private static Mesh GetSubtitleAlphaHoleMesh()
        {
            if (s_SubtitleAlphaHoleMesh != null)
                return s_SubtitleAlphaHoleMesh;

            s_SubtitleAlphaHoleMesh = new Mesh
            {
                name = "SubtitleAlphaHoleMesh",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f)
                },
                triangles = new[] { 0, 1, 2, 2, 1, 3 }
            };
            s_SubtitleAlphaHoleMesh.RecalculateBounds();
            return s_SubtitleAlphaHoleMesh;
        }

        private void BindCompositionLayerPose()
        {
            if (_compLayer == null) return;

            _compLayer.overlayTransform = transform;

            Camera xrCamera = Camera.main;
            if (xrCamera == null) return;

            _compLayer.xrRig = xrCamera;
            if (_compLayer.overlayEyeCamera == null || _compLayer.overlayEyeCamera.Length < 2)
                _compLayer.overlayEyeCamera = new Camera[2];

            if (_compLayer.overlayEyeCamera[0] == null || _compLayer.overlayEyeCamera[1] == null)
                _compLayer.RefreshCamera(xrCamera, xrCamera);
        }

        private void OnSurfaceCreated()
        {
            if (_compLayer == null || !_compLayer.isExternalAndroidSurface) return;

            _hardwareSurfaceHandle = _compLayer.externalAndroidSurfaceObject;
            Debug.Log($"[FlatSubtitleOverlaySurface] Subtitle Surface created: {_hardwareSurfaceHandle}");
            SurfaceDebug($"surface_created handle={_hardwareSurfaceHandle} external={_compLayer.externalAndroidSurfaceObject}");
        }

        private void OnDestroy()
        {
            UnderlayAlphaHoleRegistry.Disable(transform);
            if (_compLayer != null)
                _compLayer.externalAndroidSurfaceObjectCreated -= OnSurfaceCreated;
        }
    }
}
