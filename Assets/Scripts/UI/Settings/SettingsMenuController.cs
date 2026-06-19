using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRVLC;
using XRVLC.Media;
using XRVLC.Services.Settings;
using XRVLC.Services.Shortcuts;
using XRVLC.XR;

public class SettingsMenuController : MonoBehaviour
{
    private enum AudioChannelMode
    {
        Stereo,
        Mono
    }

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
    private const string AudioChannelStereoValue = "stereo";
    private const string AudioChannelMonoValue = "mono";
    private const float GestureLabelColumnWidthRatio = 0.25f;

    [Header("按钮主题")]
    public XrButtonTheme buttonTheme;

    [Header("Dropdown Prefabs")]
    public XrDropdown shortcutDropdownPrefab;

    [Header("Layout")]
    public float menuWidth = 720f;
    public float menuHeight = 420f;
    public float menuFontSize = 22f;
    public float dropdownFontSize = 20f;
    public float shortcutDropdownWidth = 320f;
    public float shortcutDropdownHeight = 48f;
    public float dropdownCornerRadius = 8f;
    public float dropdownBorderWidth = 1f;
    public float sectionTitleLeftPadding = 104f;

    private readonly Dictionary<SettingsTab, Button> _tabButtons = new Dictionary<SettingsTab, Button>();
    private readonly Dictionary<SettingsTab, GameObject> _contentRoots = new Dictionary<SettingsTab, GameObject>();
    private readonly List<string> _actionKeys = new List<string>(ShortcutActions.All);

    private XRVLC.Media.PlaybackService _playbackService;
    private ShortcutManager _shortcutManager;
    private bool _built;
    private SettingsTab _currentTab = SettingsTab.Playback;

    private XrDropdown _leftStickClickDropdown;
    private XrDropdown _rightStickClickDropdown;
    private XrDropdown _buttonYDropdown;
    private XrDropdown _buttonBDropdown;
    private readonly List<Button> _subtitleModeButtons = new List<Button>();
    private readonly List<Button> _videoScaleButtons = new List<Button>();
    private readonly List<Button> _audioModeButtons = new List<Button>();
    private int _currentVideoScaleOrdinal;
    private AudioChannelMode _currentAudioMode;

    public void Bind(XRVLC.Media.PlaybackService playbackService, ShortcutManager shortcutManager = null)
    {
        _playbackService = playbackService;
        _shortcutManager = shortcutManager;
        BuildIfNeeded();
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
            CreateTabButton(tabBar, SettingsTab.Playback, "播放");
            CreateTabButton(tabBar, SettingsTab.Gesture, "手势");
            CreateTabButton(tabBar, SettingsTab.Subtitle, "字幕");
            CreateTabButton(tabBar, SettingsTab.Video, "视频");
            CreateTabButton(tabBar, SettingsTab.Audio, "音频");

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
        CreateSectionLabel(root.transform, "播放速度");
        Transform row = CreateRow(root.transform, "PlaybackSpeedRow");
        CreateButton(row, "0.5x", menuFontSize, () => ApplyPlaybackRate(0.5f), 104f, 46f);
        CreateButton(row, "1x", menuFontSize, () => ApplyPlaybackRate(1f), 104f, 46f);
        CreateButton(row, "1.25x", menuFontSize, () => ApplyPlaybackRate(1.25f), 104f, 46f);
        CreateButton(row, "1.5x", menuFontSize, () => ApplyPlaybackRate(1.5f), 104f, 46f);
        CreateButton(row, "2x", menuFontSize, () => ApplyPlaybackRate(2f), 104f, 46f);
    }

    private void BuildGestureTab(GameObject root)
    {
        CreateSectionLabel(root.transform, "手柄快捷键");
        _leftStickClickDropdown = CreateShortcutDropdown(root.transform, "左摇杆按下");
        _rightStickClickDropdown = CreateShortcutDropdown(root.transform, "右摇杆按下");
        _buttonYDropdown = CreateShortcutDropdown(root.transform, "Y 按钮");
        _buttonBDropdown = CreateShortcutDropdown(root.transform, "B 按钮");
        CreateGestureSaveRow(root.transform);
        LoadGestureValues();
    }

    private void BuildSubtitleTab(GameObject root)
    {
        CreateSectionLabel(root.transform, "字幕渲染模式");
        Transform row = CreateRow(root.transform, "SubtitleRenderModeRow");
        _subtitleModeButtons.Add(CreateButton(row, "原生", menuFontSize, () => ApplySubtitleMode(SubtitleRenderMode.Native), 132f, 46f));
        _subtitleModeButtons.Add(CreateButton(row, "空间", menuFontSize, () => ApplySubtitleMode(SubtitleRenderMode.Spatial), 132f, 46f));
        _subtitleModeButtons.Add(CreateButton(row, "关闭", menuFontSize, () => ApplySubtitleMode(SubtitleRenderMode.Off), 132f, 46f));
    }

