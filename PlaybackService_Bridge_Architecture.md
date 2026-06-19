# XRVLC 跨端架构升级方案：基于 PlaybackService 与 Surface 桥接的硬件渲染链路

## 1. 架构目标与背景

当前架构中，Unity 侧通过 `LibVLCSharp` 创建独立的 LibVLC 实例进行播放，而 AAR 侧也维护着自己的 `PlaybackService` 和 LibVLC 实例。这导致了严重的**实例壁垒**问题：
- **凭据断层**：Unity 的 LibVLC 实例无法共享 AAR 侧（Java层）浏览网络目录时建立的 SMB Session 和 Keystore 缓存，导致 URL 盲查失败，引发无法连接的错误。
- **性能损耗**：Unity 使用内存缓冲区进行视频帧的中转，消耗了大量的 CPU 资源和内存带宽，无法做到极致的“零拷贝”硬件渲染。

**本方案旨在将播放引擎彻底下沉至 AAR 层的 `PlaybackService`**。Unity 侧退化为纯粹的 3D 渲染器和交互层：
1. Unity 生成 Android `Surface`（通过 PICO SDK），并传递给 AAR。
2. AAR 层的 `PlaybackService` 负责视频的加载、播放管理、以及硬件解码到传入的 `Surface` 上。
3. AAR 将播放状态通过回调桥接给 Unity 更新 UI。

这种架构能实现**单例状态复用（彻底解决 SMB 密码问题）**以及**真正的零拷贝硬件渲染（解决性能发热问题）**。

---

## 2. 系统模块设计

### 2.1 Android AAR 侧：`PlaybackServiceBridge`

在 `vlc-android` 工程中新建一个单例桥接类 `PlaybackServiceBridge.kt`，作为 Unity 与 `PlaybackService` 交互的唯一入口。

**核心职责：**
- 接收 Unity 传来的 Surface 句柄，并绑定到 `PlaybackService` 的 MediaPlayer 上。
- 封装 `PlaybackService` 的控制方法（Load, Play, Pause, Seek）。
- 注册 `PlaybackService.Callback`，监听播放状态改变并回调给 Unity。

**关键代码设计：**
```kotlin
package org.videolan.vlc.bridge

import android.view.Surface
import com.unity3d.player.UnityPlayer
import org.videolan.vlc.PlaybackService
import org.videolan.vlc.media.PlayerController

object PlaybackServiceBridge : PlaybackService.Callback {
    private var playbackService: PlaybackService? = null
    
    // 必须在 AAR 初始化或 Service 启动时绑定
    fun bindService(service: PlaybackService) {
        this.playbackService = service
        service.addCallback(this)
    }

    // 1. 供 Unity JNI 调用的 Surface 绑定接口
    @JvmStatic
    fun setVideoSurface(surface: Surface) {
        val player = playbackService?.mediaplayer ?: return
        val vout = player.vlcVout
        vout.setVideoSurface(surface, null)
        vout.attachViews()
    }
    
    @JvmStatic
    fun detachVideoSurface() {
        playbackService?.mediaplayer?.vlcVout?.detachViews()
    }

    // 2. 供 Unity JNI 调用的播放控制接口
    @JvmStatic
    fun loadLocation(url: String) {
        playbackService?.loadLocation(url)
    }
    
    @JvmStatic
    fun play() = playbackService?.play()
    
    @JvmStatic
    fun pause() = playbackService?.pause()
    
    @JvmStatic
    fun stop() = playbackService?.stop(false, true)

    // 3. 状态回调给 Unity
    override fun onMediaPlayerEvent(event: MediaPlayer.Event) {
        // 根据 event.type 转换状态，并通过 UnitySendMessage 发给 Unity
        when (event.type) {
            MediaPlayer.Event.Playing -> sendToUnity("OnStateChanged", "Playing")
            MediaPlayer.Event.Paused -> sendToUnity("OnStateChanged", "Paused")
            MediaPlayer.Event.EndReached -> sendToUnity("OnStateChanged", "Ended")
            MediaPlayer.Event.EncounteredError -> sendToUnity("OnStateChanged", "Error")
            MediaPlayer.Event.TimeChanged -> sendToUnity("OnTimeChanged", event.timeChanged.toString())
        }
    }
    
    private fun sendToUnity(method: String, msg: String) {
        try {
            UnityPlayer.UnitySendMessage("PlaybackServiceBridgeReceiver", method, msg)
        } catch (e: Exception) { e.printStackTrace() }
    }
    // ... 其他 Callback 必须实现的方法
}
```
*注：在 `PlaybackService.kt` 的 `onCreate` 或 `onStartCommand` 中调用 `PlaybackServiceBridge.bindService(this)`。*

