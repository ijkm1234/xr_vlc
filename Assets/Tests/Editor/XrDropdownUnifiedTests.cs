using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class XrDropdownUnifiedTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void SharedDropdownPrefab_ExposesConfigurableSizingAndScrolling()
        {
            string prefab = File.ReadAllText(ProjectFile("Assets/Prefabs/UI/XrDropdown.prefab"));

            StringAssert.Contains("m_Name: XrDropdown", prefab);
            StringAssert.Contains("m_Name: Popup", prefab);
            StringAssert.Contains("m_Name: Viewport", prefab);
            StringAssert.Contains("m_Name: Content", prefab);
            StringAssert.Contains("m_Name: RowPrefab", prefab);
            StringAssert.Contains("m_Name: PrimaryText", prefab);
            StringAssert.Contains("m_Name: SecondaryText", prefab);
            StringAssert.Contains("m_Name: Tooltip", prefab);
            StringAssert.Contains("m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.ScrollRect", prefab);
            StringAssert.Contains("maxVisibleItems: 10", prefab);
            StringAssert.Contains("width: 520", prefab);
            StringAssert.Contains("fontSize: 22", prefab);
            StringAssert.Contains("horizontalPadding: 16", prefab);
            StringAssert.Contains("verticalPadding: 15", prefab);
            StringAssert.Contains("stickScrollRowsPerSecond", prefab);
        }

        [Test]
        public void DropdownLogic_ComputesRowHeightFromFontSizeAndPadding()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdown.cs"));

            StringAssert.Contains("public float RowHeight", source);
            StringAssert.Contains("Mathf.Ceil(fontSize + verticalPadding * 2f)", source);
            StringAssert.Contains("Mathf.Min(_items.Count, maxVisibleItems) * RowHeight", source);
            StringAssert.Contains("CommonUsages.primary2DAxis", source);
            StringAssert.Contains("verticalNormalizedPosition", source);
        }

        [Test]
        public void AllDropdownConsumersUseSharedXrDropdown()
        {
            string vr = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string playlist = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Playlist/PlaylistPanelController.cs"));
            string settings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));
            string shortcut = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/ShortcutConfigPanel.cs"));

            StringAssert.Contains("public XrDropdown audioTrackDropdown", vr);
            StringAssert.Contains("public XrDropdown subtitleTrackDropdown", vr);
            StringAssert.Contains("SetItems", vr);
            StringAssert.DoesNotContain("TrackDropdownMaxVisibleItems = 4", vr);
            StringAssert.DoesNotContain("TMP_Dropdown audioTrackDropdown", vr);
            StringAssert.DoesNotContain("TMP_Dropdown subtitleTrackDropdown", vr);

            StringAssert.Contains("public XrDropdown playlistDropdown", playlist);
            StringAssert.Contains("SkipToIndex", playlist);
            StringAssert.Contains("XrDropdownItemData", playlist);
            StringAssert.DoesNotContain("PlaylistItemHoverTitle", playlist);

            StringAssert.Contains("XrDropdown shortcutDropdownPrefab", settings);
            StringAssert.Contains("Instantiate(shortcutDropdownPrefab", settings);
            StringAssert.DoesNotContain("typeof(TMP_Dropdown)", settings);
            StringAssert.DoesNotContain("AttachDropdownTemplate", settings);

            StringAssert.Contains("XrDropdown", shortcut);
            StringAssert.DoesNotContain("TMP_Dropdown", shortcut);
        }

        [Test]
        public void MainScene_ReferencesSharedDropdownsForTracksAndPlaylist()
        {
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));

            StringAssert.Contains("audioTrackDropdown:", scene);
            StringAssert.Contains("subtitleTrackDropdown:", scene);
            StringAssert.Contains("playlistDropdown:", scene);
            StringAssert.DoesNotContain("Unity.TextMeshPro::TMPro.TMP_Dropdown", scene);
        }
    }
}
