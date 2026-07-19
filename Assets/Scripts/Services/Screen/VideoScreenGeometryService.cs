using System;
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
        private readonly Func<VideoScaleMode> _scaleModeProvider;
        private readonly Func<VideoAspectRatio> _aspectRatioProvider;

        public VideoScreenGeometryService(
            VideoScreen videoScreen,
            Func<MediaWrapper> currentMediaProvider,
            Func<bool> hardwareDecodingProvider,
            Func<VideoGeometrySelection?> manualGeometryProvider = null,
            Func<VideoScaleMode> scaleModeProvider = null,
            Func<VideoAspectRatio> aspectRatioProvider = null)
        {
            _videoScreen = videoScreen;
            _currentMediaProvider = currentMediaProvider;
            _hardwareDecodingProvider = hardwareDecodingProvider;
            _manualGeometryProvider = manualGeometryProvider;
            _scaleModeProvider = scaleModeProvider;
            _aspectRatioProvider = aspectRatioProvider;
        }

        /// <summary>
        /// 根据当前媒体信息重建视频层几何。VLC 绑定由 PlaybackService 统一协调。
        /// </summary>
        public void Rebuild(VlcVideoSize videoSize, bool useTextureAlphaBlending)
        {
            if (_videoScreen == null)
                return;
            if (!videoSize.IsValid)
                return;

            VideoGeometrySelection geometry = ResolveGeometry();
            Debug.Log($"[VideoScreenGeometryService] SetGeometry -> projection={geometry.Projection}, stereo={geometry.Stereo}, curve={geometry.CurveMode}");
            uint contentWidth = (uint)videoSize.ContentWidth;
            uint contentHeight = (uint)videoSize.ContentHeight;

            _videoScreen.RebuildLayer(_hardwareDecodingProvider?.Invoke() ?? true, contentWidth, contentHeight, geometry.Projection, geometry.Stereo, geometry.CurveMode, useTextureAlphaBlending);
            _videoScreen.SetGeometry(geometry.Projection, geometry.Stereo, geometry.CurveMode);
            // Seed the new media size before SetVideoLayout refits cached dimensions.
            _videoScreen.FitVideoSize(contentWidth, contentHeight);
            _videoScreen.SetVideoLayout(
                _scaleModeProvider?.Invoke() ?? VideoScaleMode.Fit,
                _aspectRatioProvider?.Invoke() ?? VideoAspectRatio.Source);
        }

        private VideoGeometrySelection ResolveGeometry()
        {
            VideoGeometrySelection? manualGeometry = _manualGeometryProvider?.Invoke();
            if (manualGeometry.HasValue)
                return manualGeometry.Value;

            var (proj, stereo) = ProjectionDetector.Detect(_currentMediaProvider?.Invoke());
            return new VideoGeometrySelection(proj, stereo, FlatVideoCurveMode.None);
        }

    }
}
