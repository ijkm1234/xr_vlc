using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XrDropdownItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color SeparatorColor = new Color(1f, 1f, 1f, 0.9f);

    public Button button;
    public Image background;
    public TextMeshProUGUI primaryText;
    public TextMeshProUGUI secondaryText;
    public XrOverflowTooltip tooltip;

    private XrDropdown _owner;
    private int _index;
    private bool _selected;
    private bool _hovered;
    private Image _bottomSeparator;

    public void Bind(XrDropdown owner, int index, XrDropdownItemData data)
    {
        _owner = owner;
        _index = index;
        BindMissingReferences();

        if (primaryText != null)
            primaryText.text = data?.primaryText ?? string.Empty;
        if (secondaryText != null)
        {
            secondaryText.text = data?.secondaryText ?? string.Empty;
            secondaryText.gameObject.SetActive(!string.IsNullOrEmpty(data?.secondaryText));
        }

        if (tooltip != null)
        {
            if (_owner != null)
                tooltip.SetOverlayRoot(_owner.TooltipOverlayRoot);
            tooltip.SetSource(primaryText, data?.primaryText ?? string.Empty);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _owner?.SelectIndex(_index));
        }

        SetBottomSeparatorVisible(data?.showBottomSeparator == true);
        ApplyOwnerStyle();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyColors();
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    public void ApplyOwnerStyle()
    {
        BindMissingReferences();
        if (_owner == null)
            return;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _owner.width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _owner.RowHeight);
        }

        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout == null)
            layout = gameObject.AddComponent<LayoutElement>();
        layout.minHeight = _owner.RowHeight;
        layout.preferredHeight = _owner.RowHeight;
        layout.flexibleHeight = 0f;

        LayoutText(primaryText, false);
        LayoutText(secondaryText, true);
        _owner.StyleText(primaryText, false);
        _owner.StyleText(secondaryText, true);
        LayoutBottomSeparator();
        ApplyColors();
    }

    private void SetBottomSeparatorVisible(bool visible)
    {
        if (visible)
            EnsureBottomSeparator();
        if (_bottomSeparator != null)
            _bottomSeparator.gameObject.SetActive(visible);
    }

    private void EnsureBottomSeparator()
    {
        if (_bottomSeparator != null)
            return;

        GameObject separator = new GameObject("BottomSeparator", typeof(RectTransform), typeof(Image));
        separator.transform.SetParent(transform, false);
        _bottomSeparator = separator.GetComponent<Image>();
        _bottomSeparator.color = SeparatorColor;
        _bottomSeparator.raycastTarget = false;
        LayoutBottomSeparator();
    }

    private void LayoutBottomSeparator()
    {
        if (_owner == null || _bottomSeparator == null)
            return;

        RectTransform rect = _bottomSeparator.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(_owner.horizontalPadding, 0f);
        rect.offsetMax = new Vector2(-_owner.horizontalPadding, 2f);
        _bottomSeparator.transform.SetAsLastSibling();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        ApplyColors();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        ApplyColors();
    }

    private void ApplyColors()
    {
        if (_owner == null || background == null)
            return;

        Color normal = _selected ? _owner.selectedColor : _owner.normalColor;
        Color highlighted = _selected ? _owner.selectedHoverColor : _owner.hoverColor;
        background.color = _hovered ? highlighted : normal;
        background.raycastTarget = true;

        if (button != null)
        {
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.selectedColor = highlighted;
            colors.pressedColor = _owner.pressedColor;
            colors.disabledColor = normal;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }
    }

    private void LayoutText(TextMeshProUGUI text, bool secondary)
    {
        if (_owner == null || text == null)
            return;

        RectTransform rect = text.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = secondary ? new Vector2(1f, 0f) : Vector2.zero;
        rect.anchorMax = secondary ? new Vector2(1f, 1f) : Vector2.one;
        rect.pivot = secondary ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        if (secondary)
        {
            rect.anchoredPosition = new Vector2(-_owner.horizontalPadding, 0f);
            rect.sizeDelta = new Vector2(_owner.secondaryTextWidth, 0f);
        }
        else
        {
            float rightPadding = _owner.horizontalPadding + (!string.IsNullOrEmpty(secondaryText != null ? secondaryText.text : string.Empty)
                ? _owner.secondaryTextWidth + _owner.horizontalPadding
                : 0f);
            rect.offsetMin = new Vector2(_owner.horizontalPadding, 0f);
            rect.offsetMax = new Vector2(-rightPadding, 0f);
        }
    }

    private void BindMissingReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (background == null)
            background = GetComponent<Image>();
        if (primaryText == null)
        {
            Transform found = transform.Find("PrimaryText");
            if (found != null)
                primaryText = found.GetComponent<TextMeshProUGUI>();
        }
        if (secondaryText == null)
        {
            Transform found = transform.Find("SecondaryText");
            if (found != null)
                secondaryText = found.GetComponent<TextMeshProUGUI>();
        }
        if (tooltip == null)
            tooltip = GetComponent<XrOverflowTooltip>();
    }
}
