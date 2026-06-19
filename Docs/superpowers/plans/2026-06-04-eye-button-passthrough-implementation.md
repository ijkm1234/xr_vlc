# 面板眼睛按钮透视模式 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让播放面板现有 `eyeBtn` 可以一键开启或关闭 PICO Video See Through 透视背景。

**Architecture:** 新增 `PicoPassthroughModeService` 隔离 PICO API 和运行时状态；`VRUIManager` 只负责绑定 `eyeBtn`、调用服务、更新按钮视觉状态；项目资源开启 `videoSeeThrough` 构建配置。

**Tech Stack:** Unity 6、C#、UGUI、PICO Unity Integration SDK、Unity Test Framework Editor tests。

---

## 文件结构

- 新建 `Assets/Scripts/Infrastructure/Pico/PicoPassthroughModeService.cs`：封装透视状态、PICO API 调用、Editor no-op、恢复时重放状态。
- 修改 `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`：为 `eyeBtn` 注册点击事件，查找或创建透视服务，更新按钮状态。
- 修改 `Assets/Resources/PXR_ProjectSetting.asset`：把 `videoSeeThrough` 从 `0` 改成 `1`。
- 修改 `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`：增加源码级和反射测试，覆盖按钮绑定、按钮高亮、项目设置。

## Task 1: 写透视服务和 UI 绑定的失败测试

**Files:**
- Modify: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`
- Read: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`
- Read: `Assets/Resources/PXR_ProjectSetting.asset`

- [ ] **Step 1: 写失败测试**

在 `VRUIManagerIconStyleTests` 中追加三个测试：

```csharp
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
```

- [ ] **Step 2: 运行测试确认失败**

Run: `Unity Test Runner EditMode: XRVLC.Tests.VRUIManagerIconStyleTests`

Expected: 至少上述新增测试失败，因为 `OnEyeBtnClicked`、`PicoPassthroughModeService`、`SetEyeButtonPassthroughVisual` 和 `videoSeeThrough: 1` 还不存在。

## Task 2: 实现 `PicoPassthroughModeService`

**Files:**
- Create: `Assets/Scripts/Infrastructure/Pico/PicoPassthroughModeService.cs`
- Test: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`

- [ ] **Step 1: 新建服务脚本**

```csharp
using System;
using UnityEngine;

#if UNITY_ANDROID
using Unity.XR.PXR;
#endif

#if UNITY_ANDROID && PICO_OPENXR_SDK
using Unity.XR.OpenXR.Features.PICOSupport;
#endif

namespace XRVLC.Infrastructure.Pico
{
    public class PicoPassthroughModeService : MonoBehaviour
    {
        private bool _isEnabled;
        private bool _warningLogged;

        public event Action<bool> StateChanged;

