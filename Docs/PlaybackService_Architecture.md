# VLC PlaybackService 功能补齐与对齐方案

本文档基于标准 VLC 的 `PlaybackService` 架构设计，结合 XR 项目的需求，梳理并给出了完整的 `PlaybackService` 演进方案，包括相关的实体类定义、状态枚举、事件委托以及完整的核心服务方法定义。

## 1. 核心实体与枚举定义 (Core Entities & Enums)

为了取代原来松散的 `List<string>` 管理方式并引入更多控制维度，我们需要先定义数据模型。

```csharp
namespace XRVLC.Media
{
    /// <summary>
    /// 播放器的状态枚举（对齐 VLC 的 MediaPlayer.Event）
    /// </summary>
    public enum PlayerStatus
    {
        Idle,
        Opening,
        Buffering,
        Playing,
        Paused,
        Stopped,
        Ended,
        Error
    }

    /// <summary>
    /// 播放列表循环模式
    /// </summary>
    public enum RepeatMode
    {
        None,       // 不循环，播完即止
        All,        // 列表循环
        Single      // 单曲循环
    }

    /// <summary>
    /// 媒体视频的格式类型，特别是 XR 特有的格式
    /// </summary>
    public enum MediaProjectionType
    {
        Flat2D,     // 普通 2D 平面
        Sphere180,  // 180度全景
        Sphere360   // 360度全景
    }

    /// <summary>
    /// 封装单条媒体信息（对齐 VLC 的 MediaWrapper）
    /// </summary>
    public class MediaWrapper
    {
        // --- 基础标识与资源 ---
        public string Id { get; set; }           // 唯一标识
        public string Uri { get; set; }          // 本地路径或网络 URL
        
        // --- 媒体元数据 ---
        public string Title { get; set; }        // 显示标题
        public string Artist { get; set; }       // 艺术家/作者
        public string ArtworkUrl { get; set; }   // 封面/缩略图路径
        public long DurationMs { get; set; }     // 视频总时长（毫秒）

        // --- 历史播放状态 ---
        public long Time { get; set; }           // 上次播放位置（进度），用于续播
        public bool IsSeen { get; set; }         // 是否已播放完毕

        // --- XR 扩展 ---
        public MediaProjectionType Projection { get; set; } // 视频投影类型
        
        // --- 轨道偏好记录 (可选，记录用户上次选择的轨道) ---
        public int AudioTrack { get; set; } = -1;
        public int SpuTrack { get; set; } = -1;  // Subtitle track
        
        public object ExtraData { get; set; }    // 额外业务数据扩展
    }

    /// <summary>
    /// 轨道信息（音轨、字幕轨等）
    /// </summary>
    public class TrackInfo
    {
        public int Id { get; set; }              // 轨道ID，透传给 libVLC
        public string Name { get; set; }         // 轨道名称，如 "English", "Chinese"
    }
}
```

## 2. 事件系统定义 (Events & Callbacks)

将播放状态拆分成更细粒度的事件，方便不同的 UI 组件（如控制面板、进度条、播放列表UI）解耦监听。

```csharp
namespace XRVLC.Media
{
    public interface IPlaybackEvents
    {
        // --- 核心状态变更 ---
        event Action<PlayerStatus> OnStatusChanged;
        event Action<string> OnError;

        // --- 播放列表与当前曲目 ---
        event Action OnPlaylistUpdated;                 // 列表被修改时触发
        event Action<MediaWrapper, int> OnMediaChanged; // 切换歌曲时触发（带出当前项和索引）
        event Action<RepeatMode, bool> OnPlaybackModeChanged; // 循环/随机模式改变时触发

        // --- 进度与时间 ---
        event Action<long, long> OnTimeChanged;         // 时间更新: currentMs, totalMs
        event Action<float> OnBuffering;                // 缓冲进度: 0.0 ~ 100.0

        // --- 媒体元数据与轨道 ---
        event Action<List<TrackInfo>> OnAudioTracksChanged;
        event Action<List<TrackInfo>> OnSubtitleTracksChanged;
    }
}
```

## 3. PlaylistManager 与 PlaybackService 解耦重构 (Methods & API)

根据 VLC 官方架构，我们将原有的单一庞大服务拆分为：`PlaylistManager` (负责纯逻辑的列表和切歌流转) 与 `PlaybackService` (负责系统生命周期、XR 视图绑定与底层透传的门面)。