    private void BuildVideoTab(GameObject root)
    {
        CreateSectionLabel(root.transform, "画面拉伸裁剪");
        Transform firstRow = CreateRow(root.transform, "VideoScalePrimaryRow");
        _videoScaleButtons.Add(CreateVideoScaleButton(firstRow, "适应", 0));
        _videoScaleButtons.Add(CreateVideoScaleButton(firstRow, "拉伸", 1));
        _videoScaleButtons.Add(CreateVideoScaleButton(firstRow, "填充裁剪", 2));
        _videoScaleButtons.Add(CreateVideoScaleButton(firstRow, "原始", 11));

        Transform aspectRow = CreateRow(root.transform, "VideoScaleAspectRow");
        _videoScaleButtons.Add(CreateVideoScaleButton(aspectRow, "16:9", 3));
        _videoScaleButtons.Add(CreateVideoScaleButton(aspectRow, "4:3", 4));
        _videoScaleButtons.Add(CreateVideoScaleButton(aspectRow, "16:10", 5));
        _videoScaleButtons.Add(CreateVideoScaleButton(aspectRow, "2.35:1", 8));
    }

    private void BuildAudioTab(GameObject root)
    {
        CreateSectionLabel(root.transform, "声道输出");
        Transform row = CreateRow(root.transform, "AudioChannelModeRow");
        _audioModeButtons.Add(CreateButton(row, "立体声", menuFontSize, () => ApplyAudioMode(AudioChannelMode.Stereo), 154f, 46f));
        _audioModeButtons.Add(CreateButton(row, "混合单声道", menuFontSize, () => ApplyAudioMode(AudioChannelMode.Mono), 206f, 46f));
    }

    private Button CreateVideoScaleButton(Transform parent, string label, int scaleOrdinal)
    {
        Button button = CreateButton(parent, label, menuFontSize, () => ApplyVideoScale(scaleOrdinal), 132f, 46f);
        button.gameObject.name = label + "#" + scaleOrdinal;
        return button;
    }

