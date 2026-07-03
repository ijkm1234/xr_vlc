using NUnit.Framework;
using System.IO;
using UnityEngine;
using XRVLC.Media;

namespace XRVLC.Tests
{
    [TestFixture]
    public class VlcPlaybackPayloadParserTests
    {
        [Test]
        public void ParseTracksData_ParsesIdAndName()
        {
            var tracks = VlcPlaybackPayloadParser.ParseTracksData("-1:Disable|1:English");

            Assert.AreEqual(2, tracks.Count);
            Assert.AreEqual("-1", tracks[0].Id);
            Assert.AreEqual("English", tracks[1].Name);
        }

        [Test]
        public void ParseTracksData_ParsesSelectedTrackMarker()
        {
            var tracks = VlcPlaybackPayloadParser.ParseTracksData("-1:Disable|*1:Option A");

            Assert.AreEqual(2, tracks.Count);
            Assert.IsFalse(tracks[0].IsSelected);
            Assert.AreEqual("1", tracks[1].Id);
            Assert.AreEqual("Option A", tracks[1].Name);
            Assert.IsTrue(tracks[1].IsSelected);
        }

        [Test]
        public void ParseTracksData_ParsesJsonSubtitleTracksWithSlaveInfo()
        {
            string json = "{\"tracks\":[{\"id\":\"-1\",\"name\":\"Disable\",\"selected\":false},{\"id\":\"7\",\"name\":\"Track 1\",\"selected\":true,\"slave\":{\"type\":0,\"priority\":4,\"uri\":\"file:///storage/emulated/0/Movies/My.Movie.zh.ass?token=1\"}}]}";

            var tracks = VlcPlaybackPayloadParser.ParseTracksData(json);

            Assert.AreEqual(2, tracks.Count);
            Assert.AreEqual("7", tracks[1].Id);
            Assert.AreEqual("Track 1", tracks[1].Name);
            Assert.IsTrue(tracks[1].IsSelected);
            Assert.IsNotNull(tracks[1].Slave);
            Assert.AreEqual(0, tracks[1].Slave.type);
            Assert.AreEqual(4, tracks[1].Slave.priority);
            Assert.AreEqual("file:///storage/emulated/0/Movies/My.Movie.zh.ass?token=1", tracks[1].Slave.uri);
        }

        [Test]
        public void ParseTrackSnapshot_ParsesAudioAndSubtitleTracks()
        {
            string json = "{\"audioTracks\":[{\"id\":\"-1\",\"name\":\"Disable\",\"selected\":false},{\"id\":\"2\",\"name\":\"Stereo\",\"selected\":true}],\"subtitleTracks\":[{\"id\":\"-1\",\"name\":\"Disable\",\"selected\":false},{\"id\":\"7\",\"name\":\"Track 1\",\"selected\":true,\"slave\":{\"type\":0,\"priority\":4,\"uri\":\"file:///subtitle.ass\"}}]}";

            TrackSnapshot snapshot = VlcPlaybackPayloadParser.ParseTrackSnapshot(json);

            Assert.AreEqual(2, snapshot.AudioTracks.Count);
            Assert.AreEqual("2", snapshot.AudioTracks[1].Id);
            Assert.IsTrue(snapshot.AudioTracks[1].IsSelected);
            Assert.AreEqual(2, snapshot.SubtitleTracks.Count);
            Assert.AreEqual("7", snapshot.SubtitleTracks[1].Id);
            Assert.IsTrue(snapshot.SubtitleTracks[1].IsSelected);
            Assert.IsNotNull(snapshot.SubtitleTracks[1].Slave);
            Assert.AreEqual("file:///subtitle.ass", snapshot.SubtitleTracks[1].Slave.uri);
        }

        [Test]
        public void ParseMediaParseResult_UsesVisibleSizeAsContentSizeWithRawFallback()
        {
            string json = "{\"width\":3840,\"height\":2160,\"visibleWidth\":3840,\"visibleHeight\":1920,\"projection\":\"360\",\"duration\":1000}";

            VlcMediaParseResult result = VlcPlaybackPayloadParser.ParseMediaParseResult(json);

            Assert.AreEqual(3840, result.width);
            Assert.AreEqual(2160, result.height);
            Assert.AreEqual(3840, result.visibleWidth);
            Assert.AreEqual(1920, result.visibleHeight);
            Assert.AreEqual(3840, result.ContentWidth);
            Assert.AreEqual(1920, result.ContentHeight);
            Assert.AreEqual(3840, result.ToVideoSize().ContentWidth);
            Assert.AreEqual(1920, result.ToVideoSize().ContentHeight);
        }

        [Test]
        public void ParseMediaParseResult_FallsBackToRawSizeWhenVisibleSizeIsMissing()
        {
            string json = "{\"width\":1920,\"height\":1080,\"projection\":\"flat\",\"duration\":1000}";

            VlcMediaParseResult result = VlcPlaybackPayloadParser.ParseMediaParseResult(json);

            Assert.AreEqual(1920, result.ContentWidth);
            Assert.AreEqual(1080, result.ContentHeight);
            Assert.AreEqual(1920, result.ToVideoSize().ContentWidth);
            Assert.AreEqual(1080, result.ToVideoSize().ContentHeight);
        }

        [Test]
        public void ParseStartPlayPayload_DoesNotUseAndroidPayloadTimeForResume()
        {
            string json = "{\"uri\":\"file:///movie.mp4\",\"title\":\"Movie\",\"time\":12000,\"projection\":\"360\",\"stereo\":\"lr\"}";

            MediaWrapper media = VlcPlaybackPayloadParser.ParseStartPlayPayload(json);

            Assert.AreEqual("file:///movie.mp4", media.Uri);
            Assert.AreEqual(0, media.Time);
            Assert.AreEqual(MediaProjectionType.Flat2D, media.Projection);
            Assert.IsNull(media.StereoHint);
        }

        [Test]
        public void ParseStartPlayPayload_DecodesUriButDoesNotFallbackToUnityHistory()
        {
            string encodedUri = "file:///sdcard/Movies/%E4%BD%A0%E5%A5%BD%20Movie.mp4";
            string decodedUri = "file:///sdcard/Movies/\u4F60\u597D Movie.mp4";
            string json = "{\"uri\":\"" + encodedUri + "\",\"index\":3}";

            MediaWrapper media = VlcPlaybackPayloadParser.ParseStartPlayPayload(json);

            Assert.AreEqual(decodedUri, media.Id);
            Assert.AreEqual(decodedUri, media.Uri);
            Assert.AreEqual(0, media.Time);
            Assert.AreEqual(3, media.PositionInList);
            StringAssert.Contains(encodedUri, media.RawJson);
        }

