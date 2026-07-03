using XRVLC;
using XRVLC.Infrastructure.VlcBridge.Preferences;

namespace XRVLC.Services.Shortcuts
{
    public static class ShortcutSettingsService
    {
        public const string KeyShortcutButtonMappings = "xr_button_mappings";
        public const string KeyVideoJumpDelay = "video_jump_delay";
        public const int DefaultSeekSeconds = 5;
        public const int MinSeekSeconds = 1;
        public const int MaxSeekSeconds = 180;

        /// <summary>
        /// 从 VLC SharedPreferences 读取手柄快捷键使用的跳转秒数。
        /// </summary>
        public static int LoadShortcutSeekSeconds()
        {
            return ClampShortcutSeekSeconds(VlcPreferenceStore.GetInt(KeyVideoJumpDelay, DefaultSeekSeconds));
        }

        /// <summary>
        /// 保存手柄摇杆左右快进/快退使用的跳转秒数。
        /// </summary>
        public static void SaveShortcutSeekSeconds(int seconds)
        {
            VlcPreferenceStore.PutInt(KeyVideoJumpDelay, ClampShortcutSeekSeconds(seconds));
        }

        public static int ClampShortcutSeekSeconds(int seconds)
        {
            if (seconds < MinSeekSeconds)
                return MinSeekSeconds;
            if (seconds > MaxSeekSeconds)
                return MaxSeekSeconds;
            return seconds;
        }

        /// <summary>
        /// 从 VLC SharedPreferences 读取 XR 手柄按键映射。
        /// </summary>
        public static ShortcutConfigData LoadShortcutMappings()
        {
            string json = VlcPreferenceStore.GetString(
                KeyShortcutButtonMappings,
                ShortcutConfigData.DefaultJson());
            return ShortcutConfigData.FromJson(json);
        }

        /// <summary>
        /// 保存 XR 手柄按键映射，供 VLC 设置页和 Unity 运行时共享。
        /// </summary>
        public static void SaveShortcutMappings(ShortcutConfigData mappings)
        {
            ShortcutConfigData safeMappings = mappings ?? ShortcutConfigData.Defaults();
            VlcPreferenceStore.PutString(KeyShortcutButtonMappings, safeMappings.ToJson());
        }
    }
}
