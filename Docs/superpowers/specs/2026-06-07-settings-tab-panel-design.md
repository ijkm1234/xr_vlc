# 播放 UI 设置 Tab 面板重构设计

## 目标

重构播放 UI 控制面板上的设置按钮。用户点击设置按钮后，在播放面板上方弹出一个 tab 化设置页面。设置页点击内部控件时保持打开，点击设置页以外的播放 UI 或空白视频区域时隐藏。

最终 tab 为：

- 播放
- 手势
- 字幕
- 视频
- 音频

本次重构只改设置页组织方式和对应设置读写，不把现有 `geometryMenu` 投影、3D、曲面控制合并进视频 tab。

## 当前上下文

Unity 侧：

- `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs` 已经负责播放控制面板、设置按钮、二级弹窗、XR UI 路由和自动隐藏。
- `VRUIManager.settingsMenu` 在 `Assets/Scenes/MainVRScene.unity` 中目前是空引用，会由 `EnsureSettingsMenu()` 动态创建旧版两项菜单。
- `Assets/Scripts/UI/Settings/SettingsMenuController.cs` 当前只是占位类，适合承接新的 tab 设置页。
- `Assets/Scripts/UI/Settings/ShortcutConfigPanel.cs` 当前是独立面板，用于四个手柄按键映射保存。
- `Assets/Scripts/Infrastructure/VlcBridge/Preferences/VlcPreferenceStore.cs` 可以读写 Android 默认 SharedPreferences。它打开的是 `packageName + "_preferences"`，与 VLC `Settings.getInstance(context)` 使用的默认偏好文件一致。

VLC 侧：

- `vlc-android/application/tools/src/main/java/org/videolan/tools/Settings.kt` 定义了大量偏好 key。
- `vlc-android/application/vlc-android/src/org/videolan/vlc/gui/preferences/PreferencesXRController.kt` 已经用 `KEY_XR_BUTTON_MAPPINGS = "xr_button_mappings"` 读写 XR 手柄映射 JSON。
- `vlc-android/application/vlc-android/src/org/videolan/vlc/gui/video/VideoPlayerResizeDelegate.kt` 已经用 `VIDEO_RATIO = "video_ratio"` 保存视频画面比例。
- `vlc-android/application/vlc-android/res/xml/preferences_subtitles.xml` 已经存在字幕自动加载、编码、语言、字号、粗体、颜色、背景、阴影、描边等 VLC 字幕样式设置。
- 未发现 VLC 现有设置中有“XR 字幕渲染模式”或“立体声/混合单声道”偏好 key。

## VLC 设置检查结论

| 设置项 | VLC 现有 key | 结论 | Unity 读写方式 |
| --- | --- | --- | --- |
| 手柄快捷键映射 | `xr_button_mappings` | 已存在，Unity 和 VLC 已共享 | 继续通过 `ShortcutSettingsService` / `VlcPreferenceStore.GetString/PutString` |
| 手柄跳转秒数 | `video_jump_delay` | 已存在，当前 Unity 运行时已读取 | 可在手势 tab 展示或后续加入设置 |
| 字幕渲染模式：原生 / 空间 / 关闭 | 无 | 项目自定义 XR 运行时能力，不保存 | 只调用 `PlaybackService.SetSubtitleRenderMode` / `PlaybackServiceBridge.setSubtitleRenderMode` 立即生效 |
| 字幕轨选择 | 当前播放轨道状态，不是偏好 | 已由播放事件桥提供 | 复用当前 `PlaybackService.SetSubtitleTrack` 和 `OnSubtitleTracksChanged` |
| 字幕样式 | `subtitles_*`、`subtitle_text_encoding`、`subtitle_preferred_language` | 已存在 | 需要展示时通过 `VlcPreferenceStore` 读写对应 key |
| 视频拉伸/裁剪/比例 | `video_ratio` | 已存在 | 通过 `VlcPreferenceStore.GetInt/PutInt` 持久化，并新增 bridge 方法立即应用当前播放 |
| 视频 projection/3D/曲面 | Unity 手动几何状态 | 不纳入本 tab | 保持现有 `geometryMenu` |
| 音频立体声/混合单声道 | 无 | 未发现现有 VLC 偏好；按需求不保存 | 只调用 `PlaybackServiceBridge.setAudioChannelMode` 更新运行时状态 |

