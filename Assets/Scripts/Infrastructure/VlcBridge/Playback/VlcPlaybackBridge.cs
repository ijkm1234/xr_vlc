using System;
using System.Collections.Generic;
using UnityEngine;
using XRVLC.Media;

/// <summary>
/// AAR Playback Bridge
/// 负责与 Android 原生 PlaybackService 进行双向通信。
/// 既包含调用 AAR 的方法，也作为 UnitySendMessage 的接收目标（挂载在全局 GameObject 上）。
/// </summary>
public class VlcPlaybackBridge : MonoBehaviour
{
    private const string BridgeClassName = "org.videolan.vlc.bridge.PlaybackServiceBridge";
    private static VlcPlaybackBridge _instance;
    public static VlcPlaybackSnapshot Snapshot { get; } = new VlcPlaybackSnapshot();

    // --- 事件回调 ---
    public static event Action<int, int> OnVideoSizeChangedEvent;
    public static event Action<string> OnStateChangedEvent;
    public static event Action<long> OnTimeChangedEvent;
    public static event Action<float> OnPositionChangedEvent;
    public static event Action<long> OnLengthChangedEvent;
    public static event Action<float> OnBufferingEvent;
    
    // 轨道回调事件：List<TrackInfo>
    public static event Action<List<XRVLC.Media.TrackInfo>> OnAudioTracksChangedEvent;
    public static event Action<List<XRVLC.Media.TrackInfo>> OnSubtitleTracksChangedEvent;
    public static event Action<SubtitleCue> OnSubtitleCueEvent;
    
    // 播放请求事件
    public static event Action<XRVLC.Media.MediaWrapper> OnPlayRequestedEvent;

    // 媒体解析完成事件（携带尺寸、投影、时长）
    public static event Action<VlcMediaParseResult> OnMediaParseFinishedEvent;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
            // 确保 GameObject 名字与 AAR 发送的目标名字一致
            gameObject.name = "VlcPlaybackBridge";
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    // ==========================================
    // 1. 调用 Android AAR 侧的方法 (JNI)
    // ==========================================

    public static void PreloadLocation(string payload)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("preloadLocation", payload);
                Debug.Log($"[VlcPlaybackBridge] PreloadLocation called for: {payload}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] PreloadLocation failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] PreloadLocation is only supported on Android devices.");
#endif
    }

    public static void SetSurface(IntPtr surfacePtr)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (AndroidJNI.AttachCurrentThread() != 0)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to attach JNI thread");
                return;
            }

            IntPtr bridgeClass = AndroidJNI.FindClass("org/videolan/vlc/bridge/PlaybackServiceBridge");
            if (bridgeClass == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find PlaybackServiceBridge class");
                return;
            }

            IntPtr setSurfaceMethod = AndroidJNI.GetStaticMethodID(bridgeClass, "setVideoSurface", "(Landroid/view/Surface;)V");
            if (setSurfaceMethod == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find setVideoSurface method");
                return;
            }

            jvalue[] args = new jvalue[1];
            args[0].l = surfacePtr;
            AndroidJNI.CallStaticVoidMethod(bridgeClass, setSurfaceMethod, args);
            
            Debug.Log("[VlcPlaybackBridge] SetSurface called successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSurface failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSurface is only supported on Android device.");
#endif
    }

    public static void DetachSurface()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setVideoSurface", (AndroidJavaObject)null);
                Debug.Log("[VlcPlaybackBridge] DetachSurface called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] DetachSurface failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] DetachSurface is only supported on Android device.");
#endif
    }

    public static void Play()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("play");
                Debug.Log("[VlcPlaybackBridge] Play called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Play failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Play is only supported on Android device.");
#endif
    }

    public static void Pause()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("pause");
                Debug.Log("[VlcPlaybackBridge] Pause called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Pause failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Pause is only supported on Android device.");
#endif
    }

    public static void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("stop");
                Debug.Log("[VlcPlaybackBridge] Stop called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Stop failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Stop is only supported on Android device.");
#endif
    }

    public static void Next()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("next");
                Debug.Log("[VlcPlaybackBridge] Next called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Next failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Next is only supported on Android device.");
#endif
    }

    public static void Previous()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("previous");
                Debug.Log("[VlcPlaybackBridge] Previous called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Previous failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Previous is only supported on Android device.");
