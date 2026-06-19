using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using XRVLC.Infrastructure.Pico;
using XRVLC.UI.XR;

public class VRUIManager : MonoBehaviour
{
    private const string IconResourcePath = "UI/IconPark/";
    private const string BatteryUnknownIcon = "battery";
    private const string BatteryEmptyIcon = "battery-empty";
    private const string BatteryLowIcon = "battery-low";
    private const string BatteryMediumIcon = "battery-medium";
    private const string BatteryFullIcon = "battery-full";
    private static readonly Color IconTint = Color.white;
    private static readonly Color TransparentListColor = new Color(1f, 1f, 1f, 0f);
    private static readonly Color HoverListColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color SelectedListColor = new Color(1f, 1f, 1f, 0.24f);
    private static readonly Color SelectedListHoverColor = new Color(1f, 1f, 1f, 0.32f);
    private static readonly Color SelectedGeometryOptionColor = SelectedListColor;
    private static readonly Color SelectedGeometryOptionHoverColor = SelectedListHoverColor;
    private static readonly Color PassthroughSelectedColor = new Color(1f, 1f, 1f, 0.24f);
    private static readonly Color PassthroughDisabledColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color PressedListColor = new Color(1f, 1f, 1f, 0.24f);
    private static readonly Color SecondaryPanelColor = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color OpaqueTextColor = Color.white;
    private const int PopupSortingOrder = 500;
    private const float PrimaryPlaybackIconSize = 51f;
    private const float AdjacentPlaybackIconSize = 48f;
    private const float DropdownPopupGap = 4f;
    private const float SystemStatusRefreshInterval = 2f;
    private const float SystemSliderPopupWidth = 54f;
    private const float SystemSliderPopupHeight = 252f;
    private const float SystemSliderLength = 216f;
    private const float SystemSliderSize = 28f;
    private const float SystemSliderHandleSize = 27f;
    private const float SystemSliderTrackMin = 0.4f;
    private const float SystemSliderTrackMax = 0.6f;
    private const float SystemTimeTextWidth = 96f;
    private const float BatteryStatusTextWidth = 76f;
    private const float SystemStatusFontSize = 26f;
    private const float DefaultBatteryIconSize = 40f;
    private const float GeometryMenuFontSize = 22f;
    private const float geometryMenuFlatHeight = 280f;
    private const float geometryMenuPanoramicHeight = 194f;
    private const float MinWorldCanvasDynamicPixelsPerUnit = 12f;
    private const int AndroidStreamMusic = 3;
    private static readonly System.Collections.Generic.HashSet<string> SubtitleLanguageSuffixes =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sc", "tc", "chs", "cht", "zh", "zho", "chi",
            "en", "eng",
            "ja", "jpn",
            "ko", "kor"
        };
    private static readonly System.Collections.Generic.HashSet<string> SubtitleFileExtensions =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ass", ".ssa", ".srt", ".vtt", ".sub"
        };
    private static readonly System.Collections.Generic.HashSet<string> MediaFileExtensions =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".m4v", ".avi", ".mov", ".wmv", ".flv", ".webm",
            ".ts", ".m2ts", ".mts", ".3gp", ".ogv", ".rmvb"
        };
    private static readonly char[] SubtitleNameSeparators = { '.', '-', '_', ' ', '[', ']', '(', ')' };

    [Header("主面板")]
    public GameObject controlPanel;
    public GameObject topRightGroup;

    [Header("顶部信息栏")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI systemTimeText;
    public TextMeshProUGUI batteryText;
    public float batteryIconSize = DefaultBatteryIconSize;

    [Header("进度条区域")]
    public Slider progressSlider;
    public TextMeshProUGUI currentTimeText;
    public TextMeshProUGUI totalTimeText;

    [Header("底部控制栏")]
    public Button playBtn;
    public Button previousBtn;
    public Button nextBtn;
    public Button speedBtn;
    public TextMeshProUGUI speedBtnText;
    public Button playlistToggleBtn;

    [Header("图标占位按钮")]
    public Button exitBtn;
    public Button lockBtn;
    public Button brightnessBtn;
    public Button volumeBtn;
    public Button equalizerBtn;
    public Button subtitleBtn;
    public Button eyeBtn;
    public Button threeDBtn;
    public Button settingsBtn;

    [Header("系统滑条")]
    public GameObject systemSliderPopupPrefab;

    [Header("轨道控制 (隐藏面板)")]
    public XrDropdown audioTrackDropdown;
    public XrDropdown subtitleTrackDropdown;
    public float trackDropdownWidth = 520f / 3f;
    public int trackDropdownMaxVisibleItems = 10;
    public float trackDropdownFontSize = 22f;
    public float trackDropdownHorizontalPadding = 10f;
    public float trackDropdownVerticalPadding = 15f;

    [Header("设置菜单")]
    public GameObject settingsMenu;
    public SettingsMenuController settingsMenuController;
    public Button shortcutConfigEntryBtn;
    public Button subtitleRenderModeBtn;
    public TextMeshProUGUI subtitleRenderModeBtnText;
    public ShortcutConfigPanel shortcutConfigPanel;

    [Header("视频 Geometry 菜单")]
    public GameObject geometryMenu;

    [Header("播放列表")]
    public PlaylistPanelController playlistPanel;

    [Header("按钮主题")]
    public XrButtonTheme buttonTheme;

    [Header("XR UI 路由")]
    public XrUiInputGate uiInputGate;

    private XRVLC.Media.PlaybackService playbackService;
    private PicoPassthroughModeService passthroughModeService;
    private bool isPanelVisible = true;
    private bool _isUpdatingSlider = false;

    private float _hideTimer = 0f;
    private const float PanelVisibleDuration = 3f;
    private bool _autoHidePending = false;
    private bool _triggerWasPressed = false;

    private float[] speedOptions = { 1.0f, 1.25f, 1.5f, 2.0f, 0.5f };
    private int currentSpeedIndex = 0;

    private System.Collections.Generic.List<XRVLC.Media.TrackInfo> currentAudioTracks;
    private System.Collections.Generic.List<XRVLC.Media.TrackInfo> currentSubtitleTracks;
    private XRVLC.VideoProjection _geometryProjection = XRVLC.VideoProjection.Flat;
    private XRVLC.StereoMode _geometryStereo = XRVLC.StereoMode.Mono;
    private XRVLC.FlatVideoCurveMode _flatCurveMode = XRVLC.FlatVideoCurveMode.None;
    private Button _projection180Button;
    private Button _projection360Button;
    private Button _projectionFlatButton;
    private Button _stereoMonoButton;
    private Button _stereoTopBottomButton;
    private Button _stereoLeftRightButton;
    private GameObject _curveSectionLabel;
    private Transform _curveRow;
    private Button _curveNoneButton;
    private Button _curveSmallButton;
    private Button _curveLargeButton;
    private Coroutine _pendingTrackDropdownShow;
    private GameObject systemSliderPopup;
    private Slider systemSlider;
    private Image batteryIcon;
    private SystemSliderMode _activeSystemSliderMode = SystemSliderMode.None;
    private bool _isUpdatingSystemSlider;
    private float _simulatedBrightness = 0.75f;
    private float _simulatedVolume = 0.75f;
    private float _nextSystemStatusRefreshTime;

    private enum SystemSliderMode
    {
        None,
        Brightness,
        Volume
    }

    private void Start()
    {
        playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();

        if (playbackService != null)
        {
            playbackService.OnStatusChanged += OnStatusChanged;
            playbackService.OnTimeChanged += UpdateTime;
            playbackService.OnMediaChanged += UpdateMediaInfo;
            playbackService.OnAudioTracksChanged += UpdateAudioTracksDropdown;
            playbackService.OnSubtitleTracksChanged += UpdateSubtitleTracksDropdown;
        }
        else
        {
            Debug.LogWarning("VRUIManager 未找到 PlaybackService，UI 将无法响应播放器事件！");
        }

        if (progressSlider != null)
            progressSlider.onValueChanged.AddListener(OnSliderValueChanged);

        if (speedBtn != null)
        {
            speedBtn.onClick.AddListener(OnSpeedBtnClicked);
            UpdateSpeedBtnText();
        }

        if (playBtn != null)
            playBtn.onClick.AddListener(OnPlayPauseButtonClicked);
        if (previousBtn != null)
            previousBtn.onClick.AddListener(OnPreviousBtnClicked);
        if (nextBtn != null)
            nextBtn.onClick.AddListener(OnNextBtnClicked);
        if (playlistToggleBtn != null)
            playlistToggleBtn.onClick.AddListener(OnPlaylistToggleBtnClicked);
        if (exitBtn != null)
            exitBtn.onClick.AddListener(OnExitBtnClicked);
        if (brightnessBtn != null)
            brightnessBtn.onClick.AddListener(OnBrightnessBtnClicked);
        if (volumeBtn != null)
            volumeBtn.onClick.AddListener(OnVolumeBtnClicked);
        if (equalizerBtn != null)
            equalizerBtn.onClick.AddListener(OnAudioTrackBtnClicked);
        if (subtitleBtn != null)
            subtitleBtn.onClick.AddListener(OnSubtitleBtnClicked);
        EnsurePassthroughModeService();
        if (eyeBtn != null)
            eyeBtn.onClick.AddListener(OnEyeBtnClicked);
        if (threeDBtn != null)
            threeDBtn.onClick.AddListener(OnGeometryBtnClicked);
        if (settingsBtn != null)
            settingsBtn.onClick.AddListener(OnSettingsBtnClicked);
        EnsureSettingsMenuController();
        if (settingsMenu != null)
            settingsMenu.SetActive(false);
        if (geometryMenu != null)
            geometryMenu.SetActive(false);
        if (shortcutConfigPanel != null)
            shortcutConfigPanel.gameObject.SetActive(false);

        EnsureRuntimeTextVisible();
        ConfigureUiCanvasClarity();
        ConfigureSecondaryDropdown(audioTrackDropdown, true);
        ConfigureSecondaryDropdown(subtitleTrackDropdown, true);
        InitializeTrackDropdownDefault(audioTrackDropdown, "无音轨");
        InitializeTrackDropdownDefault(subtitleTrackDropdown, "无字幕");

        if (audioTrackDropdown != null)
            audioTrackDropdown.onValueChanged.AddListener(OnAudioTrackSelected);
        if (subtitleTrackDropdown != null)
            subtitleTrackDropdown.onValueChanged.AddListener(OnSubtitleTrackSelected);

        ApplyIconSprites();
        UpdateEyeButtonPassthroughVisual();
        if (playbackService != null)
            UpdatePlayButtonIcon(playbackService.CurrentStatus);
        UpdateSystemStatus();

        RegisterUiTreeNodes();
        SetPanelVisibility(false);
    }

    private void Update()
    {
        UpdateSystemStatus();

        HandleTriggerInput();

        if (_autoHidePending)
        {
            if (IsPointerOverManagedUi())
                return;

            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
            {
                _autoHidePending = false;
                SetPanelVisibility(false);
            }
        }
    }

    private void HandleTriggerInput()
    {
        var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool pressed = rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool val) && val;

        if (pressed && !_triggerWasPressed)
            HandleTriggerPressedEdge();

        _triggerWasPressed = pressed;
    }

    private void HandleTriggerPressedEdge()
    {
        bool hasCurrentUiTarget = TryGetXrUiTarget(out _);
        if (!isPanelVisible && !hasCurrentUiTarget)
        {
            CloseSecondaryPopups();
            ShowPanelAndScheduleHide();
            return;
        }

        if (TryHideVisiblePanelFromNonUiTrigger())
            return;

        if (TryCloseSettingsMenuFromCurrentUiTarget())
            return;

        if (TryCloseSystemSliderFromCurrentUiTarget())
            return;

        if (TryClosePlaylistFromCurrentUiTarget())
            return;

        if (TryConsumeCurrentUiTrigger())
            return;

        if (TryCloseTrackDropdownsFromCurrentUiTarget())
            return;

        ShowPanelAndScheduleHide();
    }

    private bool TryHideVisiblePanelFromNonUiTrigger()
    {
        if (!isPanelVisible)
            return false;

        bool hasTarget = TryGetXrUiTarget(out GameObject target);
        if (hasTarget)
            return false;

        Debug.Log($"[VRUIManager] hidePanelOutsideUi target={DescribeUiTarget(target)}");
        SetPanelVisibility(false);
        return true;
    }

    private bool TryConsumeCurrentUiTrigger()
    {
        RegisterUiTreeNodes();
        bool anyOpenBefore = IsAnyTrackDropdownOpen();
        GameObject target = null;
        bool hasTarget = uiInputGate != null && uiInputGate.TryGetCurrentUiTarget(out target);
        bool consumed = uiInputGate != null &&
            uiInputGate.TryConsumeCurrentHover(new XrUiEvent(XrUiEventType.TriggerPressed, XRNode.RightHand));

        if (anyOpenBefore || consumed)
        {
            Debug.Log(
                $"[VRUIManager][TrackDropdown] triggerGate consumed={consumed} " +
                $"hasTarget={hasTarget} target={DescribeUiTarget(target)} " +
                $"anyOpenBefore={anyOpenBefore} anyOpenAfter={IsAnyTrackDropdownOpen()}");
        }

        return consumed;
    }

    private bool TryCloseTrackDropdownsFromCurrentUiTarget()
    {
        if (!IsAnyTrackDropdownOpen())
            return false;

        bool hasTarget = TryGetXrUiTarget(out GameObject target);
        Debug.Log(
            $"[VRUIManager][TrackDropdown] fallbackCloseCheck hasTarget={hasTarget} " +
            $"target={DescribeUiTarget(target)} anyOpen=True");
        return hasTarget && CloseTrackDropdownsIfManagedUiClickOutsideDropdowns(target);
    }

    private bool TryCloseSettingsMenuFromCurrentUiTarget()
    {
        if (!IsSettingsMenuOpen())
            return false;

        bool hasTarget = TryGetXrUiTarget(out GameObject target);
        if (hasTarget && (IsSelfOrChildOf(target, settingsMenu) || IsSelfOrChildOf(target, settingsBtn != null ? settingsBtn.gameObject : null)))
            return false;

        Debug.Log($"[VRUIManager][SettingsMenu] closeOutside hasTarget={hasTarget} target={DescribeUiTarget(target)}");
        HideSettingsMenu();
        RegisterUiTreeNodes();
        return true;
    }

    private bool IsSettingsMenuOpen()
    {
        if (settingsMenuController != null)
            return settingsMenuController.IsOpen;
        return settingsMenu != null && settingsMenu.activeSelf;
    }

    private bool TryCloseSystemSliderFromCurrentUiTarget()
    {
        if (!IsSystemSliderOpen())
            return false;

        bool hasTarget = TryGetXrUiTarget(out GameObject target);
        Button activeButton = ActiveSystemSliderButton();
        if (hasTarget &&
            (IsSelfOrChildOf(target, systemSliderPopup) || IsSelfOrChildOf(target, activeButton != null ? activeButton.gameObject : null)))
        {
            return false;
        }

        HideSystemSlider();
        RegisterUiTreeNodes();
        return true;
    }

    private bool TryClosePlaylistFromCurrentUiTarget()
    {
        if (!IsPlaylistOpen())
            return false;

        bool hasTarget = TryGetXrUiTarget(out GameObject target);
        if (!hasTarget)
            return false;

        return ClosePlaylistIfManagedUiClickOutsidePlaylist(target);
    }

    private bool IsPlaylistOpen()
    {
        return playlistPanel != null && playlistPanel.IsVisible;
    }

    private bool TryGetXrUiTarget(out GameObject target)
    {
        target = null;

        RegisterUiTreeNodes();
        return uiInputGate != null && uiInputGate.TryGetCurrentUiTarget(out target);
    }

    private bool CloseTrackDropdownsIfManagedUiClickOutsideDropdowns(GameObject target)
    {
        if (!IsAnyTrackDropdownOpen())
            return false;

        if (target == null)
        {
            Debug.Log("[VRUIManager][TrackDropdown] closeSkip target=<null> anyOpen=True");
            return false;
        }

        bool isManaged = IsManagedUiObject(target);
        bool isTrackDropdownObject = IsTrackDropdownObject(target);
        if (!isManaged || isTrackDropdownObject)
        {
            Debug.Log(
                $"[VRUIManager][TrackDropdown] closeSkip target={DescribeUiTarget(target)} " +
                $"managed={isManaged} track={isTrackDropdownObject} anyOpen=True");
            return false;
        }

        Debug.Log(
            $"[VRUIManager][TrackDropdown] closeOutside target={DescribeUiTarget(target)} " +
            $"managed={isManaged} track={isTrackDropdownObject}");

        bool closed = false;
        closed |= CloseTrackDropdown(audioTrackDropdown);
        closed |= CloseTrackDropdown(subtitleTrackDropdown);
        return closed;
    }

    private bool ClosePlaylistIfManagedUiClickOutsidePlaylist(GameObject target)
    {
        if (!IsPlaylistOpen() || target == null)
            return false;

        bool isManaged = IsManagedUiObject(target);
        bool isPlaylistObject = IsPlaylistObject(target);
        if (!isManaged || isPlaylistObject)
            return false;

        Debug.Log($"[VRUIManager][Playlist] closeOutside target={DescribeUiTarget(target)}");
        playlistPanel.Hide();
        RegisterUiTreeNodes();
        return true;
    }

    private bool IsAnyTrackDropdownOpen()
    {
        return IsTrackDropdownOpen(audioTrackDropdown) || IsTrackDropdownOpen(subtitleTrackDropdown);
    }

    private static bool IsTrackDropdownOpen(XrDropdown dropdown)
    {
        return dropdown != null && dropdown.IsOpen;
    }

    private static bool ShouldShowTrackDropdown(XrDropdown dropdown)
    {
        return dropdown != null && dropdown.interactable && !dropdown.IsOpen;
    }

    private bool IsTrackDropdownObject(GameObject target)
    {
        return IsDropdownObject(target, audioTrackDropdown)
            || IsDropdownObject(target, subtitleTrackDropdown)
            || IsTrackDropdownToggleButtonObject(target);
    }

    private bool IsPlaylistObject(GameObject target)
    {
        return IsSelfOrChildOf(target, playlistPanel != null ? playlistPanel.panelRoot : null)
            || IsSelfOrChildOf(target, playlistToggleBtn != null ? playlistToggleBtn.gameObject : null);
    }

    private bool IsTrackDropdownToggleButtonObject(GameObject target)
    {
        return IsSelfOrChildOf(target, equalizerBtn != null ? equalizerBtn.gameObject : null)
            || IsSelfOrChildOf(target, subtitleBtn != null ? subtitleBtn.gameObject : null);
    }

    private static bool IsDropdownObject(GameObject target, XrDropdown dropdown)
    {
        return dropdown != null && dropdown.Contains(target);
    }

    private void ShowPanelAndScheduleHide()
    {
        SetPanelVisibility(true);
        SchedulePanelHide();
    }

    private void SchedulePanelHide()
    {
        _hideTimer = PanelVisibleDuration;
        _autoHidePending = true;
    }

    private bool IsPointerOverManagedUi()
    {
        RegisterUiTreeNodes();
        return uiInputGate != null && uiInputGate.IsHoveringAutoHideBlockingUi;
    }

    private void EnsureXrUiInputGate()
    {
        if (uiInputGate != null)
            return;

        uiInputGate = GetComponent<XrUiInputGate>();
        if (uiInputGate == null)
            uiInputGate = FindAnyObjectByType<XrUiInputGate>();
        if (uiInputGate == null)
            uiInputGate = gameObject.AddComponent<XrUiInputGate>();
    }

    private void RegisterUiTreeNodes()
    {
        EnsureXrUiInputGate();
        if (uiInputGate == null)
            return;

        uiInputGate.ClearNodes();

        var panelConsumer = new DelegateXrUiEventConsumer(ConsumeManagedUiTrigger);
        RegisterUiNode("control-panel", null, controlPanel, XrUiNodeLayer.Base, panelConsumer);
        RegisterUiNode("top-right-group", "control-panel", topRightGroup, XrUiNodeLayer.Base, panelConsumer);
        RegisterUiNode("exit-button", "top-right-group", exitBtn != null ? exitBtn.gameObject : null, XrUiNodeLayer.Base, panelConsumer);
        RegisterUiNode("audio-dropdown", "control-panel", audioTrackDropdown != null ? audioTrackDropdown.gameObject : null, XrUiNodeLayer.Popup);
        RegisterUiNode("subtitle-dropdown", "control-panel", subtitleTrackDropdown != null ? subtitleTrackDropdown.gameObject : null, XrUiNodeLayer.Popup);
        RegisterUiNode("settings-menu", "control-panel", settingsMenu, XrUiNodeLayer.Popup, panelConsumer);
        RegisterUiNode("geometry-menu", "control-panel", geometryMenu, XrUiNodeLayer.Popup, panelConsumer);
        RegisterUiNode("system-slider-popup", "control-panel", systemSliderPopup, XrUiNodeLayer.Popup, panelConsumer);
        RegisterUiNode("playlist-panel", "control-panel", playlistPanel != null ? playlistPanel.panelRoot : null, XrUiNodeLayer.Popup, panelConsumer);
        RegisterUiNode("shortcut-config-panel", "settings-menu", shortcutConfigPanel != null ? shortcutConfigPanel.gameObject : null, XrUiNodeLayer.Modal, panelConsumer);
        RegisterRuntimeUiTreeNodes();
    }

    private void RegisterRuntimeUiTreeNodes()
    {
        if (uiInputGate == null)
            return;

        RegisterUiNode("audio-dropdown-list", "audio-dropdown", ActiveDropdownList(audioTrackDropdown), XrUiNodeLayer.Popup);
        RegisterUiNode("subtitle-dropdown-list", "subtitle-dropdown", ActiveDropdownList(subtitleTrackDropdown), XrUiNodeLayer.Popup);
    }

    private void RegisterUiNode(
        string id,
        string parentId,
        GameObject root,
        XrUiNodeLayer layer,
        IXrUiEventConsumer consumer = null)
    {
        if (root == null)
            return;

        uiInputGate.RegisterNode(new XrUiNode(
            id,
            parentId,
            root,
            layer,
            true,
            true,
            true,
            consumer));
    }

    private bool ConsumeManagedUiTrigger(XrUiEvent evt, GameObject target)
    {
        if (!evt.IsTrigger)
            return false;

        if (IsAnyTrackDropdownOpen())
        {
            Debug.Log(
                $"[VRUIManager][TrackDropdown] managedTrigger target={DescribeUiTarget(target)} " +
                $"managed={IsManagedUiObject(target)} track={IsTrackDropdownObject(target)}");
            CloseTrackDropdownsIfManagedUiClickOutsideDropdowns(target);
        }

        if (IsPlaylistOpen())
            ClosePlaylistIfManagedUiClickOutsidePlaylist(target);

        return true;
    }

    private bool IsManagedUiObject(GameObject target)
    {
        if (target == null) return false;

        return IsSelfOrChildOf(target, controlPanel)
            || IsSelfOrChildOf(target, topRightGroup)
            || IsSelfOrChildOf(target, exitBtn != null ? exitBtn.gameObject : null)
            || IsSelfOrChildOf(target, audioTrackDropdown != null ? audioTrackDropdown.gameObject : null)
            || IsSelfOrChildOf(target, ActiveDropdownList(audioTrackDropdown))
            || IsSelfOrChildOf(target, subtitleTrackDropdown != null ? subtitleTrackDropdown.gameObject : null)
            || IsSelfOrChildOf(target, ActiveDropdownList(subtitleTrackDropdown))
            || IsSelfOrChildOf(target, settingsMenu)
            || IsSelfOrChildOf(target, geometryMenu)
            || IsSelfOrChildOf(target, systemSliderPopup)
            || IsSelfOrChildOf(target, shortcutConfigPanel != null ? shortcutConfigPanel.gameObject : null)
            || IsSelfOrChildOf(target, playlistPanel != null ? playlistPanel.panelRoot : null);
    }

    private static GameObject ActiveDropdownList(XrDropdown dropdown)
    {
        return dropdown != null ? dropdown.ActiveList : null;
    }

    private static bool IsSelfOrChildOf(GameObject target, GameObject root)
    {
        if (target == null || root == null) return false;
        return target == root || target.transform.IsChildOf(root.transform);
    }

    private void OnDestroy()
    {
        if (playbackService != null)
        {
            playbackService.OnStatusChanged -= OnStatusChanged;
            playbackService.OnTimeChanged -= UpdateTime;
            playbackService.OnMediaChanged -= UpdateMediaInfo;
            playbackService.OnAudioTracksChanged -= UpdateAudioTracksDropdown;
            playbackService.OnSubtitleTracksChanged -= UpdateSubtitleTracksDropdown;
        }

        if (settingsBtn != null)
            settingsBtn.onClick.RemoveListener(OnSettingsBtnClicked);
        if (threeDBtn != null)
            threeDBtn.onClick.RemoveListener(OnGeometryBtnClicked);
        if (eyeBtn != null)
            eyeBtn.onClick.RemoveListener(OnEyeBtnClicked);
        if (brightnessBtn != null)
            brightnessBtn.onClick.RemoveListener(OnBrightnessBtnClicked);
        if (volumeBtn != null)
            volumeBtn.onClick.RemoveListener(OnVolumeBtnClicked);
        if (systemSlider != null)
            systemSlider.onValueChanged.RemoveListener(OnSystemSliderValueChanged);
        if (passthroughModeService != null)
            passthroughModeService.StateChanged -= OnPassthroughStateChanged;
    }

    private void OnStatusChanged(XRVLC.Media.PlayerStatus status)
    {
        UpdatePlayButtonIcon(status);
        if (status == XRVLC.Media.PlayerStatus.Playing && isPanelVisible)
            SchedulePanelHide();
    }

    private void SetPanelVisibility(bool isVisible)
    {
        isPanelVisible = isVisible;
        if (!isVisible)
            CloseSecondaryPopups();

        if (controlPanel != null)
            controlPanel.SetActive(isVisible);
        if (exitBtn != null)
            exitBtn.gameObject.SetActive(isVisible);
        if (topRightGroup != null)
            topRightGroup.SetActive(isVisible);

        RegisterUiTreeNodes();
    }

    public void TogglePanel() => SetPanelVisibility(!isPanelVisible);
    public void HidePanel() => SetPanelVisibility(false);

    public void OnPlayPauseButtonClicked()
    {
        CloseSecondaryPopups();
        playbackService?.TogglePlayPause();
    }

    private void OnAudioTrackBtnClicked()
    {
        if (audioTrackDropdown == null) return;
        bool show = ShouldShowTrackDropdown(audioTrackDropdown);
        LogTrackDropdownClick("audio", audioTrackDropdown, show);
        CloseSecondaryPopups(show ? audioTrackDropdown.gameObject : null);
        if (show)
            ShowDropdownAboveButton(audioTrackDropdown, equalizerBtn);
    }

    private void OnSubtitleBtnClicked()
    {
        if (subtitleTrackDropdown == null) return;
        bool show = ShouldShowTrackDropdown(subtitleTrackDropdown);
        LogTrackDropdownClick("subtitle", subtitleTrackDropdown, show);
        CloseSecondaryPopups(show ? subtitleTrackDropdown.gameObject : null);
        if (show)
            ShowDropdownAboveButton(subtitleTrackDropdown, subtitleBtn);
    }

    private void OnBrightnessBtnClicked()
    {
        ToggleSystemSlider(SystemSliderMode.Brightness, brightnessBtn, ReadScreenBrightnessNormalized());
    }

    private void OnVolumeBtnClicked()
    {
        ToggleSystemSlider(SystemSliderMode.Volume, volumeBtn, ReadSystemVolumeNormalized());
    }

    private void ToggleSystemSlider(SystemSliderMode mode, Button anchorButton, float normalizedValue)
    {
        if (anchorButton == null)
            return;

        EnsureSystemSliderPopup();
        if (systemSliderPopup == null || systemSlider == null)
            return;

        bool show = !IsSystemSliderOpen(mode);
        CloseSecondaryPopups(show ? systemSliderPopup : null);
        if (show)
            ShowSystemSliderAboveButton(anchorButton, mode, normalizedValue);
        else
            HideSystemSlider();
    }

    private void ShowSystemSliderAboveButton(Button anchorButton, SystemSliderMode mode, float normalizedValue)
    {
        EnsureSystemSliderPopup();
        if (systemSliderPopup == null || systemSlider == null || anchorButton == null)
            return;

        _activeSystemSliderMode = mode;
        _isUpdatingSystemSlider = true;
        systemSlider.SetValueWithoutNotify(Mathf.Clamp01(normalizedValue));
        _isUpdatingSystemSlider = false;

        systemSliderPopup.SetActive(true);
        BringPopupToFront(systemSliderPopup);
        Canvas.ForceUpdateCanvases();
        PositionPopupAboveButton(systemSliderPopup, anchorButton, true);
        Canvas.ForceUpdateCanvases();
        RegisterUiTreeNodes();
    }

    private void EnsureSystemSliderPopup()
    {
        if (systemSliderPopup != null && systemSlider != null)
            return;

        Transform parent = controlPanel != null && controlPanel.transform.parent != null
            ? controlPanel.transform.parent
            : transform;

        if (systemSliderPopupPrefab == null)
        {
            Debug.LogError("[VRUIManager] systemSliderPopupPrefab is not assigned.");
            return;
        }

        systemSliderPopup = Instantiate(systemSliderPopupPrefab, parent, false);
        systemSliderPopup.name = systemSliderPopupPrefab.name;
        systemSlider = systemSliderPopup.GetComponentInChildren<Slider>(true);
        if (systemSlider == null)
        {
            Debug.LogError("[VRUIManager] systemSliderPopupPrefab must contain a Slider.");
            Destroy(systemSliderPopup);
            systemSliderPopup = null;
            return;
        }

        systemSlider.onValueChanged.AddListener(OnSystemSliderValueChanged);
        systemSliderPopup.SetActive(false);
    }

    private void OnSystemSliderValueChanged(float value)
    {
        if (_isUpdatingSystemSlider)
            return;

        float normalized = Mathf.Clamp01(value);
        if (_activeSystemSliderMode == SystemSliderMode.Brightness)
            SetScreenBrightnessNormalized(normalized);
        else if (_activeSystemSliderMode == SystemSliderMode.Volume)
            SetSystemVolumeNormalized(normalized);
    }

    private bool IsSystemSliderOpen()
    {
        return systemSliderPopup != null && systemSliderPopup.activeSelf && _activeSystemSliderMode != SystemSliderMode.None;
    }

    private bool IsSystemSliderOpen(SystemSliderMode mode)
    {
        return IsSystemSliderOpen() && _activeSystemSliderMode == mode;
    }

    private Button ActiveSystemSliderButton()
    {
        if (_activeSystemSliderMode == SystemSliderMode.Brightness)
            return brightnessBtn;
        if (_activeSystemSliderMode == SystemSliderMode.Volume)
            return volumeBtn;
        return null;
    }

    private void HideSystemSlider()
    {
        _activeSystemSliderMode = SystemSliderMode.None;
        if (systemSliderPopup != null)
            systemSliderPopup.SetActive(false);
    }

    private void OnEyeBtnClicked()
    {
        CloseSecondaryPopups();
        EnsurePassthroughModeService();
        passthroughModeService?.Toggle();
        UpdateEyeButtonPassthroughVisual();
    }

    private void EnsurePassthroughModeService()
    {
        if (passthroughModeService != null) return;

        passthroughModeService = FindAnyObjectByType<PicoPassthroughModeService>();
        if (passthroughModeService == null)
            passthroughModeService = gameObject.AddComponent<PicoPassthroughModeService>();

        passthroughModeService.StateChanged -= OnPassthroughStateChanged;
        passthroughModeService.StateChanged += OnPassthroughStateChanged;
    }

    private void OnPassthroughStateChanged(bool enabled)
    {
        UpdateEyeButtonPassthroughVisual();
    }

    private void UpdateEyeButtonPassthroughVisual()
    {
        bool isEnabled = passthroughModeService != null && passthroughModeService.IsEnabled;
        bool isSupported = passthroughModeService == null || passthroughModeService.IsSupported;
        SetEyeButtonPassthroughVisual(isEnabled, isSupported);
    }

    private void SetEyeButtonPassthroughVisual(bool enabled, bool supported)
    {
        if (eyeBtn == null) return;

        eyeBtn.interactable = supported;
        Image image = eyeBtn.GetComponent<Image>();
        if (image == null) return;

        if (!supported)
        {
            image.color = PassthroughDisabledColor;
            return;
        }

        ApplyRuntimeButtonTheme(eyeBtn, image, enabled);
    }

    /// <summary>
    /// 打开或关闭设置按钮上方的 tab 设置面板。
    /// </summary>
    private void OnSettingsBtnClicked()
    {
        EnsureSettingsMenuController();
        if (settingsMenu == null || settingsMenuController == null) return;

        bool show = !settingsMenuController.IsOpen;
        CloseSecondaryPopups(show ? settingsMenu : null);
        if (show)
        {
            settingsMenu.SetActive(true);
            settingsMenuController.Show(SettingsMenuController.SettingsTab.Playback);
            BringPopupToFront(settingsMenu);
            Canvas.ForceUpdateCanvases();
            PositionPopupAboveControlPanel(settingsMenu);
            Canvas.ForceUpdateCanvases();
        }
        else
        {
            HideSettingsMenu();
        }

        RegisterUiTreeNodes();
    }

    private void OnGeometryBtnClicked()
    {
        EnsureGeometryMenu();
        if (geometryMenu == null) return;

        bool show = !geometryMenu.activeSelf;
        CloseSecondaryPopups(show ? geometryMenu : null);
        if (show)
        {
            geometryMenu.SetActive(true);
            BringPopupToFront(geometryMenu);
            UpdateGeometrySelectionFromPlayback();
            UpdateGeometrySelectionHighlights();
            Canvas.ForceUpdateCanvases();
            PositionPopupAboveButton(geometryMenu, threeDBtn);
            Canvas.ForceUpdateCanvases();
        }

        RegisterUiTreeNodes();
    }

    private void EnsureSettingsMenuController()
    {
        if (settingsBtn == null)
            return;

        bool created = false;
        if (settingsMenu == null)
        {
            Transform parent = controlPanel != null && controlPanel.transform.parent != null
                ? controlPanel.transform.parent
                : settingsBtn.transform.parent;
            var menu = new GameObject("SettingsMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            menu.transform.SetParent(parent, false);
            settingsMenu = menu;
            created = true;
        }

        settingsMenuController = settingsMenu.GetComponent<SettingsMenuController>();
        if (settingsMenuController == null)
            settingsMenuController = settingsMenu.AddComponent<SettingsMenuController>();

        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        settingsMenuController.Bind(playbackService, FindAnyObjectByType<XRVLC.XR.ShortcutManager>());

        if (created)
            settingsMenu.SetActive(false);
    }

    private void HideSettingsMenu()
    {
        if (settingsMenuController != null)
        {
            settingsMenuController.Hide();
            return;
        }

        if (settingsMenu != null)
            settingsMenu.SetActive(false);
    }

    private void EnsureGeometryMenu()
    {
        if (geometryMenu != null || threeDBtn == null) return;

        Transform parent = controlPanel != null && controlPanel.transform.parent != null
            ? controlPanel.transform.parent
            : threeDBtn.transform.parent;
        var menu = new GameObject("GeometryMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        menu.transform.SetParent(parent, false);
        geometryMenu = menu;

        var layoutElement = menu.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        var rect = menu.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600f, geometryMenuFlatHeight);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        PositionPopupAboveButton(menu, threeDBtn);

        var image = menu.GetComponent<Image>();
        image.color = SecondaryPanelColor;
        image.raycastTarget = true;

        var layout = menu.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateGeometrySectionLabel(menu.transform, "投影模式");
        Transform projectionRow = CreateGeometryRow(menu.transform, "ProjectionRow");
        _projection180Button = CreateGeometryOptionButton(projectionRow, "180全景", () => SetGeometryProjection(XRVLC.VideoProjection.Sphere180));
        _projection360Button = CreateGeometryOptionButton(projectionRow, "360全景", () => SetGeometryProjection(XRVLC.VideoProjection.Sphere360));
        _projectionFlatButton = CreateGeometryOptionButton(projectionRow, "平面", () => SetGeometryProjection(XRVLC.VideoProjection.Flat));

        CreateGeometrySectionLabel(menu.transform, "3D 格式");
        Transform stereoRow = CreateGeometryRow(menu.transform, "StereoRow");
        _stereoMonoButton = CreateGeometryOptionButton(stereoRow, "平面左右眼划分", () => SetGeometryStereo(XRVLC.StereoMode.Mono));
        _stereoTopBottomButton = CreateGeometryOptionButton(stereoRow, "上下3D", () => SetGeometryStereo(XRVLC.StereoMode.TopBottom));
        _stereoLeftRightButton = CreateGeometryOptionButton(stereoRow, "左右3D", () => SetGeometryStereo(XRVLC.StereoMode.LeftRight));

        _curveSectionLabel = CreateGeometrySectionLabel(menu.transform, "平面弧度").gameObject;
        _curveRow = CreateGeometryRow(menu.transform, "CurveRow");
        _curveNoneButton = CreateGeometryOptionButton(_curveRow, "无曲面", () => SetFlatCurveMode(XRVLC.FlatVideoCurveMode.None));
        _curveSmallButton = CreateGeometryOptionButton(_curveRow, "小曲面", () => SetFlatCurveMode(XRVLC.FlatVideoCurveMode.Small));
        _curveLargeButton = CreateGeometryOptionButton(_curveRow, "大曲面", () => SetFlatCurveMode(XRVLC.FlatVideoCurveMode.Large));

        UpdateGeometrySelectionHighlights();
        geometryMenu.SetActive(false);
        RegisterUiTreeNodes();
    }

    private TextMeshProUGUI CreateGeometrySectionLabel(Transform parent, string label)
    {
        TextMeshProUGUI text = CreateText(parent, label, GeometryMenuFontSize, TextAlignmentOptions.Left);
        RectTransform rect = text.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
        }

        LayoutElement element = text.gameObject.GetComponent<LayoutElement>();
        if (element == null)
            element = text.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 28f;
        element.flexibleHeight = 0f;
        return text;
    }

    private Transform CreateGeometryRow(Transform parent, string rowName)
    {
        var row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var element = row.GetComponent<LayoutElement>();
        element.preferredHeight = 54f;
        return row.transform;
    }

    private Button CreateGeometryOptionButton(Transform parent, string label, System.Action onClick)
    {
        var item = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        item.transform.SetParent(parent, false);

        var element = item.GetComponent<LayoutElement>();
        element.preferredWidth = 184f;
        element.preferredHeight = 50f;
        element.flexibleWidth = 1f;

        var image = item.GetComponent<Image>();
        image.color = TransparentListColor;

        TextMeshProUGUI text = CreateText(item.transform, label, GeometryMenuFontSize, TextAlignmentOptions.Center);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        Button button = item.GetComponent<Button>();
        ApplyRuntimeButtonTheme(button, image, false);
        if (onClick != null)
            button.onClick.AddListener(() => onClick());
        return button;
    }

    private void SetGeometryProjection(XRVLC.VideoProjection projection)
    {
        _geometryProjection = projection;
        ApplyGeometrySelection();
        UpdateGeometrySelectionHighlights();
        RepositionGeometryMenuIfOpen();
    }

    private void SetGeometryStereo(XRVLC.StereoMode stereo)
    {
        _geometryStereo = stereo;
        ApplyGeometrySelection();
        UpdateGeometrySelectionHighlights();
    }

    private void SetFlatCurveMode(XRVLC.FlatVideoCurveMode curveMode)
    {
        _flatCurveMode = curveMode;
        ApplyGeometrySelection();
        UpdateGeometrySelectionHighlights();
        RepositionGeometryMenuIfOpen();
    }

    private void UpdateGeometrySelectionHighlights()
    {
        SetGeometryOptionSelected(_projection180Button, _geometryProjection == XRVLC.VideoProjection.Sphere180);
        SetGeometryOptionSelected(_projection360Button, _geometryProjection == XRVLC.VideoProjection.Sphere360);
        SetGeometryOptionSelected(_projectionFlatButton, _geometryProjection == XRVLC.VideoProjection.Flat);

        SetGeometryOptionSelected(_stereoMonoButton, _geometryStereo == XRVLC.StereoMode.Mono);
        SetGeometryOptionSelected(_stereoTopBottomButton, _geometryStereo == XRVLC.StereoMode.TopBottom);
        SetGeometryOptionSelected(_stereoLeftRightButton, _geometryStereo == XRVLC.StereoMode.LeftRight);

        SetGeometryOptionSelected(_curveNoneButton, _flatCurveMode == XRVLC.FlatVideoCurveMode.None);
        SetGeometryOptionSelected(_curveSmallButton, _flatCurveMode == XRVLC.FlatVideoCurveMode.Small);
        SetGeometryOptionSelected(_curveLargeButton, _flatCurveMode == XRVLC.FlatVideoCurveMode.Large);
        UpdateGeometryCascadeVisibility();
    }

    private void UpdateGeometryCascadeVisibility()
    {
        bool showFlatCurveOptions = _geometryProjection == XRVLC.VideoProjection.Flat;

        if (_curveSectionLabel != null)
            _curveSectionLabel.SetActive(showFlatCurveOptions);
        if (_curveRow != null)
            _curveRow.gameObject.SetActive(showFlatCurveOptions);

        RectTransform rect = geometryMenu != null ? geometryMenu.GetComponent<RectTransform>() : null;
        if (rect != null)
        {
            float height = showFlatCurveOptions ? geometryMenuFlatHeight : geometryMenuPanoramicHeight;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }
    }

    private void RepositionGeometryMenuIfOpen()
    {
        if (geometryMenu == null || !geometryMenu.activeSelf || threeDBtn == null)
            return;

        Canvas.ForceUpdateCanvases();
        PositionPopupAboveButton(geometryMenu, threeDBtn);
        Canvas.ForceUpdateCanvases();
    }

    private void SetGeometryOptionSelected(Button button, bool selected)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        ApplyRuntimeButtonTheme(button, image, selected);
    }

    private void ApplyRuntimeButtonTheme(Button button, Image image, bool selected)
    {
        if (button == null) return;

        if (buttonTheme != null)
        {
            buttonTheme.ApplyTo(button, image, selected, true);
            return;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = selected ? SelectedGeometryOptionColor : TransparentListColor;
        colors.highlightedColor = selected ? SelectedGeometryOptionHoverColor : HoverListColor;
        colors.selectedColor = selected ? SelectedGeometryOptionHoverColor : HoverListColor;
        colors.pressedColor = PressedListColor;
        colors.disabledColor = TransparentListColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = colors;

        if (image != null)
        {
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;
        }
    }

    private void ApplyGeometrySelection()
    {
        XRVLC.VideoProjection projection = _geometryProjection;
        XRVLC.FlatVideoCurveMode curveMode = XRVLC.FlatVideoCurveMode.None;

        if (_geometryProjection == XRVLC.VideoProjection.Flat)
        {
            curveMode = _flatCurveMode;
            if (curveMode != XRVLC.FlatVideoCurveMode.None)
                projection = XRVLC.VideoProjection.Cylinder;
        }

        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        playbackService?.SetManualVideoGeometry(projection, _geometryStereo, curveMode);
    }

    private void UpdateGeometrySelectionFromPlayback()
    {
        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        if (playbackService == null)
            return;

        SetLocalGeometrySelection(playbackService.CurrentGeometrySelection);
    }

    private void SetLocalGeometrySelection(XRVLC.VideoGeometrySelection selection)
    {
        if (selection.Projection == XRVLC.VideoProjection.Cylinder)
        {
            _geometryProjection = XRVLC.VideoProjection.Flat;
            _flatCurveMode = selection.CurveMode;
        }
        else
        {
            _geometryProjection = selection.Projection;
            _flatCurveMode = selection.Projection == XRVLC.VideoProjection.Flat
                ? selection.CurveMode
                : XRVLC.FlatVideoCurveMode.None;
        }

        _geometryStereo = selection.Stereo;
        UpdateGeometrySelectionHighlights();
    }

    private void SetLocalGeometrySelectionFromMedia(XRVLC.Media.MediaWrapper media)
    {
        var (projection, stereo) = XRVLC.ProjectionDetector.Detect(media);
        SetLocalGeometrySelection(new XRVLC.VideoGeometrySelection(projection, stereo, XRVLC.FlatVideoCurveMode.None));
    }

    private void CloseSecondaryPopups(GameObject except = null)
    {
        if (IsAnyTrackDropdownOpen())
        {
            Debug.Log(
                $"[VRUIManager][TrackDropdown] closeSecondary except={DescribeUiTarget(except)} " +
                $"audioOpen={IsTrackDropdownOpen(audioTrackDropdown)} " +
                $"subtitleOpen={IsTrackDropdownOpen(subtitleTrackDropdown)}");
        }

        CloseDropdownIfNotExcept(audioTrackDropdown, except);
        CloseDropdownIfNotExcept(subtitleTrackDropdown, except);
        if (settingsMenu != except)
            HideSettingsMenu();
        SetActiveIfNotExcept(geometryMenu, except, false);
        if (systemSliderPopup != except)
            HideSystemSlider();

        if (playlistPanel != null && playlistPanel.panelRoot != except)
            playlistPanel.Hide();

        if (shortcutConfigPanel != null && shortcutConfigPanel.gameObject != except)
            shortcutConfigPanel.gameObject.SetActive(false);

        RegisterUiTreeNodes();
    }

    private void ShowDropdownAboveButton(XrDropdown dropdown, Button anchorButton)
    {
        if (dropdown == null || anchorButton == null) return;
        if (dropdown == audioTrackDropdown || dropdown == subtitleTrackDropdown)
            ConfigureSecondaryDropdown(dropdown, false);
        if (!dropdown.interactable)
        {
            Debug.Log(
                $"[VRUIManager][TrackDropdown] showSkip interactable=False " +
                $"options={dropdown.Count} " +
                $"rootActive={dropdown.gameObject.activeSelf}");
            RegisterUiTreeNodes();
            return;
        }

        bool needsDeferredShow = !dropdown.gameObject.activeInHierarchy;
        dropdown.gameObject.SetActive(true);
        BringPopupToFront(dropdown.gameObject);
        Canvas.ForceUpdateCanvases();
        PositionPopupAboveButton(dropdown.gameObject, anchorButton, true);
        Canvas.ForceUpdateCanvases();

        if (needsDeferredShow)
        {
            if (_pendingTrackDropdownShow != null)
                StopCoroutine(_pendingTrackDropdownShow);
            _pendingTrackDropdownShow = StartCoroutine(ShowDropdownAfterActivation(dropdown, anchorButton));
            RegisterUiTreeNodes();
            return;
        }

        ShowPreparedDropdown(dropdown, anchorButton);

        RegisterUiTreeNodes();
    }

    private System.Collections.IEnumerator ShowDropdownAfterActivation(XrDropdown dropdown, Button anchorButton)
    {
        yield return null;
        _pendingTrackDropdownShow = null;

        if (dropdown == null || anchorButton == null || !dropdown.gameObject.activeInHierarchy || !dropdown.interactable)
            yield break;

        ShowPreparedDropdown(dropdown, anchorButton);
        RegisterUiTreeNodes();
    }

    private void ShowPreparedDropdown(XrDropdown dropdown, Button anchorButton)
    {
        dropdown.Show();
        Canvas.ForceUpdateCanvases();
        BringDropdownListToFront(dropdown);
        Canvas.ForceUpdateCanvases();
        PositionOpenDropdownListAboveButton(dropdown, anchorButton);
        Canvas.ForceUpdateCanvases();
        LogTrackDropdownGeometry(dropdown, anchorButton);
        Debug.Log(
            $"[VRUIManager][TrackDropdown] showComplete listActive={dropdown.ActiveList != null} " +
            $"rootActive={dropdown.gameObject.activeSelf}");
    }

    private void CloseDropdownIfNotExcept(XrDropdown dropdown, GameObject except)
    {
        if (dropdown == null || dropdown.gameObject == except) return;

        CloseTrackDropdown(dropdown);
    }

    private static bool CloseTrackDropdown(XrDropdown dropdown)
    {
        if (dropdown == null) return false;

        bool wasOpen = dropdown.gameObject.activeSelf || dropdown.IsOpen;
        bool hadList = ActiveDropdownList(dropdown) != null;
        dropdown.CloseImmediately();
        dropdown.gameObject.SetActive(false);
        Debug.Log(
            $"[VRUIManager][TrackDropdown] close wasOpen={wasOpen} hadList={hadList} " +
            $"rootActive={dropdown.gameObject.activeSelf}");
        return wasOpen;
    }

    private static void LogTrackDropdownClick(string dropdownName, XrDropdown dropdown, bool shouldShow)
    {
        GameObject activeList = ActiveDropdownList(dropdown);
        int optionCount = dropdown != null ? dropdown.Count : 0;
        Debug.Log(
            $"[VRUIManager][TrackDropdown] {dropdownName} click shouldShow={shouldShow} " +
            $"rootActive={dropdown != null && dropdown.gameObject.activeSelf} " +
            $"rootInHierarchy={dropdown != null && dropdown.gameObject.activeInHierarchy} " +
            $"listActive={activeList != null} interactable={dropdown != null && dropdown.interactable} " +
            $"options={optionCount} listTarget={DescribeUiTarget(activeList)}");
    }

    private static void LogTrackDropdownGeometry(XrDropdown dropdown, Button anchorButton)
    {
        RectTransform rootRect = dropdown != null ? dropdown.GetComponent<RectTransform>() : null;
        RectTransform anchorRect = anchorButton != null ? anchorButton.GetComponent<RectTransform>() : null;
        GameObject list = ActiveDropdownList(dropdown);

        Debug.Log(
            $"[VRUIManager][TrackDropdown] geometry root={DescribeUiTarget(dropdown != null ? dropdown.gameObject : null)} " +
            $"anchor={DescribeUiTarget(anchorButton != null ? anchorButton.gameObject : null)} " +
            $"rootRect={DescribeRect(rootRect)} anchorRect={DescribeRect(anchorRect)} " +
            $"rootOverlapsAnchor={RectTransformsOverlap(rootRect, anchorRect)} listActive={list != null}");

        RectTransform listRect = list != null ? list.GetComponent<RectTransform>() : null;
        Debug.Log(
            $"[VRUIManager][TrackDropdown] listGeometry " +
            $"list={DescribeUiTarget(list)} listRect={DescribeRect(listRect)} anchorRect={DescribeRect(anchorRect)} " +
            $"listOverlapsAnchor={RectTransformsOverlap(listRect, anchorRect)}");
    }

    private static string DescribeUiTarget(GameObject target)
    {
        if (target == null) return "<null>";

        return $"{target.name} path={BuildTransformPath(target.transform)} " +
            $"activeSelf={target.activeSelf} activeInHierarchy={target.activeInHierarchy}";
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform == null) return "<null>";

        string path = transform.name;
        Transform current = transform.parent;
        int depth = 0;
        while (current != null && depth < 10)
        {
            path = current.name + "/" + path;
            current = current.parent;
            depth++;
        }

        return current != null ? ".../" + path : path;
    }

    private static string DescribeRect(RectTransform rect)
    {
        if (rect == null) return "<null>";

        GetWorldBounds(rect, out Vector3 min, out Vector3 max);
        return $"min=({min.x:F1},{min.y:F1},{min.z:F1}) max=({max.x:F1},{max.y:F1},{max.z:F1}) " +
            $"size=({rect.rect.width:F1},{rect.rect.height:F1}) anchored=({rect.anchoredPosition.x:F1},{rect.anchoredPosition.y:F1})";
    }

    private static bool RectTransformsOverlap(RectTransform first, RectTransform second)
    {
        if (first == null || second == null) return false;

        GetWorldBounds(first, out Vector3 firstMin, out Vector3 firstMax);
        GetWorldBounds(second, out Vector3 secondMin, out Vector3 secondMax);
        return firstMin.x < secondMax.x
            && firstMax.x > secondMin.x
            && firstMin.y < secondMax.y
            && firstMax.y > secondMin.y;
    }

    private static void GetWorldBounds(RectTransform rect, out Vector3 min, out Vector3 max)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        min = corners[0];
        max = corners[0];
        for (int i = 1; i < corners.Length; i++)
        {
            min = Vector3.Min(min, corners[i]);
            max = Vector3.Max(max, corners[i]);
        }
    }

    private static void SetActiveIfNotExcept(GameObject target, GameObject except, bool active)
    {
        if (target != null && target != except)
            target.SetActive(active);
    }

    private void ConfigureSecondaryDropdown(XrDropdown dropdown, bool hideAfterConfigure)
    {
        if (dropdown == null) return;

        dropdown.showCaption = false;
        dropdown.closeOnSelect = true;
        dropdown.width = trackDropdownWidth;
        dropdown.maxVisibleItems = Mathf.Max(1, trackDropdownMaxVisibleItems);
        dropdown.fontSize = trackDropdownFontSize;
        dropdown.horizontalPadding = trackDropdownHorizontalPadding;
        dropdown.verticalPadding = trackDropdownVerticalPadding;
        dropdown.ApplyConfiguredLayout();

        RectTransform rect = dropdown.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, trackDropdownWidth);
            rect.sizeDelta = new Vector2(trackDropdownWidth, rect.sizeDelta.y);
        }

        LayoutElement layout = dropdown.GetComponent<LayoutElement>();
        if (layout == null)
            layout = dropdown.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = trackDropdownWidth;
        layout.preferredWidth = trackDropdownWidth;
        layout.flexibleWidth = 0f;
        if (hideAfterConfigure)
            dropdown.gameObject.SetActive(false);
    }

    private static void InitializeTrackDropdownDefault(XrDropdown dropdown, string placeholder)
    {
        if (dropdown == null) return;

        dropdown.SetPlaceholder(placeholder);
    }

    private void PositionPopupAboveButton(GameObject popup, Button anchorButton)
    {
        PositionPopupAboveButton(popup, anchorButton, false);
    }

    private void PositionPopupAboveControlPanel(GameObject popup)
    {
        if (popup == null || controlPanel == null)
        {
            PositionPopupAboveButton(popup, settingsBtn);
            return;
        }

        RectTransform popupRect = popup.GetComponent<RectTransform>();
        RectTransform controlPanelRect = controlPanel.GetComponent<RectTransform>();
        if (popupRect == null || controlPanelRect == null)
        {
            PositionPopupAboveButton(popup, settingsBtn);
            return;
        }

        RectTransform parentRect = popupRect.parent as RectTransform;
        if (parentRect == null)
        {
            PositionPopupAboveButton(popup, settingsBtn);
            return;
        }

        Vector2 popupSize = ResolveRectSize(popupRect, parentRect);
        Vector3[] controlPanelCorners = new Vector3[4];
        controlPanelRect.GetWorldCorners(controlPanelCorners);

        Vector3 panelTopLeft = parentRect.InverseTransformPoint(controlPanelCorners[1]);
        Vector3 panelTopRight = parentRect.InverseTransformPoint(controlPanelCorners[2]);
        float panelTopY = Mathf.Max(panelTopLeft.y, panelTopRight.y);
        float panelCenterX = (panelTopLeft.x + panelTopRight.x) * 0.5f;

        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.zero;
        popupRect.pivot = Vector2.zero;
        popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, popupSize.x);
        popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, popupSize.y);
        Vector2 anchorOrigin = new Vector2(parentRect.rect.xMin, parentRect.rect.yMin);
        popupRect.anchoredPosition = new Vector2(
            panelCenterX - popupSize.x * 0.5f - anchorOrigin.x,
            panelTopY - anchorOrigin.y + DropdownPopupGap);
    }

    private void PositionPopupAboveButton(GameObject popup, Button anchorButton, bool useVisualAnchorY)
    {
        if (popup == null || anchorButton == null) return;

        RectTransform popupRect = popup.GetComponent<RectTransform>();
        RectTransform buttonRect = anchorButton.GetComponent<RectTransform>();
        if (popupRect == null || buttonRect == null) return;

        RectTransform parentRect = popupRect.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 popupSize = ResolveRectSize(popupRect, parentRect);
        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        Vector3 topLeft = parentRect.InverseTransformPoint(corners[1]);
        if (useVisualAnchorY && TryResolveButtonVisualTop(anchorButton, parentRect, out float visualTopY))
            topLeft.y = visualTopY;

        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.zero;
        popupRect.pivot = Vector2.zero;
        popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, popupSize.x);
        popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, popupSize.y);
        Vector2 anchorOrigin = new Vector2(parentRect.rect.xMin, parentRect.rect.yMin);
        popupRect.anchoredPosition = new Vector2(
            topLeft.x - anchorOrigin.x,
            topLeft.y - anchorOrigin.y + DropdownPopupGap);
    }

    private static bool TryResolveButtonVisualTop(Button anchorButton, RectTransform targetParent, out float topY)
    {
        topY = 0f;
        if (anchorButton == null || targetParent == null)
            return false;

        Transform icon = anchorButton.transform.Find("Icon");
        RectTransform iconRect = icon as RectTransform;
        if (iconRect == null || !iconRect.gameObject.activeInHierarchy)
            return false;

        Vector3[] iconCorners = new Vector3[4];
        iconRect.GetWorldCorners(iconCorners);
        topY = targetParent.InverseTransformPoint(iconCorners[1]).y;
        return true;
    }

    private static Vector2 ResolveRectSize(RectTransform rect, RectTransform parentRect)
    {
        Vector2 size = rect.rect.size;
        if (size.x <= 0f && parentRect != null)
            size.x = parentRect.rect.width * (rect.anchorMax.x - rect.anchorMin.x) + rect.sizeDelta.x;
        if (size.y <= 0f && parentRect != null)
            size.y = parentRect.rect.height * (rect.anchorMax.y - rect.anchorMin.y) + rect.sizeDelta.y;
        if (size.x <= 0f)
            size.x = rect.sizeDelta.x;
        if (size.y <= 0f)
            size.y = rect.sizeDelta.y;

        return new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
    }

    private static void BringPopupToFront(GameObject popup)
    {
        if (popup == null) return;

        popup.transform.SetAsLastSibling();

        Canvas canvas = popup.GetComponent<Canvas>();
        if (canvas == null)
            canvas = popup.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = PopupSortingOrder;

        if (popup.GetComponent<GraphicRaycaster>() == null)
            popup.AddComponent<GraphicRaycaster>();
        if (popup.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            popup.AddComponent<TrackedDeviceGraphicRaycaster>();
    }

    private static void BringDropdownListToFront(XrDropdown dropdown)
    {
        if (dropdown == null) return;

        if (dropdown.ActiveList != null)
            BringPopupToFront(dropdown.ActiveList);
    }

    private void PositionOpenDropdownListAboveButton(XrDropdown dropdown, Button anchorButton)
    {
        if (dropdown == null || anchorButton == null || dropdown.ActiveList == null) return;

        PositionPopupAboveButton(dropdown.ActiveList, anchorButton, true);
    }

    /// <summary>
    /// 创建 TextMeshPro 文本节点。
    /// </summary>
    private TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(text + "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        return label;
    }

    private void ApplyIconSprites()
    {
        SetButtonIcon(playBtn, "play", PrimaryPlaybackIconSize);
        SetButtonIcon(previousBtn, "previous", AdjacentPlaybackIconSize);
        SetButtonIcon(nextBtn, "next", AdjacentPlaybackIconSize);
        SetButtonIcon(playlistToggleBtn, "playlist", 32f);
        SetButtonIcon(exitBtn, "exit", 32f);
        SetButtonIcon(lockBtn, "lock", 30f);
        SetButtonIcon(brightnessBtn, "brightness", 32f);
        SetButtonIcon(volumeBtn, "volume", 32f);
        SetButtonIcon(equalizerBtn, "equalizer", 32f);
        SetButtonIcon(subtitleBtn, "subtitle", 32f);
        SetButtonIcon(eyeBtn, "view", 32f);
        SetButtonIcon(threeDBtn, "stereo3d", 32f);
        SetButtonIcon(settingsBtn, "settings", 32f);
    }

    private void ConfigureUiCanvasClarity()
    {
        Canvas canvas = controlPanel != null
            ? controlPanel.GetComponentInParent<Canvas>(true)
            : GetComponentInParent<Canvas>(true);
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            return;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            return;

        scaler.dynamicPixelsPerUnit = Mathf.Max(scaler.dynamicPixelsPerUnit, MinWorldCanvasDynamicPixelsPerUnit);
    }

    private void UpdatePlayButtonIcon(XRVLC.Media.PlayerStatus status)
    {
        string iconName = status == XRVLC.Media.PlayerStatus.Playing ? "pause" : "play";
        SetButtonIcon(playBtn, iconName, PrimaryPlaybackIconSize);
    }

    private Image SetButtonIcon(Button button, string iconName, float size)
    {
        if (button == null) return null;

        Sprite sprite = LoadIcon(iconName);
        if (sprite == null) return null;

        XrThemedButton themedButton = button.GetComponent<XrThemedButton>();
        if (themedButton != null)
        {
            themedButton.SetIconName(iconName);
            return themedButton.icon;
        }

        Image icon = CreateIconImage(button.transform, sprite, size);
        HideLegacyButtonText(button.transform, icon.transform);
        return icon;
    }

    private Sprite LoadIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;
        Sprite sprite = Resources.Load<Sprite>(IconResourcePath + iconName);
        if (sprite == null)
            Debug.LogWarning($"[VRUIManager] IconPark 图标未找到: {iconName}");
        return sprite;
    }

    private Sprite LoadIconWithFallback(string iconName, string fallbackIconName)
    {
        Sprite sprite = LoadIcon(iconName);
        if (sprite == null && iconName != fallbackIconName)
            sprite = LoadIcon(fallbackIconName);
        return sprite;
    }

    private Image CreateIconImage(Transform parent, Sprite sprite, float size)
    {
        if (sprite == null || parent == null) return null;

        Transform existing = parent.Find("Icon");
        GameObject iconObject = existing != null
            ? existing.gameObject
            : new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));

        iconObject.transform.SetParent(parent, false);
        iconObject.transform.SetAsLastSibling();

        var icon = iconObject.GetComponent<Image>();
        icon.sprite = sprite;
        icon.color = IconTint;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);

        var layout = iconObject.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = size;
            layout.preferredHeight = size;
        }

        return icon;
    }

    private static void HideLegacyButtonText(Transform buttonTransform, Transform iconTransform)
    {
        if (buttonTransform == null) return;

        TextMeshProUGUI[] labels = buttonTransform.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label.transform == iconTransform) continue;
            label.enabled = false;
        }
    }

    private void OnExitBtnClicked()
    {
        CloseSecondaryPopups();
        if (VlcLibraryLauncher.Instance != null)
            VlcLibraryLauncher.Instance.OpenVLCMediaLibrary();
        else
            Debug.LogError("未找到 VlcLibraryLauncher 实例，无法打开媒体库！");
    }

    private void UpdateMediaInfo(XRVLC.Media.MediaWrapper media, int index)
    {
        if (media == null) return;

        if (titleText != null)
        {
            titleText.enabled = true;
            titleText.text = GetDisplayTitle(media);
        }

        SetLocalGeometrySelectionFromMedia(media);
    }

    private static string GetDisplayTitle(XRVLC.Media.MediaWrapper media)
    {
        if (media == null) return "未知视频";
        if (!string.IsNullOrWhiteSpace(media.Title))
            return media.Title;
        if (string.IsNullOrWhiteSpace(media.Uri))
            return "未知视频";

        string value = media.Uri;
        int queryIndex = value.IndexOf('?');
        if (queryIndex >= 0)
            value = value.Substring(0, queryIndex);

        int slashIndex = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        if (slashIndex >= 0 && slashIndex + 1 < value.Length)
            value = value.Substring(slashIndex + 1);

        if (string.IsNullOrWhiteSpace(value))
            return "未知视频";

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private void UpdateTime(long timeMs, long totalTimeMs)
    {
        if (currentTimeText != null)
            currentTimeText.text = FormatTime(timeMs);
        if (totalTimeText != null)
            totalTimeText.text = FormatTime(totalTimeMs);

        if (progressSlider != null && totalTimeMs > 0)
        {
            _isUpdatingSlider = true;
            progressSlider.value = (float)timeMs / totalTimeMs;
            _isUpdatingSlider = false;
        }
    }

    private string FormatTime(long ms)
    {
        TimeSpan t = TimeSpan.FromMilliseconds(ms);
        if (t.Hours > 0)
            return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
        return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
    }

    public void OnSliderValueChanged(float val)
    {
        if (_isUpdatingSlider) return;
        playbackService?.SeekToPosition(val);
    }

    public void OnSpeedBtnClicked()
    {
        CloseSecondaryPopups();
        currentSpeedIndex = (currentSpeedIndex + 1) % speedOptions.Length;
        playbackService?.SetPlaybackRate(speedOptions[currentSpeedIndex]);
        UpdateSpeedBtnText();
    }

    private void UpdateSpeedBtnText()
    {
        if (speedBtnText != null)
            speedBtnText.text = $"{speedOptions[currentSpeedIndex]}x";
    }

    private void EnsureRuntimeTextVisible()
    {
        ConfigureSystemStatusLayout();
        EnsureBatteryIcon();

        if (titleText != null) titleText.enabled = true;
        if (systemTimeText != null) systemTimeText.enabled = true;
        if (batteryText != null) batteryText.enabled = true;
        if (currentTimeText != null) currentTimeText.enabled = true;
        if (totalTimeText != null) totalTimeText.enabled = true;
        if (speedBtnText != null) speedBtnText.enabled = true;
    }

    private void ConfigureSystemStatusLayout()
    {
        ConfigureFixedSystemStatusText(systemTimeText, SystemTimeTextWidth);
        ConfigureFixedSystemStatusText(batteryText, BatteryStatusTextWidth);
        EnsureBatteryIconParentLayout();
    }

    private static void ConfigureFixedSystemStatusText(TextMeshProUGUI text, float width)
    {
        if (text == null)
            return;

        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = Vector4.zero;
        text.fontSize = SystemStatusFontSize;
        text.fontSizeMin = SystemStatusFontSize;
        text.fontSizeMax = SystemStatusFontSize;

        RectTransform rect = text.GetComponent<RectTransform>();
        if (rect != null)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        LayoutElement layout = text.GetComponent<LayoutElement>();
        if (layout == null)
            layout = text.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
    }

    private void EnsureBatteryIconParentLayout()
    {
        if (batteryText == null || batteryText.transform.parent == null)
            return;

        HorizontalLayoutGroup layout = batteryText.transform.parent.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            return;

        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleRight;
    }

    private void EnsureBatteryIcon()
    {
        if (batteryText == null)
            return;

        Transform parent = batteryText.transform.parent;
        if (parent == null)
            return;

        if (batteryIcon == null)
        {
            Transform existing = parent.Find("BatteryIcon");
            if (existing != null)
                batteryIcon = existing.GetComponent<Image>();
        }

        if (batteryIcon == null)
        {
            var iconObject = new GameObject("BatteryIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(parent, false);
            iconObject.transform.SetSiblingIndex(batteryText.transform.GetSiblingIndex());
            batteryIcon = iconObject.GetComponent<Image>();
        }

        SetBatteryIconSprite(-1);
        batteryIcon.gameObject.SetActive(true);
        batteryIcon.enabled = true;
        batteryIcon.color = IconTint;
        batteryIcon.preserveAspect = true;
        batteryIcon.raycastTarget = false;

        RectTransform rect = batteryIcon.GetComponent<RectTransform>();
        if (rect != null)
        {
            float size = Mathf.Max(1f, batteryIconSize);
            rect.sizeDelta = new Vector2(size, size);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
        }

        LayoutElement layout = batteryIcon.GetComponent<LayoutElement>();
        if (layout != null)
        {
            float size = Mathf.Max(1f, batteryIconSize);
            layout.minWidth = size;
            layout.minHeight = size;
            layout.preferredWidth = size;
            layout.preferredHeight = size;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
        }
    }

    private void UpdateSystemStatus()
    {
        if (systemTimeText != null)
            systemTimeText.text = DateTime.Now.ToString("HH:mm");

        if (Time.unscaledTime < _nextSystemStatusRefreshTime)
            return;

        _nextSystemStatusRefreshTime = Time.unscaledTime + SystemStatusRefreshInterval;
        if (batteryText == null)
            return;

        int batteryPercent = ReadBatteryPercent();
        batteryText.text = batteryPercent >= 0 ? $"{batteryPercent}%" : "--%";
        SetBatteryIconSprite(batteryPercent);
    }

    private void SetBatteryIconSprite(int batteryPercent)
    {
        if (batteryIcon == null)
            return;

        string iconName = ResolveBatteryIconName(batteryPercent);
        Sprite sprite = LoadIconWithFallback(iconName, BatteryUnknownIcon);
        if (sprite != null)
            batteryIcon.sprite = sprite;
    }

    private static string ResolveBatteryIconName(int batteryPercent)
    {
        if (batteryPercent < 0)
            return BatteryUnknownIcon;
        if (batteryPercent <= 10)
            return BatteryEmptyIcon;
        if (batteryPercent <= 35)
            return BatteryLowIcon;
        if (batteryPercent <= 70)
            return BatteryMediumIcon;
        return BatteryFullIcon;
    }

    private static int ReadBatteryPercent()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaObject activity = GetUnityActivity())
            using (AndroidJavaObject filter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
            using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("registerReceiver", (AndroidJavaObject)null, filter))
            {
                if (intent == null)
                    return -1;

                int level = intent.Call<int>("getIntExtra", "level", -1);
                int scale = intent.Call<int>("getIntExtra", "scale", -1);
                if (level < 0 || scale <= 0)
                    return -1;

                return Mathf.Clamp(Mathf.RoundToInt(level * 100f / scale), 0, 100);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VRUIManager] 读取系统电量失败: {e.Message}");
            return -1;
        }
