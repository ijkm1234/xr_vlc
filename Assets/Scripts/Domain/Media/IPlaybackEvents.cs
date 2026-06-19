using System;
using System.Collections.Generic;

namespace XRVLC.Media
{
    public interface IPlaybackEvents
    {
        event Action<PlayerStatus> OnStatusChanged;
        event Action<string> OnError;

        event Action OnPlaylistUpdated;
        event Action<MediaWrapper, int> OnMediaChanged;
        event Action<RepeatMode, bool> OnPlaybackModeChanged;

        event Action<long, long> OnTimeChanged;
        event Action<float> OnBuffering;

        event Action<List<TrackInfo>> OnAudioTracksChanged;
        event Action<List<TrackInfo>> OnSubtitleTracksChanged;
        event Action<SubtitleCue> OnSubtitleCue;
    }
}
