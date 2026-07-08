using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class XrOverflowTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float TooltipHeight = 52f;
    private const float TooltipPaddingHorizontal = 12f;
    private const float TooltipPaddingVertical = 7f;
    private const int TooltipSortingOrder = 30000;
    private static readonly Color TooltipBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 1f);

    public TextMeshProUGUI sourceLabel;
    public string fullText = string.Empty;
    public GameObject tooltipRoot;
    public TextMeshProUGUI tooltipText;
    public Transform overlayRoot;
    public bool alignTopLeftToSource;
    public UnityEvent onBeforeShow = new UnityEvent();

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
        bool shouldShow = ShouldShowTooltip();
        Debug.Log($"[XrOverflowTooltip] PointerEnter entry shouldShow={shouldShow} {DescribeTooltipStateForLog(eventData)}");
        if (!shouldShow)
        {
            tooltipRoot.SetActive(false);
            Debug.Log($"[XrOverflowTooltip] PointerEnter hide reason=ShouldShowTooltipFalse {DescribeTooltipStateForLog(eventData)}");
            return;
        }

        if (onBeforeShow == null)
            onBeforeShow = new UnityEvent();
        Debug.Log($"[XrOverflowTooltip] PointerEnter beforeOnBeforeShow {DescribeTooltipStateForLog(eventData)}");
        onBeforeShow.Invoke();
        Debug.Log($"[XrOverflowTooltip] PointerEnter afterOnBeforeShow {DescribeTooltipStateForLog(eventData)}");
        tooltipText.text = fullText;
        tooltipRoot.SetActive(true);
        EnsureTooltipCanvasPriority();
        tooltipRoot.transform.SetAsLastSibling();
        PositionTooltipNearSource();
        Debug.Log($"[XrOverflowTooltip] PointerEnter shown {DescribeTooltipStateForLog(eventData)}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
        Debug.Log($"[XrOverflowTooltip] PointerExit {DescribeTooltipStateForLog(eventData)}");
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
        EnsureTooltipCanvasPriority();

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

    private void EnsureTooltipCanvasPriority()
    {
        if (tooltipRoot == null)
            return;

        Canvas referenceCanvas = GetComponentInParent<Canvas>();
        Canvas canvas = tooltipRoot.GetComponent<Canvas>();
        if (canvas == null)
            canvas = tooltipRoot.AddComponent<Canvas>();

        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = TooltipSortingOrder;
        if (referenceCanvas != null)
            canvas.sortingLayerID = referenceCanvas.sortingLayerID;
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

    private string DescribeTooltipStateForLog(PointerEventData eventData)
    {
        Canvas sourceCanvas = GetComponentInParent<Canvas>();
        Canvas tooltipCanvas = tooltipRoot != null ? tooltipRoot.GetComponent<Canvas>() : null;
        RectTransform ownerRect = transform as RectTransform;
        RectTransform tooltipRect = tooltipRoot != null ? tooltipRoot.GetComponent<RectTransform>() : null;
        RectTransform sourceRect = sourceLabel != null ? sourceLabel.rectTransform : ownerRect;
        GameObject pointerEnter = eventData != null ? eventData.pointerEnter : null;
        GameObject pointerPress = eventData != null ? eventData.pointerPress : null;

        return
            $"ownerPath={GetTransformPath(transform)} ownerActive={gameObject.activeSelf}/{gameObject.activeInHierarchy} " +
            $"overlayPath={GetTransformPath(overlayRoot)} tooltipPath={GetTransformPath(tooltipRoot != null ? tooltipRoot.transform : null)} " +
            $"tooltipActive={(tooltipRoot != null ? tooltipRoot.activeSelf.ToString() : "null")} " +
            $"fullTextLength={(fullText != null ? fullText.Length : -1)} alignTopLeft={alignTopLeftToSource} " +
            $"pointerEnter={DescribeGameObjectForLog(pointerEnter)} pointerPress={DescribeGameObjectForLog(pointerPress)} " +
            $"sourceCanvas={DescribeCanvasForLog(sourceCanvas)} tooltipCanvas={DescribeCanvasForLog(tooltipCanvas)} " +
            $"ownerRect={DescribeRectTransformForLog(ownerRect)} sourceRect={DescribeRectTransformForLog(sourceRect)} " +
            $"tooltipRect={DescribeRectTransformForLog(tooltipRect)}";
    }

    private static string DescribeCanvasForLog(Canvas canvas)
    {
        if (canvas == null)
            return "null";

        return
            $"{GetTransformPath(canvas.transform)} enabled={canvas.enabled} override={canvas.overrideSorting} " +
            $"sortingOrder={canvas.sortingOrder} sortingLayer={canvas.sortingLayerID} renderMode={canvas.renderMode} root={canvas.isRootCanvas}";
    }

    private static string DescribeRectTransformForLog(RectTransform rect)
    {
        if (rect == null)
            return "null";

        return
            $"{GetTransformPath(rect)} active={rect.gameObject.activeSelf}/{rect.gameObject.activeInHierarchy} " +
            $"anchored={rect.anchoredPosition} size={rect.rect.size} localPos={rect.localPosition} " +
            $"worldPos={rect.position} sibling={rect.GetSiblingIndex()}";
    }

    private static string DescribeGameObjectForLog(GameObject target)
    {
        if (target == null)
            return "null";

        return $"{GetTransformPath(target.transform)} active={target.activeSelf}/{target.activeInHierarchy}";
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
            return "null";

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
