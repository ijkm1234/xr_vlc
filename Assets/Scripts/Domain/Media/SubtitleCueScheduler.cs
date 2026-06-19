namespace XRVLC.Media
{
    public enum SubtitleCueScheduleAction
    {
        None,
        Show,
        Hide
    }

    public readonly struct SubtitleCueScheduleResult
    {
        public SubtitleCueScheduleResult(SubtitleCueScheduleAction action, SubtitleCue cue)
        {
            Action = action;
            Cue = cue;
        }

        public SubtitleCueScheduleAction Action { get; }
        public SubtitleCue Cue { get; }

        public static SubtitleCueScheduleResult None => new SubtitleCueScheduleResult(SubtitleCueScheduleAction.None, null);
        public static SubtitleCueScheduleResult Show(SubtitleCue cue) => new SubtitleCueScheduleResult(SubtitleCueScheduleAction.Show, cue);
        public static SubtitleCueScheduleResult Hide => new SubtitleCueScheduleResult(SubtitleCueScheduleAction.Hide, null);
    }

    public sealed class SubtitleCueScheduler
    {
        public const long DefaultStartToleranceMs = 0;
        public const long DefaultEndToleranceMs = 40;

        private readonly long _startToleranceMs;
        private readonly long _endToleranceMs;
        private SubtitleCue _pendingCue;
        private SubtitleCue _activeCue;
        private long _playbackTimeMs;
        private bool _isPaused;

        public SubtitleCueScheduler()
            : this(DefaultStartToleranceMs, DefaultEndToleranceMs)
        {
        }

        public SubtitleCueScheduler(long startToleranceMs, long endToleranceMs)
        {
            _startToleranceMs = startToleranceMs < 0 ? 0 : startToleranceMs;
            _endToleranceMs = endToleranceMs < 0 ? 0 : endToleranceMs;
        }

        public bool IsPaused => _isPaused;
        public SubtitleCue ActiveCue => _activeCue;

        public SubtitleCueScheduleResult SetPlaybackTime(long playbackTimeMs)
        {
            _playbackTimeMs = playbackTimeMs < 0 ? 0 : playbackTimeMs;
            return Evaluate();
        }

        public SubtitleCueScheduleResult SetPlaybackState(PlayerStatus status)
        {
            if (IsTerminalState(status))
            {
                _isPaused = false;
                Reset();
                return SubtitleCueScheduleResult.Hide;
            }

            _isPaused = status == PlayerStatus.Paused;
            return _isPaused ? SubtitleCueScheduleResult.None : Evaluate();
        }

        public SubtitleCueScheduleResult EnqueueCue(SubtitleCue cue)
        {
            if (!IsDisplayable(cue))
            {
                Reset();
                return SubtitleCueScheduleResult.Hide;
            }

            if (!HasTiming(cue))
            {
                _pendingCue = null;
                _activeCue = cue;
                return SubtitleCueScheduleResult.Show(cue);
            }

            _pendingCue = cue;
            return Evaluate();
        }

        public void Reset()
        {
            _pendingCue = null;
            _activeCue = null;
        }

        public static bool HasTiming(SubtitleCue cue)
        {
            return cue != null && cue.endMs > cue.startMs;
        }

        private SubtitleCueScheduleResult Evaluate()
        {
            if (_isPaused)
                return SubtitleCueScheduleResult.None;

            if (_pendingCue != null)
            {
                if (_playbackTimeMs >= _pendingCue.startMs - _startToleranceMs)
                {
                    if (_playbackTimeMs <= _pendingCue.endMs + _endToleranceMs)
                    {
                        _activeCue = _pendingCue;
                        _pendingCue = null;
                        return SubtitleCueScheduleResult.Show(_activeCue);
                    }

                    _pendingCue = null;
                }
            }

            if (_activeCue != null && HasTiming(_activeCue) && IsOutsideActiveWindow(_activeCue))
            {
                _activeCue = null;
                return SubtitleCueScheduleResult.Hide;
            }

            return SubtitleCueScheduleResult.None;
        }

        private bool IsOutsideActiveWindow(SubtitleCue cue)
        {
            return _playbackTimeMs < cue.startMs - _startToleranceMs
                || _playbackTimeMs > cue.endMs + _endToleranceMs;
        }

        private static bool IsDisplayable(SubtitleCue cue)
        {
            return cue != null && !cue.IsClear && !string.IsNullOrEmpty(cue.text);
        }

        private static bool IsTerminalState(PlayerStatus status)
        {
            return status == PlayerStatus.Stopped
                || status == PlayerStatus.Ended
                || status == PlayerStatus.Error
                || status == PlayerStatus.Idle;
        }
    }
}
