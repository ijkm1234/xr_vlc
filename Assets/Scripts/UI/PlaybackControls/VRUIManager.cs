using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using XRVLC.Infrastructure.Pico;
using XRVLC.Localization;
using XRVLC.UI.XR;

public class VRUIManager : MonoBehaviour
{
    private const string IconResourcePath = "UI/IconPark/";
    private const string BatteryShellIcon = "battery-empty";
    private const string BatteryFillObjectName = "BatteryFill";
    private const float BatteryFillAlpha = 0.5f;
    private const float BatteryStatusFontSize = 20f;
    private const int BatteryLowThresholdPercent = 20;
    private const float BatteryBodyInnerMinX = 0.13f;
    private const float BatteryBodyInnerMaxX = 0.79f;
    private const float BatteryBodyInnerHeightRatio = 0.34f;
    private const string LoadingIconResourceName = "loading-four";
    private static readonly Color IconTint = Color.white;
    private static readonly Color BatteryLowFillColor = new Color(1f, 0.53333336f, 0f, BatteryFillAlpha);
    private static readonly Color BatteryNormalFillColor = new Color(1f, 1f, 1f, BatteryFillAlpha);
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
    private const float SystemSliderPercentTextHeight = 28f;
    private const float SystemSliderPercentTextGap = 8f;
    private const float SystemSliderStickThreshold = 0.25f;
    private const float SystemSliderStickPercentPerSecond = 80f;
    private const string SystemVolumeBoostMarkerName = "VolumeBoost100Marker";
    private const float LoadingSpinnerSize = 84f;
    private const float LoadingSpinnerDegreesPerSecond = -240f;
    private const float SystemTimeTextWidth = 68f;
    private const float SystemStatusFontSize = 26f;
    private const float GeometryMenuFontSize = 22f;
    private const float GeometryMenuWidth = 760f;
    private const float GeometryMenuDetailedHeight = 284f;
    private const float GeometryMenuSimpleHeight = 194f;
    private const float MinWorldCanvasDynamicPixelsPerUnit = 24f;
    private const float UiTextSharpness = 0.35f;
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
    public XrScrollingTitleText titleScroller;
    public TextMeshProUGUI systemTimeText;
    public Image batteryIcon;
    public TextMeshProUGUI batteryText;

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
    public Button seeThroughBtn;
    public Button threeDBtn;
    public Button transparentBtn;
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

    [Header("透明视频菜单")]
    public GameObject chromaKeyMenu;
    public ChromaKeyPanelController chromaKeyPanelController;
    public Toggle chromaKeyTogglePrefab;
    public Slider chromaKeySliderPrefab;

    [Header("播放列表")]
    public PlaylistPanelController playlistPanel;

    [Header("按钮主题")]
    public XrButtonTheme buttonTheme;

    [Header("XR UI 路由")]
    public XrUiInputGate uiInputGate;

        private XRVLC.Media.PlaybackService playbackService;
        private PicoPassthroughModeService passthroughModeService;
        private bool isPanelVisible = true;
    private float _hideTimer = 0f;
    private const float PanelVisibleDuration = 3f;
    private bool _autoHidePending = false;
    private bool _triggerWasPressed = false;
    private float _triggerHeldSeconds = 0f;
    private bool _triggerLongPressReleasePending = false;
    private bool _triggerLongPressEligible = false;

    private float[] speedOptions = { 1.0f, 1.25f, 1.5f, 2.0f, 0.5f };
    private int currentSpeedIndex = 0;

    private System.Collections.Generic.List<XRVLC.Media.TrackInfo> _openAudioTracks;
    private System.Collections.Generic.List<XRVLC.Media.TrackInfo> _openSubtitleTracks;
    private XRVLC.VideoProjection _geometryProjection = XRVLC.VideoProjection.Flat;
    private XRVLC.StereoMode _geometryStereo = XRVLC.StereoMode.Mono;
    private XRVLC.FlatVideoCurveMode _flatCurveMode = XRVLC.FlatVideoCurveMode.None;
    private XRVLC.FisheyeProjectionFormula _fisheyeProjectionFormula = XRVLC.FisheyeProjectionFormula.Equidistant;
    private Button _projection180Button;
    private Button _projection360Button;
    private Button _projectionFisheyeButton;
    private Button _projectionFlatButton;
    private GameObject _fisheyeFormulaSectionLabel;
    private Transform _fisheyeFormulaRow;
    private Button _fisheyeEquidistantButton;
    private Button _fisheyeEquisolidButton;
    private Button _fisheyeStereographicButton;
    private Button _fisheyeOrthographicButton;
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
    private TextMeshProUGUI systemSliderPercentText;
    private GameObject systemSliderBoostMarker;
    private ProgressHoverTimeBubble progressHoverTimeBubble;
    private Image batteryFill;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private RectTransform loadingSpinnerTransform;
    [SerializeField] private Image loadingSpinnerImage;
    private bool isLoadingVisible;
    private bool loadingOverlayMissingWarningShown;
    private SystemSliderMode _activeSystemSliderMode = SystemSliderMode.None;
    private bool _isUpdatingSystemSlider;
    private float _simulatedBrightness = 0.75f;
    private float _simulatedVolume = 0.75f;
    private float _nextSystemStatusRefreshTime;
    private static Sprite s_GeneratedLoadingSprite;

    private enum SystemSliderMode
    {
        None,
        Brightness,
        Volume
    }

    private void Awake()
    {
        SetPanelVisibility(false);
    }

    private void Start()
    {
        playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();

        if (playbackService != null)
        {
            playbackService.OnStatusChanged += OnStatusChanged;
            playbackService.OnTimeChanged += UpdateTime;
            playbackService.OnMediaChanged += UpdateMediaInfo;
            playbackService.OnBuffering += OnBuffering;
            playbackService.OnAudioTracksChanged += HandleAudioTracksDirty;
            playbackService.OnSubtitleTracksChanged += HandleSubtitleTracksDirty;
            playbackService.OnChromaKeySettingsChanged += HandleChromaKeySettingsChanged;
        }
        else
        {
            Debug.LogWarning("VRUIManager 未找到 PlaybackService，UI 将无法响应播放器事件！");
        }

        if (progressSlider != null)
        {
            EnsureProgressHoverTimeBubble();
        }

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
        if (seeThroughBtn != null)
            seeThroughBtn.onClick.AddListener(OnSeeThroughBtnClicked);
        if (threeDBtn != null)
            threeDBtn.onClick.AddListener(OnGeometryBtnClicked);
        if (transparentBtn != null)
            transparentBtn.onClick.AddListener(OnTransparentBtnClicked);
        if (settingsBtn != null)
            settingsBtn.onClick.AddListener(OnSettingsBtnClicked);
        EnsureChromaKeyMenuController();
        EnsureSettingsMenuController();
        if (settingsMenu != null)
            settingsMenu.SetActive(false);
        if (geometryMenu != null)
            geometryMenu.SetActive(false);
        if (chromaKeyMenu != null)
            chromaKeyMenu.SetActive(false);
        if (shortcutConfigPanel != null)
            shortcutConfigPanel.gameObject.SetActive(false);

        EnsureRuntimeTextVisible();
        if (titleScroller != null)
            titleScroller.SetText(XrUiText.Get(XrUiTextKey.TitlePlaceholder));
        ConfigureUiCanvasClarity();
        ConfigureUiTextEdgeClarity();
        ConfigureSecondaryDropdown(audioTrackDropdown, true);
        ConfigureSecondaryDropdown(subtitleTrackDropdown, true);
        InitializeTrackDropdownDefault(audioTrackDropdown, XrUiText.Get(XrUiTextKey.AudioTrackNone));
        InitializeTrackDropdownDefault(subtitleTrackDropdown, XrUiText.Get(XrUiTextKey.SubtitleTrackNone));

        ApplyIconSprites();
        UpdateSeeThroughButtonPassthroughVisual();
        UpdateTransparentButtonVisual();
        if (playbackService != null)
            UpdatePlayButtonIcon(playbackService.GetLivePlaybackStatus());
        EnsureLoadingOverlay();
        SetLoadingVisible(playbackService != null && IsLoadingStatus(playbackService.GetLivePlaybackStatus()));
        UpdateSystemStatus();

        RegisterUiTreeNodes();
        SetPanelVisibility(false);
    }

