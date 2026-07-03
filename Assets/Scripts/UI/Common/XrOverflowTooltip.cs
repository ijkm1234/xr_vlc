using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XrOverflowTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float TooltipHeight = 52f;
    private const float TooltipPaddingHorizontal = 12f;
    private const float TooltipPaddingVertical = 7f;
    private static readonly Color TooltipBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 1f);

    public TextMeshProUGUI sourceLabel;
    public string fullText = string.Empty;
    public GameObject tooltipRoot;
    public TextMeshProUGUI tooltipText;
    public Transform overlayRoot;
    public bool alignTopLeftToSource;

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
        Transform existing = transform.Find("Tooltip");
        if (tooltipRoot == null)
        {
            tooltipRoot = existing != null
                ? existing.gameObject
                : new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        }

        tooltipRoot.transform.SetParent(overlayRoot != null ? overlayRoot : transform, false);
        tooltipRoot.transform.SetAsLastSibling();
        EnsureTooltipRootLayout();
        ApplyTooltipRootStyle();

        Transform existingText = tooltipRoot.transform.Find("Text");
        if (tooltipText == null)
        {
            GameObject textObject = existingText != null
                ? existingText.gameObject
                : new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(tooltipRoot.transform, false);
            tooltipText = textObject.GetComponent<TextMeshProUGUI>();
        }

        ApplyTooltipTextStyle();
    }

    private void EnsureTooltipRootLayout()
    {
        LayoutElement layoutElement = tooltipRoot.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = tooltipRoot.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
    }

    private void ApplyTooltipRootStyle()
    {
        Image rootImage = tooltipRoot.GetComponent<Image>();
        if (rootImage == null)
            rootImage = tooltipRoot.AddComponent<Image>();
        rootImage.color = TooltipBackgroundColor;
        rootImage.raycastTarget = false;

        RectTransform rootRect = tooltipRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 4f);
        rootRect.sizeDelta = new Vector2(0f, TooltipHeight);
    }

    private void ApplyTooltipTextStyle()
    {
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
        tooltipText.fontSize = 20f;
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
        if (tooltipRoot == null)
            return;

        RectTransform tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        RectTransform sourceRect = sourceLabel != null ? sourceLabel.rectTransform : transform as RectTransform;
        RectTransform parentRect = tooltipRect != null ? tooltipRect.parent as RectTransform : null;
        if (tooltipRect == null || sourceRect == null || parentRect == null)
            return;

        Vector3[] corners = new Vector3[4];
        sourceRect.GetWorldCorners(corners);
        Vector3 topLeft = parentRect.InverseTransformPoint(corners[1]);
        Vector3 topRight = parentRect.InverseTransformPoint(corners[2]);
        float tooltipHeight = GetTooltipHeight();
        Vector2 preferred = tooltipText != null
            ? tooltipText.GetPreferredValues(fullText, Mathf.Infinity, tooltipHeight)
            : new Vector2(sourceRect.rect.width, TooltipHeight);

        tooltipRect.anchorMin = new Vector2(0f, 1f);
        tooltipRect.anchorMax = new Vector2(0f, 1f);
        Rect parentRectBounds = parentRect.rect;
        if (alignTopLeftToSource)
        {
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.anchoredPosition = new Vector2(
                topLeft.x - parentRectBounds.xMin,
                topLeft.y - parentRectBounds.yMax);
        }
        else
        {
            tooltipRect.pivot = new Vector2(0f, 0f);
            tooltipRect.anchoredPosition = new Vector2(
                topLeft.x - parentRectBounds.xMin - TooltipPaddingHorizontal,
                topLeft.y - parentRectBounds.yMax + 4f);
        }

        tooltipRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Max(
                topRight.x - topLeft.x + TooltipPaddingHorizontal * 2f,
                preferred.x + TooltipPaddingHorizontal * 2f,
                sourceRect.rect.width));
        tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, tooltipHeight);
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
        if (string.IsNullOrEmpty(fullText))
            return false;

        if (sourceLabel == null)
            return true;

        RectTransform rect = sourceLabel.rectTransform;
        if (rect == null)
            return false;

        float availableWidth = rect.rect.width - sourceLabel.margin.x - sourceLabel.margin.z;
        if (availableWidth <= 0f)
            return false;

        Vector2 preferred = sourceLabel.GetPreferredValues(fullText, Mathf.Infinity, rect.rect.height);
        return preferred.x > availableWidth + 1f;
    }

    private float GetTooltipHeight()
    {
        if (tooltipText == null)
            return TooltipHeight;

        Vector2 preferred = tooltipText.GetPreferredValues(fullText, Mathf.Infinity, Mathf.Infinity);
        return Mathf.Max(TooltipHeight, preferred.y + TooltipPaddingVertical * 2f);
    }
}
