namespace XRVLC.Media
{
    public enum PlayerStatus
    {
        Idle,
        Opening,
        Buffering,
        Playing,
        Paused,
        Stopped,
        Ended,
        Error
    }

    public enum RepeatMode
    {
        None,
        All,
        Single
    }

    public enum MediaProjectionType
    {
        Flat2D,
        Sphere180,
        Sphere360
    }

    public enum SubtitleRenderMode
    {
        Native = 0,
        Spatial = 1,
        Off = 2,
        DualDebug = 3
    }
}