#endif
    }

    public static void SetRepeatMode(int mode)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setRepeatMode", mode);
                Debug.Log($"[VlcPlaybackBridge] SetRepeatMode called with mode: {mode}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetRepeatMode failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetRepeatMode is only supported on Android device.");
#endif
    }

    public static void SetShuffle(bool shuffle)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setShuffle", shuffle);
                Debug.Log($"[VlcPlaybackBridge] SetShuffle called with shuffle: {shuffle}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetShuffle failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetShuffle is only supported on Android device.");
#endif
    }

    public static void Seek(float position)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("seek", position);
                Debug.Log($"[VlcPlaybackBridge] Seek called with position: {position}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Seek failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Seek is only supported on Android device.");
#endif
    }
    
    public static void SetTime(long timeMs)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setTime", timeMs);
                Debug.Log($"[VlcPlaybackBridge] SetTime called with time: {timeMs}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetTime failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetTime is only supported on Android device.");
#endif
    }

    public static void SetAudioTrack(string trackId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                // JNI CallStatic signature matching: string
                bridge.CallStatic("setAudioTrack", trackId);
                Debug.Log($"[VlcPlaybackBridge] SetAudioTrack called with trackId: {trackId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetAudioTrack failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetAudioTrack is only supported on Android device.");
#endif
    }

    public static void SetSpuTrack(string trackId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setSpuTrack", trackId);
                Debug.Log($"[VlcPlaybackBridge] SetSpuTrack called with trackId: {trackId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSpuTrack failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSpuTrack is only supported on Android device.");
#endif
    }

    public static void SetSubtitleRenderMode(SubtitleRenderMode mode)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setSubtitleRenderMode", (int)mode);
                Debug.Log($"[VlcPlaybackBridge] SetSubtitleRenderMode called with mode: {mode}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleRenderMode failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSubtitleRenderMode is only supported on Android device.");
#endif
    }

    public static void SetVideoScaleOrdinal(int scaleOrdinal)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setVideoScale", scaleOrdinal);
                Debug.Log($"[VlcPlaybackBridge] SetVideoScaleOrdinal called with scaleOrdinal: {scaleOrdinal}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetVideoScaleOrdinal failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetVideoScaleOrdinal is only supported on Android device.");
#endif
    }

    public static void SetAudioChannelMode(string mode)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setAudioChannelMode", mode);
                Debug.Log($"[VlcPlaybackBridge] SetAudioChannelMode called with mode: {mode}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetAudioChannelMode failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetAudioChannelMode is only supported on Android device.");
#endif
    }

    /// <summary>
    /// 调用 AAR 侧 PlaybackServiceBridge.setRate，用于 Unity 快捷键切换 1x/2x 倍速。
    /// </summary>
    public static void SetRate(float rate)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setRate", rate);
                Debug.Log($"[VlcPlaybackBridge] SetRate called with rate: {rate}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetRate failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetRate is only supported on Android device.");
#endif
    }

    public static string GetPlaylist()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                return bridge.CallStatic<string>("getPlaylist");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetPlaylist failed: {e.Message}");
            return "[]";
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] GetPlaylist is only supported on Android device.");
        return "[]";
#endif
    }

    public static void SkipToIndex(int index)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("skipToIndex", index);
                Debug.Log($"[VlcPlaybackBridge] SkipToIndex called with index: {index}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SkipToIndex failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SkipToIndex is only supported on Android device.");
