# Unity 侧实施与配置清单

本文档详细说明了在 `UnityProject` 目录中需要进行的所有具体操作。这些操作将把基于 Pico 的 VR 环境与我们编译出的 VLC Android 插件（媒体库与本地代理）连接起来。

---

## 1. 基础环境与 SDK 导入

1. **打开工程**：
   - 使用 Unity Hub 添加并打开 `/Users/admin/xr_vlc/UnityProject` 目录。
   - 建议 Unity 版本：`2021.3.x LTS` 或 `2022.3.x LTS`。
2. **切换平台**：
   - 打开 `File -> Build Settings`。
   - 选择 `Android` 平台，点击 `Switch Platform`。
3. **导入 PICO Unity Integration SDK**：
   - 前往 Pico 开发者中心下载 **PICO Unity Integration SDK** 压缩包并解压。
   - 由于下载解压后是一个包含 `package.json` 的标准 Unity Package 目录结构（而非单个 `.unitypackage` 文件），请使用 Package Manager 导入：
     1. 在 Unity 顶部菜单栏点击 `Window -> Package Manager`。
     2. 点击左上角的 `+` 号图标，选择 `Add package from disk...`。
     3. 浏览到你解压后的 SDK 目录，选中里面的 `package.json` 文件并点击打开，Unity 即可自动完成导入。

---

## 2. Android 插件 (AAR) 配置

1. **准备 AAR 文件**：
   - 在 Android Studio 中编译 `vlc-android` 项目，或在终端执行 `./gradlew assembleDebug`。
   - 找到生成的 `medialibrary-xxx.aar`、`vlc-android-xxx.aar`（或合并后的 AAR）以及 `proxy-server-xxx.aar`。
2. **放置到 Unity 目录**：
   - 将这些 `.aar` 文件复制到 Unity 工程的 `Assets/Plugins/Android/` 目录下（如果目录不存在则手动创建）。
3. **配置清单文件 (AndroidManifest.xml)**：
   - 如果你需要覆盖权限或主题，在 `Assets/Plugins/Android/` 目录下创建一个 `AndroidManifest.xml`。
   - 确保包含网络与存储权限：
     ```xml
     <uses-permission android:name="android.permission.INTERNET" />
     <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
     <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
     ```

---

## 3. 场景搭建 (`MainVRScene`)

1. **创建主场景**：
   - 在 `Assets/Scenes/` 下新建场景并命名为 `MainVRScene.unity`，双击打开。
2. **配置 VR 相机与手柄 (Pico Integration)**：
   - 删除默认的 `Main Camera`。
   - **配置 PICO Camera Rig 与手柄**：
     - 在最新的 PICO Integration SDK 中，不再使用旧的 `Pvr_UnitySDK` 预制体。
     - 推荐通过 Package Manager 导入 `XR Interaction Toolkit` 的 `Starter Assets`，并在场景中直接使用官方提供的 `XR Origin (XR Rig)` 预制体。
     - **放置与对齐 XR Origin**：
       - 强烈建议将 `XR Origin (XR Rig)` 的 Transform Position 设置为 `(0, 0, 0)`（原点），Rotation 设置为 `(0, 0, 0)`。
       - 这有助于在后续放置视频播放屏幕（如 `Quad` 或 `Overlay`）和 UI 画布时，更容易计算相对位置和距离（例如将屏幕放在 `(0, 1.5, 3)` 的位置）。
     - **解决 XRI 3.x 射线与 UI 交互配置**：在 XRI 3.x 架构中，手柄逻辑通过 `Near-Far Interactor` 等组件重构。
       - XRI 3.x 移除了顶层的 `XR Controller` 组件，转而使用分布式的输入：位移数据由 `Tracked Pose Driver` 接收，按键输入由各个子 Interactor 独立配置的 `Input Action Reference` 接收。
       - XRI 3.x 中 `Near-Far Interactor` 默认包含了 `XRRayInteractor` 组件（在 Inspector 面板中查看），并且子节点有 `LineVisual` 提供射线视觉。
       - 确保场景中有 `EventSystem` 并且挂载了 `XR UI Input Module`。
       - 确保 UI Canvas 设置为 `World Space` 并且挂载了 `Tracked Device Graphic Raycaster`。
     - **优化观影体验（禁用不必要的移动）**：作为固定的 VR 播放器，为了防止误触摇杆导致视角移动，建议在 Hierarchy 中：
       - 禁用左右手柄下的 `Teleport Interactor` 节点（取消勾选节点名称左侧的复选框）。
       - 禁用 `Locomotion` 节点下的所有子节点（如 `Turn`, `Move`, `Teleportation`, `Climb` 等）。
     - 请确保将原场景默认的 `Main Camera` 删掉，以防冲突。
