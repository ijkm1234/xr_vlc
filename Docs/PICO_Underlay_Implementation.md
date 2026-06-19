# PICO Underlay 视频实现方案

## 目标

平面、柱面、全景（Flat / Cylinder / Sphere360 / Sphere180）播放时：视频在 PICO 合成层中保持硬解直出，字幕 / UI / 手柄 / 射线正常显示在视频上方。

---

## 最终方案（三处必要配置）

### 1. PicoRenderSurface：合成层设为 Underlay 类型

```csharp
// PicoRenderSurface.RebuildLayer() — 必须在 InitializeBuffer() 之前设置
_compLayer.overlayType = isImmersive
_compLayer.overlayType = PXR_CompositionLayer.OverlayType.Underlay;
```

Overlay 层永远覆盖 eye buffer（手柄/UI 会被遮挡）。Underlay 层在 eye buffer 之下，eye buffer 中 alpha=0 的像素透出 Underlay 视频。

### 2. Player Settings：启用 Preserve Framebuffer Alpha

`Project Settings → Player → Android → Other Settings → Preserve Framebuffer Alpha = true`

代码等价：
```csharp
PlayerSettings.preserveFramebufferAlpha = true;
```

**原因：** 默认为 false 时，URP 选用 `B10G11R11_UFloatPack32`（无 alpha 通道）作为 HDR 渲染纹理，`_ENABLE_ALPHA_OUTPUT` 不开启，FinalBlit 强制写 `alpha = 1.0`，eye buffer 中永远不存在 alpha=0 的像素，PICO compositor 无法显示 Underlay，屏幕全黑。

启用后 URP 改用 `R16G16B16A16_SFloat`（有 alpha），FinalBlit 正确将 alpha 传递到 XR eye texture。

### 3. VideoScreen：播放模式下使用透明 eye buffer

```csharp
// SetupFlatMode / SetupImmersiveMode
_mainCamera.clearFlags = CameraClearFlags.SolidColor;
_mainCamera.backgroundColor = new Color(0, 0, 0, 0); // alpha=0
```

**原因：** `SolidColor + backgroundColor.a=0` 使 eye buffer 背景从 clear 阶段就是 alpha=0。PICO compositor 在 alpha=0 处显示 Underlay 视频。不透明对象（手柄）由 opaque pass 写入 alpha=1，UI 由透明 blend 写入 alpha=srcAlpha²（全不透明时=1），均正常可见。这是 PICO 官方 BuildingBlocks 的标准做法。

平面模式下 `BackgroundBoard` 必须停用。它是一个不透明黑色 MeshRenderer，如果继续渲染会把 Underlay 视频整块遮住；代码只保留它的 Transform 尺寸作为平面视频布局参考。

---

## Alpha 传递完整链路（URP 17 / Unity 6）

```
clear（SolidColor alpha=0）
  ↓
opaque pass：手柄等不透明对象写 alpha=1
  ↓
transparent pass：UI 以 Blend SrcAlpha OneMinusSrcAlpha 写 alpha≈srcAlpha²
                  （srcAlpha=1 时 alpha=1）
  ↓
FinalBlit（_ENABLE_ALPHA_OUTPUT=true，因 RT 有 alpha 通道）：原样复制 alpha
  ↓
PICO compositor：alpha=0 → 显示 Underlay 视频
                 alpha=1 → 显示 eye buffer 内容（手柄、UI）
```

关键依赖链：`preserveFramebufferAlpha=true` → RT 格式包含 alpha → `isAlphaOutputEnabled=true` → FinalBlit 保留 alpha。

---

## 踩坑记录

### 坑 1：打洞球（Hole Punch Sphere）破坏 UI 和射线

**现象：** 加入打洞球后手柄可见，但 UI 和射线完全消失。

**原因：** 打洞球的 `LateUpdate` 将 `transform.position` 贴到相机位置，导致 pivot 距相机距离=0。透明队列按从远到近排序，距离=0 的球体**最后渲染**。UI/射线是透明对象（ZWrite Off），不写深度缓冲，像素深度值仍是远平面，球体 ZTest 通过，将这些像素的 alpha 覆盖为 0，UI 和射线消失。

**结论：** `SolidColor + alpha=0` 清屏方案下背景 alpha 天然为 0，打洞球完全多余且有害，不要使用。

### 坑 2：overlayType 必须在 InitializeBuffer() 之前设置

`overlayShape`、`overlayType`、`externalAndroidSurface3DType` 均在 `InitializeBuffer()` 时固化，之后修改不生效。

每次切换投影模式的正确流程：`DestroyLayer()` → 设置所有参数 → `InitializeBuffer()`。

### 坑 3：Shader.Find() 在 Android APK 中会因 shader stripping 返回 null

若用 `Shader.Find()` 动态创建材质，shader 可能在打包时被剔除。需要将 shader 引用的 Material asset 序列化到 Inspector 字段，或在 `Graphics Settings → Always Included Shaders` 中手动添加。

---

## 不需要的东西

- 打洞球 GameObject
- `PXR_UnderlayHole` / 自定义打洞 shader
- 额外的 URP Renderer Feature
- 修改 UI 或射线材质