#endif
    }

    // ==========================================
    // 2. 接收 Android AAR 的回调 (UnitySendMessage)
    // ==========================================

    public void OnVideoSizeChanged(string sizeStr)
    {
        Debug.Log($"[VlcPlaybackBridge] OnVideoSizeChanged: {sizeStr}");
        try
        {
            string[] parts = sizeStr.Split('|');
            if (parts.Length == 2)
            {
                int width = int.Parse(parts[0]);
                int height = int.Parse(parts[1]);
                OnVideoSizeChangedEvent?.Invoke(width, height);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Error parsing video size: {e.Message}");
        }
    }

    public void OnStateChanged(string state)
    {
        Debug.Log($"[VlcPlaybackBridge] OnStateChanged: {state}");
        Snapshot.SetStatusFromBridge(state);
        OnStateChangedEvent?.Invoke(state);
    }

    public void OnTimeChanged(string timeStr)
    {
        if (long.TryParse(timeStr, out long timeMs))
        {
            Snapshot.TimeMs = timeMs;
            OnTimeChangedEvent?.Invoke(timeMs);
        }
    }

    public void OnPositionChanged(string posStr)
    {
        if (float.TryParse(posStr, out float pos))
        {
            OnPositionChangedEvent?.Invoke(pos);
        }
    }

    public void OnLengthChanged(string lengthStr)
    {
        Debug.Log($"[VlcPlaybackBridge] OnLengthChanged: {lengthStr}");
        if (long.TryParse(lengthStr, out long lengthMs))
        {
            Snapshot.LengthMs = lengthMs;
            OnLengthChangedEvent?.Invoke(lengthMs);
        }
    }

    public void OnBuffering(string bufferStr)
    {
        if (float.TryParse(bufferStr, out float buffering))
        {
            Snapshot.Buffering = buffering;
            OnBufferingEvent?.Invoke(buffering);
        }
    }

    public void OnAudioTracksChanged(string tracksData)
    {
        Debug.Log($"[VlcPlaybackBridge] OnAudioTracksChanged: {tracksData}");
        var tracks = VlcPlaybackPayloadParser.ParseTracksData(tracksData);
        Snapshot.SetAudioTracks(tracks);
        OnAudioTracksChangedEvent?.Invoke(tracks);
    }

    public void OnSubtitleTracksChanged(string tracksData)
    {
        Debug.Log($"[VlcPlaybackBridge] OnSubtitleTracksChanged: {tracksData}");
        var tracks = VlcPlaybackPayloadParser.ParseTracksData(tracksData);
        Snapshot.SetSubtitleTracks(tracks);
        OnSubtitleTracksChangedEvent?.Invoke(tracks);
    }

    public void OnSubtitleCue(string cueJson)
    {
        var cue = VlcPlaybackPayloadParser.ParseSubtitleCuePayload(cueJson);
        Debug.Log($"[VlcPlaybackBridge] OnSubtitleCue: source={cue.source}, textLength={cue.text?.Length ?? 0}, payload={Preview(cueJson)}");
        Snapshot.SetSubtitleCue(cue);
        OnSubtitleCueEvent?.Invoke(cue);
    }

    private static string Preview(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        value = value.Replace('\n', ' ').Replace('\r', ' ');
        return value.Length <= 220 ? value : value.Substring(0, 220) + "...";
    }

    /// <summary>
    /// 接收 Android 侧通知并触发视频播放
    /// </summary>
    public void StartPlay(string jsonPayload)
    {
        if (string.IsNullOrEmpty(jsonPayload)) return;
        
        Debug.Log($"[VlcPlaybackBridge] StartPlay: {jsonPayload}");

        try
        {
            var mediaWrapper = VlcPlaybackPayloadParser.ParseStartPlayPayload(
                jsonPayload,
                XRVLC.Media.VlcMediaLibraryBridge.GetLastTime);
            if (mediaWrapper == null) return;

            XRVLC.Media.VlcMediaLibraryBridge.AddToHistory(mediaWrapper.Uri, mediaWrapper.Title);
            Snapshot.CurrentMedia = mediaWrapper;
            OnPlayRequestedEvent?.Invoke(mediaWrapper);
        }
        catch (Exception e)
        {
            Debug.LogError($"解析 Android 传来的 JSON 数据失败: {e.Message}");
        }
    }

    // ==========================================
    // OnMediaParseFinished 回调（替代 preloadLocation 中的 OnVideoSizeChanged）
    // ==========================================

    /// <summary>
    /// Android 解析完媒体后回调，携带分辨率、libvlc projection、时长。
    /// JSON 格式: {"width":3840,"height":1920,"projection":"360","duration":7200000}
    /// </summary>
    public void OnMediaParseFinished(string json)
    {
        Debug.Log($"[VlcPlaybackBridge] OnMediaParseFinished: {json}");
        try
        {
            var result = VlcPlaybackPayloadParser.ParseMediaParseResult(json);
            if (result == null || result.width <= 0 || result.height <= 0)
            {
                Debug.LogWarning("[VlcPlaybackBridge] OnMediaParseFinished: invalid payload, ignored.");
                return;
            }
            OnMediaParseFinishedEvent?.Invoke(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] OnMediaParseFinished parse error: {e.Message}");
        }
    }

}