    private XrDropdown CreateShortcutDropdown(Transform parent, string label)
    {
        Transform row = CreateGestureRow(parent, label + "Row");
        CreateText(row, label, menuFontSize, TextAlignmentOptions.Left, GetGestureLabelColumnWidth(), shortcutDropdownHeight);

        if (shortcutDropdownPrefab == null)
        {
            Debug.LogError("[SettingsMenuController] shortcutDropdownPrefab is not assigned. Gesture shortcuts require the shared XrDropdown prefab.");
            return null;
        }

        XrDropdown dropdown = Instantiate(shortcutDropdownPrefab, row, false);

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
            new XrDropdownItemData("无操作"),
            new XrDropdownItemData("切换 2x 速度"),
            new XrDropdownItemData("切换字幕"),
            new XrDropdownItemData("恢复屏幕默认位置")
        });
        dropdown.ApplyConfiguredLayout();
        return dropdown;
    }

    private void CreateGestureSaveRow(Transform parent)
    {
        Transform row = CreateGestureRow(parent, "GestureSaveRow");
        CreateButton(row, "保存手势设置", menuFontSize, SaveGestureMappings, 216f, 48f);
    }

    private void ConfigureShortcutDropdownVisual(XrDropdown dropdown)
    {
        if (dropdown == null || dropdown.captionButton == null)
            return;

        ConfigureRoundedDropdownGraphic(dropdown.gameObject, false);
        if (dropdown.captionText != null)
        {
            dropdown.captionText.alignment = TextAlignmentOptions.Center;
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
        layout.spacing = 8f;
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
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var element = row.GetComponent<LayoutElement>();
        element.preferredHeight = 52f;
        return row.transform;
    }

    private float GetGestureLabelColumnWidth()
    {
        return Mathf.Max(160f, menuWidth * GestureLabelColumnWidthRatio);
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

    private void CreateSectionLabel(Transform parent, string label)
    {
        Transform row = CreateSectionLabelRow(parent, label + "TitleRow");
        CreateSpacer(row, sectionTitleLeftPadding, 36f);
        CreateText(row, label, menuFontSize, TextAlignmentOptions.Left, Mathf.Max(0f, menuWidth - sectionTitleLeftPadding), 36f);
    }

    private Transform CreateSectionLabelRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleLeft;
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

    private void ShowTab(SettingsTab tab)
    {
        _currentTab = tab;

        foreach (KeyValuePair<SettingsTab, GameObject> pair in _contentRoots)
            pair.Value.SetActive(pair.Key == tab);

        foreach (KeyValuePair<SettingsTab, Button> pair in _tabButtons)
            SetButtonSelected(pair.Value, pair.Key == tab);

        if (tab == SettingsTab.Gesture)
            LoadGestureValues();
    }

    private void RefreshAllSelections()
    {
        _currentVideoScaleOrdinal = PlaybackUiSettingsService.LoadVideoScaleOrdinal();
        UpdateSubtitleModeSelection();
        UpdateVideoScaleSelection();
        UpdateAudioModeSelection();
        LoadGestureValues();
    }

    private void LoadGestureValues()
    {
        if (_leftStickClickDropdown == null)
            return;

        ShortcutConfigData mappings = ShortcutSettingsService.LoadShortcutMappings();
        SetDropdownValue(_leftStickClickDropdown, mappings.GetAction(ShortcutButtons.LeftStickClick));
        SetDropdownValue(_rightStickClickDropdown, mappings.GetAction(ShortcutButtons.RightStickClick));
        SetDropdownValue(_buttonYDropdown, mappings.GetAction(ShortcutButtons.ButtonY));
        SetDropdownValue(_buttonBDropdown, mappings.GetAction(ShortcutButtons.ButtonB));
    }

    private void SaveGestureMappings()
    {
        var mappings = new ShortcutConfigData();
        mappings.SetAction(ShortcutButtons.LeftStickClick, GetDropdownAction(_leftStickClickDropdown));
        mappings.SetAction(ShortcutButtons.RightStickClick, GetDropdownAction(_rightStickClickDropdown));
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
    }

    private void ApplySubtitleMode(SubtitleRenderMode mode)
    {
        if (_playbackService == null)
            _playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        _playbackService?.SetSubtitleRenderMode(mode);
        UpdateSubtitleModeSelection();
    }

    private void ApplyVideoScale(int scaleOrdinal)
    {
        _currentVideoScaleOrdinal = scaleOrdinal;
        PlaybackUiSettingsService.SaveVideoScaleOrdinal(scaleOrdinal);
        VlcPlaybackBridge.SetVideoScaleOrdinal(scaleOrdinal);
        UpdateVideoScaleSelection();
    }

    private void ApplyAudioMode(AudioChannelMode mode)
    {
        _currentAudioMode = mode;
        VlcPlaybackBridge.SetAudioChannelMode(ToAudioChannelModeValue(mode));
        UpdateAudioModeSelection();
    }

    private void UpdateSubtitleModeSelection()
    {
        SubtitleRenderMode mode = _playbackService != null
            ? _playbackService.SubtitleRenderMode
            : SubtitleRenderMode.Spatial;

        SetButtonSelected(_subtitleModeButtons.Count > 0 ? _subtitleModeButtons[0] : null, mode == SubtitleRenderMode.Native);
        SetButtonSelected(_subtitleModeButtons.Count > 1 ? _subtitleModeButtons[1] : null, mode == SubtitleRenderMode.Spatial);
        SetButtonSelected(_subtitleModeButtons.Count > 2 ? _subtitleModeButtons[2] : null, mode == SubtitleRenderMode.Off);
    }

    private void UpdateVideoScaleSelection()
    {
        for (int i = 0; i < _videoScaleButtons.Count; i++)
        {
            Button button = _videoScaleButtons[i];
            int scaleOrdinal = ExtractTrailingScaleOrdinal(button);
            SetButtonSelected(button, scaleOrdinal == _currentVideoScaleOrdinal);
        }
    }

    private void UpdateAudioModeSelection()
    {
        SetButtonSelected(_audioModeButtons.Count > 0 ? _audioModeButtons[0] : null, _currentAudioMode == AudioChannelMode.Stereo);
        SetButtonSelected(_audioModeButtons.Count > 1 ? _audioModeButtons[1] : null, _currentAudioMode == AudioChannelMode.Mono);
    }

    private static int ExtractTrailingScaleOrdinal(Button button)
    {
        string objectName = button != null ? button.gameObject.name : string.Empty;
        int separator = objectName.LastIndexOf('#');
        if (separator < 0)
            return -1;
        return int.TryParse(objectName.Substring(separator + 1), out int value) ? value : -1;
    }

    private static string ToAudioChannelModeValue(AudioChannelMode mode)
    {
        return mode == AudioChannelMode.Mono ? AudioChannelMonoValue : AudioChannelStereoValue;
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
