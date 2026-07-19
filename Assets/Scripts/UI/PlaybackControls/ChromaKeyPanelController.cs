using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRVLC;
using XRVLC.Infrastructure.Pico;
using XRVLC.Localization;
using XRVLC.Media;

public sealed class ChromaKeyPanelController : MonoBehaviour
{
    private const string SettingsStyleSwitchResourcePath = "UI/SettingsStyleSwitch";
    private static readonly Color PanelColor = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color SwitchOnColor = new Color(1f, 0.53333336f, 0f, 1f);
    private static readonly Color SwitchOffTrackColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color SwitchBorderColor = new Color(1f, 1f, 1f, 0.22f);
    private const float SwitchWidth = 76f;
    private const float SwitchHeight = 38f;
    private const float SwitchKnobSize = 30f;
    private const float SwitchTrackLength = SwitchWidth * 0.75f;
    private const float SwitchTrackThickness = SwitchKnobSize * 0.5f;
    private const float SwitchKnobTravel = (SwitchWidth - SwitchKnobSize) * 0.5f;
    private const float ChromaKeyParameterMax = 0.5f;
    private PlaybackService _playbackService;
    private PicoPassthroughModeService _passthroughService;
    private Button _seeThroughButton;
    private Toggle _togglePrefab;
    private GameObject _settingsStyleSwitchPrefab;
    private Slider _sliderPrefab;
    private Slider _progressSliderStyle;
    private Toggle _enabledToggle;
    private XrHsvColorPicker _colorPicker;
    private Slider _hueSlider;
    private TMP_InputField _hexInput;
    private Image _colorPreview;
    private Button _extractColorButton;
    private Slider _rangeSlider;
    private Slider _falloffSlider;
    private Toggle _edgeSmoothToggle;
    private Toggle _clipBlackToggle;
    private Toggle _clipWhiteToggle;
    private Toggle _despillToggle;
    private TextMeshProUGUI _rangeValue;
    private TextMeshProUGUI _falloffValue;
    private Texture2D _hueTexture;
    private bool _built;
    private bool _updatingUi;
    private bool _colorExtractionPending;
    private bool _previousPassthroughEnabled;
    private bool _hasPassthroughSnapshot;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsChromaKeyEnabled => _playbackService != null && _playbackService.CurrentChromaKeySettings.Enabled;

    public void Bind(
        PlaybackService playbackService,
        PicoPassthroughModeService passthroughService,
        Button seeThroughButton,
        Toggle togglePrefab,
        Slider sliderPrefab)
    {
        Unsubscribe();
        _playbackService = playbackService;
        _passthroughService = passthroughService;
        _seeThroughButton = seeThroughButton;
        _togglePrefab = togglePrefab;
        _settingsStyleSwitchPrefab = Resources.Load<GameObject>(SettingsStyleSwitchResourcePath);
        _sliderPrefab = sliderPrefab;
        BuildIfNeeded();
        Subscribe();
        ApplySettings(_playbackService != null ? _playbackService.CurrentChromaKeySettings : ChromaKeySettings.Default);
    }

    public void SetProgressSliderStyle(Slider progressSlider)
    {
        _progressSliderStyle = progressSlider;
        ApplyProgressSliderStyle(_rangeSlider);
        ApplyProgressSliderStyle(_falloffSlider);
    }

