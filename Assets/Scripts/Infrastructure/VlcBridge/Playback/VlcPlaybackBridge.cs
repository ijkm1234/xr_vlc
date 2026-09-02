using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRVLC;
using XRVLC.Media;

/// <summary>
/// AAR Playback Bridge
/// 负责与 Android 原生 PlaybackService 进行双向通信。
/// 既包含调用 AAR 的方法，也作为 UnitySendMessage 的接收目标（挂载在全局 GameObject 上）。
/// </summary>
public class VlcPlaybackBridge : MonoBehaviour
{
    private const string BridgeClassName = "org.videolan.vlc.bridge.PlaybackServiceBridge";
    private const string SurfaceDebugTag = "XR_SURFACE_DEBUG";
    private const string DefaultSubtitleFontSize = "16";
    private const int DefaultSubtitleOpacity = 255;
    private static VlcPlaybackBridge _instance;

    // --- 事件回调 ---
    public static event Action<VlcVideoSize> OnVideoSizeChangedEvent;
    public static event Action<string> OnStateChangedEvent;
    public static event Action<long> OnTimeChangedEvent;
    public static event Action<float> OnPositionChangedEvent;
    public static event Action<long> OnLengthChangedEvent;
    public static event Action<float> OnPlaybackRateChangedEvent;
    public static event Action<float> OnBufferingEvent;
    public static event Action<string> OnChromaKeyColorExtractedEvent;
    public static event Action<string> OnVideoOutputSwitchEventReceived;
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

    private static void SurfaceDebug(string message)
    {
        Debug.Log($"[{SurfaceDebugTag}] bridge {message}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass log = new AndroidJavaClass("android.util.Log"))
                log.CallStatic<int>("e", SurfaceDebugTag, $"bridge {message}");
        }
        catch
        {
            // Diagnostics must never affect JNI playback calls.
        }
#endif
    }

    public static void PublishPlaybackRate(float rate)
    {
        if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            rate = 1f;

        OnPlaybackRateChangedEvent?.Invoke(rate);
    }

    // ==========================================
    // 1. 调用 Android AAR 侧的方法 (JNI)
    // ==========================================

    public static long PreloadLocation(string payload)
    {
        bool payloadIsJson = payload != null && payload.TrimStart().StartsWith("{", StringComparison.Ordinal);
        SurfaceDebug(
            $"preload_location enter payloadNull={payload == null} " +
            $"payloadLength={(payload == null ? 0 : payload.Length)} " +
            $"payloadIsJson={payloadIsJson}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                long mediaRequestId = bridge.CallStatic<long>("preloadLocation", payload);
                Debug.Log($"[VlcPlaybackBridge] PreloadLocation called for: {payload}");
                SurfaceDebug($"preload_location android_call_returned request={mediaRequestId}");
                return mediaRequestId;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] PreloadLocation failed: {e.Message}");
            SurfaceDebug($"preload_location failed exception={e}");
            return 0L;
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] PreloadLocation is only supported on Android devices.");
        return 0L;
#endif
    }

    public static void SetSurface(IntPtr surfacePtr)
    {
        SurfaceDebug($"set_video_surface enter surface={surfacePtr} isZero={surfacePtr == IntPtr.Zero}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            int attachResult = AndroidJNI.AttachCurrentThread();
            SurfaceDebug($"set_video_surface attach_thread result={attachResult}");
            if (attachResult != 0)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to attach JNI thread");
                SurfaceDebug("set_video_surface failed attach_thread");
                return;
            }

            IntPtr bridgeClass = AndroidJNI.FindClass("org/videolan/vlc/bridge/PlaybackServiceBridge");
            SurfaceDebug($"set_video_surface find_class class={bridgeClass}");
            if (bridgeClass == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find PlaybackServiceBridge class");
                SurfaceDebug("set_video_surface failed find_class");
                return;
            }

            IntPtr setSurfaceMethod = AndroidJNI.GetStaticMethodID(bridgeClass, "setVideoSurface", "(Landroid/view/Surface;)V");
            SurfaceDebug($"set_video_surface get_method method={setSurfaceMethod}");
            if (setSurfaceMethod == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find setVideoSurface method");
                SurfaceDebug("set_video_surface failed get_method");
                return;
            }

            jvalue[] args = new jvalue[1];
            args[0].l = surfacePtr;
            SurfaceDebug($"set_video_surface call_static_void surface={surfacePtr}");
            AndroidJNI.CallStaticVoidMethod(bridgeClass, setSurfaceMethod, args);
            
            Debug.Log("[VlcPlaybackBridge] SetSurface called successfully");
            SurfaceDebug($"set_video_surface returned surface={surfacePtr}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSurface failed: {e.Message}");
            SurfaceDebug($"set_video_surface exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetSurface is only supported on Android device.");
#endif
    }

    public static void DetachSurface()
    {
        SurfaceDebug("detach_video_surface enter");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setVideoSurface", (AndroidJavaObject)null);
                Debug.Log("[VlcPlaybackBridge] DetachSurface called");
                SurfaceDebug("detach_video_surface android_call_returned");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] DetachSurface failed: {e.Message}");
            SurfaceDebug($"detach_video_surface exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] DetachSurface is only supported on Android device.");
