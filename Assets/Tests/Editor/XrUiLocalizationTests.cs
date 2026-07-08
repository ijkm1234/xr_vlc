using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class XrUiLocalizationTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void Manifest_UsesOfficialUnityLocalizationPackage()
        {
            string manifest = File.ReadAllText(ProjectFile("Packages/manifest.json"));

            StringAssert.Contains("\"com.unity.localization\": \"1.5.12\"", manifest);
        }

        [Test]
        public void SharedUiTextTable_DefinesSystemLocaleAndTraditionalChineseFallback()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/Domain/Localization/XrUiText.cs"));
            string csv = File.ReadAllText(ProjectFile("Assets/Localization/XR_UI.csv"));

            StringAssert.Contains("public const string TableName = \"XR_UI\"", source);
            StringAssert.Contains("private const string LocaleZhHans = \"zh-Hans\"", source);
            StringAssert.Contains("private const string LocaleZhHant = \"zh-Hant\"", source);
            StringAssert.Contains("ZhHantOverrides.TryGetValue", source);
            StringAssert.Contains("localeCode == LocaleZhHant || localeCode == LocaleZhHans", source);
            StringAssert.Contains("zh-TW", source);
            StringAssert.Contains("zh-HK", source);
            StringAssert.Contains("zh-MO", source);

            StringAssert.Contains("key,en-US,zh-Hans,zh-Hant", csv);
            StringAssert.Contains("track.subtitle.choose_other,Choose another subtitle,选择其他字幕,", csv);
            StringAssert.Contains("settings.gesture.info_tooltip", csv);
            StringAssert.Contains("geometry.curve.large,Large curve,大曲面,", csv);
        }

        [Test]
        public void RuntimeUiScripts_UseLocalizationHelperForPlayerVisibleText()
        {
            string vr = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string settings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));
            string shortcut = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/ShortcutConfigPanel.cs"));
            string playlist = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Playlist/PlaylistPanelController.cs"));
            string parser = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/Parsing/VlcPlaybackPayloadParser.cs"));

            StringAssert.Contains("XrUiText.Get(XrUiTextKey.TitlePlaceholder)", vr);
            StringAssert.Contains("XrUiText.Get(XrUiTextKey.AudioTrackNone)", vr);
            StringAssert.Contains("XrUiText.Get(XrUiTextKey.SubtitleTrackChooseOther)", vr);
            StringAssert.Contains("XrUiText.Get(XrUiTextKey.GeometryProjection)", vr);
            StringAssert.Contains("CreateTabButton(tabBar, SettingsTab.Playback, XrUiText.Get(XrUiTextKey.SettingsTabPlayback))", settings);
            StringAssert.Contains("XrUiText.ForShortcutAction(ShortcutActions.ToggleSubtitle)", shortcut);
            StringAssert.Contains("playlistDropdown.SetPlaceholder(XrUiText.Get(XrUiTextKey.PlaylistEmpty))", playlist);
            StringAssert.Contains("XrUiText.Get(XrUiTextKey.UnknownVideo)", parser);

            StringAssert.DoesNotContain("new XrDropdownItemData(\"无操作\")", settings);
            StringAssert.DoesNotContain("new XrDropdownItemData(\"None\")", shortcut);
            StringAssert.DoesNotContain("playlistDropdown.SetPlaceholder(\"暂无播放列表\")", playlist);
            StringAssert.DoesNotContain("ChooseSubtitleTrackOptionLabel", vr);
        }
    }
}
