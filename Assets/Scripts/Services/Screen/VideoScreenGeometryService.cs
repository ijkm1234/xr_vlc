using System;
using System.Collections;
using UnityEngine;
using XRVLC.Media;

namespace XRVLC
{
    public class VideoScreenGeometryService
    {
        private readonly VideoScreen _videoScreen;
        private readonly Func<MediaWrapper> _currentMediaProvider;
        private readonly Func<bool> _hardwareDecodingProvider;
        private readonly Func<VideoGeometrySelection?> _manualGeometryProvider;

        public VideoScreenGeometryService(
            VideoScreen videoScreen,
            Func<MediaWrapper> currentMediaProvider,
            Func<bool> hardwareDecodingProvider,
            Func<VideoGeometrySelection?> manualGeometryProvider = null)
        {
            _videoScreen = videoScreen;
            _currentMediaProvider = currentMediaProvider;
            _hardwareDecodingProvider = hardwareDecodingProvider;
            _manualGeometryProvider = manualGeometryProvider;
        }

        /// <summary>
        /// 根据当前媒体信息重建视频层几何，并等待硬件 Surface 生成后绑定给 VLC。
        /// </summary>
        public IEnumerator RebuildAndBind(uint width, uint height)
        {
            if (_videoScreen == null)
                yield break;

            VideoGeometrySelection geometry = ResolveGeometry();
            Debug.Log($"[VideoScreenGeometryService] SetGeometry -> projection={geometry.Projection}, stereo={geometry.Stereo}, curve={geometry.CurveMode}");

            _videoScreen.RebuildLayer(_hardwareDecodingProvider?.Invoke() ?? true, width, height, geometry.Projection, geometry.Stereo, geometry.CurveMode);
            _videoScreen.SetGeometry(geometry.Projection, geometry.Stereo, geometry.CurveMode);
            _videoScreen.FitVideoSize(width, height);

            yield return WaitForSurfaceAndBind();
        }

        private VideoGeometrySelection ResolveGeometry()
        {
            VideoGeometrySelection? manualGeometry = _manualGeometryProvider?.Invoke();
            if (manualGeometry.HasValue)
                return manualGeometry.Value;

            var (proj, stereo) = ProjectionDetector.Detect(_currentMediaProvider?.Invoke());
            return new VideoGeometrySelection(proj, stereo, FlatVideoCurveMode.None);
        }

        private IEnumerator WaitForSurfaceAndBind()
        {
            const int maxRetries = 50;
            int retries = 0;

            while (!_videoScreen.IsHardwareSurfaceReady() && retries < maxRetries)
            {
                retries++;
                if (retries % 10 == 0)
                    Debug.Log($"[VideoScreenGeometryService] Waiting for surface... retry: {retries}/{maxRetries}");
                yield return null;
            }

            IntPtr surfacePtr = _videoScreen.GetHardwareSurfaceHandle();
            Debug.Log($"[VideoScreenGeometryService] Surface handle: {surfacePtr}");

            if (surfacePtr == IntPtr.Zero)
            {
                Debug.LogError($"[VideoScreenGeometryService] 画布重建失败，超时未能获取到 Surface 指针。retries={retries}");
                yield break;
            }

            VlcPlaybackBridge.DetachSurface();
            VlcPlaybackBridge.SetSurface(surfacePtr);
        }
    }
}
