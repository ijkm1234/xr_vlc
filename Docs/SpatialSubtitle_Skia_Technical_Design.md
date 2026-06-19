# 空间字幕 Skia 渲染技术方案

本文档用于后续开发空间字幕渲染链路。目标是在保留 Unity 空间摆放能力的同时，解决 TMP 字体 fallback 不稳定、中文字幕显示方块、ASS 填充色和描边色无法还原的问题。

约束：

- 不在自动化流程中操作 PICO 设备，PICO 上的播放和视觉验证由人工完成。
- 不做 Unity 功能调试，Unity Editor / 设备上的功能验证由人工完成。
- 不在自动化流程中打包 APK，APK 打包由人工完成。

## 目标

1. VLC 在播放时回传字幕文本、ASS 样式、内嵌字体信息。
2. Skia 根据 cue 数据渲染透明 RGBA 字幕纹理。
3. Unity 只负责空间字幕的位置、尺寸、显隐和渲染模式切换。
4. 支持字幕填充颜色和边缘线条颜色。
5. 支持内嵌字体优先、系统字体 fallback、项目默认字体兜底。
6. 复杂 ASS 特效不在第一阶段完整复刻，必要时继续保留 libass bitmap 兜底。

非目标：

- 不复刻完整 ASS 动画特效，例如 `\move`、`\fad`、`\t`、karaoke。
- 不把字幕直接混入视频帧。
- 不依赖 TMP 作为空间字幕主渲染器。

## 总体架构

```text
VLC demux / subtitle decoder
  -> libass.c 提取文本、样式、附件字体
  -> subpicture_t 保存 cue v2 扩展字段
  -> decoder.c 构造 subtitle cue JSON
  -> libvlc_MediaPlayerSubtitleCue
  -> Android AAR / Kotlin bridge
  -> Unity C# SubtitleCueParser
  -> SkiaSubtitleRendererBridge
  -> Android NDK libxr_subtitle_skia.so
  -> RGBA 字幕纹理
  -> Unity SpatialSubtitleTextureView
```

核心分工：

| 模块 | 职责 | 不负责 |
| --- | --- | --- |
| VLC core | 字幕解码、ASS 文本/样式提取、内嵌字体导出、cue JSON 回传 | 空间摆放、最终文字栅格化 |
| Android AAR | 事件转发、Skia JNI 桥接、字体缓存目录提供 | 字幕业务决策 |
| Skia renderer | 字体加载、fallback、排版、填充色、描边色、透明纹理输出 | 播放控制、空间定位 |
| Unity | cue 解析、纹理展示、空间位置、模式切换 | 字体 fallback、glyph rasterization |

## 当前已有基础

当前项目已经实现播放时字幕 cue 回传的基础链路：

- `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/include/vlc_subpicture.h`：`subpicture_t` 已新增 `psz_subtitle_text`。
- `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/src/misc/subpicture.c`：释放 `psz_subtitle_text`。
- `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/modules/codec/libass.c`：从 ASS block 中提取纯文本，并保存在 `psz_subtitle_text`。
- `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/src/input/decoder.c`：构造 subtitle cue JSON，并通过 `input_SendEventSubtitleCue` 回传。
- `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/lib/media_player.c` 和 `libvlcjni-mediaplayer.c`：把 `libvlc_MediaPlayerSubtitleCue` 送到 Android/JNI。
- `/Users/admin/xr_vlc/Assets/Scripts/Services/Playback/SpatialSubtitleService.cs`：Unity 侧已有空间字幕显示入口，目前使用 TextMeshPro。

新方案是在这条链路上扩展 cue 数据和渲染器，不替换播放主链路。

## Cue v2 协议

VLC 回传 JSON 升级为 version 2。保持现有 `text/startMs/endMs/trackId/source` 字段，新增 `styleRuns`、`fontAttachments`、`layout`。

示例：

