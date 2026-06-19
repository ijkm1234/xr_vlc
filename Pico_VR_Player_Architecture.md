# Pico VR 全能播放器系统架构设计文档

## 1. 系统概述

Pico VR 全能播放器是一款基于“双态融合”架构设计的混合型应用。它结合了传统 Android 2D 应用在文件管理、网络协议支持上的成熟优势，以及 Unity 在 VR 环境下高性能 3D/全景视频渲染的强大能力。该播放器旨在为用户提供类似 VLC 的全功能媒体库体验，同时突破原生 VLC 在 VR 设备中的 2D 平面限制。

### 1.1 核心目标
* **极致兼容**：支持本地存储及各类网络协议（SMB、FTP、DLNA 等）的媒体发现与访问。
* **沉浸体验**：在 VR 环境中实现高性能的 2D、3D（左右/上下）、180°/360° 全景视频播放。
* **性能最优**：利用硬件解码能力（如骁龙芯片的底层加速），支持高达 4K/8K @ 60/90fps 的高规格视频播放。
* **体验流畅**：“2D 选片 -> 瞬间切入 3D 沉浸式观影 -> 退出返回 2D 列表”的无缝切换。

---

## 2. 整体架构设计

系统采用 **Android AAR 插件 + Unity 核心** 的单应用混合架构模式。

```mermaid
graph TD
    A[用户交互] -->|2D 选片| B(Android UI 模块)
    A -->|VR 观影/控制| C(Unity VR 模块)

    subgraph Android AAR (基于 VLC 魔改)
        B --> D[媒体库扫描]
        B --> E[网络发现与连接 SMB/FTP/DLNA]
        E --> F[HTTP 本地流媒体代理]
        D --> G{URI 拦截与回传}
        E --> G
    end

    subgraph Unity (VR 核心渲染层)
        G -->|传递视频 URI| H[Unity C# 交互层]
        H --> I[Pico VideoPlayer SDK / ExoPlayer]
        I --> J[硬件解码层 MediaCodec]
        I --> K[渲染管线 Render Pipeline]
        K --> L[3D 场景/天空盒材质映射]
        C --> M[VR 射线交互 UI]
        M --> H
    end
```

### 2.1 模块分层

系统分为三大核心模块：

#### 2.1.1 媒体库与资源管理模块 (Android Native - 基于 VLC)
作为 Unity 的 AAR 插件存在，负责所有非播放核心的“重活”：
* **2D UI 展示**：提供基于系统级弹出面板的媒体库浏览界面。
* **本地设备扫描**：扫描并分类设备上的音视频文件。
* **局域网协议支持**：负责局域网发现与连接，处理复杂的带鉴权的 SMB (v1/2/3)、FTP、UPnP、DLNA 协议。
* **事件拦截机制**：用户在 2D 界面点击视频时，不启动原生播放器，而是关闭界面并通过 Intent/回调将媒体的 URI 和元数据回传给 Unity 层。

#### 2.1.2 代理与流媒体转换模块 (Android Native)
解决 Unity 播放器无法直接处理复杂网络协议（特别是带鉴权的 SMB）的痛点：
* **本地 HTTP 代理服务**：当选中的是 SMB/FTP 等协议的流时，后台启动一个轻量级 HTTP Server（如 NanoHTTPD）。
* **流转换与转发**：该服务负责与目标服务器建立连接并持续拉取视频流数据。
* **URI 替换**：将原始的 `smb://...` 替换为 `http://127.0.0.1:xxxx/stream` 形式，伪装成普通的在线 HTTP 视频流供 Unity 播放。

#### 2.1.3 VR 渲染与播放模块 (Unity 引擎层)
播放器的视觉与体验核心：
* **解码引擎**：集成 Pico 官方 VideoPlayer SDK（基于 AndroidX Media3/ExoPlayer），充分利用底层硬件解码能力，支持 H.264/H.265/AV1 等编码格式及高分辨率高帧率。
* **空间映射引擎与合成层 (Compositor Layers)**：将解码出的视频帧数据应用到 3D 场景中。
  * **普通与 3D 视频**：**强烈建议使用 Compositor Layers（如 PICO 的 `PXR_OverLay`）**。通过将视频画面作为 Overlay 传递给底层合成器，绕过 Unity 的常规渲染管线和眼球畸变校正，从而大幅提升视频清晰度、减少锯齿并降低渲染开销。
  * **全景视频 (180°/360°)**：若使用 Compositor Layers 支持等距柱状投影 (Equirectangular) 会有极佳效果。若底层不支持，则回退使用常规的大型 Sphere（球体）加上自定义 Shader 进行映射。
