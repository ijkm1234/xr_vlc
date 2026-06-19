# XR VLC 播放器核心控制器 (PlayerController) 设计文档

## 1. 架构定位
`PlayerController` 是整个 XR VLC 播放器的底层“引擎执行者”。它负责与 `LibVLCSharp` 进行直接的 API 交互，管理底层视频/音频的解码、渲染链路（包含软硬解切换），并向外（如 `PlaybackService` 或直接向 UI 层）暴露统一、高层次的播放控制接口和状态回调。

**设计原则：彻底解耦**
*   **UI 无关**：`PlayerController` 内部绝对不包含任何 UI 元素（如 `Button`, `Slider`, `Text`）的引用。
*   **业务逻辑解耦**：不负责维护播放列表、循环模式等业务逻辑（这些由 `PlaybackService` 负责）。
*   **纯粹的指令接收与状态广播**：只负责执行“播放、暂停、跳转”等具体指令，并通过 C# `event` 将自身的内部状态（进度、音轨列表等）广播出去。

---

## 2. 核心职责模块

### 2.1 基础播放控制 (Playback Control)
*   **加载与播放 (Play)**：接收视频 URI（本地/SMB/HTTP）并初始化底层 `Media` 对象，启动播放管线。
*   **暂停/恢复 (Pause/Resume)**：切换当前播放状态。
*   **停止 (Stop)**：停止播放并清理当前视频占用的显存/内存资源，但不销毁 VLC 实例。

### 2.2 进度与时间控制 (Time & Seeking)
*   **绝对时间跳转 (SeekToTime)**：跳转到视频指定的绝对时间（毫秒）。
*   **绝对位置跳转 (SeekToPosition)**：按视频总长度的百分比跳转（0.0 ~ 1.0）。
*   **相对跳转 (Skip)**：在当前时间基础上快进或快退。*注：LibVLC 默认采用关键帧跳转（Fast Seek）以保证速度。如果需要精确到帧（Exact Seek），需要在底层做特殊参数处理或接受一定的解码延迟。*
*   **倍速控制 (SetRate)**：动态调整视频和音频的播放速度（如 0.5x, 1.0x, 1.5x, 2.0x）。

### 2.3 媒体轨道与章节管理 (Media Tracks & Chapters)
*   **获取音轨列表 (GetAudioTracks)**：提取当前视频包含的所有音频轨道信息。
*   **切换音轨 (SetAudioTrack)**：根据用户选择的音轨 ID，无缝切换播放的音频通道。
*   **获取视频轨列表 (GetVideoTracks)**：提取视频内包含的所有视频轨道（用于多视角/多镜头视频）。
*   **切换视频轨 (SetVideoTrack)**：切换当前解码并渲染的视频流。
*   **获取字幕轨列表 (GetSpuTracks)**：提取内嵌的字幕轨道 (Subtitles/SPU)。
*   **切换字幕轨 (SetSpuTrack)**：切换或关闭内嵌字幕。
*   **章节管理 (Chapters)**：解析并获取蓝光/MKV视频内嵌的章节列表，并提供 `SetChapter(int index)` 实现段落跳转。

### 2.4 画面比例与裁剪 (Aspect Ratio & Crop)
*   **设计分析**：在传统的 2D 播放器中，画面比例（16:9, 4:3）通常由播放器内核处理。但在 **VR 环境**中，视频画面是贴在一个 3D 几何体（如 Quad 或 Cylinder）上的。
    *   **画面比例 (Aspect Ratio)**：建议 **不放在** `PlayerController` 处理（保持底层 1:1 原始输出），而是交由 UI 层/合成层通过修改 3D GameObject 的 `Transform.Scale` 来实现物理形变。这在 VR 中更符合空间直觉。
    *   **画面裁剪 (Crop)**：**必须放在** `PlayerController` 里处理。当视频带有上下黑边（Letterbox）时，通过底层 VLC 剥离黑边可以极大节省 GPU 渲染像素和显存带宽。
*   **接口提供**：提供 `SetCrop(string cropGeometry)` 等底层接口。

