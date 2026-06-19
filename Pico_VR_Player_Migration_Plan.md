# Pico VR 播放器开发与 VLC 迁移方案设计

## 1. 背景与目标
在 Pico VR 设备上开发一款体验类似 VLC 的全能播放器。
- **痛点**：VLC 原生 Android 应用在 VR 头显中仅能以 2D 平面显示，无法提供 3D、180°/360° 全景视频的沉浸式体验；且 libVLC 的软解/硬解在 VR 高帧率双眼渲染下存在性能瓶颈。
- **目标**：完美复用 VLC 强大的媒体库扫描、文件分类、网络流协议（特别是带鉴权的 SMB）功能，同时利用移动端 VR 的最佳实践（Unity + 高性能解码器）实现极致的 3D/全景播放体验。
- **约束**：接受“媒体库、网络设置等文件浏览界面”使用普通的 2D Android UI，而播放界面必须是 3D VR 环境。

## 2. 核心架构设计：AAR 混合开发 (Unity + Android Native)
采用 **“双态融合”** 的单应用架构：将精简后的 VLC Android 模块打包为 AAR 插件嵌入 Unity 项目中。

### 2.1 视频播放器插件横向对比与选型 (免费优先)
在 Unity 中实现 VR 视频播放，视频格式与编码兼容性是最核心的指标。以下是常见方案的兼容性对比：

| 特性/格式 | 1. Unity 官方 VideoPlayer (免费) | 2. Pico 官方 VideoPlayer Demo (免费/开源) | 3. AVPro Video (商业收费) |
| :--- | :--- | :--- | :--- |
| **底层解码引擎** | 依赖 Android 系统原生 MediaPlayer | **基于 AndroidX Media3 (ExoPlayer)** | 深度定制的 Android MediaCodec |
| **最高分辨率支持** | 4K @ 30fps (部分机型卡顿) | **4K/8K @ 60/90fps (硬解性能极佳)** | 8K @ 90fps (性能最强) |
| **H.264 / AVC** | ✅ 完美支持 | **✅ 完美支持** | ✅ 完美支持 |
| **H.265 / HEVC** | ⚠️ 部分机型支持，需系统硬解支持 | **✅ 完美支持 (Pico 硬件加速优化)** | ✅ 完美支持 |
| **VP9 / AV1** | ❌ 官方组件不支持 | **✅ 支持 (ExoPlayer 自带软/硬解支持)** | ✅ 支持 |
| **网络流协议** | 仅支持基础 HTTP/HTTPS | **支持 HTTP/HTTPS, DASH, HLS** | 支持全协议及自适应码率流 |
| **360°/180°/3D 格式映射** | 需自己写 Shader 处理 | **✅ Demo 源码已提供全套 360/3D 映射 Shader** | ✅ 内置各种格式的现成组件 |
| **开发难度** | 极低（拖拽组件即可） | **中等（需阅读 Pico 源码并移植）** | 极低（开箱即用） |

**【选型结论】**：既然要求**免费**，强烈推荐使用 **方案 2：Pico 官方提供的 VideoPlayer Demo（基于 ExoPlayer）**。
- **原因**：Unity 官方的 `VideoPlayer` 在移动端 VR 下对 H.265 和高码率 4K/8K 全景视频支持极差，容易出现花屏和掉帧。Pico 的 Demo 源码封装了 ExoPlayer，完美利用了 Pico 骁龙芯片的底层硬件解码能力，并且**开源免费**，自带了 360° 和 3D 左右/上下的材质映射。

### 2.2 模块分工
1. **Android AAR (基于 VLC for Android 魔改)**
   - 负责 2D UI 展示（系统级弹出面板）。
   - 负责设备本地存储扫描（视频、音频）。
   - 负责局域网发现与网络流连接（SMB 1/2/3, FTP, DLNA）。
   - **核心改造**：移除 libVLC 播放器引擎，当用户在列表点击视频时，不启动 VLC 播放器，而是关闭当前 2D 界面，将视频的 URI 回传给 Unity。

2. **Unity (VR 播放核心)**
   - 负责 3D VR 场景渲染（虚拟电影院、全景天空盒）。
   - 负责视频解码与硬件加速。考虑到成本与性能，推荐使用免费/开源的插件方案（**Pico VideoPlayer SDK** 或 **Unity 内置 VideoPlayer**），下文附有免费方案与 AVPro 商业插件的格式兼容性对比。
   - 负责 VR 手柄射线交互、播放进度条、音量控制、视角重置等 VR UI 控件。

## 3. 详细实施步骤