#endif
    }

    public static void BeginRebuildLayer(long switchToken, long mediaRequestId, bool rebuildInput, bool rebuildOutput)
    {
        SurfaceDebug(
            $"video_layer begin operation=RebuildLayer token={switchToken} mediaRequest={mediaRequestId} " +
            $"rebuildInput={rebuildInput} rebuildOutput={rebuildOutput}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("beginRebuildLayer", switchToken, mediaRequestId, rebuildInput, rebuildOutput);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] BeginRebuildLayer failed: {e.Message}");
            OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|failed|begin-rebuild-jni");
        }
#else
        OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|detached");
#endif
    }

    public static void BeginChangeLayer(long switchToken, bool rebuildOutput)
    {
        SurfaceDebug($"video_layer begin operation=ChangeLayer token={switchToken} rebuildOutput={rebuildOutput}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("beginChangeLayer", switchToken, rebuildOutput);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] BeginChangeLayer failed: {e.Message}");
            OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|failed|begin-change-jni");
        }
#else
        OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|detached");
#endif
    }

    public static void AttachRebuildLayer(
        long switchToken,
        long mediaRequestId,
        IntPtr surfacePtr,
        bool fisheyeMappingEnabled,
        bool chromaKeyEnabled,
        bool resumeCurrentMedia,
        StereoMode stereo,
        uint contentWidth,
        uint contentHeight)
    {
        SurfaceDebug(
            $"video_layer attach operation=RebuildLayer token={switchToken} mediaRequest={mediaRequestId} surface={surfacePtr} " +
            $"fisheye={fisheyeMappingEnabled} chroma={chromaKeyEnabled} stereo={stereo} " +
            $"content={contentWidth}x{contentHeight}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            int attachResult = AndroidJNI.AttachCurrentThread();
            if (attachResult != 0)
                throw new InvalidOperationException($"AttachCurrentThread returned {attachResult}");

            IntPtr bridgeClass = AndroidJNI.FindClass("org/videolan/vlc/bridge/PlaybackServiceBridge");
            if (bridgeClass == IntPtr.Zero)
                throw new InvalidOperationException("PlaybackServiceBridge class not found");

            IntPtr attachMethod = AndroidJNI.GetStaticMethodID(
                bridgeClass,
                "attachRebuildLayer",
                "(JJLandroid/view/Surface;ZZZIII)V");
            if (attachMethod == IntPtr.Zero)
                throw new InvalidOperationException("attachRebuildLayer method not found");

            jvalue[] args = new jvalue[9];
            args[0].j = switchToken;
            args[1].j = mediaRequestId;
            args[2].l = surfacePtr;
            args[3].z = fisheyeMappingEnabled;
            args[4].z = chromaKeyEnabled;
            args[5].z = resumeCurrentMedia;
            args[6].i = (int)stereo;
            args[7].i = (int)contentWidth;
            args[8].i = (int)contentHeight;
            AndroidJNI.CallStaticVoidMethod(bridgeClass, attachMethod, args);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] AttachRebuildLayer failed: {e.Message}");
            OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|failed|attach-rebuild-jni");
        }