### 2.6 VR 全景与 3D 视频支持 (VR 360/180 & 3D)
*   **架构边界**：VLC 内核（LibVLC）本身具有 360 视频视角的渲染能力，但那是基于它自己的 OpenGL 窗口（也就是 `Viewpoint` 接口）。在我们的 Unity XR 架构中，**PlayerController 绝对不能使用 VLC 的 Viewpoint 去改变视角**。
*   **职责划分**：
    *   **PlayerController (底层)**：它的职责只是**把一整张**包含全景或 3D 画面的像素（比如一张 8K 的等距柱状投影图，或一张左右格式的 4K 图）完整无损地通过硬件解码交出去。
    *   **渲染层 (PicoVideoScreen)**：负责接收业务指令，改变 `PXR_CompositionLayer` 的属性。例如，将其 `Shape` 从 `Quad` 改为 `Cylinder` 或 `Equirect` (全景球)。
    *   **3D 分离 (Stereo Split)**：对于 3D 视频（如左右半宽或上下格式），PICO 合成层原生支持通过设置 `3D Surface Type`（对应枚举 `PXR_CompositionLayer.Surface3DType`），由硬件自动将同一张纹理裁剪为两半，分别输送给左眼和右眼的屏幕。这是零延迟且完全硬件加速的 3D 实现方式。
*   **接口设计**：在 `IVideoRenderTarget` 中增加更改投影几何体的接口，`PlayerController` 作为传递者，向业务层暴露 `SetProjection(ProjectionType, StereoMode)`。
*   **痛点分析**：在之前的调试中发现，由于 PICO 等 XR 底层合成层在初始化时就“锁死”了渲染模式（是接收普通 Texture 还是硬件 Surface），如果在运行时（视频播放中）动态切换合成层的标志位，会导致底层 C++ 崩溃或回调事件丢失。
*   **设计原则：重置重建 (Teardown & Rebuild)**
    为了保证绝对的稳定性，`PlayerController` **不支持在播放过程中无缝热切换** 软硬解。
    软硬解的切换流程必须遵循以下极其严格的生命周期：
    1.  **Stop()**：彻底停止当前视频播放，销毁当前的 VLC `Media` 对象。
    2.  **Teardown UI/Layer**：通知 `IVideoRenderTarget` 销毁并重建底层的渲染层（例如 PICO 需要彻底 Disable GameObject，修改标志位，再 Enable，以确保 C++ 层的 `SwapChain` 是全新干净的）。
    3.  **Re-Initialize**：重新调用 `PlayerController.Initialize`，传入新的配置状态。
    4.  **PlayVideo()**：重新加载视频，此时 VLC 会根据新的状态决定是否加入 `mediacodec` 参数，并重新抓取 Surface。

---

## 3. 接口设计 (API Definition)

### 3.1 公开方法 (Public Methods)

```csharp
// --- 基础控制 ---
public void Initialize(IVideoRenderTarget renderTarget, bool useHardwareDecoding);
public void PlayVideo(string uri);
public void Pause();
public void Resume();
public void Stop();

// --- 渲染目标抽象 ---
public enum VideoProjection { Flat, Cylinder, Sphere360 }
public enum StereoMode { Mono, LeftRight, TopBottom }

// 用于彻底解耦 PlayerController 与 PICO/Unity 渲染组件
public interface IVideoRenderTarget
{
    // 用于软硬解切换：要求底层屏幕彻底自我销毁并以新模式重建
    void RebuildLayer(bool asHardwareSurface); 
    
    // 用于 VR 视频：改变底层的几何体形状和 3D 模式
    void SetGeometry(VideoProjection projection, StereoMode stereo);

    bool IsHardwareSurfaceReady();
    IntPtr GetHardwareSurfaceHandle(); // 用于硬解，返回 Android Surface JNI 指针
    void SetSoftwareTexture(RenderTexture texture); // 用于软解，接收渲染好的纹理
}

// --- 进度控制 ---
public void SeekToTime(long timeMs);
public void SeekToPosition(float position); // 0.0 ~ 1.0 之间的百分比
public void Skip(long deltaMs, bool fastSeek = true); // 支持选择是否使用关键帧快速跳转
public void SetRate(float rate);

// --- 媒体轨道与章节控制 ---
public struct TrackInfo { public int Id; public string Name; }
public struct ChapterInfo { public int Index; public string Name; public long TimeOffsetMs; }

public List<TrackInfo> GetAudioTracks();
public void SetAudioTrack(int trackId);

public List<TrackInfo> GetVideoTracks();
public void SetVideoTrack(int trackId);

public List<TrackInfo> GetSpuTracks(); // 字幕轨
public void SetSpuTrack(int trackId); // 传入 -1 通常代表关闭字幕

public List<ChapterInfo> GetChapters();
public void SetChapter(int chapterIndex);

// --- 画面裁剪 (Crop) 与 投影方式 ---
public void SetCrop(string cropGeometry); // 例如 "16:9" 或 "100x100+0+0"
public void SetProjection(VideoProjection projection, StereoMode stereo);
```

