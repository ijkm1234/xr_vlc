# VLC 与 Unity 同 Task Activity 切换方案

> 2026-06-17 真机复测结论：该方案在当前 PICO 设备/系统上不能解决 Home 面板被 VLC 页面遮挡的问题。原因是 PICO VDM 会在 VR Unity Activity 内启动 2D VLC Activity 时创建独立 NS_APP 虚拟显示，并将 VLC `MainActivity` 保持为 focused/obscuring window。详见 `Docs/PICO_VLC_Home_Panel_Blocking_Investigation.md`。本文保留作为历史方案和代码背景，不再作为推荐方向继续推进。

## 背景

当前 VLC 媒体库页面是从 Unity 内部拉起的独立 Android task：

- Unity 在 `Assets/Scripts/Infrastructure/VlcBridge/Launcher/VlcLibraryLauncher.cs` 中启动 `org.videolan.vlc.StartActivity`。
- 启动 Intent 额外添加了 `FLAG_ACTIVITY_NEW_TASK`。
- VLC 的多个 Activity 在 `vlc-android/application/vlc-android/AndroidManifest.xml` 中配置了 `android:taskAffinity=":vlc"`。
- `org.videolan.vlc.gui.MainActivity` 还配置了 `android:launchMode="singleTask"`。
- 用户在 VLC 页面选中视频后，VLC 调用 `MediaUtils.showUnityView()`，先向 Unity 发送播放消息，再对 VLC Activity 调用 `moveTaskToBack(true)`。

这套逻辑能工作，是因为 Unity 和 VLC 位于两个不同 task：

```text
Task A: UnityPlayerActivity
Task B: VLC StartActivity -> VLC MainActivity
```

播放时把 Task B 退到后台，Task A 里的 Unity 就会露出来。问题是，在 PICO 上 VLC 会被系统当成独立的 2D App 面板处理。ADB 排查时可以看到 VLC 页面仍然是 focused/obscuring window，所以 Home 键弹出的 PICO dock/home UI 可能被 VLC 面板遮住。

本方案的目标是：去掉 VLC 独立 task，同时尽量保留以下能力：

- 在 VLC 媒体库中选中视频后回到 Unity 播放。
- 从 Unity 再次返回 VLC 时，尽量回到之前的 VLC 页面和状态。
- 降低独立 VLC 2D 面板导致的系统弹窗遮挡和焦点冲突。

## 推荐方案

使用同一个 Android task，并通过 `FLAG_ACTIVITY_REORDER_TO_FRONT` 在 Unity Activity 和 VLC Activity 之间切换前台。

期望的 task 形态：

```text
初始:
UnityPlayerActivity

打开 VLC:
UnityPlayerActivity -> VLC StartActivity -> VLC MainActivity

选片回 Unity 播放:
VLC MainActivity -> UnityPlayerActivity

从 Unity 返回 VLC:
UnityPlayerActivity -> 上一次 VLC Activity
```

不要通过设置 View 隐藏、Window 透明、动态切换透明主题等方式“隐藏”VLC Activity。一个视觉上透明的顶层窗口仍然可能保持焦点、拦截输入，并在 PICO compositor 中继续被认为是 obscuring window。正确做法是让 Android 把目标 Activity 调到前台，让另一个 Activity 自然进入 `onPause/onStop`。

## 设计决策

### 1. 去掉独立 task 行为

Unity 启动代码调整：

- 文件：`Assets/Scripts/Infrastructure/VlcBridge/Launcher/VlcLibraryLauncher.cs`
- 从 `StartVLCActivity()` 中移除 `FLAG_ACTIVITY_NEW_TASK`。
- 继续从 Unity 当前 Activity 启动 `org.videolan.vlc.StartActivity`。

VLC Manifest 调整：

- 文件：`vlc-android/application/vlc-android/AndroidManifest.xml`
- 移除需要与 Unity 同 task 的 VLC UI Activity 上的 `android:taskAffinity=":vlc"`。
- 第一阶段至少覆盖：
  - `.StartActivity`
  - `.gui.MainActivity`
  - `.gui.video.VideoPlayerActivity`
  - 媒体库可能进入的二级页、设置页、浏览页等 VLC UI Activity。

检查 `android:launchMode="singleTask"`：

