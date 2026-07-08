using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class PlaylistPanelSceneTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void MainScene_PlaylistControllerUsesSharedDropdown()
        {
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));

            StringAssert.Contains("m_EditorClassIdentifier: Assembly-CSharp::PlaylistPanelController", scene);
            StringAssert.Contains("playlistDropdown:", scene);
            StringAssert.Contains("guid: c0c62f3c680e47df99310ff17cfc4f84", scene);
            StringAssert.DoesNotContain("m_Name: PanelRoot", scene);
        }

        [Test]
        public void PlaylistPanelController_BindsPlaylistItemsToXrDropdown()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Playlist/PlaylistPanelController.cs"));

            StringAssert.Contains("public XrDropdown playlistDropdown", source);
            StringAssert.Contains("new XrDropdownItemData(title, duration, i)", source);
            StringAssert.Contains("playlistDropdown.SetItems(items, selectedIndex)", source);
            StringAssert.Contains("playlistDropdown.SetPlaceholder(XrUiText.Get(XrUiTextKey.PlaylistEmpty))", source);
            StringAssert.Contains("VlcPlaybackBridge.SkipToIndex(index)", source);
            StringAssert.Contains("VlcPlaybackPayloadParser.ParsePlaylist(json)", source);
            StringAssert.DoesNotContain("private static List<PlaylistItemData> Parse", source);
            StringAssert.Contains("FormatDurationMs", source);
            StringAssert.Contains("DecodeDisplayTitle", source);
            StringAssert.DoesNotContain("PlaylistItemHoverTitle", source);
        }

        [Test]
        public void PlaybackServiceBridge_PlaylistJsonIncludesTotalLength()
        {
            string bridgeSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt"));

            StringAssert.Contains(".put(\"length\", mw.length)", bridgeSource);
        }
    }
}
