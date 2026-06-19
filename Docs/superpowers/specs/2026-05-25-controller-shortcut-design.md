# 手柄快捷播放操作设计方案

**日期**：2026-05-25  
**状态**：待实现

---

## 背景

当前 `VRUIManager` 只用右手 Trigger 触发显示/隐藏控制面板，其余手柄按键未被利用。本方案为 PICO XR 手柄新增快捷播放操作，支持部分按键可配置映射，配置数据在 Unity 播放面板和 VLC AAR 设置页双向共享。

---

## 一、按键与操作对应关系

### 摇杆说明

左手摇杆与右手摇杆**功能完全相同**，任意一侧均可触发操作，不区分左右手。

### 对称设计原则

左右手柄功能对称：每类按键在两侧均有对应，固定操作两侧行为相同，可配置按键两侧**独立配置**。

### 固定映射（不可配置）

| 按键 | 手 | 触发方式 | 操作 |
|---|---|---|---|
| 任意摇杆左推 | 左 / 右 | `axis.x < -0.5`，边沿触发 | 快退 N 秒 |
| 任意摇杆右推 | 左 / 右 | `axis.x > +0.5`，边沿触发 | 快进 N 秒 |
| 任意摇杆上推 | 左 / 右 | `axis.y > +0.5`，持续触发 | 屏幕拉近 |
| 任意摇杆下推 | 左 / 右 | `axis.y < -0.5`，持续触发 | 屏幕推远 |
| A 键（右）/ X 键（左） | 左 / 右 | 边沿触发 | 播放 / 暂停 |
| Grip（抓取键） | 左 / 右 | 持续按住 | 移动屏幕 |
| Trigger 按下 | 右手 | 已有逻辑 | 显示 / 隐藏控制面板 |

**N 秒配置**：复用 VLC 现有 `video_jump_delay`（Int，默认 10 秒），VLC 设置 → 播放器控制 → 跳转时长中调整，两侧摇杆共享同一数值，Unity 只读取不重复配置。

**摇杆上下触发方式**：持续按住期间每帧连续调整屏幕距离（持续触发），松手停止。

**Grip 移动屏幕**：任意手按住 Grip 期间，VideoScreen 持续跟随该手控制器朝向更新（保持当前距离）；双手同时按住时以最后检测到的手为准；松开后固定在当前位置。

**摇杆边沿检测**：每帧对左右手分别独立检测，任意一侧触发即执行，两侧不互斥。

### 可配置映射（默认值）

| 按键标识符 | 手 | 对称按键 | 默认操作 |
|---|---|---|---|
| `right_stick_click` | 右手摇杆按下（R3） | ↔ `left_stick_click` | `toggle_2x_speed` |
| `left_stick_click` | 左手摇杆按下（L3） | ↔ `right_stick_click` | `toggle_2x_speed` |
| `button_b` | 右手 B 键 | ↔ `button_y` | `toggle_subtitle` |
| `button_y` | 左手 Y 键 | ↔ `button_b` | `toggle_subtitle` |

### 可选 Action 表

| action key | 说明 |
|---|---|
| `none` | 无操作 |
| `toggle_2x_speed` | 切换 2×/1× 播放速度 |
| `toggle_subtitle` | 显示 / 隐藏字幕轨道 |

扩展新操作只需：在常量表追加 action key → 在 `ExecuteAction()` 追加 case → 在两侧配置 UI 追加下拉选项。

---

## 二、数据模型（SharedPreferences）

### 存储方式

复用 VLC 原有存储机制：`PreferenceManager.getDefaultSharedPreferences(context)`，落盘文件：

```
/data/data/{packageName}/shared_prefs/{packageName}_preferences.xml
```

同一 APK 内，Unity 与 VLC AAR 读写同一文件，无跨进程问题。

### 新增 Key

在 `Settings.kt`（tools 模块）仅追加一个新 key：

```kotlin
const val KEY_XR_BUTTON_MAPPINGS = "xr_button_mappings"  // String（JSON）
```

快退/快进秒数直接复用现有：

```kotlin
const val KEY_VIDEO_JUMP_DELAY = "video_jump_delay"  // 已存在，Int，默认 10
```

### `xr_button_mappings` JSON 结构

```json
{
  "right_stick_click": "toggle_2x_speed",
  "left_stick_click":  "toggle_2x_speed",
  "button_b":          "toggle_subtitle",
  "button_y":          "toggle_subtitle"
}
```

- key：按键标识符（字符串常量，见下表）
- value：action key（字符串常量，见上表）
- 缺失的 key 视为 `none`
- 固定映射（A/X 键、Grip、摇杆上下、Trigger）不在此 JSON 中，硬编码于 `ControllerShortcutManager`
- 新增可配置按键直接追加 JSON 字段，不改结构