#else
        OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|ready");
#endif
    }

    public static void AttachChangeLayer(
        long switchToken,
        IntPtr surfacePtr,
        bool fisheyeMappingEnabled,
        bool chromaKeyEnabled,
        StereoMode stereo,
        uint contentWidth,
        uint contentHeight)
    {
        SurfaceDebug(
            $"video_layer attach operation=ChangeLayer token={switchToken} surface={surfacePtr} " +
            $"fisheye={fisheyeMappingEnabled} chroma={chromaKeyEnabled} stereo={stereo} " +
            $"content={contentWidth}x{contentHeight}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            int attachResult = AndroidJNI.AttachCurrentThread();
            if (attachResult != 0)
                throw new InvalidOperationException($"AttachCurrentThread returned {attachResult}");

            IntPtr bridgeClass = AndroidJNI.FindClass("org/videolan/vlc/bridge/PlaybackServiceBridge");
            if (bridgeClass == IntPtr.Zero)
                throw new InvalidOperationException("PlaybackServiceBridge class not found");

            IntPtr attachMethod = AndroidJNI.GetStaticMethodID(
                bridgeClass,
                "attachChangeLayer",
                "(JLandroid/view/Surface;ZZIII)V");
            if (attachMethod == IntPtr.Zero)
                throw new InvalidOperationException("attachChangeLayer method not found");

            jvalue[] args = new jvalue[7];
            args[0].j = switchToken;
            args[1].l = surfacePtr;
            args[2].z = fisheyeMappingEnabled;
            args[3].z = chromaKeyEnabled;
            args[4].i = (int)stereo;
            args[5].i = (int)contentWidth;
            args[6].i = (int)contentHeight;
            AndroidJNI.CallStaticVoidMethod(bridgeClass, attachMethod, args);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] AttachChangeLayer failed: {e.Message}");
            OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|failed|attach-change-jni");
        }
#else
        OnVideoOutputSwitchEventReceived?.Invoke($"{switchToken}|ready");
#endif
    }

    public static void CancelVideoOutputSwitch(long switchToken)
    {
        SurfaceDebug($"video_output_switch cancel token={switchToken}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("cancelVideoOutputSwitch", switchToken);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VlcPlaybackBridge] CancelVideoOutputSwitch failed: {e.Message}");
        }
#endif
    }

    public static void CancelPendingMediaRequests()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("cancelPendingMediaRequests");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] CancelPendingMediaRequests failed: {e.Message}");
        }
#endif
    }

    public static void CancelPendingMediaRequest(long mediaRequestId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("cancelPendingMediaRequest", mediaRequestId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] CancelPendingMediaRequest failed: {e.Message}");
        }
