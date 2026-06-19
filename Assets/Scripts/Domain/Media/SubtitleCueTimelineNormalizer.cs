namespace XRVLC.Media
{
    public static class SubtitleCueTimelineNormalizer
    {
        private const long MinimumDurationMs = 250;

        public static SubtitleCue Normalize(
            SubtitleCue cue,
            long playbackTimeMs,
            long mediaLengthMs,
            long maxLeadMs,
            long mediaLengthSlackMs,
            long maxDurationMs,
            out bool changed)
        {
            changed = false;

            if (cue == null || !SubtitleCueScheduler.HasTiming(cue))
                return cue;

            long safePlaybackTimeMs = playbackTimeMs < 0 ? 0 : playbackTimeMs;
            long safeMaxLeadMs = maxLeadMs < 0 ? 0 : maxLeadMs;
            long safeMediaLengthSlackMs = mediaLengthSlackMs < 0 ? 0 : mediaLengthSlackMs;

            bool startsBeyondMediaLength = mediaLengthMs > 0 && cue.startMs > mediaLengthMs + safeMediaLengthSlackMs;
            bool startsTooFarAhead = cue.startMs - safePlaybackTimeMs > safeMaxLeadMs;

            if (!startsBeyondMediaLength && !startsTooFarAhead)
                return cue;

            long durationMs = cue.endMs - cue.startMs;
            durationMs = ClampDuration(durationMs, maxDurationMs);

            changed = true;
            return new SubtitleCue
            {
                version = cue.version,
                seq = cue.seq,
                trackId = cue.trackId,
                startMs = safePlaybackTimeMs,
                endMs = safePlaybackTimeMs + durationMs,
                text = cue.text,
                segments = cue.segments,
                styleRuns = cue.styleRuns,
                fontAttachments = cue.fontAttachments,
                layout = cue.layout,
                align = cue.align,
                source = cue.source
            };
        }

        private static long ClampDuration(long durationMs, long maxDurationMs)
        {
            long safeMaxDurationMs = maxDurationMs < MinimumDurationMs ? MinimumDurationMs : maxDurationMs;

            if (durationMs < MinimumDurationMs)
                return MinimumDurationMs;

            return durationMs > safeMaxDurationMs ? safeMaxDurationMs : durationMs;
        }
    }
}
