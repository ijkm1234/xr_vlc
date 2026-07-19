using NUnit.Framework;
using XRVLC;
using XRVLC.Media;

namespace XRVLC.Tests
{
    [TestFixture]
    public class ProjectionDetectorTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static MediaWrapper Media(string uri,
            MediaProjectionType projection = MediaProjectionType.Flat2D,
            string stereoHint = null)
            => new MediaWrapper { Uri = uri, Projection = projection, StereoHint = stereoHint };

        // ── Fallback ─────────────────────────────────────────────────────────

        [Test]
        public void Detect_NullMedia_ReturnsFlatMono()
        {
            var (proj, stereo) = ProjectionDetector.Detect(null);
            Assert.AreEqual(VideoProjection.Flat, proj);
            Assert.AreEqual(StereoMode.Mono, stereo);
        }

        [Test]
        public void Detect_NoHints_ReturnsFlatMono()
        {
            var (proj, stereo) = ProjectionDetector.Detect(Media("movie.mp4"));
            Assert.AreEqual(VideoProjection.Flat, proj);
            Assert.AreEqual(StereoMode.Mono, stereo);
        }

        // ── URI heuristics: projection ────────────────────────────────────────

        [Test]
        public void Detect_360InFilename_ReturnsSphere360()
        {
            var (proj, stereo) = ProjectionDetector.Detect(Media("/sdcard/Movies/video_360.mp4"));
            Assert.AreEqual(VideoProjection.Sphere360, proj);
            Assert.AreEqual(StereoMode.Mono, stereo);
        }

        [Test]
        public void Detect_360InFilenameUpperCase_ReturnsSphere360()
        {
            var (proj, _) = ProjectionDetector.Detect(Media("/sdcard/Movies/VIDEO_360.MP4"));
            Assert.AreEqual(VideoProjection.Sphere360, proj);
        }

        [Test]
        public void Detect_180InFilename_ReturnsSphere180()
        {
            var (proj, stereo) = ProjectionDetector.Detect(Media("/sdcard/180_video.mp4"));
            Assert.AreEqual(VideoProjection.Sphere180, proj);
            Assert.AreEqual(StereoMode.Mono, stereo);
        }

        [Test]
        public void Detect_FisheyeInFilename_ReturnsFisheye180()
        {
            var (proj, stereo) = ProjectionDetector.Detect(Media("/sdcard/Movies/concert_fisheye_lr.mp4"));
            Assert.AreEqual(VideoProjection.Fisheye180, proj);
            Assert.AreEqual(StereoMode.LeftRight, stereo);
        }

        [Test]
        public void Detect_EquirectInFilename_ReturnsSphere360()
        {
            var (proj, _) = ProjectionDetector.Detect(Media("equirectangular_clip.mp4"));
            Assert.AreEqual(VideoProjection.Sphere360, proj);
        }

        [Test]
        public void Detect_CylinderInFilename_ReturnsCylinder()
        {
            var (proj, _) = ProjectionDetector.Detect(Media("museum_cylinder.mp4"));
            Assert.AreEqual(VideoProjection.Cylinder, proj);
        }

        [Test]
        public void Detect_3D180InFilename_ReturnsSphere180()
        {
            var (proj, _) = ProjectionDetector.Detect(Media("FantasyJourney_3D180_LR.mp4"));
            Assert.AreEqual(VideoProjection.Sphere180, proj);
        }

        [Test]
        public void Detect_3D360InFilename_ReturnsSphere360()
        {
            var (proj, _) = ProjectionDetector.Detect(Media("Adventure_3D360_SBS.mp4"));
            Assert.AreEqual(VideoProjection.Sphere360, proj);
        }

        [Test]
        public void Detect_VR180InFilename_ReturnsSphere180()
        {
            var (proj, _) = ProjectionDetector.Detect(Media("clip_vr180.mp4"));
            Assert.AreEqual(VideoProjection.Sphere180, proj);
        }

        [Test]
        public void Detect_3D180LrCombined_ReturnsSphere180LeftRight()
        {
            var (proj, stereo) = ProjectionDetector.Detect(Media("FantasyJourney_3D180_LR.mp4"));
            Assert.AreEqual(VideoProjection.Sphere180, proj);
            Assert.AreEqual(StereoMode.LeftRight, stereo);
        }

        // ── URI heuristics: stereo ────────────────────────────────────────────

        [Test]
        public void Detect_SbsInFilename_ReturnsLeftRight()
        {
            var (_, stereo) = ProjectionDetector.Detect(Media("clip_sbs.mp4"));
            Assert.AreEqual(StereoMode.LeftRight, stereo);
        }

        [Test]
        public void Detect_LrInFilename_ReturnsLeftRight()
        {
            var (_, stereo) = ProjectionDetector.Detect(Media("clip_lr.mp4"));
            Assert.AreEqual(StereoMode.LeftRight, stereo);
        }

        [Test]
        public void Detect_TbInFilename_ReturnsTopBottom()
        {
            var (_, stereo) = ProjectionDetector.Detect(Media("clip_tb.mp4"));
            Assert.AreEqual(StereoMode.TopBottom, stereo);
        }

        [Test]
        public void Detect_OuInFilename_ReturnsTopBottom()
        {
            var (_, stereo) = ProjectionDetector.Detect(Media("clip_ou.mp4"));
            Assert.AreEqual(StereoMode.TopBottom, stereo);
        }

        [Test]
        public void Detect_360SbsCombined_ReturnsSphere360LeftRight()
        {
            var (proj, stereo) = ProjectionDetector.Detect(Media("travel_360_sbs.mp4"));
            Assert.AreEqual(VideoProjection.Sphere360, proj);
            Assert.AreEqual(StereoMode.LeftRight, stereo);
        }

        // ── Explicit metadata priority ────────────────────────────────────────

        [Test]
        public void Detect_ExplicitSphere360Metadata_ReturnsSphere360()
        {
            var (proj, stereo) = ProjectionDetector.Detect(Media("film.mp4", MediaProjectionType.Sphere360));
            Assert.AreEqual(VideoProjection.Sphere360, proj);
            Assert.AreEqual(StereoMode.Mono, stereo);
        }

        [Test]
        public void Detect_ExplicitSphere180Metadata_ReturnsSphere180()
        {
            var (proj, _) = ProjectionDetector.Detect(Media("film.mp4", MediaProjectionType.Sphere180));
            Assert.AreEqual(VideoProjection.Sphere180, proj);
        }

        [Test]
        public void Detect_ExplicitStereoHintLr_ReturnsLeftRight()
        {
            var (_, stereo) = ProjectionDetector.Detect(Media("film.mp4", MediaProjectionType.Sphere360, "lr"));
            Assert.AreEqual(StereoMode.LeftRight, stereo);
        }

        [Test]
        public void Detect_ExplicitStereoHintTb_ReturnsTopBottom()
        {
            var (_, stereo) = ProjectionDetector.Detect(Media("film.mp4", MediaProjectionType.Sphere360, "tb"));
            Assert.AreEqual(StereoMode.TopBottom, stereo);
        }

        [Test]
        public void Detect_ExplicitMetadataOverridesUri_ReturnsSphere360()
        {
            // URI suggests Flat (no keywords), but metadata says 360
            var (proj, _) = ProjectionDetector.Detect(Media("film.mp4", MediaProjectionType.Sphere360));
            Assert.AreEqual(VideoProjection.Sphere360, proj);
        }
    }
}
