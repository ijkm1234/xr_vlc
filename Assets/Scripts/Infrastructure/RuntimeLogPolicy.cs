using UnityEngine;

namespace XRVLC.Infrastructure
{
    internal static class RuntimeLogPolicy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            Debug.unityLogger.logEnabled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.unityLogger.filterLogType = LogType.Log;
#else
            Debug.unityLogger.filterLogType = LogType.Warning;
#endif
        }
    }
}
