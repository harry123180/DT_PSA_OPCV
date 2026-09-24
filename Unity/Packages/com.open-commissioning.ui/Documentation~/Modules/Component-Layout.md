# Component Layout module

## Purpose

Save and restore commissioning scene layout (component positions and related data) as XML files under **StreamingAssets**, with optional auto-load on scene start.

## LayoutSaveSystem

**Script:** [`Runtime/System/Scripts/ComponentLayout/LayoutSaveSystem.cs`](../../Runtime/System/Scripts/ComponentLayout/LayoutSaveSystem.cs)

**Pattern:** `MonoBehaviourSingleton<LayoutSaveSystem>`

### Storage

| Setting | Value |
|---------|--------|
| Directory | `{StreamingAssets}/Saved/` |
| Extension | `.xml` |
| Default file name | `{SceneName}_{yyyyMMdd_HHmmss}.xml` |

> The repository includes `StreamingAssets/ComponentLayouts/` as a placeholder folder; runtime saves use the **`Saved`** subfolder per `LayoutSaveSystem`.

### Auto-load

When **`SettingsManager.AutoLoadSave`** is true:

- On **Start** and **sceneLoaded**, loads the newest matching XML in the Saved folder.
- Prefers files prefixed with the active scene name.

### Dirty state

- **`MarkDirty()`** / **`ClearDirty()`** — track unsaved changes
- **`IsDirty`** — consulted by **AppUI** exit flow to prompt save

### API (selected)

| Method | Description |
|--------|-------------|
| `SaveAsync()` | Save via file browser (UniTask) |
| `LoadAsync()` | Load via file browser |
| `ImportFromFile(path)` | Import from explicit path |
| `TryAutoLoadLatest()` | Load newest layout file |
| `GetLayoutsDirectory()` | Returns StreamingAssets/Saved path |

Supporting types: **`ComponentLayoutData`**, **`ComponentLayoutXmlSerializer`**, **`ILayoutRuntimeActions`**, **`LayoutRuntimeActions`**.

## UI and console

- **Save/Load** toolbar panel — [`SaveLoadPanel`](../../Runtime/System/Scripts/Toolbar/SaveLoadWindow.cs)
- Console commands in [`ComponentLayoutCommands.cs`](../../Runtime/System/Scripts/Console/Commands/ComponentLayoutCommands.cs):

| Command | Description |
|---------|-------------|
| `layout.save` | Save layout (file browser) |
| `layout.load` | Load layout (file browser) |
| `layout.status` | Print dirty / path status |
| `layout.import` | Import from file path argument |

## Setup

1. Add **`LayoutSaveSystem`** to the scene (or use a project bootstrap that creates the singleton).
2. Ensure **`StreamingAssets/Saved`** exists at runtime (created on demand when saving).
3. Optional: enable **Auto load save** in Settings tool / `SettingsManager`.

## Related

- [AppUI / Save-Load tool](../AppUI/Toolbar-Tools.md)
- [AppUI Overview](../AppUI/Overview.md)
- [Modules/Console.md](Console.md)