#endif
    }

    public static void SetVideoSurfaceMapping(bool fisheyeMappingEnabled, bool chromaKeyEnabled, StereoMode stereo, uint contentWidth, uint contentHeight)
    {
        SurfaceDebug(
            $"set_video_surface_mapping enter fisheye={fisheyeMappingEnabled} chromaKey={chromaKeyEnabled} " +
            $"stereo={stereo} content={contentWidth}x{contentHeight}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setVideoSurfaceMapping", fisheyeMappingEnabled, chromaKeyEnabled, (int)stereo, (int)contentWidth, (int)contentHeight);
                SurfaceDebug(
                    $"set_video_surface_mapping android_call_returned fisheye={fisheyeMappingEnabled} chromaKey={chromaKeyEnabled} " +
                    $"stereo={(int)stereo} content={contentWidth}x{contentHeight}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetVideoSurfaceMapping failed: {e.Message}");
            SurfaceDebug($"set_video_surface_mapping exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetVideoSurfaceMapping is only supported on Android device.");
#endif
    }

    public static void SetVideoSurfaceProcessingParameters(
        FisheyeProjectionFormula fisheyeProjectionFormula,
        Color keyColor,
        float colorRange,
        float edgeSmooth,
        float despillStrength)
    {
        Color rgb = new Color(
            Mathf.Clamp01(keyColor.r),
            Mathf.Clamp01(keyColor.g),
            Mathf.Clamp01(keyColor.b),
            1f);
        float range = Mathf.Clamp(colorRange, 0f, ChromaKeySettings.ColorRangeMax);
        float safeEdgeSmooth = Mathf.Clamp(edgeSmooth, 0f, ChromaKeySettings.EdgeSmoothMax);
        float safeDespillStrength = Mathf.Clamp(
            despillStrength,
            0f,
            ChromaKeySettings.DespillStrengthMax);
        SurfaceDebug(
            $"set_video_surface_processing formula={fisheyeProjectionFormula} " +
            $"key=({rgb.r:F4},{rgb.g:F4},{rgb.b:F4}) range={range:F4} " +
            $"edgeSmooth={safeEdgeSmooth:F4} despill={safeDespillStrength:F4}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic(
                    "setVideoSurfaceProcessingParameters",
                    (int)fisheyeProjectionFormula,
                    rgb.r,
                    rgb.g,
                    rgb.b,
                    range,
                    safeEdgeSmooth,
                    safeDespillStrength);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetVideoSurfaceProcessingParameters failed: {e.Message}");
            SurfaceDebug($"set_video_surface_processing exception={e}");
        }
#endif
    }

    public static void RequestVideoSurfaceChromaKeyColorExtraction()
    {
        SurfaceDebug("request_chroma_key_color_extraction enter");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("requestVideoSurfaceChromaKeyColorExtraction");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] RequestVideoSurfaceChromaKeyColorExtraction failed: {e.Message}");
            OnChromaKeyColorExtractedEvent?.Invoke("error|jni-exception");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Chroma key color extraction is only supported on Android device.");
        OnChromaKeyColorExtractedEvent?.Invoke("error|unsupported-platform");
#endif
    }

    public static void SetSubtitleSurface(IntPtr surfacePtr)
    {
        SurfaceDebug($"set_subtitle_surface enter surface={surfacePtr} isZero={surfacePtr == IntPtr.Zero}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurface JNI entry: surface={surfacePtr}, isZero={surfacePtr == IntPtr.Zero}");
            if (surfacePtr == IntPtr.Zero)
            {
                Debug.LogWarning("[VlcPlaybackBridge] SetSubtitleSurface called with zero surface pointer");
            }

            int attachResult = AndroidJNI.AttachCurrentThread();
            SurfaceDebug($"set_subtitle_surface attach_thread result={attachResult}");
            if (attachResult != 0)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to attach JNI thread for subtitle surface");
                SurfaceDebug("set_subtitle_surface failed attach_thread");
                return;
            }

            IntPtr bridgeClass = AndroidJNI.FindClass("org/videolan/vlc/bridge/PlaybackServiceBridge");
            SurfaceDebug($"set_subtitle_surface find_class class={bridgeClass}");
            if (bridgeClass == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find PlaybackServiceBridge class");
                SurfaceDebug("set_subtitle_surface failed find_class");
                return;
            }

            IntPtr setSubtitleSurfaceMethod = AndroidJNI.GetStaticMethodID(bridgeClass, "setSubtitleSurface", "(Landroid/view/Surface;)V");
            SurfaceDebug($"set_subtitle_surface get_method method={setSubtitleSurfaceMethod}");
            if (setSubtitleSurfaceMethod == IntPtr.Zero)
            {
                Debug.LogError("[VlcPlaybackBridge] Failed to find setSubtitleSurface method");
                SurfaceDebug("set_subtitle_surface failed get_method");
                return;
            }

            jvalue[] args = new jvalue[1];
            args[0].l = surfacePtr;
            Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurface JNI invoking Android bridge: class={bridgeClass}, method={setSubtitleSurfaceMethod}, surface={surfacePtr}");
            SurfaceDebug($"set_subtitle_surface call_static_void surface={surfacePtr}");
            AndroidJNI.CallStaticVoidMethod(bridgeClass, setSubtitleSurfaceMethod, args);

            Debug.Log($"[VlcPlaybackBridge] SetSubtitleSurface JNI call returned: surface={surfacePtr}");
            SurfaceDebug($"set_subtitle_surface returned surface={surfacePtr}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleSurface failed: {e}");
            SurfaceDebug($"set_subtitle_surface exception={e}");
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
        SurfaceDebug("control_play enter");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("play");
                Debug.Log("[VlcPlaybackBridge] Play called");
                SurfaceDebug("control_play android_call_returned");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Play failed: {e.Message}");
            SurfaceDebug($"control_play exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Play is only supported on Android device.");
        SurfaceDebug("control_play unsupported_platform");
