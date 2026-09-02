using System.Globalization;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRVLC;
using XRVLC.Localization;
using XRVLC.Media;
using XRVLC.Services.Settings;
using XRVLC.Services.Shortcuts;
using XRVLC.XR;

public class SettingsMenuController : MonoBehaviour
{
    public enum SettingsTab
    {
        Playback,
        Gesture,
        Subtitle,
        Video,
        Audio
    }

    private static readonly Color PanelColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static readonly Color DropdownPanelColor = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color DropdownBorderColor = new Color(1f, 1f, 1f, 0.28f);
    private static readonly Color TransparentButtonColor = new Color(1f, 1f, 1f, 0f);
    private static readonly Color SelectedButtonColor = new Color(1f, 1f, 1f, 0.24f);
    private static readonly Color HoverButtonColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color SelectedButtonHoverColor = new Color(1f, 1f, 1f, 0.32f);
    private static readonly Color TextColor = Color.white;
    private const string IconResourcePath = "UI/IconPark/";
    private const string SettingsStyleSwitchResourcePath = "UI/SettingsStyleSwitch";
    private const string GestureInfoIconName = "info";
    private const string StepperControlResourcePath = "UI/XrStepperControl";
    private const int SubtitleOpacityMinimum = 50;
    private const int SubtitleOpacityMaximum = 255;
    private const float SettingsControlLabelWidth = 180f;
    private const float SettingsRowSpacing = 8f;
    private const float SubtitleOpacitySliderWidth = 220f;
    private const float SubtitleOpacityValueWidth = 64f;
    private const float SettingsControlColumnWidth = SubtitleOpacitySliderWidth + SettingsRowSpacing + SubtitleOpacityValueWidth;
    private const float GestureSectionTitleMinWidth = 112f;
    private const float GestureInfoIconSize = 28f;
    private const float GestureInfoIconGap = 6f;
    private const float SwitchWidth = 76f;
    private const float SwitchHeight = 38f;
    private static readonly VideoAspectRatio[] VideoAspectRatioOptions =
    {
        VideoAspectRatio.Source,
        VideoAspectRatio.Ratio16x9,
        VideoAspectRatio.Ratio4x3,
        VideoAspectRatio.Ratio16x10,
        VideoAspectRatio.Ratio221x1,
        VideoAspectRatio.Ratio235x1,
        VideoAspectRatio.Ratio239x1,
        VideoAspectRatio.Ratio5x4
    };
    private static readonly string[] VideoAspectRatioLabels =
    {
        XrUiTextKey.SettingsAspectRatioAuto,
        "16:9",
        "4:3",
        "16:10",
        "2.21:1",
        "2.35:1",
        "2.39:1",
        "5:4"
    };
    private static readonly string[] SubtitleFontSizeValues =
    {
        "40",
        "32",
        "25",
        "19",
        "16",
        "13",
        "10"
    };
    private static readonly string[] SubtitleFontSizeLabels =
    {
        XrUiTextKey.SettingsSubtitleFontNano,
        XrUiTextKey.SettingsSubtitleFontMicro,
        XrUiTextKey.SettingsSubtitleFontSmallest,
        XrUiTextKey.SettingsSubtitleFontSmall,
        XrUiTextKey.SettingsSubtitleFontNormal,
        XrUiTextKey.SettingsSubtitleFontBig,
        XrUiTextKey.SettingsSubtitleFontHuge
    };

    [Header("按钮主题")]
    public XrButtonTheme buttonTheme;

    [Header("Control Prefabs")]
    public XrStepperControl stepperControlPrefab;

    [Header("Dropdown Prefabs")]
    public XrDropdown shortcutDropdownPrefab;

    [Header("Layout")]
    public float menuWidth = 720f;
    public float menuHeight = 420f;
    public float menuFontSize = 22f;
    public float dropdownFontSize = 20f;
    public float shortcutDropdownWidth = 220f;
    public float shortcutDropdownHeight = 48f;
    public float dropdownCornerRadius = 8f;
    public float dropdownBorderWidth = 1f;
    public float contentLeftPadding = 110f;

    private readonly Dictionary<SettingsTab, Button> _tabButtons = new Dictionary<SettingsTab, Button>();
    private readonly Dictionary<SettingsTab, GameObject> _contentRoots = new Dictionary<SettingsTab, GameObject>();
    private readonly List<string> _actionKeys = new List<string>
    {
        ShortcutActions.None,
        ShortcutActions.Toggle2xSpeed,
        ShortcutActions.ToggleSubtitle,
        ShortcutActions.TogglePassthroughBackground
    };

    private XRVLC.Media.PlaybackService _playbackService;
    private ShortcutManager _shortcutManager;
    private Slider _sliderPrefab;
    private Slider _progressSliderStyle;
    private bool _built;
    private SettingsTab _currentTab = SettingsTab.Playback;

    private XrDropdown _buttonYDropdown;
    private XrDropdown _buttonBDropdown;
    private Button _spatialSubtitleSwitchButton;
    private Button _outsideSubtitleSwitchButton;
    private Button _audioBoostSwitchButton;
    private XrStepperControl _subtitleDelayStepper;
    private XrStepperControl _audioDelayStepper;
    private XrDropdown _subtitleFontDropdown;
    private Slider _subtitleOpacitySlider;
    private TextMeshProUGUI _subtitleOpacityValueText;
    private XrStepperControl _playbackRateStepper;
    private XrStepperControl _seekSecondsStepper;
    private XrDropdown _videoAspectRatioDropdown;
    private VideoAspectRatio _currentVideoAspectRatio;

    public void Bind(
        XRVLC.Media.PlaybackService playbackService,
        ShortcutManager shortcutManager = null,
        Slider sliderPrefab = null,
        Slider progressSliderStyle = null)
    {
        _playbackService = playbackService;
        _shortcutManager = shortcutManager;
        _sliderPrefab = sliderPrefab;
        _progressSliderStyle = progressSliderStyle;
        BuildIfNeeded();
        XrProgressSliderStyle.Apply(_subtitleOpacitySlider, _progressSliderStyle);
        RefreshAllSelections();
    }