3. **配置播放器屏幕 (引入合成层 Compositor Layers 技术)**：
   > **重点**：作为高质量 VR 播放器，**强烈建议使用合成层 (Compositor Layers)** 来渲染视频。这能让视频帧直接提交给底层硬件合成器，绕过 Unity 的渲染管线，从而实现原生分辨率级的高清画质、无锯齿，且极大降低性能开销。
   - **创建载体**：在 Unity 场景中创建一个 Quad（面片）作为视频播放的载体。
     - **放置在正前方**：在 Unity 中，玩家的默认正前方是 **Z 轴正方向 (Z+)**。
     - 假设你的 `XR Origin` 位置是 `(0, 0, 0)`，建议将 Quad 的 Transform 设置为：
       - `Position` = `(0, 1.5, 4)` （正前方 4 米，高度 1.5 米约等于人眼高度）
       - `Rotation` = `(0, 0, 0)` （屏幕完全面对玩家）
       - `Scale` = `(3.2, 1.8, 1)` （模拟一个常见的 16:9 比例的大屏幕）
   - **添加 Overlay 组件**：选中该物体，在 Inspector 中点击 `Add Component`，搜索并添加 PICO SDK 提供的 **`PXR_CompositionLayer`**。
    - **配置 Overlay**：
      - 对于普通 2D 视频，将 Overlay Shape 设置为 `Quad`（平面）或 `Cylinder`（曲面屏）。
      - 对于 180°/360° 全景视频，将 Overlay Shape 设置为 `Equirect`（等距柱状投影）。
      - **动态切换形状方案（已实现方案一）**：
        - 我们已经创建了 `Assets/Scripts/VideoScreenManager.cs` 脚本。
        - **操作**：将 `VideoScreenManager.cs` 拖拽挂载到刚才创建的 `VideoScreen` (Quad) 游戏对象上。它会自动要求该物体挂载 `PXR_CompositionLayer` 组件。
        - **使用方式**：后续在处理 VLC 播放逻辑时，只需调用 `GetComponent<VideoScreenManager>().SetupScreenForVideoType(VideoScreenType.Panorama360)` 即可实现一键切换形状和包裹玩家的位置。
    - **关联 VideoPlayer (测试画面)**：为了验证屏幕正常工作，我们可以先用 Unity 自带的 VideoPlayer 测试：
       - 1. 选中 `VideoScreen` (Quad)，在 Inspector 点击 `Add Component` 添加 `Video Player` 组件。
       - 2. 在项目资源中准备一个测试的 MP4 视频，拖拽到 `Video Player` 的 `Video Clip` 槽位中。
       - 3. **挂载桥接脚本**：将项目中的 `Assets/Scripts/VideoToCompositionLayer.cs` 脚本挂载到 `VideoScreen` 上。
       - *(原理解释：因为 `PXR_CompositionLayer` 会直接把画面送到显卡，绕过了 Unity 的材质球，所以必须用代码把视频解码出来的纹理 `Texture` 直接塞给 Composition Layer 的数组 `layerTextures[0]` 中。后续接入 VLC 时也是同理。)*

---

## 4. 挂载核心交互脚本

我们已经生成了三个核心脚本，需要将它们正确挂载到场景中：

### 4.1 挂载 `VlcAarBridge`
* **关键步骤**：在场景中创建一个**空 GameObject**，**必须命名为 `VlcAarBridge`**。
* **原因**：Android 端的 `UnitySendMessage` 是通过寻找该 GameObject 的名称来回调 `OnVideoSelected` 方法的。
* **操作**：将 `Assets/Scripts/AndroidBridge/VlcAarBridge.cs` 脚本拖拽到该 GameObject 上。

