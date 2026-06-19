using XRVLC.Infrastructure.VlcBridge.Preferences;

namespace XRVLC.Services.Settings
{
    public static class PlaybackUiSettingsService
    {
        public const string KeyVideoRatio = "video_ratio";

        public const int DefaultVideoScaleOrdinal = 0;

        public static int LoadVideoScaleOrdinal()
        {
            int value = VlcPreferenceStore.GetInt(KeyVideoRatio, DefaultVideoScaleOrdinal);
            return value < 0 ? DefaultVideoScaleOrdinal : value;
        }

        public static void SaveVideoScaleOrdinal(int scaleOrdinal)
        {
            VlcPreferenceStore.PutInt(KeyVideoRatio, scaleOrdinal < 0 ? DefaultVideoScaleOrdinal : scaleOrdinal);
        }
    }
}