---

### 2.3 播放回调与状态同步机制

为了让 Unity 侧的 UI（如进度条、播放/暂停按钮）能实时反映底层状态，AAR 的 `PlaybackService` 需要将状态和时间同步给 Unity。这通过实现 `PlaybackService.Callback` 接口并结合 `UnitySendMessage` 来完成。

**1. AAR 侧注册与事件分发：**
在 `PlaybackServiceBridge` 中，我们需要监听三类核心事件：
- `onMediaPlayerEvent`：来自 LibVLC 的底层事件（播放、暂停、缓冲、时间改变）。
- `update`：来自 `PlaybackService` 的上层状态事件（通常是生命周期和媒体元数据改变）。
- `IVLCVout.Callback`：来自视频输出模块的事件，用于**获取真实的视频分辨率**。

```kotlin
package org.videolan.vlc.bridge

import com.unity3d.player.UnityPlayer
import org.videolan.libvlc.MediaPlayer
import org.videolan.libvlc.interfaces.IVLCVout
import org.videolan.vlc.PlaybackService

object PlaybackServiceBridge : PlaybackService.Callback, IVLCVout.Callback {
    private var playbackService: PlaybackService? = null
    
    fun bindService(service: PlaybackService) {
        this.playbackService = service
        service.addCallback(this)
        // 监听视频输出的尺寸变化
        service.mediaplayer.vlcVout.addCallback(this)
    }

    // ... setVideoSurface 等绑定代码略

    // 核心事件 1：获取真实的视频分辨率
    override fun onNewVideoLayout(vout: IVLCVout, width: Int, height: Int, visibleWidth: Int, visibleHeight: Int, sarNum: Int, sarDen: Int) {
        // 当视频流被成功解析出尺寸时触发
        sendToUnity("OnVideoSizeChanged", "$width|$height")
    }
    
    override fun onSurfacesCreated(vout: IVLCVout) {}
    override fun onSurfacesDestroyed(vout: IVLCVout) {}

    // 核心事件 2：底层播放器事件 (时间更新、状态改变)
    override fun onMediaPlayerEvent(event: MediaPlayer.Event) {
        when (event.type) {
            MediaPlayer.Event.Playing -> sendToUnity("OnStateChanged", "Playing")
            MediaPlayer.Event.Paused -> sendToUnity("OnStateChanged", "Paused")
            MediaPlayer.Event.EndReached -> sendToUnity("OnStateChanged", "Ended")
            MediaPlayer.Event.EncounteredError -> sendToUnity("OnStateChanged", "Error")
            MediaPlayer.Event.TimeChanged -> sendToUnity("OnTimeChanged", event.timeChanged.toString())
            MediaPlayer.Event.PositionChanged -> sendToUnity("OnPositionChanged", event.positionChanged.toString())
            MediaPlayer.Event.LengthChanged -> sendToUnity("OnLengthChanged", event.lengthChanged.toString())
            MediaPlayer.Event.Buffering -> sendToUnity("OnBuffering", event.buffering.toString())
        }
    }

    // 核心事件 2：上层服务状态更新
    override fun update() {
        val service = playbackService ?: return
        val isPlaying = service.isPlaying
        val media = service.currentMediaWrapper
        if (media != null) {
            // 发送当前播放视频的标题和总时长
            sendToUnity("OnMediaChanged", "${media.title}|${service.length}")
        }
    }
    
    // 其他必须实现的 Callback 方法 (可留空)
    override fun onMediaEvent(event: org.videolan.libvlc.interfaces.IMedia.Event) {}
    override fun onMediaPlayerReady() {}

    private fun sendToUnity(method: String, msg: String) {
        try {
            // 参数1: Unity场景中接收回调的 GameObject 名称
            // 参数2: 调用的方法名
            // 参数3: 传递的字符串参数
            UnityPlayer.UnitySendMessage("PlaybackServiceBridgeReceiver", method, msg)
        } catch (e: Exception) { e.printStackTrace() }
    }
}
```

