using UnityEngine;
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
    public static bool IsAarTaskVisible => Instance != null && Instance.m_AarVisible;

    public event Action<string, string> OnVideoSelectedEvent { add { } remove { } }

    public GameObject[] objectsToHideWhenVlcOpens;

    [SerializeField]
    private int m_RestoreDelayFrames = 3;

    [SerializeField]
    private bool m_OpenVlcLibraryOnStart = true;

    [SerializeField, Min(0.1f)]
    private float m_FocusPollIntervalSeconds = 0.1f;

    [SerializeField, Min(1)]
    private int m_PlaybackStateRetryCount = 10;

    private XRInputModalityManager m_ModalityManager;
    private bool? m_LastObservedXrFocused;
    private bool? m_LastObservedAarTaskVisible;
    private bool m_HasOpenedVlcLibraryOnStart;
    private bool m_ExternalMediaLaunchHandled;
    private VlcFocusRestoreHandler m_FocusRestoreHandler;
    private VlcHomePanelController m_HomePanelController;
    private Coroutine m_FocusPollCoroutine;
    private Coroutine m_FocusedUiCoroutine;
    private bool m_AarVisible;
    private bool m_RestoreAarAfterSystemPanel;
    private bool m_AwaitingPlaybackStartAfterAarReturn;
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
            return;
        }

        VlcPlaybackBridge.OnPlayRequestedEvent += OnPlayRequested;
        VlcPlaybackBridge.OnStateChangedEvent += OnPlaybackStateChanged;
#if UNITY_ANDROID
        PicoSessionEvents.SessionStateChanged += OnSessionStateChanged;
#endif
    }

    private void Start()
    {
        m_ModalityManager = FindAnyObjectByType<XRInputModalityManager>();
        EnsureFocusRestoreHandler();
        EnsureHomePanelController()?.SetVisible(false);
        Application.deepLinkActivated += OnDeepLinkActivated;

#if UNITY_ANDROID
        m_FocusPollCoroutine = StartCoroutine(PollPicoFocusState());
#endif
        if (Application.platform == RuntimePlatform.Android)
            StartCoroutine(HandleStartupLaunch());
    }

    private void OnDestroy()
    {
        Application.deepLinkActivated -= OnDeepLinkActivated;
        VlcPlaybackBridge.OnPlayRequestedEvent -= OnPlayRequested;
        VlcPlaybackBridge.OnStateChangedEvent -= OnPlaybackStateChanged;
        CancelFocusedUiWork();

        if (m_FocusPollCoroutine != null)
        {
            StopCoroutine(m_FocusPollCoroutine);
            m_FocusPollCoroutine = null;
        }

#if UNITY_ANDROID
        PicoSessionEvents.SessionStateChanged -= OnSessionStateChanged;
#endif
    }

#if UNITY_ANDROID
    // Fires earlier than OnApplicationFocus — driven by OpenXR session state machine.
    // Focused → anything: session lost exclusive input (Home key, activity switch, etc.)
    // anything → Focused: session fully regained input focus
    private void OnSessionStateChanged(XrSessionState state)
    {
        if (state == XrSessionState.Visible || state == XrSessionState.Focused)
            ColdStartSplashOverlay.MarkXrVisible();

        ApplyXrFocusState(state == XrSessionState.Focused, $"SessionStateChanged({state})");
    }

    private IEnumerator PollPicoFocusState()
    {
        float interval = Mathf.Max(0.1f, m_FocusPollIntervalSeconds);
        var wait = new WaitForSecondsRealtime(interval);

        while (true)
        {
            bool focused = PXR_Plugin.System.UPxr_GetFocusState();
            ApplyXrFocusState(focused, "100ms focus poll");
            yield return wait;
        }
    }
#endif

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_ANDROID
        if (!hasFocus)
        {
            ApplyXrFocusState(false, "OnApplicationFocus(false)");
            return;
        }

        // Android window focus can return before the OpenXR runtime returns input
        // focus. Query PXR instead of treating OnApplicationFocus(true) as authoritative.
        ApplyXrFocusState(
            PXR_Plugin.System.UPxr_GetFocusState(),
            "OnApplicationFocus(true)+PXR query");
        TryConsumeExternalMediaIntent();