        public bool IsEnabled => _isEnabled;

        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && PICO_OPENXR_SDK
                return PassthroughFeature.isExtensionEnable && PassthroughFeature.IsPassthroughSupported();
#elif UNITY_ANDROID
                return true;
#else
                return true;
#endif
            }
        }

        public bool Toggle()
        {
            return SetEnabled(!_isEnabled);
        }

        public bool SetEnabled(bool enabled)
        {
            if (enabled && !IsSupported)
            {
                _isEnabled = false;
                StateChanged?.Invoke(false);
                return false;
            }

            if (!ApplyNativeState(enabled))
            {
                _isEnabled = false;
                StateChanged?.Invoke(false);
                return false;
            }

            if (_isEnabled == enabled)
                return true;

            _isEnabled = enabled;
            StateChanged?.Invoke(_isEnabled);
            return true;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && _isEnabled)
                ApplyNativeState(true);
        }

        private bool ApplyNativeState(bool enabled)
        {
#if UNITY_ANDROID && PICO_OPENXR_SDK
            PassthroughFeature.EnableVideoSeeThrough = enabled;
            return PassthroughFeature.EnableVideoSeeThrough == enabled;
#elif UNITY_ANDROID
            PXR_Manager.EnableVideoSeeThrough = enabled;
            return PXR_Manager.EnableVideoSeeThrough == enabled;
#else
            if (!_warningLogged)
            {
                Debug.Log("[PicoPassthroughModeService] 非 PICO 运行环境，仅记录透视请求状态。");
                _warningLogged = true;
            }
            return true;
#endif
        }
    }
}
```

- [ ] **Step 2: 运行测试**

Run: `Unity Test Runner EditMode: XRVLC.Tests.VRUIManagerIconStyleTests`

Expected: 服务相关源码断言开始通过，UI 绑定和项目设置测试仍失败。

## Task 3: 绑定 `eyeBtn` 并更新按钮状态

**Files:**
- Modify: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`
- Test: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`

- [ ] **Step 1: 修改 `VRUIManager`**

增加命名空间引用：

```csharp
using XRVLC.Infrastructure.Pico;
```

增加字段：

```csharp
private static readonly Color PassthroughSelectedColor = new Color(1f, 1f, 1f, 0.24f);
private static readonly Color PassthroughDisabledColor = new Color(1f, 1f, 1f, 0.08f);
private PicoPassthroughModeService passthroughModeService;
```

在 `Start()` 中绑定按钮：

```csharp
EnsurePassthroughModeService();
if (eyeBtn != null)
    eyeBtn.onClick.AddListener(OnEyeBtnClicked);
UpdateEyeButtonPassthroughVisual();
```

在 `OnDestroy()` 中移除监听：

```csharp
if (eyeBtn != null)
    eyeBtn.onClick.RemoveListener(OnEyeBtnClicked);
if (passthroughModeService != null)
    passthroughModeService.StateChanged -= OnPassthroughStateChanged;
```

新增方法：

```csharp
private void EnsurePassthroughModeService()
{
    if (passthroughModeService != null) return;

    passthroughModeService = FindAnyObjectByType<PicoPassthroughModeService>();
    if (passthroughModeService == null)
        passthroughModeService = gameObject.AddComponent<PicoPassthroughModeService>();

    passthroughModeService.StateChanged -= OnPassthroughStateChanged;
    passthroughModeService.StateChanged += OnPassthroughStateChanged;
}

private void OnEyeBtnClicked()
{
    CloseSecondaryPopups();
    EnsurePassthroughModeService();
    passthroughModeService?.Toggle();
    UpdateEyeButtonPassthroughVisual();
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

    image.color = enabled ? PassthroughSelectedColor : TransparentListColor;
}
```

- [ ] **Step 2: 运行测试**

Run: `Unity Test Runner EditMode: XRVLC.Tests.VRUIManagerIconStyleTests`

Expected: UI 绑定和按钮状态测试通过，项目设置测试仍失败。

## Task 4: 开启 PICO 项目设置并做最终验证

**Files:**
- Modify: `Assets/Resources/PXR_ProjectSetting.asset`
- Test: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`

- [ ] **Step 1: 修改项目设置**

把资源中的：

```yaml
videoSeeThrough: 0
```

改为：

```yaml
videoSeeThrough: 1
```

- [ ] **Step 2: 运行聚焦测试**

Run: `Unity Test Runner EditMode: XRVLC.Tests.VRUIManagerIconStyleTests`

Expected: 该测试类全部通过。

- [ ] **Step 3: 运行相关回归测试**

Run: `Unity Test Runner EditMode: XRVLC.Tests.SpatialSubtitleUnderlayTests`

Expected: 现有 Underlay alpha 相关测试继续通过。

- [ ] **Step 4: 手工真机验证**

在 PICO 头显上构建运行 APK：

- 点击播放面板 `eyeBtn`，真实环境透视开启。
- 再次点击 `eyeBtn`，真实环境透视关闭。
- 视频、字幕、UI、手柄和射线仍然可见。

当前目录不是 git 仓库，所以不执行 git commit。