        [Test]
        public void UnityPlayback_DoesNotReadOrWriteMedialibraryLastTime()
        {
            string parserSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/Parsing/VlcPlaybackPayloadParser.cs"));
            string playbackBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));
            string mediaLibraryBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/MediaLibrary/VlcMediaLibraryBridge.cs"));

            StringAssert.Contains("ParseStartPlayPayload(string jsonPayload)", parserSource);
            StringAssert.Contains("Time = 0", parserSource);
            StringAssert.DoesNotContain("long startTime = dto.time", parserSource);
            StringAssert.DoesNotContain("lastTimeProvider", parserSource);
            StringAssert.DoesNotContain("VlcMediaLibraryBridge.GetLastTime", playbackBridgeSource);
            StringAssert.DoesNotContain("GetLastTime", mediaLibraryBridgeSource);
            StringAssert.DoesNotContain("SetLastTime", mediaLibraryBridgeSource);
        }

        [Test]
        public void PlaybackServiceBridge_SendsSubtitleTrackJsonWithSlaveInfoAndAudioTrackMarkers()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.Contains("fun getAudioTrackSnapshot(): String", bridgeSource);
            StringAssert.Contains("fun getSubtitleTrackSnapshot(): String", bridgeSource);
            StringAssert.Contains("fun setAudioTrackAndGetSnapshot(trackId: String): String", bridgeSource);
            StringAssert.Contains("fun setSpuTrackAndGetSnapshot(trackId: String): String", bridgeSource);
            StringAssert.Contains("buildAudioTrackSnapshotJson", bridgeSource);
            StringAssert.Contains("buildSubtitleTrackSnapshotJson", bridgeSource);
            StringAssert.Contains("buildTrackSnapshotJson", bridgeSource);
            StringAssert.Contains("buildAudioTrackSnapshotJson(service, \"setAudioTrackAndGetSnapshot\")", bridgeSource);
            StringAssert.Contains("buildSubtitleTrackSnapshotJson(service, \"setSpuTrackAndGetSnapshot\")", bridgeSource);
            StringAssert.Contains("val audioTracks = service.audioTracks ?: emptyArray()", bridgeSource);
            StringAssert.Contains("val spuTracks = service.spuTracks ?: emptyArray()", bridgeSource);
            StringAssert.Contains(".put(\"audioTracks\", audioTracksArray)", bridgeSource);
            StringAssert.Contains(".put(\"subtitleTracks\", subtitleTracksArray)", bridgeSource);
            StringAssert.Contains("val selectedAudioTrack = service.audioTrack", bridgeSource);
            StringAssert.Contains("service.spuTrack", bridgeSource);
            StringAssert.DoesNotContain("val audioTracks = mediaPlayer.audioTracks ?: emptyArray()", bridgeSource);
            StringAssert.DoesNotContain("val spuTracks = mediaPlayer.spuTracks ?: emptyArray()", bridgeSource);
            StringAssert.Contains("withTimeoutOrNull(500L) { resolveSubtitleSlaves(service) } ?: emptyList()", bridgeSource);
            StringAssert.Contains("SlaveRepository.getInstance(AppContextProvider.appContext)", bridgeSource);
            StringAssert.Contains("service.currentMediaLocation", bridgeSource);
            StringAssert.Contains(".put(\"selected\", trackId == selectedSpuTrack)", bridgeSource);
            StringAssert.Contains("\"slave\"", bridgeSource);
            StringAssert.Contains("org.json.JSONArray", bridgeSource);
            StringAssert.Contains("service.currentMediaWrapper?.slaves", bridgeSource);
            StringAssert.DoesNotContain("pendingDTO?.slaves", bridgeSource);
            StringAssert.DoesNotContain("subtitleDisplayNameFromUri", bridgeSource);
            StringAssert.DoesNotContain("pushTrackInfo", bridgeSource);
        }

        [Test]
        public void PlaybackServiceBridge_UserAudioSelectionWritesSelectedTrackToMedialibrary()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));
            string playbackServiceSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/PlaybackService.kt"));

            StringAssert.Contains("val switched = service.setAudioTrack(trackId)", bridgeSource);
            StringAssert.Contains("if (switched) saveSelectedAudioTrack(service, trackId)", bridgeSource);
            StringAssert.Contains("private fun saveSelectedAudioTrack(service: PlaybackService, trackId: String)", bridgeSource);
            StringAssert.Contains("media.setStringMeta(MediaWrapper.META_AUDIOTRACK, trackId)", bridgeSource);
            StringAssert.Contains("fun setAudioTrack(index: String) = playlistManager.player.setAudioTrack(index)", playbackServiceSource);
            StringAssert.DoesNotContain("META_AUDIOTRACK", playbackServiceSource);
        }

        [Test]
        public void UnityBridge_UsesActiveTrackSnapshotAndDoesNotCacheTrackLists()
        {
            string parserSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/Parsing/VlcPlaybackPayloadParser.cs"));
            string playbackBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));
            string snapshotSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackSnapshot.cs"));
            string playbackServiceSource = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Playback/PlaybackService.cs"));

            StringAssert.Contains("public static TrackSnapshot ParseTrackSnapshot(string data)", parserSource);
            StringAssert.Contains("public static TrackSnapshot GetAudioTrackSnapshot()", playbackBridgeSource);
            StringAssert.Contains("public static TrackSnapshot GetSubtitleTrackSnapshot()", playbackBridgeSource);
            StringAssert.Contains("public static TrackSnapshot SetAudioTrackAndGetSnapshot(string trackId)", playbackBridgeSource);
            StringAssert.Contains("public static TrackSnapshot SetSpuTrackAndGetSnapshot(string trackId)", playbackBridgeSource);
            StringAssert.Contains("VlcPlaybackPayloadParser.ParseTrackSnapshot", playbackBridgeSource);
            StringAssert.DoesNotContain("SetAudioTracks", snapshotSource);
            StringAssert.DoesNotContain("SetSubtitleTracks", snapshotSource);
            StringAssert.DoesNotContain("AudioTracks { get; }", snapshotSource);
            StringAssert.DoesNotContain("SubtitleTracks { get; }", snapshotSource);
            StringAssert.DoesNotContain("Snapshot.SetAudioTracks", playbackBridgeSource);
            StringAssert.DoesNotContain("Snapshot.SetSubtitleTracks", playbackBridgeSource);
            StringAssert.Contains("GetAudioTrackSnapshotFromVlc()", playbackServiceSource);
            StringAssert.Contains("GetSubtitleTrackSnapshotFromVlc()", playbackServiceSource);
            StringAssert.Contains("VlcPlaybackBridge.SetAudioTrackAndGetSnapshot(trackId)", playbackServiceSource);
            StringAssert.Contains("VlcPlaybackBridge.SetSpuTrackAndGetSnapshot(trackId)", playbackServiceSource);
        }

        [Test]
        public void PlaybackServiceBridge_DummyDecoderDisablesAudioOutput()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            int fallbackStart = bridgeSource.IndexOf("Static parse failed to get video size", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(fallbackStart, 0);
            int fallbackEnd = bridgeSource.IndexOf("dummyMediaPlayer.play()", fallbackStart, System.StringComparison.Ordinal);
            Assert.Greater(fallbackEnd, fallbackStart);
            string fallbackBlock = bridgeSource.Substring(fallbackStart, fallbackEnd - fallbackStart);

            StringAssert.Contains("media.addOption(\":no-audio\")", fallbackBlock);
            StringAssert.Contains("media.addOption(\":no-spu\")", fallbackBlock);
        }

        [Test]
        public void PlaybackServiceBridge_SendsVisibleVideoSizeToUnity()
        {
            string androidBridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));
            string unityBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));
            string playbackEventsSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackEvents.cs"));

            StringAssert.Contains("\"visibleWidth\":$visibleWidth", androidBridgeSource);
            StringAssert.Contains("\"visibleHeight\":$visibleHeight", androidBridgeSource);
            StringAssert.Contains("VideoLayoutSize(w, h, vw, vh)", androidBridgeSource);
            StringAssert.Contains("sendToUnity(UnityBridgeContract.Method.ON_VIDEO_SIZE_CHANGED, \"$width|$height|$visibleWidth|$visibleHeight\")", androidBridgeSource);
            StringAssert.DoesNotContain("sendToUnity(UnityBridgeContract.Method.ON_VIDEO_SIZE_CHANGED, \"1920|1080\")", androidBridgeSource);
            StringAssert.Contains("public static event Action<VlcVideoSize> OnVideoSizeChangedEvent", unityBridgeSource);
            StringAssert.Contains("new VlcVideoSize(width, height, visibleWidth, visibleHeight)", unityBridgeSource);
            StringAssert.Contains("public static event Action<VlcVideoSize> OnVideoSizeChanged", playbackEventsSource);
            StringAssert.DoesNotContain("public static event Action<int, int> OnVideoSizeChangedEvent", unityBridgeSource);
        }

        [Test]
        public void PlaybackServiceBridge_SkipsDuplicateRateRequests()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.Contains("lastAppliedRate", bridgeSource);
            StringAssert.Contains("if (kotlin.math.abs(lastAppliedRate - rate) < 0.001f)", bridgeSource);
            StringAssert.Contains("Skipping duplicate setRate request", bridgeSource);

            int pendingUriStart = bridgeSource.IndexOf("pendingUri = uri", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(pendingUriStart, 0);
            int parseStart = bridgeSource.IndexOf("withContext(Dispatchers.IO)", pendingUriStart, System.StringComparison.Ordinal);
            Assert.Greater(parseStart, pendingUriStart);
            string preloadStateBlock = bridgeSource.Substring(pendingUriStart, parseStart - pendingUriStart);
            StringAssert.Contains("lastAppliedRate = 1.0f", preloadStateBlock);
        }

        [Test]
        public void PlaybackService_PublishesRateToUnitySubtitleClock()
        {
            string playbackServiceSource = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Playback/PlaybackService.cs"));
            string playbackBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));
            string playbackEventsSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackEvents.cs"));

            int methodStart = playbackServiceSource.IndexOf("public void SetPlaybackRate(float rate)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = playbackServiceSource.IndexOf("public void ToggleShortcutPlaybackRate()", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBlock = playbackServiceSource.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("VlcPlaybackBridge.PublishPlaybackRate(rate)", methodBlock);
            StringAssert.Contains("public static event Action<float> OnPlaybackRateChangedEvent", playbackBridgeSource);
            StringAssert.Contains("public static event Action<float> OnPlaybackRateChanged", playbackEventsSource);
        }

        [Test]
        public void AndroidPlaybackServiceBridge_SendsActualPlaybackRateToUnity()
        {
            string contractSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/UnityBridgeContract.kt"));
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));
            string unityBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));

            StringAssert.Contains("const val ON_PLAYBACK_RATE_CHANGED = \"OnPlaybackRateChanged\"", contractSource);
            StringAssert.Contains("private fun sendPlaybackRateToUnity(rate: Float, reason: String)", bridgeSource);
            StringAssert.Contains("sendToUnity(UnityBridgeContract.Method.ON_PLAYBACK_RATE_CHANGED", bridgeSource);
            StringAssert.Contains("sendPlaybackRateToUnity(service.rate, \"duplicate-setRate\")", bridgeSource);
            StringAssert.Contains("sendPlaybackRateToUnity(service.rate, \"setRate\")", bridgeSource);
            StringAssert.Contains("sendPlaybackRateToUnity(it.rate, \"update\")", bridgeSource);
            StringAssert.Contains("private var lastKnownPlaybackRate = 1.0f", bridgeSource);
            StringAssert.Contains("fun getRate(): Float", bridgeSource);
            StringAssert.Contains("return lastKnownPlaybackRate", bridgeSource);
            StringAssert.Contains("public static float GetPlaybackRate()", unityBridgeSource);
            StringAssert.Contains("bridge.CallStatic<float>(\"getRate\")", unityBridgeSource);
            StringAssert.Contains("public void OnPlaybackRateChanged(string rateStr)", unityBridgeSource);
            StringAssert.Contains("PublishPlaybackRate(rate)", unityBridgeSource);
        }

        [Test]
        public void FlatSubtitleOverlay_BindsVlcSubtitleSurfaceForNativeAndSpatialModes()
        {
            string androidBridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));
            string unityBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));
            string videoScreenSource = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Screen/VideoScreen.cs"));
            string playbackServiceSource = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Playback/PlaybackService.cs"));
            string flatSubtitleOverlaySource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/Rendering/FlatSubtitleOverlaySurface.cs"));
            string aWindowSource = File.ReadAllText(ProjectFile("vlc-android/libvlcjni/libvlc/src/org/videolan/libvlc/AWindow.java"));

            StringAssert.Contains("fun setSubtitleSurface(surface: Surface?)", androidBridgeSource);
            StringAssert.Contains("private var videoSurface: Surface? = null", androidBridgeSource);
            StringAssert.Contains("private var subtitleSurface: Surface? = null", androidBridgeSource);
            StringAssert.Contains("vout.setSubtitlesSurface(currentSubtitleSurface, null)", androidBridgeSource);
            StringAssert.Contains("configureVoutSurfaces()", androidBridgeSource);
            StringAssert.Contains("private fun configureVoutSurfacesOnMainBlocking(reason: String)", androidBridgeSource);
            StringAssert.Contains("CountDownLatch", androidBridgeSource);
            StringAssert.Contains("latch.await", androidBridgeSource);
            StringAssert.Contains("configureVoutSurfaces failed", androidBridgeSource);
            StringAssert.Contains("AWindow native surface state after attach", androidBridgeSource);
            StringAssert.Contains("vout.debugHasVideoSurface()", androidBridgeSource);
            StringAssert.Contains("vout.debugHasSubtitlesSurface()", androidBridgeSource);
            StringAssert.Contains("private var pendingFlatVideo = false", androidBridgeSource);
            StringAssert.Contains("pendingFlatVideo = projectionStr == \"flat\"", androidBridgeSource);
            StringAssert.Contains("private fun shouldDelayPendingMediaForSubtitleSurface(vout: IVLCVout?)", androidBridgeSource);
            StringAssert.Contains("Delaying pending media load until subtitle surface is attached", androidBridgeSource);
            StringAssert.Contains("if (shouldDelayPendingMediaForSubtitleSurface(vout)) return", androidBridgeSource);
            StringAssert.Contains("configureVoutSurfacesOnMainBlocking entry", androidBridgeSource);
            StringAssert.Contains("Calling vout.setSubtitlesSurface", androidBridgeSource);
            StringAssert.Contains("Subtitle surface configured for VLCVout; aWindowSubtitleAfterSet", androidBridgeSource);
            StringAssert.Contains("vout subtitle surface bind result", androidBridgeSource);
            StringAssert.Contains("PixelCopy.request", androidBridgeSource);
            StringAssert.Contains("scheduleSubtitleSurfacePixelProbe", androidBridgeSource);
            StringAssert.Contains("VLC subtitle surface pixel probe result", androidBridgeSource);
            StringAssert.Contains("nonTransparentPixels", androidBridgeSource);
            StringAssert.Contains("nonBlackPixels", androidBridgeSource);

            StringAssert.Contains("public static void SetSubtitleSurface(IntPtr surfacePtr)", unityBridgeSource);
            StringAssert.Contains("public static void DetachSubtitleSurface()", unityBridgeSource);
            StringAssert.Contains("GetStaticMethodID(bridgeClass, \"setSubtitleSurface\", \"(Landroid/view/Surface;)V\")", unityBridgeSource);
            StringAssert.Contains("private static SubtitleRenderMode ToAndroidSubtitleRenderMode(SubtitleRenderMode mode)", unityBridgeSource);
            StringAssert.Contains("mode == SubtitleRenderMode.Spatial ? SubtitleRenderMode.Native : mode", unityBridgeSource);
            StringAssert.Contains("uiMode={mode}, androidMode={bridgeMode}", unityBridgeSource);
            StringAssert.Contains("SetSubtitleSurface JNI entry", unityBridgeSource);
            StringAssert.Contains("SetSubtitleSurface JNI invoking Android bridge", unityBridgeSource);
            StringAssert.Contains("SetSubtitleSurface JNI call returned", unityBridgeSource);

            StringAssert.Contains("FlatSubtitleOverlaySurface", flatSubtitleOverlaySource);
            StringAssert.Contains("OverlayType.Underlay", flatSubtitleOverlaySource);
            StringAssert.Contains("useTextureAlphaBlending = true", flatSubtitleOverlaySource);
            StringAssert.Contains("usePremultipliedAlpha = false", flatSubtitleOverlaySource);
            StringAssert.Contains("UnderlayAlphaHoleRegistry.SetHole(transform", flatSubtitleOverlaySource);
            StringAssert.Contains("UnderlayAlphaHoleRegistry.Disable(transform)", flatSubtitleOverlaySource);
            StringAssert.Contains("OverlayShape.Quad", flatSubtitleOverlaySource);
            StringAssert.Contains("TextureType.ExternalSurface", flatSubtitleOverlaySource);
            StringAssert.Contains("SetWorldGeometry", flatSubtitleOverlaySource);
            StringAssert.Contains("RebuildLayer start", flatSubtitleOverlaySource);
            StringAssert.Contains("externalObject=", flatSubtitleOverlaySource);

            StringAssert.Contains("RebuildFlatSubtitleLayer", videoScreenSource);
            StringAssert.Contains("DestroyFlatSubtitleLayer", videoScreenSource);
            StringAssert.Contains("Flat subtitle layer rebuild entry", videoScreenSource);
            StringAssert.Contains("Flat subtitle layer rebuild dispatched", videoScreenSource);
            StringAssert.Contains("UpdateNativeSubtitleSurfaceBinding()", playbackServiceSource);
            StringAssert.Contains("UsesSingleHeightSubtitleSurface()", playbackServiceSource);
            StringAssert.Contains("private static bool UsesFlatSubtitleSurfaceMode(SubtitleRenderMode mode)", playbackServiceSource);
            StringAssert.Contains("mode == SubtitleRenderMode.Spatial", playbackServiceSource);
            StringAssert.Contains("private void BindSubtitleSurface(SubtitleSurfaceSpec subtitleSpec)", playbackServiceSource);
            StringAssert.Contains("VlcPlaybackBridge.SetSubtitleSurface(subtitleSurfacePtr)", playbackServiceSource);
            StringAssert.Contains("Flat subtitle surface binding requested", playbackServiceSource);
            StringAssert.Contains("Flat subtitle surface binding skipped", playbackServiceSource);
            StringAssert.Contains("Bound subtitle surface", playbackServiceSource);

            StringAssert.Contains("if (mSurfaceView == null && mTextureView == null)", aWindowSource);
            StringAssert.Contains("return getNativeSurface(mId) != null;", aWindowSource);
            StringAssert.Contains("boolean debugHasVideoSurface()", aWindowSource);
            StringAssert.Contains("boolean debugHasSubtitlesSurface()", aWindowSource);
        }

        [Test]
        public void VlcOptions_ForcesOpenSlesAoutWithoutTimeStretchProbe()
        {
            string optionsSource = File.ReadAllText(ProjectFile("vlc-android/application/resources/src/main/java/org/videolan/resources/VLCOptions.kt"));

            StringAssert.Contains("XR_AOUT_PROBE = \"opensles\"", optionsSource);
            StringAssert.Contains("context.resources.getBoolean(R.bool.time_stretching_default)", optionsSource);
            StringAssert.Contains("pref.getBoolean(KEY_ENABLE_TIME_STRETCHING_AUDIO, timeStrechingDefault)", optionsSource);
            StringAssert.Contains("options.add(if (timeStreching) \"--audio-time-stretch\" else \"--no-audio-time-stretch\")", optionsSource);
            StringAssert.Contains("options.add(\"--aout=$forcedXrAoutProbe\")", optionsSource);
            StringAssert.DoesNotContain("XR_TIME_STRETCH_PROBE", optionsSource);
            StringAssert.DoesNotContain("forcing audio-time-stretch", optionsSource);
        }

        [Test]
        public void PlaybackService_BufferingCompleteDoesNotOverridePausedStatus()
        {
            string playbackServiceSource = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Playback/PlaybackService.cs"));

            int methodStart = playbackServiceSource.IndexOf("private void HandleBuffering(float buffering)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = playbackServiceSource.IndexOf("private void HandleAudioTracksChanged", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBlock = playbackServiceSource.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("if (CurrentStatus == PlayerStatus.Paused)\n                return;", methodBlock);
            StringAssert.Contains("OnBuffering?.Invoke(buffering);", methodBlock);
            StringAssert.Contains("else HandleStatusChanged(PlayerStatus.Playing);", methodBlock);
        }

        [Test]
        public void VlcStartup_DoesNotPreloadVlcAarForHomeProbe()
        {
            string unityBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));
            string launcherSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Launcher/VlcLibraryLauncher.cs"));
            string androidBridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.DoesNotContain("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]", unityBridgeSource);
            StringAssert.DoesNotContain("bridge.CallStatic(\"warmUp\", currentActivity);", unityBridgeSource);
            StringAssert.DoesNotContain("VlcPlaybackBridge.WarmUp();", launcherSource);
            StringAssert.DoesNotContain("fun warmUp(context: Context?)", androidBridgeSource);
            StringAssert.DoesNotContain("LibVLC.loadLibraries()", androidBridgeSource);
            StringAssert.DoesNotContain("FactoryManager.registerFactory(IMediaFactory.factoryId, MediaFactory())", androidBridgeSource);
            StringAssert.DoesNotContain("FactoryManager.registerFactory(ILibVLCFactory.factoryId, LibVLCFactory())", androidBridgeSource);
        }

        [Test]
        public void PlaylistManager_SelectsDefaultSubtitleInsideVlcPlaybackPath()
        {
            string playlistManagerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt"));
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.Contains("KEY_SUBTITLE_PREFERRED_LANGUAGE", playlistManagerSource);
            StringAssert.Contains("resolveDefaultSubtitleTrack()", playlistManagerSource);
            StringAssert.Contains("Locale.getDefault()", playlistManagerSource);
            StringAssert.Contains("subtitleTrackNameMatchesLanguage(realTrack?.language", playlistManagerSource);
            StringAssert.DoesNotContain("LocaleUtil.getLocaleFromVLC", playlistManagerSource);
            StringAssert.DoesNotContain("Moshi", playlistManagerSource);
            StringAssert.Contains("findFirstAvailableSubtitleTrack", playlistManagerSource);
            StringAssert.Contains("isSavedSubtitleTrackPreference", playlistManagerSource);
            StringAssert.DoesNotContain("track != \"-1\"", playlistManagerSource);
            StringAssert.Contains("MediaPlayer.Event.ESAdded", playlistManagerSource);
            StringAssert.Contains("IMedia.Track.Type.Text", playlistManagerSource);

            StringAssert.DoesNotContain("KEY_SUBTITLE_PREFERRED_LANGUAGE", bridgeSource);
            StringAssert.DoesNotContain("resolveDefaultSubtitleTrack", bridgeSource);
        }

        [Test]
        public void PlaylistManager_RetriesSavedSubtitleTrackWhenTextEsIsAdded()
        {
            string playlistManagerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt"));

            StringAssert.Contains("private fun applySavedSubtitleTrackIfNeeded(media: MediaWrapper): Boolean", playlistManagerSource);
            StringAssert.Contains("private fun applySubtitleTrackSelection(media: MediaWrapper)", playlistManagerSource);
            StringAssert.Contains("if (applySavedSubtitleTrackIfNeeded(media)) return", playlistManagerSource);
            StringAssert.Contains("if (player.setSpuTrack(savedTrack))", playlistManagerSource);
            StringAssert.Contains("defaultSubtitleTrackApplied = true", playlistManagerSource);
            StringAssert.Contains("Saved subtitle track not available yet; will retry on ESAdded", playlistManagerSource);
            StringAssert.Contains("applySubtitleTrackSelection(media)", playlistManagerSource);
            StringAssert.Contains("media.setStringMeta(MediaWrapper.META_SUBTITLE_TRACK, index)", playlistManagerSource);
        }

        [Test]
        public void MediaUtils_ReturnsToUnityByMovingVlcTaskToBack()
        {
            string mediaUtilsSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/MediaUtils.kt"));

            StringAssert.Contains("moveVlcPickerTaskToBack(context)", mediaUtilsSource);
            StringAssert.Contains("moveTaskToBack(true)", mediaUtilsSource);
            StringAssert.DoesNotContain("bringUnityToFront(context)", mediaUtilsSource);
            StringAssert.DoesNotContain("ComponentName(context.packageName, \"com.unity3d.player.UnityPlayerActivity\")", mediaUtilsSource);
            StringAssert.DoesNotContain("Intent.FLAG_ACTIVITY_REORDER_TO_FRONT", mediaUtilsSource);
            StringAssert.DoesNotContain("finishAffinity()", mediaUtilsSource);
        }

        [Test]
        public void MediaUtils_SendsOnlyUriAndIndexInStartPlayJson()
        {
            string mediaUtilsSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/MediaUtils.kt"));

            int methodStart = mediaUtilsSource.IndexOf("fun requestUnityStartPlay(context: Context, media: MediaWrapper, index: Int)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = mediaUtilsSource.IndexOf("fun playTracks", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBlock = mediaUtilsSource.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("json.put(\"uri\", uriString)", methodBlock);
            StringAssert.Contains("json.put(\"index\", index)", methodBlock);
            StringAssert.DoesNotContain("json.put(\"time\"", methodBlock);
            StringAssert.DoesNotContain("json.put(\"fromStart\"", methodBlock);
            StringAssert.DoesNotContain("json.put(\"slaves\"", methodBlock);
        }

        [Test]
        public void PlaybackServiceBridge_DoesNotUseDtoTimeOrFromStartForPlayback()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            int matchStart = bridgeSource.IndexOf("Media matches PlaylistManager. Resuming playIndex to preserve metadata.", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(matchStart, 0);
            int mismatchStart = bridgeSource.IndexOf("Media mismatch. Loading new media into PlaylistManager.", matchStart, System.StringComparison.Ordinal);
            Assert.Greater(mismatchStart, matchStart);
            string matchedMediaBlock = bridgeSource.Substring(matchStart, mismatchStart - matchStart);

            StringAssert.Contains("pm.playIndex(resolvedIndex, 0, false, false, true)", matchedMediaBlock);
            StringAssert.DoesNotContain("dto.fromStart", bridgeSource);
            StringAssert.DoesNotContain("dto.time", bridgeSource);
            StringAssert.DoesNotContain("media.time = dto.time", bridgeSource);
            StringAssert.DoesNotContain("normalizePendingResumeAfterDurationKnown", bridgeSource);
        }

        [Test]
        public void PlaybackServiceBridge_SeekUsesPlaybackServiceSeekSoManualProgressCanPersist()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            int methodStart = bridgeSource.IndexOf("fun seek(position: Float)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = bridgeSource.IndexOf("@JvmStatic", methodStart + 1, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string seekBlock = bridgeSource.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("val safePosition = position.coerceIn(0F, 1F)", seekBlock);
            StringAssert.Contains("val length = service.length", seekBlock);
            StringAssert.Contains("service.seek(targetTime, length.toDouble(), fromUser = true)", seekBlock);
            StringAssert.DoesNotContain("playbackService?.mediaplayer?.position = position", seekBlock);
        }

        [Test]
        public void PlaybackServiceBridge_SetTimeMarksSeekAsUserInitiated()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            int methodStart = bridgeSource.IndexOf("fun setTime(timeMs: Long)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = bridgeSource.IndexOf("@JvmStatic", methodStart + 1, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string setTimeBlock = bridgeSource.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("playbackService?.seek(timeMs, fromUser = true)", setTimeBlock);
            StringAssert.DoesNotContain("playbackService?.setTime(timeMs)", setTimeBlock);
        }

        [Test]
        public void PlaybackService_UserSeekSavesExplicitPlaybackTime()
        {
            string playbackServiceSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/PlaybackService.kt"));

            int methodStart = playbackServiceSource.IndexOf("fun seek(time: Long", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = playbackServiceSource.IndexOf("fun updateViewpoint", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string seekBlock = playbackServiceSource.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("if (fromUser) playlistManager.saveExplicitPlaybackTime(time)", seekBlock);
        }

        [Test]
        public void PlaylistManager_SavesExplicitSeekTimeAndClearsFinishedState()
        {
            string playlistManagerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt"));

            StringAssert.Contains("fun saveExplicitPlaybackTime(time: Long)", playlistManagerSource);
            StringAssert.Contains("currentMedia.removeFlags(MediaWrapper.MEDIA_FROM_START)", playlistManagerSource);
            StringAssert.Contains("if (endReachedFor == currentMedia.uri.toString()) endReachedFor = null", playlistManagerSource);
            StringAssert.Contains("medialibrary.setLastTime(media.id, safeTime)", playlistManagerSource);
        }

        [Test]
        public void PlaylistManager_ForceRestartClearsFinishedStateBeforePeriodicSaves()
        {
            string playlistManagerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt"));

            int startDeclaration = playlistManagerSource.IndexOf("val start: Long", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(startDeclaration, 0);
            int mediaCreation = playlistManagerSource.IndexOf("val media = mediaFactory.getFromUri", startDeclaration, System.StringComparison.Ordinal);
            Assert.Greater(mediaCreation, startDeclaration);
            string startBlock = playlistManagerSource.Substring(startDeclaration, mediaCreation - startDeclaration);

            StringAssert.Contains("if (forceRestart) saveExplicitPlaybackTime(0L)", startBlock);
        }

        [Test]
        public void AndroidBridge_RestoresExistingVlcTaskWithoutRestartingActivity()
        {
            string appContextSource = File.ReadAllText(ProjectFile("vlc-android/application/resources/src/main/java/org/videolan/resources/AppContextProvider.kt"));
            string initializerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/VlcAppInitializer.kt"));
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.Contains("fun restoreVlcTask(context: Context?): Boolean", bridgeSource);
            StringAssert.Contains("activityManager.appTasks", bridgeSource);
            StringAssert.Contains("appTask.moveToFront()", bridgeSource);
            StringAssert.Contains("private fun isVlcTask(taskInfo: ActivityManager.RecentTaskInfo): Boolean", bridgeSource);
            StringAssert.DoesNotContain("fun hideForegroundVlcActivity(context: Context?): Boolean", bridgeSource);
            StringAssert.DoesNotContain("PicoShellPackageName = \"com.pvr.vrshell\"", bridgeSource);
            StringAssert.DoesNotContain("isPicoShellTopActivity(activityManager)", bridgeSource);
            StringAssert.DoesNotContain("lastVlcActivity", appContextSource);
            StringAssert.DoesNotContain("lastVlcActivityComponent", appContextSource);
            StringAssert.DoesNotContain("isVlcActivity(activity)", initializerSource);
            StringAssert.DoesNotContain("lastVlcActivity", initializerSource);
            StringAssert.DoesNotContain("fun showLastVlcActivity(context: Context?)", bridgeSource);
            StringAssert.DoesNotContain("lastVlcActivityComponent", bridgeSource);
        }

        [Test]
        public void VlcManifest_UsesSeparateTaskForVlcActivities()
        {
            string manifest = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/AndroidManifest.xml"));
            int mainStart = manifest.IndexOf("<activity android:name=\".gui.MainActivity\"", System.StringComparison.Ordinal);
            int nextActivity = manifest.IndexOf("<activity android:name=\".gui.onboarding.OnboardingActivity\"", mainStart, System.StringComparison.Ordinal);
            string mainActivityManifest = manifest.Substring(mainStart, nextActivity - mainStart);

            StringAssert.Contains("android:taskAffinity=\"${applicationId}.vlc\"", mainActivityManifest);
            StringAssert.Contains("android:launchMode=\"singleTask\"", mainActivityManifest);
            StringAssert.DoesNotContain("android:launchMode=\"singleTop\"", mainActivityManifest);
        }

        [Test]
        public void VlcMainActivity_UsesWhiteIconParkLoadingFourSplashIcon()
        {
            string manifest = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/AndroidManifest.xml"));
            int mainStart = manifest.IndexOf("<activity android:name=\".gui.MainActivity\"", System.StringComparison.Ordinal);
            int nextActivity = manifest.IndexOf("<activity android:name=\".gui.onboarding.OnboardingActivity\"", mainStart, System.StringComparison.Ordinal);
            string mainActivityManifest = manifest.Substring(mainStart, nextActivity - mainStart);

            string styles = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/values/styles.xml"));
            string stylesV31 = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/values-v31/styles.xml"));

            StringAssert.Contains("android:icon=\"@drawable/ic_iconpark_loading_four\"", mainActivityManifest);
            StringAssert.Contains("android:logo=\"@drawable/ic_iconpark_loading_four\"", mainActivityManifest);
            StringAssert.Contains("android:theme=\"@style/Theme.VLC.Main\"", mainActivityManifest);
            StringAssert.DoesNotContain("android:icon=\"@drawable/icon\"", mainActivityManifest);
            StringAssert.Contains("<style name=\"Theme.VLC.Main\" parent=\"Theme.VLC\"", styles);
            StringAssert.Contains("<style name=\"Theme.VLC.Main\" parent=\"Theme.VLC\"", stylesV31);
            StringAssert.Contains("<item name=\"android:windowSplashScreenAnimatedIcon\">@drawable/ic_iconpark_loading_four</item>", stylesV31);
        }

        [Test]
        public void VlcStartActivity_UsesWhiteIconParkLoadingFourSplashIcon()
        {
            string manifest = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/AndroidManifest.xml"));
            int startActivityStart = manifest.IndexOf("<activity android:name=\".StartActivity\"", System.StringComparison.Ordinal);
            int nextActivity = manifest.IndexOf("<activity android:name=\".gui.MainActivity\"", startActivityStart, System.StringComparison.Ordinal);
            string startActivityManifest = manifest.Substring(startActivityStart, nextActivity - startActivityStart);

            string drawable = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/drawable/ic_iconpark_loading_four.xml"));
            string styles = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/values/styles.xml"));
            string stylesV31 = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/values-v31/styles.xml"));

            StringAssert.Contains("android:icon=\"@drawable/ic_iconpark_loading_four\"", startActivityManifest);
            StringAssert.Contains("android:logo=\"@drawable/ic_iconpark_loading_four\"", startActivityManifest);
            StringAssert.Contains("android:theme=\"@style/Theme.VLC.Start.Transparent.NoUI\"", startActivityManifest);
            StringAssert.DoesNotContain("android:icon=\"@mipmap/app_icon\"", startActivityManifest);
            StringAssert.DoesNotContain("android:logo=\"@drawable/pico_panel_icon\"", startActivityManifest);
            StringAssert.DoesNotContain("android:roundIcon=\"@mipmap/app_icon_round\"", startActivityManifest);

            StringAssert.Contains("android:pathData=\"M4 24C4 35.0457 12.9543 44 24 44C35.0457 44 44 35.0457 44 24C44 12.9543 35.0457 4 24 4\"", drawable);
            StringAssert.Contains("android:strokeColor=\"#FFFFFFFF\"", drawable);
            StringAssert.Contains("android:strokeWidth=\"4\"", drawable);
            StringAssert.Contains("android:strokeLineCap=\"round\"", drawable);

            StringAssert.Contains("<style name=\"Theme.VLC.Start.Transparent.NoUI\" parent=\"Theme.VLC.Transparent.NoUI\"", styles);
            StringAssert.Contains("<style name=\"Theme.VLC.Start.Transparent.NoUI\" parent=\"Theme.VLC.Transparent.NoUI\"", stylesV31);
            StringAssert.Contains("<item name=\"android:windowSplashScreenAnimatedIcon\">@drawable/ic_iconpark_loading_four</item>", stylesV31);
        }

        [Test]
        public void LauncherManifest_KeepsPackageLevelAppIcons()
        {
            string manifest = File.ReadAllText(ProjectFile("Assets/Plugins/Android/LauncherManifest.xml"));
            int applicationStart = manifest.IndexOf("<application", System.StringComparison.Ordinal);
            int unityActivityStart = manifest.IndexOf("<activity", applicationStart, System.StringComparison.Ordinal);
            string applicationManifest = manifest.Substring(applicationStart, unityActivityStart - applicationStart);
            string unityActivityManifest = manifest.Substring(unityActivityStart);

            StringAssert.Contains("android:icon=\"@mipmap/app_icon\"", applicationManifest);
            StringAssert.Contains("android:roundIcon=\"@mipmap/app_icon_round\"", applicationManifest);
            StringAssert.Contains("android:logo=\"@drawable/pico_panel_icon\"", applicationManifest);
            StringAssert.DoesNotContain("android:icon=\"@drawable/ic_iconpark_loading_four\"", applicationManifest);
            StringAssert.DoesNotContain("android:roundIcon=\"@drawable/ic_iconpark_loading_four\"", applicationManifest);
            StringAssert.DoesNotContain("android:logo=\"@drawable/ic_iconpark_loading_four\"", applicationManifest);
            StringAssert.Contains("android:icon=\"@mipmap/app_icon\"", unityActivityManifest);
            StringAssert.Contains("android:roundIcon=\"@mipmap/app_icon_round\"", unityActivityManifest);
            StringAssert.Contains("android:logo=\"@drawable/pico_panel_icon\"", unityActivityManifest);
        }

        [Test]
        public void VlcMainActivity_UsesLoadingFourForVlcTaskDescription()
        {
            string source = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/gui/MainActivity.kt"));

            StringAssert.Contains("import android.app.ActivityManager", source);
            StringAssert.Contains("private fun applyVlcTaskDescription()", source);
            StringAssert.Contains("ActivityManager.TaskDescription.Builder()", source);
            StringAssert.Contains(".setIcon(R.drawable.ic_iconpark_loading_four)", source);
            StringAssert.Contains("applyVlcTaskDescription()", source);
        }

        [Test]
        public void VlcManifest_DoesNotRegisterStartupInitializerForHomeProbe()
        {
            string manifest = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/AndroidManifest.xml"));

            StringAssert.DoesNotContain("androidx.startup.InitializationProvider", manifest);
            StringAssert.DoesNotContain("android:name=\"org.videolan.vlc.VlcAppInitializer\"", manifest);
            StringAssert.DoesNotContain("android:value=\"androidx.startup\"", manifest);
        }

        [Test]
        public void VlcMainActivity_DoesNotMoveVlcTaskToBackOnUserLeave()
        {
            string mainActivitySource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/gui/MainActivity.kt"));

            StringAssert.DoesNotContain("override fun onUserLeaveHint()", mainActivitySource);
            StringAssert.DoesNotContain("VLC/HomeProbe", mainActivitySource);
            StringAssert.DoesNotContain("moveTaskToBack(true)", mainActivitySource);
            StringAssert.DoesNotContain("finishAndRemoveTask()", mainActivitySource);
            StringAssert.Contains("AppContextProvider.currentActivity = this", mainActivitySource);
            StringAssert.Contains("AppContextProvider.aliveActivities", mainActivitySource);
        }

        [Test]
        public void VlcGradle_DoesNotPostprocessAarForHomeProbe()
        {
            string gradleSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/build.gradle"));

            StringAssert.DoesNotContain("mergeEmbeddedClassesIntoDebugAar", gradleSource);
            StringAssert.DoesNotContain("embeddedClassesAarsForUnity", gradleSource);
            StringAssert.DoesNotContain("libvlcjni/libvlc/build/outputs/aar/libvlc-debug.aar", gradleSource);
        }

        [Test]
        public void PlaylistManager_MediaSwitchRequestsUnitySurfaceClearBeforePlayback()
        {
            string playlistManagerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt"));

            StringAssert.Contains("private fun clearUnityPlaybackSurfaceBeforeMediaSwitch(forcePlay: Boolean)", playlistManagerSource);
            StringAssert.Contains("UnityBridgeContract.Method.CLEAR_PLAYBACK_SURFACE", playlistManagerSource);
            StringAssert.Contains("if (forcePlay) return", playlistManagerSource);
            StringAssert.Contains("clearUnityPlaybackSurfaceBeforeMediaSwitch(forcePlay)", playlistManagerSource);
            StringAssert.Contains("MediaUtils.requestUnityStartPlay(ctx, mw, currentIndex)", playlistManagerSource);
        }

        [Test]
        public void MediaUtils_UserAudioSelectionRequestsExpandedNativeAudioPlayer()
        {
            string mediaUtilsSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/MediaUtils.kt"));
            string audioContainerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/gui/AudioPlayerContainerActivity.kt"));

            StringAssert.Contains("fun requestExpandAudioPlayerOnNextShow()", audioContainerSource);
            StringAssert.Contains("state = STATE_EXPANDED", audioContainerSource);
            StringAssert.Contains("private fun requestExpandedAudioPlayerForUserSelection(context: Context?, media: MediaWrapper?)", mediaUtilsSource);
            StringAssert.Contains("media.type == MediaWrapper.TYPE_AUDIO", mediaUtilsSource);
            StringAssert.Contains("media.hasFlag(MediaWrapper.MEDIA_FORCE_AUDIO)", mediaUtilsSource);
            StringAssert.Contains("requestExpandedAudioPlayerForUserSelection(context, media)", mediaUtilsSource);
            StringAssert.Contains("requestExpandedAudioPlayerForUserSelection(context, list.getOrNull(realPos))", mediaUtilsSource);
            StringAssert.Contains("requestExpandedAudioPlayerForUserSelection(context, tracks.getOrNull(position))", mediaUtilsSource);
        }

        [Test]
        public void UnityBridge_ClearPlaybackSurfaceMessageDetachesWithoutStoppingAndroidPlayback()
        {
            string contractSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/UnityBridgeContract.kt"));
            string playbackBridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));
            string playbackServiceSource = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Playback/PlaybackService.cs"));

            StringAssert.Contains("const val CLEAR_PLAYBACK_SURFACE = \"ClearPlaybackSurface\"", contractSource);
            StringAssert.Contains("public void ClearPlaybackSurface()", playbackBridgeSource);
            StringAssert.Contains("ClearPlaybackSurfaceEvent", playbackBridgeSource);
            StringAssert.Contains("VlcPlaybackEvents.OnClearPlaybackSurface += ClearPlaybackSurfaceForMediaSwitch", playbackServiceSource);
            StringAssert.Contains("private void ClearPlaybackSurfaceForMediaSwitch()", playbackServiceSource);
            StringAssert.Contains("VlcPlaybackBridge.DetachSurface();", playbackServiceSource);
            StringAssert.Contains("videoScreen.DestroyLayer();", playbackServiceSource);

            int clearMethodStart = playbackServiceSource.IndexOf("private void ClearPlaybackSurfaceForMediaSwitch()", System.StringComparison.Ordinal);
            int nextMethodStart = playbackServiceSource.IndexOf("\n        private ", clearMethodStart + 1, System.StringComparison.Ordinal);
            string clearMethod = playbackServiceSource.Substring(clearMethodStart, nextMethodStart - clearMethodStart);

            StringAssert.DoesNotContain("VlcPlaybackBridge.Stop()", clearMethod);
            StringAssert.DoesNotContain("StopInternal()", clearMethod);
        }

        [Test]
        public void AudioPlayer_FirstExpandedStateDoesNotLeaveMiniHeaderControlsVisible()
        {
            string animatorSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/gui/audio/AudioPlayerAnimator.kt")).Replace("\r\n", "\n");
            string containerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/gui/AudioPlayerContainerActivity.kt"));

            StringAssert.DoesNotContain("}.applyTo(binding.header)\n        headerShowPlaylistConstraint.applyTo(binding.header)", animatorSource);
            StringAssert.Contains("if (expandAudioPlayerOnNextShow) {", containerSource);
            StringAssert.Contains("state = STATE_EXPANDED", containerSource);
            StringAssert.Contains("audioPlayer.onSlide(1f)", containerSource);
        }

        [Test]
        public void AudioPlayerLayout_UsesIconParkCollapseButtonForExpandedPlayer()
        {
            string audioPlayerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/gui/audio/AudioPlayer.kt"));
            string portraitLayout = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/layout/audio_player.xml"));
            string landscapeLayout = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/layout-land/audio_player.xml"));
            string iconSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/res/drawable/ic_iconpark_expand_down_one.xml"));

            StringAssert.Contains("fun onCollapsePlayerClick", audioPlayerSource);
            StringAssert.Contains("slideDownAudioPlayer()", audioPlayerSource);
            StringAssert.Contains("android:id=\"@+id/collapse_audio_player\"", portraitLayout);
            StringAssert.Contains("android:onClick=\"@{fragment::onCollapsePlayerClick}\"", portraitLayout);
            StringAssert.Contains("app:srcCompat=\"@drawable/ic_iconpark_expand_down_one\"", portraitLayout);
            StringAssert.Contains("android:id=\"@+id/collapse_audio_player\"", landscapeLayout);
            StringAssert.Contains("android:onClick=\"@{fragment::onCollapsePlayerClick}\"", landscapeLayout);
            StringAssert.Contains("app:srcCompat=\"@drawable/ic_iconpark_expand_down_one\"", landscapeLayout);
            StringAssert.Contains("IconPark expand-down-one", iconSource);
        }

        [Test]
        public void ParseMediaParseResult_ParsesDimensionsProjectionAndDuration()
        {
            var result = VlcPlaybackPayloadParser.ParseMediaParseResult(
                "{\"width\":3840,\"height\":1920,\"projection\":\"180\",\"duration\":7200000}");

            Assert.AreEqual(3840, result.width);
            Assert.AreEqual(1920, result.height);
            Assert.AreEqual("180", result.projection);
            Assert.AreEqual(7200000, result.duration);
        }

        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
