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
        public void ParseStartPlayPayload_UsesProvidedTimeBeforeHistory()
        {
            string json = "{\"uri\":\"file:///movie.mp4\",\"title\":\"Movie\",\"time\":12000,\"projection\":\"360\",\"stereo\":\"lr\"}";

            MediaWrapper media = VlcPlaybackPayloadParser.ParseStartPlayPayload(json, _ => 34000);

            Assert.AreEqual("file:///movie.mp4", media.Uri);
            Assert.AreEqual(12000, media.Time);
            Assert.AreEqual(MediaProjectionType.Sphere360, media.Projection);
            Assert.AreEqual("lr", media.StereoHint);
        }

        [Test]
        public void ParseStartPlayPayload_DecodesUriForHistoryButKeepsRawJsonPlaybackUri()
        {
            string encodedUri = "file:///sdcard/Movies/%E4%BD%A0%E5%A5%BD%20Movie.mp4";
            string decodedUri = "file:///sdcard/Movies/\u4F60\u597D Movie.mp4";
            string lookupUri = null;
            string json = "{\"uri\":\"" + encodedUri + "\",\"title\":\"Movie\",\"time\":0,\"fromStart\":false}";

            MediaWrapper media = VlcPlaybackPayloadParser.ParseStartPlayPayload(json, uri =>
            {
                lookupUri = uri;
                return 45000;
            });

            Assert.AreEqual(decodedUri, media.Id);
            Assert.AreEqual(decodedUri, media.Uri);
            Assert.AreEqual(decodedUri, lookupUri);
            Assert.AreEqual(45000, media.Time);
            StringAssert.Contains(encodedUri, media.RawJson);
        }

        [Test]
        public void PlaybackServiceBridge_SendsSubtitleTrackJsonWithSlaveInfoAndAudioTrackMarkers()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.Contains("getTrackInfo()", bridgeSource);
            StringAssert.Contains("formatTrackPayload(trackId, it.name, trackId == selectedAudioTrack)", bridgeSource);
            StringAssert.Contains("service.spuTrack", bridgeSource);
            StringAssert.Contains("resolveSubtitleSlaves(service)", bridgeSource);
            StringAssert.Contains("SlaveRepository.getInstance(AppContextProvider.appContext)", bridgeSource);
            StringAssert.Contains("service.currentMediaLocation", bridgeSource);
            StringAssert.Contains(".put(\"selected\", trackId == selectedSpuTrack)", bridgeSource);
            StringAssert.Contains("\"slave\"", bridgeSource);
            StringAssert.Contains("org.json.JSONArray", bridgeSource);
            StringAssert.Contains("pendingDTO?.slaves", bridgeSource);
            StringAssert.DoesNotContain("subtitleDisplayNameFromUri", bridgeSource);
            StringAssert.DoesNotContain("pushTrackInfo", bridgeSource);
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
            StringAssert.Contains("isSavedEnabledSubtitleTrack", playlistManagerSource);
            StringAssert.Contains("MediaPlayer.Event.ESAdded", playlistManagerSource);
            StringAssert.Contains("IMedia.Track.Type.Text", playlistManagerSource);

            StringAssert.DoesNotContain("KEY_SUBTITLE_PREFERRED_LANGUAGE", bridgeSource);
            StringAssert.DoesNotContain("resolveDefaultSubtitleTrack", bridgeSource);
        }

        [Test]
        public void MediaUtils_ReturnsToUnityByReorderingUnityActivityToFront()
        {
            string mediaUtilsSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/media/MediaUtils.kt"));

            StringAssert.Contains("bringUnityToFront(context)", mediaUtilsSource);
            StringAssert.Contains("Intent.FLAG_ACTIVITY_REORDER_TO_FRONT", mediaUtilsSource);
            StringAssert.Contains("ComponentName(context.packageName, \"com.unity3d.player.UnityPlayerActivity\")", mediaUtilsSource);
            StringAssert.Contains("UnityPlayerActivity", mediaUtilsSource);
            StringAssert.DoesNotContain("moveVlcPickerTaskToBack(context)", mediaUtilsSource);
            StringAssert.DoesNotContain("moveTaskToBack(true)", mediaUtilsSource);
            StringAssert.DoesNotContain("finishAffinity()", mediaUtilsSource);
        }

        [Test]
        public void AndroidBridge_TracksAndRestoresLastVlcActivityForSameTaskSwitching()
        {
            string appContextSource = File.ReadAllText(ProjectFile("vlc-android/application/resources/src/main/java/org/videolan/resources/AppContextProvider.kt"));
            string initializerSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/VlcAppInitializer.kt"));
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.Contains("lastVlcActivity", appContextSource);
            StringAssert.Contains("lastVlcActivityComponent", appContextSource);
            StringAssert.Contains("isVlcActivity(activity)", initializerSource);
            StringAssert.Contains("AppContextProvider.lastVlcActivityComponent = activity.componentName", initializerSource);
            StringAssert.DoesNotContain("AppContextProvider.lastVlcActivityComponent = null", initializerSource);
            StringAssert.Contains("fun showLastVlcActivity(context: Context?)", bridgeSource);
            StringAssert.Contains("AppContextProvider.lastVlcActivityComponent", bridgeSource);
            StringAssert.Contains("Intent.FLAG_ACTIVITY_REORDER_TO_FRONT", bridgeSource);
            StringAssert.Contains("org.videolan.vlc.StartActivity", bridgeSource);
        }

        [Test]
        public void VlcManifest_UsesSameTaskActivitiesForUnityReorderFlow()
        {
            string manifest = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/AndroidManifest.xml"));

            StringAssert.DoesNotContain("android:taskAffinity=\":vlc\"", manifest);
            StringAssert.DoesNotContain("android:launchMode=\"singleTask\"", manifest);
            StringAssert.Contains("android:launchMode=\"singleTop\"", manifest);
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
        public void VlcMainActivity_MovesItselfToBackWhenUserLeavesForHomeProbe()
        {
            string mainActivitySource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/gui/MainActivity.kt"));

            StringAssert.Contains("override fun onUserLeaveHint()", mainActivitySource);
            StringAssert.Contains("VLC/HomeProbe", mainActivitySource);
            StringAssert.Contains("moveTaskToBack(true)", mainActivitySource);
            StringAssert.DoesNotContain("finishAndRemoveTask()", mainActivitySource);
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

        [Test]
        public void ParseSubtitleCuePayload_ParsesTextCueAndSegments()
        {
            var cue = VlcPlaybackPayloadParser.ParseSubtitleCuePayload(
                "{\"seq\":12,\"trackId\":\"3\",\"startMs\":124000,\"endMs\":127500,\"text\":\"Hello world\",\"align\":\"bottom-center\",\"source\":\"text\",\"segments\":[{\"text\":\"Hello\",\"bold\":true,\"italic\":false,\"underline\":false,\"color\":\"#FFFFFFFF\"},{\"text\":\" world\",\"bold\":false,\"italic\":true,\"underline\":false,\"color\":\"#FF00FFFF\"}]}");

            Assert.AreEqual(12, cue.seq);
            Assert.AreEqual("3", cue.trackId);
            Assert.AreEqual(124000, cue.startMs);
            Assert.AreEqual(127500, cue.endMs);
            Assert.AreEqual("Hello world", cue.text);
            Assert.AreEqual("bottom-center", cue.align);
            Assert.AreEqual(SubtitleCueSource.Text, cue.Source);
            Assert.AreEqual(2, cue.segments.Length);
            Assert.IsTrue(cue.segments[0].bold);
            Assert.IsTrue(cue.segments[1].italic);
        }

        [Test]
        public void ParseSubtitleCuePayload_ParsesCueV2StyleRunsAndFontAttachments()
        {
            var cue = VlcPlaybackPayloadParser.ParseSubtitleCuePayload(
                "{\"version\":2,\"seq\":21,\"trackId\":\"spu:3\",\"startMs\":1000,\"endMs\":2500,\"text\":\"你好 Hello\",\"source\":\"text\",\"styleRuns\":[{\"start\":0,\"end\":8,\"fontFamily\":\"Noto Sans CJK\",\"fontSize\":48,\"bold\":true,\"italic\":false,\"fillColor\":\"#FFFFFFFF\",\"outlineColor\":\"#FF000000\",\"outlineWidth\":3}],\"fontAttachments\":[{\"name\":\"MovieFont.ttf\",\"family\":\"Movie Font\",\"mime\":\"application/x-truetype-font\",\"cachePath\":\"/data/data/pkg/files/subtitle_fonts/font.ttf\",\"size\":1234,\"sha256\":\"abc\"}],\"layout\":{\"align\":\"bottom-center\",\"marginL\":1,\"marginR\":2,\"marginV\":48}}");

            Assert.AreEqual(2, cue.version);
            Assert.AreEqual(21, cue.seq);
            Assert.AreEqual("你好 Hello", cue.text);
            Assert.AreEqual(1, cue.styleRuns.Length);
            Assert.AreEqual(0, cue.styleRuns[0].start);
            Assert.AreEqual(8, cue.styleRuns[0].end);
            Assert.AreEqual("Noto Sans CJK", cue.styleRuns[0].fontFamily);
            Assert.AreEqual(48f, cue.styleRuns[0].fontSize);
            Assert.IsTrue(cue.styleRuns[0].bold);
            Assert.AreEqual("#FFFFFFFF", cue.styleRuns[0].fillColor);
            Assert.AreEqual("#FF000000", cue.styleRuns[0].outlineColor);
            Assert.AreEqual(3f, cue.styleRuns[0].outlineWidth);
            Assert.AreEqual(1, cue.fontAttachments.Length);
            Assert.AreEqual("MovieFont.ttf", cue.fontAttachments[0].name);
            Assert.AreEqual("Movie Font", cue.fontAttachments[0].family);
            Assert.AreEqual("/data/data/pkg/files/subtitle_fonts/font.ttf", cue.fontAttachments[0].cachePath);
            Assert.AreEqual(1234, cue.fontAttachments[0].size);
            Assert.AreEqual("bottom-center", cue.layout.align);
            Assert.AreEqual(1, cue.layout.marginL);
            Assert.AreEqual(2, cue.layout.marginR);
            Assert.AreEqual(48, cue.layout.marginV);
        }

        [Test]
        public void ParseSubtitleCuePayload_ParsesClearCue()
        {
            var cue = VlcPlaybackPayloadParser.ParseSubtitleCuePayload(
                "{\"seq\":13,\"trackId\":\"\",\"startMs\":0,\"endMs\":0,\"text\":\"\",\"segments\":[],\"align\":\"bottom-center\",\"source\":\"clear\"}");

            Assert.AreEqual(SubtitleCueSource.Clear, cue.Source);
            Assert.IsTrue(cue.IsClear);
            Assert.AreEqual(0, cue.segments.Length);
        }
    }
}