#endif
    }

    public static void ReplayFromStart()
    {
        SurfaceDebug("control_replay_from_start enter");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("replayFromStart");
                SurfaceDebug("control_replay_from_start android_call_returned");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] ReplayFromStart failed: {e.Message}");
            SurfaceDebug($"control_replay_from_start exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] ReplayFromStart is only supported on Android device.");
        SurfaceDebug("control_replay_from_start unsupported_platform");
#endif
    }

    public static bool IsPlaying()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                return bridge.CallStatic<bool>("isPlaying");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] IsPlaying failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    public static bool TryHasActivePlaybackSelection(out bool hasActiveVideoSelection)
    {
        hasActiveVideoSelection = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                int state = bridge.CallStatic<int>("getPlaybackSelectionState");
                if (state < 0)
                    return false;

                hasActiveVideoSelection = state == 1;
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetPlaybackSelectionState failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    public static int GetPlayerState()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                return bridge.CallStatic<int>("getPlayerState");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetPlayerState failed: {e.Message}");
        }
#endif
        return -1;
    }

    public static long GetTime()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                return bridge.CallStatic<long>("getTime");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetTime failed: {e.Message}");
        }
#endif
        return 0L;
    }

    public static long GetLength()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                return bridge.CallStatic<long>("getLength");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetLength failed: {e.Message}");
        }
#endif
        return 0L;
    }

    public static void Pause()
    {
        SurfaceDebug("control_pause enter");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("pause");
                Debug.Log("[VlcPlaybackBridge] Pause called");
                SurfaceDebug("control_pause android_call_returned");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Pause failed: {e.Message}");
            SurfaceDebug($"control_pause exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Pause is only supported on Android device.");
        SurfaceDebug("control_pause unsupported_platform");
#endif
    }

    public static void Stop()
    {
        SurfaceDebug("control_stop enter");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("stop");
                Debug.Log("[VlcPlaybackBridge] Stop called");
                SurfaceDebug("control_stop android_call_returned");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Stop failed: {e.Message}");
            SurfaceDebug($"control_stop exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Stop is only supported on Android device.");
        SurfaceDebug("control_stop unsupported_platform");
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
        SurfaceDebug($"control_seek enter position={position.ToString(CultureInfo.InvariantCulture)}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("seek", position);
                Debug.Log($"[VlcPlaybackBridge] Seek called with position: {position}");
                SurfaceDebug($"control_seek android_call_returned position={position.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Seek failed: {e.Message}");
            SurfaceDebug($"control_seek exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] Seek is only supported on Android device.");
        SurfaceDebug("control_seek unsupported_platform");
#endif
    }
    
    public static void SetTime(long timeMs)
    {
        SurfaceDebug($"control_set_time enter timeMs={timeMs}");
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setTime", timeMs);
                Debug.Log($"[VlcPlaybackBridge] SetTime called with time: {timeMs}");
                SurfaceDebug($"control_set_time android_call_returned timeMs={timeMs}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetTime failed: {e.Message}");
            SurfaceDebug($"control_set_time exception={e}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetTime is only supported on Android device.");
        SurfaceDebug("control_set_time unsupported_platform");
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

    public static string GetSubtitleFontSize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                return bridge.CallStatic<string>("getSubtitleFontSize") ?? DefaultSubtitleFontSize;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetSubtitleFontSize failed: {e.Message}");
        }
#endif

        return DefaultSubtitleFontSize;
    }

    public static void SetSubtitleFontSize(string value)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("setSubtitleFontSize", value ?? DefaultSubtitleFontSize);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleFontSize failed: {e.Message}");
        }
