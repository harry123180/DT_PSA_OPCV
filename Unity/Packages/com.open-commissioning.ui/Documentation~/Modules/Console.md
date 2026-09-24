# Console module

## Purpose

Provide an in-game command console for debugging, scene control, preferences, and layout operations.

## Components

| Type | Script | Role |
|------|--------|------|
| `ConsoleItem` | `Toolbar/ConsoleItem.cs` | Toolbar toggle + console UI |
| `DebugLogConsole` | `Toolbar/DebugLogConsole.cs` | Command registry and execution |
| `ConsoleMethodAttribute` | `Console/ConsoleMethodAttribute.cs` | Marks static methods as commands |
| `LogList` | `Console/LogList.cs` | Log output list |

Open the console from the **Console** tool on the AppUI toolbar.

## Registering commands

Apply **`[ConsoleMethod("command.name", "Description")]`** to static methods. Use **`[UnityEngine.Scripting.Preserve]`** so IL2CPP stripping does not remove commands.

Example attribute shape:

```csharp
[ConsoleMethod("my.command", "Does something"), Preserve]
public static void MyCommand() { }
```

`DebugLogConsole` scans assemblies at startup for attributed methods.

## Built-in commands

### Scene (`SceneCommands.cs`)

| Command | Description |
|---------|-------------|
| `scene.load` | Load scene by name/index |
| `scene.loadasync` | Load scene asynchronously |
| `scene.unload` | Unload scene |
| `scene.restart` | Restart active scene |

### Time (`TimeCommands.cs`)

| Command | Description |
|---------|-------------|
| `time.scale` | Get/set `Time.timeScale` |

### PlayerPrefs (`PlayerPrefsCommands.cs`)

| Command | Description |
|---------|-------------|
| `prefs.int` | Get/set int preference |
| `prefs.float` | Get/set float preference |
| `prefs.string` | Get/set string preference |
| `prefs.delete` | Delete key |
| `prefs.clear` | Delete all keys |

### Component layout (`ComponentLayoutCommands.cs`)

| Command | Description |
|---------|-------------|
| `layout.save` | Save layout |
| `layout.load` | Load layout |
| `layout.status` | Layout status |
| `layout.import` | Import from path |

## Related

- [AppUI / Console tool](../AppUI/Toolbar-Tools.md)
- [Component Layout](Component-Layout.md)