    public void Show()
    {
        BuildIfNeeded();
        ApplySettings(_playbackService != null ? _playbackService.CurrentChromaKeySettings : ChromaKeySettings.Default);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void BuildIfNeeded()
    {
        if (_built)
            return;

        if (_togglePrefab == null || _sliderPrefab == null)
        {
            Debug.LogError("[ChromaKeyPanel] Toggle and slider prefabs must be assigned.");
            return;
        }

        _built = true;

        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(560f, 840f);
        Image panel = GetComponent<Image>();
        if (panel != null)
        {
            panel.color = PanelColor;
            panel.raycastTarget = true;
        }

        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _enabledToggle = CreateSwitchRow(
            transform,
            "ChromaKeyEnabled",
            XrUiText.Get(XrUiTextKey.ChromaKeyEnabled));
        _enabledToggle.onValueChanged.AddListener(OnEnabledChanged);

        CreateLabel(transform, XrUiText.Get(XrUiTextKey.ChromaKeyColor), 24f, 30f);
        CreateColorPalette(transform);
        CreateHexRow(transform);
        CreateExtractColorButton(transform);

        _rangeSlider = CreatePercentSlider(
            transform,
            "ColorRange",
            XrUiText.Get(XrUiTextKey.ChromaKeyColorRange),
            out _rangeValue);
        _rangeSlider.onValueChanged.AddListener(OnRangeChanged);

        _falloffSlider = CreatePercentSlider(
            transform,
            "Falloff",
            XrUiText.Get(XrUiTextKey.ChromaKeyFalloff),
            out _falloffValue);
        _falloffSlider.onValueChanged.AddListener(OnFalloffChanged);

        CreatePostProcessSwitchGrid(transform);
    }

    private void CreateColorPalette(Transform parent)
    {
        GameObject row = CreateRow(parent, "HsvPaletteRow", 230f);

        GameObject pickerObject = new GameObject(
            "SaturationValuePalette",
            typeof(RectTransform),
            typeof(RawImage),
            typeof(LayoutElement),
            typeof(XrHsvColorPicker));
        pickerObject.transform.SetParent(row.transform, false);
        LayoutElement pickerLayout = pickerObject.GetComponent<LayoutElement>();
        pickerLayout.preferredWidth = 450f;
        pickerLayout.preferredHeight = 220f;
        pickerLayout.flexibleWidth = 1f;
        _colorPicker = pickerObject.GetComponent<XrHsvColorPicker>();
        _colorPicker.Initialize();
        _colorPicker.ColorChanged += OnPickerColorChanged;

        _hueSlider = CreateSlider(row.transform, "HueSlider", Slider.Direction.BottomToTop);
        LayoutElement hueLayout = _hueSlider.GetComponent<LayoutElement>();
        hueLayout.preferredWidth = 44f;
        hueLayout.preferredHeight = 220f;
        hueLayout.flexibleWidth = 0f;
        Transform background = _hueSlider.transform.Find("Background");
        Image oldBackground = background.GetComponent<Image>();
        oldBackground.enabled = false;
        GameObject spectrumObject = new GameObject("HueSpectrum", typeof(RectTransform), typeof(RawImage));
        spectrumObject.transform.SetParent(background, false);
        RectTransform spectrumRect = spectrumObject.GetComponent<RectTransform>();
        spectrumRect.anchorMin = Vector2.zero;
        spectrumRect.anchorMax = Vector2.one;
        spectrumRect.offsetMin = Vector2.zero;
        spectrumRect.offsetMax = Vector2.zero;
        RawImage spectrum = spectrumObject.GetComponent<RawImage>();
        _hueTexture = BuildHueTexture();
        spectrum.texture = _hueTexture;
        spectrum.raycastTarget = false;
        Image hueFill = _hueSlider.fillRect != null ? _hueSlider.fillRect.GetComponent<Image>() : null;
        if (hueFill != null)
            hueFill.enabled = false;
        _hueSlider.onValueChanged.AddListener(OnHueChanged);
    }

    private void CreateHexRow(Transform parent)
    {
        GameObject row = CreateRow(parent, "HexColorRow", 52f);
        CreateLabel(row.transform, XrUiText.Get(XrUiTextKey.ChromaKeyHex), 22f, 48f, 150f);

        GameObject inputObject = new GameObject(
            "HexInput",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField),
            typeof(LayoutElement));
        inputObject.transform.SetParent(row.transform, false);
        Image inputBackground = inputObject.GetComponent<Image>();
        inputBackground.color = new Color(1f, 1f, 1f, 0.12f);
        LayoutElement inputLayout = inputObject.GetComponent<LayoutElement>();
        inputLayout.preferredWidth = 250f;
        inputLayout.preferredHeight = 48f;
        inputLayout.flexibleWidth = 1f;

        TextMeshProUGUI text = CreateLabel(inputObject.transform, "2BE640", 22f, 48f);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);
        text.alignment = TextAlignmentOptions.MidlineLeft;

        _hexInput = inputObject.GetComponent<TMP_InputField>();
        _hexInput.textViewport = textRect;
        _hexInput.textComponent = text;
        _hexInput.characterLimit = 6;
        _hexInput.contentType = TMP_InputField.ContentType.Custom;
        _hexInput.inputType = TMP_InputField.InputType.Standard;
        _hexInput.characterValidation = TMP_InputField.CharacterValidation.None;
        _hexInput.onEndEdit.AddListener(OnHexCommitted);

