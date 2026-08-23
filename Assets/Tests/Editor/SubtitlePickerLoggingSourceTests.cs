using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public sealed class SubtitlePickerLoggingSourceTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void XrDropdown_LogsHoverClickAndSelectionBoundaries()
        {
            string dropdownSource = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdown.cs"));
            string itemSource = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdownItem.cs"));

            StringAssert.Contains("[XrDropdown] RowHover enter", itemSource);
            StringAssert.Contains("[XrDropdown] RowClick", itemSource);
            StringAssert.Contains("[XrDropdown] SelectIndex entry", dropdownSource);
            StringAssert.Contains("[XrDropdown] SelectIndex ignored", dropdownSource);
            StringAssert.Contains("[XrDropdown] SelectIndex invoke", dropdownSource);
        }

        [Test]
        public void SubtitlePickerChain_LogsUnitySelectionAndBridgeEntry()
        {
            string managerSource = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string bridgeSource = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));

            StringAssert.Contains("[VRUIManager][SubtitlePicker] Subtitle button clicked", managerSource);
            StringAssert.Contains("[VRUIManager][SubtitlePicker] Subtitle dropdown refreshed", managerSource);
            StringAssert.Contains("[VRUIManager][SubtitlePicker] OnSubtitleTrackSelected", managerSource);
            StringAssert.Contains("[VRUIManager][SubtitlePicker] Opening picker from choose-other-subtitle row", managerSource);
            StringAssert.Contains("dropdownItem=", managerSource);
            StringAssert.Contains("[VlcPlaybackBridge] OpenSubtitlePicker entry", bridgeSource);
        }
    }
}
