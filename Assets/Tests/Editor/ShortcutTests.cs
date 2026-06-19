using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR;
using XRVLC;

namespace XRVLC.Tests
{
    [TestFixture]
    public class ShortcutTests
    {
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
