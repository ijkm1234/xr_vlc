# UI 系统控制按钮补齐设计

## 目标

补齐播放 UI 面板上的系统相关能力：

- 亮度调节
- 音量调节
- 时间显示
- 电量显示

## 交互约束

- 保持当前按钮位置不变。
- 亮度按钮和音量按钮位于当前面板上方区域。
- 点击亮度或音量按钮时，在对应按钮上方显示一个滑条。
- 滑条使用与视频进度条相同的 Unity UI `Slider` 交互控件。
- 打开一个系统滑条时关闭其他二级弹窗；再次点击按钮或点击其他区域时关闭。
- 不进行本地 Unity Editor 测试。

## 运行时行为

### 时间

`systemTimeText` 继续显示当前本地时间，格式为 `HH:mm`。

### 电量

`batteryText` 显示当前电量百分比。Android/PICO 真机通过系统电池状态读取；非 Android 环境或读取失败时显示 `--%`。

### 音量

音量滑条范围为 `0-1`。Android/PICO 真机通过 `AudioManager.STREAM_MUSIC` 读取和设置媒体音量；非 Android 环境使用运行时模拟值，避免影响编辑器或桌面系统音量。

### 亮度

亮度滑条范围为 `0-1`。Android/PICO 真机优先设置当前 Activity window brightness，不写系统全局亮度，避免依赖系统设置写入权限；非 Android 环境使用运行时模拟值。

## 实现边界

- 主要改动集中在 `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`。
- 复用现有弹窗关闭、排序、定位和 UI tree 注册思路。
- 不移动场景中已有按钮，不重排顶部控制区域。
- 不新增全局设置页入口，不把亮度/音量放入 settings tab。
