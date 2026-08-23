using UnityEngine;
using UnityEngine.XR;

namespace XRVLC
{
    public class ShortcutInputState
    {
        public const float StickThreshold = 0.5f;
        public const float FullStickThreshold = 0.95f;
        public const float FullStickInitialSeekDelaySeconds = 1f;
        public const float FullStickRepeatSeekIntervalSeconds = 0.5f;
        public const float TriggerFastRateHoldSeconds = 0.5f;

        private Vector2 _leftAxis;
        private Vector2 _rightAxis;
        private int _leftFullStickDirection;
        private int _rightFullStickDirection;
        private float _leftFullStickHeldSeconds;
        private float _rightFullStickHeldSeconds;
        private float _leftFullStickNextSeekSeconds;
        private float _rightFullStickNextSeekSeconds;
        private bool _leftPrimaryPressed;
        private bool _rightPrimaryPressed;
        private bool _leftSecondaryPressed;
        private bool _rightSecondaryPressed;
        private bool _leftStickPressed;
        private bool _rightStickPressed;
        private bool _leftTriggerPressed;
        private bool _rightTriggerPressed;
        private float _leftTriggerHeldSeconds;
        private float _rightTriggerHeldSeconds;
        private bool _leftTriggerFastRateBegun;
        private bool _rightTriggerFastRateBegun;
        private bool _leftTriggerSuppressedUntilRelease;
        private bool _rightTriggerSuppressedUntilRelease;

        /// <summary>
        /// 更新单只手柄的轴值和按键状态，并在越过阈值或按键按下边沿时输出命令。
        /// </summary>
        public ShortcutCommand UpdateHand(
            XRNode hand,
            Vector2 axis,
            bool primaryButtonPressed,
            bool secondaryButtonPressed,
            bool stickPressed = false,
            bool triggerPressed = false,
            float deltaTimeSeconds = 0f,
            bool suppressTriggerAndAxisShortcuts = false)
        {
            Vector2 previousAxis = GetPreviousAxis(hand);
            SetPreviousAxis(hand, axis);

            ShortcutCommand triggerCommand = UpdateTriggerHold(
                hand,
                triggerPressed,
                deltaTimeSeconds,
                suppressTriggerAndAxisShortcuts);
            if (triggerCommand.Type != ShortcutCommandType.None)
                return triggerCommand;

            if (!suppressTriggerAndAxisShortcuts && !stickPressed && axis.x > StickThreshold && previousAxis.x <= StickThreshold)
                return new ShortcutCommand(ShortcutCommandType.SeekForward);
            if (!suppressTriggerAndAxisShortcuts && !stickPressed && axis.x < -StickThreshold && previousAxis.x >= -StickThreshold)
                return new ShortcutCommand(ShortcutCommandType.SeekBackward);

            if (suppressTriggerAndAxisShortcuts)
            {
                ResetFullStickHold(hand);
            }
            else
            {
                ShortcutCommand fullStickCommand = UpdateFullStickHold(hand, axis, stickPressed, deltaTimeSeconds);
                if (fullStickCommand.Type != ShortcutCommandType.None)
                    return fullStickCommand;
            }

            if (CheckEdge(hand, ButtonKind.Primary, primaryButtonPressed))
                return new ShortcutCommand(ShortcutCommandType.TogglePlayPause);

            if (CheckEdge(hand, ButtonKind.Stick, stickPressed))
            {
                string buttonId = hand == XRNode.LeftHand
                    ? ShortcutButtons.LeftStickClick
                    : ShortcutButtons.RightStickClick;
                return new ShortcutCommand(ShortcutCommandType.ConfigurableAction, buttonId);
            }

            if (CheckEdge(hand, ButtonKind.Secondary, secondaryButtonPressed))
            {
                string buttonId = hand == XRNode.LeftHand
                    ? ShortcutButtons.ButtonY
                    : ShortcutButtons.ButtonB;
                return new ShortcutCommand(ShortcutCommandType.ConfigurableAction, buttonId);
            }

            return ShortcutCommand.None;
        }

        public void Reset()
        {
            _leftAxis = Vector2.zero;
            _rightAxis = Vector2.zero;
            _leftFullStickDirection = 0;
            _rightFullStickDirection = 0;
            _leftFullStickHeldSeconds = 0f;
            _rightFullStickHeldSeconds = 0f;
            _leftFullStickNextSeekSeconds = FullStickInitialSeekDelaySeconds;
            _rightFullStickNextSeekSeconds = FullStickInitialSeekDelaySeconds;
            _leftPrimaryPressed = false;
            _rightPrimaryPressed = false;
            _leftSecondaryPressed = false;
            _rightSecondaryPressed = false;
            _leftStickPressed = false;
            _rightStickPressed = false;
            _leftTriggerPressed = false;
            _rightTriggerPressed = false;
            _leftTriggerHeldSeconds = 0f;
            _rightTriggerHeldSeconds = 0f;
            _leftTriggerFastRateBegun = false;
            _rightTriggerFastRateBegun = false;
            _leftTriggerSuppressedUntilRelease = false;
            _rightTriggerSuppressedUntilRelease = false;
        }