```json
{
  "version": 2,
  "seq": 12,
  "trackId": "spu:3",
  "startMs": 1200,
  "endMs": 3500,
  "text": "你好 Hello",
  "source": "text",
  "styleRuns": [
    {
      "start": 0,
      "end": 8,
      "fontFamily": "Some ASS Font",
      "fontSize": 48.0,
      "bold": false,
      "italic": false,
      "fillColor": "#FFFFFFFF",
      "outlineColor": "#FF000000",
      "outlineWidth": 3.0
    }
  ],
  "fontAttachments": [
    {
      "name": "Some ASS Font.ttf",
      "family": "Some ASS Font",
      "mime": "application/x-truetype-font",
      "cachePath": "/data/data/<package>/files/subtitle_fonts/sha256.ttf",
      "size": 123456,
      "sha256": "..."
    }
  ],
  "layout": {
    "align": "bottom-center",
    "marginL": 0,
    "marginR": 0,
    "marginV": 48
  }
}
```

字段说明：

| 字段 | 说明 |
| --- | --- |
| `styleRuns[].start/end` | UTF-16 code unit 或 Unicode scalar 的区间需要统一。推荐 Unity/Android 侧使用 UTF-16 code unit，因为 C# string 和 Android Java String 都按 UTF-16 索引。 |
| `styleRuns[].fontFamily` | ASS `Style:` 的 `Fontname` 或 Dialogue override `\fn`。 |
| `fillColor` | ASS `PrimaryColour` / `\c` / `\1c` 转换后的 RGBA。 |
| `outlineColor` | ASS `OutlineColour` / `\3c` 转换后的 RGBA。 |
| `outlineWidth` | ASS `Outline` / `\bord`，单位先按脚本像素回传，Skia 渲染前映射到目标纹理像素。 |
| `fontAttachments[].cachePath` | VLC 导出的内嵌字体文件路径，位于 app 私有目录。 |
| `layout.align` | 暂按现有 `bottom-center/top-center/...` 字段兼容，后续可扩展 ASS alignment。 |

兼容策略：

- `version` 缺失或小于 2 时，Unity 按现有 text-only cue 处理。
- `styleRuns` 为空时，Skia 使用默认字幕样式。
- `fontAttachments` 为空时，Skia 使用系统字体 fallback 和项目默认字体。

## ASS 样式映射

第一阶段支持的 ASS 字段：

| ASS 来源 | cue 字段 | 说明 |
| --- | --- | --- |
| `Style.Fontname` | `fontFamily` | 默认字体族。 |
| `Style.Fontsize` | `fontSize` | 后续根据纹理宽高和空间尺寸缩放。 |
| `Style.PrimaryColour` | `fillColor` | 字幕填充色。 |
| `Style.OutlineColour` | `outlineColor` | 字幕描边色。 |
| `Style.Outline` | `outlineWidth` | 描边宽度。 |
| `Style.Bold` / `\b` | `bold` | 字重。 |
| `Style.Italic` / `\i` | `italic` | 斜体。 |
| `\fn` | `fontFamily` | Dialogue 内局部字体覆盖。 |
| `\c` / `\1c` | `fillColor` | Dialogue 内局部填充色覆盖。 |
| `\3c` | `outlineColor` | Dialogue 内局部描边色覆盖。 |
| `\bord` | `outlineWidth` | Dialogue 内局部描边宽度覆盖。 |

ASS 颜色格式通常是 `&HAABBGGRR`，转换到 RGBA 时需要：

```text
R = RR
G = GG
B = BB
A = 255 - AA
```

如果颜色省略 alpha，例如 `&HBBGGRR`，按 `AA=00` 处理，即完全不透明。

## VLC Core 修改方案

### 1. 导出内嵌字体

切入点：

```text
/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/modules/codec/libass.c
```

当前 `Create()` 中已经遍历 `decoder_GetInputAttachments()`，并把 `.ttf/.otf/.ttc` 通过 `ass_add_font()` 交给 libass。新增逻辑：

1. 判断附件是否是字体。
2. 计算 `sha256`，避免重复写入。
3. 写入 app 私有目录，例如：

