# Unity Asset 组织与模块架构改造方案

## Summary

- 保留现有 Unity 顶层目录：`Assets/Scripts`、`Assets/Prefabs`、`Assets/Scenes`、`Assets/Plugins`，不新建 `Assets/XRVLC`。
- 架构按职责分为 `Domain`、`Services`、`Infrastructure`、`UI`、`XR`、`Utils`。
- `Domain` 放纯领域类型和纯逻辑；`Services` 放业务编排；`Infrastructure` 放 VLC/PICO/线程等外部适配；`UI` 和 `XR` 放表现层与输入适配。
- 播放状态以 `VlcBridge` 的快照为准，不新增独立 `PlaybackStateStore`。
- 快捷键相关类型去掉 `Controller` 前缀。
- `PlaybackGeometryCoordinator` 命名为 `VideoScreenGeometryService`。
- `VlcPlaybackPreferences` 拆为 `Services/Shortcuts/ShortcutSettingsService` 和 `Infrastructure/VlcBridge/Preferences/VlcPreferenceStore`。

## Target Tree

```text
Assets/Scripts/
  Domain/
    Media/
      MediaWrapper.cs
      TrackInfo.cs
      PlaylistModels.cs
      PlaybackEnums.cs
      IPlaybackEvents.cs

    Projection/
      VideoProjection.cs
      StereoMode.cs
      ProjectionDetector.cs

    Shortcuts/
      ShortcutCommand.cs
      ShortcutConfigData.cs
      ShortcutConstants.cs
      ShortcutInputState.cs

  Services/
    Playback/
      PlaybackService.cs
      TrackSelectionService.cs

    Screen/
      IRenderSurface.cs
      VideoScreen.cs
      VideoScreenGeometryService.cs
      VideoScreenTransformService.cs

    Shortcuts/
      ShortcutPlaybackService.cs
      ShortcutSettingsService.cs

  Infrastructure/
    VlcBridge/
      Playback/
        VlcPlaybackBridge.cs
        VlcPlaybackEvents.cs
        VlcPlaybackSnapshot.cs
        VlcMediaParseResult.cs        # 空兼容文件，防止 Unity 旧编译清单引用丢失
        VlcPlaybackPayloadParser.cs   # 空兼容文件，防止 Unity 旧编译清单引用丢失

        Parsing/
          MediaBridgeDTO.cs
          VlcPlaybackPayloadParser.cs
          VlcMediaParseResult.cs
          XRVLC.VlcBridge.Parsing.asmdef

      MediaLibrary/
        VlcMediaLibraryBridge.cs

      Preferences/
        VlcPreferenceStore.cs

      Launcher/
        VlcLibraryLauncher.cs
        VlcFocusRestoreHandler.cs

    Rendering/
      PicoRenderSurface.cs

    Pico/
      PicoSessionEvents.cs
      PicoRecenterEvents.cs

    Threading/
      Loom.cs

  UI/
    PlaybackControls/
      VRUIManager.cs
      PlaybackControlsPanel.cs
      TrackDropdownPanel.cs
      PanelAutoHideController.cs

    Settings/
      SettingsMenuController.cs
      ShortcutConfigPanel.cs

    Playlist/
      PlaylistPanelController.cs

    Common/
      RoundedRectImage.cs

  XR/
    Shortcuts/
      ShortcutManager.cs

    Recenter/
      XRRecenterOnStart.cs

    Visuals/
      NearFarReticleVisual.cs

  Utils/
    UriUtils.cs
```

## Module Responsibilities

**Domain**
- `Domain/Media` 定义媒体、轨道、播放状态、播放列表、播放事件接口。
- `Domain/Projection` 定义投影/3D 模式和投影识别规则。
- `Domain/Shortcuts` 定义快捷键命令、配置、常量、输入状态机。
- 该层不依赖 Unity 场景对象、VLC AAR、PICO SDK、UI。

**Services**
- `Services/Playback` 是播放业务门面，面向 UI/XR 暴露播放操作。
- `Services/Screen` 是视频屏幕业务门面，管理幕布几何、surface 绑定、移动和朝向。
- `Services/Shortcuts` 解释快捷键命令，读取快捷键配置，并调用播放/屏幕服务。
- 该层可以依赖 `Domain` 和 `Infrastructure` 的公开适配接口。

**Infrastructure**
- `Infrastructure/VlcBridge` 是 Unity 与 VLC Android AAR 的唯一桥接边界。
- `Infrastructure/Rendering/PicoRenderSurface` 是 `IRenderSurface` 的 PICO 实现。
- `Infrastructure/Pico` 放 PICO 独有能力，例如 session/recenter 事件。
- `Infrastructure/Threading` 放线程/主线程调度工具。
- 该层可以依赖外部 SDK，但不依赖 UI。

