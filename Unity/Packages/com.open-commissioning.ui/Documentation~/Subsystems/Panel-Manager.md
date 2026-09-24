# Panel Manager

## Purpose

Show UIToolkit property panels for selected commissioning components in a dockable sidebar attached to **AppUI**.

## Type and location

| | |
|--|--|
| **Class** | `OC.UI.Panel.PanelManager` |
| **Prefab object name** | **PanelsManager** (child of Interactions) |
| **Script** | [`Runtime/Panels/Scripts/PanelManager.cs`](../../Runtime/Panels/Scripts/PanelManager.cs) |
| **Pattern** | `MonoBehaviourSingleton<PanelManager>` |
| **Handlers** | Child **DefaultPanels** lists all `PanelHandler` components |

## Selection-driven panels

On **`Start`**:

1. Loads sidebar UXML (`UXML/panel-sidebar`) into **`AppUI.Instance.Root`**.
2. Builds a **`Dictionary<Type, PanelHandler>`** from serialized **`_panelHandlers`**.

On **`SelectionManager.OnSelectionChanged`**:

- Resolves panel type for selected interaction’s component.
- Creates or reuses **`IPanel`** instances via matching **`PanelHandler`**.
- Docks panels in the sidebar or floating on screen (`AddToScreen`).

**`_dockThreshold`** (default 10 px) controls snap-to-sidebar behavior when dragging panels.

## Panel handlers (default prefab)

The **Interactions** prefab registers handlers for these OC component types:

| Handler | Typical component |
|---------|-------------------|
| `PanelHandlerCylinder` | Cylinder |
| `PanelHandlerDataReader` | Data reader |
| `PanelHandlerDrivePosition` | Drive position |
| `PanelHandlerDriveSimple` | Simple drive |
| `PanelHandlerDriveSpeed` | Drive speed |
| `PanelHandlerLock` | Lock |
| `PanelHandlerPayload` | Payload |
| `PanelHandlerSensorAnalog` | Analog sensor |
| `PanelHandlerSensorBinary` | Binary sensor |
| `PanelHandlerSink` | Sink |
| `PanelHandlerSource` | Source |
| `PanelHandlerTagReader` | Tag reader |
| `PanelHandlerTypeChanger` | Type changer |

Add custom **`PanelHandler`** subclasses for new component types and register them on **DefaultPanels** or your own manager instance.

## Public API (selected)

| Member | Description |
|--------|-------------|
| `Sidebar` | Sidebar `VisualElement` |
| `Screen` | App UI root (`AppUI.Root`) |
| `ActivePanels` | Currently open panels |
| `AddToScreen(VisualElement)` | Float a panel on the main UI |
| `AddToSidebar(IPanel)` | Dock a panel |

## Setup

1. Include **Interactions** prefab (PanelsManager + DefaultPanels).
2. Ensure **AppUI** is present (nested in Interactions).
3. Add **`PanelHandler`** components for each component type you need in the inspector list.
4. Selected objects must have **`Interaction`** and a component type with a matching handler.

## Related

- [Interaction](../Components/Interaction.md)
- [Selection Manager](Selection-Manager.md)
- [Modules/Panels.md](../Modules/Panels.md)
- [AppUI Overview](../AppUI/Overview.md)
