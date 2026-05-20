---
name: add-dock-band
description: >-
  Add dock band support to your Command Palette extension for persistent toolbar widgets.
  Use when asked to add dock support, toolbar buttons, persistent UI widgets,
  taskbar integration, live-updating status displays, quick-access buttons,
  or always-visible controls. Supports single buttons, multi-button strips,
  and live-updating content.
---

# Add Dock Band Support

The Command Palette Dock is a persistent toolbar at the edge of the user's screen. Your extension can provide **dock bands** — strips of items that appear in the Dock — giving users quick access to commands without opening the full Command Palette.

## When to Use This Skill

- Adding a quick-access button to the persistent toolbar
- Creating a multi-button toolbar strip
- Displaying live-updating information (clock, CPU usage, etc.)
- Providing frequently-used commands without opening the full palette

## Prerequisites

- Command Palette Extension SDK version 0.9 or later (`Microsoft.CommandPalette.Extensions` ≥ 0.9.260303001)

## Quick Start: Single Button Dock Band

Override `GetDockBands()` in your `CommandProvider`:

```csharp
public partial class MyCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem _dockBand;

    public MyCommandsProvider()
    {
        DisplayName = "My Extension";
        Id = "com.mycompany.myextension"; // Unique ID required for dock

        var mainPage = new MyPage();
        _dockBand = new CommandItem(mainPage) { Title = DisplayName };
        _commands = [new CommandItem(mainPage) { Title = DisplayName }];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => [_dockBand];
}
```

## Multi-Button Dock Band

Use `WrappedDockItem` to create a band with multiple buttons:

```csharp
public override ICommandItem[]? GetDockBands()
{
    var button1 = new ListItem(new OpenUrlCommand("https://github.com"))
    {
        Title = "GitHub",
        Icon = new IconInfo("\uE774"),
    };
    var button2 = new ListItem(new OpenUrlCommand("https://learn.microsoft.com"))
    {
        Title = "Learn",
        Icon = new IconInfo("\uE82D"),
    };

    var band = new WrappedDockItem(
        [button1, button2],
        "com.mycompany.myextension.quicklinks", // Unique band ID
        "Quick Links");

    return [band];
}
```

## Live-Updating Dock Band

Create a dock band that updates its content periodically (like a clock):

```csharp
internal sealed partial class LiveStatusBand : ListItem
{
    private readonly System.Timers.Timer _timer;

    public LiveStatusBand()
        : base(new NoOpCommand() { Result = CommandResult.KeepOpen() })
    {
        Title = DateTime.Now.ToString("HH:mm");
        Icon = new IconInfo("\uE823"); // Clock icon

        _timer = new System.Timers.Timer(60_000); // Update every minute
        _timer.Elapsed += (s, e) =>
        {
            Title = DateTime.Now.ToString("HH:mm");
            Subtitle = DateTime.Now.ToString("dddd, MMMM d");
        };
        _timer.Start();
    }
}

// In CommandProvider:
public override ICommandItem[]? GetDockBands()
{
    var band = new WrappedDockItem(
        [new LiveStatusBand()],
        "com.mycompany.myextension.status",
        "Live Status");
    return [band];
}
```

## How Dock Bands Render

| Command Type on ICommandItem | Dock Behavior |
|------------------------------|---------------|
| `IInvokableCommand` | Single button that executes the command |
| `IListPage` | Each list item renders as a separate button in one band |
| `IContentPage` | Single expandable button with a flyout |

## Support Pinning Nested Commands

By default, only top-level commands and dock bands can be pinned. To allow pinning nested commands:

```csharp
public override ICommandItem? GetCommandItem(string id)
{
    // Look up commands by their Id
    foreach (var item in GetAllCommands())
    {
        if (item?.Command is ICommand cmd && cmd.Id == id)
            return item;
    }
    return null;
}
```

## Important Notes

- All dock band `ICommandItem` objects must have a `Command` with a **non-empty `Id`** — items without an ID are ignored
- Set `Id` on your `CommandProvider` (e.g., `Id = "com.mycompany.myextension"`)
- Use `WrappedDockItem` for multi-button bands backed by a `ListPage`
- Keep dock band updates lightweight — they run frequently

## Documentation

- [Adding Dock support](https://learn.microsoft.com/windows/powertoys/command-palette/adding-dock-support)
