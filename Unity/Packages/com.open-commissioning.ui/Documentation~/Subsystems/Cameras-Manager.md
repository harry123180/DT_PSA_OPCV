# Cameras Manager

## Purpose

Discover all **`CameraController`** instances in the scene, keep an ordered list, and activate exactly one virtual camera at a time.

## Type and location

| | |
|--|--|
| **Class** | `OC.UI.CamerasManager` |
| **Script** | [`Runtime/Camera/CamerasManager.cs`](../../Runtime/Camera/CamerasManager.cs) |
| **Prefab** | Child **CamerasManager** under [`Interactions.prefab`](../../Runtime/Prefabs/Interactions.prefab) |
| **Pattern** | `MonoBehaviourSingleton<CamerasManager>` |

## Discovery

On **`OnEnable`**:

1. Finds all **`CameraController`** components (`FindObjectsByType`, excludes inactive).
2. Sorts by full hierarchy path (parent/child order stable in UI).
3. Calls **`DisableAllCameras`**, then activates the first camera if any exist.

The serialized **`_cameras`** list is rebuilt at runtime; prefab list can be empty.

## API

| Method | Description |
|--------|-------------|
| `SetCameraActive(int index)` | Enables camera at index, disables others |
| `SetCameraActive(CameraController)` | Same, by reference |
| `DisableAllCameras()` | Deactivates all controller GameObjects |

**Properties:**

- **`ActiveCamera`** — current `CameraController`
- **`ActiveCameraIndex`** — index in internal list
- **`Cameras`** — read-only list of controllers

## Toolbar integration

**[`CameraToolWindow`](../../Runtime/System/Scripts/Toolbar/CameraToolWindow.cs)** reads `CamerasManager.Instance.Cameras` and builds a toggle per camera. Turning one on switches the active index; turning off the active camera is prevented (toggle snaps back).

## Setup

1. Place **Interactions** prefab (includes **CamerasManager**).
2. Add one or more [**Virtual Camera**](../Components/Virtual-Camera.md) prefab instances.
3. Rename camera roots for meaningful toolbar labels.

## Related

- [Virtual Camera](../Components/Virtual-Camera.md)
- [AppUI / Cameras tool](../AppUI/Toolbar-Tools.md)
- [Scene Setup](../Scene-Setup.md)