* **VR 交互 UI**：在 3D 空间中构建 World Space Canvas。利用 Pico 手柄射线实现播放进度控制、音量调节、视角重置、视频格式切换等交互功能。

---

## 3. 关键交互流程

### 3.1 场景与 Activity 切换策略

在“双态融合”架构中，由于 2D 媒体库 UI（Android Native）和 3D VR 播放器（Unity）分属不同的渲染层级，其生命周期和场景管理策略如下：

*   **单 Unity 场景原则**：Unity 端**不需要且不建议**将 UI 和播放器分离为不同的 Unity Scene。建议采用唯一的 `MainVRScene.unity`。
    *   原因：加载 Unity 场景（`SceneManager.LoadScene`）在移动端 VR 中会引起明显的卡顿和黑屏，破坏沉浸感。VR 播放器控制面板（如进度条、音量）可以直接作为 3D UI（World Space Canvas）叠加在播放画面之上，通过隐藏/显示来管理。
*   **Activity 切换机制**：Unity Player 运行在一个全屏的 `UnityPlayerActivity` 中。当需要 2D 媒体库选片时：
    1.  Unity 不卸载当前场景，而是直接通过 Intent 启动覆盖在其上方的 VLC 2D `MainActivity`。
    2.  此时 Unity 的 Activity 会进入 `onPause`/`onStop` 状态（画面可能暂停或变黑），这是 Android 系统的标准行为。
    3.  当在 2D VLC 界面选中视频并 `finish()` 后，顶层 2D Activity 消失，底层的 `UnityPlayerActivity` 会自动触发 `onResume` 并恢复到前台。
    4.  Unity 接收到选中视频的回调数据，在**原有的单一场景**中重置播放器状态、开始加载并播放新视频。

### 3.2 启动与媒体选择流程
1. **Unity 启动**：用户在 Pico 中打开应用，进入默认的 3D 虚拟场景。
2. **呼出媒体库**：用户通过手柄射线点击场景中的“打开媒体库”按钮，Unity 通过 `AndroidJavaObject` 触发 Intent，唤起 AAR 中的 VLC MainActivity（2D 界面）。
3. **浏览与选择**：用户在熟悉的 2D 列表中浏览本地或局域网视频。
4. **数据回传**：点击视频后，VLC 的 Adapter 拦截点击事件，将视频 URI 和标题通过 `setResult` 返回，并 `finish()` 当前 Activity。
5. **Unity 接收**：Unity 层接收到回调数据（URI），准备播放。

### 3.2 视频播放与代理流转流程
1. **协议判断**：Unity 拿到 URI 后，C# 逻辑判断协议类型。
2. **普通协议 (Local/HTTP)**：直接将 URI 传递给 Pico VideoPlayer SDK 进行加载解码。
3. **特殊协议 (SMB 等)**：
   - Unity 通过 JNI 通知 Android 层启动本地 HTTP 代理。
   - Android 层建立与 SMB 服务器的连接。
   - Android 层将生成的本地 `127.0.0.1` 代理 URI 返回给 Unity。
   - Unity 将代理 URI 传递给播放器组件。
4. **渲染输出**：ExoPlayer 完成硬解后，将画面渲染到 Unity 的 Texture/Material 上，展示在 VR 空间中。

---

## 4. 技术选型与依赖

| 领域 | 选型 | 理由 |
| :--- | :--- | :--- |
| **主引擎** | Unity | VR 平台最成熟的 3D 渲染引擎，Pico 官方支持度最高。 |
| **媒体库与网络** | VLC for Android (精简版) | 强大的本地扫描与全网络协议支持，开源免费，省去大量造轮子工作。 |
| **视频解码引擎** | Pico VideoPlayer SDK (ExoPlayer) | 免费开源，与 Pico 硬件适配最佳，支持 4K/8K 硬件加速，自带 3D/全景映射 Shader。 |
| **HTTP 代理库** | NanoHTTPD 或 AndroidAsync | 轻量级、易于嵌入 Android 的 HTTP 服务器，用于 SMB 视频流转发。 |
| **VR SDK** | Pico Integration SDK | 官方 SDK，提供基础的头部追踪、手柄输入、射线交互功能。 |

---

## 5. 架构优势

1. **扬长避短，极致效能**：把复杂的网络连接、文件解析、2D 列表滚动等非渲染密集型任务交给 Android 原生处理；把 3D 渲染、空间音频、高帧率画面映射交给 Unity。
2. **规避交互痛点**：避免了在 VR 3D 空间中用射线点击虚拟键盘输入 SMB 账号密码这种极其低效且容易导致晕眩的交互方式。
3. **开发成本极低**：核心组件均建立在成熟的开源/官方方案之上（VLC + Pico VideoPlayer），避免了从零编写 SMB 协议栈或底层解码器的巨大风险。
4. **无缝体验**：“2D 选片，3D 观影”的模式符合目前头部 VR 视频应用（如 Pico 视频、爱奇艺 VR）的最佳实践。

