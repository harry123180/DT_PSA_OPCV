# Toolbar tools

## Purpose

Document each tool on the **AppUI** subsystem toolbar: toggles and slide-out windows that control selection, visualization, cameras, macros, settings, layout, and console.

## Toolbar framework

| Type | Base class | Behavior |
|------|------------|----------|
| Toggle button | [`ToolbarItem`](../../Runtime/System/Scripts/Toolbar/ToolbarItem.cs) | Adds a `Toggle` to the bar; fires **`OnToggleChanged(bool)`** |
| Tool window | [`ToolbarWindow`](../../Runtime/System/Scripts/Toolbar/ToolbarWindow.cs) | Toggle opens a **`SubsystemPanel`** with custom content |

**Host:** [`ToolWindowsManager`](../../Runtime/System/Scripts/ToolWindowsManager.cs) on AppUI prefab.

## Tools reference

### Interaction Tool

| | |
|--|--|
| **Prefab name** | Interaction Tool |
| **Type** | `ToolbarItem` |
| **Effect** | Sets **`SelectionManager.Enable`** |

Turn on to allow picking commissioning objects. Off by default in the shipped **Interactions** prefab.

### Collider View Tool

| | |
|--|--|
| **Prefab name** | Collider View Tool |
| **Type** | `ToolbarItem` + scene singleton |
| **Effect** | **`ColliderViewSystem.Show(bool)`** |

Requires **`ColliderViewSystem`** in the scene (add to Interactions root or a manager object). Toggles debug visualization on all **`ColliderMaterial`** components (semi-transparent collider meshes).

> **Note:** Wire UnityEvents to `OC.UI.Interactions.ColliderViewSystem`, not legacy `PlanX.UI` type names if you reconfigure the prefab.

### Label Tool

| | |
|--|--|
| **Prefab name** | Label Tool |
| **Type** | `ToolbarItem` + **`LabelManager`** |
| **Effect** | **`LabelManager.Show(bool)`** |

Shows or hides all **`Label`** components (world-space TextMeshPro names).

Add **`LabelManager`** to the scene if not present. Wire events to `OC.UI.Interactions.LabelManager`.

### Hide Tool

| | |
|--|--|
| **Prefab name** | Hide Tool |
| **Type** | [`HideToolWindow`](../../Runtime/System/Scripts/Toolbar/HideToolWindow.cs) |
| **Effect** | Per-**`HideGroup`** transparency toggles |

Panel lists all **`HideGroup`** in the scene (sorted by name) with icon toggles. Buttons: **Hide all**, **Show all**. Sets **`HideGroup.Transparent`** for fade/hide visual modes.

### Cameras

| | |
|--|--|
| **Prefab name** | Cameras |
| **Type** | [`CameraToolWindow`](../../Runtime/System/Scripts/Toolbar/CameraToolWindow.cs) |
| **Effect** | Switch active **`CameraController`** |

Builds one slide toggle per registered virtual camera via **`CamerasManager`**.

### Macros

| | |
|--|--|
| **Prefab name** | Macros |
| **Type** | [`MacroToolWindow`](../../Runtime/System/Scripts/Toolbar/MacroToolWindow.cs) |
| **Effect** | User-defined **`UnityEvent`** buttons |

Configure **`_macros`** list in the Inspector: name + `OnClick` for project-specific actions.

### Settings

| | |
|--|--|
| **Prefab name** | Settings |
| **Type** | [`SettingsWindow`](../../Runtime/System/Scripts/Toolbar/SettingsWindow.cs) |
| **Effect** | Mouse sensitivity, visual config |

Drives **`SettingsManager`** (mouse sensitivity 1–10, **`VisualConfig`**, **`AutoLoadSave`** for layout).

### Save / Load

| | |
|--|--|
| **Prefab name** | SaveLoad |
| **Type** | [`SaveLoadPanel`](../../Runtime/System/Scripts/Toolbar/SaveLoadWindow.cs) |
| **Effect** | Component layout XML save/load |

Uses **`LayoutSaveSystem`** — see [Modules/Component-Layout.md](../Modules/Component-Layout.md).

### Console

| | |
|--|--|
| **Prefab name** | Console |
| **Type** | [`ConsoleItem`](../../Runtime/System/Scripts/Toolbar/ConsoleItem.cs) |
| **Effect** | Runtime debug console |

Command line UI backed by **`DebugLogConsole`** and **`[ConsoleMethod]`** attributes. See [Modules/Console.md](../Modules/Console.md).

## Editor tools (separate bar)

Not part of the subsystem toolbar strip — **`EditorToolsPanel`**:

| Control | Maps to |
|---------|---------|
| View | `ToolType.View` |
| Move | `ToolType.Move` |
| Rotate | `ToolType.Rotation` |
| Center (toggle) | `PivotMode.Center` vs `Pivot` |
| Global (toggle) | `CoordinateSpace.World` vs `Local` |

Visible only when at least one interaction is selected.

## Setup checklist

- [ ] AppUI / Interactions prefab in scene
- [ ] **Interaction Tool** wired to `SelectionManager`
- [ ] **LabelManager** and **ColliderViewSystem** present if using those tools
- [ ] **HideGroup** components on geometry groups for Hide Tool
- [ ] Virtual cameras in scene for Cameras tool
- [ ] **LayoutSaveSystem** for Save/Load

## Related

- [AppUI Overview](Overview.md)
- [Selection Manager](../Subsystems/Selection-Manager.md)
- [Cameras Manager](../Subsystems/Cameras-Manager.md)
- [Scene Setup](../Scene-Setup.md)