## 推荐架构

### 1. `SettingsMenuController`

位置：`Assets/Scripts/UI/Settings/SettingsMenuController.cs`

职责：

- 动态创建并维护 `SettingsTabPanel`。
- 管理 `播放 / 手势 / 字幕 / 视频 / 音频` tab 的选中状态。
- 负责 show/hide，不直接处理播放业务。
- 暴露：
  - `Bind(SettingsMenuDependencies dependencies)`
  - `Show(SettingsTab initialTab = SettingsTab.Playback)`
  - `ShowTab(SettingsTab tab)`
  - `Hide()`
  - `bool IsOpen`
  - `bool Contains(GameObject target)`

`SettingsMenuDependencies` 包含：

- `PlaybackService`
- `ShortcutManager`
- 当前音轨/字幕轨列表提供器
- 当前播放 UI 的按钮样式 helper 或小型 UI 工厂
- `Action` 类型的关闭回调

### 2. `VRUIManager`

`VRUIManager` 保留播放控制面板门面职责，但不再创建设置页内部按钮。

改造点：

- `OnSettingsBtnClicked()` 只负责：
  - 确保 `SettingsMenuController`
  - 关闭其他二级 popup
  - 将设置页定位到 `settingsBtn` 上方
  - 调用 `settingsMenuController.Show()`
  - `BringPopupToFront(settingsMenu)`
- `RegisterUiTreeNodes()` 继续注册 `settings-menu`，确保 XR ray hover、trigger 和自动隐藏逻辑把设置页视为 managed UI。
- `CloseSecondaryPopups()` 关闭设置页时调用 `settingsMenuController.Hide()`，避免 root active 状态和 tab controller 状态不一致。
- `HandleTriggerPressedEdge()` 增加外部点击关闭规则：
  - 设置页打开且当前 XR target 为空：隐藏设置页，消费本次 trigger。
  - 设置页打开且 target 不在设置页内：隐藏设置页，继续按现有逻辑处理是否唤醒/隐藏播放面板。
  - target 在设置页内：交给 `UiTreeRouter` 消费，不关闭。

### 3. `PlaybackUiSettingsService`

位置建议：`Assets/Scripts/Services/Settings/PlaybackUiSettingsService.cs`

职责：

- 集中定义 Unity 侧需要读写的 VLC / XR 设置 key。
- 对已有 VLC key 做强类型封装。
- 对视频 `video_ratio` 提供默认值、范围校验和保存。

建议 key：

```csharp
public const string KeyVideoRatio = "video_ratio";
```

默认值：

- `video_ratio`: `SURFACE_BEST_FIT` 对应的 ordinal。

### 4. Android `PlaybackServiceBridge`

位置：`vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt`

新增或调整：

- `setVideoScale(scaleOrdinal: Int)`
  - clamp 到 `MediaPlayer.ScaleType.entries` 范围。
  - 写入 `VIDEO_RATIO`。
  - 对当前 `playbackService?.mediaplayer` 设置 `videoScale`。
- `setAudioChannelMode(mode: String)`
  - 只更新运行时声道模式，不写 SharedPreferences。
  - 未找到 VLC Android 既有单声道偏好；实际 downmix 需要后续确认当前打包 LibVLC 的可用参数。

## Tab 内容细化

### 播放 tab

只放通用播放控制设置，不放字幕渲染模式。

第一版建议：

