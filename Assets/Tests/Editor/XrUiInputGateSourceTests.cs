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
        public void ShortcutManager_BlocksOnlyTriggerAndStickWhileHoveringUi()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/XR/Shortcuts/ShortcutManager.cs"));

            StringAssert.Contains("XrUiInputGate", source);
            StringAssert.Contains("IsUiTriggerAndStickInputBlocked()", source);
            StringAssert.Contains("ProcessHand(XRNode.LeftHand, blockUiTriggerAndStick)", source);
            StringAssert.Contains("ProcessHand(XRNode.RightHand, blockUiTriggerAndStick)", source);
            StringAssert.Contains("bool triggerPressed = GetButton(device, CommonUsages.triggerButton)", source);
            StringAssert.Contains("suppressTriggerAndAxisShortcuts: blockUiTriggerAndStick", source);
            StringAssert.Contains("GetButton(device, CommonUsages.primaryButton)", source);
            StringAssert.Contains("GetButton(device, CommonUsages.secondaryButton)", source);
            StringAssert.DoesNotContain("!blockUiTriggerAndStick && GetButton(device, CommonUsages.primaryButton)", source);
            StringAssert.DoesNotContain("!blockUiTriggerAndStick && GetButton(device, CommonUsages.secondaryButton)", source);
            StringAssert.Contains("IsHoveringBlockingUi", source);
            int updateIndex = source.IndexOf("private void Update()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(updateIndex, 0, "ShortcutManager should keep a testable Update method.");
            int nextMethodIndex = source.IndexOf("public void ReloadConfig()", updateIndex, System.StringComparison.Ordinal);
            Assert.Greater(nextMethodIndex, updateIndex, "ShortcutManager Update body should be followed by ReloadConfig.");
            string updateBody = source.Substring(updateIndex, nextMethodIndex - updateIndex);
            StringAssert.DoesNotContain("return;", updateBody);
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
