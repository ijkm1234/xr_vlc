using System;
using UnityEngine;

public static class ExternalMediaIntentBridge
{
    private const string ActionView = "android.intent.action.VIEW";
    private const string ExtraExternalMedia = "org.videolan.vlc.extra.XR_EXTERNAL_MEDIA";
    private const string ExtraExternalMediaToken = "org.videolan.vlc.extra.XR_EXTERNAL_MEDIA_TOKEN";
    private const string ExtraExternalMediaConsumed = "org.videolan.vlc.extra.XR_EXTERNAL_MEDIA_CONSUMED";
    private const string ExtraExternalMediaTitle = "org.videolan.vlc.extra.XR_EXTERNAL_MEDIA_TITLE";
    private const string ExternalPlaybackSource = "external";
    private const string VideoMediaType = "video";

    private static string s_LastDeepLinkUrl;

    public static bool TryConsumeCurrentIntent(out string payload)
    {
        payload = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (currentActivity == null) return false;

                using (AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent"))
                {
                    if (intent == null) return false;

                    string action = intent.Call<string>("getAction");
                    string dataString = intent.Call<string>("getDataString");
                    string title = intent.Call<string>("getStringExtra", ExtraExternalMediaTitle);
                    bool isExternalMedia = intent.Call<bool>("getBooleanExtra", ExtraExternalMedia, false);
                    bool isConsumed = intent.Call<bool>("getBooleanExtra", ExtraExternalMediaConsumed, false);

                    if (action != ActionView || string.IsNullOrEmpty(dataString) || !isExternalMedia || isConsumed)
                        return false;

                    payload = BuildStartPlayPayload(dataString, title);
                    intent.Call<AndroidJavaObject>("putExtra", ExtraExternalMediaConsumed, true);
                    s_LastDeepLinkUrl = dataString;
                    Debug.Log($"[ExternalMediaIntentBridge] Consumed external media intent: {RedactUri(dataString)}");
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ExternalMediaIntentBridge] Failed to consume Android media intent: " + e.Message);
            return false;
        }
#else
        return false;
#endif
    }

    public static bool TryBuildPayloadFromUrl(string url, out string payload)
    {
        payload = null;

        if (string.IsNullOrEmpty(url) || url == s_LastDeepLinkUrl)
            return false;

        s_LastDeepLinkUrl = url;
        payload = BuildStartPlayPayload(url, null);
        Debug.Log($"[ExternalMediaIntentBridge] Consumed external media deep link: {RedactUri(url)}");
        return true;
    }

    private static string BuildStartPlayPayload(string uri, string title)
    {
        return JsonUtility.ToJson(new ExternalMediaPayload
        {
            uri = uri,
            title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            index = 0,
            source = ExternalPlaybackSource,
            mediaType = VideoMediaType
        });
    }

    private static string RedactUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return string.Empty;

        int queryStart = uri.IndexOf('?', StringComparison.Ordinal);
        return queryStart >= 0 ? uri.Substring(0, queryStart) + "?..." : uri;
    }

    [Serializable]
    private class ExternalMediaPayload
    {
        public string uri;
        public string title;
        public int index;
        public string source;
        public string mediaType;
    }
}
