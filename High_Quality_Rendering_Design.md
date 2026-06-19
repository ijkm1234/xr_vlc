# PICO VR 高画质视频渲染适配方案

## 1. 核心目标与挑战
在 VR 环境下渲染视频，最大的挑战是如何保证 **无变形 (No Stretch)**、**无裁切 (No Crop)** 且 **无画质损耗 (Pixel-to-Pixel)**。
在使用 PICO 的底层硬件合成层 (`PXR_CompositionLayer` 的 `ExternalSurface` 模式) 时，视频由 Android 底层 `MediaCodec` 直接写入 Surface。
如果传入的 Surface 分辨率与视频真实分辨率不一致，或者屏幕的物理比例与视频比例不一致，就会产生严重的拉伸变形和二次采样带来的画质雪崩。

## 2. 终极解决方案：动态分辨率 + 双层物理嵌套
为了达到最高画质（绕过 Unity 渲染管线，实现 0 拷贝与硬件各向异性过滤），我们采用以下联合架构：

### 2.1 动态重建硬件 Surface 分辨率 (解决二次采样画质损耗)
1. 播放视频时，先不绑定最终画布，让 LibVLC 解析出视频的 **真实原始物理分辨率**（例如 3840x2160）。
2. 将此真实分辨率传入 PICO SDK。通过隐式设置 `layerTextures[0]` 为一个指定宽高的 RenderTexture，强制 PICO 底层 C++ SwapChain **按视频的原生分辨率分配硬件 Surface**。
3. 这样 `MediaCodec` 硬解出的 4K 像素，一滴不漏地原样写入了 4K 的硬件 Surface，没有任何中间损耗。

### 2.2 物理双层嵌套法 (解决画面变形与黑边问题)
1. **背景板 (Background Screen)**：
   * 尺寸固定的 Quad（如 Scale = `2:1`），带有纯黑色无光照材质。
   * 永远不改变其大小，作为“屏幕容器”。
2. **视频板 (Video Screen)**：
   * 作为背景板的子物体，挂载 `PXR_CompositionLayer`。
   * **Fit-to-Contain 算法**：
     * 设屏幕比例 `ScreenRatio = 2.0`，视频比例 `VideoRatio = Width / Height`。
     * 若 `VideoRatio > ScreenRatio`（视频更扁）：`Scale = (1.0, ScreenRatio / VideoRatio, 1.0)`
     * 若 `VideoRatio <= ScreenRatio`（视频更方正）：`Scale = (VideoRatio / ScreenRatio, 1.0, 1.0)`
   * 这样 PICO 将等比的 Surface 贴到等比的视频板上时，画面不会发生任何物理形变，且完美利用父节点的黑色背景形成上下或左右的纯黑边框。

## 3. 执行时序
1. 用户点击播放，调用 `PlayerController.PlayVideo()`。
2. 启动 `MediaPlayer` 开始解析视频（此时不向 PICO 绑定有效画面）。
3. 等待 `MediaParsed` 或 `Playing` 事件触发，通过 `GetVideoTracks()` 获取原生宽高。
4. 调用 `PicoVideoScreen.RebuildLayer(true, width, height)` 重建精确分辨率的 Surface。
5. 调用 `PicoVideoScreen.FitVideoSize(width, height)` 调整局部物理缩放。
6. 获取到新生成的 Surface 句柄后，调用 `libvlc_media_player_set_android_context` 绑定给底层的 `AWindow`，画面正式呈现。