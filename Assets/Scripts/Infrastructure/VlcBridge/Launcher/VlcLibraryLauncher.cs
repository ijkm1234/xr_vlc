using UnityEngine;
using UnityEngine.Android;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using System;
using System.Collections;
using XRVLC.Infrastructure.Pico;
#if UNITY_ANDROID
using Unity.XR.PXR;
#endif

/// <summary>
/// 负责与 Android VLC AAR 插件进行交互，唤起 2D 媒体库并接收视频选择回调。
/// 该脚本需挂载到名为 "VlcLibraryLauncher" 的 GameObject 上，以接收 UnitySendMessage。
/// </summary>
public class VlcLibraryLauncher : MonoBehaviour
{
    private const int FlagActivityNewTask = 0x10000000;
    private const string PlaybackServiceBridgeClassName = "org.videolan.vlc.bridge.PlaybackServiceBridge";

    public static VlcLibraryLauncher Instance { get; private set; }

    public event Action<string, string> OnVideoSelectedEvent { add { } remove { } }

    public GameObject[] objectsToHideWhenVlcOpens;

    [SerializeField]
    private int m_RestoreDelayFrames = 3;

    [SerializeField]
    private bool m_OpenVlcLibraryOnStart = true;

    private XRInputModalityManager m_ModalityManager;
    private bool? m_LastSessionFocused;
    private bool m_HasOpenedVlcLibraryOnStart;
    private bool m_OpenVlcAfterPermissionGranted;
    private VlcFocusRestoreHandler m_FocusRestoreHandler;
#if UNITY_ANDROID
    private PermissionCallbacks m_PermissionCallbacks;
#endif

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            gameObject.name = "VlcLibraryLauncher";
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

#if UNITY_ANDROID
        PicoSessionEvents.SessionStateChanged += OnSessionStateChanged;
#endif
    }

    private void Start()
    {
        m_ModalityManager = FindAnyObjectByType<XRInputModalityManager>();
        EnsureFocusRestoreHandler();

        if (m_OpenVlcLibraryOnStart && Application.platform == RuntimePlatform.Android)
            StartCoroutine(OpenVlcLibraryOnStart());
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID
        PicoSessionEvents.SessionStateChanged -= OnSessionStateChanged;
        ClearPermissionCallbacks();
#endif
    }

#if UNITY_ANDROID
    // Fires earlier than OnApplicationFocus — driven by OpenXR session state machine.
    // Focused → anything: session lost exclusive input (Home key, activity switch, etc.)
    // anything → Focused: session fully regained input focus
    private void OnSessionStateChanged(XrSessionState state)
    {
        bool focused = state == XrSessionState.Focused;
        if (m_LastSessionFocused.HasValue && focused == m_LastSessionFocused.Value) return;
        m_LastSessionFocused = focused;

        if (!focused)
            EnsureFocusRestoreHandler().HideControllers();
        else
            EnsureFocusRestoreHandler().TriggerRestore();
    }
#endif

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_ANDROID
        if (hasFocus)
            TryOpenVlcAfterPermissionGranted();
#else
        // 非 Android 平台没有 PICO session 事件，继续使用 Unity 应用焦点兜底。
        if (objectsToHideWhenVlcOpens == null) return;
        if (!hasFocus)
            EnsureFocusRestoreHandler().HideControllers();
        else
            EnsureFocusRestoreHandler().TriggerRestore();
#endif
    }

    private VlcFocusRestoreHandler EnsureFocusRestoreHandler()
    {
        if (m_FocusRestoreHandler == null)
        {
            m_FocusRestoreHandler = new VlcFocusRestoreHandler(
                this,
                () => objectsToHideWhenVlcOpens,
                () => m_ModalityManager,
                m_RestoreDelayFrames);
        }

        return m_FocusRestoreHandler;
    }

    /// <summary>
    /// 启动后延迟一帧打开 VLC 媒体库，确保 Activity、XR 和焦点恢复组件完成初始化。
    /// </summary>
    private IEnumerator OpenVlcLibraryOnStart()
    {
        yield return null;

        if (m_HasOpenedVlcLibraryOnStart) yield break;
        m_HasOpenedVlcLibraryOnStart = true;

        OpenVLCMediaLibrary();
    }

    /// <summary>
    /// 接收 Android VLC 侧发来的返回 Unity 视图通知。
    /// </summary>
    public void ShowUnityView()
    {
        Debug.Log("[VlcLibraryLauncher] 收到 Android 返回 Unity 视图通知。");
    }

    public void OnVlcActivityReady()
    {
        Debug.Log("[VlcLibraryLauncher] VLC Activity ready; hiding cold start splash when minimum display time is satisfied.");
        ColdStartSplashOverlay.MarkVlcActivityReady();
    }

    public void OpenVLCMediaLibrary()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.LogWarning("VLC 媒体库仅在 Android 平台可用。");
            return;
        }

        if (HasRequiredPermissions())
        {
            StartVLCActivity();
        }
        else
        {
            ColdStartSplashOverlay.Hide();
            m_OpenVlcAfterPermissionGranted = true;
            RequestRequiredPermissions();
            Debug.Log("正在请求 Android 权限，授权后将自动打开 VLC 媒体库。");
        }
    }

    private bool HasRequiredPermissions()
    {
#if UNITY_ANDROID
        using (AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            int sdkInt = buildVersion.GetStatic<int>("SDK_INT");
            if (sdkInt >= 33)
            {
                return Permission.HasUserAuthorizedPermission("android.permission.READ_MEDIA_VIDEO") &&
                       Permission.HasUserAuthorizedPermission("android.permission.READ_MEDIA_AUDIO") &&
                       Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS");
            }
            else if (sdkInt >= 30)
            {
                using (AndroidJavaClass environment = new AndroidJavaClass("android.os.Environment"))
                {
                    return environment.CallStatic<bool>("isExternalStorageManager");
                }
            }
            else
            {
                return Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead);
            }
        }