```text
/data/data/<package>/files/subtitle_fonts/<sha256>.<ext>
```

4. 记录为 `fontAttachments` JSON。
5. 保留原有 `ass_add_font()`，不影响 VLC 原生字幕渲染。

需要新增一个可写目录来源。推荐由 Android AAR 在初始化 LibVLC 或 MediaPlayer 时传入：

```text
--xr-subtitle-font-cache-dir=/data/data/<package>/files/subtitle_fonts
```

如果不想新增 VLC option，也可以在 JNI 层通过 native setter 写入全局配置，但 option 更容易排查和复现。

### 2. 保存 cue 扩展字段

切入点：

```text
/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/include/vlc_subpicture.h
/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/src/misc/subpicture.c
```

建议在 `subpicture_t` 中新增：

```c
char *psz_subtitle_style_json;
char *psz_subtitle_font_attachments_json;
char *psz_subtitle_layout_json;
```

并在 `subpicture_Delete()` 中释放。

### 3. 提取 ASS 文本和样式

切入点：

```text
/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/modules/codec/libass.c
```

当前项目已有 `AssExtractPlainText()`。后续扩展为：

```text
AssExtractCueV2()
  -> plain text
  -> styleRuns JSON
  -> layout JSON
```

提取步骤：

1. 解析 ASS header 的 `[V4+ Styles]`。
2. 建立 `styleName -> AssStyle` 映射。
3. 解析 Dialogue 的 `Style` 字段，找到默认样式。
4. 解析 Dialogue text 中的 override tags。
5. 删除 ASS 控制标签，生成最终 plain text。
6. 同步生成 `styleRuns` 区间。

第一阶段可以仅支持单条 Dialogue 的常用字段。多层嵌套、动画和 karaoke 可保留原样或忽略。

### 4. 扩展 cue JSON

切入点：

```text
/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/src/input/decoder.c
```

当前 `SubtitleCueBuildJson()` 已输出 text/segments/align/source。新增：

```text
"version":2
"styleRuns": [...]
"fontAttachments": [...]
"layout": {...}
```

事件链继续使用现有：

```text
decoder.c
  -> input_SendEventSubtitleCue()
  -> INPUT_EVENT_SUBTITLE_CUE
  -> libvlc_MediaPlayerSubtitleCue
  -> libvlcjni-mediaplayer.c
  -> Android/Kotlin
  -> Unity
```

不需要新增 libVLC event 类型。

## Skia Renderer 方案

新增 Android NDK 库：

```text
libxr_subtitle_skia.so
```

建议由 AAR 打包，并由 Unity 插件间接调用。

### 输入

```text
text
styleRuns
fontAttachments
targetWidthPx
maxHeightPx
deviceScale
defaultStyle
```

### 输出

```text
widthPx
heightPx
rgbaBuffer
baseline / contentRect
```

第一阶段推荐 CPU buffer：

```text
Skia RGBA buffer
  -> JNI DirectByteBuffer
  -> C# byte[] / NativeArray
  -> Texture2D.LoadRawTextureData()
  -> Texture2D.Apply()
```

字幕更新频率低，CPU buffer 足够稳定。后续如性能需要，再改为 AHardwareBuffer 或 OpenGL/Vulkan 外部纹理。

### 字体查找顺序

Skia 字体查找顺序：

1. cue `fontAttachments` 中 family 匹配的内嵌字体。
2. Android 系统字体。
3. 项目内置默认 CJK 字体。
4. 最终硬兜底字体。

如果 `fontFamily` 未命中内嵌字体，不阻塞渲染，交给系统 fallback。

### 填充和描边

渲染采用两遍绘制：

1. 描边 pass：`outlineColor` + stroke。
2. 填充 pass：`fillColor` + fill。

概念代码：

```cpp
SkPaint outlinePaint;
outlinePaint.setAntiAlias(true);
outlinePaint.setStyle(SkPaint::kStroke_Style);
outlinePaint.setStrokeWidth(outlineWidthPx * 2.0f);
outlinePaint.setColor(outlineColor);

SkPaint fillPaint;
fillPaint.setAntiAlias(true);
fillPaint.setStyle(SkPaint::kFill_Style);
fillPaint.setColor(fillColor);
```