**2. Unity 侧接收回调：**
在 Unity 场景中创建一个名为 `PlaybackServiceBridgeReceiver` 的 GameObject（名称必须与 `UnitySendMessage` 中的参数1完全一致），并挂载同名 C# 脚本：

```csharp
using UnityEngine;
using System;

public class PlaybackServiceBridgeReceiver : MonoBehaviour
{
    // 定义 C# 事件，供 UI 组件订阅
    public static event Action<string> OnStateChangedEvent;
    public static event Action<long> OnTimeChangedEvent;
    public static event Action<float> OnPositionChangedEvent;
    public static event Action<long> OnLengthChangedEvent;
    public static event Action<string, long> OnMediaChangedEvent;
    public static event Action<int, int> OnVideoSizeChangedEvent;

    // 必须确保该对象在场景切换时不被销毁
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    // --- 以下方法由 AAR 通过 UnitySendMessage 反射调用 ---

    public void OnVideoSizeChanged(string sizeStr)
    {
        // sizeStr 格式为 "Width|Height"
        string[] parts = sizeStr.Split('|');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
        {
            OnVideoSizeChangedEvent?.Invoke(w, h);
        }
    }

    public void OnStateChanged(string state)
    {
        OnStateChangedEvent?.Invoke(state);
    }

    public void OnTimeChanged(string timeMsStr)
    {
        if (long.TryParse(timeMsStr, out long timeMs))
            OnTimeChangedEvent?.Invoke(timeMs);
    }

    public void OnPositionChanged(string positionStr)
    {
        if (float.TryParse(positionStr, out float position))
            OnPositionChangedEvent?.Invoke(position);
    }

    public void OnLengthChanged(string lengthStr)
    {
        if (long.TryParse(lengthStr, out long length))
            OnLengthChangedEvent?.Invoke(length);
    }

    public void OnMediaChanged(string data)
    {
        // data 格式为 "Title|Length"
        string[] parts = data.Split('|');
        if (parts.Length == 2 && long.TryParse(parts[1], out long length))
        {
            OnMediaChangedEvent?.Invoke(parts[0], length);
        }
    }
}
```

通过这种方式，AAR 侧的任何播放状态变化都会以极低的延迟推送到 Unity 的 C# 事件总线中。Unity 的 UI 组件（如控制面板的进度条滑块）只需订阅 `PlaybackServiceBridgeReceiver` 里的相关 Action，即可实现 UI 与底层播放状态的完美同步。

---

## 3. 生命周期与时序要求

这是新架构中最核心的难点。如果 Android 试图向一个已经被销毁的 PICO Surface 写入数据，会导致底层崩溃。

**播放时序（握手与等待机制）：**
在跨端架构中，AAR 和 Unity 的工作是异步的。当用户在 AAR 界面点击视频时，AAR **绝不能立刻开始播放**，因为此时 Unity 尚未唤醒，PICO 的 Surface 也还未生成。必须建立严格的“握手回调”机制：

1. **AAR 挂起播放请求**：
   - 用户在 AAR UI 界面点击视频。
   - AAR 将 URL 通过 `UnitySendMessage` 发给 Unity，并**挂起当前的播放动作**（不调用 `PlaybackService.loadLocation`）。
   - AAR 将自身的 Activity 切到后台，唤醒 Unity 进程（或切换到 Unity Activity）。

2. **Unity 准备初始环境**：
   - Unity 收到播放请求，实例化挂有 `PicoVideoScreen` 的视频容器。
   - **此时 Unity 还不知道真实的视频分辨率**，因此先不请求 PICO 的 Surface，或者请求一个默认的 1080p 占位 Surface。
   - Unity 调用 `AarPlaybackBridge.LoadLocation(url)` 让 AAR 开始加载视频。

