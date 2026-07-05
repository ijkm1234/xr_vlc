using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ProgressHoverTimeBubble : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    private const float BubbleWidth = 86f;
    private const float BubbleHeight = 34f;
    private const float BubbleGap = 5f;
    private const float BubbleFontSize = 19f;
    private static readonly Color BubbleBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 1f);

    private Slider _slider;
    private RectTransform _sliderRect;
    private GameObject _bubbleRoot;
    private RectTransform _bubbleRect;
    private TextMeshProUGUI _bubbleText;
    private long _totalTimeMs;

    private void Awake()
    {
        Bind(GetComponent<Slider>());
    }

    private void OnDisable()
    {
        HideBubble();
    }

    public void Bind(Slider slider)
    {
        _slider = slider;
        _sliderRect = _slider != null ? _slider.GetComponent<RectTransform>() : transform as RectTransform;
        EnsureBubble();
        HideBubble();
    }

    public void SetDuration(long totalTimeMs)
    {
        _totalTimeMs = Math.Max(0L, totalTimeMs);
        if (_totalTimeMs == 0L)
            HideBubble();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        UpdateProgressHoverTimeBubble(eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        UpdateProgressHoverTimeBubble(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideBubble();
    }

    private void UpdateProgressHoverTimeBubble(PointerEventData eventData)
    {
        if (_totalTimeMs <= 0L || _sliderRect == null)
        {
            HideBubble();
            return;
        }

        EnsureBubble();
        if (_bubbleRoot == null || _bubbleText == null || _bubbleRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _sliderRect,
                eventData.position,
                eventData.enterEventCamera,
                out Vector2 localPoint))
        {
            HideBubble();
            return;
        }

        Rect rect = _sliderRect.rect;
        float normalized = rect.width > 0f
            ? Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width)
            : 0f;
        long hoverTimeMs = (long)Math.Round(_totalTimeMs * normalized);

        _bubbleText.text = FormatTime(hoverTimeMs);
        _bubbleRoot.SetActive(true);
        _bubbleRoot.transform.SetAsLastSibling();
        PositionBubble(rect, normalized);
    }

    private void EnsureBubble()
    {
        if (_sliderRect == null)
            return;

        if (_bubbleRoot == null)
        {
            Transform existing = _sliderRect.Find("ProgressHoverTimeBubble");
            _bubbleRoot = existing != null
                ? existing.gameObject
                : new GameObject("ProgressHoverTimeBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            _bubbleRoot.transform.SetParent(_sliderRect, false);
        }

        _bubbleRect = _bubbleRoot.GetComponent<RectTransform>();
        _bubbleRect.anchorMin = new Vector2(0f, 0f);
        _bubbleRect.anchorMax = new Vector2(0f, 0f);
        _bubbleRect.pivot = new Vector2(0.5f, 0f);
        _bubbleRect.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);

        Image background = _bubbleRoot.GetComponent<Image>();
        background.color = BubbleBackgroundColor;
        background.raycastTarget = false;

        LayoutElement layout = _bubbleRoot.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        if (_bubbleText == null)
        {
            Transform existingText = _bubbleRoot.transform.Find("Text");
            GameObject textObject = existingText != null
                ? existingText.gameObject
                : new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(_bubbleRoot.transform, false);
            _bubbleText = textObject.GetComponent<TextMeshProUGUI>();
        }

        _bubbleText.color = Color.white;
        _bubbleText.alignment = TextAlignmentOptions.Center;
        _bubbleText.fontSize = BubbleFontSize;
        _bubbleText.enableWordWrapping = false;
        _bubbleText.overflowMode = TextOverflowModes.Overflow;
        _bubbleText.raycastTarget = false;

        RectTransform textRect = _bubbleText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void PositionBubble(Rect sliderRect, float normalized)
    {
        _bubbleRect.anchoredPosition = CalculateBubbleAnchoredPosition(sliderRect, normalized, BubbleGap);
    }

    public static Vector2 CalculateBubbleAnchoredPosition(Rect sliderRect, float normalized, float bubbleGap)
    {
        float clamped = Mathf.Clamp01(normalized);
        float localX = Mathf.Lerp(sliderRect.xMin, sliderRect.xMax, clamped);
        return new Vector2(localX - sliderRect.xMin, sliderRect.yMax + bubbleGap - sliderRect.yMin);
    }

    private void HideBubble()
    {
        if (_bubbleRoot != null)
            _bubbleRoot.SetActive(false);
    }

    private static string FormatTime(long ms)
    {
        TimeSpan t = TimeSpan.FromMilliseconds(ms);
        if (t.Hours > 0)
            return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
        return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
    }
}
