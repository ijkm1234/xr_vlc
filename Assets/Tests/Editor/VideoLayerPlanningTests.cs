using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class VideoLayerPlanningTests
    {
        [Test]
        public void SameSpecNewMedia_KeepsInputAndOutputLayers()
        {
            VideoLayerSpec current = Create(VideoProjection.Flat);

            VideoLayerDelta delta = VideoLayerPlanner.Classify(current, true, true, Create(VideoProjection.Flat));

            Assert.IsFalse(delta.RebuildInput);
            Assert.IsFalse(delta.RebuildOutput);
            Assert.IsFalse(delta.MapperChanged);
        }

        [TestCase(1920u, 1080u, true, true, false)]
        [TestCase(3840u, 2160u, true, true, true)]
        [TestCase(1920u, 1080u, false, true, true)]
        [TestCase(1920u, 1080u, true, false, true)]
        public void InputDelta_UsesContentSizeDecodeModeAndSurfaceValidity(
            uint width,
            uint height,
            bool hardwareDecoding,
            bool inputSurfaceValid,
            bool expectedRebuild)
        {
            VideoLayerSpec current = Create(VideoProjection.Flat);
            VideoLayerSpec target = VideoLayerPlanner.Create(
                width,
                height,
                hardwareDecoding,
                VideoProjection.Flat,
                StereoMode.Mono,
                FlatVideoCurveMode.None,
                false);

            VideoLayerDelta delta = VideoLayerPlanner.Classify(current, inputSurfaceValid, true, target);

            Assert.AreEqual(expectedRebuild, delta.RebuildInput);
        }

        [Test]
        public void ProjectionAndCurveChanges_AreClassifiedAtTheCorrectLayer()
        {
            VideoLayerSpec sphere180 = Create(VideoProjection.Sphere180);
            VideoLayerDelta sphere360 = VideoLayerPlanner.Classify(
                sphere180,
                true,
                true,
                Create(VideoProjection.Sphere360));
            VideoLayerDelta fisheye180 = VideoLayerPlanner.Classify(
                sphere180,
                true,
                true,
                Create(VideoProjection.Fisheye180));
            VideoLayerDelta cylinder = VideoLayerPlanner.Classify(
                Create(VideoProjection.Flat),
                true,
                true,
                Create(VideoProjection.Cylinder));
            VideoLayerDelta curve = VideoLayerPlanner.Classify(
                Create(VideoProjection.Cylinder, curve: FlatVideoCurveMode.Small),
                true,
                true,
                Create(VideoProjection.Cylinder, curve: FlatVideoCurveMode.Large));

            Assert.IsFalse(sphere360.RebuildInput);
            Assert.IsFalse(sphere360.RebuildOutput, "180/360 share the Equirect output layer.");
            Assert.IsFalse(sphere360.MapperChanged);
            Assert.IsFalse(fisheye180.RebuildOutput, "Sphere180/Fisheye180 share the Equirect output layer.");
            Assert.IsTrue(fisheye180.MapperChanged);
            Assert.IsTrue(cylinder.RebuildOutput, "Quad/Cylinder changes recreate the PICO output layer.");
            Assert.IsFalse(curve.RebuildOutput, "Cylinder curvature updates radius only.");
            Assert.IsFalse(curve.MapperChanged);
        }

        [Test]
        public void StereoAndChromaChanges_RebuildOutputWithoutRebuildingInput()
        {
            VideoLayerSpec current = Create(VideoProjection.Flat);
            VideoLayerSpec stereo = VideoLayerPlanner.Create(
                1920,
                1080,
                true,
                VideoProjection.Flat,
                StereoMode.LeftRight,
                FlatVideoCurveMode.None,
                false);
            VideoLayerSpec chroma = Create(VideoProjection.Flat, chroma: true);

            VideoLayerDelta stereoDelta = VideoLayerPlanner.Classify(current, true, true, stereo);
            VideoLayerDelta chromaDelta = VideoLayerPlanner.Classify(current, true, true, chroma);

            Assert.IsFalse(stereoDelta.RebuildInput);
            Assert.IsTrue(stereoDelta.RebuildOutput);
            Assert.IsTrue(stereoDelta.MapperChanged);
            Assert.IsFalse(chromaDelta.RebuildInput);
            Assert.IsTrue(chromaDelta.RebuildOutput);
            Assert.IsTrue(chromaDelta.MapperChanged);
        }

        [Test]
        public void AndroidTransaction_StartsMediaSeriallyAndReadiesOnlyAfterMatchingOpening()
        {
            string bridge = ReadSibling(
                "vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt");

            StringAssert.Contains("private suspend fun attachVideoLayerOnMain", bridge);
            StringAssert.Contains("private suspend fun startPendingMediaForVideoOutput", bridge);
            StringAssert.Contains("phase = VideoOutputSwitchPhase.StartingMedia", bridge);
            StringAssert.Contains("phase = VideoOutputSwitchPhase.AwaitMediaOpening", bridge);
            StringAssert.Contains("openingObservedWhileStarting", bridge);
            StringAssert.Contains("opening_ignored_while_starting", bridge);
            StringAssert.Contains("completeRebuildLayerOnOpening()", bridge);
            StringAssert.Contains("sameMediaUri(active.targetMediaUri, currentUri)", bridge);
            StringAssert.Contains("pm.playIndex", bridge);
            StringAssert.Contains("pm.load", bridge);
            StringAssert.DoesNotContain("fallback_direct", bridge);
        }

        [Test]
        public void AndroidTransaction_DoesNotDetachVoutForSameInputSpec()
        {
            string bridge = ReadSibling(
                "vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt");
            int keepInput = bridge.IndexOf("if (!next.rebuildInput) {", StringComparison.Ordinal);
            int detachViews = bridge.IndexOf("if (vout.areViewsAttached()) vout.detachViews()", StringComparison.Ordinal);

            Assert.GreaterOrEqual(keepInput, 0);
            Assert.Greater(detachViews, keepInput);
            StringAssert.Contains("mapperInput !== boundVideoInputSurface", bridge);
            StringAssert.Contains("vout?.areViewsAttached() != true", bridge);
            StringAssert.Contains("input-rebuild-required", bridge);
            StringAssert.Contains("surfaceMapper?.releaseInputLayer()", bridge);
        }

        [Test]
        public void MapperOnlyChange_DoesNotTouchSubtitleSurface()
        {
            string playback = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs"));
            int changeStart = playback.IndexOf("private IEnumerator ChangeLayer", StringComparison.Ordinal);
            int changeEnd = playback.IndexOf("private IEnumerator WaitForPausedBeforeSurfaceRebuild", changeStart, StringComparison.Ordinal);
            string change = playback.Substring(changeStart, changeEnd - changeStart);

            StringAssert.Contains("if (rebuildOutput)", change);
            Assert.Greater(
                change.IndexOf("BeginNativeSubtitleSurfaceRebuild", StringComparison.Ordinal),
                change.IndexOf("if (rebuildOutput)", StringComparison.Ordinal));
        }

        private static VideoLayerSpec Create(
            VideoProjection projection,
            FlatVideoCurveMode curve = FlatVideoCurveMode.None,
            bool chroma = false)
        {
            return VideoLayerPlanner.Create(
                1920,
                1080,
                true,
                projection,
                StereoMode.Mono,
                curve,
                chroma);
        }

        private static string ReadSibling(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string workspaceRoot = Directory.GetParent(projectRoot).FullName;
            return File.ReadAllText(Path.Combine(workspaceRoot, relativePath));
        }
    }
}
