using System.Collections.Generic;
using NUnit.Framework;
using XRVLC.Media;

namespace XRVLC.Tests
{
    [TestFixture]
    public class TrackSelectionServiceTests
    {
        [Test]
        public void TryFindInitialSubtitleTrack_WhenMediaTrackDisabled_DoesNotAutoSelectInUnity()
        {
            var service = new TrackSelectionService();
            service.Reset(new MediaWrapper { SpuTrack = "-1" });
            service.UpdateSubtitleTracks(new List<TrackInfo>
            {
                Track("-1", "Disable"),
                Track("3", "Chinese"),
                Track("4", "English")
            });

            bool found = service.TryFindInitialSubtitleTrack(
                new MediaWrapper { SpuTrack = "-1" },
                SubtitleRenderMode.Spatial,
                alreadyApplied: false,
                out string trackId);

            Assert.IsFalse(found);
            Assert.AreEqual("-1", trackId);
        }

        [Test]
        public void TryFindInitialSubtitleTrack_WhenBridgeReportsDisabledSelected_DoesNotOverrideVlcSelection()
        {
            var service = new TrackSelectionService();
            service.Reset(new MediaWrapper { SpuTrack = "-1" });
            service.UpdateSubtitleTracks(new List<TrackInfo>
            {
                Track("-1", "Disable", true),
                Track("3", "Chinese")
            });

            bool found = service.TryFindInitialSubtitleTrack(
                new MediaWrapper { SpuTrack = "-1" },
                SubtitleRenderMode.Spatial,
                alreadyApplied: false,
                out string trackId);

            Assert.IsFalse(found);
            Assert.AreEqual("-1", trackId);
        }

        [Test]
        public void TryFindInitialSubtitleTrack_WhenRenderModeOff_DoesNotSelectTrack()
        {
            var service = new TrackSelectionService();
            service.UpdateSubtitleTracks(new List<TrackInfo> { Track("3", "Chinese") });

            bool found = service.TryFindInitialSubtitleTrack(
                new MediaWrapper { SpuTrack = "-1" },
                SubtitleRenderMode.Off,
                alreadyApplied: false,
                out string trackId);

            Assert.IsFalse(found);
            Assert.AreEqual("-1", trackId);
        }

        private static TrackInfo Track(string id, string name, bool selected = false)
        {
            return new TrackInfo { Id = id, Name = name, IsSelected = selected };
        }
    }
}
