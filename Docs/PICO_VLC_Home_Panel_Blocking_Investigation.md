# PICO 上 VLC 面板遮挡 Home 面板排查结论

## 结论

本次重新打包后的真机验证说明：去掉 VLC 独立 `taskAffinity` 并改用 `FLAG_ACTIVITY_REORDER_TO_FRONT`，不能解决 PICO 上 Home 面板被 VLC 页面遮挡的问题。

根因不是 APK 未更新，也不是 Android 标准 task 配置没有生效，而是 PICO 的 VDM/NS_APP 窗口系统会在 VR Unity Activity 内启动 2D VLC Activity 时，将 VLC 放进单独的 2D 虚拟显示面板。这个面板仍然会成为 focused/obscuring window，因此 Home dock/面板会被它压住或空间上遮挡。

## 设备侧证据

已安装包状态：

- `UnityPlayerActivity isVrActivity: true`
- `org.videolan.vlc.StartActivity isVrActivity: false`
- `org.videolan.vlc.gui.MainActivity isVrActivity: false`
- VLC Activity 已经没有 `android:taskAffinity=":vlc"`，`MainActivity` 已经是 `singleTop`，说明同 task 方案的 manifest 修改已进入设备。

`dumpsys activity activities` 显示：

- Unity 仍在 `Task #110`：
  - `com.ijkm.xr_vlc/com.unity3d.player.UnityPlayerActivity`
- VLC 被 PICO 放入独立 2D task/虚拟显示：
  - `Task #114`
  - `com.ijkm.xr_vlc/org.videolan.vlc.gui.MainActivity`
- Home/shortcut 面板也可见：
  - `Task #115`
  - `com.pvr.shortcut/.dock.activity.TunnelActivity`
- 但全局焦点和遮挡窗口仍是 VLC：
  - `mCurrentFocus=...org.videolan.vlc.gui.MainActivity`
  - `mFocusedApp=...org.videolan.vlc.gui.MainActivity`
  - `mObscuringWindow=...org.videolan.vlc.gui.MainActivity`

`dumpsys window windows` 显示：

- Window #0 是 VLC `MainActivity`
- Window #1 是 PICO shortcut/dock
- Unity window 仍存在
- display #257 的输入目标仍是 VLC `MainActivity`

`dumpsys display` 显示同一个包下出现了多个 PICO NS_APP 虚拟显示：

- `NS_APP[com.ijkm.xr_vlc],0`
- `NS_APP[com.ijkm.xr_vlc],1`
- `NS_APP[com.ijkm.xr_vlc],2`
- `NS_APP[com.ijkm.xr_vlc],3`

这说明多次打开/恢复 VLC 时，PICO 没有按普通 Android 同 task Activity 栈复用，而是在系统 2D 面板层创建或保留了多个 app panel。

## 关键日志

从 Unity 拉起 VLC 时，PICO VDM 日志里出现：

```text
VDM handleStartActivity : ActivityInfo{... org.videolan.vlc.StartActivity},
sourceActivityInfo : ActivityInfo{... UnityPlayerActivity}

requestCreateVirtualDisplay : com.ijkm.xr_vlc, org.videolan.vlc.StartActivity
handleNewA2DTask
in 3d app allow start far panel 2d app
forceMoveToNearStack
isInterceptHomeKey:true
```

这几个字段合起来说明：

- 启动源是 VR Unity Activity。
- 目标是 2D VLC Activity。
- PICO 为 VLC 创建 NS_APP 虚拟显示。
- 系统强制把该 2D 面板放到 near stack。
- 该面板会拦截 Home/focus 相关行为。

随后即使通过 ADB 执行：

```bash
adb shell am start --activity-reorder-to-front \
  -n com.ijkm.xr_vlc/com.unity3d.player.UnityPlayerActivity
```

系统返回 Unity intent 已投递给现有实例，但 `mCurrentFocus` 和 `mObscuringWindow` 仍然是 VLC `MainActivity`。这证明单纯 `REORDER_TO_FRONT` 只能影响 Android Activity 栈，不能移除或降低 PICO 已创建的 2D NS_APP panel。

## 为什么普通 Android 应用看起来更远且是弧面

普通 Android 应用通常是从 PICO launcher/home 启动，PICO 会按普通 2D app panel 策略展示它。当前 VLC 是从 VR Unity Activity 内部启动，VDM 将它识别为“3D app 内启动 2D app”，触发了 near stack 和 `isInterceptHomeKey:true` 路径。

因此，普通 Android app 的远距离/弧面表现，不是标准 Android `launchMode`、`taskAffinity` 或 `FLAG_ACTIVITY_REORDER_TO_FRONT` 能直接配置出来的。

目前公开可查的 PICO ADB 文档只给出了设备级 2D 窗口缩放属性：

```bash
adb shell setprop persist.pvr.2dtovr.screen_scale 1|2|3
```

该属性是全局设备属性，并且需要重启设备，不适合作为应用内修复方案。它也不是针对单个 Activity 的距离、曲率或 Home 层级配置。

## 已证伪方案

### 同 task + REORDER_TO_FRONT

目标：

- Unity 和 VLC 同一个 Android task。
- 选片后 `REORDER_TO_FRONT` 回 Unity。
- 再从 Unity 通过 lastVlcActivity 回到 VLC 上次页面。