- 当前 `MainActivity` 和 `VideoPlayerActivity` 使用了 `singleTask`。
- 如果要保留 VLC 二级页面状态，`singleTask` 可能有副作用，因为它可能复用已有根 Activity，并清掉上方页面。
- 第一阶段可以优先尝试把 `MainActivity` 改为 `singleTop` 或标准启动模式，前提是真机验证 PICO 行为正常。
- 如果 VLC 上游逻辑依赖 `singleTask`，可以先保留它，只验证是否还能满足“回到上一次可接受页面”的需求。

### 2. 记录上一次 VLC Activity

当前已有能力：

- `VlcAppInitializer` 注册了 `Application.ActivityLifecycleCallbacks`。
- `AppContextProvider.currentActivity` 记录当前 resumed 的 Activity。
- `AppContextProvider.aliveActivities` 记录所有已创建且尚未销毁的 Activity 弱引用。
- 这些记录是全局的，可能包含 `UnityPlayerActivity`，不是 VLC 专用记录。

需要新增 VLC 专用记录：

- 文件：`vlc-android/application/resources/src/main/java/org/videolan/resources/AppContextProvider.kt`
- 新增字段：

```kotlin
var lastVlcActivity: java.lang.ref.WeakReference<Activity>? = null
var lastVlcActivityComponent: android.content.ComponentName? = null
```

生命周期更新：

- 文件：`vlc-android/application/vlc-android/src/org/videolan/vlc/VlcAppInitializer.kt`
- `onActivityResumed` 中继续保持 `currentActivity` 原有行为。
- 如果 resumed 的 Activity 属于 VLC，则更新 `lastVlcActivity` 和 `lastVlcActivityComponent`。

过滤规则建议：

```kotlin
private fun isVlcActivity(activity: Activity): Boolean {
    val className = activity.javaClass.name
    return className.startsWith("org.videolan.vlc.") &&
        className != "com.unity3d.player.UnityPlayerActivity" &&
        activity.javaClass.simpleName != "UnityPlayerActivity"
}
```

`org.videolan.vlc.` 前缀本身已经能排除 Unity，额外保留 Unity 判断是防御式写法，也和当前 `MediaUtils.isVlcPickerActivity()` 的风格一致。

### 3. 播放时替换 `moveTaskToBack(true)`

当前逻辑：

- 文件：`vlc-android/application/vlc-android/src/org/videolan/vlc/media/MediaUtils.kt`
- `showUnityView(context)` 发送 Unity 消息后调用 `moveVlcPickerTaskToBack(context)`。

新逻辑：

- 保留 `UnityMessageDispatcher.send(...)`，确保播放消息仍然发给 Unity。
- 将 `moveVlcPickerTaskToBack(context)` 替换为 `bringUnityToFront(context)`。
- `bringUnityToFront` 显式拉起 Unity Activity，并使用 `FLAG_ACTIVITY_REORDER_TO_FRONT`：

```kotlin
private fun bringUnityToFront(context: Context) {
    val intent = Intent().apply {
        component = ComponentName(context.packageName, "com.unity3d.player.UnityPlayerActivity")
        addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT)
    }
    context.startActivity(intent)
}
```

如果 `context` 不是 Activity，可以只在兜底路径添加 `FLAG_ACTIVITY_NEW_TASK`。优先路径应使用当前 VLC Activity 或存活的 VLC Activity context，这样切换仍发生在同一个 task 内。

不要在同 task 方案中调用：

- `moveTaskToBack(true)`
- `finishAndRemoveTask()`
- 将 `finish()` 作为主路径
- `FLAG_ACTIVITY_CLEAR_TOP`

这些做法要么会把整个同 task 栈退到后台，要么会破坏我们希望保留的 VLC 页面状态。

### 4. 从 Unity 返回之前的 VLC 页面

Android 侧新增一个桥接方法给 Unity 调用：

- 文件：`vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt`，或新增专门的 launcher bridge。
- 方法：`showLastVlcActivity(context: Context?)`。

行为：

1. 优先使用 `AppContextProvider.lastVlcActivityComponent`。
2. 通过 `FLAG_ACTIVITY_REORDER_TO_FRONT` 启动这个 component。
3. 如果没有记录，则回退到 `org.videolan.vlc.StartActivity` 或 `org.videolan.vlc.gui.MainActivity`。

示例：

```kotlin
@JvmStatic
fun showLastVlcActivity(context: Context?) {
    val baseContext = context ?: AppContextProvider.currentActivity ?: AppContextProvider.appContext
    val component = AppContextProvider.lastVlcActivityComponent
        ?: ComponentName(baseContext.packageName, "org.videolan.vlc.StartActivity")

    val intent = Intent().apply {
        this.component = component
        addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT)
    }

    if (baseContext !is Activity) {
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    }

    baseContext.startActivity(intent)
}
```

