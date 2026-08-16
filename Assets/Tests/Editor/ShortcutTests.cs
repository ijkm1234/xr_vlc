using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using XRVLC;

namespace XRVLC.Tests
{
    [TestFixture]
    public class ShortcutTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void ShortcutSettingsPanels_SaveImmediatelyOnDropdownChange()
        {
            string tabbedSettings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));
            string legacyPanel = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/ShortcutConfigPanel.cs"));
            string shortcutSettings = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Shortcuts/ShortcutSettingsService.cs"));

            StringAssert.Contains("dropdown.onValueChanged.AddListener(_ => SaveGestureMappings())", tabbedSettings);
            StringAssert.Contains("dropdown.onValueChanged.AddListener(OnShortcutDropdownChanged)", legacyPanel);
            StringAssert.Contains("SetValueWithoutNotify", tabbedSettings);
            StringAssert.Contains("SetValueWithoutNotify", legacyPanel);
            StringAssert.DoesNotContain("CreateGestureSaveRow", tabbedSettings);
            StringAssert.DoesNotContain("saveButton.onClick.AddListener(Save)", legacyPanel);

            StringAssert.Contains("DefaultSeekSeconds = 5", shortcutSettings);
            StringAssert.Contains("MinSeekSeconds = 1", shortcutSettings);
            StringAssert.Contains("MaxSeekSeconds = 180", shortcutSettings);
            StringAssert.Contains("KeyVideoJumpDelay", shortcutSettings);
            StringAssert.Contains("SaveShortcutSeekSeconds", shortcutSettings);
            StringAssert.Contains("ClampShortcutSeekSeconds", shortcutSettings);
            StringAssert.Contains("VlcPreferenceStore.PutInt(KeyVideoJumpDelay", shortcutSettings);

            StringAssert.Contains("步进时长", tabbedSettings);
            StringAssert.Contains("LoadShortcutSeekSeconds", tabbedSettings);
            StringAssert.Contains("SaveShortcutSeekSeconds", tabbedSettings);
            StringAssert.Contains("StepSeekSeconds", tabbedSettings);
            StringAssert.Contains("ApplySeekSecondsFromInput", tabbedSettings);
            StringAssert.Contains("_shortcutManager?.ReloadConfig()", tabbedSettings);
        }

        [Test]
        public void Config_FromEmptyJson_UsesDefaults()
        {
            var config = ShortcutConfigData.FromJson("");

            Assert.AreEqual(ShortcutActions.ResetScreenTransform, config.GetAction(ShortcutButtons.RightStickClick));
            Assert.AreEqual(ShortcutActions.ResetScreenTransform, config.GetAction(ShortcutButtons.LeftStickClick));
            Assert.AreEqual(ShortcutActions.ToggleSubtitle, config.GetAction(ShortcutButtons.ButtonB));
            Assert.AreEqual(ShortcutActions.ToggleSubtitle, config.GetAction(ShortcutButtons.ButtonY));
        }

        [Test]
        public void Config_MissingButton_ReturnsNone()
        {
            var config = ShortcutConfigData.FromJson("{\"button_b\":\"toggle_subtitle\"}");

            Assert.AreEqual(ShortcutActions.ToggleSubtitle, config.GetAction(ShortcutButtons.ButtonB));
            Assert.AreEqual(ShortcutActions.None, config.GetAction(ShortcutButtons.RightStickClick));
        }

        [Test]
        public void Config_ToJson_RoundTripsMappings()
        {
            var config = new ShortcutConfigData();
            config.SetAction(ShortcutButtons.RightStickClick, ShortcutActions.ToggleSubtitle);
            config.SetAction(ShortcutButtons.ButtonY, ShortcutActions.None);

            var roundTrip = ShortcutConfigData.FromJson(config.ToJson());

            Assert.AreEqual(ShortcutActions.ToggleSubtitle, roundTrip.GetAction(ShortcutButtons.RightStickClick));
            Assert.AreEqual(ShortcutActions.None, roundTrip.GetAction(ShortcutButtons.ButtonY));
        }

        [Test]
        public void InputState_LeftStickRightCrossesThreshold_EmitsSeekForwardOnce()
        {
            var state = new ShortcutInputState();

            var first = state.UpdateHand(XRNode.LeftHand, new Vector2(0.6f, 0f), false, false);
            var held = state.UpdateHand(XRNode.LeftHand, new Vector2(0.9f, 0f), false, false);

            Assert.AreEqual(ShortcutCommandType.SeekForward, first.Type);
            Assert.AreEqual(ShortcutCommandType.None, held.Type);
        }

        [Test]
        public void InputState_LeftStickHeldFullyRight_WaitsOneSecondThenRepeatsThirtySecondSeek()
        {
            var state = new ShortcutInputState();

            var initialStep = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0f);
            var heldBeforeDelay = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0.99f);
            var heldAtDelay = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0.01f);
            var heldBeforeRepeat = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0.49f);
            var heldAtRepeat = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0.01f);

            Assert.AreEqual(ShortcutCommandType.SeekForward, initialStep.Type);
            Assert.AreEqual(ShortcutCommandType.None, heldBeforeDelay.Type);
            Assert.AreEqual(ShortcutCommandType.SeekForward30Seconds, heldAtDelay.Type);
            Assert.AreEqual(ShortcutCommandType.None, heldBeforeRepeat.Type);
            Assert.AreEqual(ShortcutCommandType.SeekForward30Seconds, heldAtRepeat.Type);
        }

        [Test]
        public void InputState_RightStickHeldFullyLeft_WaitsOneSecondThenRepeatsThirtySecondSeek()
        {
            var state = new ShortcutInputState();

            var initialStep = state.UpdateHand(XRNode.RightHand, new Vector2(-1f, 0f), false, false, deltaTimeSeconds: 0f);
            var heldBeforeDelay = state.UpdateHand(XRNode.RightHand, new Vector2(-1f, 0f), false, false, deltaTimeSeconds: 0.99f);
            var heldAtDelay = state.UpdateHand(XRNode.RightHand, new Vector2(-1f, 0f), false, false, deltaTimeSeconds: 0.01f);
            var heldBeforeRepeat = state.UpdateHand(XRNode.RightHand, new Vector2(-1f, 0f), false, false, deltaTimeSeconds: 0.49f);
            var heldAtRepeat = state.UpdateHand(XRNode.RightHand, new Vector2(-1f, 0f), false, false, deltaTimeSeconds: 0.01f);

            Assert.AreEqual(ShortcutCommandType.SeekBackward, initialStep.Type);
            Assert.AreEqual(ShortcutCommandType.None, heldBeforeDelay.Type);
            Assert.AreEqual(ShortcutCommandType.SeekBackward30Seconds, heldAtDelay.Type);
            Assert.AreEqual(ShortcutCommandType.None, heldBeforeRepeat.Type);
            Assert.AreEqual(ShortcutCommandType.SeekBackward30Seconds, heldAtRepeat.Type);
        }

        [Test]
        public void InputState_StickHeldPastNormalButNotFull_DoesNotRepeatThirtySecondSeek()
        {
            var state = new ShortcutInputState();

            var initialStep = state.UpdateHand(XRNode.LeftHand, new Vector2(0.9f, 0f), false, false, deltaTimeSeconds: 0f);
            var held = state.UpdateHand(XRNode.LeftHand, new Vector2(0.9f, 0f), false, false, deltaTimeSeconds: 2f);

            Assert.AreEqual(ShortcutCommandType.SeekForward, initialStep.Type);
            Assert.AreEqual(ShortcutCommandType.None, held.Type);
        }

        [Test]
        public void InputState_FullStickLargeDelta_DoesNotCatchUpRepeatsOnFollowingFrame()
        {
            var state = new ShortcutInputState();

            state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0f);
            var delayed = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 2f);
            var nextFrame = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0.01f);
            var nextInterval = state.UpdateHand(XRNode.LeftHand, new Vector2(1f, 0f), false, false, deltaTimeSeconds: 0.49f);

            Assert.AreEqual(ShortcutCommandType.SeekForward30Seconds, delayed.Type);
            Assert.AreEqual(ShortcutCommandType.None, nextFrame.Type);
            Assert.AreEqual(ShortcutCommandType.SeekForward30Seconds, nextInterval.Type);
        }

        [Test]
        public void InputState_RightStickLeftCrossesThreshold_EmitsSeekBackwardOnce()
        {
            var state = new ShortcutInputState();

            var first = state.UpdateHand(XRNode.RightHand, new Vector2(-0.6f, 0f), false, false);
            var held = state.UpdateHand(XRNode.RightHand, new Vector2(-0.9f, 0f), false, false);

            Assert.AreEqual(ShortcutCommandType.SeekBackward, first.Type);
            Assert.AreEqual(ShortcutCommandType.None, held.Type);
        }

        [Test]
        public void InputState_ButtonEdges_EmitFixedAndConfigurableCommands()
        {
            var state = new ShortcutInputState();

            var playPause = state.UpdateHand(XRNode.LeftHand, Vector2.zero, true, false);
            var configurable = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, true);
            var held = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, true);

            Assert.AreEqual(ShortcutCommandType.TogglePlayPause, playPause.Type);
            Assert.AreEqual(ShortcutCommandType.ConfigurableAction, configurable.Type);
            Assert.AreEqual(ShortcutButtons.ButtonB, configurable.ButtonId);
            Assert.AreEqual(ShortcutCommandType.None, held.Type);
        }

        [Test]
        public void InputState_RightPrimaryButtonA_EmitsPlayPauseOnce()
        {
            var state = new ShortcutInputState();

            var first = state.UpdateHand(XRNode.RightHand, Vector2.zero, true, false);
            var held = state.UpdateHand(XRNode.RightHand, Vector2.zero, true, false);

            Assert.AreEqual(ShortcutCommandType.TogglePlayPause, first.Type);
            Assert.AreEqual(ShortcutCommandType.None, held.Type);
        }

        [Test]
        public void InputState_SuppressedTriggerAndAxisStillAllowsButtonEdgesWithoutCatchupSeek()
        {
            var state = new ShortcutInputState();

            var buttonWhileSuppressed = state.UpdateHand(
                XRNode.RightHand,
                new Vector2(0.9f, 0f),
                true,
                false,
                triggerPressed: true,
                deltaTimeSeconds: 1f,
                suppressTriggerAndAxisShortcuts: true);
            var stillHeldAfterUnsuppressed = state.UpdateHand(
                XRNode.RightHand,
                new Vector2(0.9f, 0f),
                false,
                false);
            var recentered = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false);
            var pushedAgain = state.UpdateHand(XRNode.RightHand, new Vector2(0.9f, 0f), false, false);

            Assert.AreEqual(ShortcutCommandType.TogglePlayPause, buttonWhileSuppressed.Type);
            Assert.AreEqual(ShortcutCommandType.None, stillHeldAfterUnsuppressed.Type);
            Assert.AreEqual(ShortcutCommandType.None, recentered.Type);
            Assert.AreEqual(ShortcutCommandType.SeekForward, pushedAgain.Type);
        }

        [Test]
        public void InputState_ResetClearsHeldButtonAndAxisEdges()
        {
            var state = new ShortcutInputState();

            state.UpdateHand(XRNode.RightHand, new Vector2(0.9f, 0f), true, false);
            state.Reset();

            var axisAfterReset = state.UpdateHand(XRNode.RightHand, new Vector2(0.9f, 0f), false, false);
            state.Reset();
            var buttonAfterReset = state.UpdateHand(XRNode.RightHand, Vector2.zero, true, false);

            Assert.AreEqual(ShortcutCommandType.SeekForward, axisAfterReset.Type);
            Assert.AreEqual(ShortcutCommandType.TogglePlayPause, buttonAfterReset.Type);
        }

        [Test]
        public void InputState_TriggerHold_WaitsHalfSecondBeforeFastRateBegin()
        {
            var state = new ShortcutInputState();

            var pressed = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, true, 0f);
            var heldBeforeDelay = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, true, 0.49f);
            var heldAtDelay = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, true, 0.01f);
            var heldAfterBegin = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, true, 0.1f);
            var released = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, false);

            Assert.AreEqual(ShortcutCommandType.None, pressed.Type);
            Assert.AreEqual(ShortcutCommandType.None, heldBeforeDelay.Type);
            Assert.AreEqual(ShortcutCommandType.BeginShortcutFastRate, heldAtDelay.Type);
            Assert.AreEqual(ShortcutCommandType.None, heldAfterBegin.Type);
            Assert.AreEqual(ShortcutCommandType.EndShortcutFastRate, released.Type);
        }

        [Test]
        public void InputState_TriggerReleasedBeforeHalfSecond_DoesNotEmitFastRateEnd()
        {
            var state = new ShortcutInputState();

            var pressed = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, true, 0f);
            var heldBeforeDelay = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, true, 0.49f);
            var released = state.UpdateHand(XRNode.RightHand, Vector2.zero, false, false, false, false);

            Assert.AreEqual(ShortcutCommandType.None, pressed.Type);
            Assert.AreEqual(ShortcutCommandType.None, heldBeforeDelay.Type);
            Assert.AreEqual(ShortcutCommandType.None, released.Type);
        }

        [Test]
        public void InputState_StickPressed_SuppressesAxisShortcutsUntilRecentered()
        {
            var state = new ShortcutInputState();

            var stickClick = state.UpdateHand(XRNode.LeftHand, new Vector2(0.9f, 0.8f), false, false, true);
            var releasedWhileHeldRight = state.UpdateHand(XRNode.LeftHand, new Vector2(0.9f, 0f), false, false, false);
            var recentered = state.UpdateHand(XRNode.LeftHand, Vector2.zero, false, false, false);
            var pushedAgain = state.UpdateHand(XRNode.LeftHand, new Vector2(0.9f, 0f), false, false, false);

            Assert.AreEqual(ShortcutCommandType.ConfigurableAction, stickClick.Type);
            Assert.AreEqual(ShortcutButtons.LeftStickClick, stickClick.ButtonId);
            Assert.AreEqual(ShortcutCommandType.None, releasedWhileHeldRight.Type);
            Assert.AreEqual(ShortcutCommandType.None, recentered.Type);
            Assert.AreEqual(ShortcutCommandType.SeekForward, pushedAgain.Type);
        }
    }
}