**UI**
- UI 只负责展示和用户交互，不直接调用 VLC JNI。
- UI 通过 `PlaybackService`、`ShortcutSettingsService`、`PlaylistPanelController` 等门面工作。
- `VRUIManager` 保留为 UI facade，但拆出子控制器降低体积。

**XR**
- XR 只负责设备输入和 XR 场景适配。
- `ShortcutManager` 读取手柄输入，输出快捷键命令，交给 `ShortcutPlaybackService` 执行。
- XR 不直接读写 VLC preferences，也不直接调用 VLC bridge。

## Class Definitions

**Domain / Media**
- `MediaWrapper`: 媒体条目数据，包含 `Id`、`Uri`、`Title`、`Time`、`Projection`、`StereoHint`、`Slaves`、`RawJson`。
- `TrackInfo`: 音轨/字幕轨模型，包含 `Id`、`Name`。
- `PlaylistItemData`: 播放列表项，包含 `index`、`title`、`uri`、`isCurrent`。
- `PlaylistJsonWrapper`: VLC playlist JSON 包装。
- `PlayerStatus`: `Idle/Opening/Buffering/Playing/Paused/Stopped/Ended/Error`。
- `RepeatMode`: `None/All/Single`。
- `IPlaybackEvents`: 播放事件接口，定义状态、错误、媒体变化、时间、buffer、音轨/字幕轨事件。

**Domain / Projection**
- `VideoProjection`: `Flat/Cylinder/Sphere360/Sphere180`。
- `StereoMode`: `Mono/LeftRight/TopBottom`。
- `ProjectionDetector`: 根据 `MediaWrapper` metadata 和 uri/file name 推断 `VideoProjection` 与 `StereoMode`。

**Domain / Shortcuts**
- `ShortcutCommand`: 快捷键命令，包含 `Type` 和可选 `ButtonId`。
- `ShortcutCommandType`: `None/SeekBackward/SeekForward/TogglePlayPause/ConfigurableAction`。
- `ShortcutConfigData`: 快捷键映射配置，负责 JSON 序列化/反序列化。
- `ShortcutConstants`: 定义按键 ID 和动作 ID，例如 `right_stick_click`、`toggle_2x_speed`。
- `ShortcutInputState`: 纯输入状态机，输入手柄轴值和按键状态，输出 `ShortcutCommand`。

**Services / Playback**
- `PlaybackService`: 播放业务 facade。订阅 `VlcPlaybackEvents`，调用 `VlcPlaybackBridge`，向 UI/XR 暴露播放操作和只读状态。
- `TrackSelectionService`: 基于 `VlcPlaybackSnapshot` 的音轨/字幕轨数据执行轨道选择、字幕轮换。

**Services / Screen**
- `VideoScreen`: Unity 场景幕布 facade，持有 `videoAnchor`、`backgroundBoard`、`playerHeadCamera`。
- `IRenderSurface`: 渲染 surface 抽象，定义 layer 重建、几何设置、surface handle、销毁、软解 texture。
- `VideoScreenGeometryService`: 根据视频尺寸、projection/stereo 重建 layer，设置几何，等待 surface 并绑定 VLC。
- `VideoScreenTransformService`: 处理前后偏移、Grip 拖动、朝向玩家、沉浸球幕位置更新。

**Services / Shortcuts**
- `ShortcutPlaybackService`: 接收 `ShortcutCommand`，解释为播放/屏幕动作；负责 seek、play/pause、2x、字幕切换、幕布前后移动。
- `ShortcutSettingsService`: 读取 seek 秒数和快捷键映射，保存快捷键映射；内部使用 `VlcPreferenceStore`。

**Infrastructure / VlcBridge**
- `VlcPlaybackBridge`: 调用 VLC AAR 播放 API：`PreloadLocation`、`Play`、`Pause`、`Stop`、`Seek`、`SetTime`、`SetSurface`、`SetAudioTrack`、`SetSpuTrack`、`SetRate`、`GetPlaylist`、`SkipToIndex`。
- `VlcPlaybackEvents`: UnitySendMessage 接收入口，接收 Android 回调，更新 `VlcPlaybackSnapshot`，派发 C# events。
- `VlcPlaybackPayloadParser`: 解析 `StartPlay` JSON、音轨字符串、字幕轨字符串、media parse JSON。
- `VlcPlaybackSnapshot`: VLC bridge 当前播放快照，包含 status、currentMedia、time、length、buffering、audioTracks、subtitleTracks。
- `VlcMediaParseResult`: 媒体解析 DTO，包含 width、height、projection、duration。
- `VlcMediaLibraryBridge`: 读写 VLC Medialibrary 历史进度、插入播放历史。
- `VlcPreferenceStore`: 读写 VLC SharedPreferences，提供 `GetInt/GetString/GetBool/GetFloat/PutString/PutInt/PutBool`。
- `VlcLibraryLauncher`: 打开 VLC 媒体库，处理 Android 权限。
- `VlcFocusRestoreHandler`: 处理打开 VLC 后 Unity controller 隐藏和恢复。

