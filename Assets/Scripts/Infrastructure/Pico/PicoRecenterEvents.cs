using System;
#if UNITY_ANDROID
using Unity.XR.PXR;
#endif

namespace XRVLC.Infrastructure.Pico
{
    public static class PicoRecenterEvents
    {
        /// <summary>
        /// 转发 PICO 系统 recenter 成功事件，隔离业务脚本对 PICO SDK 的直接依赖。
        /// </summary>
        public static event Action RecenterSuccess
        {
#if UNITY_ANDROID
            add => PXR_Plugin.System.RecenterSuccess += value;
            remove => PXR_Plugin.System.RecenterSuccess -= value;
#else
            add { }
            remove { }
#endif
        }
    }
}
