# UI Hover Auto-Hide Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent the playback UI from auto-hiding while a controller ray is hovering the panel or any secondary popup.

**Architecture:** Keep the existing `VRUIManager` 3 second timer. Add a small hover gate that asks the active Unity `EventSystem` for pointer UI state before decrementing the hide timer.

**Tech Stack:** Unity 6000, C#, Unity UI, XR Interaction Toolkit UI, NUnit EditMode tests.

---

### Task 1: Add Failing Auto-Hide Hover Test

**Files:**
- Modify: `Assets/Tests/Editor/VRUIManagerIconStyleTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void VRUIManager_AutoHideTimerWaitsWhilePointerIsOverUi()
{
    string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/PlaybackControls/VRUIManager.cs"));

    StringAssert.Contains("IsPointerOverManagedUi()", source);
    StringAssert.Contains("if (IsPointerOverManagedUi())", source);
    StringAssert.Contains("return;", source);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `Unity EditMode VRUIManager_AutoHideTimerWaitsWhilePointerIsOverUi`

Expected: FAIL because `IsPointerOverManagedUi` does not exist yet.

### Task 2: Implement Hover Gate

**Files:**
- Modify: `Assets/Scripts/UI/PlaybackControls/VRUIManager.cs`

- [ ] **Step 1: Add EventSystem namespace**

```csharp
using UnityEngine.EventSystems;
```

- [ ] **Step 2: Gate the timer**

```csharp
if (_autoHidePending)
{
    if (IsPointerOverManagedUi())
        return;

    _hideTimer -= Time.deltaTime;
    if (_hideTimer <= 0f)
    {
        _autoHidePending = false;
        SetPanelVisibility(false);
    }
}
```

- [ ] **Step 3: Add managed UI detection**

```csharp
private bool IsPointerOverManagedUi()
{
    EventSystem eventSystem = EventSystem.current;
    if (eventSystem == null || !eventSystem.IsPointerOverGameObject())
        return false;

    GameObject selected = eventSystem.currentSelectedGameObject;
    return selected == null || IsManagedUiObject(selected);
}
```

- [ ] **Step 4: Add UI root matching helper**

```csharp
private bool IsManagedUiObject(GameObject target)
{
    if (target == null) return false;

    return IsSelfOrChildOf(target, controlPanel)
        || IsSelfOrChildOf(target, topRightGroup)
        || IsSelfOrChildOf(target, exitBtn != null ? exitBtn.gameObject : null)
        || IsSelfOrChildOf(target, ActiveDropdownList(audioTrackDropdown))
        || IsSelfOrChildOf(target, ActiveDropdownList(subtitleTrackDropdown))
        || IsSelfOrChildOf(target, settingsMenu)
        || IsSelfOrChildOf(target, geometryMenu)
        || IsSelfOrChildOf(target, shortcutConfigPanel != null ? shortcutConfigPanel.gameObject : null)
        || IsSelfOrChildOf(target, playlistPanel != null ? playlistPanel.panelRoot : null);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run the focused EditMode test class and confirm it passes.