#else
        // 非 Android 平台没有 PICO session 事件，继续使用 Unity 应用焦点兜底。
        if (objectsToHideWhenVlcOpens == null) return;
        if (!hasFocus)
            EnsureFocusRestoreHandler().HideControllers();
        else
            EnsureFocusRestoreHandler().TriggerRestore();
#endif
    }

    private void ApplyXrFocusState(bool focused, string source)
    {
        if (focused)
            ColdStartSplashOverlay.MarkXrVisible();

        bool aarTaskVisible = ResolveAarTaskVisibility(focused);
        bool focusChanged = !m_LastObservedXrFocused.HasValue ||
            m_LastObservedXrFocused.Value != focused;
        bool aarVisibilityChanged = !m_LastObservedAarTaskVisible.HasValue ||
            m_LastObservedAarTaskVisible.Value != aarTaskVisible;

        m_LastObservedXrFocused = focused;
        m_LastObservedAarTaskVisible = aarTaskVisible;

        if (focusChanged)
            Debug.Log($"[VlcLibraryLauncher] XR focus corrected: focused={focused}, source={source}");
        if (aarVisibilityChanged)
            Debug.Log($"[VlcLibraryLauncher] AAR task visibility corrected: visible={aarTaskVisible}, source={source}");

        ApplyControllerAndRayFocus(focused, aarTaskVisible, source);

        if (!focusChanged && !aarVisibilityChanged)
            return;

        if (!focused)
        {
            HandleUnityXrFocusLost();
            return;
        }

        if (aarTaskVisible)
        {
            CancelFocusedUiWork();
            EnsureHomePanelController()?.SetVisible(false);
            return;
        }

        BeginFocusedUiWork();
    }

    private bool ResolveAarTaskVisibility(bool focused)
    {
        bool visible = m_AarVisible || m_LastObservedAarTaskVisible == true;
        bool shouldQuery = focused &&
            (m_LastObservedXrFocused != true || visible);
        if (shouldQuery && TryGetVlcTaskVisible(out bool queriedVisible))
        {
            visible = queriedVisible;
            m_AarVisible = queriedVisible;
        }

        return visible;
    }

    // PICO can report the Unity OpenXR session as focused while the Android AAR
    // remains visible on another display. Restore controllers only when Unity is
    // focused and the AAR task has actually left the foreground.
    private void ApplyControllerAndRayFocus(bool focused, bool aarTaskVisible, string source)
    {
        bool shouldBeVisible = focused && !aarTaskVisible;
        VlcFocusRestoreHandler restoreHandler = EnsureFocusRestoreHandler();
        bool isActuallyVisible = restoreHandler.AreControllerVisualsVisible();
        bool isVisibilitySuppressed = restoreHandler.IsVisibilitySuppressed;

        if (shouldBeVisible)
        {
            // An untracked controller can legitimately have no active renderer. Restore
            // only when this handler is still suppressing its visual state.
            if (!isVisibilitySuppressed)
                return;
        }
        else if (isVisibilitySuppressed && !isActuallyVisible)
        {
            return;
        }

        Debug.Log(
            $"[VlcLibraryLauncher] Controller visibility corrected: visible={shouldBeVisible}, " +
            $"actualVisible={isActuallyVisible}, visibilitySuppressed={isVisibilitySuppressed}, " +
            $"xrFocused={focused}, aarTaskVisible={aarTaskVisible}, source={source}");

        if (shouldBeVisible)
            restoreHandler.TriggerRestore();
        else
            restoreHandler.HideControllers();
    }

    private void HandleUnityXrFocusLost()
    {
        CancelFocusedUiWork();
        EnsureHomePanelController()?.SetVisible(false);
    }

    private void BeginFocusedUiWork()
    {
        CancelFocusedUiWork();
        m_FocusedUiCoroutine = StartCoroutine(HandleFocusedUnityUi());
    }

    private void CancelFocusedUiWork()
    {
        if (m_FocusedUiCoroutine == null)
            return;

        StopCoroutine(m_FocusedUiCoroutine);
        m_FocusedUiCoroutine = null;
    }

    private IEnumerator HandleFocusedUnityUi()
    {
        CapturePendingAarRestoreRequest();

        if (m_RestoreAarAfterSystemPanel)
        {
            EnsureHomePanelController()?.SetVisible(false);

            while (m_LastObservedXrFocused == true)
            {
                long remainingCooldownMs = GetVlcRestoreCooldownRemainingMs();
                if (remainingCooldownMs > 0L)
                {
                    Debug.Log(
                        $"[VlcLibraryLauncher] AAR restore cooldown active; remainingMs={remainingCooldownMs}.");
                    yield return new WaitForSecondsRealtime(remainingCooldownMs / 1000f);

                    if (m_LastObservedXrFocused != true)
                    {
                        m_FocusedUiCoroutine = null;
                        yield break;
                    }

                    continue;
                }

                if (TryRestoreExistingVlcTask())
                {
                    m_RestoreAarAfterSystemPanel = false;
                    m_AarVisible = true;
                    m_LastObservedAarTaskVisible = true;
                    ApplyControllerAndRayFocus(
                        m_LastObservedXrFocused == true,
                        true,
                        "AAR task restored after system panel");
                    Debug.Log("[VlcLibraryLauncher] AAR restore cooldown elapsed; restored AAR after system panel.");
                    m_FocusedUiCoroutine = null;
                    yield break;
                }

                // The AAR also enforces the cooldown. If the boundary moved between
                // the query and restore call, wait for the newly reported remainder.
                if (GetVlcRestoreCooldownRemainingMs() > 0L)
                    continue;

                break;
            }

            m_RestoreAarAfterSystemPanel = false;
            m_AarVisible = false;
            Debug.LogWarning("[VlcLibraryLauncher] AAR restore after system panel failed; evaluating Unity Home panel.");
        }

        if (m_AarVisible)
        {
            if (!TryGetVlcTaskVisible(out bool aarTaskVisible))
            {
                EnsureHomePanelController()?.SetVisible(false);
                Debug.LogWarning("[VlcLibraryLauncher] Could not verify AAR task visibility; keeping Unity Home panel hidden.");
                m_FocusedUiCoroutine = null;
                yield break;
            }

            if (aarTaskVisible)
            {
                EnsureHomePanelController()?.SetVisible(false);
                m_FocusedUiCoroutine = null;
                yield break;
            }

            m_AarVisible = false;
        }

        yield return RefreshHomePanelForFocusedUnity();
        m_FocusedUiCoroutine = null;
    }

    private IEnumerator RefreshHomePanelForFocusedUnity()
    {
        VlcHomePanelController homePanel = EnsureHomePanelController();
        if (homePanel == null)
            yield break;

        homePanel.SetVisible(false);
        int attempts = m_AwaitingPlaybackStartAfterAarReturn
            ? Mathf.Max(10, m_PlaybackStateRetryCount)
            : 1;
        float interval = Mathf.Max(0.1f, m_FocusPollIntervalSeconds);
        var wait = new WaitForSecondsRealtime(interval);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (m_LastObservedXrFocused != true ||
                ColdStartSplashOverlay.IsVisible ||
                m_AarVisible ||
                m_RestoreAarAfterSystemPanel)
                yield break;

            bool querySucceeded =
                VlcPlaybackBridge.TryHasActivePlaybackSelection(out bool hasActiveVideoSelection);
            if (querySucceeded && hasActiveVideoSelection)
            {
                m_AwaitingPlaybackStartAfterAarReturn = false;
                homePanel.SetVisible(false);
                Debug.Log("[VlcLibraryLauncher] Unity focused with an active video selection; Home panel remains hidden.");
                yield break;
            }

            bool isLastAttempt = attempt == attempts - 1;
            if (querySucceeded && (!m_AwaitingPlaybackStartAfterAarReturn || isLastAttempt))
            {
                m_AwaitingPlaybackStartAfterAarReturn = false;
                homePanel.SetVisible(true);
                Debug.Log("[VlcLibraryLauncher] Unity focused without an active video selection; Home panel shown.");
                yield break;
            }

            if (isLastAttempt)
            {
                homePanel.SetVisible(false);
                Debug.LogWarning("[VlcLibraryLauncher] Playback state remained unknown; Home panel stays hidden.");
                yield break;
            }

            yield return wait;
        }
    }

    private void CapturePendingAarRestoreRequest()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (m_RestoreAarAfterSystemPanel)
            return;

        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(PlaybackServiceBridgeClassName))
            {
                if (bridge.CallStatic<bool>("consumeVlcRestoreAfterSystemPanel"))
                {
                    m_RestoreAarAfterSystemPanel = true;
                    EnsureHomePanelController()?.SetVisible(false);
                    Debug.Log("[VlcLibraryLauncher] Captured pending AAR restore after system panel.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[VlcLibraryLauncher] Failed to query pending AAR restore: " + e.Message);
        }
#endif
    }

    private bool TryRestoreExistingVlcTask()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass bridge = new AndroidJavaClass(PlaybackServiceBridgeClassName))
            {
                return bridge.CallStatic<bool>("restoreVlcTask", currentActivity);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[VlcLibraryLauncher] Failed to restore existing VLC task: " + e.Message);
        }