- 播放速度快捷选项：`0.5x / 1.0x / 1.25x / 1.5x / 2.0x`，调用现有 `PlaybackService` / `VlcPlaybackBridge.SetRate`。
- 跳转秒数只读展示 `video_jump_delay`，若要可编辑则写回同一 key 并通知 `ShortcutManager.ReloadConfig()`。

### 手势 tab

`ShortcutConfigPanel` 全部迁移到设置页内部，不再作为独立 modal 弹出。

内容：

- 左摇杆点击
- 右摇杆点击
- Y 键
- B 键
- 保存按钮

数据源：

- 继续使用 `ShortcutSettingsService.LoadShortcutMappings()`。
- 保存继续使用 `ShortcutSettingsService.SaveShortcutMappings()`。
- 保存后调用 `ShortcutManager.ReloadConfig()`。

交互变化：

- 移除旧 `shortcutConfigEntryBtn` 和独立 `shortcutConfigPanel` 弹窗路径。
- 手势 tab 内部下拉或选项点击不关闭设置页。

### 字幕 tab

内容：

- 字幕轨选择：复用现有字幕轨列表和 `PlaybackService.SetSubtitleTrack`。
- 字幕渲染模式：原生 / 空间 / 关闭。
- 可选字幕样式入口：字号、颜色、背景、阴影、描边，读取 VLC 已有 `subtitles_*` key。

字幕渲染模式数据流：

```text
用户选择字幕渲染模式
  -> SettingsMenuController
  -> PlaybackService.SetSubtitleRenderMode(mode)
  -> VlcPlaybackBridge.SetSubtitleRenderMode(mode)
  -> Android PlaybackServiceBridge.setSubtitleRenderMode(mode)
```

启动时：

```text
PlaybackService.Start()
  -> PlaybackUiSettingsService.LoadSubtitleRenderMode()
  -> SetSubtitleRenderMode(loadedMode)
```

### 视频 tab

不包含 `geometryMenu` 的 projection、3D、曲面设置。

内容为画面显示方式：

- 最佳适配：完整显示，不裁剪。
- 适应屏幕：按目标面板适配。
- 填充裁剪：铺满面板，允许裁剪边缘。
- 原始大小或居中：仅在 XR 物理尺寸语义明确时展示。
- 固定比例：16:9、4:3、16:10、2:1、2.21:1、2.35:1、2.39:1、5:4。

数据源：

- 使用 VLC 既有 `video_ratio` key。
- Unity 读写 `VlcPreferenceStore.GetInt/PutInt("video_ratio", defaultOrdinal)`。
- 用户修改时调用 `VlcPlaybackBridge.SetVideoScaleOrdinal(ordinal)`，Android 侧写 key 并立即设置当前 `mediaplayer.videoScale`。

XR 补充：

当前 XR 视频由 `PXR_CompositionLayer` 外部 Surface + Unity `VideoScreen.FitVideoSize()` 控制空间尺寸。实现时需要确认 `mediaplayer.videoScale` 对外部 Surface 是否足够生效。

若真机验证发现 `videoScale` 对 PICO external surface 不影响画面，Unity 侧需要同步支持：

- 在 `VideoScreen` 中新增 `SetDisplayFitMode(VideoDisplayFitMode mode)`。
- 在 `FitVideoSize()` 中根据模式计算 `videoAnchor.localScale`。
- 在 `PicoRenderSurface` 中新增 image rect / src rect 应用能力，用于填充裁剪。
- 仍然使用 `video_ratio` 作为持久化 key，保持与 VLC 设置语义一致。

### 音频 tab

只放一个声道模式设置：

- 立体声
- 混合成单声道

检查结论：

- 当前 VLC 设置中没有找到单声道混合的偏好 key。
- 当前 Android `PlayerController` 只暴露了音轨选择、音量、延迟、数字输出、AOUT、均衡器等能力，没有现成的单声道切换设置。

