# Selection Manager

## Purpose

Handle mouse picking, hover, and selection of objects with an **`Interaction`** component, and route Unity pointer/selection events to targets.

## Type and location

| | |
|--|--|
| **Class** | `OC.UI.Interactions.SelectionManager` |
| **Script** | [`Runtime/Interactions/Scripts/SelectionManager.cs`](../../Runtime/Interactions/Scripts/SelectionManager.cs) |
| **Prefab** | Child **SelectionManager** under [`Interactions.prefab`](../../Runtime/Prefabs/Interactions.prefab) |
| **Pattern** | `MonoBehaviourSingleton<SelectionManager>` |

## Enable / disable

**`Enable`** property gates all selection logic. When set to `false`:

- Hit state is reset
- Current selection is cleared

The **Interaction Tool** on the AppUI toolbar toggles `Enable` via UnityEvent (`set_Enable`). Selection is off by default in the shipped prefab (`_enable: 0`).

## Raycast behavior

- Uses **`Camera.main`** and **`Physics.RaycastNonAlloc`** with configurable **`_layerMask`** (default: bit 10 / layer "Interactable" depending on project setup).
- Maximum distance: 500 units.
- Ignores hits closer than `OC.Utils.TOLERANCE`.
- Objects tagged **`Handles`** populate **`HandleRaycastHit`** instead of the object hit list (transform gizmo takes priority).
- Closest hit receives **pointer enter**; previous closest receives **pointer exit**.

## Click and selection

| Input | Behavior |
|-------|----------|
| Click performed (down) | Select hit object; Ctrl cycles among multiple hits along the ray |
| Click canceled (up) | Fire pointer click/up on closest hit |
| Empty click | Clear selection |

Selection rules:

- Target must have **`Interaction`** with **`InteractionMode.Selection`**.
- Skips interactions in **`InteractionState.Disabled`**.
- Toggle: click selected object again to deselect.
- Ctrl: additive selection (no clear before add).

## Events

- **`OnSelectionChanged`** — `List<Interaction>` after any selection change; used by `PanelManager`, `EditorToolsPanel`, `RuntimeTransformHandle`.
- **`OnDestroy`** — when a selected interaction’s payload is destroyed via pool.

## AppUI integration

**Cancel** input action (when no UI focus conflict):

1. Deselects last selected interaction if any
2. Otherwise closes last floating panel
3. Otherwise toggles exit popup

See [`AppUI.cs`](../../Runtime/System/Scripts/AppUI.cs).

## Setup

1. Include **Interactions** prefab (contains **SelectionManager**).
2. Set **layer mask** to match interactable collider layers.
3. Assign **click** and **pointer** actions from OC Input Actions.
4. Enable **Interaction Tool** at runtime for operators.

## Related

- [Interaction](../Components/Interaction.md)
- [AppUI / Interaction Tool](../AppUI/Toolbar-Tools.md)
- [Panel Manager](Panel-Manager.md)
- [Modules/Transform-Handles.md](../Modules/Transform-Handles.md)