### 3.1 核心列表管理器：PlaylistManager
它是完全纯净的 C# 类，不继承 `MonoBehaviour`，方便单元测试与状态维护。

```csharp
using System;
using System.Collections.Generic;

namespace XRVLC.Media
{
    public class PlaylistManager
    {
        // --- 数据结构 ---
        public IReadOnlyList<MediaWrapper> OriginalList { get; private set; } // 原始列表
        public IReadOnlyList<MediaWrapper> PlayingList { get; private set; }  // 打乱或映射后的实际播放列表
        
        public int CurrentIndex { get; private set; }
        public MediaWrapper CurrentMedia { get; private set; }

        public RepeatMode RepeatMode { get; private set; }
        public bool IsShuffleEnabled { get; private set; }

        // --- 列表编辑 ---
        public void Load(List<MediaWrapper> items, int startIndex = 0);
        public void Append(MediaWrapper item);
        public void InsertNext(MediaWrapper item);
        public void RemoveAt(int index);
        public void MoveItem(int fromIndex, int toIndex);
        public void Clear();

        // --- 模式切换 ---
        public void SetRepeatMode(RepeatMode mode);
        public void SetShuffle(bool enable);

        // --- 核心流转算法 (供 PlaybackService 调用) ---
        /// <summary>
        /// 根据当前的循环和随机模式，计算并返回下一首。
        /// 若播放结束且无循环，返回 null。
        /// </summary>
        public MediaWrapper DetermineNext();
        
        /// <summary>
        /// 计算并返回上一首。
        /// </summary>
        public MediaWrapper DeterminePrevious();

        /// <summary>
        /// 直接跳转到指定索引
        /// </summary>
        public MediaWrapper DetermineSkipTo(int index);
        
        // --- 会话持久化 ---
        public void SaveQueue();
        public void RestoreQueue();
    }
}
```

### 3.2 播放门面服务：PlaybackService
`PlaybackService` 将作为统筹 `PlaylistManager`、XR 视图绑定、底层 `PlayerController` 控制的核心门面（Facade）。

```csharp
using System;
using UnityEngine;

namespace XRVLC.Media
{
    public class PlaybackService : MonoBehaviour, IPlaybackEvents
    {
        // --------------------------------------------------------
        // [1] 全局单例与依赖持有
        // --------------------------------------------------------
        public static PlaybackService Instance { get; private set; }
        
        public PlaylistManager Playlist { get; private set; } // 持有列表管理器实例
        public PlayerStatus CurrentStatus { get; private set; }
        public bool UseHardwareDecoding { get; private set; }

        // 事件实现 (IPlaybackEvents) ...

        // --------------------------------------------------------
        // [2] 视图与生命周期绑定 (XR 解耦架构)
        // --------------------------------------------------------
        
        /// <summary>
        /// 动态绑定渲染屏幕（当用户进入播放场景时调用）
        /// </summary>
        public void AttachVideoScreen(PicoVideoScreen screen);

        /// <summary>
        /// 解绑渲染屏幕（当用户离开场景但希望后台保持声音时调用）
        /// </summary>
        public void DetachVideoScreen();

        /// <summary>
        /// 接收 XR 焦点变化（如摘下/戴上头显）
        /// </summary>
        public void OnXRFocusChanged(bool hasFocus);

        // --------------------------------------------------------
        // [3] 基础播放控制 (门面透传)
        // --------------------------------------------------------

        public void Play();
        public void Pause();
        public void TogglePlayPause();
        public void Stop();

        /// <summary>
        /// 跳转到指定位置
        /// </summary>
        public void SeekTo(long timeMs);

        /// <summary>
        /// 触发切歌：内部调用 Playlist.DetermineNext() 获取媒体并交由 PlayerController 播放
        /// </summary>
        public void Next(bool forceUserAction = true);
        public void Previous();
        public void SkipTo(int index);

        // --------------------------------------------------------
        // [4] 硬件与高级属性控制 (透传到底层 PlayerController)
        // --------------------------------------------------------

        /// <summary>
        /// 切换软硬解码，并无缝恢复当前播放进度
        /// </summary>
        public void ToggleDecodingMode();

        public void SetPlaybackRate(float rate);
        public void SetAudioTrack(int trackId);
        public void SetSubtitleTrack(int trackId);
        public void SetAudioDelay(long delayMs);
        
        /// <summary>
        /// 设置字幕延迟补偿（毫秒）
        /// </summary>
        public void SetSubtitleDelay(long delayMs);
    }
}
```

