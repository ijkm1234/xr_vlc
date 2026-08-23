namespace XRVLC
{
    public enum ShortcutCommandType
    {
        None,
        SeekBackward,
        SeekForward,
        SeekBackward30Seconds,
        SeekForward30Seconds,
        TogglePlayPause,
        BeginShortcutFastRate,
        EndShortcutFastRate,
        ConfigurableAction
    }

    public readonly struct ShortcutCommand
    {
        public ShortcutCommandType Type { get; }
        public string ButtonId { get; }

        /// <summary>
        /// 创建一条输入状态机输出命令；可配置命令会携带按键 ID。
        /// </summary>
        public ShortcutCommand(ShortcutCommandType type, string buttonId = null)
        {
            Type = type;
            ButtonId = buttonId;
        }

        /// <summary>
        /// 表示当前帧没有可派发的快捷操作。
        /// </summary>
        public static ShortcutCommand None =>
            new ShortcutCommand(ShortcutCommandType.None);
    }
}
