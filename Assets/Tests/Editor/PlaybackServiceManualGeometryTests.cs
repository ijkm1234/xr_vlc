using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class PlaybackServiceManualGeometryTests
    {
        [Test]
        public void ParsedMediaPromotion_ClearsManualGeometryOverrideForNewMedia()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "StartNextParsedPlaybackRequest", "private void");

            StringAssert.Contains("_hasManualGeometryOverride = false", method);
        }

        [Test]
        public void SetManualVideoGeometry_UsesChangeLayerTransaction()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "SetManualVideoGeometry");

            StringAssert.Contains("RequestChangeLayer()", method);
            StringAssert.DoesNotContain("RequestRebuildLayer", method);
        }

        [Test]
        public void ChangeLayer_CanUpdateExistingPicoLayerWithoutRebuild()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "ChangeLayer", "private IEnumerator");

            StringAssert.Contains("ApplyLayerGeometryWithoutRebuild", method);
            StringAssert.Contains("VlcPlaybackBridge.BeginChangeLayer", method);
        }

        [Test]
        public void ChangeLayer_RebuildsOutputOnlyWhenOutputSpecChanges()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "ChangeLayer", "private IEnumerator");

            StringAssert.Contains("VideoLayerPlanner.NeedsOutputRebuild", method);
            StringAssert.Contains("if (rebuildOutput)", method);
            StringAssert.Contains("_geometryService.Rebuild", method);
        }

        [Test]
        public void VideoScreen_UsesPicoCylinderScaleSemantics()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "FitVideoSize");

            StringAssert.Contains("_currentProjection == VideoProjection.Cylinder", method);
            StringAssert.Contains("FlatVideoCurveMetrics.GetCylinderRadius(_currentCurveMode)", method);
            StringAssert.Contains("FlatVideoCurveMetrics.GetCylinderRadius(_currentCurveMode) * _flatZoomScale", method);
            StringAssert.Contains("new Vector3(renderSize.x, renderSize.y, cylinderRadius)", method);
            StringAssert.DoesNotContain("_currentProjection == VideoProjection.Cylinder ||", method);
        }

        [Test]
        public void VideoScreen_DefaultFlatZoomUpperLimitIsThreePointZero()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("public float maxFlatZoomScale = 3.0f", source);
        }

        [Test]
        public void VideoScreen_PlaybackBackgroundClearsSkyboxOnceAndUsesTransparentOrGray()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string setupFlatMode = ExtractMethod(source, "SetupFlatMode", "private void");
            string enterPlaybackBackground = ExtractMethod(source, "EnterPlaybackBackground", "public void");
            string updateBackground = ExtractMethod(source, "UpdateFlatUnderlayBackground", "private void");
            string transparentBackground = ExtractMethod(source, "ApplyTransparentUnderlayBackground", "private void");
            string destroyLayer = ExtractMethod(source, "DestroyLayer", "public void");

            StringAssert.Contains("ApplyFlatUnderlayAlphaHoleBackground()", setupFlatMode);
            StringAssert.DoesNotContain("ApplyTransparentUnderlayBackground()", setupFlatMode);
            StringAssert.Contains("if (RenderSettings.skybox == null)", enterPlaybackBackground);
            StringAssert.Contains("return;", enterPlaybackBackground);
            StringAssert.Contains("RenderSettings.skybox = null", enterPlaybackBackground);
            StringAssert.Contains("UpdateFlatUnderlayBackground()", enterPlaybackBackground);
            StringAssert.Contains("IsHardwareSurfaceReady() && IsVideoScreenInCameraView(targetCamera)", updateBackground);
            StringAssert.Contains("new Color(0f, 0f, 0f, 0f)", updateBackground);
            StringAssert.Contains("new Color(0.24f, 0.24f, 0.24f, 1f)", updateBackground);
            StringAssert.Contains("new Color(0f, 0f, 0f, 0f)", transparentBackground);
            StringAssert.Contains("RenderSettings.skybox == null", destroyLayer);
            StringAssert.Contains("new Color(0.24f, 0.24f, 0.24f, 1f)", destroyLayer);
            StringAssert.DoesNotContain("_hasEnteredPlaybackBackground", source);
        }

        [Test]
        public void SeeThroughMode_MakesFlatAndCylinderCameraBackgroundTransparent()
        {
            string videoScreenPath = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string uiManagerPath = Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs");
            string videoScreenSource = File.ReadAllText(videoScreenPath);
            string uiManagerSource = File.ReadAllText(uiManagerPath);
            string updateBackground = ExtractMethod(videoScreenSource, "UpdateFlatUnderlayBackground", "private void");
            string passthroughStateChanged = ExtractMethod(uiManagerSource, "OnPassthroughStateChanged", "private void");

            StringAssert.Contains("public void SetPassthroughBackgroundEnabled(bool enabled)", videoScreenSource);
            StringAssert.Contains("_passthroughBackgroundEnabled", updateBackground);
            StringAssert.Contains("new Color(0f, 0f, 0f, 0f)", updateBackground);
            StringAssert.Contains("ApplyPassthroughBackground(enabled)", passthroughStateChanged);
        }

        [Test]
        public void VideoScreen_CylinderSubtitleGeometryUsesVisibleSurfaceOffset()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "CalculateSubtitleLayerGeometry", "private SubtitleLayerGeometry");

            StringAssert.Contains("private const float ScreenSubtitleForwardOffsetMeters = -3f", source);
            StringAssert.Contains("SubtitleAnchorSurfaceOffsetMeters", source);
            StringAssert.Contains("SubtitleAnchorSurfaceOffsetMeters + ScreenSubtitleForwardOffsetMeters", method);
        }

        [Test]
        public void VideoScreen_ScalesInScreenSubtitleSurfaceByCameraDistanceRatio()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string calculateMethod = ExtractMethod(source, "CalculateSubtitleLayerGeometry", "private SubtitleLayerGeometry");
            string scaleMethod = ExtractMethod(source, "ScaleSubtitleSizeByCameraDistanceRatio", "private Vector2");

            StringAssert.Contains("ScaleSubtitleSizeByCameraDistanceRatio(size, center)", calculateMethod);
            StringAssert.Contains("if (renderOutsideScreen)", calculateMethod);
            StringAssert.Contains("else", calculateMethod);
            Assert.Greater(
                calculateMethod.IndexOf("ScaleSubtitleSizeByCameraDistanceRatio(size, center)", System.StringComparison.Ordinal),
                calculateMethod.IndexOf("else", calculateMethod.IndexOf("if (renderOutsideScreen)", System.StringComparison.Ordinal), System.StringComparison.Ordinal),
                "Screen subtitle scaling should only run for subtitles rendered inside the video screen.");

            StringAssert.Contains("Vector3 videoSurfaceCenter = videoAnchor.position + videoAnchor.forward * SubtitleAnchorSurfaceOffsetMeters", scaleMethod);
            StringAssert.Contains("Transform viewer = GetViewerTransform()", scaleMethod);
            StringAssert.Contains("float videoSurfaceDistance = Vector3.Distance(viewer.position, videoSurfaceCenter)", scaleMethod);
            StringAssert.Contains("float subtitleSurfaceDistance = Vector3.Distance(viewer.position, subtitleSurfaceCenter)", scaleMethod);
            StringAssert.Contains("float distanceRatio = subtitleSurfaceDistance / videoSurfaceDistance", scaleMethod);
            StringAssert.Contains("return referenceSize * distanceRatio", scaleMethod);
        }

        [Test]
        public void VideoScreen_UsesSurfacePointSamplingForBackgroundVisibility()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "IsVideoScreenInCameraView", "private bool");

            StringAssert.Contains("private const int FlatVisibilityGridSize = 3", source);
            StringAssert.Contains("private const int CylinderVisibilityColumns = 33", source);
            StringAssert.Contains("private const int CylinderVisibilityRows = 3", source);
            StringAssert.Contains("IsWorldPointInCameraViewport", source);
            StringAssert.Contains("targetCamera.WorldToViewportPoint", source);
            StringAssert.Contains("IsFlatVideoScreenInCameraView(targetCamera)", method);
            StringAssert.Contains("IsCylinderVideoScreenInCameraView(targetCamera)", method);
            StringAssert.DoesNotContain("GeometryUtility.TestPlanesAABB", method);
        }

        [Test]
        public void PicoUnderlayVideoAndSubtitleLayersUseExplicitDepthOrder()
        {
            string videoSurfacePath = Path.Combine(Application.dataPath, "Scripts/Infrastructure/Rendering/PicoRenderSurface.cs");
            string subtitleSurfacePath = Path.Combine(Application.dataPath, "Scripts/Infrastructure/Rendering/FlatSubtitleOverlaySurface.cs");
            string videoSurfaceSource = File.ReadAllText(videoSurfacePath);
            string subtitleSurfaceSource = File.ReadAllText(subtitleSurfacePath);

            StringAssert.Contains("_compLayer.layerDepth = 0", videoSurfaceSource);
            StringAssert.Contains("_compLayer.layerDepth = 1", subtitleSurfaceSource);
        }

        [Test]
        public void PlaybackService_UsesSingleHeightSubtitleSurfaceForFlatAndCylinderScreenSubtitles()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "UsesSingleHeightSubtitleSurface", "private bool");

            StringAssert.Contains("CurrentGeometrySelection.Projection == VideoProjection.Flat", method);
            StringAssert.Contains("CurrentGeometrySelection.Projection == VideoProjection.Cylinder", method);
            StringAssert.Contains("&& !RenderSubtitlesOutsideScreen", method);
        }

        [Test]
        public void PlaybackService_UsesContentSizeForScreenAndSubtitleSurfaces()
        {
            string playbackPath = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string geometryPath = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreenGeometryService.cs");
            string playbackSource = File.ReadAllText(playbackPath);
            string geometrySource = File.ReadAllText(geometryPath);
            string rebuildMethod = ExtractMethod(geometrySource, "Rebuild", "public void");
            string subtitleSpecMethod = ExtractMethod(playbackSource, "TryCreateSubtitleSurfaceSpec", "private bool");
            string subtitleRebuildMethod = ExtractMethod(playbackSource, "BeginNativeSubtitleSurfaceRebuild", "private bool");

            StringAssert.Contains("private VlcVideoSize CurrentVideoSize", playbackSource);
            StringAssert.Contains("CurrentVideoSize.ContentWidth", playbackSource);
            StringAssert.Contains("CurrentVideoSize.ContentHeight", playbackSource);
            StringAssert.Contains("public void Rebuild(VlcVideoSize videoSize, bool useTextureAlphaBlending)", geometrySource);
            StringAssert.Contains("uint contentWidth = (uint)videoSize.ContentWidth", rebuildMethod);
            StringAssert.Contains("uint contentHeight = (uint)videoSize.ContentHeight", rebuildMethod);
            StringAssert.Contains("RebuildLayer(_hardwareDecodingProvider?.Invoke() ?? true, contentWidth, contentHeight", rebuildMethod);
            StringAssert.Contains("FitVideoSize(contentWidth, contentHeight)", rebuildMethod);
            StringAssert.Contains("uint contentWidth = (uint)CurrentVideoSize.ContentWidth", subtitleSpecMethod);
            StringAssert.Contains("uint contentHeight = (uint)CurrentVideoSize.ContentHeight", subtitleSpecMethod);
            StringAssert.Contains("uint surfaceWidth = contentWidth", subtitleSpecMethod);
            StringAssert.Contains("videoScreen.RebuildFlatSubtitleLayer(", subtitleRebuildMethod);
            StringAssert.Contains("subtitleSpec.SurfaceWidth", subtitleRebuildMethod);
            StringAssert.Contains("subtitleSpec.ContentWidth", subtitleRebuildMethod);
            StringAssert.Contains("subtitleSpec.ContentHeight", subtitleRebuildMethod);
        }

        [Test]
        public void VideoScreenGeometryService_PrimesNewContentSizeBeforeLayoutRefit()
        {
            string geometryPath = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreenGeometryService.cs");
            string geometrySource = File.ReadAllText(geometryPath);
            string rebuildMethod = ExtractMethod(geometrySource, "Rebuild", "public void");

            int fitIndex = rebuildMethod.IndexOf("FitVideoSize(contentWidth, contentHeight)", System.StringComparison.Ordinal);
            int layoutIndex = rebuildMethod.IndexOf("_videoScreen.SetVideoLayout", System.StringComparison.Ordinal);

            Assert.That(fitIndex, Is.GreaterThanOrEqualTo(0), "Rebuild should fit using the current media content size.");
            Assert.That(layoutIndex, Is.GreaterThanOrEqualTo(0), "Rebuild should still apply the persisted video layout settings.");
            Assert.Less(
                fitIndex,
                layoutIndex,
                "Current media size must be cached before SetVideoLayout refits, otherwise media switches can briefly reuse the previous video's aspect ratio.");
        }

        [Test]
        public void PlaybackService_IgnoresMediaParseCallbacksForUnknownRequest()
        {
            string playbackPath = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string playbackSource = File.ReadAllText(playbackPath);
            string method = ExtractMethod(playbackSource, "HandleMediaParseFinished", "private void");

            StringAssert.Contains("FindPendingPlaybackRequest(result.MediaRequestId)", method);
            StringAssert.Contains("if (request == null)", method);
            Assert.Less(
                method.IndexOf("if (request == null)", System.StringComparison.Ordinal),
                method.IndexOf("request.ParseResult = result", System.StringComparison.Ordinal),
                "Request ownership must be checked before a parse result can be promoted.");
        }

        [Test]
        public void PlaybackService_AppliesAspectRatioSettingWithFitScaleMode()
        {
            string playbackPath = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string geometryPath = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreenGeometryService.cs");
            string playbackSource = File.ReadAllText(playbackPath);
            string geometrySource = File.ReadAllText(geometryPath);
            string startMethod = ExtractMethod(playbackSource, "Start", "private void");
            string setAspectRatio = ExtractMethod(playbackSource, "SetVideoAspectRatio", "public void");
            string rebuildMethod = ExtractMethod(geometrySource, "Rebuild", "public void");

            StringAssert.Contains("VideoScaleMode CurrentVideoScaleMode", playbackSource);
            StringAssert.Contains("VideoAspectRatio CurrentVideoAspectRatio", playbackSource);
            StringAssert.Contains("CurrentVideoScaleMode = VideoScaleMode.Fit", startMethod);
            StringAssert.DoesNotContain("PlaybackUiSettingsService.LoadVideoScaleMode()", startMethod);
            StringAssert.Contains("PlaybackUiSettingsService.LoadVideoAspectRatio()", startMethod);
            StringAssert.Contains("videoScreen.SetVideoLayout(CurrentVideoScaleMode, CurrentVideoAspectRatio)", startMethod);
            StringAssert.Contains("CurrentVideoScaleMode = VideoScaleMode.Fit", setAspectRatio);
            StringAssert.Contains("PlaybackUiSettingsService.SaveVideoAspectRatio(aspectRatio)", setAspectRatio);
            StringAssert.Contains("videoScreen?.SetVideoLayout(CurrentVideoScaleMode, CurrentVideoAspectRatio)", setAspectRatio);
            StringAssert.Contains("_videoScreen.SetVideoLayout", rebuildMethod);
            StringAssert.Contains("FitVideoSize(contentWidth, contentHeight)", rebuildMethod);
        }

        [Test]
        public void PlaybackService_IgnoresInvalidVideoSizeCallbacksBeforeRebuild()
        {
            string playbackPath = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string playbackSource = File.ReadAllText(playbackPath);
            string method = ExtractMethod(playbackSource, "OnVideoSizeChanged", "private void");

            StringAssert.Contains("private void OnVideoSizeChanged(VlcVideoSize videoSize)", playbackSource);
            StringAssert.Contains("if (!videoSize.IsValid)", method);
            StringAssert.DoesNotContain("IsCurrentMediaUri(payload.uri)", method);
            StringAssert.DoesNotContain("Ignoring video size callback for stale uri", method);
            Assert.Less(
                method.IndexOf("if (!videoSize.IsValid)", System.StringComparison.Ordinal),
                method.IndexOf("RequestRebuildLayer(videoSize)", System.StringComparison.Ordinal),
                "Invalid size callbacks, including 0x0 layout resets, must be ignored before any surface rebuild can run.");
        }

        [Test]
        public void VideoScreen_CombinesAspectRatioAndScaleModeForFlatLayout()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string fitMethod = ExtractMethod(source, "FitVideoSize");
            string calculateMethod = ExtractMethod(source, "CalculateFlatVideoLayout", "private VideoLayoutResult");

            StringAssert.Contains("private VideoScaleMode _videoScaleMode", source);
            StringAssert.Contains("private VideoAspectRatio _videoAspectRatio", source);
            StringAssert.Contains("public void SetVideoLayout(VideoScaleMode scaleMode, VideoAspectRatio aspectRatio)", source);
            StringAssert.Contains("ResolveTargetVideoRatio(videoWidth, videoHeight)", fitMethod);
            StringAssert.Contains("VideoAspectRatio.Ratio221x1", source);
            StringAssert.Contains("CalculateFlatVideoLayout", fitMethod);
            StringAssert.Contains("VideoAspectRatio.Ratio239x1", source);
            StringAssert.Contains("VideoAspectRatio.Ratio5x4", source);
            StringAssert.Contains("VideoScaleMode.Fit", calculateMethod);
            StringAssert.Contains("VideoScaleMode.Stretch", calculateMethod);
            StringAssert.Contains("VideoScaleMode.FillCrop", calculateMethod);
            StringAssert.Contains("targetRatio", calculateMethod);
            StringAssert.Contains("layout.VisibleSize", fitMethod);
            StringAssert.Contains("layout.RenderSize", fitMethod);
        }

        [Test]
        public void VideoScreen_ProducesDistinctLayoutsForScaleModesWithFourByThreeTarget()
        {
            var screenObject = new GameObject("VideoScreen layout test");
            try
            {
                System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
                Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

                Component screen = screenObject.AddComponent(screenType);
                MethodInfo calculate = screenType.GetMethod(
                    "CalculateFlatVideoLayout",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo scaleMode = screenType.GetField(
                    "_videoScaleMode",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNotNull(calculate);
                Assert.IsNotNull(scaleMode);

                Vector2 background = new Vector2(16f, 9f);
                float targetRatio = 4f / 3f;

                scaleMode.SetValue(screen, XRVLC.VideoScaleMode.Fit);
                object fit = calculate.Invoke(screen, new object[] { background, targetRatio });

                scaleMode.SetValue(screen, XRVLC.VideoScaleMode.FillCrop);
                object fillCrop = calculate.Invoke(screen, new object[] { background, targetRatio });

                scaleMode.SetValue(screen, XRVLC.VideoScaleMode.Stretch);
                object stretch = calculate.Invoke(screen, new object[] { background, targetRatio });

                AssertVectorApproximately(new Vector2(12f, 9f), GetLayoutVector(fit, "RenderSize"));
                AssertVectorApproximately(new Vector2(12f, 9f), GetLayoutVector(fit, "VisibleSize"));
                AssertVectorApproximately(new Vector2(16f, 12f), GetLayoutVector(fillCrop, "RenderSize"));
                AssertVectorApproximately(new Vector2(16f, 9f), GetLayoutVector(fillCrop, "VisibleSize"));
                AssertVectorApproximately(new Vector2(16f, 9f), GetLayoutVector(stretch, "RenderSize"));
                AssertVectorApproximately(new Vector2(16f, 9f), GetLayoutVector(stretch, "VisibleSize"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(screenObject);
            }
        }

        [Test]
        public void VideoScreen_UsesIndependentVisibleWindowForFillCropAlphaHole()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string refreshMethod = ExtractMethod(source, "RefreshVideoAlphaHole", "private void");
            string flatMethod = ExtractMethod(source, "RegisterFlatAlphaHole", "private void");
            string cylinderMethod = ExtractMethod(source, "RegisterCylinderAlphaHole", "private void");

            StringAssert.Contains("_lastVisibleWindowSize", source);
            StringAssert.Contains("_flatAlphaHoleMesh", source);
            StringAssert.Contains("UpdateFlatAlphaHoleMesh(_lastVisibleWindowSize.x, _lastVisibleWindowSize.y)", flatMethod);
            StringAssert.Contains("UnderlayAlphaHoleRegistry.SetHole(videoAnchor, _flatAlphaHoleMesh)", flatMethod);
            StringAssert.Contains("UpdateCylinderAlphaHoleMesh(_lastVisibleWindowSize.x, _lastVisibleWindowSize.y, radius)", cylinderMethod);
            StringAssert.Contains("UnderlayAlphaHoleRegistry.Disable(videoAnchor)", refreshMethod);
        }

        [Test]
        public void PlaybackService_DecouplesVideoAndSubtitleSurfaceLifecycles()
        {
            string playbackPath = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string geometryPath = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreenGeometryService.cs");
            string playbackSource = File.ReadAllText(playbackPath);
            string geometrySource = File.ReadAllText(geometryPath);
            string rebuildMethod = ExtractMethod(playbackSource, "RebuildLayer", "private IEnumerator");
            string attachVideoMethod = ExtractMethod(playbackSource, "AttachRebuildLayer", "private bool");
            string bindSubtitleMethod = ExtractMethod(playbackSource, "BindSubtitleSurface", "private void");
            string subtitleBindingRoutine = ExtractMethod(playbackSource, "UpdateNativeSubtitleSurfaceBindingRoutine", "private IEnumerator");

            StringAssert.Contains("VideoLayerPlanner.NeedsOutputRebuild", rebuildMethod);
            StringAssert.Contains("if (rebuildOutput)", rebuildMethod);
            StringAssert.Contains("_geometryService.Rebuild(videoSize, layerSpec.Mapper.ChromaKeyEnabled);", rebuildMethod);
            StringAssert.Contains("BeginNativeSubtitleSurfaceRebuild(out subtitleSpec", rebuildMethod);
            StringAssert.Contains("rebuildOutput", rebuildMethod);
            StringAssert.Contains("AttachRebuildLayer(token, mediaRequestId, layerSpec, wasPlaying)", rebuildMethod);
            StringAssert.Contains("BindSubtitleSurface(subtitleSpec)", rebuildMethod);
            Assert.Less(
                rebuildMethod.IndexOf("BindSubtitleSurface(subtitleSpec)", System.StringComparison.Ordinal),
                rebuildMethod.IndexOf("AttachRebuildLayer(token, mediaRequestId, layerSpec, wasPlaying)", System.StringComparison.Ordinal),
                "Subtitle surface should be bound before the video surface so Android's first vout attach sees the complete surface set.");

            StringAssert.Contains("VlcPlaybackBridge.AttachRebuildLayer(", attachVideoMethod);
            StringAssert.DoesNotContain("VlcPlaybackBridge.SetSubtitleSurface", attachVideoMethod);
            StringAssert.Contains("VlcPlaybackBridge.SetSubtitleSurface(subtitleSurfacePtr);", bindSubtitleMethod);
            StringAssert.DoesNotContain("VlcPlaybackBridge.SetSurface", bindSubtitleMethod);
            StringAssert.DoesNotContain("VlcPlaybackBridge.DetachSurface", bindSubtitleMethod);
            StringAssert.DoesNotContain("AttachRebuildLayer", subtitleBindingRoutine);
            StringAssert.DoesNotContain("SetSurface", subtitleBindingRoutine);

            StringAssert.Contains("public void Rebuild(VlcVideoSize videoSize, bool useTextureAlphaBlending)", geometrySource);
            StringAssert.DoesNotContain("VlcPlaybackBridge.SetSurface", geometrySource);
            StringAssert.DoesNotContain("VlcPlaybackBridge.DetachSurface", geometrySource);
        }

        [Test]
        public void PlaybackService_ForcesSubtitleStackOutsideForImmersiveProjection()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string policyMethod = ExtractMethod(source, "ShouldStackSubtitlesOutside", "private bool");
            string bindingMethod = ExtractMethod(source, "UpdateNativeSubtitleSurfaceBindingRoutine", "private IEnumerator");
            string setterMethod = ExtractMethod(source, "SetRenderSubtitlesOutsideScreen", "public void");

            StringAssert.Contains("IsImmersiveProjection(CurrentGeometrySelection.Projection)", policyMethod);
            StringAssert.Contains("return true", policyMethod);
            StringAssert.Contains("return RenderSubtitlesOutsideScreen", policyMethod);
            StringAssert.Contains("VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside())", bindingMethod);
            StringAssert.Contains("VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside())", setterMethod);
            StringAssert.DoesNotContain("VlcPlaybackBridge.SetSubtitleSurfacePolicy(RenderSubtitlesOutsideScreen)", source);
            StringAssert.DoesNotContain("VlcPlaybackBridge.SetSubtitleSurfacePolicy(enabled)", source);
        }

        [Test]
        public void PlaybackService_AttachTransactionsAlwaysConfigureMapper()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string attachVideoMethod = ExtractMethod(source, "AttachRebuildLayer", "private bool");
            string detachVideoMethod = ExtractMethod(source, "DetachVideoSurfaceFromVlc", "private void");

            StringAssert.Contains("VlcPlaybackBridge.AttachRebuildLayer(", attachVideoMethod);
            StringAssert.Contains("layerSpec.Mapper.FisheyeMappingEnabled", attachVideoMethod);
            StringAssert.Contains("layerSpec.Mapper.ChromaKeyEnabled", attachVideoMethod);

            StringAssert.Contains("private static bool IsFisheyeProjection(VideoProjection projection)", source);
            StringAssert.Contains("DetachVideoSurfaceFromVlc()", source);
            StringAssert.Contains("VlcPlaybackBridge.SetVideoSurfaceMapping(false, false, StereoMode.Mono, 0, 0)", detachVideoMethod);
        }

        [Test]
        public void PlaybackService_UsesManualChromaKeyForAllMediaAndResetsItOnMediaChange()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string layerPlanningSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts/Domain/Playback/VideoLayerPlanning.cs"));
            string createSpecMethod = ExtractMethod(source, "CreateVideoLayerSpec", "private VideoLayerSpec");
            string promoteMethod = ExtractMethod(source, "StartNextParsedPlaybackRequest", "private void");
            string setEnabledMethod = ExtractMethod(source, "SetChromaKeyEnabled", "public void");

            StringAssert.Contains("_chromaKeySettings.Enabled", createSpecMethod);
            StringAssert.Contains("_chromaKeySettings = ChromaKeySettings.Default", promoteMethod);
            StringAssert.Contains("OnChromaKeySettingsChanged?.Invoke(_chromaKeySettings)", promoteMethod);
            StringAssert.Contains("_chromaKeySettings.WithEnabled(enabled)", setEnabledMethod);
            StringAssert.DoesNotContain("ShouldEnableChromaKey", source);
            StringAssert.DoesNotContain("Path.GetFileName", source);
            StringAssert.Contains("ChromaKeyEnabled = chromaKeyEnabled", layerPlanningSource);
            StringAssert.Contains("public bool ChromaKeyEnabled { get; }", layerPlanningSource);
            StringAssert.Contains("ChromaKeyEnabled == other.ChromaKeyEnabled", layerPlanningSource);
        }

        [Test]
        public void VideoSurface_RebuildPassesStraightAlphaBlendingToPico()
        {
            string renderSurfaceSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Screen/IRenderSurface.cs"));
            string videoScreenSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs"));
            string geometrySource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreenGeometryService.cs"));
            string picoSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Infrastructure/Rendering/PicoRenderSurface.cs"));

            StringAssert.Contains("bool useTextureAlphaBlending", renderSurfaceSource);
            StringAssert.Contains("bool useTextureAlphaBlending", videoScreenSource);
            StringAssert.Contains("curveMode, useTextureAlphaBlending", videoScreenSource);
            StringAssert.Contains("public void Rebuild(VlcVideoSize videoSize, bool useTextureAlphaBlending)", geometrySource);
            StringAssert.Contains("geometry.CurveMode, useTextureAlphaBlending", geometrySource);
            StringAssert.Contains("bool useTextureAlphaBlending", picoSource);
            StringAssert.Contains("_compLayer.useTextureAlphaBlending = useTextureAlphaBlending", picoSource);
            StringAssert.Contains("_compLayer.usePremultipliedAlpha = false", picoSource);
        }

        [Test]
        public void PicoRenderSurface_LogsSdkSelectedCompositionLayerColorFormat()
        {
            string picoSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Infrastructure/Rendering/PicoRenderSurface.cs"));

            StringAssert.Contains("DescribeCompositionLayerColorFormat", picoSource);
            StringAssert.Contains("overlayParam", picoSource);
            StringAssert.Contains("BindingFlags.Instance", picoSource);
            StringAssert.Contains("compositionColorFormat=", picoSource);
            StringAssert.Contains("SystemInfo.graphicsDeviceType", picoSource);
            StringAssert.Contains("QualitySettings.activeColorSpace", picoSource);
        }

        [Test]
        public void VideoScreen_UsesContentSizeForImmersiveSubtitleGeometry()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string rebuildMethod = ExtractMethod(source, "RebuildFlatSubtitleLayer", "public bool");
            string calculateMethod = ExtractMethod(source, "CalculateSubtitleLayerGeometry", "private SubtitleLayerGeometry");

            StringAssert.Contains("uint contentWidth, uint contentHeight", rebuildMethod);
            StringAssert.Contains("_flatSubtitleContentWidth = contentWidth", source);
            StringAssert.Contains("_flatSubtitleContentHeight = contentHeight", source);
            StringAssert.Contains("CalculateSubtitleLayerGeometry(contentWidth, contentHeight, renderOutsideScreen)", rebuildMethod);
            StringAssert.Contains("ImmersiveSubtitleWidthMeters * contentHeight / contentWidth", calculateMethod);
            StringAssert.DoesNotContain("ImmersiveSubtitleWidthMeters * surfaceHeight / surfaceWidth", calculateMethod);
        }

        [Test]
        public void VideoScreen_OffsetsImmersiveSubtitleLayerDownInLocalSpace()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string calculateMethod = ExtractMethod(source, "CalculateSubtitleLayerGeometry", "private SubtitleLayerGeometry");

            StringAssert.Contains("private const float ImmersiveSubtitleDownOffsetMeters = 3f", source);
            StringAssert.Contains("center = videoAnchor.position + videoAnchor.forward * ImmersiveSubtitleDistanceMeters", calculateMethod);
            StringAssert.Contains("center -= videoAnchor.up * ImmersiveSubtitleDownOffsetMeters", calculateMethod);
            Assert.Less(
                calculateMethod.IndexOf("center -= videoAnchor.up * ImmersiveSubtitleDownOffsetMeters", System.StringComparison.Ordinal),
                calculateMethod.IndexOf("return new SubtitleLayerGeometry(center, rotation, size)", System.StringComparison.Ordinal),
                "Immersive subtitle down offset should be applied before returning immersive geometry.");
        }

        [Test]
        public void VideoScreen_GeneratesCylinderAlphaHoleMesh()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string updateCylinderMesh = ExtractMethod(source, "UpdateCylinderAlphaHoleMesh", "private void");

            StringAssert.Contains("private const int CylinderAlphaHoleSegments = 32", source);
            StringAssert.Contains("FlatVideoCurveMetrics.GetCylinderCentralAngle(width, _currentCurveMode)", updateCylinderMesh);
            StringAssert.Contains("Mathf.Sin(theta)", updateCylinderMesh);
            StringAssert.Contains("Mathf.Cos(theta)", updateCylinderMesh);
            StringAssert.Contains("UnderlayAlphaHoleRegistry.SetHole(videoAnchor, _cylinderAlphaHoleMesh)", source);
        }

        [Test]
        public void UnderlayAlphaHoleRenderer_DrawsMultipleRegisteredHoles()
        {
            string registryPath = Path.Combine(Application.dataPath, "Scripts/Infrastructure/Rendering/UnderlayAlphaHoleRegistry.cs");
            string featurePath = Path.Combine(Application.dataPath, "Scripts/Infrastructure/Rendering/UnderlayAlphaHoleRendererFeature.cs");
            string registrySource = File.ReadAllText(registryPath);
            string featureSource = File.ReadAllText(featurePath);

            StringAssert.Contains("UnderlayAlphaHoleEntry", registrySource);
            StringAssert.Contains("s_Holes", registrySource);
            StringAssert.Contains("CollectActiveHoles", registrySource);
            StringAssert.Contains("target.Add(new UnderlayAlphaHoleEntry", registrySource);
            StringAssert.Contains("UnderlayAlphaHoleRegistry.CollectActiveHoles(_holes)", featureSource);
            StringAssert.Contains("for (int i = 0; i < data.holes.Length; i++)", featureSource);
            StringAssert.Contains("cmd.DrawMesh(hole.Mesh, hole.Matrix", featureSource);
        }

        [Test]
        public void MainScene_SerializesFlatZoomUpperLimitAsThreePointZero()
        {
            string path = Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity");
            string scene = File.ReadAllText(path);

            StringAssert.Contains("maxFlatZoomScale: 3", scene);
        }

        [Test]
        public void MainScene_UsesAuthoredStarfieldSkyboxByDefault()
        {
            string path = Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity");
            string scene = File.ReadAllText(path);

            StringAssert.Contains(
                "m_SkyboxMaterial: {fileID: 2100000, guid: f2ac0e40f1c84e04a9a52290495c2387, type: 2}",
                scene);
            StringAssert.Contains("m_ClearFlags: 1", scene);
        }

        [Test]
        public void MainScene_DisablesXriTeleportMode()
        {
            string path = Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity");
            string scene = File.ReadAllText(path);

            Assert.That(Regex.Matches(scene, @"m_TeleportInteractor: \{fileID: 0\}").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(scene, @"m_TeleportMode: \{fileID: 0\}").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(scene, @"m_TeleportModeCancel: \{fileID: 0\}").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(scene, @"m_NearFarEnableTeleportDuringNearInteraction: 0").Count, Is.EqualTo(2));
        }

        [Test]
        public void FitVideoSize_CylinderKeepsFlatWidthHeightAndStoresRadiusInZScale()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("ChangeLayer").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Small });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });

                Assert.That(anchor.transform.localScale.x, Is.EqualTo(16f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(9f).Within(0.001f));
                Assert.That(anchor.transform.localScale.z, Is.EqualTo(32f).Within(0.001f));

                screenType.GetMethod("ChangeLayer").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Large });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });

                Assert.That(anchor.transform.localScale.x, Is.EqualTo(16f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(9f).Within(0.001f));
                Assert.That(anchor.transform.localScale.z, Is.EqualTo(16f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChangeLayer_CylinderOffsetsLayerCenterSoSurfaceMatchesFlatPosition()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                root.transform.position = new Vector3(0f, 2.5f, 15f);
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("ChangeLayer").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Large });

                Vector3 expectedSurfaceCenter = background.transform.position + background.transform.forward * -0.001f;
                Vector3 actualSurfaceCenter = anchor.transform.position + anchor.transform.forward * XRVLC.FlatVideoCurveMetrics.LargeCurveCylinderRadius;
                AssertVector3(expectedSurfaceCenter, actualSurfaceCenter);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFlatZoomDelta_CylinderScalesRadiusWithArcSize()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                root.transform.position = new Vector3(0f, 1.5f, 17f);
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("ChangeLayer").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Large });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });
                screenType.GetMethod("ApplyFlatZoomDelta").Invoke(screen, new object[] { -2f });

                Assert.That((float)screenType.GetProperty("FlatZoomScale").GetValue(screen), Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.x, Is.EqualTo(19.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(10.8f).Within(0.001f));
                Assert.That(anchor.transform.localScale.z, Is.EqualTo(19.2f).Within(0.001f));
                Vector3 expectedSurfaceCenter = background.transform.position + background.transform.forward * -0.001f;
                Vector3 actualSurfaceCenter = anchor.transform.position + anchor.transform.forward * 9.6f;
                AssertVector3(expectedSurfaceCenter, actualSurfaceCenter);
                Assert.That(root.transform.position.z, Is.EqualTo(17f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFlatZoomDelta_ScalesFlatFitSizeWithoutMovingRoot()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                root.transform.position = new Vector3(0f, 1.5f, 17f);
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("ChangeLayer").Invoke(screen, new object[] { XRVLC.VideoProjection.Flat, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.None });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });
                screenType.GetMethod("ApplyFlatZoomDelta").Invoke(screen, new object[] { -2f });

                Assert.That((float)screenType.GetProperty("FlatZoomScale").GetValue(screen), Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.x, Is.EqualTo(19.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(10.8f).Within(0.001f));
                Assert.That(root.transform.position.z, Is.EqualTo(17f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string ExtractMethod(string source, string methodName, string returnType = "public void")
        {
            Match signature = Regex.Match(source, Regex.Escape(returnType) + @" " + methodName + @"\s*\(");
            Assert.IsTrue(signature.Success, $"Could not find {methodName}.");

            int braceStart = source.IndexOf('{', signature.Index);
            Assert.GreaterOrEqual(braceStart, 0, $"Could not find body for {methodName}.");

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(signature.Index, i - signature.Index + 1);
                }
            }

            Assert.Fail($"Could not parse body for {methodName}.");
            return string.Empty;
        }

        private static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private static Vector2 GetLayoutVector(object layout, string propertyName)
        {
            PropertyInfo property = layout.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, $"Layout result should expose {propertyName}.");
            return (Vector2)property.GetValue(layout);
        }

        private static void AssertVectorApproximately(Vector2 expected, Vector2 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        }
    }
}