Unity 中用户点击“媒体库”或类似入口时，可以优先调用该桥接方法。如果之前没有打开过 VLC，或者桥接调用失败，再回退到现有 `StartVLCActivity()`。

### 5. `aliveActivities` 的使用边界

保留 `aliveActivities`，但不要用它来手动隐藏窗口。

适合使用的场景：

- 清理已销毁 Activity 的弱引用。
- 查找一个存活的 VLC Activity context，用于同 task `startActivity`。
- 调试当前存活的 Activity 列表。
- 当 `lastVlcActivityComponent` 不存在时，辅助选择最近的 VLC Activity。

不建议使用的场景：

- 将所有 VLC `decorView` 设置为 `GONE`。
- 将 VLC Window alpha 设置为 0。
- 运行时把 VLC theme 改成透明。
- 让 VLC 保持前台焦点，但假装它已经被隐藏。

## 实施任务

### 任务 1：新增 VLC 专用 Activity 记录

文件：

- 修改：`vlc-android/application/resources/src/main/java/org/videolan/resources/AppContextProvider.kt`
- 修改：`vlc-android/application/vlc-android/src/org/videolan/vlc/VlcAppInitializer.kt`

步骤：

1. 新增 `lastVlcActivity` 和 `lastVlcActivityComponent`。
2. 在生命周期回调附近或 `AppContextProvider` 中新增 `isVlcActivity(activity)`。
3. 在 `onActivityResumed` 中只把 VLC Activity 写入新增字段。
4. 保持 `currentActivity` 的现有行为不变。

### 任务 2：替换播放回 Unity 逻辑

文件：

- 修改：`vlc-android/application/vlc-android/src/org/videolan/vlc/media/MediaUtils.kt`

步骤：

1. 保留 `showUnityView(context)` 中向 Unity 发送播放消息的逻辑。
2. 将 `moveVlcPickerTaskToBack(context)` 替换为 `bringUnityToFront(context)`。
3. 旧的 `moveVlcPickerTaskToBack` 可以先保留但不再调用，真机验证稳定后再删除。
4. 增加日志：
   - `Reordering UnityPlayerActivity to front after media selection`
   - `Failed to reorder UnityPlayerActivity to front`

### 任务 3：新增 Unity 返回 VLC 的桥接能力

文件：

- 修改：`vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt`
- 修改：`Assets/Scripts/Infrastructure/VlcBridge/Launcher/VlcLibraryLauncher.cs`

步骤：

1. Android 侧新增 `showLastVlcActivity(context)`。
2. Unity C# 侧新增对应 wrapper。
3. Unity 需要重新打开媒体库时，优先调用该 wrapper。
4. 当 bridge 调用失败或没有 Android Activity 可用时，回退到现有 `StartVLCActivity()`。

### 任务 4：去掉独立 task 启动

文件：

- 修改：`Assets/Scripts/Infrastructure/VlcBridge/Launcher/VlcLibraryLauncher.cs`
- 修改：`vlc-android/application/vlc-android/AndroidManifest.xml`

步骤：

1. 从 Unity 启动 VLC 的路径中移除 `FLAG_ACTIVITY_NEW_TASK`。
2. 从媒体库和播放器相关的 VLC UI Activity 中移除 `android:taskAffinity=":vlc"`。
3. 检查 `MainActivity` 和 `VideoPlayerActivity` 的 `launchMode`。
4. 第一阶段先做最小 Manifest 改动，如果 ADB 仍然显示 VLC 独立 task，再扩大覆盖范围。

### 任务 5：静态检查与 Android 构建验证

静态检查命令：

```bash
rg -n "FLAG_ACTIVITY_NEW_TASK|moveTaskToBack|taskAffinity=\\\":vlc\\\"|launchMode=\\\"singleTask\\\"" \
  Assets/Scripts/Infrastructure/VlcBridge vlc-android/application/vlc-android
```

期望结果：

- Unity 启动 VLC 媒体库的路径不再添加 `FLAG_ACTIVITY_NEW_TASK`。
- 播放回 Unity 的路径不再调用 `moveTaskToBack(true)`。
- 需要与 Unity 同 task 的 VLC UI Activity 不再保留 `taskAffinity=":vlc"`。
- 如果仍有 `singleTask`，需要明确说明为什么保留。

构建 VLC AAR：