### 按键标识符表（当前版本）

| 标识符 | 手 | 对称标识符 | 说明 |
|---|---|---|---|
| `right_stick_click` | 右 | `left_stick_click` | 右手摇杆按下（R3） |
| `left_stick_click` | 左 | `right_stick_click` | 左手摇杆按下（L3） |
| `button_b` | 右 | `button_y` | 右手 B 键 |
| `button_y` | 左 | `button_b` | 左手 Y 键 |

### 默认值

首次运行时 `xr_button_mappings` key 不存在，`ControllerShortcutConfig` 返回内置默认 JSON：

```json
{
  "right_stick_click": "toggle_2x_speed",
  "left_stick_click":  "toggle_2x_speed",
  "button_b":          "toggle_subtitle",
  "button_y":          "toggle_subtitle"
}
```

---

## 三、架构组件

### Unity 侧（新增文件）

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/XR/VlcSharedPreferences.cs` | 通用 JNI 层，读写任意 VLC SharedPreferences key |
| `Assets/Scripts/XR/ControllerShortcutConfig.cs` | 读写 `xr_button_mappings` JSON；读取 `video_jump_delay` |
| `Assets/Scripts/XR/ControllerShortcutManager.cs` | `Update()` 轮询输入，边沿检测，执行操作 |
| `Assets/Scripts/UI/ShortcutConfigPanel.cs` | 配置 UI 逻辑：两个 Dropdown + 保存/关闭 |

### VLC AAR 侧（新增文件）

| 文件 | 职责 |
|---|---|
| `res/xml/preferences_xr_controller.xml` | 配置页 XML：两个 `ListPreference` |
| `gui/preferences/PreferencesXRController.kt` | 继承 `BasePreferenceFragment`，手动管理 JSON 序列化 |
| `res/xml/preferences_optional.xml` | 追加入口 `Preference`，指向上面 Fragment |

`VRUIManager.HandleTriggerInput()` 保持不动；`ControllerShortcutManager` 作为独立 MonoBehaviour 挂载，与 VRUIManager 解耦。

### 数据流

```
[Unity 配置面板]
    ShortcutConfigPanel ──Save()──▶ ControllerShortcutConfig ──▶ VlcSharedPreferences(JNI)
                                                                        │
[VLC AAR 配置页]                                                        ▼
    PreferencesXRController ──自动写入──────────────────────▶ SharedPreferences 文件
                                                                        │
[运行时读取]                                                            │
    ControllerShortcutManager.Update() ──▶ ControllerShortcutConfig ──▶ VlcSharedPreferences(JNI)
```

配置生效时机：Unity 面板保存后下一帧即生效；VLC 配置页改动后 Unity 下次启动时读取（遥控配置不需要播放中途热更新）。

---

## 四、关键实现细节

### `VlcSharedPreferences.cs`（通用 JNI 层）

不依赖 AAR 侧新增代码，直接通过包名访问 SharedPreferences 文件：

```csharp
// 获取 SharedPreferences 实例
// activity.getSharedPreferences(packageName + "_preferences", MODE_PRIVATE)
public static class VlcSharedPreferences
{
    public static int    GetInt(string key, int defaultValue = 0)        { ... }
    public static string GetString(string key, string defaultValue = "") { ... }
    public static bool   GetBool(string key, bool defaultValue = false)  { ... }
    public static float  GetFloat(string key, float defaultValue = 0f)   { ... }

    public static void PutInt(string key, int value)       { ... }
    public static void PutString(string key, string value) { ... }
    public static void PutBool(string key, bool value)     { ... }
    // 每次 Put 调用后立即 apply()
}
```

可被项目任意脚本调用，读取 VLC 任意配置（如 `"hardware_acceleration"`、`"subtitle_preferred_language"` 等）。

### `ControllerShortcutManager.cs` 输入检测

```csharp
// Update() 中的边沿检测逻辑（伪代码）
float axisX = GetThumbstickAxis(hand).x;
if (axisX > 0.5f && _prevAxisX <= 0.5f)
    playbackService.SeekTo(currentTime + seekSeconds * 1000L);
else if (axisX < -0.5f && _prevAxisX >= -0.5f)
    playbackService.SeekTo(currentTime - seekSeconds * 1000L);
_prevAxisX = axisX;

// 遥感按下边沿检测
bool clicked = GetThumbstickClick(hand);
if (clicked && !_prevClicked)
    ExecuteAction(config.GetActionForButton(buttonId));