#else
        return -1;
#endif
    }

    private float ReadSystemVolumeNormalized()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaObject activity = GetUnityActivity())
            using (AndroidJavaClass contextClass = new AndroidJavaClass("android.content.Context"))
            using (AndroidJavaObject audioManager = activity.Call<AndroidJavaObject>(
                       "getSystemService",
                       contextClass.GetStatic<string>("AUDIO_SERVICE")))
            {
                int maxVolume = Mathf.Max(1, audioManager.Call<int>("getStreamMaxVolume", AndroidStreamMusic));
                int currentVolume = audioManager.Call<int>("getStreamVolume", AndroidStreamMusic);
                _simulatedVolume = Mathf.Clamp01((float)currentVolume / maxVolume);
                return _simulatedVolume;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VRUIManager] 读取系统音量失败: {e.Message}");
        }
#endif
        return _simulatedVolume;
    }

    private void SetSystemVolumeNormalized(float value)
    {
        _simulatedVolume = Mathf.Clamp01(value);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaObject activity = GetUnityActivity())
            using (AndroidJavaClass contextClass = new AndroidJavaClass("android.content.Context"))
            using (AndroidJavaObject audioManager = activity.Call<AndroidJavaObject>(
                       "getSystemService",
                       contextClass.GetStatic<string>("AUDIO_SERVICE")))
            {
                int maxVolume = Mathf.Max(1, audioManager.Call<int>("getStreamMaxVolume", AndroidStreamMusic));
                int targetVolume = Mathf.Clamp(Mathf.RoundToInt(_simulatedVolume * maxVolume), 0, maxVolume);
                audioManager.Call("setStreamVolume", AndroidStreamMusic, targetVolume, 0);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VRUIManager] 设置系统音量失败: {e.Message}");
        }
