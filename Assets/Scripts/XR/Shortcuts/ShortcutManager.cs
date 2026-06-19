using UnityEngine;
using UnityEngine.XR;
using XRVLC.Media;
using XRVLC.Services.Shortcuts;
using XRVLC.UI.XR;

namespace XRVLC.XR
{
    public class ShortcutManager : MonoBehaviour
    {
        [Header("Scene References")]
        public PlaybackService playbackService;

        [Header("Screen Movement")]
        public float distanceStepMetersPerSecond = 3f;
        public Behaviour[] rayVisualsToHideWhileMoving;

        [Header("XR UI Routing")]
        public XrUiInputGate uiInputGate;

        private readonly ShortcutInputState _inputState = new ShortcutInputState();
        private ShortcutPlaybackService _shortcutPlaybackService;
        private bool _leftGripWasPressed;
        private bool _rightGripWasPressed;
        private XRNode _lastGripHand = XRNode.RightHand;
        private bool[] _rayVisualEnabledBeforeMove;
        private bool _screenMoveRayVisualsHidden;

        /// <summary>
        /// 解析场景引用并加载共享配置。
        /// </summary>
        private void Start()
        {
            if (playbackService == null)
                playbackService = FindAnyObjectByType<PlaybackService>();
            if (uiInputGate == null)
                uiInputGate = FindAnyObjectByType<XrUiInputGate>();

            _shortcutPlaybackService = new ShortcutPlaybackService(playbackService);
            EnsureRayVisualsToHideWhileMoving();
            ReloadConfig();
        }

        private void OnDisable()
        {
            if (_leftGripWasPressed || _rightGripWasPressed)
                _shortcutPlaybackService?.EndVideoScreenControllerMove();

            _shortcutPlaybackService?.CancelShortcutPlaybackRateHold();
            SetScreenMoveRayVisualsVisible(true);
        }

        /// <summary>
        /// 每帧轮询左右手柄输入，并处理 Grip 持续移屏。
        /// </summary>
        private void Update()
        {
            ProcessGripMove();

            if (IsUiInputBlocked())
            {
                ResetShortcutInputState();
                return;
            }

            ProcessHand(XRNode.LeftHand);
            ProcessHand(XRNode.RightHand);
        }

        /// <summary>
        /// 供设置面板保存后显式刷新内存缓存，避免每帧 JNI 读取。
        /// </summary>
        public void ReloadConfig()
        {
            _shortcutPlaybackService?.ReloadConfig();
        }

        public bool IsUiInputBlocked()
        {
            if (uiInputGate == null)
                uiInputGate = FindAnyObjectByType<XrUiInputGate>();

            return uiInputGate != null && uiInputGate.IsHoveringBlockingUi;
        }

        private void ResetShortcutInputState()
        {
            _shortcutPlaybackService?.CancelShortcutPlaybackRateHold();
            _inputState.Reset();
        }

        /// <summary>
        /// 读取单只手柄的摇杆与按键输入，并派发边沿触发命令。
        /// </summary>
        private void ProcessHand(XRNode hand)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
            Vector2 axis = GetAxis(device);
            bool stickPressed = GetButton(device, CommonUsages.primary2DAxisClick);

            if (!stickPressed && axis.y > ShortcutInputState.StickThreshold)
                _shortcutPlaybackService?.OffsetVideoScreenDistance(distanceStepMetersPerSecond * Time.deltaTime);
            else if (!stickPressed && axis.y < -ShortcutInputState.StickThreshold)
                _shortcutPlaybackService?.OffsetVideoScreenDistance(-distanceStepMetersPerSecond * Time.deltaTime);

            ShortcutCommand command = _inputState.UpdateHand(
                hand,
                axis,
                GetButton(device, CommonUsages.primaryButton),
                GetButton(device, CommonUsages.secondaryButton),
                stickPressed,
                GetButton(device, CommonUsages.triggerButton),
                Time.deltaTime);

            _shortcutPlaybackService?.Execute(command);
        }

