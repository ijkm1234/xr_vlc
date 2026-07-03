using XRVLC.Infrastructure.VlcBridge.Preferences;

namespace XRVLC.Services.Settings
{
    public static class PlaybackUiSettingsService
    {
        public const string KeyVideoRatio = "video_ratio";
        public const string KeyVideoScaleMode = "video_scale_mode";
        public const string KeyVideoAspectRatio = "video_aspect_ratio";
        private const string LegacyVideoRatioMigrated = "video_ratio_dual_layout_migrated";

        public const int DefaultVideoScaleOrdinal = 0;
        public const VideoScaleMode DefaultVideoScaleMode = VideoScaleMode.Fit;
        public const VideoAspectRatio DefaultVideoAspectRatio = VideoAspectRatio.Source;

        public static int LoadVideoScaleOrdinal()
        {
            int value = VlcPreferenceStore.GetInt(KeyVideoRatio, DefaultVideoScaleOrdinal);
            return value < 0 ? DefaultVideoScaleOrdinal : value;
        }

        public static void SaveVideoScaleOrdinal(int scaleOrdinal)
        {
            VlcPreferenceStore.PutInt(KeyVideoRatio, scaleOrdinal < 0 ? DefaultVideoScaleOrdinal : scaleOrdinal);
        }

        public static VideoScaleMode LoadVideoScaleMode()
        {
            MigrateLegacyVideoRatioIfNeeded();
            string value = VlcPreferenceStore.GetString(KeyVideoScaleMode, DefaultVideoScaleMode.ToString());
            return TryParseEnum(value, DefaultVideoScaleMode);
        }

        public static void SaveVideoScaleMode(VideoScaleMode mode)
        {
            VlcPreferenceStore.PutString(KeyVideoScaleMode, mode.ToString());
            VlcPreferenceStore.PutInt(KeyVideoRatio, ToLegacyScaleOrdinal(mode));
            VlcPreferenceStore.PutBool(LegacyVideoRatioMigrated, true);
        }

        public static VideoAspectRatio LoadVideoAspectRatio()
        {
            MigrateLegacyVideoRatioIfNeeded();
            string value = VlcPreferenceStore.GetString(KeyVideoAspectRatio, ToAspectRatioValue(DefaultVideoAspectRatio));
            return ParseAspectRatio(value);
        }

        public static void SaveVideoAspectRatio(VideoAspectRatio aspectRatio)
        {
            VlcPreferenceStore.PutString(KeyVideoAspectRatio, ToAspectRatioValue(aspectRatio));
            VlcPreferenceStore.PutBool(LegacyVideoRatioMigrated, true);
        }

        private static void MigrateLegacyVideoRatioIfNeeded()
        {
            if (VlcPreferenceStore.GetBool(LegacyVideoRatioMigrated, false))
                return;

            int legacyValue = LoadVideoScaleOrdinal();
            VlcPreferenceStore.PutString(KeyVideoScaleMode, ToLegacyScaleMode(legacyValue).ToString());
            VlcPreferenceStore.PutString(KeyVideoAspectRatio, ToAspectRatioValue(ToLegacyAspectRatio(legacyValue)));
            VlcPreferenceStore.PutBool(LegacyVideoRatioMigrated, true);
        }

        private static VideoScaleMode ToLegacyScaleMode(int legacyValue)
        {
            return legacyValue switch
            {
                1 => VideoScaleMode.Stretch,
                2 => VideoScaleMode.FillCrop,
                _ => VideoScaleMode.Fit
            };
        }

        private static VideoAspectRatio ToLegacyAspectRatio(int legacyValue)
        {
            return legacyValue switch
            {
                3 => VideoAspectRatio.Ratio16x9,
                4 => VideoAspectRatio.Ratio4x3,
                5 => VideoAspectRatio.Ratio16x10,
                8 => VideoAspectRatio.Ratio235x1,
                11 => VideoAspectRatio.Source,
                _ => VideoAspectRatio.Source
            };
        }

        public static int ToLegacyScaleOrdinal(VideoScaleMode mode)
        {
            return mode switch
            {
                VideoScaleMode.Stretch => 1,
                VideoScaleMode.FillCrop => 2,
                _ => 0
            };
        }

        private static string ToAspectRatioValue(VideoAspectRatio aspectRatio)
        {
            return aspectRatio switch
            {
                VideoAspectRatio.Ratio16x9 => "16:9",
                VideoAspectRatio.Ratio4x3 => "4:3",
                VideoAspectRatio.Ratio16x10 => "16:10",
                VideoAspectRatio.Ratio221x1 => "2.21:1",
                VideoAspectRatio.Ratio235x1 => "2.35:1",
                VideoAspectRatio.Ratio239x1 => "2.39:1",
                VideoAspectRatio.Ratio5x4 => "5:4",
                _ => "Source"
            };
        }

        private static VideoAspectRatio ParseAspectRatio(string value)
        {
            switch (value)
            {
                case "16:9":
                case nameof(VideoAspectRatio.Ratio16x9):
                    return VideoAspectRatio.Ratio16x9;
                case "4:3":
                case nameof(VideoAspectRatio.Ratio4x3):
                    return VideoAspectRatio.Ratio4x3;
                case "16:10":
                case nameof(VideoAspectRatio.Ratio16x10):
                    return VideoAspectRatio.Ratio16x10;
                case "2.21:1":
                case nameof(VideoAspectRatio.Ratio221x1):
                    return VideoAspectRatio.Ratio221x1;
                case "2.35:1":
                case nameof(VideoAspectRatio.Ratio235x1):
                    return VideoAspectRatio.Ratio235x1;
                case "2.39:1":
                case nameof(VideoAspectRatio.Ratio239x1):
                    return VideoAspectRatio.Ratio239x1;
                case "5:4":
                case nameof(VideoAspectRatio.Ratio5x4):
                    return VideoAspectRatio.Ratio5x4;
                case "Source":
                    return VideoAspectRatio.Source;
                default:
                    return DefaultVideoAspectRatio;
            }
        }

        private static T TryParseEnum<T>(string value, T fallback) where T : struct
        {
            return System.Enum.TryParse(value, out T parsed) ? parsed : fallback;
        }
    }
}
