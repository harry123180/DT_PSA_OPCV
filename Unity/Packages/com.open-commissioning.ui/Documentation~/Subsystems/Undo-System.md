# Undo System

## Purpose

Provide runtime undo/redo for editor-style operations (primarily transform changes) using a command stack and Input System shortcuts.

## Type and location

| | |
|--|--|
| **Class** | `OC.UI.Undo.RuntimeUndoSystem` |
| **Prefab object name** | **UndoManager** (child of Interactions) |
| **Script** | [`Runtime/Undo/RuntimeUndoSystem.cs`](../../Runtime/Undo/RuntimeUndoSystem.cs) |
| **Pattern** | `MonoBehaviourSingleton<RuntimeUndoSystem>` |

## Command interface

[`ICommand`](../../Runtime/Undo/Actions/ICommand.cs):

```csharp
void Execute();
void Undo();
void Redo();
```

### Built-in commands

| Type | File | Use |
|------|------|-----|
| `TransformUndoAction` | `TransformUndoCommand.cs` | Position/rotation/scale changes |
| `UndoCommandGroup` | `UndoCommandGroup.cs` | Batch multiple commands as one undo step |

**`Execute(ICommand)`** pushes onto the undo stack and clears the redo stack. **`Undo()`** / **`Redo()`** pop stacks and invoke the command.

Transform handles call **`Capture`** before drag and **`Execute`** on release.

## Input

Assign **undo** and **redo** actions from **OC Input Actions** on the **UndoManager** prefab instance.

## Integration points

| System | Usage |
|--------|--------|
| `RuntimeTransformHandle` | Move/rotate handle drags |
| `TransformComponent` | Inspector vector field edits |
| `RotationAxis`, `PositionPlane`, etc. | Handle sub-widgets |

## Setup

1. Include **Interactions** prefab (contains **UndoManager**).
2. Wire undo/redo input actions.
3. Optional: enable **`_debug`** to log command types on undo/redo.

## Related

- [Runtime Inspector](../Components/Runtime-Inspector.md)
- [Modules/Transform-Handles.md](../Modules/Transform-Handles.md)
- [Scene Setup](../Scene-Setup.md)