验证结果：

- Manifest 修改已安装。
- `REORDER_TO_FRONT` flag 已生效到 intent。
- PICO 仍为 VLC 创建 NS_APP 虚拟显示。
- Home 面板仍被 VLC `MainActivity` 遮挡。

结论：该方案在当前 PICO 系统版本上不成立。

### 手动隐藏 VLC Activity View

不建议继续尝试：

- `decorView.visibility = GONE`
- window alpha 设为 0
- 透明主题
- 只隐藏 root view

原因：即使视觉上透明，Activity window 仍可能保持 focus、input target 和 obscuring window 状态，PICO compositor 仍会认为它在上层。

## 后续可行方案

### 方案 A：短期止血，启动时不自动打开 VLC 页面

保留启动阶段 AAR warm up，但默认停留在 Unity 无背景场景，不自动拉起 VLC Activity。

效果：

- App 启动后 Home 面板不会立刻被 VLC 2D panel 遮挡。
- 用户只有主动进入媒体库时才进入 PICO 2D panel。

代价：

- 不能解决“用户正在 VLC 媒体库页面时按 Home”的遮挡问题。

适用场景：

- 主要问题发生在 app 启动后默认 VLC 页遮挡 Home。

### 方案 B：播放选中后主动结束 VLC 2D Activity

在 `MediaUtils.showUnityView(context)` 中向 Unity 发送播放消息后，不再保留 VLC Activity，而是主动结束当前 VLC 2D Activity/task：

- 对当前 VLC Activity 调用 `finish()`。
- 必要时对 VLC task 调用 `finishAndRemoveTask()`。
- 同时清理 `lastVlcActivity`，避免再次 reorder 到已结束的 panel。

效果：

- 播放开始后 VLC panel 被移除，Unity 播放界面不再被 VLC panel 遮挡。

代价：

- 无法“从 Unity 回到之前的 VLC Activity 页面状态”。
- 再次打开媒体库需要重新进入 StartActivity/MainActivity。

适用场景：

- 优先保证播放和 Home/system 面板不被遮挡。
- 可以接受不保留 VLC 媒体库 UI 栈状态。

### 方案 C：把媒体库改成 Unity 原生 UI

保留 VLC AAR/LibVLC 作为播放和媒体解析能力，但不要把 VLC Android Activity 当成媒体库 UI。媒体浏览、筛选、选择逻辑在 Unity 内实现。

效果：

- 不再创建 PICO 系统 2D NS_APP panel。
- Home 面板层级由 Unity/PICO VR 应用正常处理。
- 可以完全控制距离、曲率、布局和交互。

代价：

- 开发成本最高。
- 需要重新实现媒体库 UI 和部分 VLC 浏览能力。

适用场景：

- 需要稳定的 PICO VR 产品体验。
- 需要可控的空间 UI、Home 面板兼容性和沉浸式播放切换。

### 方案 D：实验性系统启动跳板

通过 `PendingIntent`、延迟启动或先把 Unity task 退后台，再由系统上下文启动 VLC 2D Activity，尝试让 PICO 不把它识别为“VR Activity 内启动 2D Activity”。

效果预期：

- 有机会让 VLC 接近普通 Android app panel 的展示策略。

风险：

- 依赖 PICO VDM 内部策略，稳定性未知。
- 可能仍然创建 NS_APP panel。
- 返回 Unity、权限弹窗、生命周期恢复都会更复杂。

适用场景：

- 在接受实验风险的前提下，想继续复用 VLC Android 原生媒体库 UI。

## 推荐决策

当前推荐分两步走：

1. 先做方案 A + B：
   - 启动只 warm up VLC AAR，不自动打开 VLC 页面。
   - 选片后关闭 VLC 2D Activity，保证播放态和 Home/system 面板不被旧 VLC panel 遮挡。
2. 中长期做方案 C：
   - 把媒体库迁移到 Unity 原生 UI。
   - VLC AAR 只承担播放、解析、缩略图/媒体信息能力。

不建议继续投入同 task + `REORDER_TO_FRONT` 方向，因为真机证据已经表明 PICO VDM 会绕过普通 Android task 语义。

## 常用排查命令

查看 Activity 焦点和遮挡：

```bash
adb shell dumpsys activity activities | rg \
  "Task #|ResumedActivity|topResumedActivity|mFocusedApp|mCurrentFocus|mObscuringWindow|org.videolan|UnityPlayerActivity|com.pvr.shortcut"
```

查看 Window 层级：

```bash
adb shell dumpsys window windows | rg \
  "Window #|mCurrentFocus|mFocusedApp|mObscuringWindow|org.videolan|UnityPlayerActivity|com.pvr.shortcut|display#"
```

查看 PICO NS_APP 虚拟显示：

```bash
adb shell dumpsys display | rg \
  "Display|NS_APP|com.ijkm.xr_vlc|com.pvr.shortcut|layerStack|displayId"
```

查看 PICO VDM 启动路径：

```bash
adb logcat -d -t 1000 | rg \
  "VDM|forceMoveToNearStack|handleStartActivity|requestCreateVirtualDisplay|handleNewA2DTask|isInterceptHomeKey|notifyVirtualDisplayTaskMoveToFront|LAUNCH_|com.ijkm.xr_vlc|org.videolan|UnityPlayerActivity"
```