## 4. VLC AAR 数据交互方案：如何读写视频信息与历史状态

在我们的 XR 架构中，由于底层的解码和媒体解析是由 Android 原生的 VLC AAR（LibVLC & Medialibrary）来完成的，因此我们需要一套机制在 Unity C# 与 Android AAR 之间同步 `MediaWrapper` 的状态。

### 4.1 数据交互架构图

```text
[ Unity C# (PlaybackService) ]
        |       ^
 (JNI Call)  (JNI Callback / Event)
        v       |
[ Android Java (VlcPlugin / MediaLibraryWrapper) ]  <--> [ SQLite Database ]
        |
[ LibVLC (AAR) ] --> 视频解析、解码、元数据提取
```

### 4.2 获取媒体元数据 (Metadata)
VLC 的 `Media` 对象在解析（Parse）后会自动提取时长、标题、轨道信息。
1. **Unity 发起解析**：C# 调用 `ParseMedia(uri)`。
2. **AAR 内部处理**：Android 插件使用 `new Media(libvlc, uri)`，并调用 `media.parse(Parse.FetchLocal)`。
3. **数据回传 Unity**：解析完成后，Android 插件通过 `UnitySendMessage` 或 JNI 回调将时长（`getDuration()`）、艺术家等信息回传给 C#，C# 组装/更新 `MediaWrapper`。
4. **获取轨道信息**：在视频 `Playing` 状态后，C# 调用 JNI 获取当前的音轨/字幕轨列表，AAR 通过 `mediaPlayer.getAudioTracks()` 返回 JSON 或字符串数组，C# 解析为 `List<TrackInfo>`。

### 4.3 读写历史播放状态与轨道偏好 (MediaLibrary 集成)
标准的 VLC 播放器不仅仅使用 `LibVLC`，还会集成 `Medialibrary`（VLC 媒体库，底层是 SQLite），用于持久化存储播放历史。

*   **写入播放进度（保存历史）**：
    *   **触发时机**：当播放器暂停（Pause）、停止（Stop）、或者 Unity 应用退到后台时。
    *   **流程**：C# 获取当前的进度 `currentMs = playerController.Time`。
    *   **JNI 调用**：C# 调用 `SaveMediaHistory(string uri, long time, bool isSeen, int audioTrack, int spuTrack)`。
    *   **AAR 处理**：Android 插件如果集成了 `Medialibrary`，会调用 `medialibrary.saveMediaProgress(uri, time)`；如果为了轻量化未集成媒体库，Android 插件可使用 `SharedPreferences` 或 SQLite 自行记录该 `uri` 的进度与轨道偏好。

*   **读取历史记录（断点续播）**：
    *   **触发时机**：在 `LoadAndPlay(MediaWrapper)` 之前。
    *   **流程**：C# 传入 `uri`，调用 JNI `GetMediaHistory(string uri)`。
    *   **AAR 处理**：Android 插件查询数据库或配置表，返回该 `uri` 的 `time`, `isSeen`, `audioTrack`, `spuTrack`。
    *   **Unity 恢复状态**：C# 更新 `MediaWrapper` 的属性。当收到 `OnStatusChanged(Playing)` 事件后，C# 立刻检查 `MediaWrapper.Time`。如果大于 0 且未播放完，调用 `SeekTo(wrapper.Time)` 恢复进度，并调用 `SetAudioTrack(wrapper.AudioTrack)` 恢复轨道。

### 4.4 方案总结
为了实现这一套完整的历史和信息读写：
1. 我们需要在 Android 的 Unity Plugin 层（Java/Kotlin 代码）补充 `MediaLibrary` 的简单封装，或者使用 Android 本地的持久化存储（如 Room/MMKV/SharedPreferences）作为轻量级替代。
2. 在 C# 的 `PlaybackService` 或单独的 `MediaDatabaseService` 中，封装 JNI 的 `Get/Save` 方法，在播放器生命周期的关键节点（Stop/Pause/Play）自动进行数据的持久化与恢复。

---

## 5. 附录：VLC 官方 PlaybackService 方法清单 (供 Review)