```bash
GRADLE_ABI=arm64-v8a java -classpath gradle/wrapper/gradle-wrapper.jar \
  org.gradle.wrapper.GradleWrapperMain :application:vlc-android:assembleDebug
```

将构建产物复制到：

```text
Assets/Plugins/Android/vlc-android-debug.aar
```

除非当前任务明确要求，否则不要运行 Unity Editor tests、Unity batchmode 或其他会启动 Unity Editor 的验证流程。

### 任务 6：PICO 真机验证

从 Unity 打开 VLC 后执行：

```bash
adb shell dumpsys activity activities | grep -E "Task #|UnityPlayerActivity|org.videolan.vlc|topResumedActivity|ResumedActivity"
adb shell dumpsys window windows | grep -E "mCurrentFocus|mFocusedApp|mObscuringWindow|UnityPlayerActivity|org.videolan.vlc"
adb shell dumpsys display | grep -E "NS_APP\\[com.ijkm.xr_vlc\\]|DisplayDeviceInfo"
```

打开 VLC 后期望：

- Unity 和 VLC 应在同一个 task 中，或至少 VLC 不再表现为独立的 `:vlc` task。
- 浏览媒体库时 VLC 可以正常获得焦点。

选中视频后期望：

- Unity 成为 focused/resumed Activity。
- VLC 不再是 focused/obscuring top window。
- Unity 能收到 VLC 发来的播放 payload 并开始播放。

从 Unity 返回 VLC 后期望：

- 通过 `REORDER_TO_FRONT` 回到上一次 VLC 页面。
- 只要 Android 没有销毁该 Activity，媒体库状态应尽量保留。

Home 键弹窗验证：

- 选片回 Unity 播放后按 Home。
- PICO dock/home 弹窗不应再被 VLC 媒体库窗口遮住。

面板距离与弧面验证：

- 对比独立 task 方案下的 VLC 面板距离和弧度。
- 本方案主要解决焦点和遮挡问题，不保证能控制 PICO 2D 面板的距离或弧面。距离和弧面仍由 PICO Shell 策略决定，不是标准 Android API 能直接配置的内容。

## 风险

### PICO 仍可能创建独立虚拟显示

即使 Android task 合并，PICO 仍可能为 VLC 创建 `NS_APP[...]` 虚拟显示。成功标准不是“完全没有虚拟显示”，而是播放回 Unity 后 VLC 不再保持 focused/obscuring top window。

### `singleTask` 可能破坏中间页面状态

如果 `MainActivity` 保留 `singleTask`，从 Unity 返回 VLC 时可能回到媒体库主页，而不是之前的二级页面。如果必须保留二级页面，需要验证将 `MainActivity` 改为 `singleTop` 或标准启动模式。

### `REORDER_TO_FRONT` 在 PICO 上可能有设备差异

`FLAG_ACTIVITY_REORDER_TO_FRONT` 是标准 Android 行为，但 PICO compositor 可能有额外策略。必须在目标头显上验证。

### Android 可能重建 VLC Activity

如果系统因内存压力销毁了停止状态的 VLC Activity，`lastVlcActivityComponent` 只能重新打开对应页面类，不能保证恢复 fragment、滚动位置、临时筛选条件等状态。除非 VLC 自身已经持久化这些状态。

## 回滚方案

如果同 task + `REORDER_TO_FRONT` 方案出现明显回归：

1. 恢复 Unity 启动 VLC 时的 `FLAG_ACTIVITY_NEW_TASK`。
2. 恢复 VLC Activity 上的 `android:taskAffinity=":vlc"`。
3. 恢复选片后的 `moveTaskToBack(true)`。
4. 保留此前已实现的 AAR 启动预热和 Unity 透明背景改动，因为它们与本方案相互独立。

## 推荐落地顺序

建议先做一个最小可验证版本，不要一次性改完所有 VLC Activity：

1. 新增 `lastVlcActivityComponent` 记录。
2. 将播放回 Unity 的逻辑替换为 `REORDER_TO_FRONT UnityPlayerActivity`。
3. 移除 Unity 启动 VLC 时的 `FLAG_ACTIVITY_NEW_TASK`。
4. 先移除 `.StartActivity` 和 `.gui.MainActivity` 的 `taskAffinity=":vlc"`。
5. 构建 AAR 并在 PICO 真机验证。

只有当 ADB 证明第一阶段确实改善了焦点和 Home 弹窗遮挡问题后，再扩大 Manifest 清理范围，覆盖更多 VLC 二级页面。
