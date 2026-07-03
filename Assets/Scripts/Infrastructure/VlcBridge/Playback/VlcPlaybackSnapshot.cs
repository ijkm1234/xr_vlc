using XRVLC.Media;

public class VlcPlaybackSnapshot
{
    public PlayerStatus Status { get; set; } = PlayerStatus.Idle;
    public MediaWrapper CurrentMedia { get; set; }
    public long TimeMs { get; set; }
    public long LengthMs { get; set; }
    public float PlaybackRate { get; set; } = 1f;
    public float Buffering { get; set; }

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
}