为了确保我们的设计没有遗漏，这里罗列了 VLC for Android 官方 `PlaybackService` (及其持有的 `PlaylistManager` 和 `MediaPlayer`) 中的核心方法及其功能。您可以对照此清单 Review 我们的方案是否需要补充。

### 5.1 生命周期与基础控制 (Lifecycle & Basic Controls)
*   `load(mediaWrapper)` / `load(mediaList, position)`: 加载单曲或列表并准备播放。
*   `play()`: 开始或恢复播放。
*   `pause()`: 暂停播放。
*   `stop()`: 停止播放并释放部分资源。
*   `playIndex(index)`: 播放列表中的特定索引。
*   `next()` / `previous()`: 切换到下一首/上一首。
*   `seek(position)`: 跳转到指定时间进度（毫秒）。
*   `getTime()` / `getLength()`: 获取当前播放进度和媒体总时长。

### 5.2 播放列表与模式管理 (Playlist & Mode Management)
*   `getMedias()`: 获取当前播放列表的所有 `MediaWrapper`。
*   `getCurrentMedia()`: 获取当前正在播放的媒体。
*   `getCurrentMediaPosition()`: 获取当前媒体在列表中的索引。
*   `append(mediaWrapper)` / `append(mediaList)`: 将媒体追加到当前列表末尾。
*   `insertNext(mediaWrapper)` / `insertNext(mediaList)`: 插队播放（下一首播放）。
*   `remove(position)`: 从列表中移除指定位置的媒体。
*   `moveItem(from, to)`: 拖拽改变列表中媒体的顺序。
*   `setRepeatType(RepeatType)`: 设置循环模式（None, All, Single）。
*   `setShuffle(boolean)`: 开启/关闭洗牌模式。VLC 的 Shuffle 会重新生成一个打乱的索引列表，保证不重复。

### 5.3 轨道与媒体属性控制 (Tracks & Media Attributes)
*   `getAudioTracks()` / `getSpuTracks()` / `getVideoTracks()`: 获取所有可用的音轨、字幕轨、视频轨列表。
*   `getAudioTrack()` / `getSpuTrack()` / `getVideoTrack()`: 获取当前正在使用的轨道 ID。
*   `setAudioTrack(id)` / `setSpuTrack(id)` / `setVideoTrack(id)`: 切换指定的轨道。
*   `setAudioDelay(delay)` / `setSpuDelay(delay)`: 设置音频/字幕的延迟补偿。
*   `getRate()` / `setRate(rate)`: 获取和设置播放倍速。
*   `setEqualizer(equalizer)`: 设置音频均衡器（EQ）。
*   `setVideoScale(scale)` / `setAspectRatio(aspect)`: 设置视频缩放比例和宽高比（如 16:9, 4:3, Fit screen）。

### 5.4 视图与后台交互 (View & Background Interaction)
*   `setVideoTrackEnabled(boolean)`: 开启/关闭视频轨道（例如退到后台时关闭视频轨省电，只播声音）。
*   `attachView(VLCVideoLayout)` / `detachView()`: 动态绑定和解绑渲染视图。支持画中画和后台播放。
*   `isBackgroundTracking()`: 检查当前是否在后台运行。
*   `onAudioFocusChange(focusChange)`: 响应系统的音频焦点变化（如来电时暂停/降低音量）。

### 5.5 其他高级特性 (Advanced Features)
*   `setSleepTimer(time)`: 设置睡眠定时器（倒计时结束后停止播放）。
*   `updateABRepeat(time)`: A-B 段循环播放控制。
*   `addSubtitleTrack(uri)`: 动态加载外部字幕文件。
*   `getPlaybackState()`: 获取当前详细的播放状态机状态。

---

## 6. 附录：VLC 官方 PlaylistManager 功能梳理 (架构解耦参考)

在 VLC 的实际源码架构中，`PlaybackService` 其实是一个“空壳”（Facade 门面模式），它处理 Android 系统的生命周期（Service、Notification、MediaSession、AudioFocus），而**所有关于播放列表的真正核心逻辑，全部委托给了 `PlaylistManager` 对象。**

如果在我们的 XR 项目中想要做到极致的代码解耦，建议在 C# 中也单独抽离出一个 `PlaylistManager` 类。以下是 VLC 官方 `PlaylistManager` 的核心功能职责：

