namespace XRVLC
{
    public static class ShortcutButtons
    {
        public const string RightStickClick = "right_stick_click";
        public const string LeftStickClick = "left_stick_click";
        public const string ButtonB = "button_b";
        public const string ButtonY = "button_y";

        public static readonly string[] All =
        {
            RightStickClick,
            LeftStickClick,
            ButtonB,
            ButtonY
        };
    }

    public static class ShortcutActions
    {
        public const string None = "none";
        public const string Toggle2xSpeed = "toggle_2x_speed";
        public const string ToggleSubtitle = "toggle_subtitle";
        public const string TogglePassthroughBackground = "toggle_passthrough_background";
        public const string ResetScreenTransform = "reset_screen_transform";

        public static readonly string[] All =
        {
            None,
            Toggle2xSpeed,
            ToggleSubtitle,
            TogglePassthroughBackground,
            ResetScreenTransform
        };

        /// <summary>
        /// 判断配置文件里的操作标识是否属于当前版本支持的快捷操作。
        /// </summary>
        public static bool IsKnown(string actionKey)
        {
            if (string.IsNullOrEmpty(actionKey)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == actionKey) return true;
            }
            return false;
        }
    }
}