        /// <summary>
        /// 安全读取主摇杆轴值；设备无效或无数据时返回零向量。
        /// </summary>
        private static Vector2 GetAxis(InputDevice device)
        {
            return device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis)
                ? axis
                : Vector2.zero;
        }

        /// <summary>
        /// 安全读取 XR 布尔按键状态。
        /// </summary>
        private static bool GetButton(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
        }

        /// <summary>
        /// 处理 Grip 持续按住移屏，双手同时按住时以最后按下或仍有效的一只手为准。
        /// </summary>
        private void ProcessGripMove()
        {
            InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool leftGrip = GetButton(leftDevice, CommonUsages.gripButton);
            bool rightGrip = GetButton(rightDevice, CommonUsages.gripButton);
            bool wasAnyGripPressed = _leftGripWasPressed || _rightGripWasPressed;
            XRNode previousGripHand = _lastGripHand;

            if (rightGrip && !_rightGripWasPressed) _lastGripHand = XRNode.RightHand;
            if (leftGrip && !_leftGripWasPressed) _lastGripHand = XRNode.LeftHand;
            if (_lastGripHand == XRNode.RightHand && !rightGrip && leftGrip) _lastGripHand = XRNode.LeftHand;
            if (_lastGripHand == XRNode.LeftHand && !leftGrip && rightGrip) _lastGripHand = XRNode.RightHand;

            _leftGripWasPressed = leftGrip;
            _rightGripWasPressed = rightGrip;

            bool isAnyGripPressed = leftGrip || rightGrip;
            if (!isAnyGripPressed)
            {
                if (wasAnyGripPressed)
                {
                    _shortcutPlaybackService?.EndVideoScreenControllerMove();
                    SetScreenMoveRayVisualsVisible(true);
                }
                return;
            }

            InputDevice activeDevice = InputDevices.GetDeviceAtXRNode(_lastGripHand);
            if (!activeDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion controllerRotation))
                return;

            Vector3 controllerRayDirection = controllerRotation * Vector3.forward;
            if (!wasAnyGripPressed || previousGripHand != _lastGripHand)
            {
                _shortcutPlaybackService?.BeginVideoScreenControllerMove(controllerRayDirection);
                SetScreenMoveRayVisualsVisible(false);
            }
            _shortcutPlaybackService?.UpdateVideoScreenControllerMove(controllerRayDirection);
        }

        private void EnsureRayVisualsToHideWhileMoving()
        {
            if (rayVisualsToHideWhileMoving != null && rayVisualsToHideWhileMoving.Length > 0)
                return;

            Behaviour[] behaviours = FindObjectsByType<Behaviour>(FindObjectsInactive.Include);
            int count = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (IsRayVisualBehaviour(behaviours[i]))
                    count++;
            }

            if (count == 0)
            {
                rayVisualsToHideWhileMoving = System.Array.Empty<Behaviour>();
                return;
            }

            rayVisualsToHideWhileMoving = new Behaviour[count];
            int index = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (IsRayVisualBehaviour(behaviour))
                    rayVisualsToHideWhileMoving[index++] = behaviour;
            }
        }

        private static bool IsRayVisualBehaviour(Behaviour behaviour)
        {
            if (behaviour == null) return false;

            string typeName = behaviour.GetType().Name;
            return typeName == "NearFarReticleVisual"
                || typeName.Contains("InteractorLineVisual")
                || typeName.Contains("LineVisual");
        }

        private void SetScreenMoveRayVisualsVisible(bool visible)
        {
            EnsureRayVisualsToHideWhileMoving();

            if (!visible)
            {
                if (_screenMoveRayVisualsHidden)
                    return;

                _rayVisualEnabledBeforeMove = new bool[rayVisualsToHideWhileMoving.Length];
                for (int i = 0; i < rayVisualsToHideWhileMoving.Length; i++)
                {
                    Behaviour visual = rayVisualsToHideWhileMoving[i];
                    if (visual == null) continue;

                    _rayVisualEnabledBeforeMove[i] = visual.enabled;
                    visual.enabled = false;
                }

                _screenMoveRayVisualsHidden = true;
                return;
            }

            if (!_screenMoveRayVisualsHidden)
                return;

            for (int i = 0; i < rayVisualsToHideWhileMoving.Length; i++)
            {
                Behaviour visual = rayVisualsToHideWhileMoving[i];
                if (visual == null) continue;

                bool wasEnabled = _rayVisualEnabledBeforeMove != null
                    && i < _rayVisualEnabledBeforeMove.Length
                    && _rayVisualEnabledBeforeMove[i];
                visual.enabled = wasEnabled;
            }

            _rayVisualEnabledBeforeMove = null;
            _screenMoveRayVisualsHidden = false;
        }
    }
}
