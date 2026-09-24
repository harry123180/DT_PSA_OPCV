# Interaction

## Purpose

Mark commissioning objects as selectable, hoverable, and panel-aware. The core **`Interaction`** type lives in the host **`OC`** assembly; this package adds selection, outline, panels, and tooling around it.

## Core component (external)

**Type:** `OC.Interactions.Interaction`  
**Assembly:** `OC` (not shipped with `com.open-commissioning.ui`)

The host project must reference the Open Commissioning core package. Without it, `OC.UI` does not compile.

### Typical responsibilities

- **Modes** — e.g. `InteractionMode.Selection` controls whether an object participates in selection.
- **State** — flags such as `Hovered`, `Selected`, `Disabled` (via `InteractionState`).
- **Renderers** — list of renderers used by outline and visual feedback.
- **Target** — reference to the commissioning component or payload.

Refer to the **OC** package documentation for full API details.

## Package integration

### SelectionManager

[`Runtime/Interactions/Scripts/SelectionManager.cs`](../../Runtime/Interactions/Scripts/SelectionManager.cs)

- Raycasts from **Main Camera** using configured **layer mask** and Input System pointer/click actions.
- Skips input when **`SelectionManager.Enable`** is false or pointer is over UI.
- **Click:** selects hit object; **Ctrl** cycles among stacked hits.
- **Handle hits** (tag `Handles`) are routed separately from object selection.
- Fires Unity pointer events (`pointerEnter`, `select`, `deselect`, etc.) on targets.
- Events: `OnSelectionChanged`, `OnDestroy` (when pooled payloads are destroyed).

Enable selection with the **Interaction Tool** in the AppUI toolbar.

### Outline

[`Runtime/Interactions/Scripts/Outline.cs`](../../Runtime/Interactions/Scripts/Outline.cs)

- Requires **`Interaction`** on the same GameObject.
- Maps **Hovered** → `Outline_1`, **Selected** → `Outline_2` rendering layers.

See [Outline](Outline.md).

### PanelManager

[`Runtime/Panels/Scripts/PanelManager.cs`](../../Runtime/Panels/Scripts/PanelManager.cs)

- Subscribes to `OnSelectionChanged`.
- Opens a **Panel** for the selected object’s component type via registered **`PanelHandler`** instances.

See [Panel Manager](../Subsystems/Panel-Manager.md) and [Modules/Panels.md](../Modules/Panels.md).

### Editor auto-setup

[`Editor/Scripts/InteractionComponentWatcher.cs`](../../Editor/Scripts/InteractionComponentWatcher.cs)

When an **`Interaction`** is added in the Editor, **`Outline`** is added automatically if missing.

## Optional companion components (this package)

| Component | Script | Role |
|-----------|--------|------|
| `Outline` | `Outline.cs` | URP outline layers for hover/select |
| `Clickable` | `Clickable.cs` | Click handling helpers |
| `PointerEventHandler` | `PointerEventHandler.cs` | Pointer event bridge |
| `Tooltip` / `TooltipEntity` / `TooltipDevice` | Various | Tooltip content for `TooltipManager` |
| `RuntimeInspector` | See [Runtime Inspector](Runtime-Inspector.md) | Transform editing in UI |
| `ColliderMaterial` | `ColliderMaterial.cs` | Collider debug draw (Collider View tool) |
| `Label` | `Label.cs` | World-space TextMeshPro label |
| `HideGroup` | `HideGroup.cs` | Hide or fade groups of renderers |

## Setup on a GameObject

1. Add **`Interaction`** (from OC core) and configure modes, colliders, and renderers.
2. Add **`Outline`** (or let the Editor watcher add it).
3. Match collider **layer** to `SelectionManager` mask (default layer 10).
4. Add a **`PanelHandler`**-compatible OC component if you need a property panel.
5. Optionally add **`RuntimeInspector`**, **`Label`**, **`HideGroup`**, **`ColliderMaterial`**.

## Related

- [Selection Manager](../Subsystems/Selection-Manager.md)
- [Outline](Outline.md)
- [Scene Setup](../Scene-Setup.md)
- [AppUI / Interaction Tool](../AppUI/Toolbar-Tools.md)
