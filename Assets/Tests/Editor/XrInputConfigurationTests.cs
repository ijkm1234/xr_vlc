using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class XrInputConfigurationTests
    {
        private static readonly string[] NonXrInputTokens =
        {
            "<Mouse>",
            "<Keyboard>",
            "<Touchscreen>",
            "<Gamepad>",
            "<Joystick>",
            "<Pen>",
            "<HandheldARInputDevice>",
            "<TouchscreenGestureInputController>",
            "Keyboard&Mouse"
        };

        [Test]
        public void MainScene_XrUiInputModuleDisablesNonXrFallbackInput()
        {
            string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity"));

            StringAssert.Contains("m_EnableXRInput: 1", scene);
            StringAssert.Contains("m_EnableBuiltinActionsAsFallback: 0", scene);
            StringAssert.DoesNotContain("m_sendNavigationEvents: 1", scene);
            StringAssert.DoesNotContain("m_EnableMouseInput: 1", scene);
            StringAssert.DoesNotContain("m_EnableTouchInput: 1", scene);
            StringAssert.DoesNotContain("m_EnableGamepadInput: 1", scene);
            StringAssert.DoesNotContain("m_EnableJoystickInput: 1", scene);
        }

        [Test]
        public void ProjectWideInputActionsContainOnlyXrBindings()
        {
            string actions = File.ReadAllText(Path.Combine(Application.dataPath, "InputSystem_Actions.inputactions"));

            AssertNoNonXrInputTokens(actions);
            StringAssert.Contains("\"name\": \"XR\"", actions);
            StringAssert.Contains("<XRController>", actions);
        }

        [Test]
        public void ReferencedXriInputActionsContainOnlyXrRelevantBindings()
        {
            string actions = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Samples/XR Interaction Toolkit/3.4.0/Starter Assets/XRI Default Input Actions.inputactions"));

            AssertNoNonXrInputTokens(actions);
            StringAssert.DoesNotContain("\"name\": \"XRI UI\"", actions);
            StringAssert.DoesNotContain("\"name\": \"Touchscreen Gestures\"", actions);
            StringAssert.DoesNotContain("*/{Submit}", actions);
            StringAssert.DoesNotContain("*/{Cancel}", actions);
            StringAssert.Contains("<XRController>", actions);
            StringAssert.Contains("<XRHMD>", actions);
        }

        [Test]
        public void LegacyInputManagerHasNoNonXrFallbackAxes()
        {
            string inputManager = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "../ProjectSettings/InputManager.asset"));

            StringAssert.Contains("m_Axes: []", inputManager);
            StringAssert.DoesNotContain("mouse", inputManager.ToLowerInvariant());
            StringAssert.DoesNotContain("keyboard", inputManager.ToLowerInvariant());
            StringAssert.DoesNotContain("joystick", inputManager.ToLowerInvariant());
        }

        [Test]
        public void PlatformSettingsDisableNonXrTouchAndGamepadFallbacks()
        {
            string projectSettings = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "../ProjectSettings/ProjectSettings.asset"));

            StringAssert.Contains("androidGamepadSupportLevel: 0", projectSettings);
            StringAssert.Contains("switchEnableTouchScreen: 0", projectSettings);
            StringAssert.Contains("embeddedLinuxEnableGamepadInput: 0", projectSettings);
        }

        private static void AssertNoNonXrInputTokens(string source)
        {
            foreach (string token in NonXrInputTokens)
                StringAssert.DoesNotContain(token, source);
        }
    }
}
