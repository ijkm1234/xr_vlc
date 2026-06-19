using System.Collections;
using Unity.XR.CoreUtils;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using XRVLC.Infrastructure.Pico;

[AddComponentMenu("XR/XR Recenter On Start")]
[RequireComponent(typeof(XROrigin))]
public class XRRecenterOnStart : MonoBehaviour
{
    XROrigin m_XROrigin;
    XRInputSubsystem m_Subsystem;
    bool m_Recentered;

    void Awake()
    {
        m_XROrigin = GetComponent<XROrigin>();
    }

    void OnEnable()
    {
        m_Subsystem = XRGeneralSettings.Instance?.Manager?.activeLoader
            ?.GetLoadedSubsystem<XRInputSubsystem>();
        if (m_Subsystem != null)
            m_Subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
        PicoRecenterEvents.RecenterSuccess += OnSystemRecenter;
    }

    void OnDisable()
    {
        if (m_Subsystem != null)
            m_Subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
        PicoRecenterEvents.RecenterSuccess -= OnSystemRecenter;
    }

    // 启动时追踪模式切换完成，等一帧数据稳定后对齐朝向
    void OnTrackingOriginUpdated(XRInputSubsystem _)
    {
        if (m_Recentered) return;
        m_Recentered = true;
        StartCoroutine(RecenterNextFrame());
    }

    IEnumerator RecenterNextFrame()
    {
        // 等待数帧，确保新追踪模式下的姿态数据稳定
        yield return null;
        DoRecenter();
    }

    // 长按 Home：OS 已重置追踪空间，XROrigin 归零即对齐
    void OnSystemRecenter()
    {
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    public void DoRecenter()
    {
        var cam = m_XROrigin.Camera;
        if (cam == null) return;

        // 将相机移至世界原点（含 Y）
        transform.position -= cam.transform.position;
        // 还原 Device mode 的 CameraYOffset，使 XROrigin.Y 回到 0
        transform.position += Vector3.up * m_XROrigin.CameraYOffset;

        // 修正朝向
        m_XROrigin.MatchOriginUpCameraForward(Vector3.up, Vector3.forward);
    }
}
