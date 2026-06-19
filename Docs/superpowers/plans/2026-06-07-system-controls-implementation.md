# UI System Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add working brightness, volume, time, and battery behavior to the existing playback UI without moving the current buttons.

**Architecture:** Keep the change inside `VRUIManager` and follow its existing secondary-popup pattern. Create one reusable system slider popup that can bind to brightness or volume, position it above the clicked button, and route value changes to Android system APIs on device with simulation fallback elsewhere.

**Tech Stack:** Unity C#, Unity UI `Button`/`Slider`, TextMeshPro, Android `AudioManager`, Android window attributes, Android battery broadcast.

---

### Task 1: Add source-level regression expectations

**Files:**
- Modify: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`

- [x] **Step 1: Add a source-level test for system controls**

Add a test that checks `VRUIManager.cs` contains bindings and helper methods for brightness, volume, and battery:

```csharp
[Test]
public void VRUIManager_BindsSystemControlsAndStatus()
{
    string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

    StringAssert.Contains("brightnessBtn.onClick.AddListener(OnBrightnessBtnClicked)", source);
    StringAssert.Contains("volumeBtn.onClick.AddListener(OnVolumeBtnClicked)", source);
    StringAssert.Contains("UpdateSystemStatus()", source);
    StringAssert.Contains("ShowSystemSliderAboveButton", source);
    StringAssert.Contains("ReadBatteryPercent", source);
    StringAssert.Contains("SetSystemVolumeNormalized", source);
    StringAssert.Contains("SetScreenBrightnessNormalized", source);
}
```

- [x] **Step 2: Do not run Unity Editor tests**

Per user request, skip local Unity Editor test execution. Verify later with static source checks instead.

### Task 2: Implement slider popups and Android-backed values

**Files:**
- Modify: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`

- [x] **Step 1: Add state fields**

Add fields for the system slider popup, active mode, simulated fallback values, and status refresh cadence.

- [x] **Step 2: Bind buttons**

In `Start()`, bind `brightnessBtn` to `OnBrightnessBtnClicked` and `volumeBtn` to `OnVolumeBtnClicked`.

- [x] **Step 3: Refresh status**

Keep `systemTimeText` updated as `HH:mm`. Refresh `batteryText` periodically via `UpdateSystemStatus()` so battery reads do not happen every frame.

- [x] **Step 4: Create shared slider popup**

Create a small `GameObject` popup with `Image`, `Canvas`, and `Slider`, styled similarly to the existing progress slider. Use `PositionPopupAboveButton(systemSliderPopup, anchorButton, true)` to show it above the clicked top-panel button.

- [x] **Step 5: Route slider changes**

When active mode is brightness, slider changes call `SetScreenBrightnessNormalized(value)`. When active mode is volume, slider changes call `SetSystemVolumeNormalized(value)`.

- [x] **Step 6: Include popup in close logic**

Update `CloseSecondaryPopups()` and `RegisterUiTreeNodes()` so the system slider behaves like the existing secondary popups.

### Task 3: Verify without local Editor tests

**Files:**
- Inspect: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`
- Inspect: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`

- [x] **Step 1: Run static checks**

Run:

```bash
rg -n "OnBrightnessBtnClicked|OnVolumeBtnClicked|ShowSystemSliderAboveButton|ReadBatteryPercent|SetSystemVolumeNormalized|SetScreenBrightnessNormalized" Assets/Scripts/UI/PlaybackControls/VRUIManager.cs Assets/Tests/Editor/VRUIManagerIconStyleTests.cs
```

Expected: All symbols appear in the implementation and the source-level test.

- [x] **Step 2: Check for obvious compile hazards**

Run:

```bash
rg -n "TODO|TBD|FIXME|nameof\\([^)]*$" Assets/Scripts/UI/PlaybackControls/VRUIManager.cs Assets/Tests/Editor/VRUIManagerIconStyleTests.cs
```

Expected: No placeholder or malformed `nameof` matches.