### 阶段一：魔改 VLC 并导出 AAR
1. **克隆源码与环境准备**：拉取 `vlc-android` 官方源码。
2. **剔除播放器核心**：在 `build.gradle` 中移除 `libvlc` 依赖，删除 `VideoPlayerActivity` 相关代码，仅保留 `medialibrary` 和 `vlc-android` 的 UI 浏览模块。
3. **拦截点击事件**：
   在视频列表的 Adapter 中，修改视频被点击时的逻辑：
   ```java
   // 伪代码：在 VLC 的 VideoListAdapter 中
   public void onVideoClick(MediaWrapper media) {
       String videoUri = media.getUri().toString();
       String videoTitle = media.getTitle();
       
       // 将选中的视频数据通过 Intent 返回
       Intent resultIntent = new Intent();
       resultIntent.putExtra("selected_video_uri", videoUri);
       resultIntent.putExtra("selected_video_title", videoTitle);
       
       // 设置返回结果并关闭当前 2D Activity
       Activity currentActivity = (Activity) context;
       currentActivity.setResult(Activity.RESULT_OK, resultIntent);
       currentActivity.finish();
   }
   ```
4. **编译导出**：将修改后的工程编译为 `vlc-media-library.aar`。

### 阶段二：解决 SMB 视频流播放（核心难点）
由于 Unity 中的 AVPro 或 ExoPlayer 可能无法直接读取带复杂鉴权的 SMB 协议流（`smb://...`），需要利用 VLC 底层建立本地代理：
1. **本地 HTTP 代理转发**：在 Android AAR 中，当用户选中 SMB 视频时，在后台启动一个轻量级 HTTP Server（如使用 `AndroidAsync` 或 `NanoHTTPD`）。
2. **流转换**：该 Server 负责与 SMB 服务器建立连接并持续拉取视频流数据。
3. **URI 替换**：向 Unity 返回的不再是 `smb://...`，而是代理地址 `http://127.0.0.1:8080/stream`。Unity 播放器将像播放普通在线 HTTP 视频一样播放 SMB 视频。

### 阶段三：Unity 端的集成与交互
1. **导入插件**：将 `vlc-media-library.aar` 放入 Unity 工程的 `Assets/Plugins/Android` 目录下。
2. **搭建 VR 场景**：
   - 引入 Pico Integration SDK。
   - 创建虚拟影院环境，放置一个 Quad（平面）或 Sphere（球体）用于视频画面映射。
   - 挂载 AVPro Video 的 `MediaPlayer` 和 `ApplyToMesh` 脚本。
3. **C# 唤起 2D 媒体库**：
   编写脚本，在 Unity 中通过一个 3D 按钮触发开启媒体库：
   ```csharp
   public void OpenVLCMediaLibrary()
   {
       AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
       AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
       
       // 构建 Intent 启动 AAR 中的 VLC MainActivity
       AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", currentActivity, new AndroidJavaClass("org.videolan.vlc.gui.MainActivity"));
       
       // 注意：此处需要编写一个继承自 AndroidJavaProxy 的回调接口来接收 onActivityResult
       // 为了简化，也可以在 Android 端通过 UnityPlayer.UnitySendMessage 向 C# 发送选中结果
       currentActivity.Call("startActivity", intent); 
   }
   ```
4. **接收回调并播放**：
   ```csharp
   // 供 Android 端回调的 C# 方法
   public void OnVideoSelected(string uri)
   {
       Debug.Log("接收到要播放的视频: " + uri);
       // 将 URI 传给 AVPro 或 Pico SDK 开始硬件解码并渲染到 3D 空间
       avproMediaPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.AbsolutePathOrURL, uri, true);
   }
   ```

### 阶段四：自定义 VR 播放控件
1. **构建 VR UI**：在 Unity 中创建一个 World Space Canvas，将其放置在视频画面下方或通过手柄按键呼出。
2. **绑定控制逻辑**：
   - 使用 Pico SDK 的 Raycast 射线进行 UI 点击。
   - 编写脚本调用 AVPro 的 API 控制播放：`avproMediaPlayer.Control.Play()` / `Pause()` / `Seek()`。
   - 监听 Pico 手柄按键（如左摇杆上下控制音量，右摇杆左右控制快进/快退）。

## 4. 方案优势总结
1. **研发成本极低**：彻底规避了“在 VR 空间中用射线点击虚拟键盘去输入 SMB 账号密码”这种极其痛苦且容易出 Bug 的开发工作，将复杂的设置与列表浏览全部交给成熟的 2D Android UI（VLC）。
2. **性能拉满**：摒弃了 libVLC 在移动端 VR 渲染上的水土不服，将核心解码与渲染交给了对底层硬件加速支持最好的 Unity/AVPro/ExoPlayer。
3. **体验流畅**：“2D 选片 -> 瞬间切入 3D 沉浸式观影 -> 退出返回 2D 列表” 是目前 Pico 平台上主流视频应用（如 Pico 视频、爱奇艺 VR 早期版本）公认最高效的交互范式。