### 6.1 核心数据结构维护 (Data Structure Maintenance)
`PlaylistManager` 内部通常维护着两个列表，这也是它解决“随机播放”和“列表管理”的核心：
*   `List<MediaWrapper> mediaList`: 原始的播放列表（用户看到的顺序）。
*   `List<MediaWrapper> playingList` (或乱序索引映射): 实际用于播放的列表。
    *   当 `Shuffle`（洗牌模式）关闭时，`playingList` 等于 `mediaList`。
    *   当 `Shuffle` 开启时，`PlaylistManager` 会使用打乱算法（如 Fisher-Yates shuffle）重新生成 `playingList`，但会**保证当前正在播放的歌曲被放置在打乱后列表的 Index 0 位置**，以确保无缝衔接。

### 6.2 复杂的索引流转逻辑 (Index Routing Logic)
它接管了所有的切歌逻辑判断（即 `determineNext()` 和 `determinePrev()`）：
*   **Next() 逻辑**：
    *   如果是 `RepeatMode.Single`（单曲循环），返回当前索引。
    *   如果是 `RepeatMode.All`（列表循环）且到达末尾，返回索引 0。
    *   如果开启了 `Shuffle`，它是在 `playingList`（打乱后的列表）中去进行 `index++`。
*   **自动切歌 (Auto-Advance)**：
    *   监听底层 MediaPlayer 的 `EndReached` 事件。当一首歌播完时，`PlaylistManager` 会根据上述的路由逻辑，自动提取下一个 `MediaWrapper` 并送入 MediaPlayer 播放。
*   **错误跳过机制 (Error Fallback)**：
    *   如果列表中的某一个视频/音频解析失败（Error），`PlaylistManager` 会自动触发 `Next()` 跳过该错误媒体，直到找到可播放的媒体或列表结束。

### 6.3 列表运行时编辑 (Runtime Editing)
当用户在播放过程中修改列表时，`PlaylistManager` 负责保证播放不中断：
*   **InsertNext() / Append()**: 插入新媒体时，它需要同时更新 `mediaList` 和 `playingList`（如果在随机模式下，插入的媒体会被塞到打乱列表的特定位置）。
*   **Remove()**: 如果移除的是当前正在播放的歌曲，它需要负责自动平滑地切到下一首。

### 6.4 播放会话持久化 (Session Persistence)
*   **保存当前队列 (Save Queue)**：在后台或停止时，将当前的 `mediaList`、`CurrentIndex` 以及 `RepeatMode` / `Shuffle` 状态打包序列化，保存到本地（通过 Medialibrary 或 SharedPreferences）。
*   **恢复队列 (Restore Queue)**：下次应用启动时，直接从本地读取上次未播完的整个列表和状态，让用户可以无缝继续上次的观看/收听体验。

### 6.5 结论与我们的改进建议
基于对 `PlaylistManager` 的梳理，在我们的 C# `PlaybackService.cs` 重构时，建议：
1. **职责分离**：将所有的 `List<MediaWrapper>`、`Next()`、`Previous()`、`Shuffle` 等逻辑从 `PlaybackService` 中剥离，创建一个独立的纯 C# 类 `PlaylistManager`。
2. `PlaybackService` 仅负责持有 `PlaylistManager` 实例，以及管理 `PicoVideoScreen`（视图绑定）和底层 `PlayerController` 的初始化交互。

---

## 7. 补充：媒体库 (Medialibrary) 对象的维护与调用方案

在对 `vlc-android` 原生代码与我们现有的 `VlcAarBridge.cs` 梳理后，明确了如何通过 Unity 与 Android 底层的 `Medialibrary` 实例打通，真正实现断点续播与历史记录同步。

### 7.1 原版 vlc-android 对媒体库的依赖方式
在原版 Android 源码中，`PlaylistManager.kt` 是媒体库操作的主阵地：
*   **获取实例**：通过 `Medialibrary.getInstance()` 获取全局单例。
*   **获取媒体信息**：通过 `medialibrary.findMedia(uri)` 或 `medialibrary.getMedia(uri)` 获取媒体在数据库中的实体（包含内部 ID、上次播放进度 `time`、总时长 `length` 等）。
*   **写入播放进度**：在暂停、停止或切歌时，调用 `medialibrary.setLastTime(media.id, time)` 保存进度，或调用 `setLastPosition` 存百分比。
*   **更新历史记录**：当开始播放新媒体时，调用 `medialibrary.addToHistory(uri, title)`，让它出现在 VLC 的“历史记录”列表中。

