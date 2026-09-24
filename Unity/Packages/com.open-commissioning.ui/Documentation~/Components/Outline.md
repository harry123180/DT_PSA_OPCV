# Outline rendering (URP)

## Purpose

Provides hover and selection highlighting for interactable objects using URP rendering layers and a shared outline post-process pass.

## Source

Integrated stack: **`com.cqf.outline`** (URP renderer feature + volume component).  
Upstream: [CristianQiu / Unity-URP-Outline](https://github.com/CristianQiu/Unity-URP-Outline) (MIT).

See also [Setup](../Setup.md) for package installation and [Interaction](Interaction.md) for the `Outline` component that drives layers at runtime.

## What it does

Single outline pass wired to the **URP volume** system. Outlined objects are selected by **Rendering Layer Mask** (four named layers). No extra per-object outline components are required beyond assigning renderers to those layers.

**Requirements:** Unity **6000.3** or newer, **URP**, **post-processing** enabled on the active **Camera** and on the **URP Renderer** asset.

## Project setup

1. **Rendering layers** — In **Edit → Project Settings → Tags and Layers**, define four **Rendering Layers** named exactly: `Outline_1`, `Outline_2`, `Outline_3`, `Outline_4`.
2. **Renderer** — Add **`OutlineRendererFeature`** to the **URP Renderer** asset used by the pipeline. Reference: `Runtime/Settings/OC URP Renderer.asset`.
3. **Volume** — Add a **Volume** (global or local) and add override **Custom → Outline**. Reference: `Runtime/Settings/OC Volume.asset`.
4. **Objects** — For each renderer that should show an outline, set **Rendering Layer Mask** so it includes one of `Outline_1`–`Outline_4` (Inspector or code). Each layer corresponds to its own block of parameters on the volume override, except **border width** (see below).

## OC.UI `Outline` component

Script: `Runtime/Interactions/Scripts/Outline.cs`

The **`Outline`** behaviour (`OC.UI.Interactions`) requires an **`Interaction`** component on the same GameObject. It subscribes to `Interaction.State` and updates rendering layers on all renderers listed on the interaction:

| Interaction state | Default rendering layer |
|-------------------|-------------------------|
| Selected | `Outline_2` |
| Hovered (not selected) | `Outline_1` |
| Neither | Default layer (`1`) |

Layer names are serialized on the component (`_layerNameHover`, `_layerNameSelection`) and must match project rendering layer names.

When you add an `Interaction` in the Editor, `InteractionComponentWatcher` automatically adds `Outline` if missing.

## Volume vs layers

- **Per-layer styling:** `Outline_1` … `Outline_4` each have independent settings on the volume (colors, intensity, etc., per upstream design).
- **Shared border width:** **Border Size** is one value for all four layers (performance / implementation constraint).

## Performance (`ForceCullPass`)

`OutlineRendererFeature` exposes a static **`ForceCullPass`**. Set it to **`true`** to skip the outline passes entirely when you know **no** renderers are using `Outline_1`–`Outline_4`.  
There is no built-in render-graph hook to flip this automatically; the application should track outlined renderers and toggle the flag.

## Limitations

- **No per-object outline width** — only the shared **Border Size**.
- **No alpha clip** on outlined materials for this path.
- **No correct outline on heavy vertex animation** — vertex-shader–displaced geometry is not supported.

## Related

- [Setup](../Setup.md) — dependencies and URP configuration
- [Scene Setup](../Scene-Setup.md) — camera post-processing checklist
- [Interaction](Interaction.md) — selection and hover state