        GameObject preview = new GameObject("ColorPreview", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        preview.transform.SetParent(row.transform, false);
        _colorPreview = preview.GetComponent<Image>();
        _colorPreview.raycastTarget = false;
        LayoutElement previewLayout = preview.GetComponent<LayoutElement>();
        previewLayout.preferredWidth = 48f;
        previewLayout.preferredHeight = 48f;
    }

    private void CreateExtractColorButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "ExtractKeyColorButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 48f;
        layout.flexibleWidth = 1f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.12f);
        if (_seeThroughButton != null)
        {
            CopyImageVisual(_seeThroughButton.targetGraphic as Image, image);
            image.color = new Color(1f, 1f, 1f, 0.12f);
        }

        _extractColorButton = buttonObject.GetComponent<Button>();
        _extractColorButton.targetGraphic = image;
        if (_seeThroughButton != null)
            _extractColorButton.colors = _seeThroughButton.colors;
        _extractColorButton.onClick.AddListener(OnExtractColorClicked);

        TextMeshProUGUI label = CreateLabel(
            buttonObject.transform,
            XrUiText.Get(XrUiTextKey.ChromaKeyExtractColor),
            22f,
            48f);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
    }

    private void CreatePostProcessSwitchGrid(Transform parent)
    {
        GameObject grid = new GameObject(
            "ChromaKeyPostProcessSwitches",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        grid.transform.SetParent(parent, false);
        VerticalLayoutGroup vertical = grid.GetComponent<VerticalLayoutGroup>();
        vertical.spacing = 6f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        grid.GetComponent<LayoutElement>().preferredHeight = 114f;

        GameObject firstRow = CreateRow(grid.transform, "ChromaKeyPostProcessRow1", 54f);
        _edgeSmoothToggle = CreateCompactSwitch(
            firstRow.transform,
            "ChromaKeyEdgeSmoothToggle",
            XrUiText.Get(XrUiTextKey.ChromaKeyEdgeSmooth));
        _clipBlackToggle = CreateCompactSwitch(
            firstRow.transform,
            "ChromaKeyClipBlackToggle",
            XrUiText.Get(XrUiTextKey.ChromaKeyClipBlack));

        GameObject secondRow = CreateRow(grid.transform, "ChromaKeyPostProcessRow2", 54f);
        _clipWhiteToggle = CreateCompactSwitch(
            secondRow.transform,
            "ChromaKeyClipWhiteToggle",
            XrUiText.Get(XrUiTextKey.ChromaKeyClipWhite));
        _despillToggle = CreateCompactSwitch(
            secondRow.transform,
            "ChromaKeyDespillToggle",
            XrUiText.Get(XrUiTextKey.ChromaKeyDespill));

        _edgeSmoothToggle.onValueChanged.AddListener(OnEdgeSmoothChanged);
        _clipBlackToggle.onValueChanged.AddListener(OnClipBlackChanged);
        _clipWhiteToggle.onValueChanged.AddListener(OnClipWhiteChanged);
        _despillToggle.onValueChanged.AddListener(OnDespillChanged);
    }

    private Toggle CreateSwitchRow(Transform parent, string name, string label)
    {
        GameObject row = CreateRow(parent, name + "Row", 54f);
        CreateLabel(row.transform, label, 24f, 50f, 360f).GetComponent<LayoutElement>().flexibleWidth = 1f;
        return CreateSwitchToggle(row.transform, name + "Toggle");
    }

    private Toggle CreateCompactSwitch(Transform parent, string name, string label)
    {
        GameObject cell = CreateRow(parent, name + "Cell", 54f);
        LayoutElement cellLayout = cell.GetComponent<LayoutElement>();
        cellLayout.flexibleWidth = 1f;
        cellLayout.preferredWidth = 0f;
        CreateLabel(cell.transform, label, 18f, 50f).GetComponent<LayoutElement>().flexibleWidth = 1f;
        return CreateSwitchToggle(cell.transform, name);
    }

    private Toggle CreateSwitchToggle(Transform parent, string name)
    {
        Toggle toggle = Instantiate(_togglePrefab, parent, false);
        toggle.name = name;
        RectTransform toggleRect = toggle.transform as RectTransform;
        if (toggleRect != null)
        {
            toggleRect.anchorMin = new Vector2(0f, 0.5f);
            toggleRect.anchorMax = new Vector2(0f, 0.5f);
            toggleRect.pivot = new Vector2(0.5f, 0.5f);
            toggleRect.anchoredPosition = Vector2.zero;
            toggleRect.localPosition = Vector3.zero;
            toggleRect.localRotation = Quaternion.identity;
            toggleRect.localScale = Vector3.one;
            toggleRect.sizeDelta = new Vector2(SwitchWidth, SwitchHeight);
        }

        RectTransform toggleFace = toggle.transform.Find("Image") as RectTransform;
        if (toggleFace != null)
            toggleFace.gameObject.SetActive(false);

        TextMeshProUGUI prefabLabel = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
        if (prefabLabel != null)
            prefabLabel.gameObject.SetActive(false);

        LayoutElement toggleLayout = toggle.GetComponent<LayoutElement>();
        if (toggleLayout == null)
            toggleLayout = toggle.gameObject.AddComponent<LayoutElement>();
        toggleLayout.preferredWidth = SwitchWidth;
        toggleLayout.preferredHeight = SwitchHeight;
        toggleLayout.flexibleWidth = 0f;
        toggleLayout.flexibleHeight = 0f;
        if (_settingsStyleSwitchPrefab != null)
            Instantiate(_settingsStyleSwitchPrefab, toggle.transform, false).name = "SettingsStyleSwitch";
        else
            CreateSettingsStyleSwitchVisual(toggle);
        SetSwitchVisual(toggle, false);
        return toggle;
    }

    private Slider CreatePercentSlider(
        Transform parent,
        string name,
        string label,
        out TextMeshProUGUI valueLabel)
    {
        GameObject root = new GameObject(name + "Control", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        LayoutElement rootLayout = root.GetComponent<LayoutElement>();
        VerticalLayoutGroup vertical = root.GetComponent<VerticalLayoutGroup>();
        vertical.spacing = 2f;
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;

        GameObject labelRow = CreateRow(root.transform, name + "LabelRow", 30f);
        CreateLabel(labelRow.transform, label, 22f, 28f).GetComponent<LayoutElement>().flexibleWidth = 1f;
        valueLabel = CreateLabel(labelRow.transform, "0%", 22f, 28f, 72f);
        valueLabel.alignment = TextAlignmentOptions.MidlineRight;

        Slider slider = CreateSlider(root.transform, name + "Slider", Slider.Direction.LeftToRight);
        LayoutElement sliderLayout = slider.GetComponent<LayoutElement>();
        sliderLayout.minHeight = 0f;
        sliderLayout.preferredHeight = 34f;
        sliderLayout.flexibleHeight = 0f;
        sliderLayout.flexibleWidth = 1f;
        rootLayout.preferredHeight = 30f + vertical.spacing + sliderLayout.preferredHeight;
        ApplyProgressSliderStyle(slider);
        return slider;
    }

    private static void CreateSettingsStyleSwitchVisual(Toggle toggle)
    {
        Image hitArea = toggle.GetComponent<Image>();
        if (hitArea != null)
        {
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            toggle.targetGraphic = hitArea;
        }

        GameObject trackObject = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectImage));
        trackObject.transform.SetParent(toggle.transform, false);
        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(SwitchTrackLength, SwitchTrackThickness);
        trackRect.anchoredPosition = Vector2.zero;
        trackRect.SetAsFirstSibling();

        RoundedRectImage track = trackObject.GetComponent<RoundedRectImage>();
        track.cornerRadius = SwitchTrackThickness * 0.5f;
        track.borderWidth = 1f;
        track.borderColor = SwitchBorderColor;
        track.color = SwitchOffTrackColor;
        track.raycastTarget = false;

        GameObject knobObject = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectImage));
        knobObject.transform.SetParent(toggle.transform, false);
        RectTransform knobRect = knobObject.GetComponent<RectTransform>();
        knobRect.anchorMin = new Vector2(0.5f, 0.5f);
        knobRect.anchorMax = new Vector2(0.5f, 0.5f);
        knobRect.pivot = new Vector2(0.5f, 0.5f);
        knobRect.sizeDelta = new Vector2(SwitchKnobSize, SwitchKnobSize);
        knobRect.anchoredPosition = new Vector2(-SwitchKnobTravel, 0f);

        RoundedRectImage knob = knobObject.GetComponent<RoundedRectImage>();
        knob.cornerRadius = SwitchKnobSize * 0.5f;
        knob.color = SwitchOnColor;
        knob.raycastTarget = false;
    }

    private static void SetSwitchVisual(Toggle toggle, bool enabled)
    {
        if (toggle == null)
            return;

        SettingsStyleSwitch switchVisual = toggle.GetComponentInChildren<SettingsStyleSwitch>(true);
        if (switchVisual != null)
        {
            switchVisual.SetState(enabled);
            return;
        }

        Transform visualRoot = toggle.transform.Find("SettingsStyleSwitch");
        RoundedRectImage track = (visualRoot != null ? visualRoot.Find("Track") : toggle.transform.Find("Track"))
            ?.GetComponent<RoundedRectImage>();
        if (track != null)
        {
            track.color = enabled ? SwitchOnColor : SwitchOffTrackColor;
            track.borderColor = enabled ? SwitchOnColor : SwitchBorderColor;
            track.SetVerticesDirty();
        }

        RectTransform knob = (visualRoot != null ? visualRoot.Find("Knob") : toggle.transform.Find("Knob")) as RectTransform;
        if (knob != null)
            knob.anchoredPosition = new Vector2(enabled ? SwitchKnobTravel : -SwitchKnobTravel, 0f);
    }

    private Slider CreateSlider(Transform parent, string name, Slider.Direction direction)
    {
        Slider slider = Instantiate(_sliderPrefab, parent, false);
        slider.name = name;
        RectTransform sliderRect = slider.transform as RectTransform;
        if (sliderRect != null)
        {
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = Vector2.zero;
            sliderRect.localPosition = Vector3.zero;
            sliderRect.localRotation = Quaternion.identity;
            sliderRect.localScale = Vector3.one;
        }

        slider.direction = direction;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(0f);
        return slider;
    }

    private void ApplyProgressSliderStyle(Slider slider)
    {
        if (slider == null || _progressSliderStyle == null)
            return;

        slider.transition = _progressSliderStyle.transition;
        slider.colors = _progressSliderStyle.colors;
        slider.spriteState = _progressSliderStyle.spriteState;
        slider.targetGraphic = null;

        SetHorizontalTrack(slider.transform.Find("Background") as RectTransform);
        SetHorizontalTrack(slider.transform.Find("Fill Area") as RectTransform);
        SetFullRect(slider.transform.Find("Handle Slide Area") as RectTransform);
        SetFullRect(slider.fillRect);

        CopyImageVisual(
            _progressSliderStyle.transform.Find("Background")?.GetComponent<Image>(),
            slider.transform.Find("Background")?.GetComponent<Image>());
        CopyImageVisual(
            _progressSliderStyle.fillRect != null ? _progressSliderStyle.fillRect.GetComponent<Image>() : null,
            slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null);
        ApplyProgressHandleStyle(slider);
    }

    private void ApplyProgressHandleStyle(Slider slider)
    {
        if (slider.handleRect == null || _progressSliderStyle.handleRect == null)
            return;

        CopyImageVisual(
            _progressSliderStyle.handleRect.GetComponent<Image>(),
            slider.handleRect.GetComponent<Image>());

        Image sourceDot = _progressSliderStyle.handleRect.Find("HandleDot")?.GetComponent<Image>();
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

    private static GameObject CreateRow(Transform parent, string name, float height)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        row.GetComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string text,
        float fontSize,
        float height,
        float width = -1f)
    {
        GameObject labelObject = new GameObject(text + "Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        LayoutElement element = labelObject.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        if (width > 0f)
            element.preferredWidth = width;
        return label;
    }

    private static Texture2D BuildHueTexture()
    {
        const int height = 256;
        var texture = new Texture2D(1, height, TextureFormat.RGB24, false, true)
        {
            name = "ChromaKeyHueSpectrum",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[height];
        for (int y = 0; y < height; y++)
            pixels[y] = Color.HSVToRGB(y / (height - 1f), 1f, 1f);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (_updatingUi || _playbackService == null)
            return;

        SetSwitchVisual(_enabledToggle, enabled);

        if (enabled)
        {
            if (_passthroughService == null || !_passthroughService.IsSupported)
            {
                _enabledToggle.SetIsOnWithoutNotify(false);
                SetSwitchVisual(_enabledToggle, false);
                Debug.LogWarning("[ChromaKeyPanel] Passthrough is unavailable; chromakey remains disabled.");
                return;
            }

            _previousPassthroughEnabled = _passthroughService.IsEnabled;
            if (!_passthroughService.SetEnabled(true))
            {
                _enabledToggle.SetIsOnWithoutNotify(false);
                SetSwitchVisual(_enabledToggle, false);
                Debug.LogWarning("[ChromaKeyPanel] Passthrough could not be enabled; chromakey remains disabled.");
                return;
            }

            _hasPassthroughSnapshot = true;
            _playbackService.SetChromaKeyEnabled(true);
            if (_seeThroughButton != null)
                _seeThroughButton.interactable = false;
            return;
        }

        _playbackService.SetChromaKeyEnabled(false);
        RestorePassthrough();
    }

    private void OnPickerColorChanged(Color color)
    {
        if (_updatingUi)
            return;
        ApplyKeyColor(color, true);
    }

    private void OnHueChanged(float hue)
    {
        if (_updatingUi || _colorPicker == null)
            return;
        _colorPicker.SetHue(hue, true);
    }

    private void OnHexCommitted(string value)
    {
        if (_updatingUi || _playbackService == null)
            return;

        if (!ChromaKeySettings.TryParseHex(NormalizeHex(value), out ChromaKeySettings parsed))
        {
            _hexInput.SetTextWithoutNotify(ToRgbText(_playbackService.CurrentChromaKeySettings.KeyColor));
            return;
        }
        ApplyKeyColor(parsed.KeyColor, true);
    }

    private void OnRangeChanged(float value)
    {
        if (_rangeValue != null)
            _rangeValue.text = FormatPercent(value);
        if (!_updatingUi)
            _playbackService?.SetChromaKeyColorRange(ToChromaKeyParameter(value));
    }

    private void OnFalloffChanged(float value)
    {
        if (_falloffValue != null)
            _falloffValue.text = FormatPercent(value);
        if (!_updatingUi)
            _playbackService?.SetChromaKeyFalloff(ToChromaKeyParameter(value));
    }

    private void OnExtractColorClicked()
    {
        if (_colorExtractionPending || _playbackService == null)
            return;

        _colorExtractionPending = true;
        if (_extractColorButton != null)
            _extractColorButton.interactable = false;
        _playbackService.RequestChromaKeyColorExtraction();
    }

    private void OnEdgeSmoothChanged(bool enabled)
    {
        SetSwitchVisual(_edgeSmoothToggle, enabled);
        if (!_updatingUi)
            _playbackService?.SetChromaKeyEdgeSmoothEnabled(enabled);
    }

    private void OnClipBlackChanged(bool enabled)
    {
        SetSwitchVisual(_clipBlackToggle, enabled);
        if (!_updatingUi)
            _playbackService?.SetChromaKeyClipBlackEnabled(enabled);
    }

    private void OnClipWhiteChanged(bool enabled)
    {
        SetSwitchVisual(_clipWhiteToggle, enabled);
        if (!_updatingUi)
            _playbackService?.SetChromaKeyClipWhiteEnabled(enabled);
    }

    private void OnDespillChanged(bool enabled)
    {
        SetSwitchVisual(_despillToggle, enabled);
        if (!_updatingUi)
            _playbackService?.SetChromaKeyDespillEnabled(enabled);
    }

    private void ApplyKeyColor(Color color, bool notifyPlayback)
    {
        color.a = 1f;
        _updatingUi = true;
        Color.RGBToHSV(color, out float hue, out _, out _);
        _hueSlider?.SetValueWithoutNotify(hue);
        _colorPicker?.SetColorWithoutNotify(color);
        if (_colorPreview != null)
            _colorPreview.color = color;
        _hexInput?.SetTextWithoutNotify(ToRgbText(color));
        _updatingUi = false;
        if (notifyPlayback)
            _playbackService?.SetChromaKeyColor(color);
    }

    private void ApplySettings(ChromaKeySettings settings)
    {
        _updatingUi = true;
        _enabledToggle?.SetIsOnWithoutNotify(settings.Enabled);
        SetSwitchVisual(_enabledToggle, settings.Enabled);
        Color.RGBToHSV(settings.KeyColor, out float hue, out _, out _);
        _hueSlider?.SetValueWithoutNotify(hue);
        _colorPicker?.SetColorWithoutNotify(settings.KeyColor);
        _hexInput?.SetTextWithoutNotify(ToRgbText(settings.KeyColor));
        if (_colorPreview != null)
            _colorPreview.color = settings.KeyColor;
        float rangeUiValue = ToUiPercentValue(settings.ColorRange);
        float falloffUiValue = ToUiPercentValue(settings.Falloff);
        _rangeSlider?.SetValueWithoutNotify(rangeUiValue);
        _falloffSlider?.SetValueWithoutNotify(falloffUiValue);
        SetToggleWithoutNotify(_edgeSmoothToggle, settings.EdgeSmoothEnabled);
        SetToggleWithoutNotify(_clipBlackToggle, settings.ClipBlackEnabled);
        SetToggleWithoutNotify(_clipWhiteToggle, settings.ClipWhiteEnabled);
        SetToggleWithoutNotify(_despillToggle, settings.DespillEnabled);
        if (_rangeValue != null)
            _rangeValue.text = FormatPercent(rangeUiValue);
        if (_falloffValue != null)
            _falloffValue.text = FormatPercent(falloffUiValue);
        _updatingUi = false;

        if (_seeThroughButton != null)
            _seeThroughButton.interactable = !settings.Enabled && (_passthroughService == null || _passthroughService.IsSupported);
    }

    private void HandleSettingsChanged(ChromaKeySettings settings)
    {
        ApplySettings(settings);
    }

    private void HandleColorExtractionCompleted(bool success)
    {
        _colorExtractionPending = false;
        if (_extractColorButton != null)
            _extractColorButton.interactable = true;
    }

    private void HandleMediaChanged(MediaWrapper media, int index)
    {
        RestorePassthrough();
        HandleColorExtractionCompleted(false);
        ApplySettings(ChromaKeySettings.Default);
    }

    private void RestorePassthrough()
    {
        if (_hasPassthroughSnapshot && _passthroughService != null)
            _passthroughService.SetEnabled(_previousPassthroughEnabled);
        _hasPassthroughSnapshot = false;
        if (_seeThroughButton != null)
            _seeThroughButton.interactable = _passthroughService == null || _passthroughService.IsSupported;
    }

    private void Subscribe()
    {
        if (_playbackService == null)
            return;
        _playbackService.OnChromaKeySettingsChanged += HandleSettingsChanged;
        _playbackService.OnChromaKeyColorExtractionCompleted += HandleColorExtractionCompleted;
        _playbackService.OnMediaChanged += HandleMediaChanged;
    }

    private void Unsubscribe()
    {
        if (_playbackService == null)
            return;
        _playbackService.OnChromaKeySettingsChanged -= HandleSettingsChanged;
        _playbackService.OnChromaKeyColorExtractionCompleted -= HandleColorExtractionCompleted;
        _playbackService.OnMediaChanged -= HandleMediaChanged;
    }

    private static void SetToggleWithoutNotify(Toggle toggle, bool enabled)
    {
        toggle?.SetIsOnWithoutNotify(enabled);
        SetSwitchVisual(toggle, enabled);
    }

    private static string NormalizeHex(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.StartsWith("#", StringComparison.Ordinal) ? normalized : "#" + normalized;
    }

    private static string ToHex(Color color)
    {
        Color32 value = color;
        return $"#{value.r:X2}{value.g:X2}{value.b:X2}";
    }

    private static string ToRgbText(Color color)
    {
        return ToHex(color).TrimStart('#');
    }

    private static string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }

    private static float ToChromaKeyParameter(float uiValue)
    {
        return Mathf.Clamp01(uiValue) * ChromaKeyParameterMax;
    }

    private static float ToUiPercentValue(float parameterValue)
    {
        return Mathf.Clamp01(parameterValue / ChromaKeyParameterMax);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        RestorePassthrough();
        if (_hueTexture != null)
            Destroy(_hueTexture);
    }
}
