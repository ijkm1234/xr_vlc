using System;
using System.Collections;
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
    /// VLC Activity 获取焦点时隐藏 Unity 控制器对象，并暂停输入模态管理器。
    /// </summary>
    public void HideControllers()
    {
        if (_hiddenByFocusLoss) return;
        _hiddenByFocusLoss = true;
        CancelRestore();

        XRInputModalityManager modalityManager = _modalityManagerProvider?.Invoke();
        if (modalityManager != null)
            modalityManager.enabled = false;

        GameObject[] objects = _objectsProvider?.Invoke();
        if (objects == null) return;
        foreach (GameObject obj in objects)
            obj?.SetActive(false);
    }

    /// <summary>
    /// Unity 重新获得焦点后延迟数帧恢复控制器，避开系统焦点切换瞬间的输入抖动。
    /// </summary>
    public void TriggerRestore()
    {
        if (!_hiddenByFocusLoss) return;
        _hiddenByFocusLoss = false;
        CancelRestore();
        if (_owner != null)
            _restoreCoroutine = _owner.StartCoroutine(RestoreAfterFrames());
    }

    private void CancelRestore()
    {
        if (_restoreCoroutine == null || _owner == null) return;
        _owner.StopCoroutine(_restoreCoroutine);
        _restoreCoroutine = null;
    }

    private IEnumerator RestoreAfterFrames()
    {
        for (int i = 0; i < _restoreDelayFrames; i++)
            yield return null;

        GameObject[] objects = _objectsProvider?.Invoke();
        if (objects != null)
        {
            foreach (GameObject obj in objects)
                obj?.SetActive(true);
        }

        XRInputModalityManager modalityManager = _modalityManagerProvider?.Invoke();
        if (modalityManager != null)
            modalityManager.enabled = true;

        _restoreCoroutine = null;
    }
}