#endif
        return false;
    }

    private long GetVlcRestoreCooldownRemainingMs()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(PlaybackServiceBridgeClassName))
            {
                return Math.Max(
                    0L,
                    bridge.CallStatic<long>("getVlcRestoreCooldownRemainingMs"));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[VlcLibraryLauncher] Failed to query AAR restore cooldown: " + e.Message);
        }
#endif
        return 0L;
    }

    private bool TryGetVlcTaskVisible(out bool visible)
    {
        visible = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass bridge = new AndroidJavaClass(PlaybackServiceBridgeClassName))
            {
                int state = bridge.CallStatic<int>("getVlcTaskVisibilityState", currentActivity);
                if (state < 0)
                    return false;

                visible = state == 1;
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[VlcLibraryLauncher] Failed to query VLC task visibility: " + e.Message);
        }
#endif
        return false;
    }

    private void OnPlayRequested(XRVLC.Media.MediaWrapper media)
    {
        m_AwaitingPlaybackStartAfterAarReturn = false;
        m_AarVisible = false;
        CancelFocusedUiWork();
        EnsureHomePanelController()?.SetVisible(false);
        Debug.Log($"[VlcLibraryLauncher] Playback requested; Home panel hidden. uri={media?.Uri}");
    }

    private void OnPlaybackStateChanged(string state)
    {
        if (string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Ended", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase))
        {
            if (m_LastObservedXrFocused == true)
                BeginFocusedUiWork();
            return;
        }

        if (!string.Equals(state, "Opening", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(state, "Buffering", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(state, "Playing", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(state, "Paused", StringComparison.OrdinalIgnoreCase))
            return;

        m_AwaitingPlaybackStartAfterAarReturn = false;
        if (m_LastObservedXrFocused == true)
            BeginFocusedUiWork();
        else
            EnsureHomePanelController()?.SetVisible(false);
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

    private VlcHomePanelController EnsureHomePanelController()
    {
        if (m_HomePanelController == null)
        {
            m_HomePanelController = FindAnyObjectByType<VlcHomePanelController>(FindObjectsInactive.Include);
        }

        return m_HomePanelController;
    }

    private void PrepareVlcLaunch()
    {
        m_AarVisible = false;
        m_AwaitingPlaybackStartAfterAarReturn = false;
        CancelFocusedUiWork();
        EnsureHomePanelController()?.SetVisible(false);
        Debug.Log("[VlcLibraryLauncher] AAR launch requested by Unity.");
    }

    private void MarkVlcActivityVisible()
    {
        m_AarVisible = true;
        m_LastObservedAarTaskVisible = true;
        ApplyControllerAndRayFocus(
            m_LastObservedXrFocused == true,
            true,
            "OnVlcActivityReady");
        CancelFocusedUiWork();
        EnsureHomePanelController()?.SetVisible(false);
    }

    private void HandleVlcLaunchFailure()
    {
        m_AarVisible = false;
        if (m_LastObservedXrFocused == true)
            BeginFocusedUiWork();
    }

    private IEnumerator HandleStartupLaunch()
    {
        yield return null;

        if (TryConsumeExternalMediaIntent()) yield break;
        if (m_ExternalMediaLaunchHandled) yield break;

        if (m_OpenVlcLibraryOnStart && Application.platform == RuntimePlatform.Android)
            StartCoroutine(OpenVlcLibraryOnStart());
    }

    private bool TryConsumeExternalMediaIntent()
    {
        return TryConsumeExternalMediaIntent(Application.absoluteURL);
    }

    private bool TryConsumeExternalMediaIntent(string fallbackUrl)
    {
        if (Application.platform != RuntimePlatform.Android) return false;

        if (!ExternalMediaIntentBridge.TryConsumeCurrentIntent(out string payload) &&
            !ExternalMediaIntentBridge.TryBuildPayloadFromUrl(fallbackUrl, out payload))
        {
            return false;
        }

        m_ExternalMediaLaunchHandled = true;
        EnsureHomePanelController()?.SetVisible(false);
        StartCoroutine(PlayExternalMediaAfterSceneReady(payload));
        return true;
    }

    private IEnumerator PlayExternalMediaAfterSceneReady(string payload)
    {
        yield return null;

        ColdStartSplashOverlay.Hide();
        VlcPlaybackBridge playbackBridge = FindAnyObjectByType<VlcPlaybackBridge>();
        if (playbackBridge == null)
        {
            Debug.LogError("[VlcLibraryLauncher] External media intent consumed, but VlcPlaybackBridge was not found.");
            yield break;
        }

        Debug.Log("[VlcLibraryLauncher] External media intent consumed; starting Unity playback.");
        playbackBridge.StartPlay(payload);
    }

    private void OnDeepLinkActivated(string url)
    {
        TryConsumeExternalMediaIntent(url);
    }

    /// <summary>
    /// XR 首次可见后由 Unity 保持 Splash 至少 500ms，再打开 VLC 媒体库。
    /// </summary>
    private IEnumerator OpenVlcLibraryOnStart()
    {
        yield return null;

        if (m_HasOpenedVlcLibraryOnStart) yield break;
        m_HasOpenedVlcLibraryOnStart = true;

        Debug.Log("[VlcLibraryLauncher] Waiting for Unity-controlled cold-start delay.");
        while (!ColdStartSplashOverlay.HasReachedMinimumVisibleTime)
        {
            if (m_ExternalMediaLaunchHandled)
                yield break;

            yield return null;
        }

        // Let the frame that satisfies the duration reach the XR compositor before
        // handing foreground control to the Android 2D activity.
        yield return new WaitForEndOfFrame();

        if (m_ExternalMediaLaunchHandled)
            yield break;

        Debug.Log("[VlcLibraryLauncher] Unity-controlled cold-start delay elapsed; launching VLC Activity.");
        OpenVLCMediaLibrary();
    }

    /// <summary>
    /// 接收 Android VLC 侧发来的返回 Unity 视图通知。
    /// </summary>
    public void ShowUnityView()
    {
        m_AarVisible = false;
        m_RestoreAarAfterSystemPanel = false;
        m_AwaitingPlaybackStartAfterAarReturn = true;
        CancelFocusedUiWork();
        EnsureHomePanelController()?.SetVisible(false);
        Debug.Log("[VlcLibraryLauncher] 收到 Android 返回 Unity 视图通知。");
    }

    public void OnVlcActivityReady()
    {
        MarkVlcActivityVisible();
        Debug.Log("[VlcLibraryLauncher] VLC Activity started; hiding cold start splash when minimum display time is satisfied.");
        ColdStartSplashOverlay.MarkVlcActivityReady();
    }

    public void OpenVLCMediaLibrary()
    {
        EnsureHomePanelController()?.SetVisible(false);

        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.LogWarning("VLC 媒体库仅在 Android 平台可用。");
            return;
        }

        StartVLCActivity();
    }

    private void StartVLCActivity()
    {
        PrepareVlcLaunch();
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
            HandleVlcLaunchFailure();
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
