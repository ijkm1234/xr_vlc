using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

[Serializable]
public sealed class XrDropdownItemData
{
    public XrDropdownItemData(string primaryText, string secondaryText = "", object payload = null)
    {
        this.primaryText = primaryText ?? string.Empty;
        this.secondaryText = secondaryText ?? string.Empty;
        this.payload = payload;
    }

    public string primaryText;
    public string secondaryText;
    public object payload;
}

[Serializable]
public sealed class XrDropdownValueChangedEvent : UnityEvent<int> { }

public sealed class XrDropdown : MonoBehaviour
{
    private static readonly Color DefaultPanelColor = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color DefaultTransparentColor = new Color(1f, 1f, 1f, 0f);
    private static readonly Color DefaultHoverColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color DefaultSelectedColor = new Color(1f, 1f, 1f, 0.24f);
    private static readonly Color DefaultSelectedHoverColor = new Color(1f, 1f, 1f, 0.32f);
    private static readonly Color DefaultPressedColor = new Color(1f, 1f, 1f, 0.24f);
    private const int PopupBaseSortingOrder = 500;
    private static int _popupSortingOrder = PopupBaseSortingOrder;

    [Header("Sizing")]
    public float width = 520f;
    public int maxVisibleItems = 10;
    public float fontSize = 22f;
    public float horizontalPadding = 16f;
    public float verticalPadding = 15f;
    public float secondaryTextWidth = 86f;
    public float minRowHeight = 52f;

    [Header("XR Scrolling")]
    public XRNode scrollHand = XRNode.RightHand;
    public float stickThreshold = 0.25f;
    public float stickScrollRowsPerSecond = 8f;

    [Header("Behavior")]
    public bool showCaption = true;
    public bool closeOnSelect = true;
    public bool interactable = true;

    [Header("References")]
    public Button captionButton;
    public TextMeshProUGUI captionText;
    public GameObject popup;
    public RectTransform popupRect;
    public ScrollRect scrollRect;
    public RectTransform viewport;
    public RectTransform content;
    public XrDropdownItem rowPrefab;

    [Header("Events")]
    public XrDropdownValueChangedEvent onValueChanged = new XrDropdownValueChangedEvent();

    [Header("Colors")]
    public Color panelColor = DefaultPanelColor;
    public Color normalColor = DefaultTransparentColor;
    public Color hoverColor = DefaultHoverColor;
    public Color selectedColor = DefaultSelectedColor;
    public Color selectedHoverColor = DefaultSelectedHoverColor;
    public Color pressedColor = DefaultPressedColor;
    public Color textColor = Color.white;
    public Color secondaryTextColor = new Color(1f, 1f, 1f, 0.68f);

    private readonly List<XrDropdownItemData> _items = new List<XrDropdownItemData>();
    private readonly List<XrDropdownItem> _rows = new List<XrDropdownItem>();
    private int _value;
    private bool _isPointerInside;

    public float RowHeight => Mathf.Max(minRowHeight, Mathf.Ceil(fontSize + verticalPadding * 2f));
    public bool IsOpen => popup != null && popup.activeSelf;
    public GameObject ActiveList => IsOpen ? popup : null;
    public Transform TooltipOverlayRoot => popup != null ? popup.transform : transform;
    public int Value => _value;
    public int Count => _items.Count;

    private void Awake()
    {
        BindMissingReferences();
        ConfigureStaticReferences();
        ApplyLayout();
        if (popup != null)
            popup.SetActive(false);
    }

    private void Update()
    {
        HandleStickScroll();
    }

    public void SetItems(IReadOnlyList<XrDropdownItemData> items, int selectedIndex = 0, bool notify = false)
    {
        _items.Clear();
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
                _items.Add(items[i] ?? new XrDropdownItemData(string.Empty));
        }

        if (_items.Count == 0)
            selectedIndex = 0;
        else
            selectedIndex = Mathf.Clamp(selectedIndex, 0, _items.Count - 1);

