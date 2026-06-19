using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class XrUiInputGateSourceTests
    {
        [Test]
        public void VRUIManager_RoutesTriggerThroughXrUiInputGate()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("XrUiInputGate", source);
            StringAssert.Contains("TryConsumeCurrentHover", source);
            StringAssert.Contains("RegisterUiTreeNodes", source);
            StringAssert.Contains("RegisterRuntimeUiTreeNodes", source);
            StringAssert.DoesNotContain("behaviour is IUIInteractor", source);
            StringAssert.DoesNotContain("TrackedDeviceModel model", source);
        }

        [Test]
        public void ShortcutManager_BlocksControllerShortcutsWhileHoveringUi()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/XR/Shortcuts/ShortcutManager.cs"));

            StringAssert.Contains("XrUiInputGate", source);
            StringAssert.Contains("IsUiInputBlocked()", source);
            StringAssert.Contains("ResetRuntimeInputState()", source);
            StringAssert.Contains("IsHoveringBlockingUi", source);
            int updateIndex = source.IndexOf("private void Update()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(updateIndex, 0, "ShortcutManager should keep a testable Update method.");
            StringAssert.Contains("return;", source.Substring(updateIndex));
        }

        [Test]
        public void XrUiRaycastTracker_OwnsInteractorModelLookup()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/XR/XrUiRaycastTracker.cs"));

            StringAssert.Contains("IUIInteractor", source);
            StringAssert.Contains("TryGetUIModel", source);
            StringAssert.Contains("currentRaycast.gameObject", source);
        }
    }
}
