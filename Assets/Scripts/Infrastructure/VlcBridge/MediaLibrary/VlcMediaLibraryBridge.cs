using System;
using UnityEngine;

namespace XRVLC.Media
{
    /// <summary>
    /// 封装 C# 层的 VlcMediaLibraryBridge，用于写入 Android 层播放历史。
    /// </summary>
    public static class VlcMediaLibraryBridge
    {
        private static AndroidJavaObject GetMedialibraryInstance()
        {
            if (Application.platform != RuntimePlatform.Android) return null;

            try
            {
                using (var mlClass = new AndroidJavaClass("org.videolan.medialibrary.interfaces.Medialibrary"))
                {
                    return mlClass.CallStatic<AndroidJavaObject>("getInstance");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VlcMediaLibraryBridge] 获取 Medialibrary 实例失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 插入/更新历史记录
        /// </summary>
        public static void AddToHistory(string uri, string title)
        {
            if (Application.platform != RuntimePlatform.Android || string.IsNullOrEmpty(uri)) return;

            try
            {
                using (var ml = GetMedialibraryInstance())
                {
                    if (ml != null)
                    {
                        ml.Call<bool>("addToHistory", uri, title);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VlcMediaLibraryBridge] AddToHistory 异常: {e.Message}");
            }
        }
    }
}
