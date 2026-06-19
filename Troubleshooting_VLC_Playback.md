## 12. 最终落地方案：清空画布 + 延迟反向加载
结合前文的评估，我们确定了以下最终实施方案。该方案旨在通过**比较并清空旧画布**触发原生 `else` 分支去通知 Unity 准备画布，并在**确保 `SetSurface` 真正生效后**反向执行新的 `load`，从而实现每次切视频都能安全重建画布并顺利播放。

### 步骤一：在 `PlaylistManager.load` 时执行比较和清空画布
**目标**：当用户请求播放一个新视频时，如果发现和当前播放的不是同一个，强制清空底层的 Surface。这样会使得 `player.hasRenderer` 变为 `false`，从而走入原生设计的 `else` 分支（发送 `sendVideoToUnity` 通知）。

**代码修改示例**：
打开 `vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt`，在 `load` 方法的开头或中间（确定了 `newMedia` 之后，且在调用 `playIndex` 之前）加入比较逻辑：

```kotlin
@MainThread
suspend fun load(list: List<MediaWrapper>, position: Int, mlUpdate: Boolean = false, avoidErasingStop:Boolean = false) {
    // ... 
    if (list.isNotEmpty() && position in list.indices) {
        val newMedia = list[position]
        val currentMedia = getCurrentMedia()
        
        // 【核心修改】比较当前视频和新视频的 URI，如果不同，主动解绑画布
        if (currentMedia != null && currentMedia.uri != newMedia.uri) {
            Log.d(TAG, "Video changed from ${currentMedia.uri} to ${newMedia.uri}. Detaching old surface.")
            player.vlcVout.detachViews() // 这会导致后续的 player.hasRenderer 变为 false
        }
    }
    // ... 原有逻辑继续执行 ...
    // playIndex(currentIndex) 将会因此走入 else 分支，通知 Unity
}
```

### 步骤二：利用原生回调机制反向加载
**难点**：Unity 调用了 `SetSurface` 后，底层的 `vlcVout.setVideoSurface()` 和 `vout.attachViews()` 可能会有微小的时序差异。我们必须确保 `hasRenderer` 真的变成了 `true`，再执行 `service.load(media)`。

**解决方案：基于事件回调替代轮询**
通过查阅 VLC 源码，`PlaybackServiceBridge` 已经实现了 `IVLCVout.Callback` 接口，其中包含 `onSurfacesCreated` 方法。我们可以完美利用这个原生回调，当底层真正创建好画布时，自动触发新的 `load`，彻底摒弃丑陋的 `while` 轮询。

**1. AAR 侧：在 `onSurfacesCreated` 中触发反向调用**
打开 `vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt`：

首先，增加一个标志位，用于表示当前是否正在等待画布重建：
```kotlin
@Volatile
private var isWaitingForSurfaceToLoadMedia = false

@JvmStatic
fun notifySurfaceReadyForLoad() {
    Log.d(TAG, "Unity has passed the Surface. Waiting for onSurfacesCreated callback to load media.")
    isWaitingForSurfaceToLoadMedia = true
}
```

然后，在已有的 `onSurfacesCreated` 回调中执行加载逻辑：
```kotlin
// --- IVLCVout.Callback ---
override fun onSurfacesCreated(vout: IVLCVout?) {
    Log.d(TAG, "onSurfacesCreated")
    
    // 如果是因为切视频/初次播放正在等待画布，则在这里触发加载
    if (isWaitingForSurfaceToLoadMedia) {
        Log.d(TAG, "Surface successfully created. Proceeding to load pending media.")
        isWaitingForSurfaceToLoadMedia = false // 消费掉该标志位
        
        CoroutineScope(Dispatchers.Main).launch {
            pendingDTO?.let { dto ->
                // 组装 MediaWrapper
                val media = MediaWrapper(Uri.parse(dto.uri)).apply {
                    title = dto.title
                    time = dto.time
                    // ... 恢复 slaves 等 ...
                }
                
                // 正式反向调用 service.load
                playbackService?.load(media)
            }
        }
    }
}
```