#endif
    }

    private float ReadScreenBrightnessNormalized()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaObject activity = GetUnityActivity())
            using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
            using (AndroidJavaObject attributes = window.Call<AndroidJavaObject>("getAttributes"))
            {
                float brightness = attributes.Get<float>("screenBrightness");
                if (brightness >= 0f)
                    _simulatedBrightness = Mathf.Clamp01(brightness);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VRUIManager] 读取窗口亮度失败: {e.Message}");
        }
#endif
        return _simulatedBrightness;
    }

    private void SetScreenBrightnessNormalized(float value)
    {
        _simulatedBrightness = Mathf.Clamp01(value);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaObject activity = GetUnityActivity())
            {
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using (AndroidJavaObject uiActivity = GetUnityActivity())
                        using (AndroidJavaObject window = uiActivity.Call<AndroidJavaObject>("getWindow"))
                        using (AndroidJavaObject attributes = window.Call<AndroidJavaObject>("getAttributes"))
                        {
                            attributes.Set("screenBrightness", _simulatedBrightness);
                            window.Call("setAttributes", attributes);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[VRUIManager] 设置窗口亮度失败: {e.Message}");
                    }
                }));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VRUIManager] 请求设置窗口亮度失败: {e.Message}");
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject GetUnityActivity()
    {
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }
#endif

    private void UpdateAudioTracksDropdown(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks)
    {
        currentAudioTracks = tracks;
        if (audioTrackDropdown == null) return;
        if (tracks == null || tracks.Count == 0)
        {
            audioTrackDropdown.SetPlaceholder("无音轨");
            return;
        }
        audioTrackDropdown.SetInteractable(true);
        var options = new System.Collections.Generic.List<XrDropdownItemData>();
        int selectedIndex = ResolveSelectedTrackIndex(tracks, playbackService?.CurrentMedia?.AudioTrack, true);
        for (int i = 0; i < tracks.Count; i++)
        {
            options.Add(new XrDropdownItemData(tracks[i].Name));
        }
        audioTrackDropdown.SetItems(options, selectedIndex);
    }

    private void UpdateSubtitleTracksDropdown(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks)
    {
        currentSubtitleTracks = tracks;
        if (subtitleTrackDropdown == null) return;
        if (tracks == null || tracks.Count == 0)
        {
            subtitleTrackDropdown.SetPlaceholder("无字幕");
            return;
        }
        subtitleTrackDropdown.SetInteractable(true);
        var options = new System.Collections.Generic.List<XrDropdownItemData>();
        int selectedIndex = ResolveSelectedTrackIndex(tracks, playbackService?.CurrentMedia?.SpuTrack, false);
        for (int i = 0; i < tracks.Count; i++)
        {
            options.Add(new XrDropdownItemData(GetSubtitleTrackDisplayNameForMedia(tracks[i], playbackService?.CurrentMedia)));
        }
        subtitleTrackDropdown.SetItems(options, selectedIndex);
    }

    private static int ResolveSelectedTrackIndex(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks, string currentTrackId, bool preferFirstEnabledWhenDisabled)
    {
        if (tracks == null || tracks.Count == 0)
            return 0;

        for (int i = 0; i < tracks.Count; i++)
            if (tracks[i].IsSelected)
                return i;

        if (!string.IsNullOrEmpty(currentTrackId))
        {
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i].Id == currentTrackId)
                    return i;
        }

        if (preferFirstEnabledWhenDisabled)
        {
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i].Id != "-1")
                    return i;
        }

        return 0;
    }

    private static string GetSubtitleTrackDisplayName(XRVLC.Media.TrackInfo track)
    {
        return GetSubtitleTrackDisplayNameForMedia(track, null);
    }

    private static string GetSubtitleTrackDisplayNameForMedia(XRVLC.Media.TrackInfo track, XRVLC.Media.MediaWrapper media)
    {
        if (track == null)
            return "未知字幕";

        string displaySource = !string.IsNullOrWhiteSpace(track.Slave?.uri)
            ? track.Slave.uri
            : track.Name;

        return NormalizeSubtitleTrackDisplayName(displaySource, media);
    }

    private static string NormalizeSubtitleTrackDisplayName(string value, XRVLC.Media.MediaWrapper media)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "未知字幕";

        string fileName = value.Trim();
        int queryIndex = fileName.IndexOf('?');
        if (queryIndex >= 0)
            fileName = fileName.Substring(0, queryIndex);

        fileName = DecodeSubtitleDisplayValue(fileName);

        int slashIndex = Math.Max(fileName.LastIndexOf('/'), fileName.LastIndexOf('\\'));
        if (slashIndex >= 0 && slashIndex + 1 < fileName.Length)
            fileName = fileName.Substring(slashIndex + 1);

        string extension = GetSubtitleFileExtension(fileName);
        if (!string.IsNullOrEmpty(extension))
        {
            string baseName = fileName.Substring(0, fileName.Length - extension.Length);
            string mediaTrimmedName = GetMediaTrimmedSubtitleName(baseName, GetMediaBaseName(media));
            if (mediaTrimmedName != null)
                return mediaTrimmedName + extension;

            string languageSuffix = GetLanguageSuffix(baseName);
            if (!string.IsNullOrEmpty(languageSuffix))
                return languageSuffix + extension;
        }

        return fileName;
    }

    private static string GetMediaTrimmedSubtitleName(string subtitleBaseName, string mediaBaseName)
    {
        if (string.IsNullOrWhiteSpace(subtitleBaseName) || string.IsNullOrWhiteSpace(mediaBaseName))
            return null;

        if (!subtitleBaseName.StartsWith(mediaBaseName, StringComparison.OrdinalIgnoreCase))
            return null;

        if (subtitleBaseName.Length == mediaBaseName.Length)
            return string.Empty;

        char boundary = subtitleBaseName[mediaBaseName.Length];
        if (Array.IndexOf(SubtitleNameSeparators, boundary) < 0)
            return null;

        string remainder = subtitleBaseName.Substring(mediaBaseName.Length).Trim(SubtitleNameSeparators);
        return string.IsNullOrWhiteSpace(remainder) ? string.Empty : remainder;
    }

    private static string GetSubtitleFileExtension(string fileName)
    {
        int dotIndex = fileName.LastIndexOf('.');
        if (dotIndex < 0 || dotIndex >= fileName.Length - 1)
            return string.Empty;

        string extension = fileName.Substring(dotIndex);
        return SubtitleFileExtensions.Contains(extension) ? extension : string.Empty;
    }

    private static string GetLanguageSuffix(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
            return string.Empty;

        string[] tokens = baseName.Split(SubtitleNameSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return string.Empty;

        string token = tokens[tokens.Length - 1];
        return SubtitleLanguageSuffixes.Contains(token) ? token : string.Empty;
    }

    private static string GetMediaBaseName(XRVLC.Media.MediaWrapper media)
    {
        if (media == null)
            return string.Empty;

        bool usesTitle = !string.IsNullOrWhiteSpace(media.Title);
        string source = usesTitle ? media.Title : media.Uri;
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        string name = source.Trim();
        int queryIndex = name.IndexOf('?');
        if (queryIndex >= 0)
            name = name.Substring(0, queryIndex);

        name = DecodeSubtitleDisplayValue(name);

        int slashIndex = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (slashIndex >= 0 && slashIndex + 1 < name.Length)
            name = name.Substring(slashIndex + 1);

        if (!usesTitle)
        {
            string extension = GetMediaFileExtension(name);
            if (!string.IsNullOrEmpty(extension))
                name = name.Substring(0, name.Length - extension.Length);
        }

        return name;
    }

    private static string GetMediaFileExtension(string fileName)
    {
        int dotIndex = fileName.LastIndexOf('.');
        if (dotIndex < 0 || dotIndex >= fileName.Length - 1)
            return string.Empty;

        string extension = fileName.Substring(dotIndex);
        return MediaFileExtensions.Contains(extension) ? extension : string.Empty;
    }

    private static string DecodeSubtitleDisplayValue(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private void OnAudioTrackSelected(int dropdownIndex)
    {
        if (currentAudioTracks != null && dropdownIndex >= 0 && dropdownIndex < currentAudioTracks.Count)
            playbackService?.SetAudioTrack(currentAudioTracks[dropdownIndex].Id);
    }

    private void OnSubtitleTrackSelected(int dropdownIndex)
    {
        if (currentSubtitleTracks != null && dropdownIndex >= 0 && dropdownIndex < currentSubtitleTracks.Count)
            playbackService?.SetSubtitleTrack(currentSubtitleTracks[dropdownIndex].Id);
    }

    public void OnPreviousBtnClicked()
    {
        CloseSecondaryPopups();
        playbackService?.Previous();
    }

    public void OnNextBtnClicked()
    {
        CloseSecondaryPopups();
        playbackService?.Next();
    }

    public void OnPlaylistToggleBtnClicked()
    {
        if (playlistPanel == null) return;

        bool show = !playlistPanel.IsVisible;
        CloseSecondaryPopups(show ? playlistPanel.panelRoot : null);
        if (show)
        {
            BringPopupToFront(playlistPanel.panelRoot);
            Canvas.ForceUpdateCanvases();
            PositionPopupAboveButton(playlistPanel.panelRoot, playlistToggleBtn);
            playlistPanel.Show();
            Canvas.ForceUpdateCanvases();
        }
    }
}