**Infrastructure / Rendering / Pico**
- `PicoRenderSurface`: `IRenderSurface` 的 PICO 实现，封装 `PXR_CompositionLayer`、External Surface、Underlay/Overlay、Equirect/Quad/Cylinder。
- `PicoSessionEvents`: 包装 PICO session/focus 状态事件。
- `PicoRecenterEvents`: 包装 PICO recenter 事件。

**UI**
- `VRUIManager`: UI facade，持有各子面板引用，连接 `PlaybackService`。
- `PlaybackControlsPanel`: 播放/暂停、上一首、下一首、速度、进度条、时间显示。
- `TrackDropdownPanel`: 音轨/字幕 dropdown 显示和选择。
- `PanelAutoHideController`: trigger 唤醒 UI、计时隐藏。
- `SettingsMenuController`: settings 下拉入口。
- `ShortcutConfigPanel`: 快捷键配置 UI，通过 `ShortcutSettingsService` 读写配置。
- `PlaylistPanelController`: 播放列表展示和跳播。
- `RoundedRectImage`: 通用 UI Graphic。

**XR**
- `ShortcutManager`: 读取 XR 左右手柄输入，使用 `ShortcutInputState` 生成 `ShortcutCommand`，交给 `ShortcutPlaybackService`。
- `XRRecenterOnStart`: XR Origin 启动/系统 recenter 适配。
- `NearFarReticleVisual`: NearFarInteractor reticle 可视化。

## Key Runtime Flows

**播放视频**
```text
VLC AAR
  -> UnitySendMessage
  -> VlcPlaybackEvents
  -> VlcPlaybackPayloadParser
  -> VlcPlaybackSnapshot 更新
  -> PlaybackService.LoadAndPlay
  -> VlcPlaybackBridge.PreloadLocation
  -> VideoScreenGeometryService
  -> VideoScreen
  -> IRenderSurface
  -> PicoRenderSurface
```

**视频尺寸/投影更新**
```text
VLC AAR OnMediaParseFinished
  -> VlcPlaybackEvents
  -> VlcMediaParseResult
  -> PlaybackService
  -> VideoScreenGeometryService
  -> ProjectionDetector
  -> VideoScreen.RebuildLayer / SetGeometry
  -> PicoRenderSurface
  -> VlcPlaybackBridge.SetSurface
```

**UI 播放控制**
```text
PlaybackControlsPanel
  -> PlaybackService
  -> VlcPlaybackBridge
  -> VLC AAR
```

**音轨/字幕选择**
```text
TrackDropdownPanel
  -> PlaybackService
  -> TrackSelectionService
  -> VlcPlaybackBridge.SetAudioTrack / SetSpuTrack
```

**手柄快捷键**
```text
XR controller input
  -> ShortcutManager
  -> ShortcutInputState
  -> ShortcutCommand
  -> ShortcutPlaybackService
  -> ShortcutSettingsService
  -> PlaybackService / VideoScreenTransformService
```

**快捷键配置保存**
```text
ShortcutConfigPanel
  -> ShortcutSettingsService
  -> VlcPreferenceStore
  -> VLC SharedPreferences
```

**打开 VLC 媒体库**
```text
UI Exit/Open button
  -> VlcLibraryLauncher
  -> Android permission check
  -> VLC Activity
  -> VlcFocusRestoreHandler
```

## Dependency Rules

```text
Domain
  ↑
Infrastructure/VlcBridge
  ↑
Services
  ↑
UI / XR
```

```text
Domain/Projection
  ↑
Services/Screen
  ↑
Infrastructure/Rendering/PicoRenderSurface
```

