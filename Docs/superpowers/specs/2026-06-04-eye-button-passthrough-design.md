# 面板眼睛按钮透视模式设计

## 目标

为现有播放面板的 `eyeBtn` 增加透视模式开关。

用户点击 `eyeBtn` 时，应用切换 PICO Video See Through，让头显摄像头画面成为环境背景。现有视频层、字幕、手柄、射线和 UI 继续沿用当前透明 eye buffer 与 PICO Underlay 合成链路渲染。

## 当前上下文

- `VRUIManager` 已经暴露 `public Button eyeBtn`。
- `VRUIManager.ApplyIconSprites()` 已经给 `eyeBtn` 绑定 `"view"` 图标。
- `eyeBtn` 目前没有点击监听。
- `VideoScreen` 已经把主相机清屏方式设为 `CameraClearFlags.SolidColor`，并使用 alpha 为 `0` 的背景色。
- `PicoRenderSurface` 已经使用 `PXR_CompositionLayer.OverlayType.Underlay`。
- Android Player Settings 已经开启 framebuffer alpha 保留。
- `Assets/Resources/PXR_ProjectSetting.asset` 当前仍是 `videoSeeThrough: 0`，构建时的 PICO Video See Through 支持还需要开启。

## 推荐方案

把现有 `eyeBtn` 作为直接切换按钮，不新增弹窗菜单。

新增一个很小的 PICO 专用透视服务，让 UI 层保持轻量：

- `VRUIManager` 负责按钮交互和视觉状态。
- `PicoPassthroughModeService` 负责运行时透视状态和 PICO API 调用。
- 项目设置负责为 Android 构建启用 PICO Video See Through 元数据。

这样可以把 PICO SDK 细节隔离在基础设施层，播放面板只依赖一个明确的服务接口。后续如果要加入手柄快捷键或设置菜单入口，也不需要再改 PICO API 调用点。

## 架构

### `PicoPassthroughModeService`

位置：`Assets/Scripts/Infrastructure/Pico/PicoPassthroughModeService.cs`

职责：

- 暴露 `IsEnabled`。
- 暴露 `IsSupported`，用于表示当前运行环境是否支持透视。
- 提供 `SetEnabled(bool enabled)` 和 `Toggle()`。
- 在 Editor 中不调用硬件 API，但仍记录请求状态，方便测试和 UI 反馈。
- 在 PICO 构建中调用可用的 PICO Video See Through API。
- 应用从暂停恢复时重新应用已启用状态，因为 PICO SDK 的 session 变化可能会暂停透视。

运行时 API 优先级：

- 定义了 `PICO_OPENXR_SDK` 时，优先使用 `Unity.XR.OpenXR.Features.PICOSupport.PassthroughFeature.EnableVideoSeeThrough`。
- 其他 PICO 路径使用 `Unity.XR.PXR.PXR_Manager.EnableVideoSeeThrough`。

服务内部需要使用编译宏隔离 PICO SDK 依赖，保证 Editor 测试和非 PICO 构建继续可编译。

### `VRUIManager`

职责：

- 查找或创建 `PicoPassthroughModeService`。
- 在 `Start()` 中注册 `eyeBtn.onClick`。
- 在 `OnDestroy()` 中移除点击监听。
- 点击时调用 `Toggle()`。
- 更新 `eyeBtn` 的视觉状态：
  - 透视关闭时使用普通状态
  - 透视开启时使用选中或高亮状态
  - 当前环境不支持透视时禁用或置灰

第一版继续使用现有 `"view"` 图标，不额外增加 eye-off 图标。

### 项目设置

设置 `Assets/Resources/PXR_ProjectSetting.asset`：

```yaml
videoSeeThrough: 1
```

保留现有 alpha 链路要求：

- `ProjectSettings/ProjectSettings.asset` 保持 `preserveFramebufferAlpha: 1`。
- `VideoScreen` 在平面和沉浸模式下继续使用透明相机清屏。
- 使用 Underlay 视频时，`BackgroundBoard` 继续保持禁用，避免遮住底层合成内容。

## 数据流

```text
用户点击 eyeBtn
  -> VRUIManager.OnEyeBtnClicked()
  -> PicoPassthroughModeService.Toggle()
  -> PICO runtime 开启或暂停 Video See Through
  -> VRUIManager 更新 eyeBtn 的选中或禁用视觉状态
```

## 渲染行为

该功能依赖现有 alpha 合成链路：

- XR 相机把 Unity eye buffer 清成 alpha `0`。
- Unity UI、字幕、手柄和射线渲染到 eye buffer。
- PICO passthrough 提供真实环境背景。
- 视频层继续作为 PICO Underlay composition layer 存在。

这样不需要自定义打洞几何体，也不需要修改字幕或 UI 材质。

## 错误处理

- 如果运行时报告不支持透视，保持透视关闭，并置灰 `eyeBtn`。
- 如果当前构建中 PICO API 不可用，记录一次 warning，并让服务保持安全的关闭状态。
- 如果开启失败，将 `IsEnabled` 恢复为 `false`，并同步按钮状态。
- 在 Editor 中不调用硬件 API。

## 测试

新增聚焦的 Editor 测试：

- `VRUIManager` 会把 `eyeBtn` 绑定到透视切换逻辑。
- 透视状态变化时，`VRUIManager` 会更新 `eyeBtn` 视觉状态。
- 源码级测试确认 `PXR_ProjectSetting.asset` 启用了 `videoSeeThrough`。
- 现有 Underlay alpha 测试继续验证透明 eye buffer 行为。

PICO 真机验证：

- 构建 APK 并运行到目标头显。
- 确认 `eyeBtn` 可以开启和关闭真实环境透视。
- 确认 UI、字幕、手柄模型和射线仍然可见。
- 确认视频在平面、柱面和沉浸投影模式下仍然可见。

## 非目标范围

- 局部透视窗口或 mesh 形状的透视遮罩。
- 场景重建、平面检测、空间网格遮挡或空间锚点。
- 新 UI 布局或弹窗菜单。
- 单独的 eye-off 图标资源。