Skia stroke 是沿 glyph 边界内外各一半，后续 fill pass 会覆盖内部一半。因此 `outlineWidthPx * 2.0f` 更接近 ASS 的外描边视觉。

如果使用 Skia Paragraph 后发现 per-run stroke/fill 不易组合，第一阶段可改用 `SkShaper + SkTextBlob`：

```text
styleRuns
  -> shape text to glyph runs
  -> build text blob per run
  -> canvas.drawTextBlob(blob, x, y, outlinePaint)
  -> canvas.drawTextBlob(blob, x, y, fillPaint)
```

该路径对描边/填充更直接，代价是需要自己处理更多换行和布局细节。Paragraph 适合布局，TextBlob 适合精确绘制；可以先用 Paragraph 计算布局，再在绘制层按 run 做两遍绘制。

### 纹理尺寸

建议由 Unity 传入目标宽度，Skia 自适应高度：

```text
targetWidthPx = 空间字幕面板宽度 * pixelsPerMeter
maxHeightPx = 空间字幕最大高度 * pixelsPerMeter
```

默认建议：

```text
targetWidthPx = 2048
maxHeightPx = 512
```

Skia 输出实际内容高度，Unity 根据 `width/height` 更新 Quad 宽高比。

## Android AAR / JNI 接口

Kotlin/Java 层建议暴露：

```kotlin
object XrSubtitleSkiaRenderer {
    fun renderCue(cueJson: String, widthPx: Int, maxHeightPx: Int): SubtitleBitmap
    fun clearFontCache()
}
```

Native 层接口：

```cpp
extern "C" JNIEXPORT jobject JNICALL
Java_org_videolan_vlc_bridge_XrSubtitleSkiaRenderer_nativeRenderCue(
    JNIEnv *env,
    jclass clazz,
    jstring cueJson,
    jint widthPx,
    jint maxHeightPx);
```

返回对象包含：

```text
width
height
stride
rgba DirectByteBuffer
```

如果后续使用 GPU texture，接口可以扩展 `textureId` 或 `hardwareBufferHandle`，不影响 cue 协议。

## Unity 接入方案

新增或改造以下组件：

```text
SubtitleCueParser
  解析 cue v2 JSON

SkiaSubtitleRendererBridge
  调用 Android AAR/JNI，得到 RGBA buffer

SpatialSubtitleTextureView
  管理 Texture2D、Material、Quad 尺寸

SpatialSubtitleService
  从 TMP 文本显示切换为纹理显示
```

Unity 材质要求：

- Unlit。
- Transparent。
- 不写深度或按现有 UI/字幕层策略配置。
- 纹理 alpha 正常参与混合。

运行时流程：

```text
OnSubtitleCue(cue)
  -> parse cue
  -> render texture via Skia bridge
  -> upload Texture2D
  -> show quad at subtitle anchor
  -> schedule hide by endMs
```

空间位置继续沿用现有 anchor 逻辑。视频已经改为 PICO Underlay 时，Unity 字幕纹理位于 eye buffer 中，可以显示在视频之上。

## 原生字幕和空间字幕切换

保留现有“原生字幕 / 空间字幕”模式。

| 模式 | VLC 字幕轨道 | VLC 原生字幕渲染 | subtitle cue | Unity 空间字幕 |
| --- | --- | --- | --- | --- |
| 原生字幕 | 开启 | 开启 | 可选 | 关闭 |
| 空间字幕 | 开启 | 尽量关闭或透明化 | 开启 | 开启 |
| 无字幕 | 关闭 | 关闭 | 关闭 | 关闭 |

后续需要确认 VLC 是否能只解码字幕但不把 subpicture 送入 vout。如果不能，空间字幕模式可先保留当前做法：Unity 显示空间字幕，同时通过 UI/设置隐藏原生字幕或将原生字幕轨道切换策略做成单独开关。

