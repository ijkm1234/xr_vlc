using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class VlcFocusRestoreHandler
{
    private readonly MonoBehaviour _owner;
    private readonly Func<GameObject[]> _objectsProvider;
    private readonly Func<XRInputModalityManager> _modalityManagerProvider;
    private readonly int _restoreDelayFrames;

    private Coroutine _restoreCoroutine;
    private bool _hiddenByFocusLoss;
    private GameObject[] m_HiddenObjects = Array.Empty<GameObject>();
    private Renderer[] m_HiddenRenderers = Array.Empty<Renderer>();
    private bool[] m_HiddenRendererWasEnabled = Array.Empty<bool>();
    private Behaviour[] m_HiddenVisualBehaviours = Array.Empty<Behaviour>();
    private bool[] m_HiddenVisualBehaviourWasEnabled = Array.Empty<bool>();
    private bool? m_ModalityManagerWasEnabled;

    public VlcFocusRestoreHandler(
        MonoBehaviour owner,
        Func<GameObject[]> objectsProvider,
        Func<XRInputModalityManager> modalityManagerProvider,
        int restoreDelayFrames)
    {
        _owner = owner;
        _objectsProvider = objectsProvider;
        _modalityManagerProvider = modalityManagerProvider;
        _restoreDelayFrames = restoreDelayFrames;
    }

    /// <summary>
    /// VLC Activity 获取焦点时隐藏 Unity 控制器可视状态，并暂停输入模态管理器。
    /// </summary>
    public void HideControllers()
    {
        if (_hiddenByFocusLoss)
        {
            if (_restoreCoroutine != null)
                CancelRestore();

            Debug.Log("[VlcFocusRestore] HideControllers ignored; controllers are already hidden by focus loss.");
            return;
        }

        _hiddenByFocusLoss = true;
        CancelRestore();

        GameObject[] objects = _objectsProvider?.Invoke() ?? Array.Empty<GameObject>();
        m_HiddenObjects = objects;
        CaptureVisualState(objects);

        XRInputModalityManager modalityManager = _modalityManagerProvider?.Invoke();
        m_ModalityManagerWasEnabled = modalityManager != null ? (bool?)modalityManager.enabled : null;

        Debug.Log($"[VlcFocusRestore] HideControllers begin; objectCount={objects.Length}; restoreDelayFrames={_restoreDelayFrames}; XRInputModalityManager={DescribeModalityManager(modalityManager)}");

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            Debug.Log($"[VlcFocusRestore] Hide target[{i}] before hide: {DescribeObject(obj)}; rayVisuals={DescribeRayVisuals(obj)}");
        }

        if (modalityManager != null)
        {
            modalityManager.enabled = false;
            Debug.Log($"[VlcFocusRestore] Disabled XRInputModalityManager; before={m_ModalityManagerWasEnabled.Value}; after={modalityManager.enabled}");
        }

        HideVisualState();

        for (int i = 0; i < objects.Length; i++)
            Debug.Log($"[VlcFocusRestore] Hide target[{i}] after hide: {DescribeObject(objects[i])}; rayVisuals={DescribeRayVisuals(objects[i])}");

        Debug.Log($"[VlcFocusRestore] HideControllers end; XRInputModalityManager={DescribeModalityManager(modalityManager)}");
    }

    /// <summary>
    /// Unity 重新获得焦点后延迟数帧恢复控制器可视状态，避开系统焦点切换瞬间的输入抖动。
    /// </summary>
    public void TriggerRestore()
    {
        if (!_hiddenByFocusLoss)
        {
            Debug.Log("[VlcFocusRestore] TriggerRestore ignored; controllers were not hidden by focus loss.");
            return;
        }

        Debug.Log($"[VlcFocusRestore] TriggerRestore begin; storedObjectCount={m_HiddenObjects.Length}; owner={DescribeOwner(_owner)}");
        CancelRestore();
        if (_owner != null)
        {
            _restoreCoroutine = _owner.StartCoroutine(RestoreAfterFrames());
        }
        else
        {
            Debug.LogWarning("[VlcFocusRestore] TriggerRestore cannot start restore coroutine because owner is null.");
        }
    }

    private void CancelRestore()
    {
        if (_restoreCoroutine == null || _owner == null) return;
        Debug.Log("[VlcFocusRestore] Cancel pending restore coroutine.");
        _owner.StopCoroutine(_restoreCoroutine);
        _restoreCoroutine = null;
    }

    private IEnumerator RestoreAfterFrames()
    {
        Debug.Log($"[VlcFocusRestore] RestoreAfterFrames begin; delayFrames={_restoreDelayFrames}; storedObjectCount={m_HiddenObjects.Length}; XRInputModalityManager={DescribeModalityManager(_modalityManagerProvider?.Invoke())}");

        for (int i = 0; i < _restoreDelayFrames; i++)
            yield return null;

        GameObject[] objects = m_HiddenObjects ?? Array.Empty<GameObject>();
        if (objects.Length == 0)
            objects = _objectsProvider?.Invoke() ?? Array.Empty<GameObject>();

        for (int i = 0; i < objects.Length; i++)
            Debug.Log($"[VlcFocusRestore] Restore target[{i}] before restore: {DescribeObject(objects[i])}; rayVisuals={DescribeRayVisuals(objects[i])}");

        XRInputModalityManager modalityManager = _modalityManagerProvider?.Invoke();
        if (modalityManager != null)
        {
            bool before = modalityManager.enabled;
            if (m_ModalityManagerWasEnabled.HasValue)
                modalityManager.enabled = m_ModalityManagerWasEnabled.Value;
            else
                modalityManager.enabled = true;

            Debug.Log($"[VlcFocusRestore] Restored XRInputModalityManager; before={before}; restored={(m_ModalityManagerWasEnabled.HasValue ? m_ModalityManagerWasEnabled.Value.ToString() : "true fallback")}; after={modalityManager.enabled}");
        }

        RestoreVisualState();
        EnsureRayVisualDriversCanRender(objects);
        _hiddenByFocusLoss = false;

        for (int i = 0; i < objects.Length; i++)
            Debug.Log($"[VlcFocusRestore] Restore target[{i}] after restore: {DescribeObject(objects[i])}; rayVisuals={DescribeRayVisuals(objects[i])}");

        _restoreCoroutine = null;
        m_HiddenObjects = Array.Empty<GameObject>();
        m_HiddenRenderers = Array.Empty<Renderer>();
        m_HiddenRendererWasEnabled = Array.Empty<bool>();
        m_HiddenVisualBehaviours = Array.Empty<Behaviour>();
        m_HiddenVisualBehaviourWasEnabled = Array.Empty<bool>();
        m_ModalityManagerWasEnabled = null;
        Debug.Log("[VlcFocusRestore] RestoreAfterFrames end.");
    }

    private void CaptureVisualState(GameObject[] objects)
    {
        var renderers = new List<Renderer>();
        var visualBehaviours = new List<Behaviour>();

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null)
                continue;

            renderers.AddRange(obj.GetComponentsInChildren<Renderer>(true));

            Behaviour[] behaviours = obj.GetComponentsInChildren<Behaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                Behaviour behaviour = behaviours[j];
                if (behaviour != null && IsVisualStateBehaviour(behaviour.GetType().Name))
                    visualBehaviours.Add(behaviour);
            }
        }

        m_HiddenRenderers = renderers.ToArray();
        m_HiddenRendererWasEnabled = new bool[m_HiddenRenderers.Length];
        for (int i = 0; i < m_HiddenRenderers.Length; i++)
            m_HiddenRendererWasEnabled[i] = m_HiddenRenderers[i] != null && m_HiddenRenderers[i].enabled;

        m_HiddenVisualBehaviours = visualBehaviours.ToArray();
        m_HiddenVisualBehaviourWasEnabled = new bool[m_HiddenVisualBehaviours.Length];
        for (int i = 0; i < m_HiddenVisualBehaviours.Length; i++)
            m_HiddenVisualBehaviourWasEnabled[i] = m_HiddenVisualBehaviours[i] != null && m_HiddenVisualBehaviours[i].enabled;

        Debug.Log($"[VlcFocusRestore] Captured visual state; renderers={m_HiddenRenderers.Length}; visualBehaviours={m_HiddenVisualBehaviours.Length}");
    }

    private void HideVisualState()
    {
        for (int i = 0; i < m_HiddenVisualBehaviours.Length; i++)
        {
            Behaviour behaviour = m_HiddenVisualBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = false;
        }

        for (int i = 0; i < m_HiddenRenderers.Length; i++)
        {
            Renderer renderer = m_HiddenRenderers[i];
            if (renderer != null)
                renderer.enabled = false;
        }
    }

    private void RestoreVisualState()
    {
        for (int i = 0; i < m_HiddenVisualBehaviours.Length; i++)
        {
            Behaviour behaviour = m_HiddenVisualBehaviours[i];
            if (behaviour == null)
                continue;

            behaviour.enabled = i < m_HiddenVisualBehaviourWasEnabled.Length && m_HiddenVisualBehaviourWasEnabled[i];
        }

        for (int i = 0; i < m_HiddenRenderers.Length; i++)
        {
            Renderer renderer = m_HiddenRenderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = i < m_HiddenRendererWasEnabled.Length && m_HiddenRendererWasEnabled[i];
        }
    }

    private static void EnsureRayVisualDriversCanRender(GameObject[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null)
                continue;

            LineRenderer[] lineRenderers = obj.GetComponentsInChildren<LineRenderer>(true);
            for (int j = 0; j < lineRenderers.Length; j++)
            {
                LineRenderer lineRenderer = lineRenderers[j];
                if (lineRenderer != null && IsRayLineRenderer(lineRenderer) && HasEnabledRayVisualDriver(lineRenderer.gameObject))
                    lineRenderer.enabled = true;
            }
        }
    }

    private static bool IsRayLineRenderer(Renderer renderer)
    {
        return renderer is LineRenderer;
    }

    private static bool HasEnabledRayVisualDriver(GameObject obj)
    {
        if (obj == null)
            return false;

        Behaviour[] behaviours = obj.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName.IndexOf("CurveVisual", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("InteractorLineVisual", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("LineVisual", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string DescribeOwner(MonoBehaviour owner)
    {
        return owner == null ? "<null>" : $"{owner.GetType().Name}@{GetPath(owner.gameObject)}";
    }

    private static string DescribeModalityManager(XRInputModalityManager modalityManager)
    {
        if (modalityManager == null)
            return "<null>";

        return $"{modalityManager.GetType().Name}@{GetPath(modalityManager.gameObject)} enabled={modalityManager.enabled} activeSelf={modalityManager.gameObject.activeSelf} activeInHierarchy={modalityManager.gameObject.activeInHierarchy}";
    }

    private static string DescribeObject(GameObject obj)
    {
        if (obj == null)
            return "<null>";

        return $"{GetPath(obj)} activeSelf={obj.activeSelf} activeInHierarchy={obj.activeInHierarchy}";
    }

    private static string DescribeRayVisuals(GameObject obj)
    {
        if (obj == null)
            return "<null>";

        var builder = new StringBuilder();
        LineRenderer[] lineRenderers = obj.GetComponentsInChildren<LineRenderer>(true);
        builder.Append("LineRenderer[");
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            if (i > 0)
                builder.Append("; ");

            LineRenderer lineRenderer = lineRenderers[i];
            builder.Append(GetPath(lineRenderer.gameObject));
            builder.Append(" enabled=").Append(lineRenderer.enabled);
            builder.Append(" activeSelf=").Append(lineRenderer.gameObject.activeSelf);
            builder.Append(" activeInHierarchy=").Append(lineRenderer.gameObject.activeInHierarchy);
        }

        builder.Append("] RayBehaviours[");
        bool hasRayBehaviour = false;
        Behaviour[] behaviours = obj.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;
            if (!IsRayRelatedBehaviour(typeName))
                continue;

            if (hasRayBehaviour)
                builder.Append("; ");

            hasRayBehaviour = true;
            builder.Append(typeName);
            builder.Append("@").Append(GetPath(behaviour.gameObject));
            builder.Append(" enabled=").Append(behaviour.enabled);
            builder.Append(" activeSelf=").Append(behaviour.gameObject.activeSelf);
            builder.Append(" activeInHierarchy=").Append(behaviour.gameObject.activeInHierarchy);
        }

        builder.Append("]");
        return builder.ToString();
    }

    private static bool IsRayRelatedBehaviour(string typeName)
    {
        return typeName.IndexOf("Ray", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("Curve", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("Interactor", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsVisualStateBehaviour(string typeName)
    {
        return typeName == "NearFarReticleVisual"
            || typeName.IndexOf("CurveVisual", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("InteractorLineVisual", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("LineVisual", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetPath(GameObject obj)
    {
        if (obj == null)
            return "<null>";

        Transform transform = obj.transform;
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}
