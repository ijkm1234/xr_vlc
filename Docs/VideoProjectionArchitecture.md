# XRVLC 播放器多投影模式适配架构设计方案

## 1. 背景与问题
目前 `PicoVideoScreen`（硬件合成层）是作为子物体固定挂载在“背景板”（父物体）之下的。这种结构在渲染传统的平面视频时非常合适，但无法满足沉浸式视频（360度全景、180度鱼眼）的播放需求。

在全景和鱼眼模式下：
1. 玩家是被包裹在视频内部的，不需要传统的“画框”或“幕布”（背景板）。
2. 视频的中心必须与玩家的头部重合，而不是挂在远处的背景板上。
3. 视频球面的缩放必须锁定，不能跟随外部物体变形。

因此，需要一套解耦的架构方案，使得视频渲染层能够根据 `VideoProjection` 动态调整其父子关系、空间位置和形态。

---

## 2. 核心设计理念：背景板与视频层的解耦
引入一个独立的 `VideoAnchor`（视频锚点）作为 `PicoVideoScreen` 的直接父节点，打破视频层与背景板的强制绑定。

### 2.1 推荐的场景层级结构 (Hierarchy)
```text
Player Controller (播放器根节点)
 ├── UI Canvas (控制面板，如进度条、播放按钮等)
 ├── BackgroundBoard (背景板实体，用于平面视频的“画框”)
 └── VideoAnchor (空物体，视频层的空间锚点)
      └── PicoVideoScreen (挂载 PXR_CompositionLayer，实际的视频渲染层)
```

### 2.2 模式状态切换逻辑
根据视频的不同投影模式，动态改变 `VideoAnchor` 的行为和状态。

#### 模式 A: 平面视频模式 (Flat - 2D / 3D 分屏)
玩家坐在外部观看，类似看电视或电影院。
* **背景板状态**：**显示**（开启 MeshRenderer 或 SetActive(true)）。
* **空间关系**：将 `VideoAnchor` 设置为 `BackgroundBoard` 的子节点，并对齐中心。
* **UI 状态**：挂载在背景板附近，方便操作。
* **尺寸适配**：依赖 `FitVideoSize` (Fit-to-Contain) 算法，确保视频在背景板内按比例缩放。

#### 模式 B: 沉浸式视频模式 (360 全景 / 180 鱼眼)
玩家被包裹在画面中，成为世界的中心。
* **背景板状态**：**强制隐藏**（SetActive(false)）。
* **空间关系**：解除 `VideoAnchor` 与背景板的父子关系（`SetParent(null)` 或挂在根节点）。
* **中心定位**：将 `VideoAnchor` 的世界坐标一次性吸附到**玩家头部相机 (Main Camera)** 的位置，使玩家位于球心。
* **UI 状态**：从背景板剥离，悬浮在玩家正前方固定距离（如 1.5 米），跟随玩家头部水平偏航旋转，避免被视频球面遮挡或找不到 UI。
* **尺寸适配**：强制锁定 `VideoAnchor` 和 `PicoVideoScreen` 的 `localScale` 为 `Vector3.one`，防止球面变形。利用 PICO 提供的 `Radius` 属性控制球体大小。

---

## 3. 球面中心的调节与复位机制
在沉浸式模式下，`VideoAnchor.position` 即为视频球体的中心。对球心的控制是解决 VR 观影痛点的核心。

### 3.1 一次性吸附 (Non-Realtime Follow)
进入沉浸式模式时，球心应**一次性吸附**到玩家头部位置，随后保持世界坐标固定。
**注意**：绝对不能在 `Update()` 中让球心实时跟随玩家头部移动，这会破坏视差，引发强烈的晕动症。

### 3.2 视角复位 (Recenter)
当玩家在现实中走动或转椅导致视角偏移时，提供一键复位功能：
1. **平移对齐**：将 `VideoAnchor.position` 重新移动到玩家当前的相机位置。
2. **旋转对齐**：将 `VideoAnchor.rotation` 的水平偏航角（Yaw）对准玩家当前的面朝方向，确保视频的正前方始终在玩家眼前。

### 3.3 高级偏移调节 (Offset Tuning)
允许玩家通过摇杆或 UI 微调球心位置：
* **高度适配**：调节 `Y` 轴坐标，解决站姿/坐姿观看导致的“地板太高/太低”问题。
* **躺姿观影**：调节 `X` 轴旋转（Pitch），将视频的“赤道”翻转到天花板。

---

## 4. 预期实现伪代码

以下是针对 `PlayerController` 或负责管理场景状态的脚本的伪代码参考：

```csharp
using UnityEngine;

public class VideoModeManager : MonoBehaviour
{
    [Header("References")]
    public Transform playerHeadCamera;     // 玩家头部相机 (Main Camera)
    public Transform backgroundBoard;      // 背景板实体
    public Transform videoAnchor;          // 视频层的挂载锚点
    public Transform uiCanvas;             // 控制面板 UI

    /// <summary>
    /// 根据视频投影模式切换场景状态
    /// </summary>
    public void SwitchVideoMode(VideoProjection projection)
    {
        if (projection == VideoProjection.Flat)
        {
            SetupFlatMode();
        }
        else if (projection == VideoProjection.Sphere360 || projection == VideoProjection.Dome180)
        {
            SetupImmersiveMode();
        }
    }

    private void SetupFlatMode()
    {
        // 1. 显示背景板
        backgroundBoard.gameObject.SetActive(true);

        // 2. 将视频锚点挂载到背景板上，重置局部坐标和旋转
        videoAnchor.SetParent(backgroundBoard);
        videoAnchor.localPosition = Vector3.zero;
        videoAnchor.localRotation = Quaternion.identity;
        videoAnchor.localScale = Vector3.one;

        // 3. UI 依附于背景板
        uiCanvas.SetParent(backgroundBoard);
        // ... 设置 UI 的局部偏移量 ...
    }

    private void SetupImmersiveMode()
    {
        // 1. 隐藏实体背景板，因为它会遮挡全景视野
        backgroundBoard.gameObject.SetActive(false);

        // 2. 解除视频锚点与背景板的绑定 (放到世界根节点或某个独立的 VR 容器下)
        videoAnchor.SetParent(null);
        videoAnchor.localScale = Vector3.one; // 锁定缩放防止球体变形

        // 3. 将球心一次性对齐到玩家头部
        RecenterImmersiveSphere();

        // 4. UI 悬浮在玩家正前方
        uiCanvas.SetParent(null);
        // ... 根据 playerHeadCamera 的前方计算 UI 位置，例如前推 1.5 米 ...
    }

    /// <summary>
    /// 视角复位：将全景/180度球体的中心重新对准玩家
    /// </summary>
    public void RecenterImmersiveSphere()
    {
        if (videoAnchor.parent != null) return; // 仅在沉浸式模式下有效

        // 平移：球心对准玩家头部
        videoAnchor.position = playerHeadCamera.position;

        // 旋转：将视频的正前方(Z轴)对准玩家当前的面朝方向 (仅提取 Y 轴水平旋转)
        float playerYaw = playerHeadCamera.eulerAngles.y;
        videoAnchor.rotation = Quaternion.Euler(0, playerYaw, 0);
        
        Debug.Log($"[VideoModeManager] 视角已复位，新球心位置: {videoAnchor.position}");
    }
}
```
