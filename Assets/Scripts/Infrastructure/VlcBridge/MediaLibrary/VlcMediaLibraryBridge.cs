using System;
using UnityEngine;

namespace XRVLC.Media
{
    /// <summary>
    /// 封装 C# 层的 VlcMediaLibraryBridge，用于直接调用 Android 层的 Medialibrary
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
        /// 获取上次播放进度 (毫秒)，若无记录返回 0
        /// </summary>
        public static long GetLastTime(string uri)
        {
            if (Application.platform != RuntimePlatform.Android || string.IsNullOrEmpty(uri)) return 0;

            try
            {
                using (var ml = GetMedialibraryInstance())
                {
                    if (ml != null)
                    {
                        using (var media = ml.Call<AndroidJavaObject>("getMedia", uri))
                        {
                            if (media != null)
                            {
                                long time = media.Call<long>("getTime"); // 返回上次的播放时间
                                Debug.Log($"[VlcMediaLibraryBridge] GetLastTime 成功读取 uri={XRVLC.Utils.UriUtils.RedactUri(uri)}, time={time} ms");
                                return time;
                            }
                            else
                            {
                                Debug.Log($"[VlcMediaLibraryBridge] GetLastTime 无法在媒体库中找到对应的 uri={XRVLC.Utils.UriUtils.RedactUri(uri)}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VlcMediaLibraryBridge] GetLastTime 异常: {e.Message}");
            }
            return 0;
        }

        /// <summary>
        /// 保存当前播放进度
        /// </summary>
        public static void SetLastTime(string uri, long timeMs, bool silent = false)
        {
            if (Application.platform != RuntimePlatform.Android || string.IsNullOrEmpty(uri)) return;

            try
            {
                using (var ml = GetMedialibraryInstance())
                {
                    if (ml != null)
                    {
                        using (var media = ml.Call<AndroidJavaObject>("getMedia", uri))
                        {
                            if (media != null)
                            {
                                long mediaId = media.Call<long>("getId");
                                if (mediaId > 0)
                                {
                                    ml.Call<int>("setLastTime", mediaId, timeMs);
                                    if (!silent) Debug.Log($"[VlcMediaLibraryBridge] SetLastTime 成功写入 uri={XRVLC.Utils.UriUtils.RedactUri(uri)}, id={mediaId}, timeMs={timeMs}");
                                }
                                else
                                {
                                    if (!silent) Debug.LogWarning($"[VlcMediaLibraryBridge] SetLastTime 失败，获取到的 mediaId={mediaId} 无效");
                                }
                            }
                            else
                            {
                                if (!silent) Debug.LogWarning($"[VlcMediaLibraryBridge] SetLastTime 失败，未能在库中找到对应的 media 对象，uri={XRVLC.Utils.UriUtils.RedactUri(uri)}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!silent) Debug.LogWarning($"[VlcMediaLibraryBridge] SetLastTime 异常: {e.Message}");
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