### 7.2 目前我们 XR 项目的现状
*   **VlcAarBridge.cs 现状**：目前仅仅是通过构建 `Intent` 唤起 VLC 的 2D 界面，然后接收 Android 侧 `VideoGridFragment.kt` 传回的 `UnitySendMessage("VlcAarBridge", "OnVideoSelected", uri|title)`。
*   **缺陷**：收到回调后，Unity 直接从 0 开始播放（因为没有去数据库查进度）；Unity 在 XR 里播放的进度，也没有写回给数据库，导致两边的历史状态割裂。

### 7.3 媒体库的维护与 JNI 调用落地层方案

为了补齐这一环，我们不需要在 Android 侧写大量额外的插件代码，可以直接在 C# 端封装一个 `MediaLibraryBridge`，利用 Unity 的 `AndroidJavaClass` 直接调用 Android 的 `Medialibrary`。

#### 步骤一：封装 C# 层的 MediaLibraryBridge
创建一个专用的桥接类，用来直接调用 Java 层的 `Medialibrary.getInstance()`：
```csharp
public class MediaLibraryBridge
{
    private static AndroidJavaObject GetMedialibraryInstance()
    {
        using (var mlClass = new AndroidJavaClass("org.videolan.medialibrary.interfaces.Medialibrary"))
        {
            return mlClass.CallStatic<AndroidJavaObject>("getInstance");
        }
    }

    /// <summary>
    /// 获取上次播放进度 (毫秒)，若无记录返回 0
    /// </summary>
    public static long GetLastTime(string uri)
    {
        if (Application.platform != RuntimePlatform.Android) return 0;
        try {
            using (var ml = GetMedialibraryInstance())
            using (var media = ml.Call<AndroidJavaObject>("getMedia", uri))
            {
                if (media != null) {
                    return media.Call<long>("getTime"); // 返回上次的播放时间
                }
            }
        } catch (Exception e) { Debug.LogError(e); }
        return 0;
    }

    /// <summary>
    /// 保存当前播放进度
    /// </summary>
    public static void SetLastTime(string uri, long timeMs)
    {
        if (Application.platform != RuntimePlatform.Android) return;
        try {
            using (var ml = GetMedialibraryInstance())
            using (var media = ml.Call<AndroidJavaObject>("getMedia", uri))
            {
                if (media != null) {
                    long mediaId = media.Call<long>("getId");
                    ml.Call<int>("setLastTime", mediaId, timeMs);
                }
            }
        } catch (Exception e) { Debug.LogError(e); }
    }

    /// <summary>
    /// 插入/更新历史记录
    /// </summary>
    public static void AddToHistory(string uri, string title)
    {
        if (Application.platform != RuntimePlatform.Android) return;
        try {
            using (var ml = GetMedialibraryInstance())
            {
                ml.Call<bool>("addToHistory", uri, title);
            }
        } catch (Exception e) { Debug.LogError(e); }
    }
}
```

#### 步骤二：PlaybackService 与 VlcAarBridge 的闭环调用
1. **加载媒体时 (断点续播)**：
   在 `VlcAarBridge.OnVideoSelected` 回调后，调用 `PlaybackService` 前：
   ```csharp
   long lastTime = MediaLibraryBridge.GetLastTime(uri);
   MediaLibraryBridge.AddToHistory(uri, title);
   
   var mediaWrapper = new MediaWrapper { Uri = uri, Title = title, Time = lastTime };
   PlaybackService.Instance.LoadAndPlay(mediaWrapper);
   // PlaybackService 在触发 Play 后，若 Time > 0 则自动调用 SeekTo(lastTime)
   ```
2. **保存进度时 (同步回 Android 库)**：
   在 `PlaybackService` 发生 **Pause()**, **Stop()** 或 Unity 触发 `OnApplicationPause()`/`OnApplicationQuit()` 时：
   ```csharp
   if (CurrentMedia != null && playerController != null) {
       long currentMs = playerController.Time;
       MediaLibraryBridge.SetLastTime(CurrentMedia.Uri, currentMs);
   }
   ```

通过这个 `MediaLibraryBridge` 的直连方案，我们能在**不修改 Android 端底层 AAR 库逻辑**的前提下，完美实现 XR 播放器与 VLC 2D 媒体库之间状态和进度的无缝双向同步。
