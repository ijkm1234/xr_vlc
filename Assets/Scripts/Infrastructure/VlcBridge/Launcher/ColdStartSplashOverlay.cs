using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class ColdStartSplashOverlay
{
    private const string SplashResourcePath = "AppIcon/icon_pico_splash";
    private const int OverlayLayer = 5;
    private const float CanvasDistance = 2.0f;
    private const float CanvasScale = 0.002f;
    public const int MinimumVisibleMilliseconds = 500;
    private const float MinimumVisibleSeconds = MinimumVisibleMilliseconds / 1000f;
    private static readonly Vector2 CanvasSize = new Vector2(3200f, 2200f);
    private static readonly Vector2 IconSize = new Vector2(320f, 320f);

    private static GameObject s_Root;
    private static OverlayLifetime s_Lifetime;
    private static Coroutine s_HideCoroutine;
    private static float s_XrVisibleAtRealtime;
    private static bool s_XrVisibleTimerStarted;
    private static bool s_HideRequested;
    private static bool s_WorldCoordinatesAligned;
    private static bool s_WorldAlignmentTimedOut;

    public static bool IsVisible => s_Root != null;
    public static bool HasReachedMinimumVisibleTime =>
        s_XrVisibleTimerStarted && GetRemainingMinimumVisibleSeconds() <= 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ShowBeforeSceneLoad()
    {
#if UNITY_ANDROID
        ShowForColdStart();
#endif
    }

    private static void ShowForColdStart()
    {
#if UNITY_ANDROID
        if (Application.platform != RuntimePlatform.Android)
            return;
#endif
        s_HideRequested = false;
        s_WorldCoordinatesAligned = false;
        s_WorldAlignmentTimedOut = false;
        s_XrVisibleAtRealtime = 0f;
        s_XrVisibleTimerStarted = false;
        CancelPendingHide();
        Show();
    }

    public static void MarkXrVisible()
    {
        if (s_XrVisibleTimerStarted)
            return;

        s_XrVisibleAtRealtime = Time.realtimeSinceStartup;
        s_XrVisibleTimerStarted = true;
        Debug.Log("[ColdStartSplashOverlay] XR visible; minimum visible timer started.");
        HideWhenReadyAfterMinimumVisibleTime();
    }

    public static void MarkVlcActivityReady()
    {
        if (s_Root == null)
            return;

        s_HideRequested = true;
        HideWhenReadyAfterMinimumVisibleTime();
    }

    public static void MarkWorldCoordinatesAligned()
    {
        s_WorldCoordinatesAligned = true;
        Debug.Log("[ColdStartSplashOverlay] World coordinates aligned.");
        HideWhenReadyAfterMinimumVisibleTime();
    }

    public static void MarkWorldAlignmentTimedOut()
    {
        s_WorldAlignmentTimedOut = true;
        Debug.LogWarning("[ColdStartSplashOverlay] World alignment timed out; splash alignment gate released.");
        HideWhenReadyAfterMinimumVisibleTime();
    }

    public static void Hide()
    {
        s_HideRequested = true;
        HideWhenReadyAfterMinimumVisibleTime();
    }

    private static void Show()
    {
        if (s_Root != null)
            return;

        Sprite icon = Resources.Load<Sprite>(SplashResourcePath);
        if (icon == null)
        {
            Debug.LogWarning($"[ColdStartSplashOverlay] Splash sprite not found at Resources/{SplashResourcePath}.");
            return;
        }

        GameObject root = new GameObject("ColdStartSplashOverlay");
        root.layer = OverlayLayer;
        Object.DontDestroyOnLoad(root);

        s_Lifetime = root.AddComponent<OverlayLifetime>();
        Canvas canvas = CreateCanvas(root.transform, icon);
        s_Lifetime.Initialize(canvas);

        s_Root = root;
        Debug.Log("[ColdStartSplashOverlay] Shown for VLC launch.");
    }

    private static void HideWhenReadyAfterMinimumVisibleTime()
    {
        if (!s_HideRequested || !IsWorldAlignmentGateReleased() || s_Root == null ||
            !s_XrVisibleTimerStarted)
            return;

        float remainingSeconds = GetRemainingMinimumVisibleSeconds();
        if (remainingSeconds <= 0f)
        {
            HideImmediately();
            return;
        }

        if (s_HideCoroutine == null && s_Lifetime != null)
            s_HideCoroutine = s_Lifetime.StartCoroutine(HideAfterDelay(remainingSeconds));
    }

    private static float GetRemainingMinimumVisibleSeconds()
    {
        float visibleSeconds = Time.realtimeSinceStartup - s_XrVisibleAtRealtime;
        return MinimumVisibleSeconds - visibleSeconds;
    }

    private static IEnumerator HideAfterDelay(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        s_HideCoroutine = null;

        if (s_HideRequested && IsWorldAlignmentGateReleased())
            HideImmediately();
    }

    private static bool IsWorldAlignmentGateReleased()
    {
        return s_WorldCoordinatesAligned || s_WorldAlignmentTimedOut;
    }

    private static void HideImmediately()
    {
        CancelPendingHide();

        if (s_Root == null)
            return;

        Object.Destroy(s_Root);
        s_Root = null;
        s_Lifetime = null;
        s_HideRequested = false;
        Debug.Log("[ColdStartSplashOverlay] Hidden.");
    }

    private static void CancelPendingHide()
    {
        if (s_HideCoroutine == null || s_Lifetime == null)
        {
            s_HideCoroutine = null;
            return;
        }

        s_Lifetime.StopCoroutine(s_HideCoroutine);
        s_HideCoroutine = null;
    }

    private static Canvas CreateCanvas(Transform parent, Sprite icon)
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = OverlayLayer;
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = Vector3.zero;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * CanvasScale;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = CanvasSize;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.layer = OverlayLayer;
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.layer = OverlayLayer;
        iconObject.transform.SetParent(canvasObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = IconSize;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        return canvas;
    }

    private sealed class OverlayLifetime : MonoBehaviour
    {
        private Canvas m_Canvas;
        private Camera m_XrCamera;
        private bool m_HasLoggedCameraBinding;

        public void Initialize(Canvas canvas)
        {
            m_Canvas = canvas;
            AlignToXrCamera();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += AlignToXrCamera;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= AlignToXrCamera;
        }

        private void LateUpdate()
        {
            AlignToXrCamera();
        }

        private void AlignToXrCamera()
        {
            if (!IsUsableXrCamera(m_XrCamera))
                m_XrCamera = FindXrCamera();

            if (m_XrCamera == null)
                return;

            Transform cameraTransform = m_XrCamera.transform;
            transform.SetPositionAndRotation(
                cameraTransform.position + cameraTransform.forward * CanvasDistance,
                cameraTransform.rotation);

            if (m_Canvas != null && m_Canvas.worldCamera != m_XrCamera)
                m_Canvas.worldCamera = m_XrCamera;

            if (m_HasLoggedCameraBinding)
                return;

            m_HasLoggedCameraBinding = true;
            Debug.Log($"[ColdStartSplashOverlay] Bound to XR camera '{m_XrCamera.name}'.");
        }

        private static Camera FindXrCamera()
        {
            Camera mainCamera = Camera.main;
            if (IsUsableXrCamera(mainCamera))
                return mainCamera;

            foreach (Camera camera in Camera.allCameras)
            {
                if (IsUsableXrCamera(camera))
                    return camera;
            }

            return null;
        }

        private static bool IsUsableXrCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.stereoTargetEye != StereoTargetEyeMask.None;
        }
    }
}