### 4.2 挂载 `StreamProxyBridge`
* **操作**：在场景中创建一个空 GameObject（可命名为 `StreamProxyManager`）。
* **操作**：将 `Assets/Scripts/AndroidBridge/StreamProxyBridge.cs` 拖拽到其上。

### 4.3 挂载逻辑控制器与 UI 管理器
* **操作**：创建一个名为 `PlayerManager` 的空 GameObject，作为核心业务的管理节点。
* **挂载 PlayerController**：
  - 将 `Assets/Scripts/Core/PlayerController.cs` 拖拽到 `PlayerManager` 上。
  - 在 Inspector 面板中，将刚才创建并带有 `Video Player` 组件的屏幕物体，拖拽赋值给 `Video Player` 变量。
* **挂载 VRUIManager (事件解耦)**：
  - 将 `Assets/Scripts/UI/VRUIManager.cs` 也拖拽到 `PlayerManager` 上（或者挂载到专门管理 UI 的节点上）。
  - 在 Inspector 面板中，将 `Title Text`、`Status Text` 和包含这些 UI 的根节点 `Control Panel` 分别拖拽赋值给对应的公开变量。
  - *说明：`PlayerController` 现已完全与 UI 解耦，它只负责发出状态事件，由 `VRUIManager` 负责接收事件并更新画面文字。*

---

## 5. 构建 VR UI 面板

1. **创建 Canvas**：
   - 在 Hierarchy 中右键 -> `UI -> Canvas`。
   - 将 Canvas 的 `Render Mode` 改为 **World Space**。
     - **重要**：将场景中 `XR Origin` 下的 `Main Camera` 拖拽到 Canvas 的 **`Event Camera`** 槽位中（解决黄色警告，确保射线能正确计算点击坐标）。
     - **调整 Canvas 比例**：World Space 的 Canvas 默认尺寸非常巨大（如宽 800 米）。在 Rect Transform 中，将 Scale 的 X、Y、Z 全部设置为 `0.001` 或 `0.002`。这样 Canvas 宽 800 就相当于真实世界的 0.8 米，文字的字号（如 36）就能以合理的物理大小显示，保证边缘清晰。
     - 调整 Canvas 的 Position，将其放置在视频屏幕下方，面向玩家（例如 `(0, 1, 3)`）。
2. **添加 UI 元素**：
     - **组织结构**：建议在 Canvas 下创建一个名为 `ControlPanel` 的空 GameObject（调整它的尺寸如宽 800 高 200），然后把所有的文本和按钮都放在这个 `ControlPanel` 下面。这样你可以把这个节点拖给 `VRUIManager` 的 `Control Panel` 变量，实现一键隐藏/显示整个操作区。
     - **状态文本**：在 `ControlPanel` 下右键 -> `UI -> Text - TextMeshPro` 添加 TextMeshPro 组件（如果你是第一次使用 TMP，Unity 会弹窗提示导入 TMP Essentials，点击导入即可）。创建两个文本框，分别命名为 `TitleText` 和 `StatusText`，调整它们的位置，用于显示当前播放状态和视频标题。
     - **打开媒体库按钮**：在 `ControlPanel` 下添加一个 Button。在 Button 的 `OnClick()` 事件中，点击 `+` 号，将挂载了 `VRUIManager` 的 GameObject（如 `PlayerManager`）拖入，选择 `VRUIManager.OnOpenMediaLibraryButtonClicked` 方法。
      - **播放控制按钮**：在 `ControlPanel` 下再添加一个 Button（例如命名为 `PlayPauseBtn`）。在它的 `OnClick()` 事件中，同样拖入挂载了 `VRUIManager` 的 GameObject，然后选择 `VRUIManager.OnPlayPauseButtonClicked` 方法。
      - *(架构提示：按照 MVC/MVP 模式，所有的 UI 按钮事件现在都统一发送给 `VRUIManager`（视图管理器），再由它将指令分发给底层的 `PlayerController`（大脑）。这样视图层的交互和底层业务逻辑就实现了完全隔离！)*