Rules:
- `Domain` 不引用 `UnityEngine.MonoBehaviour`、VLC、PICO、UI。
- `Services` 可引用 `Domain` 和 `Infrastructure` 的公开适配，但不解析 Android payload。
- `Infrastructure/VlcBridge` 是 VLC AAR 状态源，负责维护 `VlcPlaybackSnapshot`。
- `PlaybackService` 不维护独立 `PlaybackStateStore`，状态读取来自 `VlcPlaybackSnapshot`。
- `UI` 不直接调用 VLC JNI，不直接解析 VLC payload。
- `XR` 不直接读写 VLC SharedPreferences，不直接调用 VLC bridge。
- `PicoRenderSurface` 只实现 `IRenderSurface`，不依赖 Playback/UI/XR。
- PICO 通用能力实现按能力放，例如 `Infrastructure/Rendering/PicoRenderSurface`；PICO 独有事件放 `Infrastructure/Pico`。

## Scene Organization

当前 `MainVRScene` 根节点建议整理为：

```text
Services
  PlaybackService

Infrastructure
  VlcPlaybackBridge
  VlcLibraryLauncher
  Loom

Rendering
  VideoScreen

UI
  Controller Canvas

XR
  XR Origin

Lighting
  Directional Light
  Global Volume

EventSystem
```

Rules:
- `PlaybackService` 根对象只挂播放服务相关组件。
- `ShortcutManager` 可放到 `XR` 分组下，引用 `ShortcutPlaybackService` 或 `PlaybackService`。
- `VlcPlaybackEvents` 和 `VlcLibraryLauncher` 放 `Infrastructure` 分组。
- `VideoScreen` 放 `Rendering` 分组。
- UI Canvas 下只挂 UI facade 和 UI 子控制器。

## Migration Steps

1. 移动并拆分 `Domain` 类型，保持 `.meta` 文件，减少引用丢失。
2. 将快捷键类型重命名：
   - `ControllerShortcutConfigData` -> `ShortcutConfigData`
   - `ControllerShortcutInputState` -> `ShortcutInputState`
   - `ControllerShortcutCommand` -> `ShortcutCommand`
   - `ControllerShortcutManager` -> `ShortcutManager`
3. 将 `PlaybackAarBridge` 拆为 `VlcPlaybackBridge`、`VlcPlaybackEvents`、`VlcPlaybackPayloadParser`、`VlcPlaybackSnapshot`。
4. 删除 `PlaybackStateStore` 设计，播放状态归入 `VlcPlaybackSnapshot`。
5. 将 `VlcSharedPreferences` 改为 `VlcPreferenceStore`。
6. 将 `VlcPlaybackPreferences` 改为 `ShortcutSettingsService`，移动到 `Services/Shortcuts`。
7. 将 `PlaybackService` 调整为业务 facade：订阅 bridge events，调用 bridge commands，暴露只读状态。
8. 将几何绑定流程抽成 `VideoScreenGeometryService`。
9. 将幕布移动逻辑抽成 `VideoScreenTransformService`。
10. 将 `VRUIManager` 拆为播放控制、轨道 dropdown、设置菜单、自动隐藏子控制器。
11. 将 `VlcAarBridge` 拆成 `VlcLibraryLauncher` 和 `VlcFocusRestoreHandler`。
12. 调整主场景根节点分组，确认所有 serialized references 保持有效。
13. 第一阶段保留最少 asmdef 变更；目录/命名空间稳定后，再逐步新增 Runtime asmdef。

## Test Plan

- Unity MCP 编译，确认 Console `error=0`。
- `manage_scene validate`，确认 missing scripts 和 broken prefabs 为 0。
- EditMode tests:
  - `ProjectionDetectorTests`
  - `ShortcutInputStateTests`
  - `ShortcutConfigDataTests`
  - `VlcPlaybackPayloadParserTests`
- 手工验收:
  - VLC 媒体库打开、选片、播放正常。
  - 平面/180/360 视频几何和 surface 绑定正常。
  - UI 播放控制、进度、音轨、字幕、播放列表正常。
  - 手柄 seek、倍速、字幕、前后移动、Grip 拖动正常。
  - 快捷键配置保存后 Unity 和 VLC 设置页读取一致。

## Assumptions

- 不新建 `Assets/XRVLC` 根目录。
- `PlaybackStateStore` 不作为独立类落地。
- 快捷键相关类型统一去掉 `Controller` 前缀。
- `PlaybackGeometryCoordinator` 统一命名为 `VideoScreenGeometryService`。
- `VlcBridge` 表示 VLC AAR/媒体库/设置/启动入口的统一外部系统边界。
- `VlcPlaybackPreferences` 不放在 `XR/Shortcuts`，改为 `Services/Shortcuts/ShortcutSettingsService`。
- `PicoRenderSurface` 放 `Infrastructure/Rendering`，PICO 独有事件放 `Infrastructure/Pico`。
