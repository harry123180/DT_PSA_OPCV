# Virtual Camera

## Purpose

Provide orbit, pan, zoom, focus, and follow controls for commissioning scene views using Cinemachine 3 and the Input System.

## Prefab

**Path:** [`Runtime/Prefabs/Virtual Camera.prefab`](../../Runtime/Prefabs/Virtual%20Camera.prefab)

Hierarchy:

```text
Virtual Camera          ← CameraController
├── Cinemachine Camera  ← Cinemachine 3 virtual camera
└── Pivot               ← Orbit / look-at pivot
```

## CameraController

**Script:** [`Runtime/Camera/CameraController.cs`](../../Runtime/Camera/CameraController.cs)

### Responsibilities

- Drives the **Pivot** transform (position/rotation) based on input.
- Uses a child **CinemachineCamera** for the actual view.
- Respects **`AppUI.Instance.IsPointerValidForAction`** so camera input is ignored when the pointer is over UI, unfocused, or outside the screen.
- Exposes **`State`**, **`IsBusy`**, **`IsStatic`**, and optional **`Target`** for follow/focus behavior.

### Inspector settings

| Field | Description |
|-------|-------------|
| `_isStatic` | Disables user camera manipulation when true |
| `_focusOnSelection` | Focus camera on selected interactions when applicable |
| `_settings` | [`CameraSettings`](../../Runtime/Camera/CameraSettings.cs) asset (speeds, limits) |
| `_updateLoop` | `Update` or `FixedUpdate` for camera motion |
| `_pivot` | Transform used as orbit center |
| `_camera` | Cinemachine virtual camera reference |

### Input actions

Configured on the prefab (from **OC Input Actions**):

- Move, mouse, scroll, look, orbit, pan, zoom, sprint, focus, follow

Assign or override these per project in the Inspector.

## Switching cameras

**[`CamerasManager`](../Subsystems/Cameras-Manager.md)** discovers all `CameraController` instances, sorts them by hierarchy path, and enables exactly one at a time.

Users can switch views via:

- **Cameras** toolbar tool ([`CameraToolWindow`](../../Runtime/System/Scripts/Toolbar/CameraToolWindow.cs))
- Code: `CamerasManager.Instance.SetCameraActive(index)` or `SetCameraActive(controller)`

## Setup

1. Place one **Main Camera** prefab (scene render camera).
2. Add one or more **Virtual Camera** prefab instances.
3. Ensure `CamerasManager` is present (included in **Interactions** prefab).
4. Wire Input System actions if you deviate from the default asset.

## Usage tips

- Rename virtual camera roots for clearer labels in the **Cameras** toolbar.
- Use **`_isStatic`** for fixed diagnostic views.
- Only the active `CameraController` GameObject is enabled; inactive cameras are deactivated.

## Related

- [Cameras Manager](../Subsystems/Cameras-Manager.md)
- [Scene Setup](../Scene-Setup.md)
- [AppUI / Cameras tool](../AppUI/Toolbar-Tools.md)
