# Settings Tab Panel Implementation Plan

## Objective

Replace the current small playback settings popup with a tabbed settings panel above the playback controls. The panel must hide when the user clicks outside it, and must consolidate shortcut configuration and playback-related VLC-backed settings.

## Scope

- Add tabs: `播放`, `手势`, `字幕`, `视频`, `音频`.
- Move all `ShortcutConfigPanel` controls into the new `手势` tab.
- Move subtitle render mode into the `字幕` tab with exactly: `原生`, `空间`, `关闭`.
- Keep geometry/projection/stereo/curved-screen controls in the existing 3D menu, not in the settings panel.
- Put video stretch/crop display settings into the `视频` tab using VLC's existing `video_ratio` setting where available.
- Put audio channel mode into the `音频` tab: `立体声`, `混合单声道`.
- Read/write existing VLC settings through the SharedPreferences bridge when VLC already has a key.

## Files

- `Assets/Scripts/UI/Settings/SettingsMenuController.cs`
- `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`
- `Assets/Scripts/Services/Settings/PlaybackUiSettingsService.cs`
- `Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs`
- `vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt`
- `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`
- `Docs/superpowers/specs/2026-06-07-settings-tab-panel-design.md`

## Test First

1. Add source-level tests that require the new settings panel to expose the five tabs and exclude geometry labels from the video tab.
2. Add tests that require `PlaybackUiSettingsService` to use VLC's existing `video_ratio` key and the existing `xr_button_mappings` key.
3. Add tests that require Unity bridge methods `SetVideoScaleOrdinal` and `SetAudioChannelMode`.
4. Add tests that require Android bridge methods `setVideoScale` and `setAudioChannelMode`.

## Implementation Steps

1. Create `PlaybackUiSettingsService`.
   - Use existing VLC keys:
     - `video_ratio` for video scale/crop.
     - `xr_button_mappings` remains owned by `ShortcutSettingsService`.
   - Do not persist subtitle render mode or audio channel mode.
   - Clamp invalid video values to safe defaults.

2. Implement `SettingsMenuController`.
   - Dynamically builds a compact tab bar and content region.
   - `播放`: playback speed shortcuts.
   - `手势`: four shortcut dropdowns, migrated from `ShortcutConfigPanel`.
   - `字幕`: render mode buttons for native/spatial/off.
   - `视频`: VLC `video_ratio` options for fit/fill/crop/aspect display behavior.
   - `音频`: stereo/mono channel mode buttons.
   - Persists only video scale through `PlaybackUiSettingsService`; gesture mappings keep using the existing shortcut service.
   - Applies runtime changes through `PlaybackService` and `VlcPlaybackBridge`.

3. Integrate `VRUIManager`.
   - `settingsBtn` toggles the new panel.
   - Position panel above the playback settings button.
   - Register panel as XR UI popup.
   - Hide panel when trigger/click target is outside the panel.
   - Stop opening the legacy shortcut modal from settings.

4. Extend Android bridge.
   - `setVideoScale(scaleOrdinal)` writes VLC `VIDEO_RATIO` and applies `MediaPlayer.ScaleType`.
   - `setAudioChannelMode(mode)` updates runtime mode only and does not write SharedPreferences.
   - Runtime downmix depends on future LibVLC option/application support because no existing VLC mono-downmix preference was found.

5. Verify.
   - Run available EditMode tests.
   - Run targeted source checks with `rg`.
   - Check C# compile diagnostics when Unity tooling is available.