3. **挂载 `VRUIManager`（如果在 4.3 步尚未挂载）**：
    - 将 `Assets/Scripts/UI/VRUIManager.cs` 挂载到专门管理逻辑或 UI 的节点上（如 `PlayerManager`）。
    - 在 Inspector 面板中，将 `ControlPanel` 节点拖拽赋值给 `Control Panel` 变量，以便实现按键隐藏/显示控制台的功能。

---

## 6. 打包与测试

1. **Player Settings 配置**：
   - 打开 `Edit -> Project Settings -> Player`。
   - 展开 `Other Settings`。
   - **Package Name**：确保与 Android AAR 的包名不冲突，通常设置为你自己的包名（如 `com.yourcompany.xr_vlc`）。
   - **Minimum API Level**：设置为 Android 6.0 (API Level 23) 或更高，以兼容 VLC 模块。
   - **Scripting Backend**：建议设置为 `IL2CPP`，并勾选 `ARM64` 架构。
2. **XR Plug-in Management**：
   - 在 `Project Settings -> XR Plug-in Management` 中，安装并勾选 `Pico` 的提供者插件。
3. **Build And Run**：
   - 连接 Pico 头显。
   - 在 `File -> Build Profiles` (Unity 6+) 或 `Build Settings` 中，确保 Platform 是 **Android**。
   - **指定 PICO 设备**：在右侧面板找到 **Run Device** 下拉菜单。
     - 确保你的 PICO 头显已经通过 USB 数据线连接到电脑，并且在头显内允许了“USB 调试”。
     - 点击 `Refresh` 刷新按钮。
     - 在 `Run Device` 下拉列表中选择你的 PICO 设备（通常会显示设备型号或一串序列号，而不是 `Default device`）。
   - 点击右下角的 `Build And Run`。Unity 会自动编译 APK 并推送到你的 PICO 头显中运行。
   - 戴上头显测试：
     1. 点击 VR UI 上的“打开媒体库”按钮。
     2. 观察是否弹出了 2D 的 VLC 界面。
     3. 在 VLC 中浏览本地或局域网视频，点击其中一个。
     4. 观察 VLC 界面是否关闭，Unity 画面是否恢复，以及是否成功开始播放选中的视频。

---

## 7. 常见问题排查与技术填坑记录 (Troubleshooting)

在整合 PICO SDK、VLC Android AAR 和 Unity XR 的过程中，我们解决了一系列深层的架构冲突。特此记录以防后续踩坑：

### 7.1 PICO 头显内打开 VLC 卡在 Loading 界面
* **现象**：通过 Intent 启动 VLC Activity 后，头显内一直显示 Loading 动画，无法看到 2D 界面。
* **原因**：Pico 系统的 `ViewRootImpl` 默认会拦截未声明类型的 Activity。如果一个 Activity 没有声明为 2D，Pico 可能会尝试将其作为 VR 渲染层处理，从而导致渲染挂起。
* **解决方案**：在合并打包的 `AndroidManifest.xml` 中，为 VLC 的核心 Activity（或 `application` 标签）补充 Pico 专用的 2D 元数据标签：
  `<meta-data android:name="pvr.app.type" android:value="2d" />`

### 7.2 多任务栈 (Multiple Task) 导致的 OpenXR 崩溃与手柄残影
* **现象**：最初为了让 VLC 和 Unity 分离，我们为 Intent 添加了 `FLAG_ACTIVITY_NEW_TASK` 和 `FLAG_ACTIVITY_MULTIPLE_TASK`。但这会导致从 VLC 返回 Unity 时，底层 OpenXR 报 `SIGSEGV` 致命崩溃；同时，打开 VLC 的瞬间，Unity 的 3D 手柄模型会像“残影”一样叠加在 2D 界面上。
* **原因**：Pico OS 在处理同一个应用的 2D 和 3D 混合多任务栈时存在系统级 Bug。
* **解决方案**：
  1. 放弃使用 `MULTIPLE_TASK`，让 VLC Activity 叠加在 Unity 的 Task 栈之上。
  2. 为了解决手柄残影，在 `VlcAarBridge.cs` 中添加逻辑，在弹出 VLC 前**瞬间隐藏所有 3D 模型 (Controller/UI)**，在收到播放回调时再恢复。
  3. 在 VLC Kotlin 代码中拦截视频点击事件，发送 `UnitySendMessage` 后，调用 `moveTaskToBack(true)` 而不是 `finish()`。这能将 VLC 原生层挂起，保留用户的目录浏览进度，同时平滑回到 Unity 3D 渲染层。

