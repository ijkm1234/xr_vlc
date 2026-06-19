# Spatial Subtitle Skia Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first development slice of spatial subtitles using cue v2 data and a Skia renderer bridge, while keeping VLC playback and Unity spatial placement boundaries intact.

**Architecture:** VLC emits richer subtitle cue JSON. Unity parses cue v2 into domain models and can route rendering through a texture renderer bridge. Android exposes a renderer facade that is API-compatible with a native Skia implementation; until Skia binary integration lands, the facade has a deterministic Android Canvas fallback so the bridge is testable and does not block Unity integration.

**Tech Stack:** VLC C, Kotlin/Android AAR, Unity C#, NUnit editor tests, Robolectric/JUnit unit tests, future Android NDK Skia.

---

## File Structure

- Modify `/Users/admin/xr_vlc/Assets/Scripts/Domain/Media/SubtitleCue.cs`
  - Add cue v2 style, layout, and font attachment DTOs.
  - Preserve existing cue v1 fields and behavior.
- Modify `/Users/admin/xr_vlc/Assets/Scripts/Infrastructure/VlcBridge/Playback/Parsing/VlcPlaybackPayloadParser.cs`
  - Keep parsing through `JsonUtility`, then normalize cue v2 arrays.
- Add `/Users/admin/xr_vlc/Assets/Scripts/Infrastructure/VlcBridge/Playback/SubtitleRendering/SubtitleBitmap.cs`
  - Unity-side immutable render result.
- Add `/Users/admin/xr_vlc/Assets/Scripts/Infrastructure/VlcBridge/Playback/SubtitleRendering/ISubtitleTextureRenderer.cs`
  - Renderer abstraction for spatial subtitle service.
- Add `/Users/admin/xr_vlc/Assets/Scripts/Infrastructure/VlcBridge/Playback/SubtitleRendering/AndroidSkiaSubtitleRenderer.cs`
  - Unity Android bridge wrapper. Calls Android AAR facade when running on Android; returns empty result in editor.
- Modify `/Users/admin/xr_vlc/Assets/Scripts/Services/Playback/SpatialSubtitleService.cs`
  - Keep current TMP fallback; add optional texture renderer route without breaking current visible subtitle behavior.
- Modify `/Users/admin/xr_vlc/Assets/Tests/Editor/VlcPlaybackPayloadParserTests.cs`
  - Add cue v2 parser tests first.
- Add `/Users/admin/xr_vlc/vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/subtitle/SubtitleRenderModels.kt`
  - Kotlin models for renderer input/output.
- Add `/Users/admin/xr_vlc/vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/subtitle/XrSubtitleSkiaRenderer.kt`
  - Android renderer facade; exposes `renderCue` and native hook boundary.
- Add `/Users/admin/xr_vlc/vlc-android/application/vlc-android/test/org/videolan/vlc/bridge/subtitle/XrSubtitleSkiaRendererTest.kt`
  - Robolectric tests for transparent bitmap output and outline/fill parameters.

## Task 1: Unity Cue v2 DTOs

**Files:**
- Modify: `/Users/admin/xr_vlc/Assets/Scripts/Domain/Media/SubtitleCue.cs`
- Modify: `/Users/admin/xr_vlc/Assets/Tests/Editor/VlcPlaybackPayloadParserTests.cs`

- [ ] **Step 1: Write failing parser test**

Add an NUnit test that parses cue v2 with one style run, one font attachment, and layout data:

```csharp
[Test]
public void ParseSubtitleCuePayload_ParsesCueV2StyleRunsAndFontAttachments()
{
    var cue = VlcPlaybackPayloadParser.ParseSubtitleCuePayload(
        "{\"version\":2,\"seq\":21,\"trackId\":\"spu:3\",\"startMs\":1000,\"endMs\":2500,\"text\":\"你好 Hello\",\"source\":\"text\",\"styleRuns\":[{\"start\":0,\"end\":8,\"fontFamily\":\"Noto Sans CJK\",\"fontSize\":48,\"bold\":true,\"italic\":false,\"fillColor\":\"#FFFFFFFF\",\"outlineColor\":\"#FF000000\",\"outlineWidth\":3}],\"fontAttachments\":[{\"name\":\"MovieFont.ttf\",\"family\":\"Movie Font\",\"mime\":\"application/x-truetype-font\",\"cachePath\":\"/data/data/pkg/files/subtitle_fonts/font.ttf\",\"size\":1234,\"sha256\":\"abc\"}],\"layout\":{\"align\":\"bottom-center\",\"marginL\":1,\"marginR\":2,\"marginV\":48}}");

    Assert.AreEqual(2, cue.version);
    Assert.AreEqual(1, cue.styleRuns.Length);
    Assert.AreEqual("Noto Sans CJK", cue.styleRuns[0].fontFamily);
    Assert.AreEqual("#FFFFFFFF", cue.styleRuns[0].fillColor);
    Assert.AreEqual("#FF000000", cue.styleRuns[0].outlineColor);
    Assert.AreEqual(3f, cue.styleRuns[0].outlineWidth);
    Assert.AreEqual(1, cue.fontAttachments.Length);
    Assert.AreEqual("MovieFont.ttf", cue.fontAttachments[0].name);
    Assert.AreEqual("bottom-center", cue.layout.align);
}
```