        /// <summary>
        /// 读取对应手柄上一帧的摇杆轴值，用于左右推边沿判断。
        /// </summary>
        private Vector2 GetPreviousAxis(XRNode hand) =>
            hand == XRNode.LeftHand ? _leftAxis : _rightAxis;

        /// <summary>
        /// 保存当前帧轴值，供下一帧检测是否重新越过阈值。
        /// </summary>
        private void SetPreviousAxis(XRNode hand, Vector2 axis)
        {
            if (hand == XRNode.LeftHand) _leftAxis = axis;
            else _rightAxis = axis;
        }

        private ShortcutCommand UpdateFullStickHold(XRNode hand, Vector2 axis, bool stickPressed, float deltaTimeSeconds)
        {
            int direction = GetFullStickDirection(axis, stickPressed);
            if (direction == 0)
            {
                ResetFullStickHold(hand);
                return ShortcutCommand.None;
            }

            if (direction != GetFullStickDirection(hand))
                StartFullStickHold(hand, direction);

            float heldSeconds = GetFullStickHeldSeconds(hand) + Mathf.Max(0f, deltaTimeSeconds);
            SetFullStickHeldSeconds(hand, heldSeconds);

            float nextSeekSeconds = GetFullStickNextSeekSeconds(hand);
            if (heldSeconds < nextSeekSeconds)
                return ShortcutCommand.None;

            SetFullStickNextSeekSeconds(hand, heldSeconds + FullStickRepeatSeekIntervalSeconds);
            return new ShortcutCommand(direction > 0
                ? ShortcutCommandType.SeekForward30Seconds
                : ShortcutCommandType.SeekBackward30Seconds);
        }

        private static int GetFullStickDirection(Vector2 axis, bool stickPressed)
        {
            if (stickPressed)
                return 0;
            if (axis.x >= FullStickThreshold)
                return 1;
            if (axis.x <= -FullStickThreshold)
                return -1;
            return 0;
        }

        private int GetFullStickDirection(XRNode hand) =>
            hand == XRNode.LeftHand ? _leftFullStickDirection : _rightFullStickDirection;

        private float GetFullStickHeldSeconds(XRNode hand) =>
            hand == XRNode.LeftHand ? _leftFullStickHeldSeconds : _rightFullStickHeldSeconds;

        private void SetFullStickHeldSeconds(XRNode hand, float seconds)
        {
            if (hand == XRNode.LeftHand) _leftFullStickHeldSeconds = seconds;
            else _rightFullStickHeldSeconds = seconds;
        }

        private float GetFullStickNextSeekSeconds(XRNode hand) =>
            hand == XRNode.LeftHand ? _leftFullStickNextSeekSeconds : _rightFullStickNextSeekSeconds;

        private void SetFullStickNextSeekSeconds(XRNode hand, float seconds)
        {
            if (hand == XRNode.LeftHand) _leftFullStickNextSeekSeconds = seconds;
            else _rightFullStickNextSeekSeconds = seconds;
        }

        private void StartFullStickHold(XRNode hand, int direction)
        {
            if (hand == XRNode.LeftHand) _leftFullStickDirection = direction;
            else _rightFullStickDirection = direction;

            SetFullStickHeldSeconds(hand, 0f);
            SetFullStickNextSeekSeconds(hand, FullStickInitialSeekDelaySeconds);
        }

        private void ResetFullStickHold(XRNode hand)
        {
            if (hand == XRNode.LeftHand) _leftFullStickDirection = 0;
            else _rightFullStickDirection = 0;

            SetFullStickHeldSeconds(hand, 0f);
            SetFullStickNextSeekSeconds(hand, FullStickInitialSeekDelaySeconds);
        }

        /// <summary>
        /// 检测按键是否从未按下变为按下，保证快捷操作只触发一次。
        /// </summary>
        private bool CheckEdge(XRNode hand, ButtonKind kind, bool pressed)
        {
            bool wasPressed = GetPreviousButton(hand, kind);
            SetPreviousButton(hand, kind, pressed);
            return pressed && !wasPressed;
        }

