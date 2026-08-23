using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class VlcLibraryLauncherSourceTests
    {
        private static string ReadLauncherSource()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts/Infrastructure/VlcBridge/Launcher/VlcLibraryLauncher.cs");
            return File.ReadAllText(path);
        }

        [Test]
        public void SessionFocusState_UsesNullableObservedStateAndPollingCorrection()
        {
            string source = ReadLauncherSource();

            StringAssert.Contains("private bool? m_LastObservedXrFocused;", source);
            StringAssert.Contains("m_LastObservedXrFocused.HasValue", source);
            StringAssert.Contains("m_LastObservedXrFocused.Value == focused", source);
            StringAssert.Contains("private IEnumerator PollPicoFocusState()", source);
            StringAssert.Contains("m_FocusPollIntervalSeconds = 0.1f;", source);
            StringAssert.Contains("PXR_Plugin.System.UPxr_GetFocusState()", source);
            Assert.IsFalse(source.Contains("private bool m_SessionWasFocused;"));
        }

        [Test]
        public void StartupLaunch_OpensVlcLibraryWithoutUnityPermissionGate()
        {
            string source = ReadLauncherSource();

            StringAssert.Contains("private bool m_OpenVlcLibraryOnStart = true;", source);
            StringAssert.Contains("StartCoroutine(HandleStartupLaunch());", source);
            StringAssert.Contains("private IEnumerator HandleStartupLaunch()", source);
            StringAssert.Contains("private IEnumerator OpenVlcLibraryOnStart()", source);
            StringAssert.Contains("StartVLCActivity();", source);
            StringAssert.DoesNotContain("m_OpenVlcAfterPermissionGranted", source);
            StringAssert.DoesNotContain("RequestRequiredPermissions", source);
            StringAssert.DoesNotContain("Permission.RequestUserPermissions", source);
        }

        [Test]
        public void ExternalMediaLaunch_ConsumesIntentBeforeOpeningVlcLibrary()
        {
            string source = ReadLauncherSource();
            int startupStart = source.IndexOf("private IEnumerator HandleStartupLaunch()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(startupStart, 0);
            int startupEnd = source.IndexOf("private IEnumerator OpenVlcLibraryOnStart()", startupStart, System.StringComparison.Ordinal);
            Assert.Greater(startupEnd, startupStart);
            string startupBlock = source.Substring(startupStart, startupEnd - startupStart);

            StringAssert.Contains("yield return null;", startupBlock);
            StringAssert.Contains("if (TryConsumeExternalMediaIntent()) yield break;", startupBlock);
            StringAssert.Contains("if (m_ExternalMediaLaunchHandled) yield break;", startupBlock);
            StringAssert.Contains("StartCoroutine(OpenVlcLibraryOnStart());", startupBlock);
            StringAssert.Contains("Application.deepLinkActivated += OnDeepLinkActivated;", source);
            StringAssert.Contains("Application.deepLinkActivated -= OnDeepLinkActivated;", source);
            StringAssert.Contains("private bool m_ExternalMediaLaunchHandled;", source);
            StringAssert.Contains("private bool TryConsumeExternalMediaIntent()", source);
            StringAssert.Contains("m_ExternalMediaLaunchHandled = true;", source);
            StringAssert.Contains("playbackBridge.StartPlay(payload);", source);
            StringAssert.Contains("private void OnDeepLinkActivated(string url)", source);

            int focusStart = source.IndexOf("private void OnApplicationFocus(bool hasFocus)", System.StringComparison.Ordinal);
            int focusEnd = source.IndexOf("private VlcFocusRestoreHandler EnsureFocusRestoreHandler()", focusStart, System.StringComparison.Ordinal);
            Assert.Greater(focusEnd, focusStart);
            string focusBlock = source.Substring(focusStart, focusEnd - focusStart);
            StringAssert.Contains("TryConsumeExternalMediaIntent();", focusBlock);
            StringAssert.DoesNotContain("TryOpenVlcAfterPermissionGranted", focusBlock);
        }

        [Test]
        public void ExternalMediaIntentBridge_BuildsStartPlayPayloadAndMarksIntentConsumed()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Infrastructure/VlcBridge/Launcher/ExternalMediaIntentBridge.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("private const string ActionView = \"android.intent.action.VIEW\";", source);
            StringAssert.Contains("private const string ExtraExternalMedia = \"org.videolan.vlc.extra.XR_EXTERNAL_MEDIA\";", source);
            StringAssert.Contains("private const string ExtraExternalMediaToken = \"org.videolan.vlc.extra.XR_EXTERNAL_MEDIA_TOKEN\";", source);
            StringAssert.Contains("private const string ExtraExternalMediaConsumed = \"org.videolan.vlc.extra.XR_EXTERNAL_MEDIA_CONSUMED\";", source);
            StringAssert.Contains("getIntent", source);
            StringAssert.Contains("getDataString", source);
            StringAssert.Contains("getBooleanExtra\", ExtraExternalMedia, false", source);
            StringAssert.Contains("putExtra\", ExtraExternalMediaConsumed, true", source);
            StringAssert.Contains("JsonUtility.ToJson", source);
            StringAssert.Contains("public string uri;", source);
            StringAssert.Contains("public int index;", source);
        }

        [Test]
        public void StartupLaunch_WaitsInUnityAfterXrBecomesVisible()
        {
            string launcherSource = ReadLauncherSource();

            StringAssert.Contains("while (!ColdStartSplashOverlay.HasReachedMinimumVisibleTime)", launcherSource);
            StringAssert.Contains("yield return new WaitForEndOfFrame();", launcherSource);
            StringAssert.Contains("OpenVLCMediaLibrary();", launcherSource);
            StringAssert.DoesNotContain("ExtraDeferVlcForegroundMs", launcherSource);
            StringAssert.DoesNotContain("deferForegroundUntilColdStartSplashElapsed", launcherSource);
        }

        [Test]
        public void VlcStartActivity_StartsMainActivityWithoutForegroundDelay()
        {
            string path = Path.Combine(Application.dataPath, "..", "..", "vlc-android/application/vlc-android/src/org/videolan/vlc/StartActivity.kt");
            string source = File.ReadAllText(path);

            StringAssert.Contains("resume()", source);
            StringAssert.Contains("startActivity(mainIntent)", source);
            StringAssert.DoesNotContain("EXTRA_DEFER_VLC_FOREGROUND_MS", source);
            StringAssert.DoesNotContain("requestedForegroundDelayMs", source);
            StringAssert.DoesNotContain("moveTaskToBack(true)", source);
        }

        [Test]
        public void VlcMainActivity_NotifiesUnityWithoutManagingColdStartDelay()
        {
            string path = Path.Combine(Application.dataPath, "..", "..", "vlc-android/application/vlc-android/src/org/videolan/vlc/gui/MainActivity.kt");
            string source = File.ReadAllText(path);

            StringAssert.Contains("override fun onStart()", source);
            StringAssert.Contains("notifyUnityVlcActivityReady()", source);
            StringAssert.DoesNotContain("EXTRA_DEFER_VLC_FOREGROUND_MS", source);
            StringAssert.DoesNotContain("maybeDeferVlcForeground", source);
            StringAssert.DoesNotContain("bringVlcTaskToFront", source);
            StringAssert.DoesNotContain("moveTaskToBack(true)", source);
            StringAssert.DoesNotContain("mainHandler.postDelayed", source);
            StringAssert.DoesNotContain("override fun onWindowFocusChanged(hasFocus: Boolean)", source);
        }

        [Test]
        public void AndroidLaunch_RestoresExistingVlcTaskBeforeStartingFallback()
        {
            string source = ReadLauncherSource();

            StringAssert.Contains("RestoreVlcTaskOrStartFallback(currentActivity);", source);
            StringAssert.Contains("restoreVlcTask", source);
            StringAssert.Contains("if (bridge.CallStatic<bool>(\"restoreVlcTask\", currentActivity))", source);
            StringAssert.Contains("FlagActivityNewTask", source);
            StringAssert.Contains("addFlags\", FlagActivityNewTask", source);
            StringAssert.DoesNotContain("ShowLastVlcActivityOrStartFallback", source);
            StringAssert.DoesNotContain("showLastVlcActivity", source);
        }

        [Test]
        public void AndroidFocusFallback_RestoresControllerRaysWhenUnityFocusReturns()
        {
            string source = ReadLauncherSource();
            int focusStart = source.IndexOf("private void OnApplicationFocus(bool hasFocus)", System.StringComparison.Ordinal);
            int nextMethod = source.IndexOf("private VlcFocusRestoreHandler EnsureFocusRestoreHandler()", focusStart, System.StringComparison.Ordinal);
            string focusBlock = source.Substring(focusStart, nextMethod - focusStart);

            StringAssert.Contains("TryConsumeExternalMediaIntent();", focusBlock);
            StringAssert.DoesNotContain("TryOpenVlcAfterPermissionGranted", focusBlock);
            StringAssert.Contains("EnsureFocusRestoreHandler().HideControllers();", focusBlock);
            StringAssert.Contains("EnsureFocusRestoreHandler().TriggerRestore();", focusBlock);
        }

        [Test]
        public void HomePanelRequiresCurrentPlaylistItemToBeVideo()
        {
            string launcherSource = ReadLauncherSource();
            string bridgePath = Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt");
            string bridgeSource = File.ReadAllText(bridgePath);

            StringAssert.Contains("val currentIndex = playlistManager.currentIndex", bridgeSource);
            StringAssert.Contains("val currentMedia = if (currentIndex >= 0)", bridgeSource);
            StringAssert.Contains("playlistManager.getMedia(currentIndex)", bridgeSource);
            StringAssert.Contains("service.mediaListSize > 0", bridgeSource);
            StringAssert.Contains("currentIndex != -1", bridgeSource);
            StringAssert.Contains("currentMedia?.type == MediaWrapper.TYPE_VIDEO", bridgeSource);

            StringAssert.Contains(
                "VlcPlaybackBridge.TryHasActivePlaybackSelection(out bool hasActiveVideoSelection)",
                launcherSource);
            StringAssert.Contains("homePanel.SetVisible(true)", launcherSource);
            StringAssert.Contains("homePanel.SetVisible(false)", launcherSource);
            StringAssert.Contains("Playback state remained unknown; Home panel stays hidden.", launcherSource);
            StringAssert.DoesNotContain("m_PlaybackActiveOrStarting", launcherSource);
        }

        [Test]
        public void SubtitlePicker_FallsBackToInternalStorageWhenMediaHasNoBrowsableParent()
        {
            string pickerPath = Path.Combine(Application.dataPath, "..", "..", "vlc-android/application/vlc-android/src/org/videolan/vlc/gui/browser/FilePickerFragment.kt");
            string pickerSource = File.ReadAllText(pickerPath);

            StringAssert.Contains("import org.videolan.resources.KEY_MRL", pickerSource);
            StringAssert.Contains("private fun applySubtitleFallbackToInternalStorage()", pickerSource);
            StringAssert.Contains("if (pickerType != PickerType.SUBTITLE) return", pickerSource);
            StringAssert.Contains("bundle.remove(KEY_MEDIA)", pickerSource);
            StringAssert.Contains("bundle.putString(KEY_MRL, internalStorageMrl())", pickerSource);
            StringAssert.Contains("private fun shouldFallbackToInternalStorage(media: MediaWrapper?): Boolean", pickerSource);
            StringAssert.Contains("scheme == \"content\"", pickerSource);
            StringAssert.Contains("scheme == \"fd\"", pickerSource);
            StringAssert.Contains("scheme?.startsWith(\"http\") == true", pickerSource);
            StringAssert.Contains("private fun internalStorageMrl() = \"file://${AndroidDevices.EXTERNAL_PUBLIC_DIRECTORY}\"", pickerSource);
            StringAssert.DoesNotContain("activity?.intent = null", pickerSource);

            string bridgePath = Path.Combine(Application.dataPath, "..", "..", "vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt");
            string bridgeSource = File.ReadAllText(bridgePath);
            StringAssert.Contains("import org.videolan.resources.AndroidDevices", bridgeSource);
            StringAssert.Contains("intent.putExtra(KEY_MEDIA, createSubtitlePickerParentMedia())", bridgeSource);
            StringAssert.Contains("private fun createSubtitlePickerParentMedia(): MediaWrapper", bridgeSource);
            StringAssert.Contains("return createInternalStorageMediaWrapper()", bridgeSource);
            StringAssert.Contains("private fun createInternalStorageMediaWrapper() = MediaWrapperImpl(\"file://${AndroidDevices.EXTERNAL_PUBLIC_DIRECTORY}\".toUri())", bridgeSource);

            string overlayPath = Path.Combine(Application.dataPath, "..", "..", "vlc-android/application/vlc-android/src/org/videolan/vlc/gui/video/VideoPlayerOverlayDelegate.kt");
            string overlaySource = File.ReadAllText(overlayPath);
            StringAssert.Contains("private fun createSubtitlePickerParentMedia(): MediaWrapper", overlaySource);
            StringAssert.Contains("val fallback = \"file://${AndroidDevices.EXTERNAL_PUBLIC_DIRECTORY}\"", overlaySource);
            StringAssert.Contains("MediaWrapperImpl((parent ?: fallback).toUri())", overlaySource);
        }
    }
}
