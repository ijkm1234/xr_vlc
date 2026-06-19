using UnityEngine;

namespace XRVLC.Infrastructure.VlcBridge.Preferences
{
    public static class VlcPreferenceStore
    {
        private const int ModePrivate = 0;

        /// <summary>
        /// 从 VLC 默认 SharedPreferences 中读取整型配置；编辑器环境直接返回默认值。
        /// </summary>
        public static int GetInt(string key, int defaultValue = 0)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject prefs = GetPreferences())
            {
                return prefs.Call<int>("getInt", key, defaultValue);
            }
#else
            return defaultValue;
#endif
        }

        /// <summary>
        /// 从 VLC 默认 SharedPreferences 中读取字符串配置；用于共享按键映射 JSON。
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject prefs = GetPreferences())
            {
                return prefs.Call<string>("getString", key, defaultValue);
            }
#else
            return defaultValue;
#endif
        }

        /// <summary>
        /// 从 VLC 默认 SharedPreferences 中读取布尔配置，保留给其他 Unity 脚本复用。
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject prefs = GetPreferences())
            {
                return prefs.Call<bool>("getBoolean", key, defaultValue);
            }
#else
            return defaultValue;
#endif
        }

        /// <summary>
        /// 从 VLC 默认 SharedPreferences 中读取浮点配置，保留给其他 Unity 脚本复用。
        /// </summary>
        public static float GetFloat(string key, float defaultValue = 0f)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject prefs = GetPreferences())
            {
                return prefs.Call<float>("getFloat", key, defaultValue);
            }
#else
            return defaultValue;
#endif
        }

        /// <summary>
        /// 写入整型配置并立即 apply，让 VLC 和 Unity 后续读取到同一份值。
        /// </summary>
        public static void PutInt(string key, int value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject prefs = GetPreferences())
            using (AndroidJavaObject editor = prefs.Call<AndroidJavaObject>("edit"))
            {
                editor.Call<AndroidJavaObject>("putInt", key, value);
                editor.Call("apply");
            }
#endif
        }

        /// <summary>
        /// 写入字符串配置并立即 apply；当前主要用于 xr_button_mappings JSON。
        /// </summary>
        public static void PutString(string key, string value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject prefs = GetPreferences())
            using (AndroidJavaObject editor = prefs.Call<AndroidJavaObject>("edit"))
            {
                editor.Call<AndroidJavaObject>("putString", key, value);
                editor.Call("apply");
            }
#endif
        }

        /// <summary>
        /// 写入布尔配置并立即 apply，保留给其他 Unity 设置面板复用。
        /// </summary>
        public static void PutBool(string key, bool value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject prefs = GetPreferences())
            using (AndroidJavaObject editor = prefs.Call<AndroidJavaObject>("edit"))
            {
                editor.Call<AndroidJavaObject>("putBoolean", key, value);
                editor.Call("apply");
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 通过当前 Activity 打开与 VLC AAR 相同的 packageName_preferences 文件。
        /// </summary>
        private static AndroidJavaObject GetPreferences()
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                string packageName = activity.Call<string>("getPackageName");
                return activity.Call<AndroidJavaObject>("getSharedPreferences", packageName + "_preferences", ModePrivate);
            }
        }
#endif
    }
}
