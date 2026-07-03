using System;
using System.Collections.Generic;
using System.Globalization;
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
    public static event Action<VlcVideoSize> OnVideoSizeChangedEvent;
    public static event Action<string> OnStateChangedEvent;
    public static event Action<long> OnTimeChangedEvent;
    public static event Action<float> OnPositionChangedEvent;
    public static event Action<long> OnLengthChangedEvent;
    public static event Action<float> OnPlaybackRateChangedEvent;
    public static event Action<float> OnBufferingEvent;
    public static event Action ClearPlaybackSurfaceEvent;
    
    // 轨道回调事件：List<TrackInfo>
    public static event Action<List<XRVLC.Media.TrackInfo>> OnAudioTracksChangedEvent;
    public static event Action<List<XRVLC.Media.TrackInfo>> OnSubtitleTracksChangedEvent;
    
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

    public static void PublishPlaybackRate(float rate)
    {
        if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            rate = 1f;

        Snapshot.PlaybackRate = rate;
        OnPlaybackRateChangedEvent?.Invoke(rate);
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

    public static void SetSubtitleSurface(IntPtr surfacePtr)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurface JNI entry: surface={surfacePtr}, isZero={surfacePtr == IntPtr.Zero}");
            if (surfacePtr == IntPtr.Zero)
            {
                Debug.LogWarning("[VlcPlaybackBridge] SetSubtitleSurface called with zero surface pointer");
            }

            if (AndroidJNI.AttachCurrentThread() != 0)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to attach JNI thread for subtitle surface");
                return;
            }

            IntPtr bridgeClass = AndroidJNI.FindClass("org/videolan/vlc/bridge/PlaybackServiceBridge");
            if (bridgeClass == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find PlaybackServiceBridge class");
                return;
            }

            IntPtr setSubtitleSurfaceMethod = AndroidJNI.GetStaticMethodID(bridgeClass, "setSubtitleSurface", "(Landroid/view/Surface;)V");
            if (setSubtitleSurfaceMethod == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find setSubtitleSurface method");
                return;
            }

            jvalue[] args = new jvalue[1];
            args[0].l = surfacePtr;
            Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurface JNI invoking Android bridge: class={bridgeClass}, method={setSubtitleSurfaceMethod}, surface={surfacePtr}");
            AndroidJNI.CallStaticVoidMethod(bridgeClass, setSubtitleSurfaceMethod, args);

            Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurface JNI call returned: surface={surfacePtr}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleSurface failed: {e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSubtitleSurface is only supported on Android device.");
#endif
    }

    public static void DetachSubtitleSurface()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                Debug.Log("[VlcPlaybackBridge] DetachSubtitleSurface entry: passing null subtitle surface to Android bridge");
                bridge.CallStatic("setSubtitleSurface", (AndroidJavaObject)null);
                Debug.Log("[VlcPlaybackBridge] DetachSubtitleSurface called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] DetachSubtitleSurface failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] DetachSubtitleSurface is only supported on Android device.");
#endif
    }

    public static void SetSubtitleSurfacePolicy(bool stackOutside)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setSubtitleSurfacePolicy", stackOutside);
                Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurfacePolicy called stackOutside={stackOutside}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleSurfacePolicy failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSubtitleSurfacePolicy is only supported on Android device.");
#endif
    }

    public static void SetSubtitleSurfaceEnabled(bool enabled)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setSubtitleSurfaceEnabled", enabled);
                Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurfaceEnabled called enabled={enabled}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleSurfaceEnabled failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSubtitleSurfaceEnabled is only supported on Android device.");
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

    public static TrackSnapshot GetTrackSnapshot()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                string payload = bridge.CallStatic<string>("getTrackSnapshot");
                return VlcPlaybackPayloadParser.ParseTrackSnapshot(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetTrackSnapshot failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] GetTrackSnapshot is only supported on Android device.");
#endif

        return new TrackSnapshot();
    }

    public static TrackSnapshot GetAudioTrackSnapshot()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                string payload = bridge.CallStatic<string>("getAudioTrackSnapshot");
                return VlcPlaybackPayloadParser.ParseTrackSnapshot(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetAudioTrackSnapshot failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] GetAudioTrackSnapshot is only supported on Android device.");
#endif

        return new TrackSnapshot();
    }

    public static TrackSnapshot GetSubtitleTrackSnapshot()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                string payload = bridge.CallStatic<string>("getSubtitleTrackSnapshot");
                return VlcPlaybackPayloadParser.ParseTrackSnapshot(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetSubtitleTrackSnapshot failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] GetSubtitleTrackSnapshot is only supported on Android device.");
