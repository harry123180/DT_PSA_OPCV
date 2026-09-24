# Transform Handles module

## Purpose

Display move/rotate gizmos for selected objects with **`RuntimeInspector`**, integrated with selection, undo, and the editor tools toolbar.

## RuntimeTransformHandle

**Script:** [`Runtime/Interactions/Scripts/TransformHandles/Scripts/RuntimeTransformHandle.cs`](../../Runtime/Interactions/Scripts/TransformHandles/Scripts/RuntimeTransformHandle.cs)

**Prefab:** Nested **TransformHandles** under [`Interactions.prefab`](../../Runtime/Prefabs/Interactions.prefab) (from `TransformHandles.prefab`).

### Properties

| Member | Description |
|--------|-------------|
| `Tool` | `View`, `Move`, or `Rotation` |
| `Pivot` | `Pivot` vs `Center` |
| `Coordinate` | `Local` vs `World` |
| `Targets` | List of `RuntimeInspector` being manipulated |

### Selection link

Subscribes to **`SelectionManager.OnSelectionChanged`** and updates targets/handle visibility when selection changes.

### Input

Uses click/pointer/delete actions from OC Input Actions. Handle colliders use tag **`Handles`** so **`SelectionManager`** routes hits to **`HandleRaycastHit`** instead of object selection.

### Undo

Drag operations on position/rotation handles create **`TransformUndoAction`** entries on **`RuntimeUndoSystem`**.

## Editor tools panel

[`EditorToolsPanel`](../../Runtime/System/Scripts/Toolbar/EditorToolsPanel.cs) drives `Tool`, `Pivot`, and `Coordinate` when objects are selected.

## Handle implementation

- **`Handle`**, **`HandleBase`** — shared handle logic
- **`PositionAxis`**, **`PositionPlane`**, **`RotationAxis`** — axis widgets
- **`HandleCollider`** — raycast targets for handles
- Prefab assets under `Runtime/Interactions/Prefabs/TransformHandles/`

## Setup

Included automatically with **Interactions** prefab. Ensure:

- **SelectionManager** and **RuntimeUndoSystem** present
- Selected objects use **`RuntimeInspector`** if gizmos should target them
- Handle colliders on layer/tag expected by selection raycast

## Related

- [Runtime Inspector](../Components/Runtime-Inspector.md)
- [Selection Manager](../Subsystems/Selection-Manager.md)
- [Undo System](../Subsystems/Undo-System.md)
- [AppUI / Editor tools](../AppUI/Toolbar-Tools.md)