    public void Show(SettingsTab initialTab = SettingsTab.Playback)
    {
        BuildIfNeeded();
        RefreshAllSelections();
        ShowTab(initialTab);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public bool IsOpen => gameObject.activeSelf;

    public bool Contains(GameObject target)
    {
        return target != null && (target == gameObject || target.transform.IsChildOf(transform));
    }

    private void OnEnable()
    {
        VlcPlaybackEvents.OnPlaybackRateChanged += OnPlaybackRateChanged;
        UpdatePlaybackRateControl();
    }

    private void OnDisable()
    {
        VlcPlaybackEvents.OnPlaybackRateChanged -= OnPlaybackRateChanged;
    }

    private void BuildIfNeeded()
    {
        if (_built)
            return;

        _tabButtons.Clear();
        _contentRoots.Clear();
        ClearChildren(transform);

        try
        {
            ConfigureRoot();
            Transform tabBar = CreateTabBar(transform);
            CreateTabButton(tabBar, SettingsTab.Playback, XrUiText.Get(XrUiTextKey.SettingsTabPlayback));
            CreateTabButton(tabBar, SettingsTab.Gesture, XrUiText.Get(XrUiTextKey.SettingsTabGesture));
            CreateTabButton(tabBar, SettingsTab.Subtitle, XrUiText.Get(XrUiTextKey.SettingsTabSubtitle));
            CreateTabButton(tabBar, SettingsTab.Video, XrUiText.Get(XrUiTextKey.SettingsTabVideo));
            CreateTabButton(tabBar, SettingsTab.Audio, XrUiText.Get(XrUiTextKey.SettingsTabAudio));

            Transform contentRoot = CreateContentFrame(transform);
            BuildPlaybackTab(CreateContentRoot(contentRoot, SettingsTab.Playback));
            BuildGestureTab(CreateContentRoot(contentRoot, SettingsTab.Gesture));
            BuildSubtitleTab(CreateContentRoot(contentRoot, SettingsTab.Subtitle));
            BuildVideoTab(CreateContentRoot(contentRoot, SettingsTab.Video));
            BuildAudioTab(CreateContentRoot(contentRoot, SettingsTab.Audio));

            _built = true;
            ShowTab(_currentTab);
        }
        catch
        {
            _built = false;
            _tabButtons.Clear();
            _contentRoots.Clear();
            ClearChildren(transform);
            throw;
        }
    }

    private void ConfigureRoot()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(menuWidth, menuHeight);
            rect.pivot = Vector2.zero;
        }

        Image image = GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = true;

        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private Transform CreateTabBar(Transform parent)
    {
        var bar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        bar.transform.SetParent(parent, false);

        var layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var element = bar.GetComponent<LayoutElement>();
        element.preferredHeight = 48f;
        return bar.transform;
    }

    private Transform CreateContentFrame(Transform parent)
    {
        var frame = new GameObject("ContentFrame", typeof(RectTransform), typeof(LayoutElement));
        frame.transform.SetParent(parent, false);

        var element = frame.GetComponent<LayoutElement>();
        element.preferredHeight = Mathf.Max(0f, menuHeight - 76f);
        element.flexibleHeight = 1f;
        return frame.transform;
    }

