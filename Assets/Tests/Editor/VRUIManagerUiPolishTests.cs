using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class VRUIManagerUiPolishTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void TrackDropdownsUseCompactWidth()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));

            StringAssert.Contains("public float trackDropdownWidth = 520f / 3f", source);
            StringAssert.Contains("public int trackDropdownMaxVisibleItems = 10", source);
            StringAssert.Contains("dropdown.width = trackDropdownWidth", source);
            StringAssert.Contains("rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, trackDropdownWidth)", source);
            StringAssert.Contains("layout.preferredWidth = trackDropdownWidth", source);
            StringAssert.Contains("dropdown.maxVisibleItems = Mathf.Max(1, trackDropdownMaxVisibleItems)", source);
            StringAssert.Contains("trackDropdownWidth: 173.33333", scene);
        }

        [Test]
        public void DropdownTooltipUsesUnmaskedOverlayRoot()
        {
            string tooltip = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrOverflowTooltip.cs"));
            string item = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdownItem.cs"));
            string dropdown = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdown.cs"));

            StringAssert.Contains("public void SetOverlayRoot(Transform root)", tooltip);
            StringAssert.Contains("tooltipRoot.transform.SetParent(overlayRoot", tooltip);
            StringAssert.Contains("PositionTooltipNearSource()", tooltip);
            StringAssert.Contains("tooltipText.GetPreferredValues(fullText", tooltip);
            StringAssert.Contains("Destroy(tooltipRoot)", tooltip);
            StringAssert.Contains("tooltip.SetOverlayRoot(_owner.TooltipOverlayRoot)", item);
            StringAssert.Contains("public Transform TooltipOverlayRoot", dropdown);
        }

        [Test]
        public void SpeedButtonIsTextOnly()
        {
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));

            StringAssert.Contains("m_Name: SpeedBtn", scene);
            StringAssert.Contains("hideLegacyText: 0", scene);
            StringAssert.Contains("iconName: ", scene);
            StringAssert.DoesNotContain("iconName: speed", scene);
        }

        [Test]
        public void SpeedButtonIsWideEnoughForRateText()
        {
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.DoesNotContain("ConfigureSpeedButtonLayout()", source);
            int speedButtonIndex = scene.IndexOf("m_Name: SpeedBtn", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(speedButtonIndex, 0);
            string speedButtonBlock = scene.Substring(speedButtonIndex, Mathf.Min(6000, scene.Length - speedButtonIndex));
            StringAssert.Contains("m_MinWidth: 86", speedButtonBlock);
            StringAssert.Contains("m_PreferredWidth: 86", speedButtonBlock);

            int speedTextIndex = scene.IndexOf("m_Name: SpeedText", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(speedTextIndex, 0);
            string speedTextBlock = scene.Substring(speedTextIndex, Mathf.Min(3600, scene.Length - speedTextIndex));
            StringAssert.Contains("m_TextWrappingMode: 0", speedTextBlock);
        }

        [Test]
        public void GestureDropdownsKeepVisibleCaptionAndOptions()
        {
            string settings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));
            string dropdown = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdown.cs"));
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));

            StringAssert.Contains("new XrDropdownItemData(\"无操作\")", settings);
            StringAssert.Contains("new XrDropdownItemData(\"切换 2x 速度\")", settings);
            StringAssert.Contains("new XrDropdownItemData(\"切换字幕\")", settings);
            StringAssert.Contains("Instantiate(shortcutDropdownPrefab, row, false)", settings);
            StringAssert.Contains("Debug.LogError(\"[SettingsMenuController] shortcutDropdownPrefab is not assigned.", settings);
            StringAssert.DoesNotContain("CreateFallbackShortcutDropdown", settings);
            StringAssert.Contains("ConfigureRoundedDropdownGraphic(dropdown.gameObject, false)", settings);
            StringAssert.DoesNotContain("ConfigureRoundedDropdownGraphic(dropdown.popup", settings);
            StringAssert.DoesNotContain("ConfigureRoundedDropdownGraphic(dropdown.captionButton.gameObject", settings);
            StringAssert.DoesNotContain("ConfigureRoundedDropdownGraphic(dropdown.rowPrefab.gameObject", settings);
            StringAssert.Contains("DestroyImmediate(legacyImage)", settings);
            StringAssert.Contains("if (rounded == null)", settings);
            StringAssert.Contains("dropdown.captionText.alignment = TextAlignmentOptions.Center", settings);
            StringAssert.Contains("captionImage.color = Color.clear", settings);
            StringAssert.Contains("dropdown.captionButton.transition = Selectable.Transition.None", settings);
            StringAssert.Contains("colors.highlightedColor = Color.clear", settings);
            StringAssert.Contains("colors.pressedColor = Color.clear", settings);
            StringAssert.DoesNotContain("viewportImage.color = Color.clear", settings);
            StringAssert.DoesNotContain("dropdown.captionButton.targetGraphic = captionGraphic", settings);
            StringAssert.Contains("dropdown.onBeforeShow.AddListener(() => CloseOtherShortcutDropdowns(dropdown))", settings);
            StringAssert.Contains("private void CloseOtherShortcutDropdowns(XrDropdown keepOpen)", settings);
            StringAssert.Contains("CloseShortcutDropdownIfNot(_leftStickClickDropdown, keepOpen)", settings);
            StringAssert.Contains("dropdown.CloseImmediately()", settings);
            StringAssert.Contains("public void ApplyConfiguredLayout()", dropdown);
            StringAssert.Contains("public UnityEvent onBeforeShow = new UnityEvent()", dropdown);
            StringAssert.Contains("onBeforeShow.Invoke()", dropdown);
            StringAssert.Contains("shortcutDropdownPrefab: {fileID: 100002, guid: c0c62f3c680e47df99310ff17cfc4f84, type: 3}", scene);
        }

        [Test]
        public void GestureDropdownLayoutCentersControlsAndPadsCaptionText()
        {
            string settings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));
            string dropdown = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdown.cs"));

            StringAssert.Contains("private const float GestureLabelColumnWidthRatio = 0.25f", settings);
            StringAssert.Contains("CreateGestureRow(parent, label + \"Row\")", settings);
            StringAssert.Contains("CreateText(row, label, menuFontSize, TextAlignmentOptions.Left", settings);
            StringAssert.DoesNotContain("CreateGestureSaveRow(root.transform)", settings);
            StringAssert.DoesNotContain("保存手势设置", settings);
            StringAssert.Contains("dropdown.onValueChanged.AddListener(_ => SaveGestureMappings())", settings);
            StringAssert.Contains("layout.childAlignment = TextAnchor.MiddleCenter", settings);
            StringAssert.Contains("public float sectionTitleLeftPadding = 104f", settings);
            StringAssert.Contains("CreateSpacer(row, sectionTitleLeftPadding, 36f)", settings);
            StringAssert.DoesNotContain("TitleStartWidthRatio", settings);
            StringAssert.DoesNotContain("GetTitleStartX()", settings);
            StringAssert.Contains("captionTextRect.offsetMin = new Vector2(horizontalPadding, 0f)", dropdown);
            StringAssert.Contains("captionTextRect.offsetMax = new Vector2(-horizontalPadding, 0f)", dropdown);
        }

        [Test]
        public void GestureSectionTitleUsesIconParkInfoTooltipForFixedShortcuts()
        {
            string settings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));

            StringAssert.Contains("private const string IconResourcePath = \"UI/IconPark/\"", settings);
            StringAssert.Contains("private const string GestureInfoIconName = \"info\"", settings);
            StringAssert.Contains("private const float GestureSectionTitleWidth = 112f", settings);
            StringAssert.Contains("private const float GestureInfoIconGap = 6f", settings);
            StringAssert.Contains("摇杆左右：步进/步退\\n\" +", settings);
            StringAssert.Contains("摇杆左右长按：30s 快进/快退\\n\" +", settings);
            StringAssert.Contains("摇杆前后：调整屏幕距离\\n\" +", settings);
            StringAssert.Contains("板机键长按：2x 快速播放\\n\" +", settings);
            StringAssert.Contains("抓取键：移动屏幕\\n\" +", settings);
            StringAssert.Contains("按下摇杆：重置屏幕位置", settings);
            StringAssert.Contains("CreateSectionLabel(root.transform, \"手柄快捷键\", true)", settings);
            StringAssert.Contains("CreateText(row, label, menuFontSize, TextAlignmentOptions.Left, GestureSectionTitleWidth, 36f)", settings);
            StringAssert.Contains("CreateSpacer(row, GestureInfoIconGap, 36f)", settings);
            StringAssert.Contains("CreateGestureInfoButton(row)", settings);
            StringAssert.Contains("Resources.Load<Sprite>(IconResourcePath + GestureInfoIconName)", settings);
            StringAssert.Contains("tooltip.alignTopLeftToSource = true", settings);
            StringAssert.Contains("tooltip.SetSource(null, GestureShortcutTooltipText)", settings);
            StringAssert.DoesNotContain("CreateText(button.transform, \"i\"", settings);
        }

        [Test]
        public void InfoTooltipUsesOpaqueBackgroundAndTopLeftSourceAlignment()
        {
            string tooltip = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrOverflowTooltip.cs"));

            StringAssert.Contains("TooltipBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 1f)", tooltip);
            StringAssert.Contains("EnsureTooltipRootLayout()", tooltip);
            StringAssert.Contains("layoutElement.ignoreLayout = true", tooltip);
            StringAssert.Contains("ApplyTooltipRootStyle()", tooltip);
            StringAssert.Contains("rootImage.color = TooltipBackgroundColor", tooltip);
            StringAssert.Contains("public bool alignTopLeftToSource", tooltip);
            StringAssert.Contains("if (alignTopLeftToSource)", tooltip);
            StringAssert.Contains("tooltipRect.pivot = new Vector2(0f, 1f)", tooltip);
            StringAssert.Contains("topLeft.x - parentRectBounds.xMin,", tooltip);
            StringAssert.Contains("topLeft.y - parentRectBounds.yMax);", tooltip);
        }

        [Test]
        public void SettingsDropdownPopupUsesCanvasPriorityWithoutReparenting()
        {
            string dropdown = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrDropdown.cs"));

            StringAssert.Contains("private const int PopupBaseSortingOrder = 500", dropdown);
            StringAssert.Contains("private static int _popupSortingOrder = PopupBaseSortingOrder", dropdown);
            StringAssert.Contains("EnsureOpenCanvasPriority()", dropdown);
            StringAssert.Contains("EnsureSortedCanvas(gameObject, parentCanvas, rootSortingOrder)", dropdown);
            StringAssert.Contains("EnsureSortedCanvas(popup, rootCanvas, popupSortingOrder)", dropdown);
            StringAssert.Contains("ResetOpenCanvasPriority()", dropdown);
            StringAssert.Contains("ResetCaptionGraphicState()", dropdown);
            StringAssert.Contains("SetPriorityCanvasEnabled(popup, false)", dropdown);
            StringAssert.DoesNotContain("SetPriorityCanvasEnabled(gameObject, false)", dropdown);
            StringAssert.Contains("captionButton.transition != Selectable.Transition.None", dropdown);
            StringAssert.Contains("targetGraphic.canvasRenderer.SetColor(Color.clear)", dropdown);
            StringAssert.Contains("targetGraphic.CrossFadeColor(Color.clear, 0f, true, true)", dropdown);
            StringAssert.Contains("captionText.ForceMeshUpdate()", dropdown);
            StringAssert.Contains("NextPopupSortingOrder()", dropdown);
            StringAssert.Contains("canvas.overrideSorting = true", dropdown);
            StringAssert.Contains("canvas.enabled = true", dropdown);
            StringAssert.Contains("canvas.sortingOrder = sortingOrder", dropdown);
            StringAssert.Contains("canvas.sortingLayerID = referenceCanvas.sortingLayerID", dropdown);
            StringAssert.Contains("target.AddComponent<GraphicRaycaster>()", dropdown);
            StringAssert.Contains("target.AddComponent<TrackedDeviceGraphicRaycaster>()", dropdown);
            StringAssert.DoesNotContain("DetachPopupToCanvasOverlay", dropdown);
            StringAssert.DoesNotContain("PositionDetachedPopupBelowCaption", dropdown);
            StringAssert.DoesNotContain("RestorePopupParent", dropdown);
            StringAssert.DoesNotContain("popup.transform.SetParent", dropdown);
            StringAssert.DoesNotContain("popup.transform.SetAsLastSibling()", dropdown);
        }

        [Test]
        public void GeometryMenuCascadesFlatCurveOptionsAndUsesLeftAlignedSectionLabels()
        {
            string vr = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("CreateGeometrySectionLabel(menu.transform, \"投影模式\")", vr);
            StringAssert.Contains("CreateGeometrySectionLabel(menu.transform, \"3D 格式\")", vr);
            StringAssert.Contains("_curveSectionLabel = CreateGeometrySectionLabel(menu.transform, \"平面弧度\").gameObject", vr);
            StringAssert.Contains("_curveRow = CreateGeometryRow(menu.transform, \"CurveRow\")", vr);
            StringAssert.Contains("UpdateGeometryCascadeVisibility()", vr);
            StringAssert.Contains("bool showFlatCurveOptions = _geometryProjection == XRVLC.VideoProjection.Flat", vr);
            StringAssert.Contains("_curveSectionLabel.SetActive(showFlatCurveOptions)", vr);
            StringAssert.Contains("_curveRow.gameObject.SetActive(showFlatCurveOptions)", vr);
            StringAssert.Contains("geometryMenuFlatHeight", vr);
            StringAssert.Contains("TextAlignmentOptions.Left", vr);
        }

        [Test]
        public void SettingsMenuBuildDoesNotCachePartialTabsAfterDropdownFailure()
        {
            string settings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));

            int buildAudioIndex = settings.IndexOf("BuildAudioTab(CreateContentRoot(contentRoot, SettingsTab.Audio))", System.StringComparison.Ordinal);
            int markBuiltIndex = settings.IndexOf("_built = true", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(buildAudioIndex, 0);
            Assert.Greater(markBuiltIndex, buildAudioIndex);

            StringAssert.Contains("catch", settings);
            StringAssert.Contains("_built = false", settings);
            StringAssert.Contains("ClearChildren(transform)", settings);
            StringAssert.Contains("throw;", settings);
        }

        [Test]
        public void RoundedRectImageAvoidsFullPanelTriangleFan()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/RoundedRectImage.cs"));

            StringAssert.Contains("private static void AddSolidRoundedRect", source);
            StringAssert.Contains("private static void AddQuad", source);
            StringAssert.Contains("borderWidth", source);
            StringAssert.DoesNotContain("Vector2 center = rect.center", source);
            StringAssert.DoesNotContain("vh.AddTriangle(0, N, 1)", source);
        }

        [Test]
        public void TooltipAndButtonHoverStatesStayNearPointerAndDoNotStick()
        {
            string tooltip = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrOverflowTooltip.cs"));
            string theme = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrButtonTheme.cs"));
            string vr = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("topLeft.x - parentRectBounds.xMin", tooltip);
            StringAssert.Contains("topLeft.y - parentRectBounds.yMax", tooltip);
            StringAssert.Contains("colors.selectedColor = ResolveNormalColor(selected)", theme);
            StringAssert.Contains("ApplyRuntimeButtonTheme(seeThroughBtn, image, enabled)", vr);
            StringAssert.DoesNotContain("image.color = enabled ? PassthroughSelectedColor : TransparentListColor", vr);
        }

        [Test]
        public void MainScene_UsesOpaquePanelSquareBottomButtonsAndNoSelectedButtonState()
        {
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));

            StringAssert.Contains("m_Name: MainPanel", scene);
            StringAssert.Contains("m_Color: {r: 0.15, g: 0.15, b: 0.15, a: 1}", scene);
            StringAssert.Contains("m_Name: LeftGroup", scene);
            StringAssert.Contains("m_Name: CenterGroup", scene);
            StringAssert.Contains("m_Name: RightGroup", scene);

            foreach (string buttonName in new[]
            {
                "EqualizerBtn", "SubtitleBtn", "PreviousBtn", "PlayBtn", "NextBtn",
                "SpeedBtn", "SeeThroughBtn", "ThreeDBtn", "SettingsBtn", "PlaylistBtn"
            })
            {
                StringAssert.Contains($"m_Name: {buttonName}", scene);
            }

            StringAssert.Contains("m_MinWidth: 58", scene);
            StringAssert.Contains("m_MinHeight: 58", scene);
            StringAssert.Contains("m_PreferredWidth: 58", scene);
            StringAssert.Contains("m_PreferredHeight: 58", scene);
            StringAssert.DoesNotContain("m_SelectedColor: {r: 1, g: 1, b: 1, a: 0.16}", scene);
            StringAssert.DoesNotContain("selected: 1", scene);
        }

        [Test]
        public void RuntimeMenusUseLargerTextAndCloseOnOutsideTriggers()
        {
            string vr = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string settings = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Settings/SettingsMenuController.cs"));

            StringAssert.Contains("private const float GeometryMenuFontSize = 22f", vr);
            StringAssert.Contains("TryHideVisiblePanelFromNonUiTrigger()", vr);
            StringAssert.Contains("TryCloseSecondaryPanelFromCurrentUiTarget()", vr);
            StringAssert.Contains("IsAnySecondaryPanelOpen()", vr);
            StringAssert.Contains("TryClosePlaylistFromCurrentUiTarget()", vr);
            StringAssert.Contains("ClosePlaylistIfManagedUiClickOutsidePlaylist", vr);

            StringAssert.Contains("private static readonly Color PanelColor = new Color(0.15f, 0.15f, 0.15f, 1f)", settings);
            StringAssert.Contains("public float menuFontSize = 22f", settings);
            StringAssert.Contains("public float dropdownFontSize = 20f", settings);
            StringAssert.Contains("public float dropdownCornerRadius = 8f", settings);
            StringAssert.Contains("ConfigureShortcutDropdownVisual(dropdown)", settings);
            StringAssert.Contains("TextAlignmentOptions.Center", settings);
        }

        [Test]
        public void PlaybackTitleUsesScrollingTextPrefab()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string scroller = File.ReadAllText(ProjectFile("Assets/Scripts/UI/Common/XrScrollingTitleText.cs"));
            string prefab = File.ReadAllText(ProjectFile("Assets/Prefabs/UI/ScrollingTitleText.prefab"));
            string scene = File.ReadAllText(ProjectFile("Assets/Scenes/MainVRScene.unity"));

            StringAssert.DoesNotContain("public TextMeshProUGUI titleText", source);
            StringAssert.Contains("public XrScrollingTitleText titleScroller", source);
            StringAssert.Contains("titleScroller.SetText(GetDisplayTitle(media))", source);
            StringAssert.DoesNotContain("titleText.enabled = true", source);

            StringAssert.Contains("public void SetText(string value)", scroller);
            StringAssert.Contains("TextAlignmentOptions.MidlineLeft", scroller);
            StringAssert.Contains("TextOverflowModes.Overflow", scroller);
            StringAssert.Contains("preferredWidth", scroller);
            StringAssert.Contains("UnityEditor.EditorApplication.delayCall += ApplyQueuedEditorRefresh", scroller);
            StringAssert.Contains("UnityEditor.EditorApplication.delayCall -= ApplyQueuedEditorRefresh", scroller);

            int onValidateIndex = scroller.IndexOf("private void OnValidate()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(onValidateIndex, 0);
            string onValidateBody = scroller.Substring(onValidateIndex, Mathf.Min(180, scroller.Length - onValidateIndex));
            StringAssert.Contains("QueueEditorRefresh();", onValidateBody);
            StringAssert.DoesNotContain("ConfigureText();", onValidateBody);
            StringAssert.DoesNotContain("ResetScroll();", onValidateBody);

            StringAssert.Contains("m_Name: ScrollingTitleText", prefab);
            StringAssert.Contains("m_Name: TitleText", prefab);
            StringAssert.Contains("UnityEngine.UI::UnityEngine.UI.RectMask2D", prefab);
            StringAssert.Contains("Assembly-CSharp::XrScrollingTitleText", prefab);
            StringAssert.Contains("m_HorizontalAlignment: 1", prefab);
            StringAssert.Contains("m_TextWrappingMode: 0", prefab);
            StringAssert.Contains("m_overflowMode: 3", prefab);

            StringAssert.Contains("titleScroller:", scene);
            StringAssert.DoesNotContain("titleText:", scene);
        }
    }
}
