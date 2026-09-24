# Industrial UI module

## Purpose

UIToolkit-based industrial HMI-style UI elements and panel sampling for operator-facing layouts (tabs, knobs, component groups).

## Location

`Runtime/Industrial/`

| Area | Contents |
|------|----------|
| Scripts | `IndustrialPanelManager`, `PanelSamplerUI`, `IIndustrialPanel` |
| VisualElements | `TabMenu`, `Knob`, `ComponentsGroup` |
| Resources | `UXML/`, `StyleSheet/industrial-tabs.uss` |

Namespace: **`OC.UI.Industrial`**.

## Key types

### IndustrialPanelManager

[`Runtime/Industrial/Scripts/IndustrialPanelManager.cs`](../../Runtime/Industrial/Scripts/IndustrialPanelManager.cs)

Manages industrial panel instances in the scene (lifecycle and visibility). Use when embedding operator panels separate from the main **PanelManager** sidebar.

### PanelSamplerUI

[`Runtime/Industrial/Scripts/PanelSamplerUI.cs`](../../Runtime/Industrial/Scripts/PanelSamplerUI.cs)

Bridges OC interaction/UIElements types for sampled industrial panel content.

### Visual elements

- **`TabMenu`** — tabbed container for industrial views
- **`Knob`** — rotary control using OC UIElements integration
- **`ComponentsGroup`** — grouped component presentation

Styles live under `Runtime/Industrial/Resources/StyleSheet/`.

## Usage

Industrial UI is optional relative to the core commissioning flow (**Interactions**, **PanelManager**, **AppUI**). Import UXML/USS from `Runtime/Industrial/Resources` into your **UIDocument** or subclass the provided visual elements.

Depends on **`OC.Interactions.UIElements`** from the host **OC** assembly.

## Related

- [Panels module](Panels.md)
- [AppUI Overview](../AppUI/Overview.md)
- [Setup](../Setup.md) — OC assembly requirement
