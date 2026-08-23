using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using XRVLC.Infrastructure.Pico;

[AddComponentMenu("XR/XR Recenter On Start")]
[RequireComponent(typeof(XROrigin))]
public class XRRecenterOnStart : MonoBehaviour
{
    const float TrackingWaitTimeoutSeconds = 3f;

    XROrigin m_XROrigin;
    Coroutine m_StartupRecenterCoroutine;
    bool m_StartupAlignmentFinished;

    void Awake()
    {
        m_XROrigin = GetComponent<XROrigin>();
    }

    void OnEnable()
    {
        PicoRecenterEvents.RecenterSuccess += OnSystemRecenter;

        if (!m_StartupAlignmentFinished && m_StartupRecenterCoroutine == null)
            m_StartupRecenterCoroutine = StartCoroutine(WaitForTrackingAndRecenter());
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

    IEnumerator WaitForTrackingAndRecenter()
    {
        float waitStartedAt = Time.realtimeSinceStartup;
        Debug.Log("[XRRecenterOnStart] Waiting for valid HMD position and rotation tracking.");

        while (Time.realtimeSinceStartup - waitStartedAt < TrackingWaitTimeoutSeconds)
        {
            if (IsHeadTrackingValid() && TryRecenter())
            {
                m_StartupAlignmentFinished = true;
                m_StartupRecenterCoroutine = null;
                ColdStartSplashOverlay.MarkWorldCoordinatesAligned();
                Debug.Log("[XRRecenterOnStart] HMD tracking is valid; world forward aligned to view yaw.");
                yield break;
            }

            yield return null;
        }

        m_StartupAlignmentFinished = true;
        m_StartupRecenterCoroutine = null;
        ColdStartSplashOverlay.MarkWorldAlignmentTimedOut();
        Debug.LogWarning($"[XRRecenterOnStart] HMD tracking did not become valid within {TrackingWaitTimeoutSeconds:0.#} seconds; startup recenter skipped.");
    }

    static bool IsHeadTrackingValid()
    {
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

        return headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out _) &&
            headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out _);
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
