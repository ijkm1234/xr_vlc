using System;
using System.Collections.Generic;
using XRVLC.Media;

public static class VlcPlaybackEvents
{
    public static VlcPlaybackSnapshot Snapshot => VlcPlaybackBridge.Snapshot;

    public static event Action<VlcVideoSize> OnVideoSizeChanged
    {
        add => VlcPlaybackBridge.OnVideoSizeChangedEvent += value;
        remove => VlcPlaybackBridge.OnVideoSizeChangedEvent -= value;
    }

    public static event Action<string> OnStateChanged
    {
        add => VlcPlaybackBridge.OnStateChangedEvent += value;
        remove => VlcPlaybackBridge.OnStateChangedEvent -= value;
    }

    public static event Action<long> OnTimeChanged
    {
        add => VlcPlaybackBridge.OnTimeChangedEvent += value;
        remove => VlcPlaybackBridge.OnTimeChangedEvent -= value;
    }

    public static event Action<long> OnLengthChanged
    {
        add => VlcPlaybackBridge.OnLengthChangedEvent += value;
        remove => VlcPlaybackBridge.OnLengthChangedEvent -= value;
    }

    public static event Action<float> OnPlaybackRateChanged
    {
        add => VlcPlaybackBridge.OnPlaybackRateChangedEvent += value;
        remove => VlcPlaybackBridge.OnPlaybackRateChangedEvent -= value;
    }

    public static event Action<float> OnBuffering
    {
        add => VlcPlaybackBridge.OnBufferingEvent += value;
        remove => VlcPlaybackBridge.OnBufferingEvent -= value;
    }

    public static event Action OnClearPlaybackSurface
    {
        add => VlcPlaybackBridge.ClearPlaybackSurfaceEvent += value;
        remove => VlcPlaybackBridge.ClearPlaybackSurfaceEvent -= value;
    }

    public static event Action<List<TrackInfo>> OnAudioTracksChanged
    {
        add => VlcPlaybackBridge.OnAudioTracksChangedEvent += value;
        remove => VlcPlaybackBridge.OnAudioTracksChangedEvent -= value;
    }

    public static event Action<List<TrackInfo>> OnSubtitleTracksChanged
    {
        add => VlcPlaybackBridge.OnSubtitleTracksChangedEvent += value;
        remove => VlcPlaybackBridge.OnSubtitleTracksChangedEvent -= value;
    }

    public static event Action<MediaWrapper> OnPlayRequested
    {
        add => VlcPlaybackBridge.OnPlayRequestedEvent += value;
        remove => VlcPlaybackBridge.OnPlayRequestedEvent -= value;
    }

    public static event Action<VlcMediaParseResult> OnMediaParseFinished
    {
        add => VlcPlaybackBridge.OnMediaParseFinishedEvent += value;
        remove => VlcPlaybackBridge.OnMediaParseFinishedEvent -= value;
    }
}
