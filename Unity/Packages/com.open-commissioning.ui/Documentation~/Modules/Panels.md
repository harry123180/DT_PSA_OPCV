# Panels module

## Purpose

Build UIToolkit property panels for Open Commissioning components when their **`Interaction`** is selected.

## Core types

| Type | Script | Role |
|------|--------|------|
| `PanelManager` | `PanelManager.cs` | Sidebar host, selection subscription |
| `PanelHandler` | `PanelHandler.cs` | Abstract factory: `ReferenceType` → `IPanel` |
| `IPanel` | `IPanel.cs` | Panel instance contract |
| `Panel` | `Panel.cs` | Base panel implementation |
| `PanelFactory` | `PanelFactory.cs` | Panel creation helpers |
| `SubsystemPanel` | `SubsystemPanel.cs` | Container for toolbar tool content |

Namespace: **`OC.UI.Panel`**.

## Panel handlers

Each handler is a **`MonoBehaviour`** on **DefaultPanels** (under Interactions):

```csharp
public abstract class PanelHandler : MonoBehaviour
{
    public abstract Type ReferenceType { get; }
    public abstract IPanel Create();
}
```

Shipped handlers (see [Panel Manager](../Subsystems/Panel-Manager.md) for the full list) map OC component types to panel UXML/USS and field bindings.

## Field controls

Reusable UIToolkit field wrappers under `Runtime/Panels/Scripts/Fields/`, including:

- Numeric: `PanelFloatField`, `PanelIntegerField`, `PanelSlider`, `PanelSliderInt`
- Vectors: `PanelVector2Field`, `PanelVector3Field`, `PanelVector3IntField`
- Text: `PanelTextField`, `PanelTextFieldInt`, `PanelTextFieldFloat`
- UI: `PanelButton`, `PanelToggle`, `PanelToggleIcon`, `PanelDropdownField`, `PanelEnumField`
- Status: `PanelBinaryStatusField`, `PanelLinkStatus`, `PanelProgressBarField`
- Layout: `PanelGroupContainer`, `PanelHorizontalGroup`, `PanelScrollView`, `PanelListView`

Panels compose these elements to mirror commissioning data on selected devices.

## Drag and dock

- **`PanelDragAndDrop`** — reposition floating panels
- **`Moveable`** — drag behavior helper
- **`PanelManager._dockThreshold`** — snap distance to sidebar

## Adding a new panel type

1. Subclass **`PanelHandler`**; implement **`ReferenceType`** and **`Create()`**.
2. Build a **`Panel`** (or `IPanel`) that binds to your OC component.
3. Add the handler component to **DefaultPanels** (or your custom manager object).
4. Register the handler reference on **`PanelManager._panelHandlers`**.

## Related

- [Panel Manager](../Subsystems/Panel-Manager.md)
- [Interaction](../Components/Interaction.md)
- [AppUI Overview](../AppUI/Overview.md)