#else
        return true;
#endif
    }

    private void RequestRequiredPermissions()
    {
#if UNITY_ANDROID
        using (AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            int sdkInt = buildVersion.GetStatic<int>("SDK_INT");
            if (sdkInt >= 33)
            {
                EnsurePermissionCallbacks();
                Permission.RequestUserPermissions(new string[] {
                    "android.permission.READ_MEDIA_VIDEO",
                    "android.permission.READ_MEDIA_AUDIO",
                    "android.permission.POST_NOTIFICATIONS"
                }, m_PermissionCallbacks);
            }
            else if (sdkInt >= 30)
            {
                try
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION"))
                    using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
                    {
                        string packageName = currentActivity.Call<string>("getPackageName");
                        AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + packageName);
                        intent.Call<AndroidJavaObject>("setData", uri);
                        currentActivity.Call("startActivity", intent);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("请求所有文件访问权限失败: " + e.Message);
                    Permission.RequestUserPermission(Permission.ExternalStorageRead);
                }
            }
            else
            {
                Permission.RequestUserPermission(Permission.ExternalStorageRead);
            }
        }
#endif
    }

#if UNITY_ANDROID
    private void EnsurePermissionCallbacks()
    {
        if (m_PermissionCallbacks != null) return;

        m_PermissionCallbacks = new PermissionCallbacks();
        m_PermissionCallbacks.PermissionGranted += OnRuntimePermissionResolved;
        m_PermissionCallbacks.PermissionDenied += OnRuntimePermissionResolved;
        m_PermissionCallbacks.PermissionRequestDismissed += OnRuntimePermissionResolved;
    }

    private void ClearPermissionCallbacks()
    {
        if (m_PermissionCallbacks == null) return;

        m_PermissionCallbacks.PermissionGranted -= OnRuntimePermissionResolved;
        m_PermissionCallbacks.PermissionDenied -= OnRuntimePermissionResolved;
        m_PermissionCallbacks.PermissionRequestDismissed -= OnRuntimePermissionResolved;
        m_PermissionCallbacks = null;
    }

    private void OnRuntimePermissionResolved(string permissionName)
    {
        TryOpenVlcAfterPermissionGranted();
    }
#endif

    /// <summary>
    /// 从权限弹窗或系统设置页回到 Unity 后，如果权限已满足则继续打开 VLC 媒体库。
    /// </summary>
    private void TryOpenVlcAfterPermissionGranted()
    {
#if UNITY_ANDROID
        if (!m_OpenVlcAfterPermissionGranted) return;
        if (!HasRequiredPermissions()) return;

        m_OpenVlcAfterPermissionGranted = false;
        ClearPermissionCallbacks();
        StartVLCActivity();
#endif
    }

    private void StartVLCActivity()
    {
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                RestoreVlcTaskOrStartFallback(currentActivity);
            }
            Debug.Log("已发送启动 VLC 媒体库 Intent");
        }
        catch (Exception e)
        {
            ColdStartSplashOverlay.Hide();
            Debug.LogError("打开 VLC 媒体库失败: " + e.Message);
        }
    }

    private void RestoreVlcTaskOrStartFallback(AndroidJavaObject currentActivity)
    {
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(PlaybackServiceBridgeClassName))
            {
                if (bridge.CallStatic<bool>("restoreVlcTask", currentActivity))
                {
                    Debug.Log("已恢复现有 VLC task。");
                    return;
                }
            }
        }
        catch (Exception bridgeException)
        {
            Debug.LogWarning("恢复现有 VLC task 失败，改用启动入口: " + bridgeException.Message);
        }

        StartVlcStartActivity(currentActivity);
    }

    private void StartVlcStartActivity(AndroidJavaObject currentActivity)
    {
        using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", currentActivity, new AndroidJavaClass("org.videolan.vlc.StartActivity")))
        {
            intent.Call<AndroidJavaObject>("addFlags", FlagActivityNewTask);
            currentActivity.Call("startActivity", intent);
        }
    }
}