_prevClicked = clicked;
```

### `PreferencesXRController.kt` JSON 管理

```kotlin
// onCreatePreferences 时，读取 JSON 填入 ListPreference 初始值
// onSharedPreferenceChanged 时，从各 ListPreference 重建 JSON 写回
// ListPreference 的 android:key 使用临时内部 key（不落盘到独立字段）
// 仅 xr_button_mappings 落盘
```

### 播放时配置读取与 Action 触发

#### 配置缓存生命周期

JNI 读取 SharedPreferences 有开销，不在每帧 `Update()` 中调用，采用**启动时加载 + 显式刷新**策略：

```
ControllerShortcutManager.Start()
    └─▶ ControllerShortcutConfig.Reload()
            ├─ VlcSharedPreferences.GetInt("video_jump_delay", 10)  → 缓存 _seekSeconds
            └─ VlcSharedPreferences.GetString("xr_button_mappings", defaultJson)
                    └─ JsonUtility 解析 → 缓存 _mappings: Dictionary<string, string>

ShortcutConfigPanel.OnSaveClicked()
    ├─ VlcSharedPreferences.PutString("xr_button_mappings", newJson)  // 写入落盘
    └─ ControllerShortcutManager.ReloadConfig()                       // 刷新内存缓存

Update() 每帧
    └─ 只读 _seekSeconds、_mappings（纯内存，无 JNI）
