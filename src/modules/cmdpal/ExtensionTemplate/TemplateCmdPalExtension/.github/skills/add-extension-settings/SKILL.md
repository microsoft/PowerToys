---
name: add-extension-settings
description: >-
  Add a settings page to your Command Palette extension.
  Use when asked to add settings, preferences, configuration options,
  toggles, text inputs, dropdowns, or user-customizable behavior.
  Covers ToggleSetting, TextSetting, ChoiceSetSetting, and persistence.
---

# Add Extension Settings

Add a settings page to your Command Palette extension using the built-in settings helpers. Settings are automatically persisted and restored by the extension host.

## When to Use This Skill

- Adding user-configurable options to your extension
- Creating toggle switches for features
- Adding text input fields for configuration
- Creating dropdown menus for option selection
- Persisting user preferences across sessions

## Quick Start

### Step 1: Create a Settings Manager

Create a new file `SettingsManager.cs`:

```csharp
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace YourExtension;

internal sealed class SettingsManager
{
    private readonly Settings _settings;

    public SettingsManager()
    {
        _settings = new Settings();

        var maxResults = new TextSetting(
            "maxResults",
            "Maximum Results",
            "Maximum number of results to display",
            "10");

        var showSubtitles = new ToggleSetting(
            "showSubtitles",
            "Show Subtitles",
            "Display subtitle text under each result",
            true);

        var sortOrder = new ChoiceSetSetting(
            "sortOrder",
            "Sort Order",
            "How to sort results",
            [
                new ChoiceSetSetting.Choice("Alphabetical", "alpha"),
                new ChoiceSetSetting.Choice("Most Recent", "recent"),
                new ChoiceSetSetting.Choice("Most Used", "frequent"),
            ],
            "alpha");

        _settings.AddSetting(maxResults);
        _settings.AddSetting(showSubtitles);
        _settings.AddSetting(sortOrder);

        // React to settings changes
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public ICommandSettings Settings => _settings;

    public int MaxResults => int.TryParse(
        _settings.GetSetting<string>("maxResults"), out var val) ? val : 10;

    public bool ShowSubtitles =>
        _settings.GetSetting<bool>("showSubtitles");

    public string SortOrder =>
        _settings.GetSetting<string>("sortOrder") ?? "alpha";

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // React to settings changes (e.g., refresh data)
    }
}
```

### Step 2: Wire into CommandProvider

In your `CommandsProvider`, expose the settings:

```csharp
public partial class MyCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();
    private readonly ICommandItem[] _commands;

    public MyCommandsProvider()
    {
        DisplayName = "My Extension";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings = _settingsManager.Settings; // This exposes settings to CmdPal
        _commands = [
            new CommandItem(new MyPage(_settingsManager)) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
```

### Step 3: Use Settings in Pages

```csharp
internal sealed partial class MyPage : ListPage
{
    private readonly SettingsManager _settings;

    public MyPage(SettingsManager settings)
    {
        _settings = settings;
    }

    public override IListItem[] GetItems()
    {
        var items = GetAllItems();
        return items.Take(_settings.MaxResults).ToArray();
    }
}
```

## Setting Types

| Type | UI Control | Value Type | Constructor Parameters |
|------|-----------|------------|----------------------|
| `ToggleSetting` | Toggle switch | `bool` | `(id, label, description, defaultValue)` |
| `TextSetting` | Text input | `string` | `(id, label, description, defaultValue)` |
| `ChoiceSetSetting` | Dropdown | `string` | `(id, label, description, choices[], defaultValue)` |

## Key Points

- Settings are automatically persisted by the CmdPal host
- Use `SettingsChanged` event to react to changes in real-time
- Access values via `GetSetting<T>(id)` with the setting's string id
- Pass the settings manager to pages/commands that need configuration
- Settings page appears automatically when `Settings` is set on `CommandProvider`

## Grouping Settings

For extensions with many settings, organize them into logical groups:

```csharp
public SettingsManager()
{
    _settings = new Settings();

    // Appearance group
    var theme = new ChoiceSetSetting("theme", "Theme", "UI theme",
        [
            new ChoiceSetSetting.Choice("Light", "light"),
            new ChoiceSetSetting.Choice("Dark", "dark"),
            new ChoiceSetSetting.Choice("System", "system"),
        ],
        "system");

    var fontSize = new TextSetting("fontSize", "Font Size", "Display font size", "14");

    // Behavior group
    var autoRefresh = new ToggleSetting("autoRefresh", "Auto-Refresh",
        "Automatically refresh results", true);

    var refreshInterval = new TextSetting("refreshInterval", "Refresh Interval",
        "Seconds between auto-refreshes", "30");

    _settings.AddSetting(theme);
    _settings.AddSetting(fontSize);
    _settings.AddSetting(autoRefresh);
    _settings.AddSetting(refreshInterval);
}
```

## Reacting to Changes

Use the `SettingsChanged` event to update behavior when the user modifies settings:

```csharp
private void OnSettingsChanged(object? sender, EventArgs e)
{
    // Invalidate cached data
    _cachedItems = null;

    // Notify pages to refresh
    OnItemsChanged?.Invoke(this, EventArgs.Empty);
}
```

## Documentation

- [SampleSettingsPage.cs](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/ext/SamplePagesExtension/Pages/SampleSettingsPage.cs)