3. **AAR 解析出真实分辨率并回调**：
   - AAR 底层连接网络并读取视频头信息。
   - 一旦解析成功，LibVLC 触发 `onNewVideoLayout(width, height)`。
   - AAR 通过 `UnitySendMessage` 触发 Unity 侧的 `OnVideoSizeChanged`。

4. **Unity 动态重建 PICO 画布并反向绑定 (握手完成)**：
   - Unity 的 `PlayerController` 收到真实的分辨率 (如 3840x2160)。
   - Unity 调用 `PicoVideoScreen.RebuildLayer(true, width, height)`，强制 PICO 底层按此真实分辨率分配最完美的点对点 Surface。
   - Unity 拿到这个新 Surface 的句柄 (`IntPtr`)。
   - Unity 调用 `AarPlaybackBridge.SetSurface(IntPtr)` 将其绑定给 AAR。
   - AAR 底层无缝将解码后的视频帧推向这块量身定制的新画布。

**停止/切换时序（画布清理与防崩溃机制）：**
无论是用户主动退出播放，还是视频自然播放结束，或者是切换到下一个视频，都必须保证 AAR 停止对旧 Surface 的占用，然后 Unity 才能安全地清理 PICO 画布。

1. **收到结束信号/主动停止**：
   - Unity 侧的 `PlaybackServiceBridgeReceiver` 收到 `Ended` 状态，或者用户主动调用 `PlayerController.StopVideo()`。
2. **强制解绑 Surface（关键第一步）**：
   - Unity 立刻调用 `AarPlaybackBridge.DetachSurface()`。
   - 这会触发 AAR 侧的 `vout.detachViews()`，底层 MediaPlayer 会立即停止往当前 Surface 推送任何新的视频帧，并释放对该 Surface 对象的强引用。
3. **停止底层引擎（可选/取决于业务）**：
   - 如果是退出播放，Unity 接着调用 `AarPlaybackBridge.Stop()` 让 AAR 停止解码器并重置状态。
   - 如果是切换视频，则在 Detach 后直接走重新准备 Surface 并 `LoadLocation` 的流程。
4. **清理 PICO 画布（安全落地）**：
   - 在确认 AAR 已经解绑之后（由于 JNI 调用是同步的，DetachSurface 执行完即代表解绑完成），Unity 侧的 `PlayerController` 就可以安全地调用 `PicoVideoScreen` 里的清理方法。
   - 具体做法是：禁用并销毁 PICO 合成层：
     ```csharp
     // PicoVideoScreen.cs 中的销毁逻辑
      _compLayer.enabled = false;
      _compLayer.DestroyLayer(); 
      _hardwareSurfaceHandle = IntPtr.Zero;
      ```
    - 当 `DestroyLayer()` 被调用时，PICO 底层会回收 Android Surface。由于此时 AAR 已经没有人在往里面渲染了，所以不会引发内存越界或崩溃，画布被干净利落地清理掉，视频画面瞬间消失。

---

## 4. 极致平滑：预加载与画布就绪等待机制 (Zero-Drop-Frame)

在 XR 环境下，PICO 动态分配点对点硬件 Surface 需要耗费一定的时间。如果 LibVLC 在画布尚未就绪时就开始播放，会导致开头丢失若干视频帧，甚至出现音频先行而画面滞后的体验瑕疵。

为了实现原生的极致平滑播放体验，我们需要在 AAR 侧引入**“预加载并等待画布”**机制。核心思想是：利用 LibVLC 的媒体解析能力，先让其在“无头（Headless）”模式下提取视频头信息，然后强行挂起播放流程，直到 Unity 反向注入配置好的 Surface 后才真正启动时钟渲染。

### 4.1 方案原理：分离 Load 与 Play

默认情况下，调用 `PlaybackService.loadLocation()` 会触发 `PlaylistManager` 的连续动作：加载 -> 解析 -> 立即播放。
为了阻断这一自动流程，我们需要在 `PlaybackServiceBridge` 中利用 LibVLC 的底层配置项进行干预。

