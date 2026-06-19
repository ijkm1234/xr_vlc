using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XrOverflowTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float TooltipHeight = 52f;
    private const float TooltipPaddingHorizontal = 12f;
    private const float TooltipPaddingVertical = 7f;
    private static readonly Color TooltipBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 0.82f);

    public TextMeshProUGUI sourceLabel;
    public string fullText = string.Empty;
    public GameObject tooltipRoot;
    public TextMeshProUGUI tooltipText;
    public Transform overlayRoot;

    public void SetOverlayRoot(Transform root)
    {
        overlayRoot = root;
        if (tooltipRoot != null)
            tooltipRoot.transform.SetParent(overlayRoot != null ? overlayRoot : transform, false);
    }

    public void SetSource(TextMeshProUGUI label, string text)
    {
        sourceLabel = label;
        fullText = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        EnsureTooltip();
        CopySourceStyle();
        tooltipText.text = fullText;
        tooltipRoot.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureTooltip();
        if (!ShouldShowTooltip())
        {
            tooltipRoot.SetActive(false);
            return;
        }

        tooltipText.text = fullText;
        tooltipRoot.SetActive(true);
        tooltipRoot.transform.SetAsLastSibling();
        PositionTooltipNearSource();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (tooltipRoot != null && tooltipRoot.transform.parent != transform)
            Destroy(tooltipRoot);
    }

    private void EnsureTooltip()
    {
        if (tooltipRoot != null && tooltipText != null)
            return;

        Transform existing = transform.Find("Tooltip");
        tooltipRoot = existing != null
            ? existing.gameObject
            : new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipRoot.transform.SetParent(overlayRoot != null ? overlayRoot : transform, false);
        tooltipRoot.transform.SetAsLastSibling();

        Image rootImage = tooltipRoot.GetComponent<Image>();
        rootImage.color = TooltipBackgroundColor;
        rootImage.raycastTarget = false;

        RectTransform rootRect = tooltipRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 4f);
        rootRect.sizeDelta = new Vector2(0f, TooltipHeight);

        Transform existingText = tooltipRoot.transform.Find("Text");
        GameObject textObject = existingText != null
            ? existingText.gameObject
            : new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(tooltipRoot.transform, false);

        tooltipText = textObject.GetComponent<TextMeshProUGUI>();
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
        tooltipText.enableWordWrapping = false;
        tooltipText.overflowMode = TextOverflowModes.Overflow;
        tooltipText.raycastTarget = false;

        RectTransform textRect = tooltipText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(TooltipPaddingHorizontal, TooltipPaddingVertical);
        textRect.offsetMax = new Vector2(-TooltipPaddingHorizontal, -TooltipPaddingVertical);
    }

    private void PositionTooltipNearSource()
    {
        if (tooltipRoot == null || sourceLabel == null)
            return;

        RectTransform tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        RectTransform sourceRect = sourceLabel.rectTransform;
        RectTransform parentRect = tooltipRect != null ? tooltipRect.parent as RectTransform : null;
        if (tooltipRect == null || sourceRect == null || parentRect == null)
            return;

        Vector3[] corners = new Vector3[4];
        sourceRect.GetWorldCorners(corners);
        Vector3 topLeft = parentRect.InverseTransformPoint(corners[1]);
        Vector3 topRight = parentRect.InverseTransformPoint(corners[2]);

        tooltipRect.anchorMin = new Vector2(0f, 1f);
        tooltipRect.anchorMax = new Vector2(0f, 1f);
        tooltipRect.pivot = new Vector2(0f, 0f);
        Rect parentRectBounds = parentRect.rect;
        tooltipRect.anchoredPosition = new Vector2(
            topLeft.x - parentRectBounds.xMin - TooltipPaddingHorizontal,
            topLeft.y - parentRectBounds.yMax + 4f);
        Vector2 preferred = tooltipText != null
            ? tooltipText.GetPreferredValues(fullText, Mathf.Infinity, TooltipHeight)
            : new Vector2(sourceRect.rect.width, TooltipHeight);
        tooltipRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Max(
                topRight.x - topLeft.x + TooltipPaddingHorizontal * 2f,
                preferred.x + TooltipPaddingHorizontal * 2f,
                sourceRect.rect.width));
        tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, TooltipHeight);
    }

    private void CopySourceStyle()
    {
        if (tooltipText == null || sourceLabel == null)
            return;

        tooltipText.font = sourceLabel.font;
        tooltipText.fontSharedMaterial = sourceLabel.fontSharedMaterial;
        tooltipText.fontSize = sourceLabel.fontSize;
        tooltipText.fontStyle = sourceLabel.fontStyle;
        tooltipText.color = Color.white;
    }

    private bool ShouldShowTooltip()
    {
        if (sourceLabel == null || string.IsNullOrEmpty(fullText))
            return false;

        RectTransform rect = sourceLabel.rectTransform;
        if (rect == null)
            return false;

        float availableWidth = rect.rect.width - sourceLabel.margin.x - sourceLabel.margin.z;
        if (availableWidth <= 0f)
            return false;

        Vector2 preferred = sourceLabel.GetPreferredValues(fullText, Mathf.Infinity, rect.rect.height);
        return preferred.x > availableWidth + 1f;
    }
}
