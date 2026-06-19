# UI Playlist Track URI Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make playlist and track popups readable and XR-hover friendly, and stop encoded URI strings from being saved as media library history names.

**Architecture:** Keep the existing Unity UI objects and `TMP_Dropdown` flow. Add focused runtime styling helpers in `VRUIManager`, playlist presentation helpers in `PlaylistPanelController`, and URI normalization at the Unity playback payload parser boundary so downstream history/playback uses a decoded URI consistently.

**Tech Stack:** Unity C#, TextMeshPro, Unity UI, XR Interaction Toolkit UI raycasters, NUnit EditMode tests, Kotlin Android VLC bridge source checks.

---

### Task 1: Playlist Readability, Width, Hover, And Reticle

**Files:**
- Modify: `Assets/Tests/Editor/PlaylistPanelSceneTests.cs`
- Modify: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`
- Modify: `Assets/Scripts/UI/Playlist/PlaylistPanelController.cs`
- Modify: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`

- [ ] **Step 1: Write failing tests**

Add tests that assert playlist source contains white opaque text styling, ellipsis/no-wrap title setup, full-title hover tooltip behavior, selected current item highlighting, wider panel sizing, and `OnPlaylistToggleBtnClicked` calls `BringPopupToFront(playlistPanel.panelRoot)`.

- [ ] **Step 2: Run focused EditMode tests and verify RED**

Run Unity EditMode tests for `PlaylistPanelSceneTests` and `VRUIManagerIconStyleTests`.
Expected: new playlist/UI assertions fail before implementation.

- [ ] **Step 3: Implement playlist UI behavior**

In `PlaylistPanelController`, add constants for panel width, item height, selected color, and `OpaqueTextColor = Color.white`. During `Show`/`Rebuild`, style the panel and item rows, set labels to white opaque, no-wrap, ellipsis, and create a small tooltip label/panel that appears on pointer hover with the full title. In `Highlight`, set the current item from `isCurrent` or `_currentIndex`.

- [ ] **Step 4: Implement playlist reticle path**

In `VRUIManager.OnPlaylistToggleBtnClicked`, call `BringPopupToFront(playlistPanel.panelRoot)` before positioning/showing so the panel receives `TrackedDeviceGraphicRaycaster`.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run Unity EditMode tests for `PlaylistPanelSceneTests` and `VRUIManagerIconStyleTests`.
Expected: playlist/UI tests pass.

### Task 2: Subtitle And Audio Track Popup Styling

**Files:**
- Modify: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`
- Modify: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`

- [ ] **Step 1: Write failing tests**

Add tests that assert secondary dropdowns hide collapsed caption/arrow display, style template and runtime list text with opaque white, set larger item height/font size, apply selected item highlight, and call the runtime styling helper after `dropdown.Show()`.

- [ ] **Step 2: Run focused EditMode tests and verify RED**

Run Unity EditMode tests for `VRUIManagerIconStyleTests`.
Expected: new track dropdown styling assertions fail before implementation.

- [ ] **Step 3: Implement dropdown styling helpers**

Extend `StyleSecondaryDropdown` to hide caption/arrow display, make root background transparent, keep template background opaque, and style all template labels as white opaque text. Add a runtime `StyleOpenDropdownList` helper that finds `Dropdown List`, applies `BringPopupToFront`, sets item layout height, label font size/color, transparent non-selected item backgrounds, hover colors, and selected item background.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run Unity EditMode tests for `VRUIManagerIconStyleTests`.
Expected: track dropdown styling tests pass.

### Task 3: Subtitle Track Display Names

**Files:**
- Modify: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`
- Modify: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`

- [ ] **Step 1: Write failing test**

Add a reflection test for `GetSubtitleTrackDisplayName`, expecting `/storage/video/Movie.zh.ass` to display `zh.ass` and names without a dot to remain unchanged.

- [ ] **Step 2: Run focused EditMode tests and verify RED**

Run Unity EditMode tests for `VRUIManagerIconStyleTests`.
Expected: helper does not exist or returns the original filename.

- [ ] **Step 3: Implement subtitle display helper**

Add `GetSubtitleTrackDisplayName(TrackInfo track)` and use it in `UpdateSubtitleTracksDropdown`; audio options continue using `track.Name`.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run Unity EditMode tests for `VRUIManagerIconStyleTests`.
Expected: subtitle display-name tests pass.

### Task 4: URI Normalization Across Unity And VLC

**Files:**
- Modify: `Assets/Tests/Editor/VlcPlaybackPayloadParserTests.cs`
- Modify: `Assets/Scripts/Infrastructure/VlcBridge/Playback/Parsing/VlcPlaybackPayloadParser.cs`

- [ ] **Step 1: Write failing tests**

Add tests that parse `{"uri":"file:///sdcard/Movies/%E4%BD%A0%E5%A5%BD%20Movie.mp4"}` and expect `MediaWrapper.Uri`, `MediaWrapper.Id`, `RawJson`, and the `lastTimeProvider` lookup key to use `file:///sdcard/Movies/你好 Movie.mp4`.

- [ ] **Step 2: Run focused EditMode tests and verify RED**

Run Unity EditMode tests for `VlcPlaybackPayloadParserTests`.
Expected: URI remains encoded before implementation.

- [ ] **Step 3: Implement parser boundary normalization**

Add a safe single-pass URI decode helper in `VlcPlaybackPayloadParser`. Use the decoded URI for `Id`, `Uri`, `lastTimeProvider`, and rewrite `RawJson` by replacing `dto.uri` with the decoded URI through the parsed DTO before `JsonUtility.ToJson`.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run Unity EditMode tests for `VlcPlaybackPayloadParserTests`.
Expected: parser URI tests pass.

### Task 5: Regression Sweep

**Files:**
- No additional files.

- [ ] **Step 1: Run focused UI/parser tests**

Run Unity EditMode tests for `PlaylistPanelSceneTests`, `VRUIManagerIconStyleTests`, and `VlcPlaybackPayloadParserTests`.
Expected: all focused tests pass.

- [ ] **Step 2: Run full EditMode suite**

Run all Unity EditMode tests.
Expected: suite passes with zero failures.

- [ ] **Step 3: Review changed files**

Inspect modified source and tests. Confirm no unrelated files were reverted and no generated metadata churn was introduced.
