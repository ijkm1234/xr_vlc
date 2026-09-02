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
            "setting-two",
            "exit",
            "home",
            "lock",
            "brightness",
            "volume",
            "volume-notice",
            "battery",
            "battery-empty",
            "battery-low",
            "battery-medium",
            "battery-full",
            "progress-dot",
            "equalizer",
            "subtitle",
            "info",
            "sphere",
            "stereo3d",
            "vr-glasses",
            "eyes"
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
        public void IconParkBatteryEmpty_IsEmptyShellWithoutInnerChargeBar()
        {
            string path = Path.Combine(Application.dataPath, "Resources/UI/IconPark/battery-empty.png");
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(texture, bytes), "battery-empty.png should decode as a PNG.");
                Assert.AreEqual(128, texture.width);
                Assert.AreEqual(128, texture.height);
                Assert.Less(texture.GetPixel(32, 64).a, 0.05f, "Battery empty icon should not contain an inner left charge bar.");
                Assert.Greater(texture.GetPixel(8, 64).a, 0.75f, "Battery empty icon should still contain the outer shell stroke.");
                Assert.Less(texture.GetPixel(15, 64).a, 0.2f, "Battery empty icon stroke should be thin enough to match the other IconPark controls.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
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
            StringAssert.Contains("SetButtonIcon(exitBtn, \"home\"", source);
            StringAssert.Contains("SetButtonIcon(brightnessBtn, \"brightness\"", source);
            StringAssert.Contains("SetButtonIcon(volumeBtn, \"volume-notice\"", source);
            StringAssert.Contains("SetButtonIcon(threeDBtn, \"vr-glasses\"", source);
            StringAssert.Contains("SetButtonIcon(settingsBtn, \"setting-two\"", source);
            StringAssert.DoesNotContain("SetButtonIcon(exitBtn, \"exit\"", source);
            StringAssert.DoesNotContain("SetButtonIcon(volumeBtn, \"volume\"", source);
            StringAssert.DoesNotContain("SetButtonIcon(threeDBtn, \"stereo3d\"", source);
            StringAssert.DoesNotContain("SetButtonIcon(settingsBtn, \"settings\"", source);
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
            StringAssert.Contains("无3D", uiSource);
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
        public void VRUIManager_BindsSeeThroughButtonToPassthroughToggle()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("seeThroughBtn.onClick.AddListener(OnSeeThroughBtnClicked)", source);
            StringAssert.Contains("seeThroughBtn.onClick.RemoveListener(OnSeeThroughBtnClicked)", source);
            StringAssert.Contains("PicoPassthroughModeService", source);
            StringAssert.Contains("OnSeeThroughBtnClicked", source);
        }

        [Test]
        public void VRUIManager_SeeThroughButtonReflectsPassthroughState()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            var managerObject = new GameObject("Passthrough Button Manager");
            var seeThroughButtonObject = new GameObject(
                "SeeThroughButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button));

            try
            {
                Component manager = managerObject.AddComponent(managerType);
                var seeThroughButton = seeThroughButtonObject.GetComponent<UnityEngine.UI.Button>();
                managerType.GetField("seeThroughBtn").SetValue(manager, seeThroughButton);

                managerType.GetMethod("SetSeeThroughButtonPassthroughVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { true, true });
                Assert.Greater(seeThroughButtonObject.GetComponent<UnityEngine.UI.Image>().color.a, 0.2f);
                Assert.IsTrue(seeThroughButton.interactable);

                managerType.GetMethod("SetSeeThroughButtonPassthroughVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { false, true });
                Assert.AreEqual(0f, seeThroughButtonObject.GetComponent<UnityEngine.UI.Image>().color.a);
                Assert.IsTrue(seeThroughButton.interactable);

                managerType.GetMethod("SetSeeThroughButtonPassthroughVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { false, false });
                Assert.IsFalse(seeThroughButton.interactable);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(seeThroughButtonObject);
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
            StringAssert.Contains("titleScroller.SetText(GetDisplayTitle(media))", source);
            StringAssert.Contains("systemTimeText.enabled = true", source);
            StringAssert.Contains("batteryText.enabled = true", source);
            StringAssert.Contains("currentTimeText.enabled = true", source);
            StringAssert.Contains("totalTimeText.enabled = true", source);
            StringAssert.Contains("speedBtnText.enabled = true", source);
            StringAssert.Contains("string.IsNullOrWhiteSpace(media.Title)", source);
            StringAssert.Contains("XrUiText.Get(XrUiTextKey.TitlePlaceholder)", source);
            StringAssert.Contains("XrUiText.Get(XrUiTextKey.UnknownVideo)", source);
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
        public void SystemVerticalSliderPopup_SliderRootHasTransparentRaycastHitArea()
        {
            string prefab = File.ReadAllText(Path.Combine(Application.dataPath, "Prefabs/UI/SystemVerticalSliderPopup.prefab"));
            int sliderIndex = prefab.IndexOf("m_Name: SystemSlider", StringComparison.Ordinal);
            int backgroundIndex = prefab.IndexOf("m_Name: Background", sliderIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(sliderIndex, 0, "System slider popup prefab should contain a SystemSlider object.");
            Assert.Greater(backgroundIndex, sliderIndex, "SystemSlider should contain a Background child after the root object.");

            string sliderBlock = prefab.Substring(sliderIndex, backgroundIndex - sliderIndex);
            StringAssert.Contains("UnityEngine.UI::UnityEngine.UI.Image", sliderBlock);
            StringAssert.Contains("m_Color: {r: 1, g: 1, b: 1, a: 0}", sliderBlock);
            StringAssert.Contains("m_RaycastTarget: 1", sliderBlock);
        }

        [Test]
        public void VRUIManager_SystemSliderShowsPercentAndBoostedVolumeRange()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.DoesNotContain("SystemSliderBoostedLength", source);
            StringAssert.Contains("SystemVolumeBoostMarkerName = \"VolumeBoost100Marker\"", source);
            StringAssert.Contains("SystemSliderStickPercentPerSecond", source);
            StringAssert.Contains("private TextMeshProUGUI systemSliderPercentText", source);
            StringAssert.Contains("private GameObject systemSliderBoostMarker", source);
            StringAssert.Contains("ConfigureSystemSliderForMode(mode)", source);
            StringAssert.Contains("UpdateSystemSliderPercentLabel", source);
            StringAssert.Contains("UpdateVolumeBoostMarker", source);
            StringAssert.Contains("HandleSystemSliderStickInput()", source);
            StringAssert.Contains("TryGetSystemSliderStickAxis(out float axisY)", source);
            StringAssert.Contains("IsSelfOrChildOf(target, systemSliderPopup)", source);
            StringAssert.Contains("GetSystemSliderMaxPercent", source);
            StringAssert.Contains("sliderLength = SystemSliderLength", source);
            StringAssert.Contains("textRect.anchoredPosition = new Vector2(0f, SystemSliderPopupHeight * 0.5f + SystemSliderPercentTextGap + SystemSliderPercentTextHeight * 0.5f)", source);
            StringAssert.Contains("ReadVolumePercentNormalized", source);
            StringAssert.Contains("SetVolumePercentNormalized", source);
            StringAssert.Contains("VlcPlaybackBridge.GetVolumePercent()", source);
            StringAssert.Contains("VlcPlaybackBridge.SetVolumePercent(percent)", source);
        }

        [Test]
        public void VRUIManager_OnlyUpdatesInspectorAuthoredBatteryIconAndText()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("ConfigureFixedSystemStatusText(systemTimeText, SystemTimeTextWidth)", source);
            StringAssert.Contains("public Image batteryIcon", source);
            StringAssert.Contains("private Image batteryFill", source);
            StringAssert.Contains("SetBatteryVisuals(batteryPercent)", source);
            StringAssert.Contains("EnsureBatteryFillImage()", source);
            StringAssert.Contains("LoadIconWithFallback(BatteryShellIcon, BatteryShellIcon)", source);
            StringAssert.Contains("batteryIcon.sprite = sprite", source);
            StringAssert.Contains("ConfigureBatteryTextNoOutline()", source);
            StringAssert.Contains("ConfigureTextEdgeClarity(batteryText)", source);
            StringAssert.DoesNotContain("EnsureBatteryIcon()", source);
            StringAssert.DoesNotContain("ConfigureBatteryStatusText", source);
            StringAssert.DoesNotContain("ConfigureBatteryIconLayout", source);
            StringAssert.DoesNotContain("new GameObject(\"BatteryIcon\"", source);
            StringAssert.DoesNotContain("batteryText.transform.SetParent", source);
            StringAssert.DoesNotContain("batteryIcon.transform.SetParent", source);
            StringAssert.DoesNotContain("GetPaddingCroppedBatterySprite", source);
        }

        [Test]
        public void VRUIManager_UsesBatteryEmptyShellWithTransparentFill()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("BatteryShellIcon = \"battery-empty\"", source);
            StringAssert.Contains("BatteryFillObjectName = \"BatteryFill\"", source);
            StringAssert.Contains("BatteryFillAlpha = 0.5f", source);
            StringAssert.Contains("BatteryStatusFontSize = 20f", source);
            StringAssert.Contains("BatteryBodyInnerMinX = 0.13f", source);
            StringAssert.Contains("BatteryBodyInnerMaxX = 0.79f", source);
            StringAssert.Contains("BatteryBodyInnerHeightRatio = 0.34f", source);
            StringAssert.Contains("BatteryLowThresholdPercent = 20", source);
            StringAssert.Contains("BatteryLowFillColor = new Color(1f, 0.53333336f, 0f, BatteryFillAlpha)", source);
            StringAssert.Contains("BatteryNormalFillColor = new Color(1f, 1f, 1f, BatteryFillAlpha)", source);
            StringAssert.Contains("batteryText.fontSize = BatteryStatusFontSize", source);
            StringAssert.Contains("LayoutBatteryStatusText(contentRect)", source);
            StringAssert.Contains("GetBatteryBodyContentRect()", source);
            StringAssert.Contains("GetPreservedAspectSpriteRect(iconRect)", source);
            StringAssert.Contains("batteryFill.color = batteryPercent < BatteryLowThresholdPercent ? BatteryLowFillColor : BatteryNormalFillColor", source);
            StringAssert.Contains("batteryFill.gameObject.SetActive(batteryPercent >= 0)", source);
            StringAssert.Contains("Mathf.Clamp01(batteryPercent / 100f)", source);
            StringAssert.Contains("LayoutBatteryFill(fillRect, normalized)", source);
            StringAssert.DoesNotContain("ResolveBatteryIconName", source);
            StringAssert.DoesNotContain("BatteryLowIcon", source);
            StringAssert.DoesNotContain("BatteryMediumIcon", source);
            StringAssert.DoesNotContain("BatteryFullIcon", source);
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
        public void VRUIManager_AwakeHidesPanelBeforeStart()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string awakeMethod = ExtractMethod(source, "Awake", "private void");

            int awakeIndex = source.IndexOf("private void Awake()", StringComparison.Ordinal);
            int startIndex = source.IndexOf("private void Start()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(awakeIndex, 0);
            Assert.Greater(startIndex, awakeIndex);
            StringAssert.Contains("SetPanelVisibility(false);", awakeMethod);
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
        public void VRUIManager_ShowsLoadingOverlayForOpeningAndBufferingStates()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string startMethod = ExtractMethod(source, "Start", "private void");
            string destroyMethod = ExtractMethod(source, "OnDestroy", "private void");
            string statusMethod = ExtractMethod(source, "OnStatusChanged", "private void");
            string loadingStatusMethod = ExtractMethod(source, "IsLoadingStatus", "private static bool");
            string bufferingMethod = ExtractMethod(source, "OnBuffering", "private void");

            StringAssert.Contains("playbackService.OnBuffering += OnBuffering", startMethod);
            StringAssert.Contains("playbackService.OnBuffering -= OnBuffering", destroyMethod);
            StringAssert.Contains("SetLoadingVisible(IsLoadingStatus(status));", statusMethod);
            StringAssert.Contains("status == XRVLC.Media.PlayerStatus.Opening", loadingStatusMethod);
            StringAssert.Contains("status == XRVLC.Media.PlayerStatus.Buffering", loadingStatusMethod);
            StringAssert.Contains("SetLoadingVisible(buffering < 100f)", bufferingMethod);
        }

        [Test]
        public void VRUIManager_UsesEditorCreatedLoadingOverlayWithLoadingIconFallback()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string ensureMethod = ExtractMethod(source, "EnsureLoadingOverlay", "private void");
            string updateMethod = ExtractMethod(source, "UpdateLoadingAnimation", "private void");
            string resolveMethod = ExtractMethod(source, "ResolveLoadingOverlayReferences", "private void");
            string findMethod = ExtractMethod(source, "FindSceneLoadingOverlay", "private static Transform");
            string spriteMethod = ExtractMethod(source, "LoadLoadingSprite", "private static Sprite");

            StringAssert.Contains("private const string LoadingIconResourceName = \"loading-four\"", source);
            StringAssert.Contains("[SerializeField] private GameObject loadingOverlay", source);
            StringAssert.Contains("[SerializeField] private RectTransform loadingSpinnerTransform", source);
            StringAssert.Contains("[SerializeField] private Image loadingSpinnerImage", source);
            StringAssert.Contains("ResolveLoadingOverlayReferences();", ensureMethod);
            StringAssert.DoesNotContain("new GameObject(\"LoadingOverlay\"", ensureMethod);
            StringAssert.DoesNotContain("GameObject.Find(\"LoadingOverlay\")", resolveMethod);
            StringAssert.Contains("Resources.FindObjectsOfTypeAll<Transform>()", findMethod);
            StringAssert.Contains("canvasGroup.blocksRaycasts = false", ensureMethod);
            StringAssert.Contains("loadingSpinnerImage.raycastTarget = false", ensureMethod);
            StringAssert.Contains("loadingSpinnerImage.sprite = LoadLoadingSprite()", ensureMethod);
            StringAssert.DoesNotContain("UpdateLoadingOverlayPose", source);
            StringAssert.DoesNotContain("LoadingPanelUpOffsetMeters", source);
            StringAssert.DoesNotContain("LoadingDistanceFromCameraMeters", source);
            StringAssert.DoesNotContain("GetControlPanelWorldCenter", source);
            StringAssert.DoesNotContain("loadingOverlay.transform.position", source);
            StringAssert.DoesNotContain("loadingOverlay.transform.LookAt", source);
            StringAssert.Contains("loadingSpinnerTransform.Rotate(0f, 0f, LoadingSpinnerDegreesPerSecond * Time.unscaledDeltaTime)", updateMethod);
            StringAssert.Contains("Resources.Load<Sprite>(IconResourcePath + LoadingIconResourceName)", spriteMethod);
            StringAssert.Contains("CreateGeneratedLoadingSpinnerSprite()", spriteMethod);
        }

        [Test]
        public void VRUIManager_KeepsLoadingOverlayEditorAuthoredTransform()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string ensureMethod = ExtractMethod(source, "EnsureLoadingOverlay", "private void");
            string updateMethod = ExtractMethod(source, "UpdateLoadingAnimation", "private void");

            StringAssert.Contains("canvas.renderMode = RenderMode.WorldSpace", ensureMethod);
            StringAssert.Contains("canvas.worldCamera = GetLoadingCamera()", ensureMethod);
            StringAssert.Contains("loadingOverlay.transform.SetAsLastSibling()", source);
            StringAssert.DoesNotContain("UpdateLoadingOverlayPose();", updateMethod);
            StringAssert.DoesNotContain("overlayRect.localScale = Vector3.one * LoadingWorldCanvasScale", ensureMethod);
            StringAssert.DoesNotContain("overlayRect.anchoredPosition", ensureMethod);
            StringAssert.DoesNotContain("overlayRect.localPosition", ensureMethod);
        }

        [Test]
        public void VRUIManager_PlaybackPanelShowUsesActiveVideoSelectionGate()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string showPanelMethod = ExtractMethod(source, "ShowPanelAndScheduleHide", "private void");
            string toggleMethod = ExtractMethod(source, "TogglePanel", "public void");
            string gateMethod = ExtractMethod(source, "CanShowPlaybackPanel", "private bool");

            StringAssert.Contains("VlcPlaybackBridge.TryHasActivePlaybackSelection(out bool hasActiveVideoSelection)", gateMethod);
            StringAssert.Contains("return hasActiveVideoSelection", gateMethod);
            StringAssert.Contains("return playbackService.CurrentMedia != null", gateMethod);
            StringAssert.DoesNotContain("HasCurrentMediaOrPlaylistItems", gateMethod);
            StringAssert.Contains("if (!mediaReady && !CanShowPlaybackPanel())", showPanelMethod);
            StringAssert.Contains("SetPanelVisibility(false)", showPanelMethod);
            StringAssert.Contains("if (!isPanelVisible && !CanShowPlaybackPanel())", toggleMethod);
            StringAssert.Contains("SetPanelVisibility(!isPanelVisible)", toggleMethod);
        }

        [Test]
        public void MediaParsePromotionShowsPanelBeforeSurfaceRebuildWithoutGeometryRetrigger()
        {
            string uiSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));
            string playbackSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs"));
            string promoteMethod = ExtractMethod(playbackSource, "StartNextParsedPlaybackRequest", "private void");
            string updateMediaMethod = ExtractMethod(uiSource, "UpdateMediaInfo", "private void");
            string geometryMethod = ExtractMethod(playbackSource, "SetManualVideoGeometry", "public void");

            int backgroundIndex = promoteMethod.IndexOf("videoScreen.EnterPlaybackBackground()", System.StringComparison.Ordinal);
            int mediaChangedIndex = promoteMethod.IndexOf("OnMediaChanged?.Invoke(CurrentMedia, 0)", System.StringComparison.Ordinal);
            int rebuildIndex = promoteMethod.IndexOf("RequestRebuildLayer(videoSize, request.MediaRequestId)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(backgroundIndex, 0);
            Assert.Greater(mediaChangedIndex, backgroundIndex);
            Assert.Greater(rebuildIndex, mediaChangedIndex);

            StringAssert.Contains("playbackService.OnMediaChanged += UpdateMediaInfo", uiSource);
            StringAssert.Contains("ShowPanelAndScheduleHide(mediaReady: true)", updateMediaMethod);
            StringAssert.Contains("SchedulePanelHide()", uiSource);
            StringAssert.Contains("private const float PanelVisibleDuration = 3f", uiSource);
            StringAssert.DoesNotContain("OnMediaChanged?.Invoke", geometryMethod);
            StringAssert.DoesNotContain("ShowPanelAndScheduleHide", geometryMethod);
            StringAssert.DoesNotContain("VideoPending", uiSource);
            StringAssert.DoesNotContain("VideoReady", uiSource);
        }

        [Test]
        [Ignore("Track dropdowns now use XrDropdown; covered by XrDropdownUnifiedTests.")]
        public void VRUIManager_HiddenPanelTriggerReleaseShowsPanelEvenWhenTrackDropdownIsOpen()
        {
            Type managerType = Type.GetType("VRUIManager, Assembly-CSharp");
            Assert.IsNotNull(managerType, "VRUIManager type should be available in Assembly-CSharp.");

            MethodInfo method = managerType.GetMethod("HandleTriggerReleasedEdge", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "VRUIManager should handle a trigger release edge in a testable helper.");

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
        public void VRUIManager_AutoHideTimerWaitsWhilePointerIsOverManagedUiOrSecondaryPanelOpen()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("IsPointerOverManagedUi()", source);
            StringAssert.Contains("IsAnySecondaryPanelOpen()", source);
            StringAssert.Contains("if (IsPointerOverManagedUi() || IsAnySecondaryPanelOpen())", source);
            StringAssert.Contains("_hideTimer -= Time.deltaTime", source);
        }

        [Test]
        public void VRUIManager_TriggerPanelClickRunsOnShortReleaseAndSkipsLongPressRelease()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("HandleTriggerReleasedEdge()", source);
            StringAssert.DoesNotContain("HandleTriggerPressedEdge();", source);
            StringAssert.Contains("XRVLC.ShortcutInputState.TriggerFastRateHoldSeconds", source);
            StringAssert.Contains("if (!_triggerLongPressReleasePending)", source);
            StringAssert.Contains("new XrUiEvent(XrUiEventType.TriggerReleased", source);
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
        public void VRUIManager_TrackDropdownsUseActiveVlcSnapshotsWithoutLongLivedTrackCache()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            StringAssert.Contains("ChooseSubtitleTrackOptionLabel", source);
            StringAssert.Contains("RefreshAudioTracksDropdownFromVlc()", source);
            StringAssert.Contains("RefreshSubtitleTracksDropdownFromVlc()", source);
            StringAssert.Contains("playbackService.GetAudioTrackSnapshotFromVlc()", source);
            StringAssert.Contains("playbackService.GetSubtitleTrackSnapshotFromVlc()", source);
            StringAssert.DoesNotContain("playbackService.GetTrackSnapshotFromVlc()", source);
            StringAssert.Contains("_openAudioTracks", source);
            StringAssert.Contains("_openSubtitleTracks", source);
            StringAssert.DoesNotContain("currentAudioTracks", source);
            StringAssert.DoesNotContain("currentSubtitleTracks", source);
            StringAssert.Contains("ResolveSelectedTrackIndex", source);
            StringAssert.Contains("tracks[i].IsSelected", source);
            StringAssert.DoesNotContain("playbackService?.CurrentMedia?.AudioTrack", source);
            StringAssert.DoesNotContain("playbackService?.CurrentMedia?.SpuTrack", source);
            StringAssert.Contains("playbackService?.SetAudioTrack(_openAudioTracks[dropdownIndex].Id)", source);
            StringAssert.Contains("playbackService?.SetSubtitleTrack(_openSubtitleTracks[trackIndex].Id)", source);

            string audioRefresh = ExtractMethodBody(source, "private void RefreshAudioTracksDropdownFromVlc()");
            StringAssert.Contains("playbackService.GetAudioTrackSnapshotFromVlc()", audioRefresh);
            StringAssert.DoesNotContain("GetSubtitleTrackSnapshotFromVlc", audioRefresh);

            string subtitleRefresh = ExtractMethodBody(source, "private void RefreshSubtitleTracksDropdownFromVlc()");
            StringAssert.Contains("playbackService.GetSubtitleTrackSnapshotFromVlc()", subtitleRefresh);
            StringAssert.DoesNotContain("GetAudioTrackSnapshotFromVlc", subtitleRefresh);

            int chooseBranchStart = source.IndexOf("if (dropdownIndex == 0)", System.StringComparison.Ordinal);
            int chooseBranchEnd = source.IndexOf("int trackIndex = dropdownIndex - 1", chooseBranchStart, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(chooseBranchStart, 0);
            Assert.Greater(chooseBranchEnd, chooseBranchStart);
            string chooseBranch = source.Substring(chooseBranchStart, chooseBranchEnd - chooseBranchStart);
            StringAssert.Contains("VlcPlaybackBridge.OpenSubtitlePicker()", chooseBranch);
            StringAssert.DoesNotContain("SetSubtitleTrack", chooseBranch);
        }

        [Test]
        public void VRUIManager_TrackButtonsRefreshBeforeCheckingDropdownInteractable()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

            string audioClick = ExtractMethodBody(source, "private void OnAudioTrackBtnClicked()");
            int audioRefreshIndex = audioClick.IndexOf("RefreshAudioTracksDropdownFromVlc()", StringComparison.Ordinal);
            int audioShowCheckIndex = audioClick.IndexOf("ShouldShowTrackDropdown(audioTrackDropdown)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(audioRefreshIndex, 0, "Audio button should refresh tracks from VLC before opening.");
            Assert.GreaterOrEqual(audioShowCheckIndex, 0, "Audio button should still use the shared show predicate.");
            Assert.Less(
                audioRefreshIndex,
                audioShowCheckIndex,
                "Audio dropdown must query VLC before checking interactable; placeholder dropdowns start non-interactable.");

            string subtitleClick = ExtractMethodBody(source, "private void OnSubtitleBtnClicked()");
            int subtitleRefreshIndex = subtitleClick.IndexOf("RefreshSubtitleTracksDropdownFromVlc()", StringComparison.Ordinal);
            int subtitleShowCheckIndex = subtitleClick.IndexOf("ShouldShowTrackDropdown(subtitleTrackDropdown)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(subtitleRefreshIndex, 0, "Subtitle button should refresh tracks from VLC before opening.");
            Assert.GreaterOrEqual(subtitleShowCheckIndex, 0, "Subtitle button should still use the shared show predicate.");
            Assert.Less(
                subtitleRefreshIndex,
                subtitleShowCheckIndex,
                "Subtitle dropdown must query VLC before checking interactable; placeholder dropdowns start non-interactable.");
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
                Assert.Greater(GetButtonAlpha(geometryMenu, "无3D"), 0f);
                Assert.Greater(GetButtonAlpha(geometryMenu, "无曲面"), 0f);
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "360全景"));
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "鱼眼180"));
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
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "鱼眼180"));
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "无3D"));
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "无曲面"));

                managerType.GetMethod("SetGeometryProjection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { XRVLC.VideoProjection.Fisheye180 });

                Assert.Greater(GetButtonAlpha(geometryMenu, "鱼眼180"), 0f);
                Assert.AreEqual(0f, GetButtonAlpha(geometryMenu, "360全景"));
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

            StringAssert.Contains("XrUiTextKey.SettingsTabPlayback", source);
            StringAssert.Contains("XrUiTextKey.SettingsTabGesture", source);
            StringAssert.Contains("CreateTabButton(tabBar, SettingsTab.Gesture, XrUiText.Get(XrUiTextKey.SettingsTabGesture))", source);
            StringAssert.DoesNotContain("CreateTabButton(tabBar, SettingsTab.Gesture, \"手势\")", source);
            StringAssert.Contains("XrUiTextKey.SettingsTabSubtitle", source);
            StringAssert.Contains("XrUiTextKey.SettingsTabVideo", source);
            StringAssert.Contains("XrUiTextKey.SettingsTabAudio", source);
            StringAssert.Contains("ShortcutSettingsService.LoadShortcutMappings", source);
            StringAssert.Contains("ShortcutSettingsService.SaveShortcutMappings", source);
            StringAssert.Contains("CreatePlaybackRateStepper(root.transform)", source);
            StringAssert.Contains("UpdatePlaybackRateControl()", source);
            StringAssert.DoesNotContain("CreateButton(row, \"0.5x\"", source);
            StringAssert.DoesNotContain("CreateGestureSaveRow", source);
            StringAssert.DoesNotContain("保存手势设置", source);
            StringAssert.Contains("SubtitleRenderMode.Native", source);
            StringAssert.Contains("SubtitleRenderMode.Spatial", source);
            StringAssert.DoesNotContain("SubtitleRenderMode.Off", source);
            StringAssert.Contains("ApplyVideoAspectRatio", source);
            StringAssert.DoesNotContain("ApplyVideoScaleMode", source);
            StringAssert.Contains("_audioBoostSwitchButton", source);
            StringAssert.Contains("CreateSwitchRow(root.transform, \"AudioBoostSwitchRow\", XrUiText.Get(XrUiTextKey.SettingsAudioBoost), ToggleAudioBoost)", source);
            StringAssert.DoesNotContain("MixToMonoSwitchRow", source);
            StringAssert.DoesNotContain("ToggleMixToMono", source);
            StringAssert.DoesNotContain("ShouldMixAudioToMono", source);
            StringAssert.DoesNotContain("CreateSectionLabel(root.transform, \"声道输出\")", source);
            StringAssert.DoesNotContain("CreateButton(row, \"立体声\"", source);
            StringAssert.DoesNotContain("CreateButton(row, \"混合单声道\"", source);
            StringAssert.Contains("VlcPlaybackBridge.IsAudioBoostEnabled()", source);
            StringAssert.Contains("VlcPlaybackBridge.SetAudioBoostEnabled(enabled)", source);
            StringAssert.DoesNotContain("PlaybackUiSettingsService.SaveSubtitleRenderMode", source);
            StringAssert.DoesNotContain("PlaybackUiSettingsService.SaveAudioChannelMode", source);
            StringAssert.DoesNotContain("180全景", source);
            StringAssert.DoesNotContain("360全景", source);
            StringAssert.DoesNotContain("上下3D", source);
            StringAssert.DoesNotContain("左右3D", source);

            int playbackStart = source.IndexOf("private void BuildPlaybackTab", System.StringComparison.Ordinal);
            int gestureStart = source.IndexOf("private void BuildGestureTab", playbackStart, System.StringComparison.Ordinal);
            int subtitleStart = source.IndexOf("private void BuildSubtitleTab", System.StringComparison.Ordinal);
            int videoStart = source.IndexOf("private void BuildVideoTab", subtitleStart, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(playbackStart, 0);
            Assert.Greater(gestureStart, playbackStart);
            Assert.GreaterOrEqual(subtitleStart, 0);
            Assert.Greater(videoStart, subtitleStart);

            string playbackBlock = source.Substring(playbackStart, gestureStart - playbackStart);
            string subtitleBlock = source.Substring(subtitleStart, videoStart - subtitleStart);
            StringAssert.Contains("CreatePlaybackRateStepper(root.transform)", playbackBlock);
            StringAssert.DoesNotContain("PlaybackRateStepperRow", subtitleBlock);
            StringAssert.DoesNotContain("CreatePlaybackRateStepper", subtitleBlock);
        }

        [Test]
        public void SettingsMenuController_UsesVlcOrangeThinCapsuleSwitch()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Settings/SettingsMenuController.cs"));

            StringAssert.Contains("VlcOrange = new Color(1f, 0.53333336f, 0f, 1f)", source);
            StringAssert.Contains("SwitchTrackLength = SwitchWidth * 0.75f", source);
            StringAssert.Contains("SwitchTrackThickness = SwitchKnobSize * 0.5f", source);
            StringAssert.Contains("SwitchKnobTravel = (SwitchWidth - SwitchKnobSize) * 0.5f", source);
            StringAssert.Contains("new GameObject(\"Track\", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectImage))", source);
            StringAssert.Contains("trackRect.sizeDelta = new Vector2(SwitchTrackLength, SwitchTrackThickness)", source);
            StringAssert.Contains("trackRect.SetAsFirstSibling()", source);
            StringAssert.Contains("knob.color = VlcOrange", source);
            StringAssert.Contains("track.color = enabled ? VlcOrange : SwitchOffTrackColor", source);
            StringAssert.Contains("button.targetGraphic = hitArea", source);
            StringAssert.DoesNotContain("SwitchOnTrackColor = new Color(0.18f, 0.58f, 0.95f, 1f)", source);
            StringAssert.DoesNotContain("SwitchKnobColor = new Color(1f, 1f, 1f, 0.96f)", source);
        }

        [Test]
        public void XrStepperControl_UsesSoftKeyboardWithVisibleDecimalKey()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Common/XrStepperControl.cs"));

            StringAssert.Contains("ConfigureNumericKeyboard(inputField)", source);
            StringAssert.Contains("ConfigureNumericKeyboard(field)", source);
            StringAssert.Contains("field.contentType = TMP_InputField.ContentType.Custom", source);
            StringAssert.Contains("field.inputType = TMP_InputField.InputType.Standard", source);
            StringAssert.Contains("field.keyboardType = TouchScreenKeyboardType.EmailAddress", source);
            StringAssert.Contains("field.characterValidation = TMP_InputField.CharacterValidation.None", source);

            string prefab = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/UI/XrStepperControl.prefab"));
            StringAssert.Contains("m_ContentType: 9", prefab);
            StringAssert.Contains("m_KeyboardType: 7", prefab);
            StringAssert.Contains("m_CharacterValidation: 0", prefab);
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
        public void SettingsMenuController_UsesSingleAspectRatioDropdownForVideoSettings()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/Settings/SettingsMenuController.cs"));
            string videoTab = ExtractMethodBody(source, "private void BuildVideoTab");

            StringAssert.Contains("CreateVideoAspectRatioDropdown(root.transform)", videoTab);
            StringAssert.Contains("XrUiTextKey.SettingsAspectRatio", source);
            StringAssert.Contains("VideoAspectRatioDropdown", source);
            StringAssert.Contains("VideoAspectRatioRow", source);
            StringAssert.Contains("XrDropdown _videoAspectRatioDropdown", source);
            StringAssert.Contains("ApplyVideoAspectRatioFromDropdown", source);
            StringAssert.Contains("XrUiTextKey.SettingsAspectRatioAuto", source);
            StringAssert.Contains("\"16:9\"", source);
            StringAssert.Contains("\"4:3\"", source);
            StringAssert.Contains("\"16:10\"", source);
            StringAssert.Contains("\"2.21:1\"", source);
            StringAssert.Contains("\"2.35:1\"", source);
            StringAssert.Contains("\"2.39:1\"", source);
            StringAssert.Contains("\"5:4\"", source);
            StringAssert.DoesNotContain("画面拉伸裁剪", videoTab);
            StringAssert.DoesNotContain("缩放方式", videoTab);
            StringAssert.DoesNotContain("显示比例", videoTab);
            StringAssert.DoesNotContain("VideoScaleModeRow", source);
            StringAssert.DoesNotContain("CreateVideoScaleModeButton", source);
            StringAssert.DoesNotContain("CreateVideoAspectRatioButton", source);
        }

        [Test]
        public void PlaybackUiSettingsService_PersistsAspectRatioSettingsAndMigratesLegacyRatio()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Services/Settings/PlaybackUiSettingsService.cs"));

            StringAssert.Contains("KeyVideoRatio = \"video_ratio\"", source);
            StringAssert.Contains("KeyVideoAspectRatio = \"video_aspect_ratio\"", source);
            StringAssert.Contains("LoadVideoAspectRatio", source);
            StringAssert.Contains("SaveVideoAspectRatio", source);
            StringAssert.Contains("MigrateLegacyVideoRatioIfNeeded", source);
            StringAssert.Contains("LegacyVideoRatioMigrated", source);
            StringAssert.Contains("ToLegacyAspectRatio", source);
            StringAssert.Contains("ToAspectRatioValue", source);
            StringAssert.Contains("ParseAspectRatio", source);
            StringAssert.Contains("\"16:9\"", source);
            StringAssert.Contains("\"4:3\"", source);
            StringAssert.Contains("\"16:10\"", source);
            StringAssert.Contains("\"2.21:1\"", source);
            StringAssert.Contains("\"2.35:1\"", source);
            StringAssert.Contains("\"2.39:1\"", source);
            StringAssert.Contains("\"5:4\"", source);
            StringAssert.Contains("VlcPreferenceStore.GetInt", source);
            StringAssert.DoesNotContain("KeySubtitleRenderMode", source);
            StringAssert.DoesNotContain("KeyAudioChannelMode", source);
            StringAssert.DoesNotContain("SaveSubtitleRenderMode", source);
            StringAssert.DoesNotContain("SaveAudioChannelMode", source);
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
            StringAssert.DoesNotContain("SetAudioChannelMode", source);
            StringAssert.DoesNotContain("ShouldMixAudioToMono", source);
            StringAssert.Contains("public static bool IsAudioBoostEnabled()", source);
            StringAssert.Contains("bridge.CallStatic<bool>(\"isAudioBoostEnabled\")", source);
            StringAssert.Contains("public static void SetAudioBoostEnabled(bool enabled)", source);
            StringAssert.Contains("bridge.CallStatic(\"setAudioBoostEnabled\", enabled)", source);
            StringAssert.Contains("public static int GetVolumePercent()", source);
            StringAssert.Contains("bridge.CallStatic<int>(\"getVolumePercent\")", source);
            StringAssert.Contains("public static void SetVolumePercent(int percent)", source);
            StringAssert.Contains("bridge.CallStatic(\"setVolumePercent\", percent)", source);
        }

        [Test]
        public void AndroidPlaybackServiceBridge_ExposesPlaybackSettingsMethods()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string bridgePath = Path.Combine(
                projectRoot,
                "../vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt");
            string source = File.ReadAllText(bridgePath);

            StringAssert.Contains("fun setVideoScale", source);
            StringAssert.Contains("VIDEO_RATIO", source);
            StringAssert.Contains("MediaPlayer.ScaleType.entries", source);
            StringAssert.DoesNotContain("fun setAudioChannelMode", source);
            StringAssert.DoesNotContain("fun shouldMixAudioToMono", source);
            StringAssert.Contains("fun isAudioBoostEnabled(): Boolean", source);
            StringAssert.Contains("fun setAudioBoostEnabled(enabled: Boolean)", source);
            StringAssert.Contains("putSingle(KEY_AUDIO_BOOST, enabled)", source);
            StringAssert.Contains("fun getVolumePercent(): Int", source);
            StringAssert.Contains("fun setVolumePercent(percent: Int)", source);
            StringAssert.Contains("private var lastUnityVolumePercent = 100", source);
            StringAssert.Contains("lastUnityVolumePercent = safePercent", source);
            StringAssert.Contains("if (isAudioBoostEnabled() && lastUnityVolumePercent > 100)", source);
            StringAssert.Contains("coerceIn(0, if (isAudioBoostEnabled()) 200 else 100)", source);
            StringAssert.DoesNotContain("xr_audio_channel_mode", source);
            StringAssert.DoesNotContain("putSingle(XR_AUDIO_CHANNEL_MODE", source);
        }

        [Test]
        public void AndroidPlaylistManager_DoesNotApplyMonoAudioFilter()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string playlistManagerPath = Path.Combine(
                projectRoot,
                "../vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt");
            string source = File.ReadAllText(playlistManagerPath);

            StringAssert.DoesNotContain("PlaybackServiceBridge.shouldMixAudioToMono()", source);
            StringAssert.DoesNotContain("media.addOption(\":audio-filter=mono\")", source);
            StringAssert.DoesNotContain("Enabled mono downmix audio filter", source);
            StringAssert.DoesNotContain(":stereo-mode=6", source);
            StringAssert.DoesNotContain("--stereo-mode=6", source);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, $"Missing method signature: {signature}");

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.Greater(bodyStart, signatureIndex, $"Missing method body: {signature}");

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                    depth--;

                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }

            Assert.Fail($"Unterminated method body: {signature}");
            return string.Empty;
        }

        private static string ExtractMethod(string source, string methodName, string returnType)
        {
            return ExtractMethodBody(source, $"{returnType} {methodName}");
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
