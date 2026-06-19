using NUnit.Framework;
using XRVLC.Media;

namespace XRVLC.Tests
{
    [TestFixture]
    public class SubtitleCueSchedulerTests
    {
        [Test]
        public void EnqueueCue_WaitsUntilPlaybackTimeReachesStart()
        {
            var scheduler = new SubtitleCueScheduler(startToleranceMs: 0, endToleranceMs: 0);
            scheduler.SetPlaybackState(PlayerStatus.Playing);
            scheduler.SetPlaybackTime(900);

            SubtitleCue cue = TimedCue(1000, 2000, "late enough");
            SubtitleCueScheduleResult enqueueResult = scheduler.EnqueueCue(cue);
            Assert.AreEqual(SubtitleCueScheduleAction.None, enqueueResult.Action);

            SubtitleCueScheduleResult dueResult = scheduler.SetPlaybackTime(1000);
            Assert.AreEqual(SubtitleCueScheduleAction.Show, dueResult.Action);
            Assert.AreSame(cue, dueResult.Cue);
        }

        [Test]
        public void PlaybackPause_SuspendsHideUntilResume()
        {
            var scheduler = new SubtitleCueScheduler(startToleranceMs: 0, endToleranceMs: 0);
            scheduler.SetPlaybackState(PlayerStatus.Playing);
            scheduler.SetPlaybackTime(1000);

            SubtitleCue cue = TimedCue(1000, 2000, "pause me");
            Assert.AreEqual(SubtitleCueScheduleAction.Show, scheduler.EnqueueCue(cue).Action);

            scheduler.SetPlaybackState(PlayerStatus.Paused);
            Assert.AreEqual(SubtitleCueScheduleAction.None, scheduler.SetPlaybackTime(3000).Action);
            Assert.AreSame(cue, scheduler.ActiveCue);

            SubtitleCueScheduleResult resumeResult = scheduler.SetPlaybackState(PlayerStatus.Playing);
            Assert.AreEqual(SubtitleCueScheduleAction.Hide, resumeResult.Action);
        }

        [Test]
        public void ClearCue_HidesImmediately()
        {
            var scheduler = new SubtitleCueScheduler();
            scheduler.SetPlaybackState(PlayerStatus.Playing);
            scheduler.SetPlaybackTime(1000);
            scheduler.EnqueueCue(TimedCue(1000, 2000, "visible"));

            SubtitleCueScheduleResult result = scheduler.EnqueueCue(SubtitleCue.Clear());
            Assert.AreEqual(SubtitleCueScheduleAction.Hide, result.Action);
        }

        [Test]
        public void UntimedCue_ShowsImmediately()
        {
            var scheduler = new SubtitleCueScheduler();
            scheduler.SetPlaybackState(PlayerStatus.Playing);

            SubtitleCueScheduleResult result = scheduler.EnqueueCue(TimedCue(0, 0, "fallback"));
            Assert.AreEqual(SubtitleCueScheduleAction.Show, result.Action);
        }

        private static SubtitleCue TimedCue(long startMs, long endMs, string text)
        {
            return new SubtitleCue
            {
                startMs = startMs,
                endMs = endMs,
                text = text,
                source = "text"
            };
        }
    }
}
