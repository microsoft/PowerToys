---
author: Yu Leng
created on: 2025-01-24
last updated: 2025-01-24
---

# Extension Host Settings API

## Abstract

This document describes the Host Settings API, which allows Command Palette extensions to access and respond to host application settings. Extensions can read settings like hotkey configuration, UI preferences, and behavior options, enabling them to adapt their behavior and display relevant information to users.

## Table of Contents

- [Background](#background)
  - [Problem Statement](#problem-statement)
  - [User Scenarios](#user-scenarios)
  - [Design Goals](#design-goals)
- [High-Level Design](#high-level-design)
  - [Architecture Overview](#architecture-overview)
  - [Key Design Decisions](#key-design-decisions)
- [Detailed Design](#detailed-design)
  - [Data Flow](#data-flow)
  - [Cross-Process Communication](#cross-process-communication)
  - [Implementation Files](#implementation-files)
- [API Reference](#api-reference)
  - [WinRT Interfaces](#winrt-interfaces)
  - [Toolkit Classes](#toolkit-classes)
- [Usage Examples](#usage-examples)
- [Compatibility](#compatibility)
- [Future Considerations](#future-considerations)

## Background

### Problem Statement

Command Palette extensions run in separate processes (out-of-process/OOP) from the host application for security and stability. However, some extensions need to be aware of the host's configuration to provide a better user experience. For example:

1. An extension displaying keyboard shortcuts needs to know the current hotkey setting
2. An extension with animations should respect the "Disable Animations" preference
3. A diagnostic extension might want to display all current settings for troubleshooting

Without a formal API, extensions have no way to access this information, limiting their ability to integrate seamlessly with the host application.

### User Scenarios

**Scenario 1: Settings Display Extension**
A developer creates a diagnostic page that displays all current Command Palette settings. This helps users understand their configuration and troubleshoot issues.

**Scenario 2: Adaptive UI Extension**
An extension with rich animations checks the `DisableAnimations` setting and disables its own animations when the user prefers reduced motion.

**Scenario 3: Keyboard Shortcut Helper**
An extension that teaches keyboard shortcuts displays the user's configured hotkey so instructions match their actual configuration.

### Design Goals

1. **Cross-Process Safety**: Work reliably across the OOP boundary between host and extension
2. **Real-Time Updates**: Extensions receive notifications when settings change
3. **AOT Compatibility**: All code must be compatible with Ahead-of-Time compilation
4. **Minimal API Surface**: Simple, focused interfaces that are easy to use
5. **Backward Compatibility**: Old extensions gracefully ignore new capabilities; old hosts don't break new extensions

## High-Level Design

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Host (CmdPal.UI)                            │
│                                                                 │
│  SettingsModel ──(SettingsChanged)──► ExtensionService          │
│       │                                    │                    │
│       │                                    ▼                    │
│       │                            ExtensionWrapper             │
│       │                                    │                    │
│       │                      GetApiExtensionStubs()             │
│       │                                    │                    │
│       │                         ┌──────────┴──────────┐         │
│       │                         ▼                     ▼         │
│       │              IExtendedAttributesProvider  IHostSettingsChanged
│       │                                               │         │
│       └──(initial settings)───────────────────────────┤         │
│                      via CommandProviderWrapper       │         │
└───────────────────────────────────────────────────────│─────────┘
                               OOP Boundary             │
┌───────────────────────────────────────────────────────│─────────┐
│                     Extension Process                 │         │
│                                                       ▼         │
│  CommandProvider                                                │
│       │                                                         │
│       ├── GetApiExtensionStubs() returns:                       │
│       │      • SupportCommandsWithProperties                    │
│       │      • HostSettingsChangedHandler ◄─────────────────┐   │
│       │                                                     │   │
│       └── OnHostSettingsChanged(settings) ◄─────────────────┘   │
│                      │                                          │
│                      ▼                                          │
│            HostSettingsManager.Update(settings)                 │
│                      │                                          │
│                      ▼                                          │
│            SettingsChanged event                                │
│                      │                                          │
│                      ▼                                          │
│            Extension Pages (e.g., HostSettingsPage)             │
└─────────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

#### 1. Cross-Process Interface Detection via GetApiExtensionStubs

**Problem:** The `is` operator for interface detection doesn't work reliably across process boundaries in WinRT/COM scenarios. When the host tries to check `if (provider is IHostSettingsChanged)`, it fails because the interface isn't in the direct inheritance chain of the proxy object.

**Solution:** Use the `GetApiExtensionStubs()` pattern. The extension returns stub objects that are properly marshalled across the process boundary, allowing reliable interface detection on the host side.

```csharp
// In CommandProvider.cs (Toolkit)
public object[] GetApiExtensionStubs()
{
    return [
        new SupportCommandsWithProperties(),      // For IExtendedAttributesProvider
        new HostSettingsChangedHandler(this)      // For IHostSettingsChanged
    ];
}

// In ExtensionWrapper.cs (Host)
foreach (var stub in provider2.GetApiExtensionStubs())
{
    if (stub is IHostSettingsChanged handler)  // This works across OOP!
    {
        handler.OnHostSettingsChanged(settings);
        return;
    }
}
```

#### 2. Unified Settings Delivery

Both initial settings and change notifications use the same `OnHostSettingsChanged` path:

- **Initial Settings:** Sent by `CommandProviderWrapper.UnsafePreCacheApiAdditions()` when discovering the handler during extension startup
- **Change Notifications:** Sent by `ExtensionWrapper.NotifyHostSettingsChanged()` when the user modifies settings

This simplifies the design by having a single code path for settings delivery.

#### 3. Capability Detection via ICommandProvider2

Use `is ICommandProvider2` as the capability gate. If an extension implements `ICommandProvider2`, it's using the modern toolkit and may support host settings (if it provides an `IHostSettingsChanged` stub).

```csharp
if (provider is ICommandProvider2 provider2)
{
    // Extension uses modern toolkit, check for settings support
    var stubs = provider2.GetApiExtensionStubs();
    // ...
}
```

## Detailed Design

### Data Flow

#### Extension Startup (Initial Settings)

```
Extension starts
    └─► CommandProviderWrapper created
        └─► LoadTopLevelCommands()
            └─► UnsafePreCacheApiAdditions(provider2)
                └─► GetApiExtensionStubs()
                    └─► Find IHostSettingsChanged handler
                        └─► handler.OnHostSettingsChanged(currentSettings)
                            └─► HostSettingsManager.Update(settings)
```

#### Settings Change Notification

```
User changes settings in CmdPal
    └─► SettingsModel.SettingsChanged event
        └─► ExtensionService.NotifyHostSettingsChanged(settings)
            └─► foreach extension:
                └─► ExtensionWrapper.NotifyHostSettingsChanged(settings)
                    └─► GetApiExtensionStubs()
                        └─► Find IHostSettingsChanged handler
                            └─► handler.OnHostSettingsChanged(settings)
                                └─► HostSettingsManager.Update(settings)
                                    └─► SettingsChanged?.Invoke()
```

### Cross-Process Communication

The settings are passed as an `IHostSettings` WinRT interface, which is automatically marshalled by the WinRT runtime across the process boundary. The host creates a `HostSettings` object (via `HostSettingsConverter.ToHostSettings()`), and the extension receives a proxy that implements the same interface.

Key considerations:

1. **No Reflection:** All type checking uses WinRT's native QueryInterface mechanism
2. **AOT Safe:** All types are known at compile time
3. **Error Handling:** All cross-process calls are wrapped in try-catch to handle extension crashes gracefully

### Implementation Files

#### Host Side

| File | Purpose |
|------|---------|
| `App.xaml.cs` | Subscribes to `SettingsModel.SettingsChanged`, sets up `GetHostSettingsFunc` |
| `AppExtensionHost.cs` | Provides `GetHostSettingsFunc` static property for settings access |
| `HostSettingsConverter.cs` | Extension method to convert `SettingsModel` to `IHostSettings` |
| `ExtensionService.cs` | Broadcasts settings changes to all extensions via `NotifyHostSettingsChanged()` |
| `ExtensionWrapper.cs` | Sends settings to individual extension via `NotifyHostSettingsChanged()` |
| `CommandProviderWrapper.cs` | Sends initial settings when discovering `IHostSettingsChanged` handler |

#### Extension/Toolkit Side

| File | Purpose |
|------|---------|
| `CommandProvider.cs` | Base class providing `IHostSettingsChanged` stub via `GetApiExtensionStubs()` |
| `HostSettingsManager.cs` | Static manager with `Current` property and `SettingsChanged` event |
| `HostSettings.cs` | Toolkit implementation of `IHostSettings` interface |

## API Reference

### WinRT Interfaces

#### IHostSettings

Represents the host application settings that can be passed to extensions.

```idl
[contract(Microsoft.CommandPalette.Extensions.ExtensionsContract, 1)]
interface IHostSettings
{
    String Hotkey { get; };
    Boolean ShowAppDetails { get; };
    Boolean HotkeyGoesHome { get; };
    Boolean BackspaceGoesBack { get; };
    Boolean SingleClickActivates { get; };
    Boolean HighlightSearchOnActivate { get; };
    Boolean ShowSystemTrayIcon { get; };
    Boolean IgnoreShortcutWhenFullscreen { get; };
    Boolean DisableAnimations { get; };
    SummonTarget SummonOn { get; };
}
```

| Property | Type | Description |
|----------|------|-------------|
| `Hotkey` | String | The keyboard shortcut to activate Command Palette (e.g., "Alt+Space") |
| `ShowAppDetails` | Boolean | Whether to show application details in the UI |
| `HotkeyGoesHome` | Boolean | Whether pressing the hotkey returns to the home page |
| `BackspaceGoesBack` | Boolean | Whether backspace navigates back when search is empty |
| `SingleClickActivates` | Boolean | Whether single-click activates items (vs double-click) |
| `HighlightSearchOnActivate` | Boolean | Whether to highlight search text on activation |
| `ShowSystemTrayIcon` | Boolean | Whether to show the system tray icon |
| `IgnoreShortcutWhenFullscreen` | Boolean | Whether to ignore the hotkey when a fullscreen app is active |
| `DisableAnimations` | Boolean | Whether animations are disabled |
| `SummonOn` | SummonTarget | Where to position the window when summoned |

#### SummonTarget

Enum representing the position behavior when summoning Command Palette.

```idl
[contract(Microsoft.CommandPalette.Extensions.ExtensionsContract, 1)]
enum SummonTarget
{
    ToMouse = 0,
    ToPrimary = 1,
    ToFocusedWindow = 2,
    InPlace = 3,
    ToLast = 4,
};
```

#### IHostSettingsChanged

Interface for extensions to receive settings change notifications.

```idl
[contract(Microsoft.CommandPalette.Extensions.ExtensionsContract, 1)]
interface IHostSettingsChanged
{
    void OnHostSettingsChanged(IHostSettings settings);
}
```

### Toolkit Classes

#### HostSettingsManager

Static class providing access to current host settings and change notifications.

```csharp
namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static class HostSettingsManager
{
    /// <summary>
    /// Occurs when the host settings have changed.
    /// </summary>
    public static event Action? SettingsChanged;

    /// <summary>
    /// Gets the current host settings, or null if not yet initialized.
    /// </summary>
    public static IHostSettings? Current { get; }

    /// <summary>
    /// Gets whether host settings are available.
    /// </summary>
    public static bool IsAvailable { get; }
}
```

#### CommandProvider Virtual Method

Extensions can override this method for custom settings handling:

```csharp
public abstract partial class CommandProvider
{
    /// <summary>
    /// Called when host settings change. Override to handle settings changes.
    /// The default implementation updates HostSettingsManager.
    /// </summary>
    public virtual void OnHostSettingsChanged(IHostSettings settings)
    {
        HostSettingsManager.Update(settings);
    }
}
```

## Usage Examples

### Reading Settings in a Page

```csharp
public class MyPage : ListPage
{
    public MyPage()
    {
        // Subscribe to changes
        HostSettingsManager.SettingsChanged += () => RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        var settings = HostSettingsManager.Current;
        if (settings == null)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = "Settings not available"
            }];
        }

        return [
            new ListItem(new NoOpCommand())
            {
                Title = $"Hotkey: {settings.Hotkey}"
            },
            new ListItem(new NoOpCommand())
            {
                Title = $"Animations: {(settings.DisableAnimations ? "Off" : "On")}"
            },
        ];
    }
}
```

### Custom Settings Handler in CommandProvider

```csharp
public class MyCommandProvider : CommandProvider
{
    public override void OnHostSettingsChanged(IHostSettings settings)
    {
        base.OnHostSettingsChanged(settings);  // Update HostSettingsManager

        // Custom logic
        if (settings.DisableAnimations)
        {
            DisableMyAnimations();
        }
    }
}
```

### Checking Settings Availability

```csharp
public void DoSomething()
{
    if (!HostSettingsManager.IsAvailable)
    {
        // Running with old host, use defaults
        return;
    }

    var settings = HostSettingsManager.Current!;
    // Use settings...
}
```

## Compatibility

### Extension Compatibility

| Extension Type | Behavior |
|----------------|----------|
| Old extensions (no ICommandProvider2) | Won't receive settings, gracefully skipped |
| Extensions without IHostSettingsChanged stub | Won't receive settings, gracefully skipped |
| New extensions with IHostSettingsChanged | Receive initial settings and change notifications |

### Host Compatibility

| Host Version | Behavior |
|--------------|----------|
| Old hosts (no settings support) | `HostSettingsManager.Current` will be null |
| New hosts | Full settings support |

Extensions should always check `HostSettingsManager.IsAvailable` or handle null `Current` values.

## Future Considerations

### Adding New Settings

To add a new setting property:

1. Add property to `IHostSettings` interface in IDL
2. Add property to `HostSettings.cs` class in Toolkit
3. Update `HostSettingsConverter.ToHostSettings()` to include the new property

### Extension-Specific Settings

Future versions could allow extensions to register their own settings that the host would store and provide back, enabling a unified settings experience.

### Bi-Directional Settings

Currently, settings flow one-way from host to extension. A future enhancement could allow extensions to request settings changes (with user consent).