**步骤 1：静默预加载 (Pre-parse)**
当 Unity 准备播放视频时，只告诉 AAR 去**解析**这个视频流，而不是立刻播放。

```kotlin
// PlaybackServiceBridge.kt
@JvmStatic
fun preloadLocation(url: String) {
    val service = playbackService ?: return
    // 构建媒体对象
    val media = Media(service.libVLC, Uri.parse(url))
    
    // 关键配置：阻止媒体被自动播放，仅作为解析用途
    media.addOption(":start-paused") 
    // 或者直接使用异步解析 API，不触发播放管线
    media.parseAsync(Media.Parse.FetchNetwork, 5000)
    
    // 监听解析完成事件
    media.setEventListener { event ->
        if (event.type == Media.Event.ParsedChanged && event.parsedStatus == Media.ParsedStatus.Done) {
            // 获取解析到的真实尺寸
            val track = media.getTrack(Media.Track.Type.Video)
            if (track != null && track is IMedia.VideoTrack) {
                sendToUnity("OnVideoSizeChanged", "${track.width}|${track.height}")
            }
        }
    }
}
```
*注：对于 SMB 等网络流，`parseAsync` 有时无法提取完整的 Track 信息，此时更可靠的做法是使用 `:start-paused` 配置选项将其装载到 MediaPlayer 中，让解码器刚刚启动解出第一帧后立刻处于暂停状态。*

**步骤 2：暂停态启动与尺寸截获**
采用 `:start-paused` 选项将其传入 MediaPlayer。
1. MediaPlayer 启动，网络缓冲填满。
2. 视频解码器解出第一帧，此时由于是 paused 状态，时钟停止，不会丢帧。
3. 触发 `onNewVideoLayout`，将尺寸发给 Unity。

**步骤 3：Unity 重建画布并释放播放锁定**
1. Unity 收到真实尺寸。
2. Unity 销毁旧图层，申请全新尺寸的 PICO `PXR_CompositionLayer`。
3. Unity 将拿到新句柄调用 `AarPlaybackBridge.SetSurface(IntPtr)`。
4. Unity 调用 `AarPlaybackBridge.Play()`。
5. AAR 解除暂停状态，画面第一帧完美、瞬间呈现在精准的 PICO 画布上。

### 4.2 改进后的握手时序图

1. **Unity** -> `AarPlaybackBridge.Preload(url)` -> **AAR**
2. **AAR** (底层连接 SMB，提取流信息，解出第一帧，保持挂起) -> `onNewVideoLayout`
3. **AAR** -> `UnitySendMessage("OnVideoSizeChanged", "3840|2160")` -> **Unity**
4. **Unity** (重建 PICO 硬件层，耗时 N 毫秒)
5. **Unity** -> `AarPlaybackBridge.SetSurface(ptr)` -> **AAR**
6. **Unity** -> `AarPlaybackBridge.Play()` -> **AAR** (恢复时钟，音画同步开始)

---

## 5. 可行性评估与结论

**可行性：极高。**
1. AAR 侧的 `PlaybackService` 已经封装了完整的播放逻辑（`loadLocation`, `play`, `stop`），只需要暴露一个 Bridge 即可，无需修改核心逻辑。
2. Unity 侧的 `PicoVideoScreen` 已经跑通了获取 `IntPtr` (Surface) 的链路，直接通过 JNI 传递没有任何技术障碍。
3. `UnitySendMessage` 的单向通信机制完全足以应对播放状态（如时间、暂停状态）的回调需求。

**收益：**
- **零拷贝硬件渲染**：视频帧在底层直接输出到 PICO 的合成层，不再经过内存中转，极大降低发热和 CPU 占用。
- **无缝衔接原生逻辑**：完美解决 SMB 密码验证、网络状态恢复、断点续播等复杂逻辑，这些功能直接由 `PlaybackService` 免费提供。
- **瘦身 Unity 工程**：Unity 彻底摆脱 `LibVLCSharp`，大大减小包体，逻辑更加清晰纯粹。