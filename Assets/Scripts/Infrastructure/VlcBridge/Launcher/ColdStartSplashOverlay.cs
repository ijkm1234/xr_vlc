using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class ColdStartSplashOverlay
{
    private const string SplashResourcePath = "AppIcon/icon_pico_splash";
    private const int OverlayLayer = 5;
    private const float CanvasDistance = 2.5f;
    public const int MinimumVisibleMilliseconds = 500;
    private const float MinimumVisibleSeconds = MinimumVisibleMilliseconds / 1000f;
    private static readonly Vector2 CanvasSize = new Vector2(2200f, 1400f);
    private static readonly Vector2 IconSize = new Vector2(260f, 260f);

    private static GameObject s_Root;
    private static OverlayLifetime s_Lifetime;
    private static Coroutine s_HideCoroutine;
    private static float s_ShownAtRealtime;
    private static bool s_HideRequested;
    private static bool s_WorldCoordinatesAligned;
    private static bool s_WorldAlignmentTimedOut;

    public static bool IsVisible => s_Root != null;

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
        CancelPendingHide();
        Show();
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
        Camera splashCamera = CreateCamera(root.transform);
        CreateCanvas(splashCamera.transform, splashCamera, icon);

        s_Root = root;
        s_ShownAtRealtime = Time.realtimeSinceStartup;
        Debug.Log("[ColdStartSplashOverlay] Shown for VLC launch.");
    }

    private static void HideWhenReadyAfterMinimumVisibleTime()
    {
        if (!s_HideRequested || !IsWorldAlignmentGateReleased() || s_Root == null)
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
        if (s_Root == null)
            return 0f;

        float visibleSeconds = Time.realtimeSinceStartup - s_ShownAtRealtime;
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

    private static Camera CreateCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.layer = OverlayLayer;
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.localPosition = Vector3.zero;
        cameraObject.transform.localRotation = Quaternion.identity;

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << OverlayLayer;
        camera.depth = short.MaxValue;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 10f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        return camera;
    }

    private static void CreateCanvas(Transform parent, Camera camera, Sprite icon)
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = OverlayLayer;
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = Vector3.forward * CanvasDistance;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.002f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = CanvasSize;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

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
    }

    private sealed class OverlayLifetime : MonoBehaviour
    {
    }
}