#endif
    }

    public static int GetSubtitleOpacity()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                return bridge.CallStatic<int>("getSubtitleOpacity");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetSubtitleOpacity failed: {e.Message}");
        }
#endif

        return DefaultSubtitleOpacity;
    }

    public static void SetSubtitleOpacity(int value)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("setSubtitleOpacity", value);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetSubtitleOpacity failed: {e.Message}");
        }
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

    public static void SetAudioDelayMicroseconds(long delayUs)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setAudioDelay", delayUs);
                Debug.Log($"[VlcPlaybackBridge] SetAudioDelayMicroseconds called with delayUs={delayUs}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetAudioDelayMicroseconds failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetAudioDelayMicroseconds is only supported on Android device.");
#endif
    }

    public static long GetAudioDelayMicroseconds()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                long delayUs = bridge.CallStatic<long>("getAudioDelay");
                Debug.Log($"[VlcPlaybackBridge] GetAudioDelayMicroseconds returned delayUs={delayUs}");
                return delayUs;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetAudioDelayMicroseconds failed: {e.Message}");
            return 0L;
        }
#else
        return 0L;
#endif
    }

    public static void OpenSubtitlePicker()
    {
        Debug.Log("[VlcPlaybackBridge] OpenSubtitlePicker entry");
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
        // Android mode 1 selects VLC's separate XR subtitle Surface. Unity owns
        // whether that Surface is aligned to the screen or placed spatially.
        return mode switch
        {
            SubtitleRenderMode.Native => SubtitleRenderMode.Spatial,
            SubtitleRenderMode.Spatial => SubtitleRenderMode.Spatial,
            SubtitleRenderMode.DualDebug => SubtitleRenderMode.Spatial,
            _ => SubtitleRenderMode.Off
        };
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

    public static bool IsAudioBoostEnabled()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                return bridge.CallStatic<bool>("isAudioBoostEnabled");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] IsAudioBoostEnabled failed: {e.Message}");
            return true;
        }
#else
        return true;
#endif
    }

    public static void SetAudioBoostEnabled(bool enabled)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setAudioBoostEnabled", enabled);
                Debug.Log($"[VlcPlaybackBridge] SetAudioBoostEnabled called with enabled: {enabled}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetAudioBoostEnabled failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetAudioBoostEnabled is only supported on Android device.");
#endif
    }

    public static int GetVolumePercent()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                return Mathf.Clamp(bridge.CallStatic<int>("getVolumePercent"), 0, 200);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetVolumePercent failed: {e.Message}");
            return 100;
        }
#else
        return 100;
#endif
    }

    public static void SetVolumePercent(int percent)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("setVolumePercent", percent);
                Debug.Log($"[VlcPlaybackBridge] SetVolumePercent called with percent: {percent}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] SetVolumePercent failed: {e.Message}");
        }