---

## 6. 项目结构规划

根据双态融合架构，项目在文件系统上主要分为两大独立部分：Unity 工程目录和 Android 插件目录。以下是规划的完整项目结构：

```text
xr_vlc/
├── UnityProject/                      # Unity VR 核心工程
│   ├── Assets/
│   │   ├── PicoVideoPlayer/           # Pico 官方视频播放 SDK (ExoPlayer 封装)
│   │   ├── PicoIntegration/           # Pico 官方 VR 基础 SDK (头部追踪、手柄)
│   │   ├── Plugins/
│   │   │   └── Android/
│   │   │       ├── AndroidManifest.xml        # 混合应用合并后的清单文件
│   │   │       └── vlc-media-library.aar      # 核心：编译自 vlc-android 的精简版 AAR 插件
│   │   ├── Scenes/
│   │   │   └── MainVRScene.unity      # 唯一的 Unity 场景，包含虚拟影院与交互 UI
│   │   ├── Scripts/                   # Unity C# 脚本目录
│   │   │   ├── Core/
│   │   │   │   └── PlayerController.cs    # 视频播放核心控制逻辑
│   │   │   ├── AndroidBridge/
│   │   │   │   ├── VlcAarBridge.cs        # 负责呼起 VLC 2D 界面及接收 Intent 回调
│   │   │   │   └── StreamProxyBridge.cs   # 负责控制 Android 端 HTTP 代理服务的启停
│   │   │   └── UI/
│   │   │       ├── VRUIManager.cs         # 射线交互与控制面板管理
│   │   │       └── FormatSelector.cs      # 2D/3D/180/360 格式切换逻辑
│   │   └── Shaders/                   # 自定义视频映射着色器
│   │       ├── Equirectangular.shader     # 360° 全景映射 Shader
│   │       └── StereoSideBySide.shader    # 3D 左右格式分离 Shader
│   └── ProjectSettings/               # Unity 工程配置
│
└── vlc-android/                       # Android AAR 插件源码 (从官方拉取并魔改)
    ├── application/
    │   └── vlc-android/               # 核心 UI 模块
    │       ├── src/org/videolan/vlc/gui/
    │       │   ├── MainActivity.kt        # 媒体库 2D 入口界面
    │       │   ├── video/
    │       │   │   ├── VideoGridFragment.kt   # [已修改] 拦截视频点击，回传 URI 给 Unity
    │       │   │   └── VideoPlayerActivity.kt # [已修改] 剔除原生播放器，保留空 Stub
    │       │   └── network/               # SMB/FTP 网络发现界面
    │       ├── res/                   # 2D UI 资源文件 (Layout, Drawable 等)
    │       ├── AndroidManifest.xml    # AAR 清单文件，声明 Activity 等组件
    │       └── build.gradle           # [已修改] 移除 libvlc 依赖，仅保留 medialibrary
    ├── medialibrary/                  # VLC 官方媒体扫描库
    ├── proxy-server/                  # [待开发] 新增：用于 SMB 协议的 HTTP 代理转发模块
    │   ├── src/.../StreamProxyService.kt  # 后台流媒体代理服务
    │   └── build.gradle
    ├── settings.gradle                # [已修改] 配置子工程依赖
    └── build.gradle                   # 顶层构建脚本
```

### 6.1 目录职责说明

*   **`UnityProject/`**：整个 APP 的最终打包入口。负责编译 APK，包含所有 3D 资产、Shader 和 C# 业务逻辑。
*   **`UnityProject/Assets/Plugins/Android/vlc-media-library.aar`**：连接两个世界的桥梁。包含了 VLC 强大的媒体扫描、UI 列表和网络协议栈。
*   **`vlc-android/`**：独立的 Android Studio 工程。作为 AAR 插件的生产工厂。我们在此处进行源码魔改，通过 `./gradlew assembleDebug` 等命令输出 AAR 文件，并拷贝到 Unity 的 Plugins 目录中。
*   **`vlc-android/proxy-server/`**：为了解决 Unity ExoPlayer 无法直接读取 SMB 的问题，将在 Android 侧新增的代理模块。

---

## 7. 后续演进规划
* **V1.0 (MVP)**：完成 VLC AAR 嵌入，实现本地视频 2D/360° 播放。
* **V1.5**：引入 HTTP 本地代理，实现 SMB 局域网视频流畅播放。
* **V2.0**：丰富 VR 播放场景（虚拟影院、星空等），增加字幕（SRT/ASS）解析与 3D 空间渲染支持。
