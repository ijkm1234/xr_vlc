using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(RectMask2D))]
[AddComponentMenu("XRVLC/UI/XR Scrolling Title Text")]
public sealed class XrScrollingTitleText : MonoBehaviour
{
    public TextMeshProUGUI text;
    public float scrollSpeed = 36f;
    public float startDelay = 0.8f;
    public float endDelay = 0.8f;

    private RectTransform _viewportRect;
    private RectTransform _textRect;
    private float _contentWidth;
    private float _viewportWidth;
    private float _maxOffset;
    private float _offset;
    private float _pauseRemaining;
    private bool _pausedAtEnd;

#if UNITY_EDITOR
    private bool _editorRefreshQueued;

    private void Reset()
    {
        EnsureReferences();
        QueueEditorRefresh();
    }

    private void OnValidate()
    {
        EnsureReferences();
        QueueEditorRefresh();
    }

    private void QueueEditorRefresh()
    {
        if (_editorRefreshQueued)
            return;

        _editorRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += ApplyQueuedEditorRefresh;
    }

    private void ApplyQueuedEditorRefresh()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyQueuedEditorRefresh;
        if (this == null)
            return;

        _editorRefreshQueued = false;
        EnsureReferences();
        ConfigureText();
        ResetScroll();
    }
#endif

    private void Awake()
    {
        EnsureReferences();
        ConfigureText();
        ResetScroll();
    }

    private void OnEnable()
    {
        EnsureReferences();
        ConfigureText();
        ResetScroll();
    }

    private void Update()
    {
        EnsureReferences();
        if (text == null || _viewportRect == null || _textRect == null)
            return;

        UpdateMeasurements();
        if (_maxOffset <= 0f)
        {
            ApplyOffset(0f);
            return;
        }

        if (_pauseRemaining > 0f)
        {
            _pauseRemaining -= Time.deltaTime;
            return;
        }

        if (_pausedAtEnd)
        {
            ResetScroll();
            return;
        }

        ApplyOffset(Mathf.Min(_maxOffset, _offset + scrollSpeed * Time.deltaTime));
        if (_offset >= _maxOffset)
        {
            _pausedAtEnd = true;
            _pauseRemaining = Mathf.Max(0f, endDelay);
        }
    }

    public void SetText(string value)
    {
        EnsureReferences();

        string next = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        if (text != null)
        {
            text.enabled = true;
            text.text = next;
        }

        ConfigureText();
        ResetScroll();
    }

    private void EnsureReferences()
    {
        if (_viewportRect == null)
            _viewportRect = GetComponent<RectTransform>();

        if (text == null)
            text = GetComponentInChildren<TextMeshProUGUI>(true);

        if (text != null && _textRect == null)
            _textRect = text.rectTransform;
    }

    private void ConfigureText()
    {
        if (text == null)
            return;

        text.enableWordWrapping = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ResetScroll()
    {
        _offset = 0f;
        _pausedAtEnd = false;
        _pauseRemaining = Mathf.Max(0f, startDelay);
        UpdateMeasurements();
        ApplyOffset(0f);
    }

    private void UpdateMeasurements()
    {
        if (text == null || _viewportRect == null || _textRect == null)
            return;

        text.ForceMeshUpdate();
        _viewportWidth = Mathf.Max(0f, _viewportRect.rect.width);
        _contentWidth = Mathf.Max(_viewportWidth, text.preferredWidth);
        _maxOffset = Mathf.Max(0f, _contentWidth - _viewportWidth);

        _textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _contentWidth);
        _textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _viewportRect.rect.height);
        _offset = Mathf.Min(_offset, _maxOffset);
    }

    private void ApplyOffset(float value)
    {
        _offset = Mathf.Max(0f, value);
        if (_textRect != null)
            _textRect.anchoredPosition = new Vector2(-_offset, 0f);
    }
}