- [ ] **Step 2: Run red test**

Run the Unity editor test for `VlcPlaybackPayloadParserTests`. Expected: compile fail or assertion fail because cue v2 fields do not exist yet.

- [ ] **Step 3: Implement DTOs**

Add serializable cue v2 classes to `SubtitleCue.cs` and normalize null arrays/strings.

- [ ] **Step 4: Run green parser test**

Run the same Unity editor test. Expected: the new cue v2 parser test passes. If Unity test execution is blocked by the user constraint or an open editor, run static compile checks only and record the limitation.

## Task 2: Android Renderer Facade

**Files:**
- Add: `/Users/admin/xr_vlc/vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/subtitle/SubtitleRenderModels.kt`
- Add: `/Users/admin/xr_vlc/vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/subtitle/XrSubtitleSkiaRenderer.kt`
- Add: `/Users/admin/xr_vlc/vlc-android/application/vlc-android/test/org/videolan/vlc/bridge/subtitle/XrSubtitleSkiaRendererTest.kt`

- [ ] **Step 1: Write failing Android unit test**

Test that `renderCue` returns a non-empty transparent PNG-like bitmap payload for a cue with fill and outline colors.

- [ ] **Step 2: Run red test**

Run:

```bash
cd /Users/admin/xr_vlc/vlc-android
./gradlew :application:vlc-android:testDebugUnitTest --tests org.videolan.vlc.bridge.subtitle.XrSubtitleSkiaRendererTest
```

Expected: fail because renderer classes do not exist.

- [ ] **Step 3: Implement renderer facade**

Implement `XrSubtitleSkiaRenderer.renderCue(cueJson, widthPx, maxHeightPx)`. The first implementation may use Android Canvas internally but must expose the future Skia-compatible API and keep all style values in the request model.

- [ ] **Step 4: Run green Android unit test**

Run the same Gradle unit test. Expected: pass. This is not APK packaging.

## Task 3: Unity Renderer Bridge

**Files:**
- Add: `/Users/admin/xr_vlc/Assets/Scripts/Infrastructure/VlcBridge/Playback/SubtitleRendering/SubtitleBitmap.cs`
- Add: `/Users/admin/xr_vlc/Assets/Scripts/Infrastructure/VlcBridge/Playback/SubtitleRendering/ISubtitleTextureRenderer.cs`
- Add: `/Users/admin/xr_vlc/Assets/Scripts/Infrastructure/VlcBridge/Playback/SubtitleRendering/AndroidSkiaSubtitleRenderer.cs`
- Modify: `/Users/admin/xr_vlc/Assets/Scripts/Services/Playback/SpatialSubtitleService.cs`

- [ ] **Step 1: Write editor tests for bridge-safe behavior**

Add tests that instantiate the renderer in editor and verify it returns an empty result instead of throwing.

- [ ] **Step 2: Run red test**

Expected: fail because bridge classes do not exist.

- [ ] **Step 3: Implement bridge**

Use `AndroidJavaClass("org.videolan.vlc.bridge.subtitle.XrSubtitleSkiaRenderer")` on Android. In editor and unsupported platforms, return `SubtitleBitmap.Empty`.

- [ ] **Step 4: Wire optional renderer route**

`SpatialSubtitleService` should keep existing TMP rendering as fallback. When a renderer returns a valid bitmap, display a texture view; if not, continue showing TMP text.

## Task 4: VLC Cue v2 Skeleton

**Files:**
- Modify: `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/include/vlc_subpicture.h`
- Modify: `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/src/misc/subpicture.c`
- Modify: `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/modules/codec/libass.c`
- Modify: `/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc/src/input/decoder.c`

- [ ] **Step 1: Add static/unit-level parser tests if available**

If VLC test harness is practical, add tests for ASS color conversion and style JSON. If not practical in this workspace, keep extraction helpers small and validate by static compile.

- [ ] **Step 2: Add new subpicture fields**

Add:

```c
char *psz_subtitle_style_json;
char *psz_subtitle_font_attachments_json;
char *psz_subtitle_layout_json;
```

- [ ] **Step 3: Extend JSON builder**

Emit `version:2`, `styleRuns`, `fontAttachments`, and `layout` when fields are present. Preserve v1-compatible fields.

- [ ] **Step 4: Generate VLC patch**

After native changes are stable, update the relevant patch under `/Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/patches`.

## Self-Review

- Spec coverage: phase 1 renderer bridge, cue v2 parsing, Android render facade, and VLC cue extension are covered. Full Skia binary/NDK integration is deferred behind the renderer facade because no Skia dependency is currently configured in the Android module.
- Placeholder scan: no TBD/TODO placeholders remain in task definitions.
- Type consistency: Unity names use `SubtitleCueStyleRun`, `SubtitleCueFontAttachment`, `SubtitleCueLayout`, and Android names use `SubtitleRenderRequest` / `SubtitleRenderResult`.
