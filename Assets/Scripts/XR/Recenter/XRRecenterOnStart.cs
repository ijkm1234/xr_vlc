using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using XRVLC.Infrastructure.Pico;

[AddComponentMenu("XR/XR Recenter On Start")]
[RequireComponent(typeof(XROrigin))]
[DefaultExecutionOrder(100)]
public class XRRecenterOnStart : MonoBehaviour
{
    const float TrackingWaitTimeoutSeconds = 3f;
    const int RequiredSynchronizedCameraPoseFrames = 2;
    const float CameraPositionMatchToleranceMeters = 0.05f;
    const float CameraRotationMatchToleranceDegrees = 5f;
    const float WorldForwardAlignmentToleranceDegrees = 2f;

    XROrigin m_XROrigin;
    Coroutine m_StartupRecenterCoroutine;
    bool m_StartupAlignmentFinished;
    bool m_StartCalled;

    void Awake()
    {
        m_XROrigin = GetComponent<XROrigin>();
    }

    void OnEnable()
    {
        PicoRecenterEvents.RecenterSuccess += OnSystemRecenter;

        if (m_StartCalled)
            StartStartupAlignmentIfNeeded();
    }

    void Start()
    {
        m_StartCalled = true;
        StartStartupAlignmentIfNeeded();
    }

    void OnDisable()
    {
        PicoRecenterEvents.RecenterSuccess -= OnSystemRecenter;

        if (m_StartupRecenterCoroutine != null)
        {
            StopCoroutine(m_StartupRecenterCoroutine);
            m_StartupRecenterCoroutine = null;
        }
    }

    void StartStartupAlignmentIfNeeded()
    {
        if (!m_StartupAlignmentFinished && m_StartupRecenterCoroutine == null)
            m_StartupRecenterCoroutine = StartCoroutine(WaitForTrackingAndRecenter());
    }

    IEnumerator WaitForTrackingAndRecenter()
    {
        float waitStartedAt = Time.realtimeSinceStartup;
        bool xrOriginInitialized = false;
        bool xrOriginInitializationReported = false;
        bool sessionFocused = false;
        bool cameraPoseSynchronized = false;
        int synchronizedCameraPoseFrames = 0;
        Debug.Log(
            "[XRRecenterOnStart] Waiting for focused XR session, XROrigin initialization, " +
            "and synchronized HMD camera pose.");

        while (Time.realtimeSinceStartup - waitStartedAt < TrackingWaitTimeoutSeconds)
        {
            xrOriginInitialized = IsXrOriginInitialized();
            if (xrOriginInitialized && !xrOriginInitializationReported)
            {
                xrOriginInitializationReported = true;
                ColdStartSplashOverlay.MarkXrOriginInitialized();
                Debug.Log(
                    $"[XRRecenterOnStart] XROrigin initialized with tracking mode " +
                    $"{m_XROrigin.CurrentTrackingOriginMode}.");
            }

            sessionFocused = PicoSessionEvents.IsFocused;
            bool headTrackingValid = TryGetValidHeadPose(
                out Vector3 headLocalPosition,
                out Quaternion headLocalRotation);
            cameraPoseSynchronized = headTrackingValid &&
                IsCameraPoseSynchronized(headLocalPosition, headLocalRotation);

            if (sessionFocused && xrOriginInitialized && cameraPoseSynchronized)
                synchronizedCameraPoseFrames++;
            else
                synchronizedCameraPoseFrames = 0;

            if (synchronizedCameraPoseFrames < RequiredSynchronizedCameraPoseFrames ||
                !TryRecenter())
            {
                yield return null;
                continue;
            }

            // TrackedPoseDriver 仍会在下一帧写入 Camera transform；等它完成后再确认
            // 本次对齐没有被首个有效头显 pose 覆盖。
            yield return null;

            sessionFocused = PicoSessionEvents.IsFocused;
            bool verificationTrackingValid = TryGetValidHeadPose(
                out headLocalPosition,
                out headLocalRotation);
            cameraPoseSynchronized = verificationTrackingValid &&
                IsCameraPoseSynchronized(headLocalPosition, headLocalRotation);

            if (!sessionFocused || !IsXrOriginInitialized() || !cameraPoseSynchronized ||
                !IsWorldForwardAligned())
            {
                synchronizedCameraPoseFrames = 0;
                continue;
            }

            m_StartupAlignmentFinished = true;
            m_StartupRecenterCoroutine = null;
            ColdStartSplashOverlay.MarkWorldCoordinatesAligned();
            Debug.Log(
                "[XRRecenterOnStart] Focused XR session and synchronized camera pose confirmed; " +
                "world forward aligned to view yaw.");
            yield break;
        }

        m_StartupAlignmentFinished = true;
        m_StartupRecenterCoroutine = null;
        if (!xrOriginInitialized)
            ColdStartSplashOverlay.MarkXrOriginInitializationTimedOut();
        ColdStartSplashOverlay.MarkWorldAlignmentTimedOut();
        Debug.LogWarning(
            $"[XRRecenterOnStart] Focused XR session, XROrigin initialization, or synchronized " +
            $"HMD camera pose did not become valid within " +
            $"{TrackingWaitTimeoutSeconds:0.#} seconds; startup recenter skipped. " +
            $"sessionFocused={sessionFocused}, xrOriginInitialized={xrOriginInitialized}, " +
            $"cameraPoseSynchronized={cameraPoseSynchronized}, " +
            $"synchronizedFrames={synchronizedCameraPoseFrames}, " +
            $"trackingMode={m_XROrigin.CurrentTrackingOriginMode}.");
    }

