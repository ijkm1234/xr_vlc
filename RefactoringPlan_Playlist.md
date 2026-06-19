# VLC-Unity 播放列表状态下沉重构方案

## 1. 重构背景与目标

目前，Unity 端和 Android AAR 端都在维护自己的播放列表（Unity 端的 `PlaylistManager` vs AAR 端的 `PlaylistManager`）。这种双端维护状态容易导致数据不一致（如循环状态不同步、切歌时机不匹配）。

**核心目标：**
将所有播放列表相关的状态管理（包含上下曲切换、列表持久化、随机/循环模式）全部下沉至 Android AAR 端。Unity 端仅作为 UI 展示和画布渲染容器。

**关键约束：**
必须保持现有的“播放握手链路”**完全不变**。即：
`MediaUtils 拦截选中的视频 -> 发送 JSON 给 Unity -> Unity 解析并请求 AAR 获取尺寸 -> Unity 准备好画布 -> 告诉 AAR 开始 play()`

## 2. 核心重构逻辑

### 2.1 修复 `AAR play()` 覆盖列表问题
在当前的握手链路最后一步，Unity 调用 `PlaybackServiceBridge.play()` 时，AAR 会将传递来的 URI 单独构建为一个只包含一首歌的列表并调用 `service.load()`，从而**清空了原本完整的播放列表**。
- **改造方案**：在 `play()` 中加入校验。如果 `pendingUri` 对应的就是当前 AAR `PlaylistManager` 正在准备播放的那个媒体，则跳过 `load()`，直接调用 `service.play()` 开始解码渲染。

### 2.2 拦截 AAR 的自动/手动切歌
当 AAR 端播放完当前视频触发自动下一首，或者被调用了 `next()` 准备播放下一个视频时，它默认会直接开始解码渲染，这会跳过 Unity 准备画布的流程。
- **改造方案**：在 AAR 端的 `PlaylistManager.kt -> playIndex` 方法中，在真正将媒体交给播放器前进行拦截。将即将播放的媒体通过 `MediaUtils.sendVideoToUnity` 回传给 Unity。这样一来，不管是自动切歌还是列表连播，都会重新走一遍标准的“通知 Unity -> 建画布 -> 播放”的握手链路。

### 2.3 暴露 AAR 播放控制能力
在 `PlaybackServiceBridge` 中新增 JNI 接口：
- `next()`: 播放下一首。
- `previous()`: 播放上一首。
- `setRepeatMode(mode)`: 设置循环模式。
- `setShuffle(enabled)`: 设置随机播放。

## 3. 实施步骤

### Phase 1: 改造 Android AAR 侧
1. 修改 `PlaybackServiceBridge.kt`，新增 `next()`, `previous()`, `setRepeatMode()`, `setShuffle()` 方法。
2. 调整 `PlaybackServiceBridge.play()` 逻辑，防止覆盖当前播放列表。
3. 修改 `PlaylistManager.kt` 中的 `playIndex` 逻辑，识别到是视频流且是切歌时，挂起播放并调用 `MediaUtils.sendVideoToUnity` 将控制权交给 Unity 握手链路。

### Phase 2: 改造 Unity Bridge 侧
1. 在 `PlaybackAarBridge.cs` 中增加对应的 C# 调用封装，映射到新增的 JNI 方法。

### Phase 3: 清理 Unity 业务代码
1. 删除 `Assets/Scripts/Core/PlaylistManager.cs`。
2. 改造 `Assets/Scripts/Core/PlaybackService.cs`，移除所有本地播放队列的维护代码。
3. 将 Unity 的 UI 按钮（如上一曲、下一曲、循环模式按钮）直接绑定到 `PlaybackAarBridge` 的控制接口上。
