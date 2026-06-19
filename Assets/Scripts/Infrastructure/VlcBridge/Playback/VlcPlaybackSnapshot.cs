using System.Collections.Generic;
using XRVLC.Media;

public class VlcPlaybackSnapshot
{
    public PlayerStatus Status { get; set; } = PlayerStatus.Idle;
    public MediaWrapper CurrentMedia { get; set; }
    public long TimeMs { get; set; }
    public long LengthMs { get; set; }
    public float Buffering { get; set; }
    public SubtitleCue CurrentSubtitleCue { get; private set; } = SubtitleCue.Clear();
    public List<TrackInfo> AudioTracks { get; } = new List<TrackInfo>();
    public List<TrackInfo> SubtitleTracks { get; } = new List<TrackInfo>();

    /// <summary>
    /// 将 VLC 回调中的字符串状态归一化为 Unity 领域播放状态。
    /// </summary>
    public void SetStatusFromBridge(string state)
    {
        Status = state switch
        {
            "Playing" => PlayerStatus.Playing,
            "Paused" => PlayerStatus.Paused,
            "Stopped" => PlayerStatus.Stopped,
            "Ended" => PlayerStatus.Ended,
            "Error" => PlayerStatus.Error,
            _ => Status
        };
    }

    /// <summary>
    /// 替换音轨快照内容，避免外部持有并修改内部列表引用。
    /// </summary>
    public void SetAudioTracks(List<TrackInfo> tracks)
    {
        AudioTracks.Clear();
        if (tracks != null) AudioTracks.AddRange(tracks);
    }

    /// <summary>
    /// 替换字幕轨快照内容，避免外部持有并修改内部列表引用。
    /// </summary>
    public void SetSubtitleTracks(List<TrackInfo> tracks)
    {
        SubtitleTracks.Clear();
        if (tracks != null) SubtitleTracks.AddRange(tracks);
    }

    public void SetSubtitleCue(SubtitleCue cue)
    {
        CurrentSubtitleCue = cue ?? SubtitleCue.Clear();
    }
}