    private void Update()
    {
        UpdateSystemStatus();

        HandleTriggerInput();
        HandleSystemSliderStickInput();
        UpdateLoadingAnimation();

        if (_autoHidePending)
        {
            if (IsPointerOverManagedUi() || IsAnySecondaryPanelOpen())
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
        bool uiHasFocus = uiInputGate != null && uiInputGate.IsHoveringBlockingUi;

        if (pressed)
        {
            if (!_triggerWasPressed)
            {
                _triggerHeldSeconds = 0f;
                _triggerLongPressReleasePending = false;
                _triggerLongPressEligible = !uiHasFocus;
            }
            else if (uiHasFocus)
            {
                _triggerLongPressEligible = false;
            }

            if (_triggerLongPressEligible)
            {
                _triggerHeldSeconds += Time.deltaTime;
                if (_triggerHeldSeconds >= XRVLC.ShortcutInputState.TriggerFastRateHoldSeconds)
                    _triggerLongPressReleasePending = true;
            }
        }
        else if (_triggerWasPressed)
        {
            if (!_triggerLongPressReleasePending)
                HandleTriggerReleasedEdge();

            _triggerHeldSeconds = 0f;
            _triggerLongPressReleasePending = false;
            _triggerLongPressEligible = false;
        }

        _triggerWasPressed = pressed;
    }

    private void HandleTriggerReleasedEdge()
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

        if (TryCloseSecondaryPanelFromCurrentUiTarget())
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

    private bool TryCloseSecondaryPanelFromCurrentUiTarget()
    {
        if (!IsAnySecondaryPanelOpen())
            return false;

        bool hasTarget = TryGetXrUiTarget(out GameObject target);
        if (!hasTarget)
            return false;

        if (IsActiveSecondaryPanelObject(target))
            return false;

        Debug.Log($"[VRUIManager][SecondaryPanel] closeOutside target={DescribeUiTarget(target)}");
        CloseSecondaryPopups();
        RegisterUiTreeNodes();
        return true;
    }

    private bool TryConsumeCurrentUiTrigger()
    {
        RegisterUiTreeNodes();
        bool anyOpenBefore = IsAnyTrackDropdownOpen();
        GameObject target = null;
        bool hasTarget = uiInputGate != null && uiInputGate.TryGetCurrentUiTarget(out target);
        XrDropdownItem dropdownItem = target != null ? target.GetComponentInParent<XrDropdownItem>() : null;
        bool consumed = uiInputGate != null &&
            uiInputGate.TryConsumeCurrentHover(new XrUiEvent(XrUiEventType.TriggerReleased, XRNode.RightHand));

        if (anyOpenBefore || consumed)
        {
            Debug.Log(
                $"[VRUIManager][TrackDropdown] triggerGate consumed={consumed} " +
                $"hasTarget={hasTarget} target={DescribeUiTarget(target)} " +
                $"dropdownItem={DescribeUiTarget(dropdownItem != null ? dropdownItem.gameObject : null)} " +
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

    private bool IsAnySecondaryPanelOpen()
    {
        return IsAnyTrackDropdownOpen()
            || IsSettingsMenuOpen()
            || (geometryMenu != null && geometryMenu.activeSelf)
            || (chromaKeyMenu != null && chromaKeyMenu.activeSelf)
            || IsSystemSliderOpen()
            || IsPlaylistOpen()
            || (shortcutConfigPanel != null && shortcutConfigPanel.gameObject.activeSelf);
    }

    private bool IsActiveSecondaryPanelObject(GameObject target)
    {
        if (target == null)
            return false;

        if (IsTrackDropdownOpen(audioTrackDropdown) &&
            (IsDropdownObject(target, audioTrackDropdown) || IsSelfOrChildOf(target, equalizerBtn != null ? equalizerBtn.gameObject : null)))
        {
            return true;
        }

        if (IsTrackDropdownOpen(subtitleTrackDropdown) &&
            (IsDropdownObject(target, subtitleTrackDropdown) || IsSelfOrChildOf(target, subtitleBtn != null ? subtitleBtn.gameObject : null)))
        {
            return true;
        }

        if (IsSettingsMenuOpen() &&
            (IsSelfOrChildOf(target, settingsMenu) || IsSelfOrChildOf(target, settingsBtn != null ? settingsBtn.gameObject : null)))
        {
            return true;
        }

        if (geometryMenu != null && geometryMenu.activeSelf &&
            (IsSelfOrChildOf(target, geometryMenu) || IsSelfOrChildOf(target, threeDBtn != null ? threeDBtn.gameObject : null)))
        {
            return true;
        }

        if (chromaKeyMenu != null && chromaKeyMenu.activeSelf &&
            (IsSelfOrChildOf(target, chromaKeyMenu) || IsSelfOrChildOf(target, transparentBtn != null ? transparentBtn.gameObject : null)))
        {
            return true;
        }

        Button activeSystemButton = ActiveSystemSliderButton();
        if (IsSystemSliderOpen() &&
            (IsSelfOrChildOf(target, systemSliderPopup) || IsSelfOrChildOf(target, activeSystemButton != null ? activeSystemButton.gameObject : null)))
        {
            return true;
        }

        if (IsPlaylistOpen() &&
            (IsPlaylistObject(target) || IsSelfOrChildOf(target, playlistToggleBtn != null ? playlistToggleBtn.gameObject : null)))
        {
            return true;
        }

        return shortcutConfigPanel != null
            && shortcutConfigPanel.gameObject.activeSelf
            && IsSelfOrChildOf(target, shortcutConfigPanel.gameObject);
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
        bool result = dropdown != null && dropdown.interactable && !dropdown.IsOpen;
        Debug.Log(
            $"[VRUIManager][TrackDropdown] shouldShow return result={result} " +
            $"reason={DescribeShouldShowTrackDropdownReason(dropdown)} " +
            $"dropdown={DescribeUiTarget(dropdown != null ? dropdown.gameObject : null)}");
        return result;
    }

    private static string DescribeShouldShowTrackDropdownReason(XrDropdown dropdown)
    {
        if (dropdown == null)
            return "dropdownNull";
        if (!dropdown.interactable)
            return "notInteractable";
        if (dropdown.IsOpen)
            return "alreadyOpen";
        return "ready";
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

    private void ShowPanelAndScheduleHide(bool mediaReady = false)
    {
        if (!mediaReady && !CanShowPlaybackPanel())
        {
            SetPanelVisibility(false);
            return;
        }

        SetPanelVisibility(true);
        SchedulePanelHide();
    }

    private bool CanShowPlaybackPanel()
    {
        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();

        if (playbackService == null)
            return false;

        if (VlcPlaybackBridge.TryHasActivePlaybackSelection(out bool hasActiveVideoSelection))
            return hasActiveVideoSelection;

        return playbackService.CurrentMedia != null;
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
        RegisterUiNode("chroma-key-menu", "control-panel", chromaKeyMenu, XrUiNodeLayer.Popup, panelConsumer);
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
            || IsSelfOrChildOf(target, chromaKeyMenu)
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
            playbackService.OnBuffering -= OnBuffering;
            playbackService.OnAudioTracksChanged -= HandleAudioTracksDirty;
            playbackService.OnSubtitleTracksChanged -= HandleSubtitleTracksDirty;
            playbackService.OnChromaKeySettingsChanged -= HandleChromaKeySettingsChanged;
        }

        if (settingsBtn != null)
            settingsBtn.onClick.RemoveListener(OnSettingsBtnClicked);
        if (threeDBtn != null)
            threeDBtn.onClick.RemoveListener(OnGeometryBtnClicked);
        if (transparentBtn != null)
            transparentBtn.onClick.RemoveListener(OnTransparentBtnClicked);
        if (seeThroughBtn != null)
            seeThroughBtn.onClick.RemoveListener(OnSeeThroughBtnClicked);
        if (brightnessBtn != null)
            brightnessBtn.onClick.RemoveListener(OnBrightnessBtnClicked);
        if (volumeBtn != null)
            volumeBtn.onClick.RemoveListener(OnVolumeBtnClicked);
        if (systemSlider != null)
            systemSlider.onValueChanged.RemoveListener(OnSystemSliderValueChanged);
        if (progressHoverTimeBubble != null)
            progressHoverTimeBubble.SeekReleased -= OnProgressSeekReleased;
        if (passthroughModeService != null)
            passthroughModeService.StateChanged -= OnPassthroughStateChanged;
    }

    private void OnStatusChanged(XRVLC.Media.PlayerStatus status)
    {
        UpdatePlayButtonIcon(status);
        SetLoadingVisible(IsLoadingStatus(status));
        if (status == XRVLC.Media.PlayerStatus.Playing && isPanelVisible)
            SchedulePanelHide();
    }

    private void OnBuffering(float buffering)
    {
        SetLoadingVisible(buffering < 100f);
    }

    private static bool IsLoadingStatus(XRVLC.Media.PlayerStatus status)
    {
        return status == XRVLC.Media.PlayerStatus.Opening
            || status == XRVLC.Media.PlayerStatus.Buffering;
    }

    private void EnsureLoadingOverlay()
    {
        ResolveLoadingOverlayReferences();
        if (loadingOverlay == null)
        {
            if (!loadingOverlayMissingWarningShown)
            {
                Debug.LogWarning("[VRUIManager] LoadingOverlay is not assigned. Create it in the scene and wire it to VRUIManager.");
                loadingOverlayMissingWarningShown = true;
            }
            return;
        }

        Canvas canvas = loadingOverlay.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = PopupSortingOrder + 1;
            canvas.worldCamera = GetLoadingCamera();
        }

        CanvasScaler canvasScaler = loadingOverlay.GetComponent<CanvasScaler>();
        if (canvasScaler != null)
            canvasScaler.dynamicPixelsPerUnit = MinWorldCanvasDynamicPixelsPerUnit;

        CanvasGroup canvasGroup = loadingOverlay.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (loadingSpinnerTransform != null)
            loadingSpinnerTransform.sizeDelta = new Vector2(LoadingSpinnerSize, LoadingSpinnerSize);

        if (loadingSpinnerImage != null)
        {
            if (loadingSpinnerImage.sprite == null)
                loadingSpinnerImage.sprite = LoadLoadingSprite();
            loadingSpinnerImage.color = IconTint;
            loadingSpinnerImage.preserveAspect = true;
            loadingSpinnerImage.raycastTarget = false;
        }
    }

    private void ResolveLoadingOverlayReferences()
    {
        if (loadingOverlay == null)
        {
            Transform sceneOverlay = transform.root.Find("LoadingOverlay");
            if (sceneOverlay == null)
                sceneOverlay = FindSceneLoadingOverlay();

            if (sceneOverlay != null)
                loadingOverlay = sceneOverlay.gameObject;
        }

        if (loadingOverlay == null)
            return;

        if (loadingSpinnerTransform == null)
        {
            Transform spinner = loadingOverlay.transform.Find("LoadingSpinner");
            loadingSpinnerTransform = spinner as RectTransform;
        }

        if (loadingSpinnerImage == null && loadingSpinnerTransform != null)
            loadingSpinnerImage = loadingSpinnerTransform.GetComponent<Image>();
    }

    private static Transform FindSceneLoadingOverlay()
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.name != "LoadingOverlay")
                continue;
            if (!candidate.gameObject.scene.IsValid())
                continue;
            return candidate;
        }

        return null;
    }

