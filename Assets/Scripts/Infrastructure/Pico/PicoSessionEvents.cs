using System;
#if UNITY_ANDROID
using Unity.XR.PXR;
#endif

namespace XRVLC.Infrastructure.Pico
{
    public static class PicoSessionEvents
    {
#if UNITY_ANDROID
        /// <summary>
        /// 转发 PICO OpenXR session 状态变化，隐藏具体 SDK 事件来源。
        /// </summary>
        public static event Action<XrSessionState> SessionStateChanged
        {
            add => PXR_Plugin.System.SessionStateChanged += value;
            remove => PXR_Plugin.System.SessionStateChanged -= value;
        }
#else
        /// <summary>
        /// 非 Android 平台下的空事件，保证编辑器编译时调用方无需额外分支。
        /// </summary>
        public static event Action<int> SessionStateChanged { add { } remove { } }
#endif
    }
}