#endif

        return new TrackSnapshot();
    }

    public static TrackSnapshot SetAudioTrackAndGetSnapshot(string trackId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                string payload = bridge.CallStatic<string>("setAudioTrackAndGetSnapshot", trackId);
                Debug.Log($"[VlcPlaybackBridge] SetAudioTrackAndGetSnapshot called with trackId: {trackId}");
                return VlcPlaybackPayloadParser.ParseTrackSnapshot(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetAudioTrackAndGetSnapshot failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetAudioTrackAndGetSnapshot is only supported on Android device.");
#endif

        return new TrackSnapshot();
    }

    public static TrackSnapshot SetSpuTrackAndGetSnapshot(string trackId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                string payload = bridge.CallStatic<string>("setSpuTrackAndGetSnapshot", trackId);
                Debug.Log($"[VlcPlaybackBridge] SetSpuTrackAndGetSnapshot called with trackId: {trackId}");
                return VlcPlaybackPayloadParser.ParseTrackSnapshot(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSpuTrackAndGetSnapshot failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSpuTrackAndGetSnapshot is only supported on Android device.");
#endif

        return new TrackSnapshot();
    }

    public static void SetAudioTrack(string trackId)
    {
        SetAudioTrackAndGetSnapshot(trackId);
    }

    public static void SetSpuTrack(string trackId)
    {
        SetSpuTrackAndGetSnapshot(trackId);
    }

    public static void SetSubtitleRenderMode(SubtitleRenderMode mode)
    {
        SubtitleRenderMode bridgeMode = ToAndroidSubtitleRenderMode(mode);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setSubtitleRenderMode", (int)bridgeMode);
                Debug.Log($"[VlcPlaybackBridge] SetSubtitleRenderMode called with uiMode={mode}, androidMode={bridgeMode}");
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

    public static void SetSubtitleDelayMicroseconds(long delayUs)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setSubtitleDelay", delayUs);
                Debug.Log($"[VlcPlaybackBridge] SetSubtitleDelayMicroseconds called with delayUs={delayUs}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleDelayMicroseconds failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSubtitleDelayMicroseconds is only supported on Android device.");
#endif
    }

    public static long GetSubtitleDelayMicroseconds()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                long delayUs = bridge.CallStatic<long>("getSubtitleDelay");
                Debug.Log($"[VlcPlaybackBridge] GetSubtitleDelayMicroseconds returned delayUs={delayUs}");
                return delayUs;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetSubtitleDelayMicroseconds failed: {e.Message}");
            return 0L;
        }
#else
        return 0L;
#endif
    }

    public static void OpenSubtitlePicker()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("openSubtitlePicker");
                Debug.Log("[VlcPlaybackBridge] OpenSubtitlePicker called");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] OpenSubtitlePicker failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] OpenSubtitlePicker is only supported on Android device.");
#endif
    }

    private static SubtitleRenderMode ToAndroidSubtitleRenderMode(SubtitleRenderMode mode)
    {
        return mode;
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

    public static float GetPlaybackRate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                float rate = bridge.CallStatic<float>("getRate");
                PublishPlaybackRate(rate);
                Debug.Log($"[VlcPlaybackBridge] GetPlaybackRate returned: {rate}");
                return Snapshot.PlaybackRate;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetPlaybackRate failed: {e.Message}");
            return Snapshot.PlaybackRate;
        }
#else
        return Snapshot.PlaybackRate;
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
            if (parts.Length == 2 || parts.Length == 4)
            {
                int width = int.Parse(parts[0], CultureInfo.InvariantCulture);
                int height = int.Parse(parts[1], CultureInfo.InvariantCulture);
                int visibleWidth = parts.Length == 4
                    ? int.Parse(parts[2], CultureInfo.InvariantCulture)
                    : width;
                int visibleHeight = parts.Length == 4
                    ? int.Parse(parts[3], CultureInfo.InvariantCulture)
                    : height;
                OnVideoSizeChangedEvent?.Invoke(new VlcVideoSize(width, height, visibleWidth, visibleHeight));
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

    public void OnPlaybackRateChanged(string rateStr)
    {
        if (float.TryParse(rateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float rate))
        {
            Debug.Log($"[VlcPlaybackBridge] OnPlaybackRateChanged: {rate}");
            PublishPlaybackRate(rate);
        }
        else
        {
            Debug.LogWarning($"[VlcPlaybackBridge] OnPlaybackRateChanged parse failed: {rateStr}");
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
        Debug.Log($"[VlcPlaybackBridge] OnAudioTracksChanged dirty notification: {tracksData}");
        OnAudioTracksChangedEvent?.Invoke(null);
    }

    public void OnSubtitleTracksChanged(string tracksData)
    {
        Debug.Log($"[VlcPlaybackBridge] OnSubtitleTracksChanged dirty notification: {tracksData}");
        OnSubtitleTracksChangedEvent?.Invoke(null);
    }

    public void ClearPlaybackSurface()
    {
        Debug.Log("[VlcPlaybackBridge] ClearPlaybackSurface");
        ClearPlaybackSurfaceEvent?.Invoke();
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
            var mediaWrapper = VlcPlaybackPayloadParser.ParseStartPlayPayload(jsonPayload);
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
    /// JSON 格式: {"width":3840,"height":1920,"visibleWidth":3840,"visibleHeight":1920,"projection":"360","duration":7200000}
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