    bool IsXrOriginInitialized()
    {
        if (m_XROrigin == null)
            return false;

        TrackingOriginModeFlags currentMode = m_XROrigin.CurrentTrackingOriginMode;
        return m_XROrigin.RequestedTrackingOriginMode switch
        {
            XROrigin.TrackingOriginMode.NotSpecified => currentMode != TrackingOriginModeFlags.Unknown,
            XROrigin.TrackingOriginMode.Device =>
                (currentMode & TrackingOriginModeFlags.Device) != 0,
            XROrigin.TrackingOriginMode.Floor =>
                (currentMode & TrackingOriginModeFlags.Floor) != 0,
            XROrigin.TrackingOriginMode.Unbounded =>
                (currentMode & TrackingOriginModeFlags.Unbounded) != 0,
            _ => false
        };
    }

    static bool TryGetValidHeadPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!headDevice.isValid)
            return false;

        if (!headDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) || !isTracked)
            return false;

        if (!headDevice.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState trackingState))
            return false;

        const InputTrackingState requiredTracking =
            InputTrackingState.Position | InputTrackingState.Rotation;
        if ((trackingState & requiredTracking) != requiredTracking)
            return false;

        return headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
            headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
    }

    bool IsCameraPoseSynchronized(Vector3 headLocalPosition, Quaternion headLocalRotation)
    {
        Camera cam = m_XROrigin?.Camera;
        if (cam == null)
            return false;

        Transform cameraTransform = cam.transform;
        float positionError = Vector3.Distance(cameraTransform.localPosition, headLocalPosition);
        float rotationError = Quaternion.Angle(cameraTransform.localRotation, headLocalRotation);
        return positionError <= CameraPositionMatchToleranceMeters &&
            rotationError <= CameraRotationMatchToleranceDegrees;
    }

    bool IsWorldForwardAligned()
    {
        Camera cam = m_XROrigin?.Camera;
        if (cam == null)
            return false;

        Vector3 projectedForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (projectedForward.sqrMagnitude < 0.0001f)
            return false;

        return Vector3.Angle(projectedForward, Vector3.forward) <=
            WorldForwardAlignmentToleranceDegrees;
    }

    // 长按 Home：OS 已重置追踪空间，XROrigin 归零即对齐
    void OnSystemRecenter()
    {
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    public void DoRecenter()
    {
        TryRecenter();
    }

    bool TryRecenter()
    {
        Camera cam = m_XROrigin.Camera;
        if (cam == null)
            return false;

        // 将相机移至世界原点（含 Y）
        transform.position -= cam.transform.position;
        // 还原 Device mode 的 CameraYOffset，使 XROrigin.Y 回到 0
        transform.position += Vector3.up * m_XROrigin.CameraYOffset;

        // 仅修正水平朝向，保持世界 Y 轴竖直。
        return m_XROrigin.MatchOriginUpCameraForward(Vector3.up, Vector3.forward);
    }
}
