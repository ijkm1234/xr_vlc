# PICO XR SDK (v3.4.0) ExternalSurface 视频裁切修复补丁说明

## 问题背景
在使用 PICO SDK 的 `PXR_CompositionLayer` 进行视频硬件解码（通过 Android `ExternalSurface` 将 `MediaCodec` 直接渲染到合成层）时，视频画面会出现严重的比例失调和左下角裁切现象。

**根本原因分析：**
1. **1024x1024 硬编码限制**：PICO SDK 在初始化 `ExternalAndroidSurface` 时，无视传入的纹理尺寸，强行将画布请求分辨率写死为 1024x1024。
2. **C++ 底层懒加载 Bug**：在调用 `CreateExternalSurface` 获取底层句柄时，SDK 遗漏了发送配置参数（`UPxr_CreateLayerParam`）的步骤。导致底层 C++ 引擎在没有参数的情况下，使用默认的 1024x1024 兜底生成 Surface。
3. **重建权限封闭**：核心的参数重置方法 `InitializeBuffer()` 被声明为 `private`，导致在切换不同分辨率的视频时，无法在 Unity 层主动销毁并以新分辨率重建 Surface。

当视频的真实物理像素（例如 1920x1080）大于被硬编码限制的 Android Surface（1024x1024）时，Android 底层绘图机制会将溢出的像素直接丢弃，从而导致了最终的“裁切”现象。

---

## 补丁详情

为了彻底解决上述问题，我们需要对 PICO SDK 的源码进行三处核心修改。

### 1. 修改文件：`PXR_CompositionLayer.cs`

**路径：** `Runtime/Scripts/Features/PXR_CompositionLayer.cs`

#### Patch 1.1: 开放初始化方法的访问权限
为了允许我们在播放新视频时强制刷新分辨率参数，需要将 `InitializeBuffer` 从私有改为公开。

**修改前：**
```csharp
private void InitializeBuffer()
{
    // ...
}
```

**修改后：**
```csharp
public void InitializeBuffer()
{
    // ...
}
```

#### Patch 1.2: 解除 1024x1024 硬编码限制
修改 `InitializeBuffer()` 内部的判断逻辑，使其在 `isExternalAndroidSurface` 为 `true` 时，也能优先读取 Unity 层传入的 `layerTextures` 分辨率。

**修改前：**
```csharp
else
{
    overlayParam.width = 1024;
    overlayParam.height = 1024;
}
```

**修改后：**
```csharp
else
{
    if (layerTextures[0] != null)
    {
        overlayParam.width = (uint)layerTextures[0].width;
        overlayParam.height = (uint)layerTextures[0].height;
        PLog.i(TAG, $"[PICO_MOD] Set ExternalSurface resolution from layerTextures[0]: {overlayParam.width}x{overlayParam.height}");
    }
    else if (layerTextures[1] != null)
    {
        overlayParam.width = (uint)layerTextures[1].width;
        overlayParam.height = (uint)layerTextures[1].height;
        PLog.i(TAG, $"[PICO_MOD] Set ExternalSurface resolution from layerTextures[1]: {overlayParam.width}x{overlayParam.height}");
    }
    else
    {
        overlayParam.width = 1024;
        overlayParam.height = 1024;
        PLog.i(TAG, "[PICO_MOD] Set ExternalSurface resolution to fallback 1024x1024");
    }
}
```

#### Patch 1.3: 修复底层懒加载引发的分辨率丢失 Bug
在 `CreateExternalSurface` 方法中，强制在获取 Android Surface 句柄之前，将刚刚计算好的正确分辨率参数下发给底层 C++ 引擎。

**修改前：**
```csharp
public void CreateExternalSurface(PXR_CompositionLayer overlayInstance)
{
#if UNITY_ANDROID && !UNITY_EDITOR
    if (IntPtr.Zero != overlayInstance.externalAndroidSurfaceObject)
    {
        return;
    }

    PXR_Plugin.Render.UPxr_GetLayerAndroidSurface(overlayInstance.overlayIndex, 0, ref overlayInstance.externalAndroidSurfaceObject);
    // ...
```

**修改后：**
```csharp
public void CreateExternalSurface(PXR_CompositionLayer overlayInstance)
{
#if UNITY_ANDROID && !UNITY_EDITOR
    if (IntPtr.Zero != overlayInstance.externalAndroidSurfaceObject)
    {
        return;
    }

    // [PICO_MOD] 强制在获取 Surface 之前创建图层参数，防止底层 C++ 懒加载使用 1024x1024 兜底
    PLog.i(TAG, $"[PICO_MOD] Forcing UPxr_CreateLayerParam before UPxr_GetLayerAndroidSurface. Size: {overlayInstance.overlayParam.width}x{overlayInstance.overlayParam.height}");
    PXR_Plugin.Render.UPxr_CreateLayerParam(overlayInstance.overlayParam);

    PXR_Plugin.Render.UPxr_GetLayerAndroidSurface(overlayInstance.overlayIndex, 0, ref overlayInstance.externalAndroidSurfaceObject);
    // ...
```

---

### 2. 修改文件：`PXR_OverLay.cs` (可选，兼容旧版逻辑)

如果项目中存在使用旧版 `PXR_OverLay` 的遗留代码，建议同步修改以防万一。

**路径：** `Runtime/Scripts/Features/PXR_OverLay.cs`

**修改前：**
```csharp
else
{
    overlayParam.width = 1024;
    overlayParam.height = 1024;
}
```

**修改后：**
```csharp
else
{
    if (layerTextures[0] != null)
    {
        overlayParam.width = (uint)layerTextures[0].width;
        overlayParam.height = (uint)layerTextures[0].height;
    }
    else if (layerTextures[1] != null)
    {
        overlayParam.width = (uint)layerTextures[1].width;
        overlayParam.height = (uint)layerTextures[1].height;
    }
    else
    {
        overlayParam.width = 1024;
        overlayParam.height = 1024;
    }
}
```

---

## 业务层 (Unity) 调用规范

在应用了上述补丁后，业务层（例如我们的 `PicoVideoScreen.cs`）在准备播放新视频时，需要按照以下流程强制 PICO 根据视频的真实分辨率重建 Surface：

```csharp
// 1. 彻底销毁旧的 Android Surface
_compLayer.DestroyLayer();
_compLayer.enabled = false;

// 2. 清理旧句柄
_compLayer.externalAndroidSurfaceObject = IntPtr.Zero;
_compLayer.textureType = Unity.XR.PXR.PXR_CompositionLayer.TextureType.ExternalSurface;
_compLayer.isExternalAndroidSurface = true;

// 3. 传入带有真实视频分辨率的 RenderTexture 以“欺骗”并告知 PICO 目标尺寸
var rt = new RenderTexture((int)videoWidth, (int)videoHeight, 0);
_compLayer.layerTextures[0] = rt;
_compLayer.layerTextures[1] = rt;

// 4. 调用我们公开的 InitializeBuffer，让 PICO 重新计算 overlayParam 的宽高
_compLayer.InitializeBuffer();

// 5. 重新启用，触发底层带着正确的参数去申请 Android Surface
_compLayer.enabled = true;
```

## 注意事项
由于 PICO SDK 通常作为 Local Package 引入，修改源码后，Unity 极大概率不会自动清理旧的编译缓存。**在应用此补丁后，必须手动删除 Unity 项目下的缓存 DLL，否则补丁不会生效！**

**清理命令（Mac/Linux）：**
```bash
rm -f Library/ScriptAssemblies/Unity.XR.PICO.dll
```
删除后返回 Unity 等待重新编译即可。