## 分阶段开发计划

### 阶段 1：Skia 纹理渲染最小闭环

- Android AAR 集成 `libxr_subtitle_skia.so`。
- Unity 调 JNI 渲染纯文本透明纹理。
- 支持默认填充色、描边色、描边宽度。
- 不处理 ASS 内嵌字体，先使用 Android 系统 fallback。

验收：

- 中文不显示方块。
- 填充色和描边色可配置。
- 空间位置由 Unity anchor 控制。

### 阶段 2：VLC cue v2 样式回传

- VLC 提取 ASS `Style:` 常用字段。
- cue JSON 增加 `version/styleRuns/layout`。
- Unity 解析 styleRuns 并传给 Skia。

验收：

- ASS `PrimaryColour` 能映射为填充色。
- ASS `OutlineColour` 能映射为描边色。
- ASS `Outline` 能映射为描边宽度。

### 阶段 3：内嵌字体导出

- VLC 导出 `.ttf/.otf/.ttc` attachment 到 app 私有目录。
- cue JSON 增加 `fontAttachments`。
- Skia 优先加载内嵌字体。

验收：

- 带内嵌字体的 MKV/ASS 字幕能优先使用内嵌字体。
- 字体缓存可复用，切换视频时不会无限堆积。

### 阶段 4：Dialogue inline override

- 支持 `\fn`、`\b`、`\i`、`\c`、`\1c`、`\3c`、`\bord`。
- 生成多段 `styleRuns`。

验收：

- 同一句字幕中不同颜色/字体/描边能分段生效。

### 阶段 5：复杂 ASS 兜底策略

- 对复杂 ASS 特效判断是否转 libass bitmap fallback。
- 维持空间字幕主链路为 Skia texture。

验收：

- 常规字幕走 Skia。
- 复杂特效不会导致崩溃或空白，可退到原生/libass bitmap。

## 风险和处理

| 风险 | 影响 | 处理 |
| --- | --- | --- |
| ASS parser 覆盖不完整 | 部分复杂字幕样式不准 | 阶段化支持常用字段，复杂特效走 fallback。 |
| Skia Paragraph 对描边控制不够直接 | 描边/填充难以按 run 精确绘制 | 使用 SkShaper + SkTextBlob 绘制层两遍 pass。 |
| 内嵌字体 family 名称匹配失败 | 无法优先使用附件字体 | 同时按 family、文件名、PostScript name 建索引；失败时系统 fallback。 |
| TTC face index 不准 | CJK 字体加载错误 | Skia 加载 TTC 时记录 index；必要时扫描 family name。 |
| CPU buffer 上传开销 | 高频字幕可能卡顿 | 字幕变更时才渲染；后续升级 GPU texture。 |
| 原生字幕和空间字幕重复显示 | 用户看到两套字幕 | 模式切换时明确控制 VLC 字幕显示和 Unity 字幕显示。 |
| cue JSON 过大 | JNI/Unity 解析成本增加 | 字体附件只回传路径和元信息，不内联字体二进制。 |

## 验证方式

自动化可做：

- C/C++ parser 单元测试：ASS 颜色、Style 行、Dialogue override。
- Kotlin/JNI 单元测试：cue JSON 解析、Skia renderer 返回非空 bitmap。
- Unity 静态检查：组件引用、JSON 兼容解析、纹理尺寸计算。

人工验证由 PICO 设备完成：

- 中文字幕是否不再显示方块。
- 填充色、描边色是否正确。
- 字幕是否悬浮在固定空间位置。
- Underlay 视频是否不遮挡字幕。
- 原生字幕/空间字幕/无字幕模式切换是否符合预期。

## 后续落地顺序

推荐先开发阶段 1 和阶段 2。这样即使暂时没有内嵌字体导出，也能先证明 Skia 纹理字幕可以替代 TMP，并验证填充色/描边色链路。内嵌字体导出属于第二个稳定性增强点，放到 Skia 闭环之后做更稳。
