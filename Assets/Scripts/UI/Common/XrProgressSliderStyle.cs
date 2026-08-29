using UnityEngine;
using UnityEngine.UI;

public static class XrProgressSliderStyle
{
    public static void Apply(Slider slider, Slider progressSlider)
    {
        if (slider == null || progressSlider == null)
            return;

        slider.transition = progressSlider.transition;
        slider.colors = progressSlider.colors;
        slider.spriteState = progressSlider.spriteState;
        slider.targetGraphic = null;

        SetHorizontalTrack(slider.transform.Find("Background") as RectTransform);
        SetHorizontalTrack(slider.transform.Find("Fill Area") as RectTransform);
        SetFullRect(slider.transform.Find("Handle Slide Area") as RectTransform);
        SetFullRect(slider.fillRect);

        CopyImageVisual(
            progressSlider.transform.Find("Background")?.GetComponent<Image>(),
            slider.transform.Find("Background")?.GetComponent<Image>());
        CopyImageVisual(
            progressSlider.fillRect != null ? progressSlider.fillRect.GetComponent<Image>() : null,
            slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null);
        ApplyHandleStyle(slider, progressSlider);
    }

    private static void ApplyHandleStyle(Slider slider, Slider progressSlider)
    {
        if (slider.handleRect == null || progressSlider.handleRect == null)
            return;

        CopyImageVisual(
            progressSlider.handleRect.GetComponent<Image>(),
            slider.handleRect.GetComponent<Image>());

        Image sourceDot = progressSlider.handleRect.Find("HandleDot")?.GetComponent<Image>();
        if (sourceDot == null)
            return;

        Transform existingDot = slider.handleRect.Find("ProgressStyleHandleDot");
        GameObject dotObject = existingDot != null
            ? existingDot.gameObject
            : new GameObject("ProgressStyleHandleDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existingDot == null)
            dotObject.transform.SetParent(slider.handleRect, false);

        RectTransform dotRect = dotObject.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = sourceDot.rectTransform.sizeDelta;

        Image dotImage = dotObject.GetComponent<Image>();
        CopyImageVisual(sourceDot, dotImage);
        dotImage.raycastTarget = false;
    }

    private static void SetHorizontalTrack(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0.4f);
        rect.anchorMax = new Vector2(1f, 0.6f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetFullRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void CopyImageVisual(Image source, Image target)
    {
        if (source == null || target == null)
            return;

        target.enabled = source.enabled;
        target.sprite = source.sprite;
        target.type = source.type;
        target.color = source.color;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillOrigin = source.fillOrigin;
        target.fillClockwise = source.fillClockwise;
        target.fillAmount = source.fillAmount;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }
}