        SetValueInternal(selectedIndex, notify);
        RebuildRows();
        ApplyLayout();
        RefreshShownValue();
    }

    public void SetItems(IReadOnlyList<string> items, int selectedIndex = 0, bool notify = false)
    {
        var data = new List<XrDropdownItemData>();
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
                data.Add(new XrDropdownItemData(items[i]));
        }
        SetItems(data, selectedIndex, notify);
    }

    public void SetPlaceholder(string placeholder)
    {
        SetItems(new[] { new XrDropdownItemData(placeholder) }, 0, false);
        SetInteractable(false);
    }

    public void SetInteractable(bool enabled)
    {
        interactable = enabled;
        if (captionButton != null)
            captionButton.interactable = enabled;
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].SetInteractable(enabled);
    }

    public void ApplyConfiguredLayout()
    {
        BindMissingReferences();
        ConfigureStaticReferences();
        ApplyLayout();
        RefreshShownValue();
        RefreshRows();
    }

    public void SetValueWithoutNotify(int value)
    {
        SetValueInternal(value, false);
        RefreshRows();
        RefreshShownValue();
    }

    public void Show()
    {
        if (!interactable || popup == null)
            return;

        gameObject.SetActive(true);
        EnsureOpenCanvasPriority();
        popup.SetActive(true);
        ApplyLayout();
        RefreshRows();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Hide()
    {
        if (popup != null)
            popup.SetActive(false);
        ResetOpenCanvasPriority();
        ResetCaptionGraphicState();
    }

    public void Toggle()
    {
        if (IsOpen)
            Hide();
        else
            Show();
    }

    public void CloseImmediately()
    {
        Hide();
    }

    public bool Contains(GameObject target)
    {
        return IsSelfOrChildOf(target, gameObject) || IsSelfOrChildOf(target, popup);
    }

    internal void SelectIndex(int index)
    {
        if (!interactable || index < 0 || index >= _items.Count)
            return;

        SetValueInternal(index, true);
        RefreshRows();
        RefreshShownValue();
        if (closeOnSelect)
            Hide();
    }

    private void SetValueInternal(int value, bool notify)
    {
        int clamped = _items.Count == 0 ? 0 : Mathf.Clamp(value, 0, _items.Count - 1);
        if (_value == clamped && !notify)
            return;

        _value = clamped;
        if (notify)
            onValueChanged.Invoke(_value);
    }

    private void RefreshShownValue()
    {
        if (captionText == null)
            return;

        captionText.text = _items.Count == 0 ? string.Empty : _items[_value].primaryText;
        StyleText(captionText, false);
    }

    private void RebuildRows()
    {
        if (content == null || rowPrefab == null)
            return;

        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null && _rows[i] != rowPrefab)
                Destroy(_rows[i].gameObject);
        _rows.Clear();

        rowPrefab.gameObject.SetActive(false);
        for (int i = 0; i < _items.Count; i++)
        {
            XrDropdownItem row = Instantiate(rowPrefab, content);
            row.gameObject.name = $"Item {i}";
            row.gameObject.SetActive(true);
            row.Bind(this, i, _items[i]);
            _rows.Add(row);
        }

        RefreshRows();
    }

    private void RefreshRows()
    {
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].SetSelected(i == _value);
    }

    private void ApplyLayout()
    {
        float rowHeight = RowHeight;
        int visibleCount = Mathf.Max(1, Mathf.Min(_items.Count > 0 ? _items.Count : 1, maxVisibleItems));
        float popupHeight = Mathf.Min(_items.Count, maxVisibleItems) * RowHeight;
        if (popupHeight <= 0f)
            popupHeight = rowHeight;

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, showCaption ? rowHeight : popupHeight);
        }

        if (captionButton != null)
        {
            captionButton.gameObject.SetActive(showCaption);
            RectTransform rect = captionButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
            }
        }

        RectTransform captionTextRect = captionText != null ? captionText.GetComponent<RectTransform>() : null;
        if (captionTextRect != null)
        {
            captionTextRect.anchorMin = Vector2.zero;
            captionTextRect.anchorMax = Vector2.one;
            captionTextRect.offsetMin = new Vector2(horizontalPadding, 0f);
            captionTextRect.offsetMax = new Vector2(-horizontalPadding, 0f);
        }

        if (popupRect != null)
        {
            popupRect.anchorMin = showCaption ? new Vector2(0f, 0f) : Vector2.zero;
            popupRect.anchorMax = showCaption ? new Vector2(1f, 0f) : Vector2.one;
            popupRect.pivot = showCaption ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
            popupRect.anchoredPosition = showCaption ? new Vector2(0f, -2f) : Vector2.zero;
            popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, popupHeight);
        }

        if (content != null)
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(rowHeight, _items.Count * rowHeight));
        }

        bool canScroll = _items.Count > maxVisibleItems;
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.enabled = canScroll || _items.Count > 0;
        }

        if (rowPrefab != null)
            rowPrefab.ApplyOwnerStyle();
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].ApplyOwnerStyle();
    }

    private void HandleStickScroll()
    {
        if (!IsOpen || !_isPointerInside || scrollRect == null || _items.Count <= maxVisibleItems)
            return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(scrollHand);
        if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            return;

        if (Mathf.Abs(axis.y) < stickThreshold)
            return;

        float contentHeight = Mathf.Max(RowHeight, _items.Count * RowHeight);
        float viewportHeight = Mathf.Max(RowHeight, Mathf.Min(_items.Count, maxVisibleItems) * RowHeight);
        float scrollableRows = Mathf.Max(1f, (contentHeight - viewportHeight) / RowHeight);
        float delta = axis.y * stickScrollRowsPerSecond * Time.deltaTime / scrollableRows;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + delta);
    }

    private void BindMissingReferences()
    {
        if (captionButton == null)
            captionButton = GetComponentInChildren<Button>(true);
        if (captionText == null && captionButton != null)
            captionText = captionButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (popup == null)
        {
            Transform found = transform.Find("Popup");
            if (found != null)
                popup = found.gameObject;
        }
        if (popupRect == null && popup != null)
            popupRect = popup.GetComponent<RectTransform>();
        if (scrollRect == null && popup != null)
            scrollRect = popup.GetComponentInChildren<ScrollRect>(true);
        if (viewport == null && scrollRect != null)
            viewport = scrollRect.viewport;
        if (content == null && scrollRect != null)
            content = scrollRect.content;
        if (rowPrefab == null && content != null)
            rowPrefab = content.GetComponentInChildren<XrDropdownItem>(true);
    }

    private void ConfigureStaticReferences()
    {
        if (captionButton != null)
        {
            captionButton.onClick.RemoveListener(Toggle);
            captionButton.onClick.AddListener(Toggle);
            captionButton.interactable = interactable;
        }

        if (popup != null)
        {
            Image image = popup.GetComponent<Image>();
            if (image != null)
                image.color = panelColor;

            EventTrigger trigger = popup.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = popup.AddComponent<EventTrigger>();
            EnsureTrigger(trigger, EventTriggerType.PointerEnter, _ => _isPointerInside = true);
            EnsureTrigger(trigger, EventTriggerType.PointerExit, _ => _isPointerInside = false);
        }

        if (scrollRect != null)
        {
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
        }
    }

    private void EnsureOpenCanvasPriority()
    {
        if (popup == null)
            return;

        Canvas parentCanvas = transform.parent != null
            ? transform.parent.GetComponentInParent<Canvas>()
            : null;
        int rootSortingOrder = NextPopupSortingOrder();
        Canvas rootCanvas = EnsureSortedCanvas(gameObject, parentCanvas, rootSortingOrder);
        int popupSortingOrder = NextPopupSortingOrder();
        EnsureSortedCanvas(popup, rootCanvas, popupSortingOrder);
    }

    private static Canvas EnsureSortedCanvas(GameObject target, Canvas referenceCanvas, int sortingOrder)
    {
        if (target == null)
            return null;

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas == null)
            canvas = target.AddComponent<Canvas>();
        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        if (referenceCanvas != null)
            canvas.sortingLayerID = referenceCanvas.sortingLayerID;

        if (target.GetComponent<GraphicRaycaster>() == null)
            target.AddComponent<GraphicRaycaster>();
        if (target.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            target.AddComponent<TrackedDeviceGraphicRaycaster>();

        return canvas;
    }

    private void ResetOpenCanvasPriority()
    {
        SetPriorityCanvasEnabled(popup, false);
    }

    private static void SetPriorityCanvasEnabled(GameObject target, bool enabled)
    {
        if (target == null)
            return;

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas != null && canvas.overrideSorting)
            canvas.enabled = enabled;
    }

    private void ResetCaptionGraphicState()
    {
        if (captionButton == null || captionButton.transition != Selectable.Transition.None)
            return;

        Graphic targetGraphic = captionButton != null ? captionButton.targetGraphic : null;
        if (targetGraphic == null)
            return;

        targetGraphic.color = Color.clear;
        targetGraphic.canvasRenderer.SetColor(Color.clear);
        targetGraphic.CrossFadeColor(Color.clear, 0f, true, true);
        if (captionText != null)
            captionText.ForceMeshUpdate();
    }

    private static int NextPopupSortingOrder()
    {
        _popupSortingOrder++;
        if (_popupSortingOrder < PopupBaseSortingOrder)
            _popupSortingOrder = PopupBaseSortingOrder + 1;
        return _popupSortingOrder;
    }

    internal void StyleText(TextMeshProUGUI text, bool secondary)
    {
        if (text == null)
            return;

        text.enabled = true;
        text.color = secondary ? secondaryTextColor : textColor;
        text.fontSize = fontSize;
        text.alignment = secondary ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private static void EnsureTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> callback)
    {
        for (int i = 0; i < trigger.triggers.Count; i++)
        {
            if (trigger.triggers[i].eventID == type)
            {
                trigger.triggers[i].callback.RemoveListener(callback);
                trigger.triggers[i].callback.AddListener(callback);
                return;
            }
        }

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private static bool IsSelfOrChildOf(GameObject target, GameObject parent)
    {
        if (target == null || parent == null)
            return false;

        Transform current = target.transform;
        Transform parentTransform = parent.transform;
        while (current != null)
        {
            if (current == parentTransform)
                return true;
            current = current.parent;
        }

        return false;
    }
}