        private ShortcutCommand UpdateTriggerHold(
            XRNode hand,
            bool pressed,
            float deltaTimeSeconds,
            bool suppressFastRate)
        {
            bool wasPressed = GetPreviousButton(hand, ButtonKind.Trigger);
            SetPreviousButton(hand, ButtonKind.Trigger, pressed);

            if (!pressed)
            {
                bool hadBegun = GetTriggerFastRateBegun(hand);
                SetTriggerHeldSeconds(hand, 0f);
                SetTriggerFastRateBegun(hand, false);
                SetTriggerSuppressedUntilRelease(hand, false);
                return wasPressed && hadBegun
                    ? new ShortcutCommand(ShortcutCommandType.EndShortcutFastRate)
                    : ShortcutCommand.None;
            }

            if (suppressFastRate)
                SetTriggerSuppressedUntilRelease(hand, true);

            if (GetTriggerSuppressedUntilRelease(hand))
            {
                bool hadBegun = GetTriggerFastRateBegun(hand);
                SetTriggerHeldSeconds(hand, 0f);
                SetTriggerFastRateBegun(hand, false);
                return hadBegun
                    ? new ShortcutCommand(ShortcutCommandType.EndShortcutFastRate)
                    : ShortcutCommand.None;
            }

            float heldSeconds = wasPressed
                ? GetTriggerHeldSeconds(hand)
                : 0f;
            heldSeconds += Mathf.Max(0f, deltaTimeSeconds);
            SetTriggerHeldSeconds(hand, heldSeconds);

            if (!GetTriggerFastRateBegun(hand) && heldSeconds >= TriggerFastRateHoldSeconds)
            {
                SetTriggerFastRateBegun(hand, true);
                return new ShortcutCommand(ShortcutCommandType.BeginShortcutFastRate);
            }

            return ShortcutCommand.None;
        }

        private float GetTriggerHeldSeconds(XRNode hand) =>
            hand == XRNode.LeftHand ? _leftTriggerHeldSeconds : _rightTriggerHeldSeconds;

        private void SetTriggerHeldSeconds(XRNode hand, float seconds)
        {
            if (hand == XRNode.LeftHand) _leftTriggerHeldSeconds = seconds;
            else _rightTriggerHeldSeconds = seconds;
        }

        private bool GetTriggerFastRateBegun(XRNode hand) =>
            hand == XRNode.LeftHand ? _leftTriggerFastRateBegun : _rightTriggerFastRateBegun;

        private void SetTriggerFastRateBegun(XRNode hand, bool begun)
        {
            if (hand == XRNode.LeftHand) _leftTriggerFastRateBegun = begun;
            else _rightTriggerFastRateBegun = begun;
        }

        private bool GetTriggerSuppressedUntilRelease(XRNode hand) =>
            hand == XRNode.LeftHand
                ? _leftTriggerSuppressedUntilRelease
                : _rightTriggerSuppressedUntilRelease;

        private void SetTriggerSuppressedUntilRelease(XRNode hand, bool suppressed)
        {
            if (hand == XRNode.LeftHand) _leftTriggerSuppressedUntilRelease = suppressed;
            else _rightTriggerSuppressedUntilRelease = suppressed;
        }

        /// <summary>
        /// 获取指定手柄和按键类型上一帧的按下状态。
        /// </summary>
        private bool GetPreviousButton(XRNode hand, ButtonKind kind)
        {
            if (hand == XRNode.LeftHand)
            {
                return kind switch
                {
                    ButtonKind.Primary => _leftPrimaryPressed,
                    ButtonKind.Secondary => _leftSecondaryPressed,
                    ButtonKind.Stick => _leftStickPressed,
                    ButtonKind.Trigger => _leftTriggerPressed,
                    _ => false
                };
            }

            return kind switch
            {
                ButtonKind.Primary => _rightPrimaryPressed,
                ButtonKind.Secondary => _rightSecondaryPressed,
                ButtonKind.Stick => _rightStickPressed,
                ButtonKind.Trigger => _rightTriggerPressed,
                _ => false
            };
        }

        /// <summary>
        /// 更新指定手柄和按键类型的上一帧状态缓存。
        /// </summary>
        private void SetPreviousButton(XRNode hand, ButtonKind kind, bool pressed)
        {
            if (hand == XRNode.LeftHand)
            {
                switch (kind)
                {
                    case ButtonKind.Primary:
                        _leftPrimaryPressed = pressed;
                        break;
                    case ButtonKind.Secondary:
                        _leftSecondaryPressed = pressed;
                        break;
                    case ButtonKind.Stick:
                        _leftStickPressed = pressed;
                        break;
                    case ButtonKind.Trigger:
                        _leftTriggerPressed = pressed;
                        break;
                }
                return;
            }

            switch (kind)
            {
                case ButtonKind.Primary:
                    _rightPrimaryPressed = pressed;
                    break;
                case ButtonKind.Secondary:
                    _rightSecondaryPressed = pressed;
                    break;
                case ButtonKind.Stick:
                    _rightStickPressed = pressed;
                    break;
                case ButtonKind.Trigger:
                    _rightTriggerPressed = pressed;
                    break;
            }
        }

        private enum ButtonKind
        {
            Primary,
            Secondary,
            Stick,
            Trigger
        }
    }
}