- 不新增持久化 key，不用 `VlcPreferenceStore.GetString/PutString`。
- Unity 直接通过 `VlcPlaybackBridge.SetAudioChannelMode` 下发当前运行时选择。
- 单声道实际应用优先放在 VLC 播放链路中，而不是 Unity 音频层，因为当前音频不是 Unity AudioSource 输出。
- 若 LibVLC 单声道 filter 支持运行时切换，则 bridge 立即应用；否则只能保留运行时状态并等待后续补充可用输出策略。

## 外部点击隐藏

规则：

- 设置页打开时，点击 tab、content、下拉项、保存按钮，不隐藏。
- 点击播放控制面板其他按钮、播放列表、音轨弹窗、视频区域或空白世界，隐藏设置页。
- 再次点击设置按钮，toggle 隐藏。
- 播放面板自动隐藏时，设置页同步隐藏。

实现点：

- `SettingsMenuController.Contains(GameObject target)` 判断是否在设置页内。
- `VRUIManager` 在 trigger edge 中先处理设置页外部点击。
- `IsManagedUiObject()` 纳入新的设置页 root 和运行时下拉列表 root。
- `RegisterUiTreeNodes()` 注册设置页 root，且设置页内部控件使用 `XrUiNodeLayer.Popup`。

## 测试方案

EditMode 测试：

- `SettingsMenuController` 创建五个 tab：播放、手势、字幕、视频、音频。
- 默认显示播放 tab，点击其他 tab 只显示对应 content。
- 字幕渲染模式不保存，只调用 `PlaybackService.SetSubtitleRenderMode`。
- 手势 tab 保存后写入 `xr_button_mappings` 并调用 `ShortcutManager.ReloadConfig()`。
- 视频 tab 写入 `video_ratio`，并调用 `VlcPlaybackBridge.SetVideoScaleOrdinal`。
- 音频 tab 不保存，只调用 `VlcPlaybackBridge.SetAudioChannelMode`。
- `VRUIManager` 点击设置按钮显示面板，点击设置面板外隐藏。
- `VRUIManager` 隐藏播放面板时同步隐藏设置面板。

VLC / Android 侧测试：

- 验证 `PlaybackServiceBridge.setVideoScale` clamp ordinal 并写入 `VIDEO_RATIO`。
- 验证播放启动时读取 `video_ratio`。
- 验证音频声道模式不写 SharedPreferences。
- 验证 mono 模式在当前 LibVLC 包中可用；若不可运行时应用，记录 bridge 当前只保持运行时模式。

真机验证：

- PICO 设备上点击设置按钮，设置面板出现在播放面板上方。
- tab 切换清晰且不会误触关闭。
- 点击视频区域隐藏设置页。
- 字幕模式原生 / 空间 / 关闭生效。
- 手势设置保存后手柄快捷键即时更新。
- 视频“填充裁剪”和“最佳适配”在平面视频中可观察到不同效果。
- 音频单声道模式按实现策略即时或下次播放生效。

## 非目标范围

- 不把 `geometryMenu` 的 180、360、3D、曲面设置迁入视频 tab。
- 不重做播放主面板布局。
- 不增加新的 VLC Android 原生设置页面，除非后续需要让 VLC 设置页也展示新增 XR key。
- 不做 Unity 音频后处理替代 VLC 音频输出。

## 风险与处理

- `video_ratio` 在普通 VLC Android 播放器中已生效，但 XR 使用 external Android Surface。需要真机确认 `mediaplayer.videoScale` 对 PICO external surface 的影响。如果无效，则 Unity/PICO layer 侧补充 fit/crop 实现。
- 单声道混合没有现成 key，且具体 LibVLC filter 可用性需要在当前 AAR 中验证。第一版应把它设计为可保存、有默认值，并明确当前播放即时生效或下次播放生效的状态。
- `ShortcutConfigPanel` 迁移后要避免重复注册旧 `saveButton` 监听，建议迁移成 tab content builder 或重写为无 modal 依赖的 `GestureSettingsContent`。
