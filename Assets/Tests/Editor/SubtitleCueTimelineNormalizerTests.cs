using NUnit.Framework;
using XRVLC.Media;

namespace XRVLC.Tests
{
    [TestFixture]
    public class SubtitleCueTimelineNormalizerTests
    {
        [Test]
        public void Normalize_WhenCueStartsBeyondMediaLength_ProjectsToCurrentPlaybackTime()
        {
            SubtitleCue cue = TimedCue(3027854, 3030154, "project me");

            SubtitleCue normalized = SubtitleCueTimelineNormalizer.Normalize(
                cue,
                playbackTimeMs: 523842,
                mediaLengthMs: 1421090,
                maxLeadMs: 60000,
                mediaLengthSlackMs: 10000,
                maxDurationMs: 10000,
                out bool changed);

            Assert.IsTrue(changed);
            Assert.AreEqual(523842, normalized.startMs);
            Assert.AreEqual(526142, normalized.endMs);
            Assert.AreEqual(cue.text, normalized.text);
        }

        [Test]
        public void Normalize_WhenCueIsAlreadyOnMediaTimeline_ReturnsOriginalCue()
        {
            SubtitleCue cue = TimedCue(523842, 526142, "keep me");

            SubtitleCue normalized = SubtitleCueTimelineNormalizer.Normalize(
                cue,
                playbackTimeMs: 523000,
                mediaLengthMs: 1421090,
                maxLeadMs: 60000,
                mediaLengthSlackMs: 10000,
                maxDurationMs: 10000,
                out bool changed);

            Assert.IsFalse(changed);
            Assert.AreSame(cue, normalized);
        }

        private static SubtitleCue TimedCue(long startMs, long endMs, string text)
        {
            return new SubtitleCue
            {
                version = 2,
                seq = 1,
                trackId = "spu:-1",
                startMs = startMs,
                endMs = endMs,
                text = text,
                source = "text"
            };
        }
    }
}
