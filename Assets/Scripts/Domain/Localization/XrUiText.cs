using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace XRVLC.Localization
{
    public static class XrUiTextKey
    {
        public const string TitlePlaceholder = "title.placeholder";
        public const string UnknownVideo = "fallback.unknown_video";
        public const string UnknownSubtitle = "fallback.unknown_subtitle";
        public const string PlaylistEmpty = "playlist.empty";
        public const string AudioTrackNone = "track.audio.none";
        public const string SubtitleTrackNone = "track.subtitle.none";
        public const string SubtitleTrackChooseOther = "track.subtitle.choose_other";

        public const string SettingsTabPlayback = "settings.tab.playback";
        public const string SettingsTabGesture = "settings.tab.gesture";
        public const string SettingsTabSubtitle = "settings.tab.subtitle";
        public const string SettingsTabVideo = "settings.tab.video";
        public const string SettingsTabAudio = "settings.tab.audio";
        public const string SettingsSectionPlaybackSpeed = "settings.section.playback_speed";
        public const string SettingsSectionShortcuts = "settings.section.shortcuts";
        public const string SettingsSectionSubtitle = "settings.section.subtitle";
        public const string SettingsSectionVideoScale = "settings.section.video_scale";
        public const string SettingsSectionAudioChannel = "settings.section.audio_channel";
        public const string SettingsGestureInfoTooltip = "settings.gesture.info_tooltip";
        public const string SettingsSeekStep = "settings.row.seek_step";
        public const string SettingsLeftStickClick = "settings.shortcut.left_stick_click";
        public const string SettingsRightStickClick = "settings.shortcut.right_stick_click";
        public const string SettingsButtonY = "settings.shortcut.button_y";
        public const string SettingsButtonB = "settings.shortcut.button_b";
        public const string SettingsSpatialSubtitles = "settings.toggle.spatial_subtitles";
        public const string SettingsSubtitlesOutside = "settings.toggle.subtitles_outside";
        public const string SettingsSubtitleFont = "settings.row.subtitle_font";
        public const string SettingsSubtitleOpacity = "settings.row.subtitle_opacity";
        public const string SettingsSubtitleDelay = "settings.row.subtitle_delay";
        public const string SettingsSubtitleFontNano = "settings.subtitle_font.nano";
        public const string SettingsSubtitleFontMicro = "settings.subtitle_font.micro";
        public const string SettingsSubtitleFontSmallest = "settings.subtitle_font.smallest";
        public const string SettingsSubtitleFontSmall = "settings.subtitle_font.small";
        public const string SettingsSubtitleFontNormal = "settings.subtitle_font.normal";
        public const string SettingsSubtitleFontBig = "settings.subtitle_font.big";
        public const string SettingsSubtitleFontHuge = "settings.subtitle_font.huge";
        public const string SettingsPlaybackRate = "settings.row.playback_rate";
        public const string SettingsAspectRatio = "settings.row.aspect_ratio";
        public const string SettingsAspectRatioAuto = "settings.aspect_ratio.auto";
        public const string SettingsVideoScaleFit = "settings.video_scale.fit";
        public const string SettingsVideoScaleStretch = "settings.video_scale.stretch";
        public const string SettingsVideoScaleFillCrop = "settings.video_scale.fill_crop";
        public const string SettingsVideoScaleOriginal = "settings.video_scale.original";
        public const string SettingsAudioBoost = "settings.audio.boost";
        public const string SettingsAudioStereo = "settings.audio.stereo";
        public const string SettingsAudioMono = "settings.audio.mono";

        public const string ShortcutNone = "shortcut.action.none";
        public const string ShortcutToggle2xSpeed = "shortcut.action.toggle_2x_speed";
        public const string ShortcutToggleSubtitle = "shortcut.action.toggle_subtitle";
        public const string ShortcutTogglePassthroughBackground = "shortcut.action.toggle_passthrough_background";
        public const string ShortcutResetScreen = "shortcut.action.reset_screen";

        public const string GeometryProjection = "geometry.section.projection";
        public const string GeometryStereo = "geometry.section.stereo";
        public const string GeometryCurve = "geometry.section.curve";
        public const string GeometryProjection180 = "geometry.projection.180";
        public const string GeometryProjection360 = "geometry.projection.360";
        public const string GeometryProjectionFisheye = "geometry.projection.fisheye";
        public const string GeometryProjectionFlat = "geometry.projection.flat";
        public const string GeometryStereoMono = "geometry.stereo.mono";
        public const string GeometryStereoTopBottom = "geometry.stereo.top_bottom";
        public const string GeometryStereoLeftRight = "geometry.stereo.left_right";
        public const string GeometryCurveNone = "geometry.curve.none";
        public const string GeometryCurveSmall = "geometry.curve.small";
        public const string GeometryCurveLarge = "geometry.curve.large";
        public const string GeometryFisheyeFormula = "geometry.section.fisheye_formula";
        public const string GeometryFisheyeEquidistant = "geometry.fisheye.equidistant";
        public const string GeometryFisheyeEquisolid = "geometry.fisheye.equisolid";
        public const string GeometryFisheyeStereographic = "geometry.fisheye.stereographic";
        public const string GeometryFisheyeOrthographic = "geometry.fisheye.orthographic";

        public const string ChromaKeyEnabled = "chromakey.enabled";
        public const string ChromaKeyColor = "chromakey.key_color";
        public const string ChromaKeyHex = "chromakey.hex";
        public const string ChromaKeyColorRange = "chromakey.color_range";
        public const string ChromaKeyExtractColor = "chromakey.extract_color";
        public const string ChromaKeyEdgeSmooth = "chromakey.edge_smooth";
        public const string ChromaKeyDespill = "chromakey.despill";
    }

    public static class XrUiText
    {
        public const string TableName = "XR_UI";
        private const string LocaleEnUs = "en-US";
        private const string LocaleZhHans = "zh-Hans";
        private const string LocaleZhHant = "zh-Hant";

        private static readonly Dictionary<string, string> EnUs = new Dictionary<string, string>
        {
            [XrUiTextKey.TitlePlaceholder] = "Video title",
            [XrUiTextKey.UnknownVideo] = "Unknown video",
            [XrUiTextKey.UnknownSubtitle] = "Unknown subtitle",
            [XrUiTextKey.PlaylistEmpty] = "No playlist",
            [XrUiTextKey.AudioTrackNone] = "No audio track",
            [XrUiTextKey.SubtitleTrackNone] = "No subtitles",
            [XrUiTextKey.SubtitleTrackChooseOther] = "Choose another subtitle",

            [XrUiTextKey.SettingsTabPlayback] = "Playback",
            [XrUiTextKey.SettingsTabGesture] = "Shortcuts",
            [XrUiTextKey.SettingsTabSubtitle] = "Subtitles",
            [XrUiTextKey.SettingsTabVideo] = "Video",
            [XrUiTextKey.SettingsTabAudio] = "Audio",
            [XrUiTextKey.SettingsSectionPlaybackSpeed] = "Playback speed",
            [XrUiTextKey.SettingsSectionShortcuts] = "Controller shortcuts",
            [XrUiTextKey.SettingsSectionSubtitle] = "Subtitles",
            [XrUiTextKey.SettingsSectionVideoScale] = "Video scaling",
            [XrUiTextKey.SettingsSectionAudioChannel] = "Audio output",
            [XrUiTextKey.SettingsGestureInfoTooltip] =
                "Stick left/right: seek backward/forward\n" +
                "Hold stick left/right: rewind/fast-forward 30s\n" +
                "Stick forward/back: adjust screen distance\n" +
                "Hold trigger: play at 2x speed\n" +
                "Grip: move screen\n" +
                "Press stick: reset screen position",
            [XrUiTextKey.SettingsSeekStep] = "Seek step",
            [XrUiTextKey.SettingsLeftStickClick] = "Left stick press",
            [XrUiTextKey.SettingsRightStickClick] = "Right stick press",
            [XrUiTextKey.SettingsButtonY] = "Y button",
            [XrUiTextKey.SettingsButtonB] = "B button",
            [XrUiTextKey.SettingsSpatialSubtitles] = "Use spatial subtitles",
            [XrUiTextKey.SettingsSubtitlesOutside] = "Render outside screen",
            [XrUiTextKey.SettingsSubtitleFont] = "Font size",
            [XrUiTextKey.SettingsSubtitleOpacity] = "Opacity",
            [XrUiTextKey.SettingsSubtitleDelay] = "Subtitle delay",
            [XrUiTextKey.SettingsSubtitleFontNano] = "Tiny",
            [XrUiTextKey.SettingsSubtitleFontMicro] = "Very small",
            [XrUiTextKey.SettingsSubtitleFontSmallest] = "Smaller",
            [XrUiTextKey.SettingsSubtitleFontSmall] = "Small",
            [XrUiTextKey.SettingsSubtitleFontNormal] = "Normal",
            [XrUiTextKey.SettingsSubtitleFontBig] = "Large",
            [XrUiTextKey.SettingsSubtitleFontHuge] = "Huge",
            [XrUiTextKey.SettingsPlaybackRate] = "Speed",
            [XrUiTextKey.SettingsAspectRatio] = "Aspect ratio",
            [XrUiTextKey.SettingsAspectRatioAuto] = "Auto",
            [XrUiTextKey.SettingsVideoScaleFit] = "Fit",
            [XrUiTextKey.SettingsVideoScaleStretch] = "Stretch",
            [XrUiTextKey.SettingsVideoScaleFillCrop] = "Fill crop",
            [XrUiTextKey.SettingsVideoScaleOriginal] = "Original",
            [XrUiTextKey.SettingsAudioBoost] = "Volume boost",
            [XrUiTextKey.SettingsAudioStereo] = "Stereo",
            [XrUiTextKey.SettingsAudioMono] = "Mix to mono",

            [XrUiTextKey.ShortcutNone] = "None",
            [XrUiTextKey.ShortcutToggle2xSpeed] = "Toggle 2x speed",
            [XrUiTextKey.ShortcutToggleSubtitle] = "Toggle subtitles",
            [XrUiTextKey.ShortcutTogglePassthroughBackground] = "Passthrough background",
            [XrUiTextKey.ShortcutResetScreen] = "Reset screen",

            [XrUiTextKey.GeometryProjection] = "Projection",
            [XrUiTextKey.GeometryStereo] = "3D format",
            [XrUiTextKey.GeometryCurve] = "Flat curve",
            [XrUiTextKey.GeometryProjection180] = "180 panorama",
            [XrUiTextKey.GeometryProjection360] = "360 panorama",
            [XrUiTextKey.GeometryProjectionFisheye] = "Fisheye 180",
            [XrUiTextKey.GeometryProjectionFlat] = "Flat",
            [XrUiTextKey.GeometryStereoMono] = "No 3D",
            [XrUiTextKey.GeometryStereoTopBottom] = "Top-bottom 3D",
            [XrUiTextKey.GeometryStereoLeftRight] = "Left-right 3D",
            [XrUiTextKey.GeometryCurveNone] = "No curve",
            [XrUiTextKey.GeometryCurveSmall] = "Small curve",
            [XrUiTextKey.GeometryCurveLarge] = "Large curve",
            [XrUiTextKey.GeometryFisheyeFormula] = "Fisheye formula",
            [XrUiTextKey.GeometryFisheyeEquidistant] = "Equidistant",
            [XrUiTextKey.GeometryFisheyeEquisolid] = "Equisolid angle",
            [XrUiTextKey.GeometryFisheyeStereographic] = "Stereographic",
            [XrUiTextKey.GeometryFisheyeOrthographic] = "Orthographic",

            [XrUiTextKey.ChromaKeyEnabled] = "Transparency",
            [XrUiTextKey.ChromaKeyColor] = "Key color",
            [XrUiTextKey.ChromaKeyHex] = "Hex",
            [XrUiTextKey.ChromaKeyColorRange] = "Color Range",
            [XrUiTextKey.ChromaKeyExtractColor] = "Extract Key Color",
            [XrUiTextKey.ChromaKeyEdgeSmooth] = "Edge Smooth",
            [XrUiTextKey.ChromaKeyDespill] = "Despill"
        };

        private static readonly Dictionary<string, string> ZhHans = new Dictionary<string, string>
        {
            [XrUiTextKey.TitlePlaceholder] = "视频标题",
            [XrUiTextKey.UnknownVideo] = "未知视频",
            [XrUiTextKey.UnknownSubtitle] = "未知字幕",
            [XrUiTextKey.PlaylistEmpty] = "暂无播放列表",
            [XrUiTextKey.AudioTrackNone] = "无音轨",
            [XrUiTextKey.SubtitleTrackNone] = "无字幕",
            [XrUiTextKey.SubtitleTrackChooseOther] = "选择其他字幕",

            [XrUiTextKey.SettingsTabPlayback] = "播放",
            [XrUiTextKey.SettingsTabGesture] = "快捷键",
            [XrUiTextKey.SettingsTabSubtitle] = "字幕",
            [XrUiTextKey.SettingsTabVideo] = "视频",
            [XrUiTextKey.SettingsTabAudio] = "音频",
            [XrUiTextKey.SettingsSectionPlaybackSpeed] = "播放速度",
            [XrUiTextKey.SettingsSectionShortcuts] = "手柄快捷键",
            [XrUiTextKey.SettingsSectionSubtitle] = "字幕",
            [XrUiTextKey.SettingsSectionVideoScale] = "画面拉伸裁剪",
            [XrUiTextKey.SettingsSectionAudioChannel] = "声道输出",
            [XrUiTextKey.SettingsGestureInfoTooltip] =
                "摇杆左右：步进/步退\n" +
                "摇杆左右长按：30s 快进/快退\n" +
                "摇杆前后：调整屏幕距离\n" +
                "板机键长按：2x 快速播放\n" +
                "抓取键：移动屏幕\n" +
                "按下摇杆：恢复屏幕位置",
            [XrUiTextKey.SettingsSeekStep] = "步进时长",
            [XrUiTextKey.SettingsLeftStickClick] = "左摇杆按下",
            [XrUiTextKey.SettingsRightStickClick] = "右摇杆按下",
            [XrUiTextKey.SettingsButtonY] = "Y 按钮",
            [XrUiTextKey.SettingsButtonB] = "B 按钮",
            [XrUiTextKey.SettingsSpatialSubtitles] = "使用空间字幕",
            [XrUiTextKey.SettingsSubtitlesOutside] = "渲染在屏幕外",
            [XrUiTextKey.SettingsSubtitleFont] = "字体",
            [XrUiTextKey.SettingsSubtitleOpacity] = "透明度",
            [XrUiTextKey.SettingsSubtitleDelay] = "字幕延迟",
            [XrUiTextKey.SettingsSubtitleFontNano] = "极小",
            [XrUiTextKey.SettingsSubtitleFontMicro] = "很小",
            [XrUiTextKey.SettingsSubtitleFontSmallest] = "较小",
            [XrUiTextKey.SettingsSubtitleFontSmall] = "小",
            [XrUiTextKey.SettingsSubtitleFontNormal] = "普通",
            [XrUiTextKey.SettingsSubtitleFontBig] = "大",
            [XrUiTextKey.SettingsSubtitleFontHuge] = "超大",
            [XrUiTextKey.SettingsPlaybackRate] = "倍速",
            [XrUiTextKey.SettingsAspectRatio] = "宽高比",
            [XrUiTextKey.SettingsAspectRatioAuto] = "自动",
            [XrUiTextKey.SettingsVideoScaleFit] = "适应",
            [XrUiTextKey.SettingsVideoScaleStretch] = "拉伸",
            [XrUiTextKey.SettingsVideoScaleFillCrop] = "填充裁剪",
            [XrUiTextKey.SettingsVideoScaleOriginal] = "原始",
            [XrUiTextKey.SettingsAudioBoost] = "音量增益",
            [XrUiTextKey.SettingsAudioStereo] = "立体声",
            [XrUiTextKey.SettingsAudioMono] = "混合为单声道",

            [XrUiTextKey.ShortcutNone] = "无操作",
            [XrUiTextKey.ShortcutToggle2xSpeed] = "切换 2x 速度",
            [XrUiTextKey.ShortcutToggleSubtitle] = "字幕显隐",
            [XrUiTextKey.ShortcutTogglePassthroughBackground] = "透视背景",
            [XrUiTextKey.ShortcutResetScreen] = "恢复屏幕默认位置",

            [XrUiTextKey.GeometryProjection] = "投影模式",
            [XrUiTextKey.GeometryStereo] = "3D 格式",
            [XrUiTextKey.GeometryCurve] = "平面弧度",
            [XrUiTextKey.GeometryProjection180] = "180全景",
            [XrUiTextKey.GeometryProjection360] = "360全景",
            [XrUiTextKey.GeometryProjectionFisheye] = "鱼眼180",
            [XrUiTextKey.GeometryProjectionFlat] = "平面",
            [XrUiTextKey.GeometryStereoMono] = "无3D",
            [XrUiTextKey.GeometryStereoTopBottom] = "上下3D",
            [XrUiTextKey.GeometryStereoLeftRight] = "左右3D",
            [XrUiTextKey.GeometryCurveNone] = "无曲面",
            [XrUiTextKey.GeometryCurveSmall] = "小曲面",
            [XrUiTextKey.GeometryCurveLarge] = "大曲面",
            [XrUiTextKey.GeometryFisheyeFormula] = "鱼眼公式",
            [XrUiTextKey.GeometryFisheyeEquidistant] = "等距",
            [XrUiTextKey.GeometryFisheyeEquisolid] = "等立体角",
            [XrUiTextKey.GeometryFisheyeStereographic] = "立体投影",
            [XrUiTextKey.GeometryFisheyeOrthographic] = "正交投影",

            [XrUiTextKey.ChromaKeyEnabled] = "透明模式",
            [XrUiTextKey.ChromaKeyColor] = "键色",
            [XrUiTextKey.ChromaKeyHex] = "Hex",
            [XrUiTextKey.ChromaKeyColorRange] = "颜色范围",
            [XrUiTextKey.ChromaKeyExtractColor] = "提取主色",
            [XrUiTextKey.ChromaKeyEdgeSmooth] = "边缘平滑",
            [XrUiTextKey.ChromaKeyDespill] = "去溢色"
        };

        private static readonly Dictionary<string, string> ZhHantOverrides = new Dictionary<string, string>();

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            string official = TryGetFromUnityLocalization(key);
            if (!string.IsNullOrEmpty(official) && official != key)
                return official;

            return GetFromFallbackTables(key, ResolveLocaleCode());
        }

        public static string ForShortcutAction(string actionKey)
        {
            return actionKey switch
            {
                XRVLC.ShortcutActions.Toggle2xSpeed => Get(XrUiTextKey.ShortcutToggle2xSpeed),
                XRVLC.ShortcutActions.ToggleSubtitle => Get(XrUiTextKey.ShortcutToggleSubtitle),
                XRVLC.ShortcutActions.TogglePassthroughBackground => Get(XrUiTextKey.ShortcutTogglePassthroughBackground),
                XRVLC.ShortcutActions.ResetScreenTransform => Get(XrUiTextKey.ShortcutResetScreen),
                _ => Get(XrUiTextKey.ShortcutNone)
            };
        }

        private static string GetFromFallbackTables(string key, string localeCode)
        {
            if (localeCode == LocaleZhHant && ZhHantOverrides.TryGetValue(key, out string zhHantValue))
                return zhHantValue;

            if ((localeCode == LocaleZhHant || localeCode == LocaleZhHans) && ZhHans.TryGetValue(key, out string zhHansValue))
                return zhHansValue;

            if (EnUs.TryGetValue(key, out string enUsValue))
                return enUsValue;

            return key;
        }

        private static string ResolveLocaleCode()
        {
            string selectedLocale = TryGetSelectedUnityLocaleCode();
            if (!string.IsNullOrEmpty(selectedLocale))
                return NormalizeLocaleCode(selectedLocale);

            string applicationLocale = ResolveApplicationSystemLanguage();
            if (!string.IsNullOrEmpty(applicationLocale))
                return applicationLocale;

            string cultureName = CultureInfo.CurrentUICulture?.Name;
            if (!string.IsNullOrEmpty(cultureName))
                return NormalizeLocaleCode(cultureName);

            return LocaleEnUs;
        }

        private static string ResolveApplicationSystemLanguage()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.ChineseSimplified => LocaleZhHans,
                SystemLanguage.ChineseTraditional => LocaleZhHant,
                SystemLanguage.Chinese => LocaleZhHans,
                SystemLanguage.English => LocaleEnUs,
                _ => null
            };
        }

        private static string NormalizeLocaleCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return LocaleEnUs;

            string normalized = code.Replace('_', '-');
            if (normalized.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("zh-MO", StringComparison.OrdinalIgnoreCase))
            {
                return LocaleZhHant;
            }

            if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return LocaleZhHans;

            return LocaleEnUs;
        }

        private static string TryGetSelectedUnityLocaleCode()
        {
            try
            {
                Type settingsType = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
                object selectedLocale = settingsType?.GetProperty("SelectedLocale", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                object identifier = selectedLocale?.GetType().GetProperty("Identifier")?.GetValue(selectedLocale);
                return identifier?.GetType().GetProperty("Code")?.GetValue(identifier) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string TryGetFromUnityLocalization(string key)
        {
            try
            {
                Type settingsType = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
                object stringDatabase = settingsType?.GetProperty("StringDatabase", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                MethodInfo getLocalizedString = stringDatabase?.GetType().GetMethod(
                    "GetLocalizedString",
                    new[] { typeof(string), typeof(string) });
                return getLocalizedString?.Invoke(stringDatabase, new object[] { TableName, key }) as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