```

#### Update() 输入检测与派发完整流程

```csharp
void Update()
{
    foreach (XRNode hand in new[] { XRNode.LeftHand, XRNode.RightHand })
    {
        Vector2 axis = GetThumbstickAxis(hand);

        // 1. 摇杆左右推 — 快退/快进（边沿触发）
        if (axis.x > 0.5f && _prevAxis[hand].x <= 0.5f)
            playbackService.SeekTo(_currentTime + _seekSeconds * 1000L);
        else if (axis.x < -0.5f && _prevAxis[hand].x >= -0.5f)
            playbackService.SeekTo(_currentTime - _seekSeconds * 1000L);

        // 2. 摇杆上下推 — 屏幕距离（持续触发）
        if (axis.y > 0.5f)
            AdjustScreenDistance(+_distanceStep * Time.deltaTime);
        else if (axis.y < -0.5f)
            AdjustScreenDistance(-_distanceStep * Time.deltaTime);

        _prevAxis[hand] = axis;

        // 3. 摇杆按下 — 可配置行为（边沿触发）
        if (hand == XRNode.LeftHand)
            CheckConfigurableButton(hand, "left_stick_click", CommonUsages.primary2DAxisClick);
        else
            CheckConfigurableButton(hand, "right_stick_click", CommonUsages.primary2DAxisClick);
    }

    // 4. A/X 键 — 播放/暂停（固定，两侧对称，边沿触发）
    CheckFixedButton(XRNode.RightHand, CommonUsages.primaryButton, ref _prevButtonA,
        () => playbackService.TogglePlayPause());
    CheckFixedButton(XRNode.LeftHand, CommonUsages.primaryButton, ref _prevButtonX,
        () => playbackService.TogglePlayPause());

    // 5. B/Y 键 — 可配置行为（两侧独立，边沿触发）
    CheckConfigurableButton(XRNode.RightHand, "button_b", CommonUsages.secondaryButton);
    CheckConfigurableButton(XRNode.LeftHand,  "button_y", CommonUsages.secondaryButton);

    // 6. Grip — 移动屏幕（两侧对称，持续触发，任意手触发均有效）
    bool rightGrip = GetButton(XRNode.RightHand, CommonUsages.gripButton);
    bool leftGrip  = GetButton(XRNode.LeftHand,  CommonUsages.gripButton);
    if (rightGrip) UpdateScreenPositionToController(XRNode.RightHand);
    else if (leftGrip) UpdateScreenPositionToController(XRNode.LeftHand);
}
```

#### 屏幕操作实现

**屏幕距离调整**（摇杆上下）：

```csharp
void AdjustScreenDistance(float delta)
{
    _screenDistance = Mathf.Clamp(_screenDistance + delta, MinDistance, MaxDistance);
    Vector3 pos = videoScreen.transform.position;
    // 保持水平位置，只调整 Z（相对摄像机前方）
    videoScreen.transform.position = Camera.main.transform.position
        + Camera.main.transform.forward * _screenDistance;
    videoScreen.transform.LookAt(Camera.main.transform);  // 始终面向玩家
    videoScreen.transform.Rotate(0, 180f, 0);
}
```

常量：`MinDistance = 1.0f`，`MaxDistance = 10.0f`，`_distanceStep = 3.0f`（米/秒）。

**移动屏幕**（Grip 按住）：

```csharp
void UpdateScreenPositionToController(XRNode hand)
{
    var device = InputDevices.GetDeviceAtXRNode(hand);
    if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 ctrlPos)
     && device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion ctrlRot))
    {
        Vector3 forward = ctrlRot * Vector3.forward;
        videoScreen.transform.position = ctrlPos + forward * _screenDistance;
        videoScreen.transform.LookAt(Camera.main.transform);
        videoScreen.transform.Rotate(0, 180f, 0);
    }
}
```

松开 Grip 后屏幕保持当前位置，`_screenDistance` 保留供摇杆继续调整。

#### ExecuteAction 实现

```csharp
void ExecuteAction(string actionKey)
{
    switch (actionKey)
    {
        case "toggle_2x_speed":
            _is2xSpeed = !_is2xSpeed;
            playbackService.SetPlaybackRate(_is2xSpeed ? 2.0f : 1.0f);
            break;

        case "toggle_subtitle":
            if (_subtitleEnabled)
            {
                _lastSubtitleTrackId = playbackService.CurrentMedia?.SpuTrack;
                playbackService.SetSubtitleTrack("");   // 空串关闭字幕
                _subtitleEnabled = false;
            }
            else
            {
                playbackService.SetSubtitleTrack(_lastSubtitleTrackId ?? "");
                _subtitleEnabled = true;
            }
            break;

        case "none":
        default:
            break;
    }
}
```

#### 状态字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `_seekSeconds` | `int` | 缓存自 `video_jump_delay` |
| `_mappings` | `Dictionary<string,string>` | 缓存自 `xr_button_mappings` JSON |
| `_prevAxis` | `Dictionary<XRNode,Vector2>` | 上帧各手摇杆轴值 |
| `_prevStickPressed` | `Dictionary<XRNode,bool>` | 上帧各手摇杆按下状态 |
| `_prevButtonA` | `bool` | 上帧右手 A 键状态 |
| `_prevButtonX` | `bool` | 上帧左手 X 键状态 |
| `_is2xSpeed` | `bool` | 当前是否处于 2× 速 |
| `_subtitleEnabled` | `bool` | 当前字幕是否显示 |
| `_lastSubtitleTrackId` | `string` | 关闭字幕前记录的轨道 ID |
| `_currentTime` | `long` | 当前播放时间（ms），订阅 `OnTimeChanged` 更新 |
| `_screenDistance` | `float` | 屏幕当前距离（米），初始值 3.0f |

`_currentTime` 通过订阅 `PlaybackService.OnTimeChanged` 事件保持同步，不在 `Update()` 中额外查询。

#### 注意事项

- `SetPlaybackRate` 在 `PlaybackService` 中当前标注为未实现，需 AAR Bridge 侧补充 `setRate(float)` 方法；本方案调用路径已预留。
- 字幕关闭传空串，需确认 AAR 侧 `setSpuTrack("")` 行为；若 AAR 用 `"-1"` 表示关闭，常量需对齐。
- 屏幕移动依赖 `videoScreen.transform`，全景模式（Sphere 投影）下此操作无意义，应在 `UpdateScreenPositionToController` 入口判断当前投影类型并跳过。

### Unity 配置面板说明

- 四个 Dropdown（左右对称，各自独立）：左摇杆按下 / 右摇杆按下 / Y 键（左）/ B 键（右）
- 跳转秒数不在此面板配置，显示提示：*"跳转时长沿用 VLC 设置 → 播放器控制 → 跳转时长"*
- 保存按钮：序列化 JSON → `VlcSharedPreferences.PutString(KEY_XR_BUTTON_MAPPINGS, json)`

VLC AAR 配置页同步更新为四个 `ListPreference`（left_stick_click / right_stick_click / button_y / button_b）。

---

## 五、扩展性设计

### 新增可配置按键

1. 按键标识符常量表追加一行（Unity C# + Kotlin 各一处）
2. `preferences_xr_controller.xml` 追加一个 `ListPreference`
3. `ShortcutConfigPanel` 追加一个 Dropdown
4. `ControllerShortcutManager` 追加输入检测分支

### 新增操作类型

1. action key 常量表追加一行
2. `ControllerShortcutManager.ExecuteAction()` 追加一个 `case`
3. 两侧配置 UI 下拉选项各追加一条

### 读取其他 VLC 配置

任意 Unity 脚本直接调用：

```csharp
int jumpDelay = VlcSharedPreferences.GetInt("video_jump_delay", 10);
bool hwAccel  = VlcSharedPreferences.GetBool("hardware_acceleration", true);
```

---

## 六、不在本方案范围内

- 左手 X 键固定为播放/暂停，不可配置（与右手 A 键对称）
- 遥感上下用于音量调节（当前已用于屏幕距离）
- 播放速度多档切换（当前仅 2×/1× 切换）
- 全景模式下屏幕移动的替代交互（本方案入口处跳过）
- 配置页多语言字符串（沿用 VLC 现有 i18n 机制，后续补充）
