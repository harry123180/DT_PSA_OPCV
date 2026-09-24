# Setup and dependencies

## Purpose

Install **Open Commissioning UI** (`com.open-commissioning.ui`) and configure the Unity project so all runtime systems compile and render correctly.

## Install from Git

Add Git dependencies in the Unity project’s `**Packages/manifest.json`**:

```json
{
  "dependencies": {
    "com.open-commissioning.core": "https://github.com/OpenCommissioning/OC_Unity_Core.git#upm",
    "com.cqf.outline": "https://github.com/CristianQiu/Unity-URP-Outline.git",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.dbrizov.naughtyattributes": "https://github.com/dbrizov/NaughtyAttributes.git#upm",
    "com.unity.inputsystem": "1.19.0",
    "com.unity.cinemachine": "3.1.6",
  }
}
```

After Unity resolves packages, confirm **Package Manager** shows **Open Commissioning UI** without compile errors.

## Unity version
> **Unity 6000.3** or newer (see `[package.json](../package.json)`).

## Render pipeline (URP)

This package targets **Universal Render Pipeline (URP)** only.

Assembly references (see `[Runtime/OC.UI.asmdef](../Runtime/OC.UI.asmdef)`):

- `Unity.RenderPipelines.Core.Runtime`
- `Unity.RenderPipelines.Universal.Runtime`

**Recommended:** Use or merge settings from package reference assets:


| Asset                               | Path                                     |
| ----------------------------------- | ---------------------------------------- |
| URP asset                           | `Runtime/Settings/OC URP UI Asset.asset` |
| URP Renderer (with outline feature) | `Runtime/Settings/OC URP Renderer.asset` |
| Volume profile (outline override)   | `Runtime/Settings/OC Volume.asset`       |


Set the active pipeline asset in **Project Settings → Graphics** and assign the renderer on the pipeline asset.

## Unity / registry packages

Declared in `[Runtime/OC.UI.asmdef](../Runtime/OC.UI.asmdef)`:


| Package      | Version / notes                                                    |
| ------------ | ------------------------------------------------------------------ |
| TextMeshPro  | Required for labels and UI text                                    |
| Cinemachine  | **3.0 – 3.1.5** — defines `CINEMACHINE_3_0_OR_NEWER` when in range |
| Input System | **≥ 1.17.0** — defines `INPUTSYSTEM_1_17_0_OR_NEWER`               |


Install via **Window → Package Manager** if not already present.

## Additional assembly references

The host project must also provide:


| Assembly                   | Purpose                                                                                               |
| -------------------------- | ----------------------------------------------------------------------------------------------------- |
| **UniTask**                | Async layout save/load                                                                                |
| **NaughtyAttributes.Core** | Inspector buttons on several runtime types                                                            |
| **OC**                     | Open Commissioning core (`OC.Interactions`, `OC.Components`, etc.) — **not included in this package** |


Without the `**OC`** assembly, `OC.UI` will not compile. Add the Open Commissioning core package or project reference from your organization’s repository.

## Outline package (`com.cqf.outline`)

- URP **renderer feature** + volume override **Custom → Outline**.
- Driven by rendering layers `**Outline_1`** … `**Outline_4**`.
- Enable **post-processing** on the **camera** and on the **URP Renderer** asset.
- Optional: `OutlineRendererFeature.ForceCullPass = true` when nothing uses outline layers, to skip passes.

Full outline setup: [Components/Outline.md](Components/Outline.md).

## Input actions

Assign or merge `**Runtime/Settings/OC Input Actions.inputactions`** in your project. Used by:

- `SelectionManager` (click, pointer)
- `CameraController` (orbit, pan, zoom, focus, follow)
- `RuntimeUndoSystem` (undo, redo)
- `AppUI` (cancel, window mode)

Wire actions on prefabs (`Interactions`, `Virtual Camera`, `AppUI`) to match your project's **Input System** settings (e.g. **Project Settings → Input System Package**).

## Platforms

`[Runtime/OC.UI.asmdef](../Runtime/OC.UI.asmdef)` includes:

- Editor
- WindowsStandalone32
- WindowsStandalone64

Other platforms are excluded unless you extend the asmdef in a fork.

## Setup checklist

1. Add `**com.open-commissioning.ui`** and `**com.cqf.outline**` to `manifest.json`.
2. Ensure **URP** is active; add `**OutlineRendererFeature`** to the renderer.
3. Add a **Volume** with the **Outline** override.
4. Define rendering layers `**Outline_1`–`Outline_4`** in **Tags and Layers**.
5. Enable **Post Processing** on the main camera.
6. Provide `**OC`**, **UniTask**, and **NaughtyAttributes** assemblies.
7. Configure **Input System** and reference **OC Input Actions**.
8. Follow [Scene Setup](Scene-Setup.md) to place scene prefabs.

## Related

- [Scene Setup](Scene-Setup.md)
- [Components/Outline.md](Components/Outline.md)
- [README](../README.md)