### 7.3 Unity VideoPlayer 播放本地文件失败
* **现象**：VLC 传回的 URI 类似于 `file:///storage/emulated/0/Movies/test%20video.mp4`，Unity 报错无法识别路径。
* **原因**：Unity 安卓端的 `VideoPlayer` 在处理绝对路径时，**不支持 `file://` 前缀**，且无法自动解析 URL 编码（如 `%20` 代表空格）。
* **解决方案**：在 `PlayerController.cs` 中进行路径清洗：
  1. 截取并移除 `file://` 前缀。
  2. 使用 `UnityEngine.Networking.UnityWebRequest.UnEscapeURL()` 对路径进行解码。

### 7.4 PICO 合成层 (PXR_CompositionLayer) 播放视频有声音无画面（透明）
* **现象**：视频可以正常播放，能听到声音，但挂载了 `PXR_CompositionLayer` 的 Quad 屏幕完全透明，或者变黑。
* **原因**：
  1. PICO 合成层默认开启的是 `External Surface`（外部原生表面）模式，它期待安卓原生的播放器（如 MediaPlayer/ExoPlayer）直接向底层的 Surface 句柄推送画面。
  2. Unity 的 `VideoPlayer` 只认识 Unity 自己的渲染管线，无法推送给外部原生表面，导致画面丢失。
* **解决方案**：
  使用 **`DynamicTexture`** 模式作为桥接：
  1. 在 `VideoToCompositionLayer.cs` 中，创建一个与视频分辨率匹配的 `RenderTexture`。
  2. 将 `VideoPlayer.targetTexture` 指向这个 `RenderTexture`。
  3. 将 `PXR_CompositionLayer` 的 `textureType` 改为 `DynamicTexture`，并关闭 `isExternalAndroidSurface`。
  4. 将生成的 `RenderTexture` 赋值给 `PXR_CompositionLayer.layerTextures[0]`（双眼复用）。
  这样既保留了 PICO 合成层绕过眼缓冲带来的超高清画质，又完美兼容了 Unity 内部组件的渲染输出。

### 7.5 退出应用后重新进入，直接显示 VLC 界面而不是 Unity 天空盒
* **现象**：在 Unity 中打开过一次 VLC 后，按下手柄 Home 键回到系统主页，再次点击 App 图标进入应用时，并没有回到 Unity 的 3D 场景，而是直接停留在了 VLC 的 2D 界面上。
* **原因**：
  这是由于 Android 任务栈 (Task Stack) 的亲和性 (TaskAffinity) 混淆导致的：
  1. VLC 的所有 Activity 默认没有声明独立的 `taskAffinity`，因此它们继承了宿主（Unity）的包名亲和性。
  2. 当 `VlcAarBridge` 使用 `FLAG_ACTIVITY_NEW_TASK` 启动 VLC 时，因为亲和性相同，Android 并没有真正创建一个新任务，而是将 VLC 的 `MainActivity`（配置为 `singleTask`）强行压入了 Unity 的任务栈顶。
  3. 当按下 Home 键时，整个 Unity+VLC 混合任务栈被压入后台。
  4. 再次点击 App 图标时，系统恢复了该任务栈，由于栈顶是 VLC 的 `MainActivity`，所以直接展示了 VLC，遮挡了 Unity。
  5. 此外，VLC 的 `StartActivity` 源码中残留了 `android.intent.category.LAUNCHER`，可能导致系统桌面将其误认为主入口。
* **解决方案**：
  在 `vlc-android/application/vlc-android/AndroidManifest.xml` 中进行彻底隔离：
  1. 移除 `StartActivity` 中的 `MAIN` 和 `LAUNCHER` intent-filter，确保 Unity 是唯一的桌面入口。
  2. 为 VLC 模块中的**所有** `<activity>` 标签统一增加 `android:taskAffinity=":vlc"` 属性。
  这样，VLC 就会被隔离到一个完全独立的后台任务栈中。按下 Home 键再进入时，系统会准确恢复处于主任务栈的 Unity 场景。