    private void SetLoadingVisible(bool visible)
    {
        EnsureLoadingOverlay();
        isLoadingVisible = visible;

        if (loadingOverlay == null)
            return;

        if (visible)
            loadingOverlay.transform.SetAsLastSibling();

        if (loadingOverlay.activeSelf != visible)
            loadingOverlay.SetActive(visible);
    }

    private void UpdateLoadingAnimation()
    {
        if (!isLoadingVisible)
            return;

        EnsureLoadingOverlay();
        if (loadingSpinnerTransform != null)
            loadingSpinnerTransform.Rotate(0f, 0f, LoadingSpinnerDegreesPerSecond * Time.unscaledDeltaTime);
    }

    private Camera GetLoadingCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Canvas parentCanvas = controlPanel != null
            ? controlPanel.GetComponentInParent<Canvas>(true)
            : GetComponentInParent<Canvas>(true);
        return parentCanvas != null ? parentCanvas.worldCamera : null;
    }

    private static Sprite LoadLoadingSprite()
    {
        Sprite sprite = Resources.Load<Sprite>(IconResourcePath + LoadingIconResourceName);
        if (sprite != null)
            return sprite;

        return CreateGeneratedLoadingSpinnerSprite();
    }

    private static Sprite CreateGeneratedLoadingSpinnerSprite()
    {
        if (s_GeneratedLoadingSprite != null)
            return s_GeneratedLoadingSprite;

        const int textureSize = 96;
        const float radius = 38f;
        const float halfStrokeWidth = 4f;
        const float gapDegrees = 72f;

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "GeneratedLoadingSpinnerTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[textureSize * textureSize];
        Color32 white = new Color32(255, 255, 255, 255);
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance < radius - halfStrokeWidth || distance > radius + halfStrokeWidth)
                    continue;

                float angle = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f, 360f);
                if (angle < gapDegrees)
                    continue;

                pixels[y * textureSize + x] = white;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        s_GeneratedLoadingSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        s_GeneratedLoadingSprite.name = "GeneratedLoadingSpinnerSprite";
        return s_GeneratedLoadingSprite;
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

    public void TogglePanel()
    {
        if (!isPanelVisible && !CanShowPlaybackPanel())
        {
            SetPanelVisibility(false);
            return;
        }

        SetPanelVisibility(!isPanelVisible);
    }
    public void HidePanel() => SetPanelVisibility(false);

    public void OnPlayPauseButtonClicked()
    {
        CloseSecondaryPopups();
        playbackService?.TogglePlayPause();
    }

    private void OnAudioTrackBtnClicked()
    {
        if (audioTrackDropdown == null)
        {
            Debug.LogWarning("[VRUIManager][TrackDropdown] audio button return reason=audioTrackDropdownNull");
            return;
        }
        if (!IsTrackDropdownOpen(audioTrackDropdown))
            RefreshAudioTracksDropdownFromVlc();
        bool show = ShouldShowTrackDropdown(audioTrackDropdown);
        LogTrackDropdownClick("audio", audioTrackDropdown, show);
        CloseSecondaryPopups(show ? audioTrackDropdown.gameObject : null);
        if (show)
            ShowDropdownAboveButton(audioTrackDropdown, equalizerBtn);
    }

    private void OnSubtitleBtnClicked()
    {
        if (subtitleTrackDropdown == null)
        {
            Debug.LogWarning(
                "[VRUIManager][SubtitlePicker] Subtitle button return reason=subtitleTrackDropdownNull");
            return;
        }
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] Subtitle button clicked " +
            $"dropdown={DescribeUiTarget(subtitleTrackDropdown.gameObject)} " +
            $"wasOpen={IsTrackDropdownOpen(subtitleTrackDropdown)} " +
            $"knownTracks={(_openSubtitleTracks != null ? _openSubtitleTracks.Count : -1)}");
        if (!IsTrackDropdownOpen(subtitleTrackDropdown))
        {
            Debug.Log("[VRUIManager][SubtitlePicker] RefreshSubtitleTracksDropdownFromVlc because dropdown is closed");
            RefreshSubtitleTracksDropdownFromVlc();
        }
        else
        {
            Debug.Log("[VRUIManager][SubtitlePicker] RefreshSubtitleTracksDropdownFromVlc skipped because dropdown is already open");
        }
        bool show = ShouldShowTrackDropdown(subtitleTrackDropdown);
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] Subtitle dropdown refreshed " +
            $"options={subtitleTrackDropdown.Count} interactable={subtitleTrackDropdown.interactable} " +
            $"shouldShow={show} list={DescribeUiTarget(subtitleTrackDropdown.ActiveList)}");
        LogTrackDropdownClick("subtitle", subtitleTrackDropdown, show);
        CloseSecondaryPopups(show ? subtitleTrackDropdown.gameObject : null);
        if (show)
            ShowDropdownAboveButton(subtitleTrackDropdown, subtitleBtn);
        else
            Debug.Log(
                $"[VRUIManager][SubtitlePicker] Subtitle button no-show return reason=shouldShowFalse " +
                $"interactable={subtitleTrackDropdown.interactable} isOpen={subtitleTrackDropdown.IsOpen} " +
                $"count={subtitleTrackDropdown.Count}");
    }

    private void OnBrightnessBtnClicked()
    {
        ToggleSystemSlider(SystemSliderMode.Brightness, brightnessBtn, ReadScreenBrightnessNormalized());
    }

    private void OnVolumeBtnClicked()
    {
        ToggleSystemSlider(SystemSliderMode.Volume, volumeBtn, ReadVolumePercentNormalized());
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
        ConfigureSystemSliderForMode(mode);
        _isUpdatingSystemSlider = true;
        float normalized = Mathf.Clamp01(normalizedValue);
        systemSlider.SetValueWithoutNotify(normalized);
        _isUpdatingSystemSlider = false;
        UpdateSystemSliderPercentLabel(normalized);
        UpdateVolumeBoostMarker(mode);

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
        EnsureSystemSliderRuntimeDecorations();
        systemSliderPopup.SetActive(false);
    }

    private void EnsureSystemSliderRuntimeDecorations()
    {
        if (systemSliderPopup == null || systemSlider == null)
            return;

        if (systemSliderPercentText == null)
        {
            var textObject = new GameObject("SystemSliderPercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(systemSliderPopup.transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(96f, SystemSliderPercentTextHeight);

            systemSliderPercentText = textObject.GetComponent<TextMeshProUGUI>();
            systemSliderPercentText.alignment = TextAlignmentOptions.Center;
            systemSliderPercentText.color = Color.white;
            systemSliderPercentText.fontSize = 22f;
            systemSliderPercentText.enableWordWrapping = false;
            systemSliderPercentText.raycastTarget = false;
        }

        if (systemSliderBoostMarker == null)
        {
            systemSliderBoostMarker = new GameObject(SystemVolumeBoostMarkerName, typeof(RectTransform), typeof(Image));
            systemSliderBoostMarker.transform.SetParent(systemSlider.transform, false);

            RectTransform markerRect = systemSliderBoostMarker.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(18f, 2f);
            markerRect.anchoredPosition = Vector2.zero;

            Image marker = systemSliderBoostMarker.GetComponent<Image>();
            marker.color = Color.white;
            marker.raycastTarget = false;
            systemSliderBoostMarker.SetActive(false);
        }
    }

    private void ConfigureSystemSliderForMode(SystemSliderMode mode)
    {
        EnsureSystemSliderRuntimeDecorations();
        if (systemSliderPopup == null || systemSlider == null)
            return;

        float sliderLength = SystemSliderLength;
        float popupHeight = SystemSliderPopupHeight;
        float sliderYOffset = 0f;

        RectTransform popupRect = systemSliderPopup.GetComponent<RectTransform>();
        if (popupRect != null)
            popupRect.sizeDelta = new Vector2(SystemSliderPopupWidth, popupHeight);

        LayoutElement popupLayout = systemSliderPopup.GetComponent<LayoutElement>();
        if (popupLayout != null)
        {
            popupLayout.minHeight = popupHeight;
            popupLayout.preferredHeight = popupHeight;
        }

        RectTransform sliderRect = systemSlider.GetComponent<RectTransform>();
        if (sliderRect != null)
        {
            sliderRect.sizeDelta = new Vector2(SystemSliderSize, sliderLength);
            sliderRect.anchoredPosition = new Vector2(0f, sliderYOffset);
        }

        LayoutElement sliderLayout = systemSlider.GetComponent<LayoutElement>();
        if (sliderLayout != null)
        {
            sliderLayout.minHeight = sliderLength;
            sliderLayout.preferredHeight = sliderLength;
        }

        if (systemSliderPercentText != null)
        {
            RectTransform textRect = systemSliderPercentText.GetComponent<RectTransform>();
            textRect.anchoredPosition = new Vector2(0f, SystemSliderPopupHeight * 0.5f + SystemSliderPercentTextGap + SystemSliderPercentTextHeight * 0.5f);
        }

        UpdateVolumeBoostMarker(mode);
    }

    private int GetSystemSliderMaxPercent(SystemSliderMode mode)
    {
        return mode == SystemSliderMode.Volume && IsAudioBoostEnabled() ? 200 : 100;
    }

    private bool IsAudioBoostEnabled()
    {
        return VlcPlaybackBridge.IsAudioBoostEnabled();
    }

    private void UpdateSystemSliderPercentLabel(float normalized)
    {
        if (systemSliderPercentText == null)
            return;

        int percent = Mathf.RoundToInt(Mathf.Clamp01(normalized) * GetSystemSliderMaxPercent(_activeSystemSliderMode));
        systemSliderPercentText.text = $"{percent}%";
    }

    private void UpdateVolumeBoostMarker(SystemSliderMode mode)
    {
        if (systemSliderBoostMarker == null)
            return;

        systemSliderBoostMarker.SetActive(mode == SystemSliderMode.Volume && IsAudioBoostEnabled());
    }

    private void HandleSystemSliderStickInput()
    {
        if (!IsSystemSliderOpen() || systemSlider == null)
            return;

        if (!TryGetXrUiTarget(out GameObject target) || !IsSelfOrChildOf(target, systemSliderPopup))
            return;

        if (!TryGetSystemSliderStickAxis(out float axisY))
            return;

        if (Mathf.Abs(axisY) < SystemSliderStickThreshold)
            return;

        int maxPercent = Mathf.Max(1, GetSystemSliderMaxPercent(_activeSystemSliderMode));
        float delta = axisY * SystemSliderStickPercentPerSecond * Time.deltaTime / maxPercent;
        systemSlider.value = Mathf.Clamp01(systemSlider.value + delta);
    }

    private bool TryGetSystemSliderStickAxis(out float axisY)
    {
        axisY = 0f;
        bool hasAxis = false;

        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftAxis))
        {
            axisY = leftAxis.y;
            hasAxis = true;
        }

        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rightAxis) &&
            (!hasAxis || Mathf.Abs(rightAxis.y) > Mathf.Abs(axisY)))
        {
            axisY = rightAxis.y;
            hasAxis = true;
        }

        return hasAxis;
    }

    private void OnSystemSliderValueChanged(float value)
    {
        if (_isUpdatingSystemSlider)
            return;

        float normalized = Mathf.Clamp01(value);
        if (_activeSystemSliderMode == SystemSliderMode.Brightness)
        {
            SetScreenBrightnessNormalized(normalized);
        }
        else if (_activeSystemSliderMode == SystemSliderMode.Volume)
        {
            SetVolumePercentNormalized(normalized);
        }

        UpdateSystemSliderPercentLabel(normalized);
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

    private void OnSeeThroughBtnClicked()
    {
        CloseSecondaryPopups();
        EnsurePassthroughModeService();
        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();

        if (playbackService != null)
            playbackService.TogglePassthroughBackground();
        else
            passthroughModeService?.Toggle();
        UpdateSeeThroughButtonPassthroughVisual();
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
        ApplyPassthroughBackground(enabled);
        UpdateSeeThroughButtonPassthroughVisual();
    }

    private void UpdateSeeThroughButtonPassthroughVisual()
    {
        bool isEnabled = passthroughModeService != null && passthroughModeService.IsEnabled;
        bool isSupported = passthroughModeService == null || passthroughModeService.IsSupported;
        ApplyPassthroughBackground(isEnabled && isSupported);
        SetSeeThroughButtonPassthroughVisual(isEnabled, isSupported);
    }

    private void ApplyPassthroughBackground(bool enabled)
    {
        XRVLC.VideoScreen videoScreen = ResolveVideoScreen();
        videoScreen?.SetPassthroughBackgroundEnabled(enabled);
    }

    private XRVLC.VideoScreen ResolveVideoScreen()
    {
        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();

        if (playbackService != null && playbackService.videoScreen != null)
            return playbackService.videoScreen;

        return FindAnyObjectByType<XRVLC.VideoScreen>();
    }

    private void SetSeeThroughButtonPassthroughVisual(bool enabled, bool supported)
    {
        if (seeThroughBtn == null) return;

        bool chromaKeyLocked = playbackService != null && playbackService.CurrentChromaKeySettings.Enabled;
        seeThroughBtn.interactable = supported && !chromaKeyLocked;
        Image image = seeThroughBtn.GetComponent<Image>();
        if (image == null) return;

        if (!supported)
        {
            image.color = PassthroughDisabledColor;
            return;
        }

        ApplyRuntimeButtonTheme(seeThroughBtn, image, enabled);
        if (chromaKeyLocked)
        {
            ColorBlock colors = seeThroughBtn.colors;
            colors.disabledColor = PassthroughSelectedColor;
            seeThroughBtn.colors = colors;
        }
    }

    private void OnTransparentBtnClicked()
    {
        EnsureChromaKeyMenuController();
        if (chromaKeyMenu == null || chromaKeyPanelController == null)
            return;

        bool show = !chromaKeyPanelController.IsOpen;
        CloseSecondaryPopups(show ? chromaKeyMenu : null);
        if (show)
        {
            chromaKeyPanelController.Show();
            BringPopupToFront(chromaKeyMenu);
            Canvas.ForceUpdateCanvases();
            PositionPopupAboveButton(chromaKeyMenu, transparentBtn);
            Canvas.ForceUpdateCanvases();
        }
        else
        {
            chromaKeyPanelController.Hide();
        }

        RegisterUiTreeNodes();
    }

    private void EnsureChromaKeyMenuController()
    {
        if (transparentBtn == null)
            return;

        EnsurePassthroughModeService();
        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();

        bool created = false;
        if (chromaKeyMenu == null)
        {
            Transform parent = controlPanel != null && controlPanel.transform.parent != null
                ? controlPanel.transform.parent
                : transparentBtn.transform.parent;
            chromaKeyMenu = new GameObject(
                "ChromaKeyMenu",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));
            chromaKeyMenu.transform.SetParent(parent, false);
            LayoutElement layout = chromaKeyMenu.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;
            RectTransform rect = chromaKeyMenu.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            created = true;
        }

        chromaKeyPanelController = chromaKeyMenu.GetComponent<ChromaKeyPanelController>();
        if (chromaKeyPanelController == null)
            chromaKeyPanelController = chromaKeyMenu.AddComponent<ChromaKeyPanelController>();
        chromaKeyPanelController.Bind(
            playbackService,
            passthroughModeService,
            seeThroughBtn,
            chromaKeyTogglePrefab,
            chromaKeySliderPrefab);
        chromaKeyPanelController.SetProgressSliderStyle(progressSlider);
        if (created)
            chromaKeyMenu.SetActive(false);
    }

    private void HandleChromaKeySettingsChanged(XRVLC.ChromaKeySettings settings)
    {
        UpdateTransparentButtonVisual();
        UpdateSeeThroughButtonPassthroughVisual();
    }

    private void UpdateTransparentButtonVisual()
    {
        if (transparentBtn == null)
            return;
        bool enabled = playbackService != null && playbackService.CurrentChromaKeySettings.Enabled;
        ApplyRuntimeButtonTheme(transparentBtn, transparentBtn.GetComponent<Image>(), enabled);
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
        rect.sizeDelta = new Vector2(GeometryMenuWidth, GeometryMenuDetailedHeight);
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

        CreateGeometrySectionLabel(menu.transform, XrUiText.Get(XrUiTextKey.GeometryProjection));
        Transform projectionRow = CreateGeometryRow(menu.transform, "ProjectionRow");
        _projectionFlatButton = CreateGeometryOptionButton(projectionRow, XrUiText.Get(XrUiTextKey.GeometryProjectionFlat), () => SetGeometryProjection(XRVLC.VideoProjection.Flat));
        _projection180Button = CreateGeometryOptionButton(projectionRow, XrUiText.Get(XrUiTextKey.GeometryProjection180), () => SetGeometryProjection(XRVLC.VideoProjection.Sphere180));
        _projection360Button = CreateGeometryOptionButton(projectionRow, XrUiText.Get(XrUiTextKey.GeometryProjection360), () => SetGeometryProjection(XRVLC.VideoProjection.Sphere360));
        _projectionFisheyeButton = CreateGeometryOptionButton(projectionRow, XrUiText.Get(XrUiTextKey.GeometryProjectionFisheye), () => SetGeometryProjection(XRVLC.VideoProjection.Fisheye180));

        _fisheyeFormulaSectionLabel = CreateGeometrySectionLabel(menu.transform, XrUiText.Get(XrUiTextKey.GeometryFisheyeFormula)).gameObject;
        _fisheyeFormulaRow = CreateGeometryRow(menu.transform, "FisheyeFormulaRow");
        _fisheyeEquidistantButton = CreateGeometryOptionButton(_fisheyeFormulaRow, XrUiText.Get(XrUiTextKey.GeometryFisheyeEquidistant), () => SetFisheyeProjectionFormula(XRVLC.FisheyeProjectionFormula.Equidistant));
        _fisheyeEquisolidButton = CreateGeometryOptionButton(_fisheyeFormulaRow, XrUiText.Get(XrUiTextKey.GeometryFisheyeEquisolid), () => SetFisheyeProjectionFormula(XRVLC.FisheyeProjectionFormula.EquisolidAngle));
        _fisheyeStereographicButton = CreateGeometryOptionButton(_fisheyeFormulaRow, XrUiText.Get(XrUiTextKey.GeometryFisheyeStereographic), () => SetFisheyeProjectionFormula(XRVLC.FisheyeProjectionFormula.Stereographic));
        _fisheyeOrthographicButton = CreateGeometryOptionButton(_fisheyeFormulaRow, XrUiText.Get(XrUiTextKey.GeometryFisheyeOrthographic), () => SetFisheyeProjectionFormula(XRVLC.FisheyeProjectionFormula.Orthographic));

        _curveSectionLabel = CreateGeometrySectionLabel(menu.transform, XrUiText.Get(XrUiTextKey.GeometryCurve)).gameObject;
        _curveRow = CreateGeometryRow(menu.transform, "CurveRow");
        _curveNoneButton = CreateGeometryOptionButton(_curveRow, XrUiText.Get(XrUiTextKey.GeometryCurveNone), () => SetFlatCurveMode(XRVLC.FlatVideoCurveMode.None));
        _curveSmallButton = CreateGeometryOptionButton(_curveRow, XrUiText.Get(XrUiTextKey.GeometryCurveSmall), () => SetFlatCurveMode(XRVLC.FlatVideoCurveMode.Small));
        _curveLargeButton = CreateGeometryOptionButton(_curveRow, XrUiText.Get(XrUiTextKey.GeometryCurveLarge), () => SetFlatCurveMode(XRVLC.FlatVideoCurveMode.Large));

        CreateGeometryDivider(menu.transform);

        CreateGeometrySectionLabel(menu.transform, XrUiText.Get(XrUiTextKey.GeometryStereo));
        Transform stereoRow = CreateGeometryRow(menu.transform, "StereoRow");
        _stereoMonoButton = CreateGeometryOptionButton(stereoRow, XrUiText.Get(XrUiTextKey.GeometryStereoMono), () => SetGeometryStereo(XRVLC.StereoMode.Mono));
        _stereoTopBottomButton = CreateGeometryOptionButton(stereoRow, XrUiText.Get(XrUiTextKey.GeometryStereoTopBottom), () => SetGeometryStereo(XRVLC.StereoMode.TopBottom));
        _stereoLeftRightButton = CreateGeometryOptionButton(stereoRow, XrUiText.Get(XrUiTextKey.GeometryStereoLeftRight), () => SetGeometryStereo(XRVLC.StereoMode.LeftRight));

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

    private void CreateGeometryDivider(Transform parent)
    {
        var divider = new GameObject(
            "ProjectionStereoDivider",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));
        divider.transform.SetParent(parent, false);

        Image image = divider.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.16f);
        image.raycastTarget = false;

        LayoutElement element = divider.GetComponent<LayoutElement>();
        element.minHeight = 1f;
        element.preferredHeight = 1f;
        element.flexibleHeight = 0f;
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

    private void SetFisheyeProjectionFormula(XRVLC.FisheyeProjectionFormula formula)
    {
        _fisheyeProjectionFormula = formula;
        if (playbackService == null)
            playbackService = FindAnyObjectByType<XRVLC.Media.PlaybackService>();
        playbackService?.SetFisheyeProjectionFormula(formula);
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
        SetGeometryOptionSelected(_projectionFisheyeButton, _geometryProjection == XRVLC.VideoProjection.Fisheye180);
        SetGeometryOptionSelected(_projectionFlatButton, _geometryProjection == XRVLC.VideoProjection.Flat);

        SetGeometryOptionSelected(_fisheyeEquidistantButton, _fisheyeProjectionFormula == XRVLC.FisheyeProjectionFormula.Equidistant);
        SetGeometryOptionSelected(_fisheyeEquisolidButton, _fisheyeProjectionFormula == XRVLC.FisheyeProjectionFormula.EquisolidAngle);
        SetGeometryOptionSelected(_fisheyeStereographicButton, _fisheyeProjectionFormula == XRVLC.FisheyeProjectionFormula.Stereographic);
        SetGeometryOptionSelected(_fisheyeOrthographicButton, _fisheyeProjectionFormula == XRVLC.FisheyeProjectionFormula.Orthographic);

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
        bool showFisheyeFormulaOptions = _geometryProjection == XRVLC.VideoProjection.Fisheye180;

        if (_curveSectionLabel != null)
            _curveSectionLabel.SetActive(showFlatCurveOptions);
        if (_curveRow != null)
            _curveRow.gameObject.SetActive(showFlatCurveOptions);
        if (_fisheyeFormulaSectionLabel != null)
            _fisheyeFormulaSectionLabel.SetActive(showFisheyeFormulaOptions);
        if (_fisheyeFormulaRow != null)
            _fisheyeFormulaRow.gameObject.SetActive(showFisheyeFormulaOptions);

        RectTransform rect = geometryMenu != null ? geometryMenu.GetComponent<RectTransform>() : null;
        if (rect != null)
        {
            float height = showFlatCurveOptions || showFisheyeFormulaOptions
                ? GeometryMenuDetailedHeight
                : GeometryMenuSimpleHeight;
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
        _fisheyeProjectionFormula = selection.FisheyeProjectionFormula;
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
        SetActiveIfNotExcept(chromaKeyMenu, except, false);
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
        if (dropdown == null || anchorButton == null)
        {
            Debug.LogWarning(
                $"[VRUIManager][TrackDropdown] show return reason={(dropdown == null ? "dropdownNull" : "anchorButtonNull")} " +
                $"dropdown={DescribeUiTarget(dropdown != null ? dropdown.gameObject : null)} " +
                $"anchor={DescribeUiTarget(anchorButton != null ? anchorButton.gameObject : null)}");
            return;
        }
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
            Debug.Log(
                $"[VRUIManager][TrackDropdown] show return reason=deferredUntilActive " +
                $"dropdown={DescribeUiTarget(dropdown.gameObject)} anchor={DescribeUiTarget(anchorButton.gameObject)}");
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
        {
            Debug.LogWarning(
                $"[VRUIManager][TrackDropdown] deferredShow return reason={DescribeDeferredShowReturnReason(dropdown, anchorButton)} " +
                $"dropdown={DescribeUiTarget(dropdown != null ? dropdown.gameObject : null)} " +
                $"anchor={DescribeUiTarget(anchorButton != null ? anchorButton.gameObject : null)} " +
                $"interactable={dropdown != null && dropdown.interactable}");
            yield break;
        }

        ShowPreparedDropdown(dropdown, anchorButton);
        RegisterUiTreeNodes();
    }

    private static string DescribeDeferredShowReturnReason(XrDropdown dropdown, Button anchorButton)
    {
        if (dropdown == null)
            return "dropdownNull";
        if (anchorButton == null)
            return "anchorButtonNull";
        if (!dropdown.gameObject.activeInHierarchy)
            return "dropdownInactiveInHierarchy";
        if (!dropdown.interactable)
            return "notInteractable";
        return "unknown";
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
        if (dropdown == null || dropdown.gameObject == except)
        {
            Debug.Log(
                $"[VRUIManager][TrackDropdown] closeIfNotExcept return " +
                $"reason={(dropdown == null ? "dropdownNull" : "isExcept")} except={DescribeUiTarget(except)}");
            return;
        }

        CloseTrackDropdown(dropdown);
    }

    private static bool CloseTrackDropdown(XrDropdown dropdown)
    {
        if (dropdown == null)
        {
            Debug.Log("[VRUIManager][TrackDropdown] close return result=False reason=dropdownNull");
            return false;
        }

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
        SetButtonIcon(exitBtn, "home", 32f);
        SetButtonIcon(lockBtn, "lock", 30f);
        SetButtonIcon(brightnessBtn, "brightness", 32f);
        SetButtonIcon(volumeBtn, "volume-notice", 32f);
        SetButtonIcon(equalizerBtn, "equalizer", 32f);
        SetButtonIcon(subtitleBtn, "subtitle", 32f);
        SetButtonIcon(seeThroughBtn, "sphere", 32f);
        SetButtonIcon(threeDBtn, "vr-glasses", 32f);
        SetButtonIcon(transparentBtn, "eyes", 32f);
        SetButtonIcon(settingsBtn, "setting-two", 32f);
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

    private void ConfigureUiTextEdgeClarity()
    {
        Transform root = GetUiClarityRoot();
        if (root == null)
            return;

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
            ConfigureTextEdgeClarity(text);
    }

    private Transform GetUiClarityRoot()
    {
        Canvas canvas = controlPanel != null
            ? controlPanel.GetComponentInParent<Canvas>(true)
            : GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.transform : transform;
    }

    private static void ConfigureTextEdgeClarity(TextMeshProUGUI text)
    {
        if (text == null || text.fontSharedMaterial == null)
            return;

        Material material = text.fontMaterial;
        if (material == null)
            return;

        ConfigureTextMaterialForClarity(material);
        text.fontMaterial = material;
        text.UpdateMeshPadding();
    }

    private static void ConfigureTextMaterialForClarity(Material material)
    {
        if (material == null)
            return;

        SetTextMaterialFloat(material, "_Sharpness", UiTextSharpness);
        SetTextMaterialFloat(material, "_FaceDilate", 0f);
        SetTextMaterialFloat(material, "_OutlineWidth", 0f);
        SetTextMaterialFloat(material, "_UnderlayDilate", 0f);
        SetTextMaterialFloat(material, "_UnderlaySoftness", 0f);
        SetTextMaterialFloat(material, "_GlowPower", 0f);
        SetTextMaterialFloat(material, "_GlowOuter", 0f);
        material.DisableKeyword("OUTLINE_ON");
        material.DisableKeyword("UNDERLAY_ON");
        material.DisableKeyword("UNDERLAY_INNER");
        material.DisableKeyword("GLOW_ON");
    }

    private static void SetTextMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
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

        if (titleScroller != null)
            titleScroller.SetText(GetDisplayTitle(media));

        SetLocalGeometrySelectionFromMedia(media);
        ShowPanelAndScheduleHide(mediaReady: true);
    }

    private static string GetDisplayTitle(XRVLC.Media.MediaWrapper media)
    {
        if (media == null) return XrUiText.Get(XrUiTextKey.UnknownVideo);
        if (!string.IsNullOrWhiteSpace(media.Title))
            return media.Title;
        if (string.IsNullOrWhiteSpace(media.Uri))
            return XrUiText.Get(XrUiTextKey.UnknownVideo);

        string value = media.Uri;
        int queryIndex = value.IndexOf('?');
        if (queryIndex >= 0)
            value = value.Substring(0, queryIndex);

        int slashIndex = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        if (slashIndex >= 0 && slashIndex + 1 < value.Length)
            value = value.Substring(slashIndex + 1);

        if (string.IsNullOrWhiteSpace(value))
            return XrUiText.Get(XrUiTextKey.UnknownVideo);

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

        if (progressSlider != null &&
            totalTimeMs > 0 &&
            (progressHoverTimeBubble == null || !progressHoverTimeBubble.IsInteracting))
        {
            progressSlider.SetValueWithoutNotify((float)timeMs / totalTimeMs);
        }

        if (progressHoverTimeBubble != null)
            progressHoverTimeBubble.SetDuration(totalTimeMs);

    }

    private string FormatTime(long ms)
    {
        TimeSpan t = TimeSpan.FromMilliseconds(ms);
        if (t.Hours > 0)
            return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
        return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
    }

    private void OnProgressSeekReleased(float val)
    {
        playbackService?.SeekToPosition(val);
    }

    private void EnsureProgressHoverTimeBubble()
    {
        if (progressSlider == null)
            return;

        progressHoverTimeBubble = progressSlider.gameObject.GetComponent<ProgressHoverTimeBubble>();
        if (progressHoverTimeBubble == null)
            progressHoverTimeBubble = progressSlider.gameObject.AddComponent<ProgressHoverTimeBubble>();

        progressHoverTimeBubble.Bind(progressSlider);
        progressHoverTimeBubble.SeekReleased -= OnProgressSeekReleased;
        progressHoverTimeBubble.SeekReleased += OnProgressSeekReleased;
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

        if (systemTimeText != null) systemTimeText.enabled = true;
        if (batteryText != null) batteryText.enabled = true;
        if (currentTimeText != null) currentTimeText.enabled = true;
        if (totalTimeText != null) totalTimeText.enabled = true;
        if (speedBtnText != null) speedBtnText.enabled = true;
    }

    private void ConfigureSystemStatusLayout()
    {
        ConfigureFixedSystemStatusText(systemTimeText, SystemTimeTextWidth);
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

    private void UpdateSystemStatus()
    {
        if (systemTimeText != null)
            systemTimeText.text = DateTime.Now.ToString("HH:mm");

        if (Time.unscaledTime < _nextSystemStatusRefreshTime)
            return;

        _nextSystemStatusRefreshTime = Time.unscaledTime + SystemStatusRefreshInterval;
        int batteryPercent = ReadBatteryPercent();
        if (batteryText != null)
        {
            string batteryLabel = batteryPercent >= 0 ? batteryPercent.ToString() : "--";
            batteryText.text = batteryLabel;
            ConfigureBatteryTextNoOutline();
        }

        SetBatteryVisuals(batteryPercent);
    }

    private void SetBatteryVisuals(int batteryPercent)
    {
        if (batteryIcon == null)
            return;

        Sprite sprite = LoadIconWithFallback(BatteryShellIcon, BatteryShellIcon);
        if (sprite != null)
            batteryIcon.sprite = sprite;

        batteryFill = EnsureBatteryFillImage();
        if (batteryFill == null)
            return;

        Rect contentRect = GetBatteryBodyContentRect();
        LayoutBatteryStatusText(contentRect);

        batteryFill.gameObject.SetActive(batteryPercent >= 0);
        if (batteryPercent < 0)
            return;

        float normalized = Mathf.Clamp01(batteryPercent / 100f);
        batteryFill.color = batteryPercent < BatteryLowThresholdPercent ? BatteryLowFillColor : BatteryNormalFillColor;
        RectTransform fillRect = batteryFill.rectTransform;
        LayoutBatteryFill(fillRect, normalized);
    }

    private Image EnsureBatteryFillImage()
    {
        if (batteryFill != null)
            return batteryFill;
        if (batteryIcon == null)
            return null;

        Transform fillParent = batteryIcon.transform.parent != null ? batteryIcon.transform.parent : batteryIcon.transform;
        Transform existing = fillParent.Find(BatteryFillObjectName);
        GameObject fillObject = existing != null
            ? existing.gameObject
            : new GameObject(BatteryFillObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        fillObject.transform.SetParent(fillParent, false);
        fillObject.transform.SetAsFirstSibling();

        batteryFill = fillObject.GetComponent<Image>();
        batteryFill.sprite = null;
        batteryFill.color = BatteryNormalFillColor;
        batteryFill.raycastTarget = false;

        LayoutBatteryFill(batteryFill.rectTransform, 0f);
        return batteryFill;
    }

    private void LayoutBatteryFill(RectTransform fillRect, float normalized)
    {
        if (fillRect == null || batteryIcon == null)
            return;

        Rect contentRect = GetBatteryBodyContentRect();
        float maxWidth = Mathf.Max(0f, contentRect.width);
        float fillWidth = maxWidth * Mathf.Clamp01(normalized);
        float fillHeight = Mathf.Max(0f, contentRect.height);

        RectTransform iconRect = batteryIcon.rectTransform;
        fillRect.anchorMin = iconRect.anchorMin;
        fillRect.anchorMax = iconRect.anchorMax;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(contentRect.xMin, contentRect.center.y);
        fillRect.sizeDelta = new Vector2(fillWidth, fillHeight);
    }

    private Rect GetBatteryBodyContentRect()
    {
        if (batteryIcon == null)
            return Rect.zero;

        RectTransform iconRect = batteryIcon.rectTransform;
        Rect spriteRect = GetPreservedAspectSpriteRect(iconRect);
        float innerWidth = spriteRect.width * (BatteryBodyInnerMaxX - BatteryBodyInnerMinX);
        float innerHeight = spriteRect.height * BatteryBodyInnerHeightRatio;
        float innerCenterX = spriteRect.xMin + spriteRect.width * ((BatteryBodyInnerMinX + BatteryBodyInnerMaxX) * 0.5f);
        return new Rect(
            innerCenterX - innerWidth * 0.5f,
            spriteRect.center.y - innerHeight * 0.5f,
            innerWidth,
            innerHeight);
    }

    private Rect GetPreservedAspectSpriteRect(RectTransform iconRect)
    {
        Vector2 rectSize = iconRect.rect.size;
        if (rectSize.x <= 0f)
            rectSize.x = iconRect.sizeDelta.x;
        if (rectSize.y <= 0f)
            rectSize.y = iconRect.sizeDelta.y;

        Vector2 renderedSize = rectSize;
        Sprite sprite = batteryIcon != null ? batteryIcon.sprite : null;
        if (sprite != null && rectSize.x > 0f && rectSize.y > 0f)
        {
            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float rectAspect = rectSize.x / rectSize.y;
            if (spriteAspect > rectAspect)
                renderedSize.y = rectSize.x / spriteAspect;
            else
                renderedSize.x = rectSize.y * spriteAspect;
        }

        Vector2 center = iconRect.anchoredPosition + new Vector2(
            (0.5f - iconRect.pivot.x) * rectSize.x,
            (0.5f - iconRect.pivot.y) * rectSize.y);
        return new Rect(center - renderedSize * 0.5f, renderedSize);
    }

    private void LayoutBatteryStatusText(Rect contentRect)
    {
        if (batteryText == null)
            return;

        RectTransform textRect = batteryText.rectTransform;
        RectTransform iconRect = batteryIcon != null ? batteryIcon.rectTransform : null;
        if (iconRect != null)
        {
            textRect.anchorMin = iconRect.anchorMin;
            textRect.anchorMax = iconRect.anchorMax;
        }

        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = contentRect.center;
        textRect.sizeDelta = contentRect.size;
    }

    private void ConfigureBatteryTextNoOutline()
    {
        if (batteryText != null)
        {
            batteryText.fontSize = BatteryStatusFontSize;
            batteryText.fontSizeMin = BatteryStatusFontSize;
            batteryText.fontSizeMax = BatteryStatusFontSize;
        }

        ConfigureTextEdgeClarity(batteryText);
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

    private float ReadVolumePercentNormalized()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            int maxPercent = GetSystemSliderMaxPercent(SystemSliderMode.Volume);
            int percent = Mathf.Clamp(VlcPlaybackBridge.GetVolumePercent(), 0, maxPercent);
            _simulatedVolume = Mathf.Clamp01((float)percent / maxPercent);
            return _simulatedVolume;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VRUIManager] 读取 VLC 音量增益失败: {e.Message}");
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

    private void SetVolumePercentNormalized(float value)
    {
        _simulatedVolume = Mathf.Clamp01(value);
        int percent = Mathf.RoundToInt(_simulatedVolume * GetSystemSliderMaxPercent(SystemSliderMode.Volume));
#if UNITY_ANDROID && !UNITY_EDITOR
        VlcPlaybackBridge.SetVolumePercent(percent);
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

    private void HandleAudioTracksDirty(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks)
    {
        if (IsTrackDropdownOpen(audioTrackDropdown))
            RefreshAudioTracksDropdownFromVlc();
    }

    private void HandleSubtitleTracksDirty(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks)
    {
        if (IsTrackDropdownOpen(subtitleTrackDropdown))
            RefreshSubtitleTracksDropdownFromVlc();
    }

    private void RefreshAudioTracksDropdownFromVlc()
    {
        XRVLC.Media.TrackSnapshot snapshot = playbackService != null
            ? playbackService.GetAudioTrackSnapshotFromVlc()
            : new XRVLC.Media.TrackSnapshot();
        UpdateAudioTracksDropdown(snapshot.AudioTracks);
    }

    private void RefreshSubtitleTracksDropdownFromVlc()
    {
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] RefreshSubtitleTracksDropdownFromVlc entry " +
            $"playbackServiceNull={playbackService == null}");
        XRVLC.Media.TrackSnapshot snapshot = playbackService != null
            ? playbackService.GetSubtitleTrackSnapshotFromVlc()
            : new XRVLC.Media.TrackSnapshot();
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] RefreshSubtitleTracksDropdownFromVlc snapshot " +
            $"trackCount={(snapshot.SubtitleTracks != null ? snapshot.SubtitleTracks.Count : -1)}");
        UpdateSubtitleTracksDropdown(snapshot.SubtitleTracks);
    }

    private void UpdateAudioTracksDropdown(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks)
    {
        _openAudioTracks = tracks ?? new System.Collections.Generic.List<XRVLC.Media.TrackInfo>();
        if (audioTrackDropdown == null)
        {
            Debug.LogWarning(
                $"[VRUIManager][TrackDropdown] updateAudio return reason=audioTrackDropdownNull " +
                $"trackCount={_openAudioTracks.Count}");
            return;
        }
        if (_openAudioTracks.Count == 0)
        {
            audioTrackDropdown.SetPlaceholder(XrUiText.Get(XrUiTextKey.AudioTrackNone));
            Debug.Log(
                "[VRUIManager][TrackDropdown] updateAudio return reason=noAudioTracks");
            return;
        }
        audioTrackDropdown.SetInteractable(true);
        var options = new System.Collections.Generic.List<XrDropdownItemData>();
        int selectedIndex = ResolveSelectedTrackIndex(_openAudioTracks, true);
        for (int i = 0; i < _openAudioTracks.Count; i++)
        {
            options.Add(new XrDropdownItemData(
                _openAudioTracks[i].Name,
                payload: _openAudioTracks[i].Id,
                onSelected: OnAudioTrackItemSelected));
        }
        audioTrackDropdown.SetItems(options, selectedIndex);
    }

    private void UpdateSubtitleTracksDropdown(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks)
    {
        _openSubtitleTracks = tracks ?? new System.Collections.Generic.List<XRVLC.Media.TrackInfo>();
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] UpdateSubtitleTracksDropdown entry " +
            $"inputNull={tracks == null} openCount={_openSubtitleTracks.Count} " +
            $"dropdownNull={subtitleTrackDropdown == null}");
        if (subtitleTrackDropdown == null)
        {
            Debug.LogWarning(
                $"[VRUIManager][SubtitlePicker] UpdateSubtitleTracksDropdown return reason=subtitleTrackDropdownNull " +
                $"openCount={_openSubtitleTracks.Count}");
            return;
        }
        subtitleTrackDropdown.SetInteractable(true);
        var options = new System.Collections.Generic.List<XrDropdownItemData>();
        int selectedIndex = _openSubtitleTracks.Count > 0 ? ResolveSelectedTrackIndex(_openSubtitleTracks, false) + 1 : 0;
        options.Add(new XrDropdownItemData(
            XrUiText.Get(XrUiTextKey.SubtitleTrackChooseOther),
            showBottomSeparator: true,
            onSelected: OnChooseSubtitleTrackItemSelected));
        for (int i = 0; i < _openSubtitleTracks.Count; i++)
        {
            options.Add(new XrDropdownItemData(
                GetSubtitleTrackDisplayNameForMedia(_openSubtitleTracks[i], playbackService?.CurrentMedia),
                payload: _openSubtitleTracks[i].Id,
                onSelected: OnSubtitleTrackItemSelected));
        }
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] UpdateSubtitleTracksDropdown setItems " +
            $"options={options.Count} selectedIndex={selectedIndex} itemHandlers=True " +
            $"{subtitleTrackDropdown.DescribeValueChangedEventForLog()}");
        subtitleTrackDropdown.SetItems(options, selectedIndex);
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] UpdateSubtitleTracksDropdown exit " +
            $"options={subtitleTrackDropdown.Count} value={subtitleTrackDropdown.Value} " +
            $"{subtitleTrackDropdown.DescribeValueChangedEventForLog()}");
    }

    private static int ResolveSelectedTrackIndex(System.Collections.Generic.List<XRVLC.Media.TrackInfo> tracks, bool preferFirstEnabledWhenDisabled)
    {
        if (tracks == null || tracks.Count == 0)
            return 0;

        for (int i = 0; i < tracks.Count; i++)
            if (tracks[i].IsSelected)
                return i;

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
            return XrUiText.Get(XrUiTextKey.UnknownSubtitle);

        string displaySource = !string.IsNullOrWhiteSpace(track.Slave?.uri)
            ? track.Slave.uri
            : track.Name;

        return NormalizeSubtitleTrackDisplayName(displaySource, media);
    }

    private static string NormalizeSubtitleTrackDisplayName(string value, XRVLC.Media.MediaWrapper media)
    {
        if (string.IsNullOrWhiteSpace(value))
            return XrUiText.Get(XrUiTextKey.UnknownSubtitle);

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
        if (_openAudioTracks != null && dropdownIndex >= 0 && dropdownIndex < _openAudioTracks.Count)
        {
            SelectAudioTrackById(_openAudioTracks[dropdownIndex].Id, dropdownIndex, _openAudioTracks[dropdownIndex].Name);
            return;
        }

        Debug.LogWarning(
            $"[VRUIManager][TrackDropdown] OnAudioTrackSelected return reason=invalidAudioIndex " +
            $"dropdownIndex={dropdownIndex} openAudioTracks={(_openAudioTracks != null ? _openAudioTracks.Count : -1)}");
    }

    private void OnAudioTrackItemSelected(int dropdownIndex, XrDropdownItemData item)
    {
        SelectAudioTrackById(item?.payload as string, dropdownIndex, item?.primaryText);
    }

    private void SelectAudioTrackById(string trackId, int dropdownIndex, string label)
    {
        if (string.IsNullOrEmpty(trackId))
        {
            Debug.LogWarning(
                $"[VRUIManager][TrackDropdown] SelectAudioTrackById return reason=missingTrackId " +
                $"dropdownIndex={dropdownIndex} label={label}");
            return;
        }

        Debug.Log(
            $"[VRUIManager][TrackDropdown] SelectAudioTrackById trackId={trackId} " +
            $"dropdownIndex={dropdownIndex} label={label}");
        XRVLC.Media.TrackSnapshot snapshot = playbackService?.SetAudioTrack(trackId)
            ?? new XRVLC.Media.TrackSnapshot();
        UpdateAudioTracksDropdown(snapshot.AudioTracks);
    }

    private void OnChooseSubtitleTrackItemSelected(int dropdownIndex, XrDropdownItemData item)
    {
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] ChooseSubtitleTrackItemSelected dropdownIndex={dropdownIndex} " +
            $"label={item?.primaryText}");
        OnSubtitleTrackSelected(0);
    }

    private void OnSubtitleTrackItemSelected(int dropdownIndex, XrDropdownItemData item)
    {
        SelectSubtitleTrackById(item?.payload as string, dropdownIndex, item?.primaryText);
    }

    private void SelectSubtitleTrackById(string trackId, int dropdownIndex, string label)
    {
        if (string.IsNullOrEmpty(trackId))
        {
            Debug.LogWarning(
                $"[VRUIManager][SubtitlePicker] SelectSubtitleTrackById return reason=missingTrackId " +
                $"dropdownIndex={dropdownIndex} label={label}");
            return;
        }

        Debug.Log(
            $"[VRUIManager][SubtitlePicker] SelectSubtitleTrackById trackId={trackId} " +
            $"dropdownIndex={dropdownIndex} label={label}");
        XRVLC.Media.TrackSnapshot snapshot = playbackService?.SetSubtitleTrack(trackId)
            ?? new XRVLC.Media.TrackSnapshot();
        UpdateSubtitleTracksDropdown(snapshot.SubtitleTracks);
    }

    private void OnSubtitleTrackSelected(int dropdownIndex)
    {
        Debug.Log(
            $"[VRUIManager][SubtitlePicker] OnSubtitleTrackSelected index={dropdownIndex} " +
            $"openSubtitleTracks={(_openSubtitleTracks != null ? _openSubtitleTracks.Count : -1)}");

        if (dropdownIndex == 0)
        {
            Debug.Log("[VRUIManager][SubtitlePicker] Opening picker from choose-other-subtitle row");
            CloseSecondaryPopups();
            VlcPlaybackBridge.OpenSubtitlePicker();
            Debug.Log("[VRUIManager][SubtitlePicker] OnSubtitleTrackSelected return reason=chooseOtherSubtitle");
            return;
        }

        int trackIndex = dropdownIndex - 1;
        if (_openSubtitleTracks != null && trackIndex >= 0 && trackIndex < _openSubtitleTracks.Count)
        {
            SelectSubtitleTrackById(
                _openSubtitleTracks[trackIndex].Id,
                dropdownIndex,
                GetSubtitleTrackDisplayNameForMedia(_openSubtitleTracks[trackIndex], playbackService?.CurrentMedia));
            Debug.Log(
                $"[VRUIManager][SubtitlePicker] OnSubtitleTrackSelected return reason=existingSubtitleSelected " +
                $"dropdownIndex={dropdownIndex} trackIndex={trackIndex}");
            return;
        }

        Debug.LogWarning(
            $"[VRUIManager][SubtitlePicker] OnSubtitleTrackSelected return reason=invalidSubtitleIndex " +
            $"dropdownIndex={dropdownIndex} trackIndex={trackIndex} " +
            $"openSubtitleTracks={(_openSubtitleTracks != null ? _openSubtitleTracks.Count : -1)}");
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