### 3.2 状态广播事件 (Events)

利用 C# 的 `event Action<T>` 将底层状态异步抛出给主线程的监听者。

```csharp
// 1. 播放状态变更 (Playing, Paused, Stopped, Error)
public event Action<PlayerStatus> OnStatusChanged;

// 2. 视频元数据就绪 (视频总时长，分辨率等信息已解析完毕，UI 可以初始化进度条上限)
public event Action<long /* totalTimeMs */> OnMediaParsed;

// 3. 播放进度高频更新 (用于驱动进度条滑动，建议在底层节流，如每 200ms 触发一次)
public event Action<long /* currentTimeMs */, float /* position 0.0~1.0 */> OnTimeChanged;

// 4. 视频播放结束 (自然播完)
public event Action OnEndReached;

// 5. 错误发生
public event Action<string /* errorMessage */> OnError;
```

---

## 4. 内部工作流与线程调度 (Workflow & Threading)

### 4.1 线程安全屏障 (The Loom/Dispatcher)
LibVLC 的所有回调（如时间更新、状态改变）都是在底层的 **C++ 线程** 中触发的。如果直接在这些回调中触发 Unity 的事件，会导致订阅者（UI）在更新 Unity 组件时抛出 `Not in Main Thread` 崩溃。
**设计**：`PlayerController` 内部必须使用 `Loom` 或 `SynchronizationContext` 将所有向外广播的事件封转并抛回 Unity 主线程执行。

### 4.2 事件节流 (Event Throttling)
VLC 底层的 `TimeChanged` 事件触发频率极高（可能每秒几十次）。如果全部抛给 UI 渲染进度条，会造成严重的性能浪费。
**设计**：在 `PlayerController` 内部实现时间节流器（Throttler），保证 `OnTimeChanged` 事件每秒最多只向外广播 5-10 次。

### 4.3 资源清理 (Resource Disposal)
视频切换或销毁时，必须严格遵循清理顺序：
1. 停止 `MediaPlayer`。
2. 释放 `Media` 对象。
3. 如果是硬解模式，清理 JNI 全局引用 (`DeleteGlobalRef`)。
4. 如果是软解模式，释放非托管内存 (`Marshal.FreeHGlobal`) 和销毁 `Texture2D`。

---

## 5. 与周边系统的协作关系

*   **UI 层 (VRUIManager)**：仅通过订阅 `PlayerController` 的事件来更新界面（例如收到 `OnTimeChanged` 更新 Slider），通过调用 `SeekTo` 或 `SetRate` 发送指令。UI 不关心视频是如何解码的。
*   **业务层 (PlaybackService)**：负责处理“播放列表下一个”、“记录上次播放位置”、“SMB 密码鉴权”等宏观业务，然后将最终的 URI 交给 `PlayerController` 执行。
*   **渲染层抽象 (IRenderTarget)**：`PlayerController` 不直接依赖 `PXR_CompositionLayer` 或具体的 Unity 材质。它应该通过一个接口或委托，向外部请求或投递渲染目标。
    *   **硬解**：向外部请求一个 `IntPtr` 类型的 Surface 句柄。
    *   **软解**：向外部投递一个准备好的 `RenderTexture`。