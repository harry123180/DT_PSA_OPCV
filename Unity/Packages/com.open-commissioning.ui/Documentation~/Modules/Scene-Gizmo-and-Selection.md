# Scene Gizmo and extended selection

## Purpose

Optional viewport helpers: orientation **scene gizmo**, **box selection**, and debug **box drawing** for commissioning views.

## Scene Gizmo

**Namespace:** `OC.UI.Interactions.SceneGizmo`

| Type | Script | Role |
|------|--------|------|
| `SceneGizmoController` | `SceneGizmoController.cs` | Input and orientation logic |
| `SceneGizmoRenderer` | `SceneGizmoRenderer.cs` | Renders the gizmo mesh |

**Prefabs:**

- `Runtime/Interactions/Prefabs/SceneGizmo/SceneGizmoController.prefab`
- `Runtime/Interactions/Prefabs/SceneGizmo/SceneGizmoRenderer.prefab`

**Resources:** materials and shader under `Runtime/Interactions/Resources/` (SceneGizmo).

Add to the scene when you need a Unity-editor-style view axis widget for camera orientation.

## Box selection

**`BoxSelector`** — [`Runtime/Interactions/Scripts/BoxSelector.cs`](../../Runtime/Interactions/Scripts/BoxSelector.cs)

Drag-select multiple objects in the view (namespace `OC.UI.Interactions.Scripts`). Works alongside **`SelectionManager`** for multi-select workflows.

## Box drawer

**`BoxDrawer`** — [`Runtime/Interactions/Scripts/BoxDrawer.cs`](../../Runtime/Interactions/Scripts/BoxDrawer.cs)

Debug visualization for selection or volume boxes in the scene view.

## Tooltip system

Related pointer feedback (not gizmo-specific):

| Type | Role |
|------|------|
| `Tooltip` / `ITooltip` | Tooltip data |
| `TooltipEntity` / `TooltipDevice` | Specialized tooltip sources |
| `TooltipManager` | Shows tooltips for hovered targets |

**`TooltipManager`** is a singleton that displays tooltip UI when interacting with annotated objects.

## Setup

1. Instantiate Scene Gizmo prefabs parented to the camera or UI root as designed in your scene.
2. Add **`BoxSelector`** / **`BoxDrawer`** if multi-select or debug regions are required.
3. Configure **`Tooltip`** components on devices and ensure **`TooltipManager`** exists in the scene.

## Related

- [Selection Manager](../Subsystems/Selection-Manager.md)
- [Interaction](../Components/Interaction.md)
- [Virtual Camera](../Components/Virtual-Camera.md)