#else
        Debug.LogWarning("[VlcPlaybackBridge] SetVolumePercent is only supported on Android device.");
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
                if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
                    rate = 1f;
                Debug.Log($"[VlcPlaybackBridge] GetPlaybackRate returned: {rate}");
                return rate;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] GetPlaybackRate failed: {e.Message}");
            return 1f;
        }
#else
        return 1f;
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
        SurfaceDebug($"layout_unity_message raw={sizeStr}");
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
                SurfaceDebug(
                    $"layout_unity_message parsed raw={width}x{height} visible={visibleWidth}x{visibleHeight} " +
                    $"subscriberPresent={OnVideoSizeChangedEvent != null}");
                OnVideoSizeChangedEvent?.Invoke(new VlcVideoSize(width, height, visibleWidth, visibleHeight));
            }
            else
            {
                SurfaceDebug($"layout_unity_message ignored invalid_parts count={parts.Length}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] Error parsing video size: {e.Message}");
            SurfaceDebug($"layout_unity_message parse_exception={e}");
        }
    }

    public void OnVideoOutputSwitchEvent(string payload)
    {
        SurfaceDebug($"video_output_switch unity_event payload={payload}");
        OnVideoOutputSwitchEventReceived?.Invoke(payload ?? string.Empty);
    }

    public void OnChromaKeyColorExtracted(string payload)
    {
        SurfaceDebug($"chroma_key_color_extracted payload={payload}");
        OnChromaKeyColorExtractedEvent?.Invoke(payload ?? string.Empty);
    }

    public void OnStateChanged(string state)
    {
        Debug.Log($"[VlcPlaybackBridge] OnStateChanged: {state}");
        OnStateChangedEvent?.Invoke(state);
    }

    public void OnTimeChanged(string timeStr)
    {
        if (long.TryParse(timeStr, out long timeMs))
        {
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
        SurfaceDebug($"start_play received payloadLength={jsonPayload.Length}");

        try
        {
            var mediaWrapper = VlcPlaybackPayloadParser.ParseStartPlayPayload(jsonPayload);
            if (mediaWrapper == null) return;

            XRVLC.Media.VlcMediaLibraryBridge.AddToHistory(mediaWrapper.Uri, mediaWrapper.Title);
            SurfaceDebug($"start_play parsed uri={XRVLC.Utils.UriUtils.RedactUri(mediaWrapper.Uri)} title={mediaWrapper.Title}");
            OnPlayRequestedEvent?.Invoke(mediaWrapper);
        }
        catch (Exception e)
        {
            Debug.LogError($"解析 Android 传来的 JSON 数据失败: {e.Message}");
            SurfaceDebug($"start_play parse_exception={e}");
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
        SurfaceDebug($"parse_unity_message rawLength={(json == null ? 0 : json.Length)}");
        try
        {
            var result = VlcPlaybackPayloadParser.ParseMediaParseResult(json);
            if (result == null || string.IsNullOrEmpty(result.uri))
            {
                Debug.LogWarning("[VlcPlaybackBridge] OnMediaParseFinished: invalid payload, ignored.");
                SurfaceDebug($"parse_unity_message ignored resultNull={result == null} uriEmpty={result != null && string.IsNullOrEmpty(result.uri)}");
                return;
            }
            SurfaceDebug(
                $"parse_unity_message parsed uri={XRVLC.Utils.UriUtils.RedactUri(result.uri)} " +
                $"raw={result.width}x{result.height} visible={result.visibleWidth}x{result.visibleHeight} " +
                $"projection={result.projection} subscriberPresent={OnMediaParseFinishedEvent != null}");
            OnMediaParseFinishedEvent?.Invoke(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VlcPlaybackBridge] OnMediaParseFinished parse error: {e.Message}");
            SurfaceDebug($"parse_unity_message parse_exception={e}");
        }
    }

}