**2. Unity 侧：在 `SetSurface` 后设置等待标志**
修改 Unity 项目中原来仅仅调用 `PlaybackAarBridge.Play()` 的地方：

```csharp
// PlaybackService.cs
private IEnumerator WaitForSurfaceAndBind()
{
    // ... 
    if (surfacePtr != IntPtr.Zero)
    {
        // 1. 解绑旧画布
        PlaybackAarBridge.DetachSurface();

        // 2. 告诉 AAR：“我马上要把画布给你了，你准备好后就自动加载”
        // （这对应刚才新增的 notifySurfaceReadyForLoad 接口）
        PlaybackAarBridge.NotifySurfaceReadyForLoad();

        // 3. 绑定新画布
        Debug.Log($"[PlaybackService] Calling PlaybackAarBridge.SetSurface...");
        PlaybackAarBridge.SetSurface(surfacePtr);
        
        // 此时不再需要主动调用 Play() 或轮询
    }
    // ...
}
```

### 步骤三：清理 `if` 分支中冗余的 Unity 通知
在目前的 `PlaylistManager.kt` 的 `playIndex` 逻辑中，当代码成功走入 `if` 分支（即 `hasRenderer` 为 `true`，成功调用 `player.startPlayback` 后），末尾还残留着一段通知 Unity 的代码：

```kotlin
// /vlc-android/application/vlc-android/src/org/videolan/vlc/media/PlaylistManager.kt#L591

if (mw.type == MediaWrapper.TYPE_VIDEO || mw.type == MediaWrapper.TYPE_STREAM) {
    mw.addFlags(MediaWrapper.MEDIA_VIDEO)
    // 冗余调用！因为如果走到这里，说明 Unity 已经准备好画布了
    MediaUtils.sendVideoToUnity(ctx, mw) 
}
```

**问题分析**：
在我们的新架构下，如果代码走到了 `if` 分支，意味着要么是纯音频播放，要么是**由 Unity 准备好画布后触发的第二次反向加载**。此时 Unity 端的 UI 和 Surface 早已就绪，如果在这里再次调用 `MediaUtils.sendVideoToUnity`，会导致 Unity 端再次触发 `OnVideoSelected` 回调，从而引发第二次不必要的画布销毁与重建，导致死循环或画面闪烁。

**解决方案**：
完全删除 `if` 分支末尾这段针对视频的 `MediaUtils.sendVideoToUnity(ctx, mw)` 调用。确保 `sendVideoToUnity` 仅仅作为**“无画布时请求外部环境建立画布”的信号**，只保留在 `else` 分支中。

修改后的 `if` 分支末尾应该非常干净：
```kotlin
// ... 
player.startPlayback(media, mediaplayerEventListener, start)
player.setSlaves(media, mw)
if (browserAudioActive) player.setVolume(0)
newMedia = true
determinePrevAndNextIndices()
service.onNewPlayback()

// 删除冗余的 sendVideoToUnity，只保留纯粹的标志位添加（如有必要）
if (mw.type == MediaWrapper.TYPE_VIDEO || mw.type == MediaWrapper.TYPE_STREAM) {
    mw.addFlags(MediaWrapper.MEDIA_VIDEO)
}
```

### 总结
1. **触发切换** -> `PlaylistManager.load` 发现视频变了 -> 强制 `detachViews()`。
2. **状态流转** -> 触发原生 `else` 拦截 -> 发送 `sendVideoToUnity`。
3. **Unity 响应** -> 销毁旧图层 -> 重建新画布 -> 告诉 AAR 准备接收 -> 塞给 AAR (`SetSurface`)。
4. **原生回调** -> VLC 底层完成画布挂载 -> 触发 `onSurfacesCreated` 事件。
5. **事件驱动加载** -> Bridge 拦截到 `onSurfacesCreated` 事件 -> 提取 `pendingDTO` -> 反向调 `service.load(media)`。
6. **完美播放** -> 第二次进入 `PlaylistManager.load` -> 顺利进入 `if` 分支 -> 完美解码出画（且不再发送冗余的 Unity 通知）。

这种完全由事件驱动的闭环设计，不仅时序最严谨，也彻底清除了冗余的跨端通信。