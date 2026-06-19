using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class VRUIManagerIconStyleTests
    {
        private static readonly string[] RequiredIcons =
        {
            "play",
            "pause",
            "previous",
            "next",
            "playlist",
            "settings",
            "exit",
            "lock",
            "brightness",
            "volume",
            "battery",
            "battery-empty",
            "battery-low",
            "battery-medium",
            "battery-full",
            "progress-dot",
            "equalizer",
            "subtitle",
            "view",
            "stereo3d"
        };

        [Test]
        public void IconParkSprites_AreAvailableAsUnitySprites()
        {
            foreach (string iconName in RequiredIcons)
            {
                Sprite sprite = Resources.Load<Sprite>($"UI/IconPark/{iconName}");

                Assert.IsNotNull(sprite, $"IconPark sprite should load from Resources: {iconName}");
            }
        }

        [Test]
        public void IconParkMetas_AreConfiguredForSpriteImport()
        {
            foreach (string iconName in RequiredIcons)
            {
                string assetPath = $"Assets/Resources/UI/IconPark/{iconName}.png";
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                Assert.IsNotNull(importer, $"{iconName} should have a TextureImporter.");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, $"{iconName} should import as Sprite.");
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, $"{iconName} should import as a single Sprite.");
                Assert.IsTrue(importer.alphaIsTransparency, $"{iconName} should preserve transparent icon pixels.");
                Assert.IsFalse(importer.mipmapEnabled, $"{iconName} should not generate mipmaps for UI rendering.");
            }
        }

        [Test]
        public void VRUIManager_BindsIconParkSpritesForExistingControls()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("SetButtonIcon(playBtn, \"play\"", source);
            StringAssert.Contains("SetButtonIcon(previousBtn, \"previous\"", source);
            StringAssert.Contains("SetButtonIcon(nextBtn, \"next\"", source);
            StringAssert.Contains("SetButtonIcon(playlistToggleBtn, \"playlist\"", source);
            StringAssert.Contains("SetButtonIcon(settingsBtn, \"settings\"", source);
            StringAssert.Contains("HideLegacyButtonText(button.transform", source);
        }

        [Test]
        public void VRUIManager_UsesLargerPrimaryPlaybackIcons()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("PrimaryPlaybackIconSize = 51f", source);
            StringAssert.Contains("AdjacentPlaybackIconSize = 48f", source);
            StringAssert.Contains("SetButtonIcon(playBtn, \"play\", PrimaryPlaybackIconSize)", source);
            StringAssert.Contains("SetButtonIcon(playBtn, iconName, PrimaryPlaybackIconSize)", source);
            StringAssert.Contains("SetButtonIcon(previousBtn, \"previous\", AdjacentPlaybackIconSize)", source);
            StringAssert.Contains("SetButtonIcon(nextBtn, \"next\", AdjacentPlaybackIconSize)", source);
        }

        [Test]
        public void VRUIManager_DefinesThreeLayerGeometryMenu()
        {
            string uiSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string playbackSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs"));

            StringAssert.Contains("threeDBtn", uiSource);
            StringAssert.Contains("OnGeometryBtnClicked", uiSource);
            StringAssert.Contains("180全景", uiSource);
            StringAssert.Contains("360全景", uiSource);
            StringAssert.Contains("平面", uiSource);
            StringAssert.Contains("平面左右眼划分", uiSource);
            StringAssert.Contains("上下3D", uiSource);
            StringAssert.Contains("左右3D", uiSource);
            StringAssert.Contains("无曲面", uiSource);
            StringAssert.Contains("小曲面", uiSource);
            StringAssert.Contains("大曲面", uiSource);
            StringAssert.Contains("SetManualVideoGeometry", uiSource);
            StringAssert.Contains("SetManualVideoGeometry", playbackSource);
        }

        [Test]
        public void VRUIManager_ButtonPopupsOpenTheirVisibleLists()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("ShowDropdownAboveButton(audioTrackDropdown, equalizerBtn)", source);
            StringAssert.Contains("ShowDropdownAboveButton(subtitleTrackDropdown, subtitleBtn)", source);
            StringAssert.Contains("dropdown.Show()", source);
            StringAssert.Contains("dropdown.Hide()", source);
            StringAssert.Contains("Canvas.ForceUpdateCanvases()", source);
        }

        [Test]
        public void VRUIManager_DoesNotUseMouseInputInXrPlayer()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.DoesNotContain("Input.GetMouseButtonDown", source);
            StringAssert.DoesNotContain("Input.mousePosition", source);
            StringAssert.DoesNotContain("Mouse.current", source);
            StringAssert.DoesNotContain("UnityEngine.InputSystem", source);
            StringAssert.DoesNotContain("EventSystem.current", source);
            StringAssert.DoesNotContain("PointerEventData", source);
            StringAssert.DoesNotContain("TryGetSelectedUiTarget", source);
            StringAssert.DoesNotContain("HandleMouseUiDismissalInput", source);
            StringAssert.Contains("TryGetXrUiTarget", source);
        }

        [Test]
        public void VRUIManager_BindsEyeButtonToPassthroughToggle()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("eyeBtn.onClick.AddListener(OnEyeBtnClicked)", source);
            StringAssert.Contains("eyeBtn.onClick.RemoveListener(OnEyeBtnClicked)", source);
            StringAssert.Contains("PicoPassthroughModeService", source);
            StringAssert.Contains("OnEyeBtnClicked", source);
        }

        [Test]
        public void VRUIManager_EyeButtonReflectsPassthroughState()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            var managerObject = new GameObject("Passthrough Button Manager");
            var eyeButtonObject = new GameObject(
                "EyeButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button));

            try
            {
                Component manager = managerObject.AddComponent(managerType);
                var eyeButton = eyeButtonObject.GetComponent<UnityEngine.UI.Button>();
                managerType.GetField("eyeBtn").SetValue(manager, eyeButton);

                managerType.GetMethod("SetEyeButtonPassthroughVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { true, true });
                Assert.Greater(eyeButtonObject.GetComponent<UnityEngine.UI.Image>().color.a, 0.2f);
                Assert.IsTrue(eyeButton.interactable);

                managerType.GetMethod("SetEyeButtonPassthroughVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { false, true });
                Assert.AreEqual(0f, eyeButtonObject.GetComponent<UnityEngine.UI.Image>().color.a);
                Assert.IsTrue(eyeButton.interactable);

                managerType.GetMethod("SetEyeButtonPassthroughVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { false, false });
                Assert.IsFalse(eyeButton.interactable);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(eyeButtonObject);
            }
        }

        [Test]
        public void PicoProjectSettings_EnableVideoSeeThroughForPassthroughMode()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/PXR_ProjectSetting.asset"));

            StringAssert.Contains("videoSeeThrough: 1", source);
        }

        [Test]
        public void VRUIManager_PopupPositioningAccountsForParentRectAnchors()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("parentRect.rect.xMin", source);
            StringAssert.Contains("parentRect.rect.yMin", source);
            StringAssert.Contains("topLeft.x - anchorOrigin.x", source);
            StringAssert.Contains("topLeft.y - anchorOrigin.y", source);
        }

        [Test]
        public void VRUIManager_TrackDropdownPopupsKeepClearanceAboveAnchorButton()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("PositionPopupAboveButton", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should position track dropdown popups through a testable helper.");

            var root = new GameObject("PopupClearanceRoot", typeof(RectTransform), typeof(Canvas));
            try
            {
                Component manager = root.AddComponent(managerType);
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(1000f, 600f);

                var buttonObject = new GameObject(
                    "AnchorButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.zero;
                buttonRect.pivot = Vector2.zero;
                buttonRect.sizeDelta = new Vector2(120f, 44f);
                buttonRect.anchoredPosition = new Vector2(320f, 140f);

                var popup = new GameObject("TrackDropdownPopup", typeof(RectTransform));
                popup.transform.SetParent(root.transform, false);
                RectTransform popupRect = popup.GetComponent<RectTransform>();
                popupRect.sizeDelta = new Vector2(220f, 162f);

                method.Invoke(manager, new object[] { popup, buttonObject.GetComponent<UnityEngine.UI.Button>() });

                var buttonCorners = new Vector3[4];
                var popupCorners = new Vector3[4];
                buttonRect.GetWorldCorners(buttonCorners);
                popupRect.GetWorldCorners(popupCorners);

                const float expectedGap = 4f;
                Assert.GreaterOrEqual(
                    popupCorners[0].y,
                    buttonCorners[1].y + expectedGap - 0.01f,
                    "Track dropdown popups should leave a small stable gap above the triggering button so they do not cover it.");
                Assert.LessOrEqual(
                    popupCorners[0].y,
                    buttonCorners[1].y + expectedGap + 0.01f,
                    "Track dropdown popups should not float noticeably above the triggering button.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VRUIManager_TrackDropdownCanAnchorToVisibleButtonIconTop()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "PositionPopupAboveButton",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(GameObject), typeof(UnityEngine.UI.Button), typeof(bool) },
                null);
            Assert.IsNotNull(method, "Track dropdowns should be able to position against the visible icon instead of the full button cell.");

            var root = new GameObject("PopupIconAnchorRoot", typeof(RectTransform), typeof(Canvas));
            try
            {
                Component manager = root.AddComponent(managerType);
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(1000f, 600f);

                var buttonObject = new GameObject(
                    "TallAnchorButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.zero;
                buttonRect.pivot = Vector2.zero;
                buttonRect.sizeDelta = new Vector2(58f, 116f);
                buttonRect.anchoredPosition = new Vector2(320f, 140f);

                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(48f, 48f);

                var popup = new GameObject("TrackDropdownPopup", typeof(RectTransform));
                popup.transform.SetParent(root.transform, false);
                RectTransform popupRect = popup.GetComponent<RectTransform>();
                popupRect.sizeDelta = new Vector2(220f, 108f);

                method.Invoke(manager, new object[] { popup, buttonObject.GetComponent<UnityEngine.UI.Button>(), true });

                var buttonCorners = new Vector3[4];
                var iconCorners = new Vector3[4];
                var popupCorners = new Vector3[4];
                buttonRect.GetWorldCorners(buttonCorners);
                iconRect.GetWorldCorners(iconCorners);
                popupRect.GetWorldCorners(popupCorners);

                const float expectedGap = 4f;
                Assert.AreEqual(
                    iconCorners[1].y + expectedGap,
                    popupCorners[0].y,
                    0.01f,
                    "Track dropdowns should sit above the visible icon, not the much taller invisible button cell.");
                Assert.Less(
                    popupCorners[0].y,
                    buttonCorners[1].y,
                    "Icon anchoring should lower dropdowns when the button hit target is taller than the icon.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VRUIManager_TrackDropdownPositioningPreservesStretchedPopupWidth()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("PositionPopupAboveButton", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should position track dropdown popups through a testable helper.");

            var root = new GameObject("PopupWidthRoot", typeof(RectTransform), typeof(Canvas));
            try
            {
                Component manager = root.AddComponent(managerType);
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(300f, 600f);

                var buttonObject = new GameObject(
                    "AnchorButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.zero;
                buttonRect.pivot = Vector2.zero;
                buttonRect.sizeDelta = new Vector2(58f, 116f);
                buttonRect.anchoredPosition = new Vector2(90f, 80f);

                var popup = new GameObject("Dropdown List", typeof(RectTransform));
                popup.transform.SetParent(root.transform, false);
                RectTransform popupRect = popup.GetComponent<RectTransform>();
                popupRect.anchorMin = new Vector2(0f, 0f);
                popupRect.anchorMax = new Vector2(1f, 0f);
                popupRect.sizeDelta = new Vector2(0f, 82f);
                Canvas.ForceUpdateCanvases();

                method.Invoke(manager, new object[] { popup, buttonObject.GetComponent<UnityEngine.UI.Button>() });

                Assert.Greater(
                    popupRect.rect.width,
                    0f,
                    "Positioning a TMP generated list must preserve its stretched width instead of collapsing it to zero.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownTemplateHasCanvasGroupForTmpFade()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("EnsureDropdownTemplateCanvasGroup", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TMP_Dropdown.Show requires the generated dropdown list to have a CanvasGroup for AlphaFadeList.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for template tests.");

            var dropdownObject = new GameObject("SubtitleDropdown", typeof(RectTransform), dropdownType);
            var templateObject = new GameObject("Template", typeof(RectTransform));
            try
            {
                templateObject.transform.SetParent(dropdownObject.transform, false);
                Component dropdown = dropdownObject.GetComponent(dropdownType);
                dropdownType.GetProperty("template").SetValue(dropdown, templateObject.GetComponent<RectTransform>());

                Assert.IsNull(templateObject.GetComponent<CanvasGroup>());

                method.Invoke(null, new object[] { dropdown });

                Assert.IsNotNull(templateObject.GetComponent<CanvasGroup>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropdownObject);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownStartupDefaultRemovesScenePlaceholderOptions()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("InitializeTrackDropdownDefault", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Track dropdowns should replace scene placeholder options before the first XR trigger click.");

            var dropdownObject = new GameObject("AudioDropdown", typeof(RectTransform), typeof(TMP_Dropdown));
            try
            {
                var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                dropdown.AddOptions(new List<string> { "Option A", "Option B" });
                dropdown.SetValueWithoutNotify(1);
                dropdown.interactable = true;

                method.Invoke(null, new object[] { dropdown, "无音轨" });

                Assert.AreEqual(1, dropdown.options.Count);
                Assert.AreEqual("无音轨", dropdown.options[0].text);
                Assert.AreEqual(0, dropdown.value);
                Assert.IsFalse(dropdown.interactable);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropdownObject);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownDoesNotOpenPlaceholderOptions()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("ShouldShowTrackDropdown", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Track dropdowns should skip showing non-interactable placeholder choices.");

            var dropdownObject = new GameObject("AudioDropdown", typeof(RectTransform), typeof(TMP_Dropdown));
            try
            {
                var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                dropdown.AddOptions(new List<string> { "无音轨" });
                dropdown.interactable = false;

                bool shouldShow = (bool)method.Invoke(null, new object[] { dropdown });

                Assert.IsFalse(shouldShow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropdownObject);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownTemplateUsesTrackRowHeightBeforeShow()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("PrepareTrackDropdownTemplate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TMP dropdown templates should be resized before Show so generated rows are not squeezed.");

            var dropdownObject = new GameObject("SubtitleDropdown", typeof(RectTransform), typeof(TMP_Dropdown));
            try
            {
                RectTransform dropdownRect = dropdownObject.GetComponent<RectTransform>();
                dropdownRect.sizeDelta = new Vector2(300f, 160f);

                var templateObject = new GameObject("Template", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect));
                templateObject.transform.SetParent(dropdownObject.transform, false);
                RectTransform templateRect = templateObject.GetComponent<RectTransform>();
                templateRect.anchorMin = new Vector2(0f, 0f);
                templateRect.anchorMax = new Vector2(1f, 0f);
                templateRect.sizeDelta = new Vector2(0f, 150f);

                var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
                viewportObject.transform.SetParent(templateObject.transform, false);
                RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
                viewportRect.anchorMin = new Vector2(0f, 0f);
                viewportRect.anchorMax = new Vector2(1f, 1f);
                viewportRect.sizeDelta = new Vector2(-18f, 0f);
                var contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.transform.SetParent(viewportObject.transform, false);
                RectTransform contentRect = contentObject.GetComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.sizeDelta = new Vector2(0f, 28f);

                var itemObject = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Toggle));
                itemObject.transform.SetParent(contentObject.transform, false);
                RectTransform itemRect = itemObject.GetComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0f, 0.5f);
                itemRect.anchorMax = new Vector2(1f, 0.5f);
                itemRect.sizeDelta = new Vector2(0f, 20f);

                var labelObject = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(itemObject.transform, false);

                var scrollRect = templateObject.GetComponent<UnityEngine.UI.ScrollRect>();
                scrollRect.content = contentRect;
                scrollRect.viewport = viewportObject.GetComponent<RectTransform>();

                var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                dropdown.template = templateRect;
                dropdown.AddOptions(new List<string> { "中文音轨", "English Track" });

                method.Invoke(null, new object[] { dropdown });

                Assert.AreEqual(108f, templateRect.rect.height, 0.01f);
                Assert.AreEqual(0f, viewportRect.offsetMin.y, 0.01f);
                Assert.AreEqual(0f, viewportRect.offsetMax.y, 0.01f);
                Assert.AreEqual(54f, contentRect.rect.height, 0.01f);
                Assert.AreEqual(0f, contentRect.anchoredPosition.y, 0.01f);
                Assert.AreEqual(54f, itemRect.rect.height, 0.01f);
                Assert.AreEqual(54f, itemObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropdownObject);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownSuppressesTmpBlockerRaycasts()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("DisableDropdownBlockers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Track dropdowns should disable TMP's full-canvas blocker because XR UI routing closes dropdowns itself.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for blocker tests.");

            var root = new GameObject("DropdownBlockerRoot", typeof(RectTransform), typeof(Canvas));
            try
            {
                var dropdownObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
                dropdownObject.transform.SetParent(root.transform, false);

                var blocker = new GameObject(
                    "Blocker",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(UnityEngine.UI.GraphicRaycaster),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button),
                    typeof(CanvasGroup));
                blocker.transform.SetParent(root.transform, false);

                var raycaster = blocker.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                var image = blocker.GetComponent<UnityEngine.UI.Image>();
                var button = blocker.GetComponent<UnityEngine.UI.Button>();
                var canvasGroup = blocker.GetComponent<CanvasGroup>();

                Assert.IsTrue(raycaster.enabled);
                Assert.IsTrue(image.raycastTarget);
                Assert.IsTrue(button.interactable);
                Assert.IsTrue(canvasGroup.blocksRaycasts);

                method.Invoke(null, new object[] { dropdownObject.GetComponent(dropdownType) });

                Assert.IsFalse(raycaster.enabled);
                Assert.IsFalse(image.raycastTarget);
                Assert.IsFalse(button.interactable);
                Assert.IsFalse(canvasGroup.blocksRaycasts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VRUIManager_EnablesRuntimeTextAndAvoidsBlankTitles()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("EnsureRuntimeTextVisible()", source);
            StringAssert.Contains("titleText.enabled = true", source);
            StringAssert.Contains("systemTimeText.enabled = true", source);
            StringAssert.Contains("batteryText.enabled = true", source);
            StringAssert.Contains("currentTimeText.enabled = true", source);
            StringAssert.Contains("totalTimeText.enabled = true", source);
            StringAssert.Contains("speedBtnText.enabled = true", source);
            StringAssert.Contains("string.IsNullOrWhiteSpace(media.Title)", source);
            StringAssert.Contains("\"未知视频\"", source);
        }

        [Test]
        public void VRUIManager_BindsSystemControlsAndStatus()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("brightnessBtn.onClick.AddListener(OnBrightnessBtnClicked)", source);
            StringAssert.Contains("volumeBtn.onClick.AddListener(OnVolumeBtnClicked)", source);
            StringAssert.Contains("UpdateSystemStatus()", source);
            StringAssert.Contains("ShowSystemSliderAboveButton", source);
            StringAssert.Contains("ReadBatteryPercent", source);
            StringAssert.Contains("SetSystemVolumeNormalized", source);
            StringAssert.Contains("SetScreenBrightnessNormalized", source);
        }

        [Test]
        public void VRUIManager_SystemSlidersAreVerticalAndMatchProgressSliderStyle()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string prefabPath = Path.Combine(Application.dataPath, "Prefabs/UI/SystemVerticalSliderPopup.prefab");

            Assert.IsTrue(
                File.Exists(prefabPath),
                "System vertical slider popup prefab should exist under Assets/Prefabs/UI.");
            string prefab = File.ReadAllText(prefabPath);

            StringAssert.Contains("SystemSliderPopupWidth = 54f", source);
            StringAssert.Contains("SystemSliderPopupHeight = 252f", source);
            StringAssert.Contains("SystemSliderLength = 216f", source);
            StringAssert.Contains("SystemSliderSize = 28f", source);
            StringAssert.Contains("SystemSliderHandleSize = 27f", source);
            StringAssert.Contains("SystemSliderTrackMin = 0.4f", source);
            StringAssert.Contains("SystemSliderTrackMax = 0.6f", source);
            StringAssert.Contains("public GameObject systemSliderPopupPrefab", source);
            StringAssert.Contains("systemSliderPopupPrefab == null", source);
            StringAssert.Contains("Debug.LogError", source);
            StringAssert.Contains("if (systemSliderPopup == null || systemSlider == null)", source);
            StringAssert.Contains("Instantiate(systemSliderPopupPrefab, parent, false)", source);
            StringAssert.DoesNotContain("using runtime fallback", source);
            StringAssert.Contains("m_Name: SystemVerticalSliderPopup", prefab);
            StringAssert.Contains("m_Name: SystemSlider", prefab);
            StringAssert.Contains("m_Name: Handle", prefab);
            StringAssert.Contains("m_Name: Fill Area", prefab);
            StringAssert.Contains("m_Direction: 2", prefab);
            StringAssert.Contains("guid: 6acd4b7c768c5406195222e5c87f8ff1", prefab);
        }

        [Test]
        public void VRUIManager_FixesSystemStatusLayoutAndAddsBatteryIcon()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("ConfigureFixedSystemStatusText(systemTimeText, SystemTimeTextWidth)", source);
            StringAssert.Contains("ConfigureFixedSystemStatusText(batteryText, BatteryStatusTextWidth)", source);
            StringAssert.Contains("text.enableWordWrapping = false", source);
            StringAssert.Contains("text.overflowMode = TextOverflowModes.Ellipsis", source);
            StringAssert.Contains("EnsureBatteryIcon()", source);
            StringAssert.Contains("ResolveBatteryIconName(batteryPercent)", source);
            StringAssert.Contains("SetBatteryIconSprite(batteryPercent)", source);
            StringAssert.Contains("batteryText.text = batteryPercent >= 0 ? $\"{batteryPercent}%\" : \"--%\"", source);
        }

        [Test]
        public void VRUIManager_UsesBatteryIconsByChargeLevel()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("BatteryUnknownIcon = \"battery\"", source);
            StringAssert.Contains("BatteryEmptyIcon = \"battery-empty\"", source);
            StringAssert.Contains("BatteryLowIcon = \"battery-low\"", source);
            StringAssert.Contains("BatteryMediumIcon = \"battery-medium\"", source);
            StringAssert.Contains("BatteryFullIcon = \"battery-full\"", source);
            StringAssert.Contains("if (batteryPercent <= 10)", source);
            StringAssert.Contains("if (batteryPercent <= 35)", source);
            StringAssert.Contains("if (batteryPercent <= 70)", source);
            StringAssert.Contains("LoadIconWithFallback(iconName, BatteryUnknownIcon)", source);
        }

        [Test]
        public void VRUIManager_ButtonsAndGeometryUseConsistentHighlightColors()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string themeSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Common/XrButtonTheme.cs"));
            string buttonSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Common/XrThemedButton.cs"));

            StringAssert.Contains("HoverListColor = new Color(1f, 1f, 1f, 0.16f)", themeSource);
            StringAssert.Contains("SelectedListColor = new Color(1f, 1f, 1f, 0.24f)", themeSource);
            StringAssert.Contains("SelectedListHoverColor = new Color(1f, 1f, 1f, 0.32f)", themeSource);
            StringAssert.Contains("SelectedGeometryOptionColor = SelectedListColor", source);
            StringAssert.Contains("SelectedGeometryOptionHoverColor = SelectedListHoverColor", source);
            StringAssert.Contains("public void ApplyTo(Button button, Image background, bool selected, bool hoverEnabled)", themeSource);
            StringAssert.Contains("button.transition = Selectable.Transition.ColorTint", themeSource);
            StringAssert.Contains("background.color = backgroundBaseColor", themeSource);
            StringAssert.Contains("colors.highlightedColor = ResolveHighlightedColor(selected, hoverEnabled)", themeSource);
            StringAssert.Contains("ApplyTheme()", buttonSource);
            StringAssert.Contains("OnValidate()", buttonSource);
            StringAssert.DoesNotContain("StyleMainHoverButtons();", source);
            StringAssert.DoesNotContain("ButtonHoverDiagnostics.Attach", source);
        }

        [Test]
        public void XrThemedButtonPrefab_UsesSharedThemeAssetAndEditorConfiguration()
        {
            string themeSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Common/XrButtonTheme.cs"));
            string buttonSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Common/XrThemedButton.cs"));
            string prefab = File.ReadAllText(Path.Combine(Application.dataPath, "Prefabs/UI/XrThemedButton.prefab"));

            StringAssert.Contains("[CreateAssetMenu(fileName = \"XrButtonTheme\"", themeSource);
            StringAssert.Contains("[ExecuteAlways]", buttonSource);
            StringAssert.Contains("[Header(\"Editor Configuration\")]", buttonSource);
            StringAssert.Contains("public bool hoverEnabled", buttonSource);
            StringAssert.Contains("public bool selected", buttonSource);
            StringAssert.Contains("public string iconName", buttonSource);
            StringAssert.Contains("public float iconSize", buttonSource);
            StringAssert.Contains("public void SetIconName(string value)", buttonSource);
            StringAssert.Contains("m_Name: XrThemedButton", prefab);
            StringAssert.Contains("m_Name: Icon", prefab);
        }

        [Test]
        public void VRUIManager_StartsWithPanelHidden()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            var root = new GameObject("StartupPanelHiddenRoot");
            try
            {
                Component manager = root.AddComponent(managerType);
                GameObject controlPanel = new GameObject("ControlPanel");
                GameObject topRightGroup = new GameObject("TopRightGroup");
                GameObject exitButtonObject = new GameObject(
                    "ExitButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button));

                managerType.GetField("controlPanel").SetValue(manager, controlPanel);
                managerType.GetField("topRightGroup").SetValue(manager, topRightGroup);
                managerType.GetField("exitBtn").SetValue(manager, exitButtonObject.GetComponent<UnityEngine.UI.Button>());

                managerType.GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, null);

                Assert.IsFalse(controlPanel.activeSelf);
                Assert.IsFalse(topRightGroup.activeSelf);
                Assert.IsFalse(exitButtonObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VRUIManager_PlayingStatusDoesNotAutoShowHiddenPanel()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            var root = new GameObject("PlayingStatusPanelHiddenRoot");
            try
            {
                Component manager = root.AddComponent(managerType);
                GameObject controlPanel = new GameObject("ControlPanel");

                managerType.GetField("controlPanel").SetValue(manager, controlPanel);
                managerType.GetMethod("HidePanel").Invoke(manager, null);
                managerType.GetMethod("OnStatusChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { XRVLC.Media.PlayerStatus.Playing });

                Assert.IsFalse(controlPanel.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_HiddenPanelTriggerPressShowsPanelEvenWhenTrackDropdownIsOpen()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("HandleTriggerPressedEdge", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should handle a trigger press edge in a testable helper.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for trigger wake tests.");

            var root = new GameObject("TriggerWakeRoot", typeof(RectTransform));
            try
            {
                Component manager = root.AddComponent(managerType);
                GameObject controlPanel = new GameObject("ControlPanel");
                GameObject topRightGroup = new GameObject("TopRightGroup");
                GameObject exitButtonObject = new GameObject(
                    "ExitButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button));
                var audioObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
                audioObject.SetActive(true);

                managerType.GetField("controlPanel").SetValue(manager, controlPanel);
                managerType.GetField("topRightGroup").SetValue(manager, topRightGroup);
                managerType.GetField("exitBtn").SetValue(manager, exitButtonObject.GetComponent<UnityEngine.UI.Button>());
                managerType.GetField("audioTrackDropdown").SetValue(manager, audioObject.GetComponent(dropdownType));

                managerType.GetMethod("HidePanel").Invoke(manager, null);
                method.Invoke(manager, null);

                Assert.IsTrue(controlPanel.activeSelf);
                Assert.IsTrue(topRightGroup.activeSelf);
                Assert.IsTrue(exitButtonObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VRUIManager_AutoHideTimerWaitsWhilePointerIsOverManagedUi()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("IsPointerOverManagedUi()", source);
            StringAssert.Contains("if (IsPointerOverManagedUi())", source);
            StringAssert.Contains("_hideTimer -= Time.deltaTime", source);
        }

        [Test]
        public void VRUIManager_ManagedUiHoverIncludesPanelAndSecondaryPopups()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("IsManagedUiObject", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should expose a private IsManagedUiObject helper for auto-hide hover gating.");

            var root = new GameObject("ManagedUiHoverRoot", typeof(RectTransform));
            try
            {
                Component manager = root.AddComponent(managerType);

                var controlPanel = new GameObject("ControlPanel", typeof(RectTransform));
                controlPanel.transform.SetParent(root.transform, false);
                var controlChild = new GameObject("ControlChild", typeof(RectTransform));
                controlChild.transform.SetParent(controlPanel.transform, false);

                var playlistRoot = new GameObject("PlaylistRoot", typeof(RectTransform));
                playlistRoot.transform.SetParent(root.transform, false);
                var playlistChild = new GameObject("PlaylistChild", typeof(RectTransform));
                playlistChild.transform.SetParent(playlistRoot.transform, false);
                Type playlistType = Type.GetType("PlaylistPanelController, Assembly-CSharp");
                Assert.IsNotNull(playlistType, "PlaylistPanelController type should be available in Assembly-CSharp.");
                Component playlist = root.AddComponent(playlistType);
                playlistType.GetField("panelRoot").SetValue(playlist, playlistRoot);

                Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
                Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for UI hover tests.");

                var dropdownObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
                dropdownObject.transform.SetParent(root.transform, false);
                var dropdownList = new GameObject("Dropdown List", typeof(RectTransform));
                dropdownList.transform.SetParent(dropdownObject.transform, false);
                var dropdownItem = new GameObject("DropdownItem", typeof(RectTransform));
                dropdownItem.transform.SetParent(dropdownList.transform, false);
                var externalUi = new GameObject("ExternalUi", typeof(RectTransform));
                externalUi.transform.SetParent(root.transform, false);

                managerType.GetField("controlPanel").SetValue(manager, controlPanel);
                managerType.GetField("playlistPanel").SetValue(manager, playlist);
                managerType.GetField("audioTrackDropdown").SetValue(manager, dropdownObject.GetComponent(dropdownType));

                Assert.IsTrue((bool)method.Invoke(manager, new object[] { controlChild }));
                Assert.IsTrue((bool)method.Invoke(manager, new object[] { playlistChild }));
                Assert.IsTrue((bool)method.Invoke(manager, new object[] { dropdownItem }));
                Assert.IsFalse((bool)method.Invoke(manager, new object[] { externalUi }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VRUIManager_BringsSecondaryPopupsToFront()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("TrackedDeviceGraphicRaycaster", source);
            StringAssert.Contains("PopupSortingOrder", source);
            StringAssert.Contains("BringPopupToFront(settingsMenu)", source);
            StringAssert.Contains("BringPopupToFront(geometryMenu)", source);
            StringAssert.Contains("BringPopupToFront(shortcutConfigPanel.gameObject)", source);
            StringAssert.Contains("BringPopupToFront(dropdown.gameObject)", source);
            StringAssert.Contains("BringDropdownListToFront(dropdown)", source);
            StringAssert.Contains("BringPopupToFront(playlistPanel.panelRoot)", source);
            StringAssert.Contains("canvas.overrideSorting = true", source);
            StringAssert.Contains("canvas.sortingOrder = PopupSortingOrder", source);
            StringAssert.Contains("popup.transform.SetAsLastSibling()", source);
        }

        [Test]
        public void VRUIManager_PopupCanvasGetsTrackedDeviceRaycasterForXrInput()
        {
            Type trackedRaycasterType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");

            Assert.IsNotNull(trackedRaycasterType, "XRI tracked UI raycaster type should be available.");

            var popup = new GameObject("Popup", typeof(RectTransform));
            try
            {
                Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
                Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

                var method = managerType.GetMethod(
                    "BringPopupToFront",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

                Assert.IsNotNull(method, "BringPopupToFront should exist.");
                method.Invoke(null, new object[] { popup });

                Assert.IsNotNull(
                    popup.GetComponent(trackedRaycasterType),
                    "Secondary popup canvases need TrackedDeviceGraphicRaycaster for XR controller UI clicks.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popup);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownListLookupStaysScopedToSourceDropdown()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("FindOpenDropdownLists", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should locate the open list for the requested TMP_Dropdown only.");
            MethodInfo activeMethod = managerType.GetMethod("ActiveDropdownList", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(activeMethod, "VRUIManager should locate the active list for a specific TMP_Dropdown only.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for dropdown scope tests.");

            var root = new GameObject("DropdownScopeRoot", typeof(RectTransform));
            try
            {
                var audioObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
                audioObject.transform.SetParent(root.transform, false);
                var audioList = new GameObject("Dropdown List", typeof(RectTransform));
                audioList.transform.SetParent(audioObject.transform, false);

                var subtitleObject = new GameObject("SubtitleDropdown", typeof(RectTransform), dropdownType);
                subtitleObject.transform.SetParent(root.transform, false);
                var subtitleList = new GameObject("Dropdown List", typeof(RectTransform));
                subtitleList.transform.SetParent(subtitleObject.transform, false);

                var lists = (List<Transform>)method.Invoke(null, new object[] { audioObject.GetComponent(dropdownType) });
                var activeList = (GameObject)activeMethod.Invoke(null, new object[] { audioObject.GetComponent(dropdownType) });

                Assert.AreEqual(1, lists.Count, "Opening one track dropdown should not reposition or restyle another dropdown list.");
                Assert.AreSame(audioList.transform, lists[0]);
                Assert.AreSame(audioList, activeList);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownReopensWhenGeneratedListIsClosedButRootIsActive()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("ShouldShowTrackDropdown", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should decide track dropdown toggling from TMP's generated list state.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for dropdown reopen tests.");

            var dropdownObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
            try
            {
                dropdownObject.SetActive(true);
                var closedList = new GameObject("Dropdown List", typeof(RectTransform));
                closedList.transform.SetParent(dropdownObject.transform, false);
                closedList.SetActive(false);

                bool shouldShow = (bool)method.Invoke(null, new object[] { dropdownObject.GetComponent(dropdownType) });

                Assert.IsTrue(
                    shouldShow,
                    "After TMP closes the generated list, the next trigger click should reopen it even if the dropdown root is still active.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropdownObject);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_TrackDropdownCloseBypassesTmpDelayedDestroy()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("HideTrackDropdownImmediately(dropdown)", source);
            StringAssert.Contains("dropdown.alphaFadeSpeed = 0f", source);
            StringAssert.Contains("dropdown.Hide()", source);
            StringAssert.Contains("dropdown.gameObject.SetActive(false)", source);
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_PanelClickOutsideTrackDropdownsClosesTrackDropdowns()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("CloseTrackDropdownsIfManagedUiClickOutsideDropdowns", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should close track dropdowns when another managed UI region is clicked.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for dropdown dismissal tests.");

            var root = new GameObject("DropdownDismissRoot", typeof(RectTransform));
            try
            {
                Component manager = root.AddComponent(managerType);

                var controlPanel = new GameObject("ControlPanel", typeof(RectTransform));
                controlPanel.transform.SetParent(root.transform, false);
                var panelBackground = new GameObject("PanelBackground", typeof(RectTransform));
                panelBackground.transform.SetParent(controlPanel.transform, false);

                var audioObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
                audioObject.transform.SetParent(root.transform, false);
                audioObject.SetActive(true);

                var subtitleObject = new GameObject("SubtitleDropdown", typeof(RectTransform), dropdownType);
                subtitleObject.transform.SetParent(root.transform, false);
                subtitleObject.SetActive(true);

                managerType.GetField("controlPanel").SetValue(manager, controlPanel);
                managerType.GetField("audioTrackDropdown").SetValue(manager, audioObject.GetComponent(dropdownType));
                managerType.GetField("subtitleTrackDropdown").SetValue(manager, subtitleObject.GetComponent(dropdownType));

                bool closed = (bool)method.Invoke(manager, new object[] { panelBackground });

                Assert.IsTrue(closed);
                Assert.IsFalse(audioObject.activeSelf);
                Assert.IsFalse(subtitleObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_ClickInsideTrackDropdownKeepsTrackDropdownsOpen()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("CloseTrackDropdownsIfManagedUiClickOutsideDropdowns", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should distinguish dropdown clicks from other panel clicks.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for dropdown dismissal tests.");

            var root = new GameObject("DropdownInsideClickRoot", typeof(RectTransform));
            try
            {
                Component manager = root.AddComponent(managerType);

                var audioObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
                audioObject.transform.SetParent(root.transform, false);
                audioObject.SetActive(true);
                var audioList = new GameObject("Dropdown List", typeof(RectTransform));
                audioList.transform.SetParent(audioObject.transform, false);
                var audioItem = new GameObject("AudioDropdownItem", typeof(RectTransform));
                audioItem.transform.SetParent(audioList.transform, false);

                managerType.GetField("audioTrackDropdown").SetValue(manager, audioObject.GetComponent(dropdownType));

                bool closed = (bool)method.Invoke(manager, new object[] { audioItem });

                Assert.IsFalse(closed);
                Assert.IsTrue(audioObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_ClickOnTrackToggleButtonKeepsTrackDropdownsOpen()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("CloseTrackDropdownsIfManagedUiClickOutsideDropdowns", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should not dismiss track dropdowns from their trigger buttons.");

            Type dropdownType = Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.IsNotNull(dropdownType, "TMP_Dropdown type should be available for dropdown dismissal tests.");

            var root = new GameObject("DropdownTriggerButtonClickRoot", typeof(RectTransform));
            try
            {
                Component manager = root.AddComponent(managerType);

                var controlPanel = new GameObject("ControlPanel", typeof(RectTransform));
                controlPanel.transform.SetParent(root.transform, false);
                var subtitleButtonObject = new GameObject("SubtitleButton", typeof(RectTransform), typeof(UnityEngine.UI.Button));
                subtitleButtonObject.transform.SetParent(controlPanel.transform, false);

                var audioObject = new GameObject("AudioDropdown", typeof(RectTransform), dropdownType);
                audioObject.transform.SetParent(controlPanel.transform, false);
                audioObject.SetActive(true);
                var audioList = new GameObject("Dropdown List", typeof(RectTransform));
                audioList.transform.SetParent(audioObject.transform, false);

                managerType.GetField("controlPanel").SetValue(manager, controlPanel);
                managerType.GetField("subtitleBtn").SetValue(manager, subtitleButtonObject.GetComponent<UnityEngine.UI.Button>());
                managerType.GetField("audioTrackDropdown").SetValue(manager, audioObject.GetComponent(dropdownType));

                bool closed = (bool)method.Invoke(manager, new object[] { subtitleButtonObject });

                Assert.IsFalse(closed, "Hovering on a track toggle button during the trigger click should not immediately close the opened dropdown.");
                Assert.IsTrue(audioObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_UsesOpaquePanelsAndTransparentItemsForSecondaryMenus()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("SecondaryPanelColor = new Color(0.04f, 0.04f, 0.04f, 1f)", source);
            StringAssert.Contains("OpaqueTextColor = Color.white", source);
            StringAssert.Contains("image.color = SecondaryPanelColor", source);
            StringAssert.Contains("background.color = TransparentListColor", source);
            StringAssert.Contains("background.raycastTarget = false", source);
            StringAssert.Contains("templateBackground.color = SecondaryPanelColor", source);
            StringAssert.Contains("HideDropdownCollapsedDisplay(dropdown)", source);
            StringAssert.Contains("StyleDropdownText(text", source);
            StringAssert.Contains("StyleOpenDropdownList(dropdown)", source);
            StringAssert.Contains("PositionOpenDropdownListAboveButton(dropdown, anchorButton)", source);
            StringAssert.Contains("image.color = TransparentListColor", source);
            StringAssert.Contains("colors.normalColor = TransparentListColor", source);
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_SecondaryDropdownItemsUseReadableSizingAndSelectionHighlight()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("TrackDropdownItemHeight = 54f", source);
            StringAssert.Contains("TrackDropdownFontSize = 22f", source);
            StringAssert.Contains("SelectedListColor", source);
            StringAssert.Contains("GetVisibleDropdownToggles", source);
            StringAssert.Contains("item.SetIsOnWithoutNotify(isSelected)", source);
            StringAssert.Contains("layout.preferredHeight = TrackDropdownItemHeight", source);
            StringAssert.Contains("text.fontSize = TrackDropdownFontSize", source);
            StringAssert.Contains("image.color = isSelected ? SelectedListColor : TransparentListColor", source);
        }

        [Test]
        public void VRUIManager_TrackDropdownSelectionUsesBridgeSelectedMarkerBeforeFallback()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("ResolveSelectedTrackIndex", source);
            StringAssert.Contains("tracks[i].IsSelected", source);
            StringAssert.Contains("preferFirstEnabledWhenDisabled", source);
            StringAssert.Contains("ResolveSelectedTrackIndex(tracks, playbackService?.CurrentMedia?.AudioTrack, true)", source);
            StringAssert.Contains("ResolveSelectedTrackIndex(tracks, playbackService?.CurrentMedia?.SpuTrack, false)", source);
        }

        [Test]
        public void VRUIManager_SubtitleTrackDisplayNameDoesNotStripAtFirstDotForNonLanguageSuffix()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "GetSubtitleTrackDisplayName",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "VRUIManager should normalize subtitle track display names.");

            var subtitleTrack = new XRVLC.Media.TrackInfo
            {
                Id = "3",
                Name = "/storage/emulated/0/Movies/My.Movie.zhshi.to.foo.ass"
            };
            var plainTrack = new XRVLC.Media.TrackInfo
            {
                Id = "4",
                Name = "English"
            };

            Assert.AreEqual("My.Movie.zhshi.to.foo.ass", method.Invoke(null, new object[] { subtitleTrack }));
            Assert.AreEqual("English", method.Invoke(null, new object[] { plainTrack }));
        }

        [Test]
        public void VRUIManager_SubtitleTrackDisplayNamePrefersSlaveUriOverGenericTrackName()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "GetSubtitleTrackDisplayName",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "VRUIManager should normalize subtitle track display names.");

            var subtitleTrack = new XRVLC.Media.TrackInfo
            {
                Id = "3",
                Name = "Track 1",
                Slave = new XRVLC.Media.SlaveDTO
                {
                    type = 0,
                    priority = 4,
                    uri = "file:///storage/emulated/0/Movies/My.Movie.SC.ass?token=1"
                }
            };

            Assert.AreEqual("SC.ass", method.Invoke(null, new object[] { subtitleTrack }));
        }

        [Test]
        public void VRUIManager_SubtitleTrackDisplayNameRemovesCurrentVideoFileName()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "GetSubtitleTrackDisplayNameForMedia",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "VRUIManager should normalize subtitle track display names with media context.");

            var subtitleTrack = new XRVLC.Media.TrackInfo
            {
                Id = "3",
                Name = "Track 1",
                Slave = new XRVLC.Media.SlaveDTO
                {
                    type = 0,
                    priority = 4,
                    uri = "file:///storage/emulated/0/Movies/My.Movie.2024.Commentary.ass"
                }
            };
            var media = new XRVLC.Media.MediaWrapper
            {
                Uri = "file:///storage/emulated/0/Movies/My.Movie.2024.mp4"
            };

            Assert.AreEqual("Commentary.ass", method.Invoke(null, new object[] { subtitleTrack, media }));
        }

        [Test]
        public void VRUIManager_SubtitleTrackDisplayNameKeepsOnlyOriginalExtensionForSameVideoName()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "GetSubtitleTrackDisplayNameForMedia",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "VRUIManager should normalize subtitle track display names with media context.");

            var subtitleTrack = new XRVLC.Media.TrackInfo
            {
                Id = "2",
                Name = "Track 1",
                Slave = new XRVLC.Media.SlaveDTO
                {
                    type = 0,
                    priority = 3,
                    uri = "smb://192.168.1.3/videos/%5BMoozzi2%5D%20Rosario%20to%20Vampire%20-%2011%20%28BD%201920x1080%20x.264%20Flac%29.ass"
                }
            };
            var media = new XRVLC.Media.MediaWrapper
            {
                Title = "[Moozzi2] Rosario to Vampire - 11 (BD 1920x1080 x.264 Flac)",
                Uri = "smb://192.168.1.3/videos/%5BMoozzi2%5D%20Rosario%20to%20Vampire%20-%2011%20%28BD%201920x1080%20x.264%20Flac%29.mkv"
            };

            Assert.AreEqual(".ass", method.Invoke(null, new object[] { subtitleTrack, media }));
        }

        [Test]
        public void VRUIManager_SubtitleTrackDisplayNameDoesNotTreatCodecDotAsMediaExtension()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "GetSubtitleTrackDisplayNameForMedia",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "VRUIManager should normalize subtitle track display names with media context.");

            var subtitleTrack = new XRVLC.Media.TrackInfo
            {
                Id = "2",
                Name = "Track 1",
                Slave = new XRVLC.Media.SlaveDTO
                {
                    type = 0,
                    priority = 2,
                    uri = "smb://192.168.1.3/videos/[Moozzi2] Cross Ange Tenshi to Ryuu no Rondo - TV + Tokuten BD/[Moozzi2] Cross Ange Tenshi to Ryuu no Rondo - 05 (BD 1920x1080 x.264 Flac).YY-SC.ass"
                }
            };
            var media = new XRVLC.Media.MediaWrapper
            {
                Title = "[Moozzi2] Cross Ange Tenshi to Ryuu no Rondo - 05 (BD 1920x1080 x.264 Flac)",
                Uri = "smb://192.168.1.3/videos/%5BMoozzi2%5D%20Cross%20Ange%20Tenshi%20to%20Ryuu%20no%20Rondo%20-%20TV%20%2B%20Tokuten%20BD/%5BMoozzi2%5D%20Cross%20Ange%20Tenshi%20to%20Ryuu%20no%20Rondo%20-%2005%20%28BD%201920x1080%20x.264%20Flac%29.mkv"
            };

            Assert.AreEqual("YY-SC.ass", method.Invoke(null, new object[] { subtitleTrack, media }));
        }

        [Test]
        public void VRUIManager_SubtitleTrackDisplayNameDoesNotStripMediaExtensionFromVlcTitle()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "GetSubtitleTrackDisplayNameForMedia",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "VRUIManager should trust VLC media titles as already-normalized.");

            var subtitleTrack = new XRVLC.Media.TrackInfo
            {
                Id = "6",
                Name = "Track 1",
                Slave = new XRVLC.Media.SlaveDTO
                {
                    type = 0,
                    priority = 2,
                    uri = "file:///storage/emulated/0/Movies/Movie.mp4.SC.ass"
                }
            };
            var media = new XRVLC.Media.MediaWrapper
            {
                Title = "Movie.mp4",
                Uri = "file:///storage/emulated/0/Movies/Movie.mp4.mkv"
            };

            Assert.AreEqual("SC.ass", method.Invoke(null, new object[] { subtitleTrack, media }));
        }

        [Test]
        public void VRUIManager_SubtitleTrackDisplayNameDoesNotRemovePartialVideoNamePrefix()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod(
                "GetSubtitleTrackDisplayNameForMedia",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "VRUIManager should only remove a complete media base name.");

            var subtitleTrack = new XRVLC.Media.TrackInfo
            {
                Id = "5",
                Name = "file:///storage/emulated/0/Movies/MovieNight.Commentary.ass"
            };
            var media = new XRVLC.Media.MediaWrapper
            {
                Title = "Movie"
            };

            Assert.AreEqual("MovieNight.Commentary.ass", method.Invoke(null, new object[] { subtitleTrack, media }));
        }

        [Test]
        public void VRUIManager_HighlightsCurrentGeometrySelections()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            var root = new GameObject("GeometryTestRoot", typeof(RectTransform), typeof(Canvas));
            try
            {
                var managerObject = new GameObject("Manager");
                managerObject.transform.SetParent(root.transform, false);
                Component manager = managerObject.AddComponent(managerType);

                var controlPanel = new GameObject("ControlPanel", typeof(RectTransform));
                controlPanel.transform.SetParent(root.transform, false);
                var threeDButtonObject = new GameObject(
                    "ThreeDBtn",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button));
                threeDButtonObject.transform.SetParent(controlPanel.transform, false);

                managerType.GetField("controlPanel").SetValue(manager, controlPanel);
                managerType.GetField("threeDBtn").SetValue(manager, threeDButtonObject.GetComponent<UnityEngine.UI.Button>());

                managerType.GetMethod("EnsureGeometryMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, null);

                GameObject geometryMenu = managerType.GetField("geometryMenu").GetValue(manager) as GameObject;
                Assert.IsNotNull(geometryMenu, "Geometry menu should be created.");

                Assert.Greater(GetButtonAlpha(geometryMenu, "平面"), 0f);
                Assert.Greater(GetButtonAlpha(geometryMenu, "平面左右眼划分"), 0f);
                Assert.Greater(GetButtonAlpha(geometryMenu, "无曲面"), 0f);
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "360全景"));
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "上下3D"));
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "大曲面"));

                managerType.GetMethod("SetGeometryProjection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { XRVLC.VideoProjection.Sphere360 });
                managerType.GetMethod("SetGeometryStereo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { XRVLC.StereoMode.TopBottom });
                managerType.GetMethod("SetFlatCurveMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { XRVLC.FlatVideoCurveMode.Large });

                Assert.Greater(GetButtonAlpha(geometryMenu, "360全景"), 0f);
                Assert.Greater(GetButtonAlpha(geometryMenu, "上下3D"), 0f);
                Assert.Greater(GetButtonAlpha(geometryMenu, "大曲面"), 0f);
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "平面"));
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "平面左右眼划分"));
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "无曲面"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VRUIManager_InitializesGeometryHighlightsFromCurrentPlayback()
        {
            string uiSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string playbackSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs"));

            StringAssert.Contains("UpdateGeometrySelectionFromPlayback()", uiSource);
            StringAssert.Contains("playbackService.CurrentGeometrySelection", uiSource);
            StringAssert.Contains("SetLocalGeometrySelection", uiSource);
            StringAssert.Contains("public VideoGeometrySelection CurrentGeometrySelection", playbackSource);
            StringAssert.Contains("_currentGeometrySelection = initialGeometry", playbackSource);
        }

        [Test]
        public void SettingsMenuController_DefinesTabbedPlaybackSettingsPanel()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Settings/SettingsMenuController.cs"));

            StringAssert.Contains("播放", source);
            StringAssert.Contains("手势", source);
            StringAssert.Contains("字幕", source);
            StringAssert.Contains("视频", source);
            StringAssert.Contains("音频", source);
            StringAssert.Contains("ShortcutSettingsService.LoadShortcutMappings", source);
            StringAssert.Contains("ShortcutSettingsService.SaveShortcutMappings", source);
            StringAssert.Contains("SubtitleRenderMode.Native", source);
            StringAssert.Contains("SubtitleRenderMode.Spatial", source);
            StringAssert.Contains("SubtitleRenderMode.Off", source);
            StringAssert.Contains("VlcPlaybackBridge.SetVideoScaleOrdinal", source);
            StringAssert.Contains("VlcPlaybackBridge.SetAudioChannelMode", source);
            StringAssert.DoesNotContain("PlaybackUiSettingsService.SaveSubtitleRenderMode", source);
            StringAssert.DoesNotContain("PlaybackUiSettingsService.SaveAudioChannelMode", source);
            StringAssert.DoesNotContain("180全景", source);
            StringAssert.DoesNotContain("360全景", source);
            StringAssert.DoesNotContain("上下3D", source);
            StringAssert.DoesNotContain("左右3D", source);
        }

        [Test]
        public void SettingsMenuController_TabSelectionUsesHoverHighlight()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Settings/SettingsMenuController.cs"));

            StringAssert.Contains("public XrButtonTheme buttonTheme", source);
            StringAssert.Contains("ApplyButtonTheme(button, image, false)", source);
            StringAssert.Contains("XrThemedButton themed = button.GetComponent<XrThemedButton>()", source);
            StringAssert.Contains("themed.theme = buttonTheme", source);
            StringAssert.Contains("themed.selected = selected", source);
            StringAssert.Contains("themed.ApplyTheme()", source);
            StringAssert.Contains("SetButtonColorsFallback(button, image, selected)", source);
        }

        [Test]
        public void PlaybackUiSettingsService_PersistsOnlyVlcVideoRatio()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Settings/PlaybackUiSettingsService.cs"));

            StringAssert.Contains("KeyVideoRatio = \"video_ratio\"", source);
            StringAssert.Contains("VlcPreferenceStore.GetInt", source);
            StringAssert.Contains("VlcPreferenceStore.PutInt", source);
            StringAssert.DoesNotContain("KeySubtitleRenderMode", source);
            StringAssert.DoesNotContain("KeyAudioChannelMode", source);
            StringAssert.DoesNotContain("SaveSubtitleRenderMode", source);
            StringAssert.DoesNotContain("SaveAudioChannelMode", source);
            StringAssert.DoesNotContain("VlcPreferenceStore.GetString", source);
            StringAssert.DoesNotContain("VlcPreferenceStore.PutString", source);
        }

        [Test]
        public void VRUIManager_SettingsButtonUsesTabbedSettingsPanelAndOutsideDismiss()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("SettingsMenuController", source);
            StringAssert.Contains("EnsureSettingsMenuController", source);
            StringAssert.Contains("TryCloseSettingsMenuFromCurrentUiTarget", source);
            StringAssert.Contains("HideSettingsMenu", source);
            StringAssert.Contains("PositionPopupAboveControlPanel(settingsMenu)", source);
            StringAssert.Contains("controlPanelRect.GetWorldCorners", source);
            StringAssert.Contains("settingsMenuController.Show", source);
        }

        [Test]
        public void VlcPlaybackBridge_ExposesPlaybackSettingsMethods()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs"));

            StringAssert.Contains("SetVideoScaleOrdinal", source);
            StringAssert.Contains("bridge.CallStatic(\"setVideoScale\"", source);
            StringAssert.Contains("SetAudioChannelMode", source);
            StringAssert.Contains("bridge.CallStatic(\"setAudioChannelMode\"", source);
        }

        [Test]
        public void AndroidPlaybackServiceBridge_ExposesPlaybackSettingsMethods()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string bridgePath = Path.Combine(
                projectRoot,
                "vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt");
            string source = File.ReadAllText(bridgePath);

            StringAssert.Contains("fun setVideoScale", source);
            StringAssert.Contains("VIDEO_RATIO", source);
            StringAssert.Contains("MediaPlayer.ScaleType.entries", source);
            StringAssert.Contains("fun setAudioChannelMode", source);
            StringAssert.DoesNotContain("xr_audio_channel_mode", source);
            StringAssert.DoesNotContain("putSingle(XR_AUDIO_CHANNEL_MODE", source);
        }

        private static float GetButtonAlpha(GameObject root, string buttonName)
        {
            UnityEngine.UI.Button[] buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (UnityEngine.UI.Button button in buttons)
            {
                if (button.name == buttonName)
                    return button.GetComponent<UnityEngine.UI.Image>().color.a;
            }

            Assert.Fail($"Button not found: {buttonName}");
            return 0f;
        }
    }
}