    private GameObject CreateContentRoot(Transform parent, SettingsTab tab)
    {
        var root = new GameObject(tab + "Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _contentRoots[tab] = root;
        return root;
    }

    private void CreateTabButton(Transform parent, SettingsTab tab, string label)
    {
        Button button = CreateButton(parent, label, menuFontSize, () => ShowTab(tab), 128f, 44f);
        _tabButtons[tab] = button;
    }

    private void BuildPlaybackTab(GameObject root)
    {
        CreatePlaybackRateStepper(root.transform);
        _seekSecondsStepper = CreateStepperRow(
            root.transform,
            "ShortcutSeekSecondsRow",
            XrUiText.Get(XrUiTextKey.SettingsSeekStep),
            ShortcutSettingsService.DefaultSeekSeconds.ToString(CultureInfo.InvariantCulture),
            () => StepSeekSeconds(-1),
            () => StepSeekSeconds(1),
            ApplySeekSecondsFromInput);
    }

    private void BuildGestureTab(GameObject root)
    {
        CreateSectionLabel(root.transform, XrUiText.Get(XrUiTextKey.SettingsSectionShortcuts), true);
        _buttonYDropdown = CreateShortcutDropdown(root.transform, XrUiText.Get(XrUiTextKey.SettingsButtonY));
        _buttonBDropdown = CreateShortcutDropdown(root.transform, XrUiText.Get(XrUiTextKey.SettingsButtonB));
        LoadGestureValues();
    }

    private void BuildSubtitleTab(GameObject root)
    {
        _subtitleFontDropdown = CreateSubtitleFontDropdown(root.transform);
        _subtitleOpacitySlider = CreateSubtitleOpacitySlider(root.transform);
        _spatialSubtitleSwitchButton = CreateSwitchRow(
            root.transform,
            "SpatialSubtitleSwitchRow",
            XrUiText.Get(XrUiTextKey.SettingsSpatialSubtitles),
            ToggleSpatialSubtitles,
            SettingsControlLabelWidth);
        _outsideSubtitleSwitchButton = CreateSwitchRow(
            root.transform,
            "SubtitleOutsideSwitchRow",
            XrUiText.Get(XrUiTextKey.SettingsSubtitlesOutside),
            ToggleRenderSubtitlesOutsideScreen,
            SettingsControlLabelWidth);
        _subtitleDelayStepper = CreateStepperRow(
            root.transform,
            "SubtitleDelayRow",
            XrUiText.Get(XrUiTextKey.SettingsSubtitleDelay),
            "0.0",
            () => StepSubtitleDelay(-0.5f),
            () => StepSubtitleDelay(0.5f),
            ApplySubtitleDelayFromInput);
    }

    private void CreatePlaybackRateStepper(Transform parent)
    {
        _playbackRateStepper = CreateStepperRow(
            parent,
            "PlaybackRateStepperRow",
            XrUiText.Get(XrUiTextKey.SettingsPlaybackRate),
            "1.00",
            () => StepPlaybackRate(-0.25f),
            () => StepPlaybackRate(0.25f),
            ApplyPlaybackRateFromInput);
    }

    private void BuildVideoTab(GameObject root)
    {
        _videoAspectRatioDropdown = CreateVideoAspectRatioDropdown(root.transform);
    }

    private void BuildAudioTab(GameObject root)
    {
        _audioBoostSwitchButton = CreateSwitchRow(root.transform, "AudioBoostSwitchRow", XrUiText.Get(XrUiTextKey.SettingsAudioBoost), ToggleAudioBoost);
        _audioDelayStepper = CreateStepperRow(
            root.transform,
            "AudioDelayRow",
            XrUiText.Get(XrUiTextKey.SettingsAudioDelay),
            "0.0",
            () => StepAudioDelay(-0.5f),
            () => StepAudioDelay(0.5f),
            ApplyAudioDelayFromInput);
    }

    private XrDropdown CreateSubtitleFontDropdown(Transform parent)
    {
        Transform row = CreateRow(parent, "SubtitleFontRow");
        CreateSettingLabel(row, XrUiText.Get(XrUiTextKey.SettingsSubtitleFont), shortcutDropdownHeight);
        Transform valueColumn = CreateValueColumn(row, "SubtitleFontValueColumn", shortcutDropdownHeight);

        if (shortcutDropdownPrefab == null)
        {
            Debug.LogError("[SettingsMenuController] shortcutDropdownPrefab is not assigned. Subtitle font settings require the shared XrDropdown prefab.");
            return null;
        }

        XrDropdown dropdown = Instantiate(shortcutDropdownPrefab, valueColumn, false);
        dropdown.gameObject.name = "SubtitleFontDropdown";
        dropdown.showCaption = true;
        dropdown.width = shortcutDropdownWidth;
        dropdown.fontSize = dropdownFontSize;
        dropdown.horizontalPadding = 16f;
        dropdown.verticalPadding = 12f;
        dropdown.minRowHeight = shortcutDropdownHeight;
        dropdown.secondaryTextWidth = 0f;
        dropdown.maxVisibleItems = SubtitleFontSizeValues.Length;
        dropdown.panelColor = DropdownPanelColor;
        ConfigureShortcutDropdownVisual(dropdown);
        dropdown.ApplyConfiguredLayout();

        var element = dropdown.GetComponent<LayoutElement>();
        if (element == null)
            element = dropdown.gameObject.AddComponent<LayoutElement>();
        element.minWidth = shortcutDropdownWidth;
        element.minHeight = shortcutDropdownHeight;
        element.preferredWidth = shortcutDropdownWidth;
        element.preferredHeight = shortcutDropdownHeight;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        dropdown.SetItems(CreateSubtitleFontItems(), GetSubtitleFontSizeIndex(VlcPlaybackBridge.GetSubtitleFontSize()));
        dropdown.onBeforeShow.AddListener(() => CloseOtherDropdowns(dropdown));
        dropdown.onValueChanged.AddListener(ApplySubtitleFontFromDropdown);
        dropdown.ApplyConfiguredLayout();
        return dropdown;
    }

    private Slider CreateSubtitleOpacitySlider(Transform parent)
    {
        Transform row = CreateRow(parent, "SubtitleOpacityRow");
        CreateSettingLabel(row, XrUiText.Get(XrUiTextKey.SettingsSubtitleOpacity), 46f);
        Transform valueColumn = CreateValueColumn(row, "SubtitleOpacityValueColumn", 46f);

        if (_sliderPrefab == null)
        {
            Debug.LogError("[SettingsMenuController] slider prefab is not assigned. Subtitle opacity requires the shared slider control.");
            return null;
        }

        Slider slider = Instantiate(_sliderPrefab, valueColumn, false);
        slider.gameObject.name = "SubtitleOpacitySlider";
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

        LayoutElement sliderLayout = slider.GetComponent<LayoutElement>();
        if (sliderLayout == null)
            sliderLayout = slider.gameObject.AddComponent<LayoutElement>();
        sliderLayout.minWidth = 0f;
        sliderLayout.minHeight = 34f;
        sliderLayout.preferredWidth = SubtitleOpacitySliderWidth;
        sliderLayout.preferredHeight = 34f;
        sliderLayout.flexibleWidth = 0f;
        sliderLayout.flexibleHeight = 0f;

        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = SubtitleOpacityMinimum;
        slider.maxValue = SubtitleOpacityMaximum;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(SubtitleOpacityMaximum);
        XrProgressSliderStyle.Apply(slider, _progressSliderStyle);

        _subtitleOpacityValueText = CreateText(
            valueColumn,
            "100%",
            menuFontSize,
            TextAlignmentOptions.Center,
            SubtitleOpacityValueWidth,
            46f);
        slider.onValueChanged.AddListener(PreviewSubtitleOpacityFromSlider);
        AddSubtitleOpacityCommitOnPointerUp(slider);
        return slider;
    }

    private void AddSubtitleOpacityCommitOnPointerUp(Slider slider)
    {
        if (slider == null)
            return;

        EventTrigger trigger = slider.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = slider.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener(_ => ApplySubtitleOpacityFromSlider(slider.value));
        trigger.triggers.Add(pointerUp);
    }

    private XrDropdown CreateVideoAspectRatioDropdown(Transform parent)
    {
        Transform row = CreateRow(parent, "VideoAspectRatioRow");
        CreateSettingLabel(row, XrUiText.Get(XrUiTextKey.SettingsAspectRatio), shortcutDropdownHeight);
        Transform valueColumn = CreateValueColumn(row, "VideoAspectRatioValueColumn", shortcutDropdownHeight);

        if (shortcutDropdownPrefab == null)
        {
            Debug.LogError("[SettingsMenuController] shortcutDropdownPrefab is not assigned. Video aspect ratio settings require the shared XrDropdown prefab.");
            return null;
        }

        XrDropdown dropdown = Instantiate(shortcutDropdownPrefab, valueColumn, false);
        dropdown.gameObject.name = "VideoAspectRatioDropdown";
        dropdown.showCaption = true;
        dropdown.width = shortcutDropdownWidth;
        dropdown.fontSize = dropdownFontSize;
        dropdown.horizontalPadding = 16f;
        dropdown.verticalPadding = 12f;
        dropdown.minRowHeight = shortcutDropdownHeight;
        dropdown.secondaryTextWidth = 0f;
        dropdown.maxVisibleItems = VideoAspectRatioOptions.Length;
        dropdown.panelColor = DropdownPanelColor;
        ConfigureShortcutDropdownVisual(dropdown);
        dropdown.ApplyConfiguredLayout();

        var element = dropdown.GetComponent<LayoutElement>();
        if (element == null)
            element = dropdown.gameObject.AddComponent<LayoutElement>();
        element.minWidth = shortcutDropdownWidth;
        element.minHeight = shortcutDropdownHeight;
        element.preferredWidth = shortcutDropdownWidth;
        element.preferredHeight = shortcutDropdownHeight;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        dropdown.SetItems(CreateVideoAspectRatioItems(), GetVideoAspectRatioIndex(_currentVideoAspectRatio));
        dropdown.onBeforeShow.AddListener(() => CloseOtherDropdowns(dropdown));
        dropdown.onValueChanged.AddListener(ApplyVideoAspectRatioFromDropdown);
        dropdown.ApplyConfiguredLayout();
        return dropdown;
    }

    private Button CreateSwitchRow(
        Transform parent,
        string name,
        string label,
        UnityEngine.Events.UnityAction onClick,
        float labelWidth = SettingsControlLabelWidth)
    {
        Transform row = CreateRow(parent, name);
        CreateText(row, label, menuFontSize, TextAlignmentOptions.Center, labelWidth, 46f);
        Transform valueColumn = CreateValueColumn(row, name + "ValueColumn", 46f);
        return CreateSwitchButton(valueColumn, name + "Switch", onClick);
    }

    private Button CreateSwitchButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectImage), typeof(Button), typeof(LayoutElement));
        item.transform.SetParent(parent, false);

        var element = item.GetComponent<LayoutElement>();
        element.preferredWidth = SwitchWidth;
        element.preferredHeight = SwitchHeight;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        var hitArea = item.GetComponent<RoundedRectImage>();
        hitArea.cornerRadius = SwitchHeight * 0.5f;
        hitArea.borderWidth = 0f;
        hitArea.borderColor = Color.clear;
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        SettingsStyleSwitch switchPrefab = Resources.Load<SettingsStyleSwitch>(SettingsStyleSwitchResourcePath);
        if (switchPrefab != null)
            Instantiate(switchPrefab, item.transform, false).name = "SettingsStyleSwitch";
        else
            Debug.LogError("[SettingsMenu] SettingsStyleSwitch prefab is missing.");

        Button button = item.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hitArea;
        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    private XrStepperControl CreateStepperRow(
        Transform parent,
        string name,
        string label,
        string initialValue,
        UnityEngine.Events.UnityAction onDecrement,
        UnityEngine.Events.UnityAction onIncrement,
        UnityEngine.Events.UnityAction<string> onSubmit)
    {
        Transform row = CreateRow(parent, name);
        CreateSettingLabel(row, label, 46f);
        Transform valueColumn = CreateValueColumn(row, name + "ValueColumn", 46f);
        return CreateStepperControl(valueColumn, label + "Stepper", initialValue, onDecrement, onIncrement, onSubmit);
    }

    private XrStepperControl CreateStepperControl(
        Transform parent,
        string name,
        string initialValue,
        UnityEngine.Events.UnityAction onDecrement,
        UnityEngine.Events.UnityAction onIncrement,
        UnityEngine.Events.UnityAction<string> onSubmit)
    {
        XrStepperControl prefab = stepperControlPrefab != null
            ? stepperControlPrefab
            : Resources.Load<XrStepperControl>(StepperControlResourcePath);

        XrStepperControl control = prefab != null
            ? Instantiate(prefab, parent, false)
            : new GameObject(name, typeof(RectTransform), typeof(XrStepperControl)).GetComponent<XrStepperControl>();

        control.transform.SetParent(parent, false);
        control.gameObject.name = name;
        control.Configure(initialValue, menuFontSize, onDecrement, onIncrement, onSubmit);
        ApplyStepperControlTheme(control);
        return control;
    }

    private void ApplyStepperControlTheme(XrStepperControl control)
    {
        if (control == null)
            return;

        control.ApplyStyle(menuFontSize, TextColor, new Color(1f, 1f, 1f, 0.12f));
        ApplyButtonTheme(control.decrementButton, control.decrementButton != null ? control.decrementButton.GetComponent<Image>() : null, false);
        ApplyButtonTheme(control.incrementButton, control.incrementButton != null ? control.incrementButton.GetComponent<Image>() : null, false);
    }

    private XrDropdown CreateShortcutDropdown(Transform parent, string label)
    {
        Transform row = CreateGestureRow(parent, label + "Row");
        CreateSettingLabel(row, label, shortcutDropdownHeight);
        Transform valueColumn = CreateValueColumn(row, label + "ValueColumn", shortcutDropdownHeight);

        if (shortcutDropdownPrefab == null)
        {
            Debug.LogError("[SettingsMenuController] shortcutDropdownPrefab is not assigned. Gesture shortcuts require the shared XrDropdown prefab.");
            return null;
        }

        XrDropdown dropdown = Instantiate(shortcutDropdownPrefab, valueColumn, false);

        dropdown.gameObject.name = label + "Dropdown";
        dropdown.showCaption = true;
        dropdown.width = shortcutDropdownWidth;
        dropdown.fontSize = dropdownFontSize;
        dropdown.horizontalPadding = 16f;
        dropdown.verticalPadding = 12f;
        dropdown.minRowHeight = shortcutDropdownHeight;
        dropdown.secondaryTextWidth = 0f;
        dropdown.maxVisibleItems = 10;
        dropdown.panelColor = DropdownPanelColor;
        ConfigureShortcutDropdownVisual(dropdown);
        dropdown.ApplyConfiguredLayout();

        var element = dropdown.GetComponent<LayoutElement>();
        if (element == null)
            element = dropdown.gameObject.AddComponent<LayoutElement>();
        element.minWidth = shortcutDropdownWidth;
        element.minHeight = shortcutDropdownHeight;
        element.preferredWidth = shortcutDropdownWidth;
        element.preferredHeight = shortcutDropdownHeight;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        dropdown.SetItems(new List<XrDropdownItemData>
        {
            new XrDropdownItemData(XrUiText.ForShortcutAction(ShortcutActions.None)),
            new XrDropdownItemData(XrUiText.ForShortcutAction(ShortcutActions.Toggle2xSpeed)),
            new XrDropdownItemData(XrUiText.ForShortcutAction(ShortcutActions.ToggleSubtitle)),
            new XrDropdownItemData(XrUiText.ForShortcutAction(ShortcutActions.TogglePassthroughBackground))
        });
        dropdown.onBeforeShow.AddListener(() => CloseOtherShortcutDropdowns(dropdown));
        dropdown.onValueChanged.AddListener(_ => SaveGestureMappings());
        dropdown.ApplyConfiguredLayout();
        return dropdown;
    }

    private void CloseOtherShortcutDropdowns(XrDropdown keepOpen)
    {
        CloseOtherDropdowns(keepOpen);
    }

    private void CloseOtherDropdowns(XrDropdown keepOpen)
    {
        Debug.Log(
            $"[SettingsMenuController] CloseOtherDropdowns entry keepOpen={DescribeDropdownNameForLog(keepOpen)} " +
            $"{DescribeDropdownStatesForLog()}");
        CloseShortcutDropdownIfNot(_buttonYDropdown, keepOpen);
        CloseShortcutDropdownIfNot(_buttonBDropdown, keepOpen);
        CloseShortcutDropdownIfNot(_subtitleFontDropdown, keepOpen);
        CloseShortcutDropdownIfNot(_videoAspectRatioDropdown, keepOpen);
        Debug.Log(
            $"[SettingsMenuController] CloseOtherDropdowns exit keepOpen={DescribeDropdownNameForLog(keepOpen)} " +
            $"{DescribeDropdownStatesForLog()}");
    }

    private static void CloseShortcutDropdownIfNot(XrDropdown dropdown, XrDropdown keepOpen)
    {
        if (dropdown == null)
            return;

        if (dropdown == keepOpen)
        {
            Debug.Log($"[SettingsMenuController] CloseDropdown skip reason=keepOpen {dropdown.DescribeRenderStateForLog()}");
            return;
        }

        if (!dropdown.IsOpen)
        {
            Debug.Log($"[SettingsMenuController] CloseDropdown skip reason=alreadyClosed {dropdown.DescribeRenderStateForLog()}");
            return;
        }

        Debug.Log($"[SettingsMenuController] CloseDropdown closing {dropdown.DescribeRenderStateForLog()}");
        dropdown.CloseImmediately();
        Debug.Log($"[SettingsMenuController] CloseDropdown closed {dropdown.DescribeRenderStateForLog()}");
    }

    private string DescribeDropdownStatesForLog()
    {
        return
            $"buttonY=[{DescribeDropdownForLog(_buttonYDropdown)}] " +
            $"buttonB=[{DescribeDropdownForLog(_buttonBDropdown)}] " +
            $"subtitleFont=[{DescribeDropdownForLog(_subtitleFontDropdown)}] " +
            $"aspect=[{DescribeDropdownForLog(_videoAspectRatioDropdown)}]";
    }

    private static string DescribeDropdownForLog(XrDropdown dropdown)
    {
        return dropdown != null ? dropdown.DescribeRenderStateForLog() : "null";
    }

    private static string DescribeDropdownNameForLog(XrDropdown dropdown)
    {
        return dropdown != null ? dropdown.name : "null";
    }

    private void ConfigureShortcutDropdownVisual(XrDropdown dropdown)
    {
        if (dropdown == null || dropdown.captionButton == null)
            return;

        ConfigureRoundedDropdownGraphic(dropdown.gameObject, false);
        if (dropdown.captionText != null)
        {
            dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText.enableWordWrapping = false;
            dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
            dropdown.captionText.raycastTarget = false;
        }

        Image captionImage = dropdown.captionButton.GetComponent<Image>();
        if (captionImage != null)
        {
            captionImage.enabled = true;
            captionImage.color = Color.clear;
            captionImage.raycastTarget = true;
            dropdown.captionButton.targetGraphic = captionImage;
        }

        dropdown.captionButton.transition = Selectable.Transition.None;
        ColorBlock colors = dropdown.captionButton.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = Color.clear;
        colors.selectedColor = Color.clear;
        colors.pressedColor = Color.clear;
        colors.disabledColor = Color.clear;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        dropdown.captionButton.colors = colors;

        // Keep the viewport Image intact: Mask uses its graphic to define the stencil area.
    }

    private RoundedRectImage ConfigureRoundedDropdownGraphic(GameObject target, bool raycastTarget)
    {
        if (target == null)
            return null;

        RoundedRectImage rounded = target.GetComponent<RoundedRectImage>();
        Image legacyImage = target.GetComponent<Image>();
        if (rounded == null && legacyImage != null)
        {
            DestroyImmediate(legacyImage);
            legacyImage = null;
        }

        if (rounded == null)
            rounded = target.AddComponent<RoundedRectImage>();
        if (rounded == null)
            return null;

        rounded.color = DropdownPanelColor;
        rounded.cornerRadius = dropdownCornerRadius;
        rounded.borderWidth = dropdownBorderWidth;
        rounded.borderColor = DropdownBorderColor;
        rounded.raycastTarget = raycastTarget;

        legacyImage = target.GetComponent<Image>();
        if (legacyImage != null)
        {
            legacyImage.enabled = false;
            legacyImage.raycastTarget = false;
        }

        return rounded;
    }

    private Transform CreateGestureRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = SettingsRowSpacing;
        int horizontalPadding = Mathf.RoundToInt(contentLeftPadding);
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var element = row.GetComponent<LayoutElement>();
        element.preferredHeight = 52f;
        return row.transform;
    }

    private Transform CreateRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = SettingsRowSpacing;
        int horizontalPadding = Mathf.RoundToInt(contentLeftPadding);
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var element = row.GetComponent<LayoutElement>();
        element.preferredHeight = 52f;
        return row.transform;
    }

    private TextMeshProUGUI CreateSettingLabel(Transform parent, string label, float height)
    {
        return CreateText(parent, label, menuFontSize, TextAlignmentOptions.Center, SettingsControlLabelWidth, height);
    }

    private Transform CreateValueColumn(Transform parent, string name, float height)
    {
        var column = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        column.transform.SetParent(parent, false);

        var layout = column.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = SettingsRowSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var element = column.GetComponent<LayoutElement>();
        element.minWidth = SettingsControlColumnWidth;
        element.preferredWidth = SettingsControlColumnWidth;
        element.preferredHeight = height;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;
        return column.transform;
    }

    private Transform CreateSectionLabelColumn(Transform parent, string name, float height)
    {
        var column = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        column.transform.SetParent(parent, false);

        var layout = column.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var element = column.GetComponent<LayoutElement>();
        element.minWidth = SettingsControlLabelWidth;
        element.preferredWidth = SettingsControlLabelWidth;
        element.preferredHeight = height;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;
        return column.transform;
    }

    private void CreateSpacer(Transform parent, float width, float height)
    {
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);

        var element = spacer.GetComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;
    }

    private void CreateSectionLabel(Transform parent, string label, bool showGestureInfo = false)
    {
        Transform row = CreateSectionLabelRow(parent, label + "TitleRow");
        Transform labelColumn = CreateSectionLabelColumn(row, label + "LabelColumn", 36f);

        if (showGestureInfo)
        {
            TextMeshProUGUI title = CreateText(labelColumn, label, menuFontSize, TextAlignmentOptions.Center, GestureSectionTitleMinWidth, 36f);
            FitTextLayoutWidth(
                title,
                label,
                GestureSectionTitleMinWidth,
                Mathf.Max(GestureSectionTitleMinWidth, SettingsControlLabelWidth - GestureInfoIconGap - GestureInfoIconSize));
            CreateSpacer(labelColumn, GestureInfoIconGap, 36f);
            CreateGestureInfoButton(labelColumn);
        }
        else
        {
            CreateText(labelColumn, label, menuFontSize, TextAlignmentOptions.Center, SettingsControlLabelWidth, 36f);
        }

        CreateValueColumn(row, label + "ValueColumn", 36f);
    }

    private void CreateGestureInfoButton(Transform parent)
    {
        var item = new GameObject("GestureInfoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(XrOverflowTooltip));
        item.transform.SetParent(parent, false);

        var element = item.GetComponent<LayoutElement>();
        element.preferredWidth = GestureInfoIconSize;
        element.preferredHeight = GestureInfoIconSize;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        Image image = item.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>(IconResourcePath + GestureInfoIconName);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;
        if (image.sprite == null)
            Debug.LogWarning($"[SettingsMenuController] IconPark 图标未找到: {GestureInfoIconName}");

        Button button = item.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.78f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(1f, 1f, 1f, 0.64f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.32f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        XrOverflowTooltip tooltip = item.GetComponent<XrOverflowTooltip>();
        tooltip.SetOverlayRoot(transform);
        tooltip.alignTopLeftToSource = true;
        tooltip.onBeforeShow.AddListener(() => CloseOtherDropdowns(null));
        tooltip.SetSource(null, XrUiText.Get(XrUiTextKey.SettingsGestureInfoTooltip));
    }

    private Transform CreateSectionLabelRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = SettingsRowSpacing;
        int horizontalPadding = Mathf.RoundToInt(contentLeftPadding);
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var element = row.GetComponent<LayoutElement>();
        element.preferredHeight = 36f;
        return row.transform;
    }

    private Button CreateButton(Transform parent, string label, float fontSize, UnityEngine.Events.UnityAction onClick, float width, float height)
    {
        var item = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        item.transform.SetParent(parent, false);

        var element = item.GetComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;

        Image image = item.GetComponent<Image>();
        image.color = Color.white;

        TextMeshProUGUI text = CreateText(item.transform, label, fontSize, TextAlignmentOptions.Center, width, height);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        Button button = item.GetComponent<Button>();
        button.targetGraphic = image;
        ApplyButtonTheme(button, image, false);
        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    private TextMeshProUGUI CreateText(Transform parent, string label, float fontSize, TextAlignmentOptions alignment, float width, float height)
    {
        var textObject = new GameObject(label + "Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        var element = textObject.GetComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = fontSize;
        text.color = TextColor;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        return text;
    }

    private static void FitTextLayoutWidth(TextMeshProUGUI text, string value, float minWidth, float maxWidth)
    {
        if (text == null)
            return;

        text.ForceMeshUpdate();
        float preferredWidth = text.GetPreferredValues(value ?? string.Empty, Mathf.Infinity, text.rectTransform.rect.height).x;
        float width = Mathf.Clamp(Mathf.Ceil(preferredWidth), minWidth, Mathf.Max(minWidth, maxWidth));

        RectTransform rect = text.GetComponent<RectTransform>();
        if (rect != null)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        LayoutElement element = text.GetComponent<LayoutElement>();
        if (element == null)
            element = text.gameObject.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
    }

    private void ShowTab(SettingsTab tab)
    {
        _currentTab = tab;

        foreach (KeyValuePair<SettingsTab, GameObject> pair in _contentRoots)
            pair.Value.SetActive(pair.Key == tab);

        foreach (KeyValuePair<SettingsTab, Button> pair in _tabButtons)
            SetButtonSelected(pair.Value, pair.Key == tab);

        if (tab == SettingsTab.Gesture)
            LoadGestureValues();
        if (tab == SettingsTab.Subtitle)
            UpdateSubtitleControls();
        if (tab == SettingsTab.Playback)
        {
            UpdatePlaybackRateControl();
            UpdateSeekSecondsControl();
        }
        if (tab == SettingsTab.Audio)
        {
            _playbackService?.RefreshAudioDelayFromVlc();
            UpdateAudioBoostSwitch();
            UpdateAudioDelayControl();
        }
    }

    private void RefreshAllSelections()
    {
        _currentVideoAspectRatio = _playbackService != null
            ? _playbackService.CurrentVideoAspectRatio
            : PlaybackUiSettingsService.LoadVideoAspectRatio();
        UpdateSubtitleControls();
        UpdatePlaybackRateControl();
        UpdateSeekSecondsControl();
        UpdateVideoLayoutSelection();
        UpdateAudioBoostSwitch();
        _playbackService?.RefreshAudioDelayFromVlc();
        UpdateAudioDelayControl();
        LoadGestureValues();
    }

    private void LoadGestureValues()
    {
        if (_buttonYDropdown == null && _buttonBDropdown == null)
            return;

        ShortcutConfigData mappings = ShortcutSettingsService.LoadShortcutMappings();
        SetDropdownValue(_buttonYDropdown, mappings.GetAction(ShortcutButtons.ButtonY));
        SetDropdownValue(_buttonBDropdown, mappings.GetAction(ShortcutButtons.ButtonB));
    }

    private void SaveGestureMappings()
    {
        ShortcutConfigData mappings = ShortcutSettingsService.LoadShortcutMappings();
        mappings.SetAction(ShortcutButtons.ButtonY, GetDropdownAction(_buttonYDropdown));
        mappings.SetAction(ShortcutButtons.ButtonB, GetDropdownAction(_buttonBDropdown));

        ShortcutSettingsService.SaveShortcutMappings(mappings);
        if (_shortcutManager == null)
            _shortcutManager = FindAnyObjectByType<ShortcutManager>();
        _shortcutManager?.ReloadConfig();
    }

    private void ApplyPlaybackRate(float rate)
    {
        _playbackService?.SetPlaybackRate(rate);
        UpdatePlaybackRateControl();
    }

    private void ToggleSpatialSubtitles()
    {
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        if (_playbackService == null)
            return;

        bool enableSpatial = _playbackService.SubtitleRenderMode != SubtitleRenderMode.Spatial;
        _playbackService.SetSubtitleRenderMode(enableSpatial ? SubtitleRenderMode.Spatial : SubtitleRenderMode.Native);
        UpdateSubtitleControls();
    }

    private void ToggleRenderSubtitlesOutsideScreen()
    {
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        if (_playbackService == null || _playbackService.SubtitleRenderMode != SubtitleRenderMode.Spatial)
            return;

        _playbackService.SetRenderSubtitlesOutsideScreen(!_playbackService.RenderSubtitlesOutsideScreen);
        UpdateSubtitleControls();
    }

    private void ApplySubtitleFontFromDropdown(int index)
    {
        if (index < 0 || index >= SubtitleFontSizeValues.Length)
        {
            UpdateSubtitleControls();
            return;
        }

        VlcPlaybackBridge.SetSubtitleFontSize(SubtitleFontSizeValues[index]);
        UpdateSubtitleControls();
    }

    private void ApplySubtitleOpacityFromSlider(float value)
    {
        int opacity = Mathf.Clamp(Mathf.RoundToInt(value), SubtitleOpacityMinimum, SubtitleOpacityMaximum);
        VlcPlaybackBridge.SetSubtitleOpacity(opacity);
        UpdateSubtitleOpacityControl(opacity);
    }

    private void PreviewSubtitleOpacityFromSlider(float value)
    {
        int opacity = Mathf.Clamp(Mathf.RoundToInt(value), SubtitleOpacityMinimum, SubtitleOpacityMaximum);
        UpdateSubtitleOpacityValueText(opacity);
    }

    private void StepSubtitleDelay(float deltaSeconds)
    {
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        if (_playbackService == null)
            return;

        _playbackService.SetSubtitleDelaySeconds(_playbackService.SubtitleDelaySeconds + deltaSeconds);
        UpdateSubtitleControls();
    }

    private void ApplySubtitleDelayFromInput(string value)
    {
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        if (_playbackService == null)
            return;

        if (TryParseFloat(value, out float seconds))
            _playbackService.SetSubtitleDelaySeconds(SnapToStep(seconds, 0.5f));
        UpdateSubtitleControls();
    }

    private void StepAudioDelay(float deltaSeconds)
    {
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        if (_playbackService == null)
            return;

        _playbackService.SetAudioDelaySeconds(_playbackService.AudioDelaySeconds + deltaSeconds);
        UpdateAudioDelayControl();
    }

    private void ApplyAudioDelayFromInput(string value)
    {
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        if (_playbackService == null)
            return;

        if (TryParseFloat(value, out float seconds))
            _playbackService.SetAudioDelaySeconds(SnapToStep(seconds, 0.5f));
        UpdateAudioDelayControl();
    }

    private void StepPlaybackRate(float delta)
    {
        float current = _playbackService != null ? VlcPlaybackBridge.GetPlaybackRate() : 1f;
        ApplyPlaybackRate(Mathf.Max(0.25f, SnapToStep(current + delta, 0.25f)));
    }

    private void ApplyPlaybackRateFromInput(string value)
    {
        if (TryParseFloat(value, out float rate))
            ApplyPlaybackRate(Mathf.Max(0.25f, SnapToStep(rate, 0.25f)));
        else
            UpdatePlaybackRateControl();
    }

    private void StepSeekSeconds(int deltaSeconds)
    {
        int current = ShortcutSettingsService.LoadShortcutSeekSeconds();
        ApplySeekSeconds(current + deltaSeconds);
    }

    private void ApplySeekSecondsFromInput(string value)
    {
        if (TryParseInt(value, out int seconds))
            ApplySeekSeconds(seconds);
        else
            UpdateSeekSecondsControl();
    }

    private void ApplySeekSeconds(int seconds)
    {
        int safeSeconds = ShortcutSettingsService.ClampShortcutSeekSeconds(seconds);
        ShortcutSettingsService.SaveShortcutSeekSeconds(safeSeconds);
        if (_shortcutManager == null)
            _shortcutManager = FindAnyObjectByType<ShortcutManager>();
        _shortcutManager?.ReloadConfig();
        UpdateSeekSecondsControl();
    }

    private void ApplyVideoAspectRatio(VideoAspectRatio aspectRatio)
    {
        _currentVideoAspectRatio = aspectRatio;
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();

        if (_playbackService != null)
            _playbackService.SetVideoAspectRatio(aspectRatio);
        else
            PlaybackUiSettingsService.SaveVideoAspectRatio(aspectRatio);

        UpdateVideoLayoutSelection();
    }

    private void ApplyVideoAspectRatioFromDropdown(int index)
    {
        ApplyVideoAspectRatio(GetVideoAspectRatioOption(index));
    }

    private void ToggleAudioBoost()
    {
        bool enabled = !VlcPlaybackBridge.IsAudioBoostEnabled();
        VlcPlaybackBridge.SetAudioBoostEnabled(enabled);
        UpdateAudioBoostSwitch();
    }

    private void UpdateAudioBoostSwitch()
    {
        SetSwitchButtonState(_audioBoostSwitchButton, VlcPlaybackBridge.IsAudioBoostEnabled());
    }

    private void UpdateAudioDelayControl()
    {
        if (_audioDelayStepper != null)
            _audioDelayStepper.SetValueWithoutNotify(FormatSignedSeconds(_playbackService != null ? _playbackService.AudioDelaySeconds : 0f));
    }

    private void UpdateSubtitleControls()
    {
        SubtitleRenderMode mode = _playbackService != null
            ? _playbackService.SubtitleRenderMode
            : SubtitleRenderMode.Spatial;

        bool spatial = mode == SubtitleRenderMode.Spatial;
        SetSwitchButtonState(_spatialSubtitleSwitchButton, spatial);
        SetSwitchButtonState(_outsideSubtitleSwitchButton, _playbackService != null && _playbackService.RenderSubtitlesOutsideScreen);
        SetDependentSwitchRowVisible(_outsideSubtitleSwitchButton, spatial);

        if (_subtitleFontDropdown != null)
            _subtitleFontDropdown.SetValueWithoutNotify(GetSubtitleFontSizeIndex(VlcPlaybackBridge.GetSubtitleFontSize()));
        UpdateSubtitleOpacityControl(VlcPlaybackBridge.GetSubtitleOpacity());

        if (_subtitleDelayStepper != null)
            _subtitleDelayStepper.SetValueWithoutNotify(FormatSignedSeconds(_playbackService != null ? _playbackService.SubtitleDelaySeconds : 0f));
    }

    private void UpdatePlaybackRateControl()
    {
        if (_playbackRateStepper != null)
            _playbackRateStepper.SetValueWithoutNotify(FormatRate(VlcPlaybackBridge.GetPlaybackRate()));
    }

    private void UpdateSeekSecondsControl()
    {
        if (_seekSecondsStepper != null)
            _seekSecondsStepper.SetValueWithoutNotify(FormatSeconds(ShortcutSettingsService.LoadShortcutSeekSeconds()));
    }

    private void OnPlaybackRateChanged(float rate)
    {
        if (_playbackRateStepper != null)
            _playbackRateStepper.SetValueWithoutNotify(FormatRate(rate));
    }

    private void SetSwitchButtonState(Button button, bool enabled)
    {
        if (button == null) return;

        SettingsStyleSwitch switchVisual = button.GetComponentInChildren<SettingsStyleSwitch>(true);
        switchVisual?.SetState(enabled);
    }

    private static void SetDependentSwitchRowVisible(Button button, bool visible)
    {
        if (button == null)
            return;

        button.interactable = visible;
        Transform valueColumn = button.transform.parent;
        Transform row = valueColumn != null ? valueColumn.parent : null;
        if (row != null)
            row.gameObject.SetActive(visible);
        else if (valueColumn != null)
            valueColumn.gameObject.SetActive(visible);
    }

    private void UpdateVideoLayoutSelection()
    {
        if (_videoAspectRatioDropdown != null)
            _videoAspectRatioDropdown.SetValueWithoutNotify(GetVideoAspectRatioIndex(_currentVideoAspectRatio));
    }

    private static List<XrDropdownItemData> CreateSubtitleFontItems()
    {
        var items = new List<XrDropdownItemData>(SubtitleFontSizeLabels.Length);
        for (int i = 0; i < SubtitleFontSizeLabels.Length; i++)
            items.Add(new XrDropdownItemData(XrUiText.Get(SubtitleFontSizeLabels[i])));
        return items;
    }

    private static int GetSubtitleFontSizeIndex(string value)
    {
        for (int i = 0; i < SubtitleFontSizeValues.Length; i++)
            if (SubtitleFontSizeValues[i] == value)
                return i;

        return 4;
    }

    private void UpdateSubtitleOpacityControl(int opacity)
    {
        int safeOpacity = Mathf.Clamp(opacity, SubtitleOpacityMinimum, SubtitleOpacityMaximum);
        if (_subtitleOpacitySlider != null)
            _subtitleOpacitySlider.SetValueWithoutNotify(safeOpacity);
        UpdateSubtitleOpacityValueText(safeOpacity);
    }

    private void UpdateSubtitleOpacityValueText(int opacity)
    {
        if (_subtitleOpacityValueText != null)
        {
            int percent = Mathf.RoundToInt(opacity * 100f / SubtitleOpacityMaximum);
            _subtitleOpacityValueText.text = percent.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }

    private static List<XrDropdownItemData> CreateVideoAspectRatioItems()
    {
        var items = new List<XrDropdownItemData>(VideoAspectRatioLabels.Length);
        for (int i = 0; i < VideoAspectRatioLabels.Length; i++)
            items.Add(new XrDropdownItemData(GetVideoAspectRatioLabel(VideoAspectRatioLabels[i])));
        return items;
    }

    private static string GetVideoAspectRatioLabel(string label)
    {
        return label == XrUiTextKey.SettingsAspectRatioAuto ? XrUiText.Get(label) : label;
    }

    private static int GetVideoAspectRatioIndex(VideoAspectRatio aspectRatio)
    {
        for (int i = 0; i < VideoAspectRatioOptions.Length; i++)
            if (VideoAspectRatioOptions[i] == aspectRatio)
                return i;

        return 0;
    }

    private static VideoAspectRatio GetVideoAspectRatioOption(int index)
    {
        return index >= 0 && index < VideoAspectRatioOptions.Length
            ? VideoAspectRatioOptions[index]
            : VideoAspectRatio.Source;
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            || float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryParseInt(string value, out int result)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            return true;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out result))
            return true;
        if (TryParseFloat(value, out float floatValue))
        {
            result = Mathf.RoundToInt(floatValue);
            return true;
        }

        result = 0;
        return false;
    }

    private static float SnapToStep(float value, float step)
    {
        return step <= 0f ? value : Mathf.Round(value / step) * step;
    }

    private static string FormatSignedSeconds(float seconds)
    {
        return seconds.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string FormatRate(float rate)
    {
        if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            rate = 1f;
        return rate.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatSeconds(int seconds)
    {
        return seconds.ToString(CultureInfo.InvariantCulture);
    }

    private void SetDropdownValue(XrDropdown dropdown, string actionKey)
    {
        if (dropdown == null) return;
        int index = _actionKeys.IndexOf(actionKey);
        dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
    }

    private string GetDropdownAction(XrDropdown dropdown)
    {
        if (dropdown == null || dropdown.Value < 0 || dropdown.Value >= _actionKeys.Count)
            return ShortcutActions.None;
        return _actionKeys[dropdown.Value];
    }

    private void SetButtonSelected(Button button, bool selected)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        ApplyButtonTheme(button, image, selected);
    }

    private void ApplyButtonTheme(Button button, Image image, bool selected)
    {
        if (button == null) return;

        if (buttonTheme != null)
        {
            XrThemedButton themed = button.GetComponent<XrThemedButton>();
            if (themed == null)
                themed = button.gameObject.AddComponent<XrThemedButton>();

            themed.theme = buttonTheme;
            themed.button = button;
            themed.background = image;
            themed.icon = null;
            themed.hoverEnabled = true;
            themed.selected = selected;
            themed.hideLegacyText = false;
            themed.ApplyTheme();
            return;
        }

        SetButtonColorsFallback(button, image, selected);
    }

    private static void SetButtonColorsFallback(Button button, Image image, bool selected)
    {
        if (button == null) return;

        Color normal = selected ? SelectedButtonColor : TransparentButtonColor;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = selected ? SelectedButtonHoverColor : HoverButtonColor;
        colors.selectedColor = selected ? SelectedButtonHoverColor : HoverButtonColor;
        colors.pressedColor = SelectedButtonColor;
        colors.disabledColor = normal;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (image != null)
        {
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;
        }
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
