# Runtime Inspector

## Purpose

Allow runtime editing of transform properties (and optional destroy) on selected objects through UI Toolkit fields integrated with transform handles and undo.

## RuntimeInspector marker

**Script:** [`Runtime/Inspector/Scripts/RuntimeInspector.cs`](../../Runtime/Inspector/Scripts/RuntimeInspector.cs)

Add this component to a GameObject that should expose inspector fields when selected.

### Settings

| Field | Description |
|-------|-------------|
| `_transformType` | Flags: `Position`, `Rotation`, `Scale`, or `All` — which fields are editable |
| `_canDestroyed` | Whether destroy actions are allowed (reserved for future use) |

## TransformComponent UI

**Script:** [`Runtime/Inspector/Scripts/TransformComponent.cs`](../../Runtime/Inspector/Scripts/TransformComponent.cs)

UIToolkit visual element loaded from `Resources/UXML/TransformComponent` with stylesheet `inspector-component`.

### Behavior

- **`Bind(Transform)`** — Shows the inspector when the transform has a **`RuntimeInspector`** component.
- Displays **position**, **rotation**, and **scale** `Vector3Field` controls based on `_transformType`.
- Field changes create **`TransformUndoAction`** commands on **`RuntimeUndoSystem`**.
- Coordinates with **`RuntimeTransformHandle`** for space (local/world) when applying values.
- Refreshes on a scheduled 100 ms interval while visible.

## When it appears

1. User selects an object with **`Interaction`**.
2. **`RuntimeTransformHandle`** and panel systems react to selection.
3. If the selected hierarchy includes **`RuntimeInspector`**, **`TransformComponent`** binds and becomes visible in the inspector UI hierarchy.

Selection alone does not add `RuntimeInspector`; you must place the component on objects that should be editable.

## Setup

1. Add **`RuntimeInspector`** to the device root or the transform you want to edit.
2. Configure which transform channels are editable.
3. Ensure **`RuntimeUndoSystem`** is in the scene (included in **Interactions** prefab).
4. Use **Move** / **Rotate** tools on the editor toolbar ([`EditorToolsPanel`](../../Runtime/System/Scripts/Toolbar/EditorToolsPanel.cs)) for gizmo-based edits; inspector fields complement gizmos.

## Undo

Transform edits from the inspector use the same undo stack as transform handles:

- **`TransformUndoAction`** — captures before/after pose
- **`RuntimeUndoSystem`** — Ctrl+Z / redo input actions

See [Undo System](../Subsystems/Undo-System.md).

## Related

- [Interaction](Interaction.md)
- [Modules/Transform-Handles.md](../Modules/Transform-Handles.md)
- [Undo System](../Subsystems/Undo-System.md)
- [Selection Manager](../Subsystems/Selection-Manager.md)
