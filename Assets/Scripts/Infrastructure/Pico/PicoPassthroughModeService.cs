using System;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.XR.PXR;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR && PICO_OPENXR_SDK
using Unity.XR.OpenXR.Features.PICOSupport;
#endif

namespace XRVLC.Infrastructure.Pico
{
    /// <summary>
    /// 封装 PICO Video See Through 状态，避免 UI 层直接依赖 PICO SDK 调用。
    /// </summary>
    public class PicoPassthroughModeService : MonoBehaviour
    {
        private bool _isEnabled;
        private bool _editorNoticeLogged;

        public event Action<bool> StateChanged;

        public bool IsEnabled => _isEnabled;

        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_OPENXR_SDK
                return PassthroughFeature.isExtensionEnable && PassthroughFeature.IsPassthroughSupported();
#elif UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return true;
#endif
            }
        }

        public bool Toggle()
        {
            return SetEnabled(!_isEnabled);
        }

        public bool SetEnabled(bool enabled)
        {
            if (enabled && !IsSupported)
            {
                SetCachedState(false);
                return false;
            }

            if (!ApplyNativeState(enabled))
            {
                SetCachedState(false);
                return false;
            }

            SetCachedState(enabled);
            return true;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && _isEnabled)
                ApplyNativeState(true);
        }

        private void SetCachedState(bool enabled)
        {
            if (_isEnabled == enabled) return;

            _isEnabled = enabled;
            StateChanged?.Invoke(_isEnabled);
        }

        private bool ApplyNativeState(bool enabled)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_OPENXR_SDK
            PassthroughFeature.EnableVideoSeeThrough = enabled;
            return PassthroughFeature.EnableVideoSeeThrough == enabled;
#elif UNITY_ANDROID && !UNITY_EDITOR
            PXR_Manager.EnableVideoSeeThrough = enabled;
            return PXR_Manager.EnableVideoSeeThrough == enabled;
#else
            if (!_editorNoticeLogged)
            {
                Debug.Log("[PicoPassthroughModeService] 非 PICO 运行环境，仅记录透视请求状态。");
                _editorNoticeLogged = true;
            }
            return true;
#endif
        }
    }
}
