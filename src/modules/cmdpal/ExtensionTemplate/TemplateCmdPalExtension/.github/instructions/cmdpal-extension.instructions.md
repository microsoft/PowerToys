---
description: 'Comprehensive guide for developing Command Palette extensions — covers pages, content, commands, items, icons, settings, dock, and debugging'
applyTo: '**/*.cs'
---

# Command Palette Extension Development

Complete reference for building Command Palette (CmdPal) extensions. Extensions run out-of-process as MSIX-packaged COM servers.

## Extension Architecture

### IExtension Interface

The root class implements `IExtension` and `IDisposable`:

```csharp
[Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
public sealed partial class MyExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly MyCommandsProvider _provider = new();

    public MyExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType) => providerType switch
    {
        ProviderType.Commands => _provider,
        _ => null,
    };

    public void Dispose() => _extensionDisposedEvent.Set();
}
```

- Only `ProviderType.Commands` is currently supported
- The `[Guid]` must match the CLSID in `Package.appxmanifest`

### CommandProvider

Override `TopLevelCommands()` to register main commands. Optionally override `FallbackCommands()` and `GetDockBands()`:

```csharp
public partial class MyCommandsProvider : CommandProvider
{
    public MyCommandsProvider()
    {
        DisplayName = "My Extension";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override ICommandItem[] TopLevelCommands() => [
        new CommandItem(new MyPage()) { Title = DisplayName },
    ];
}
```

### COM Server (Program.cs)

`Program.cs` hosts the COM server. Do not change this pattern:

```csharp
public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            global::Shmuelie.WinRTServer.ComServer server = new();
            ManualResetEvent extensionDisposedEvent = new(false);
            var extensionInstance = new MyExtension(extensionDisposedEvent);
            server.RegisterClass<MyExtension, IExtension>(() => extensionInstance);
            server.Start();
            extensionDisposedEvent.WaitOne();
            server.Stop();
            server.UnsafeDispose();
        }
    }
}
```

### Package.appxmanifest

Two critical extension registrations must be present:

1. **COM server** — `com:ComServer` with matching CLSID and `-RegisterProcessAsComServer` args
2. **App extension** — `uap3:AppExtension` with `Name="com.microsoft.commandpalette"` and `CreateInstance ClassId` matching the GUID

The CLSID must be identical in three places: the `[Guid]` attribute, the `com:Class Id`, and the `CreateInstance ClassId`.

## Page Types

### ListPage (Most Common)

Displays a searchable list of items:

```csharp
internal sealed partial class MyPage : ListPage
{
    public MyPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "My page";
        Name = "Open";
    }

    public override IListItem[] GetItems() => [
        new ListItem(new OpenUrlCommand("https://example.com")) { Title = "Example" },
    ];
}
```

### DynamicListPage (Search-Reactive)

Responds to search text changes for filtering or live queries:

```csharp
internal sealed partial class MyDynamicPage : DynamicListPage
{
    private IListItem[] _filteredItems = [];

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _filteredItems = _allItems
            .Where(i => i.Title.Contains(newSearch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems() => _filteredItems;
}
```

- Supports `Filters` property for category filtering
- Call `RaiseItemsChanged()` after updating items to notify the UI

### ContentPage (Rich Content)

Displays rich content like markdown, forms, or images:

```csharp
internal sealed partial class MyContentPage : ContentPage
{
    public override IContent[] GetContent() => [
        new MarkdownContent("# Hello\nThis is **markdown**."),
    ];
}
```

- Can return multiple `IContent` items (mix markdown, forms, images, etc.)
- Supports `Commands` property for context menu items via `CommandContextItem`

## Content Types

| Type | Description |
|------|-------------|
| `MarkdownContent(string)` | Renders markdown with headers, links, code blocks, tables, images |
| `FormContent` | Adaptive Cards forms with `TemplateJson`, optional `DataJson`, and `SubmitForm()` |
| `PlainTextContent(string)` | Plain text; optional `FontFamily.Monospace` and `WrapWords` |
| `ImageContent` | Images with `MaxWidth`/`MaxHeight` constraints |
| `TreeContent` | Hierarchical nested content; override `GetChildren()` for child `IContent[]` |

### MarkdownContent Images

Supports `file:`, `data:` (base64), and `https:` URLs. Image hints control rendering:

```markdown
![alt](https://example.com/img.png?--x-cmdpal-fit=fit&--x-cmdpal-maxwidth=400)
```

### FormContent (Adaptive Cards)

```csharp
internal sealed partial class MyForm : FormContent
{
    public MyForm()
    {
        TemplateJson = """{ "type": "AdaptiveCard", ... }""";
        DataJson = """{ "name": "default" }""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var data = JsonSerializer.Deserialize<MyFormData>(payload);
        return CommandResult.Dismiss();
    }
}
```

- Design cards visually at [adaptivecards.io/designer](https://adaptivecards.io/designer)
- Use `${...}` placeholders in `TemplateJson` bound to `DataJson` properties

## Commands

### InvokableCommand

Actions that do something when activated:

```csharp
internal sealed partial class MyCommand : InvokableCommand
{
    public override string Name => "Do it";
    public override IconInfo Icon => new("\uE945");

    public override CommandResult Invoke()
    {
        // Do work here
        return CommandResult.Dismiss();
    }
}
```

### Built-in Command Helpers

| Helper | Purpose |
|--------|---------|
| `OpenUrlCommand(string url)` | Open URL in default browser |
| `CopyTextCommand(string text)` | Copy to clipboard with toast |
| `NoOpCommand()` | Does nothing (placeholder) |
| `AnonymousCommand(Action? action)` | Lambda command; set `Result` property for navigation |

### CommandResult Types

| Result | Behavior |
|--------|----------|
| `CommandResult.Dismiss()` | Hide palette, go home |
| `CommandResult.KeepOpen()` | Stay on current page |
| `CommandResult.Hide()` | Hide palette, keep page state |
| `CommandResult.GoBack()` | Navigate back one page |
| `CommandResult.GoHome()` | Navigate to home page |
| `CommandResult.ShowToast("msg")` | Show toast notification, then dismiss |
| `CommandResult.Confirm(args)` | Show confirmation dialog before proceeding |

## ListItem Properties

```csharp
new ListItem(command)
{
    Title = "Display name",
    Subtitle = "Secondary text",
    Icon = new IconInfo("\uE8A7"),
    Tags = [new Tag("label") { Foreground = ColorHelpers.FromRgb(255, 0, 0) }],
    Details = new Details
    {
        Title = "Detail panel",
        Body = "**Markdown** body",
        HeroImage = IconHelpers.FromRelativePath("Assets\\hero.png"),
        Size = ContentSize.Medium,
        Metadata = [
            new DetailsLink("URL", "https://example.com"),
            new DetailsSeparator(),
        ],
    },
    MoreCommands = [
        new CommandContextItem(deleteCommand)
        {
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                true, false, false, (int)VirtualKey.Delete),
        },
    ],
}
```

## Sections and Grid Layouts

### Sections

Group items under section headers:

```csharp
public override ISection[] GetSections() => [
    new Section { Title = "Group A", Items = itemsA },
    new Section { Title = "Group B", Items = itemsB },
];
```

### Grid Layouts

Set `GridProperties` on a `ListPage`:

| Layout | Description |
|--------|-------------|
| `GalleryGridLayout()` | Large tiles with title + subtitle |
| `SmallGridLayout()` | Compact grid |
| `MediumGridLayout()` | Medium tiles with title |

## Icons

```csharp
// Segoe Fluent UI icons (most common)
new IconInfo("\uE8A5")                                    // Document
new IconInfo("\uE945")                                    // Lightning bolt

// Emoji
new IconInfo("📂")

// Image from package assets
IconHelpers.FromRelativePath("Assets\\StoreLogo.png")

// Remote URL or SVG
new IconInfo("https://example.com/icon.svg")

// From exe/dll resource
new IconInfo("%systemroot%\\system32\\shell32.dll,3")
```

## Dynamic Updates

- Call `RaiseItemsChanged()` on any page to trigger a UI refresh of its items
- Call `RaisePropertyChanged(propertyName)` for individual property updates (e.g., title)
- For top-level command changes, call `RaiseItemsChanged()` on the `CommandProvider`
- Use `System.Timers.Timer` for periodic background updates

## Status Messages and Toasts

```csharp
// Inline status message (e.g., loading indicator)
var msg = new StatusMessage
{
    Message = "Loading...",
    State = MessageState.Info,
    Progress = new ProgressState { IsIndeterminate = true },
};
ExtensionHost.ShowStatus(msg, StatusContext.Page);
ExtensionHost.HideStatus(msg);

// Transient toast notification
new ToastStatusMessage("Copied to clipboard").Show();
```

## Build & Debug

1. Select **Debug** configuration
2. **Deploy** via Build > Deploy (not just Build) — this registers the MSIX package
3. Press **F5** to launch with debugger attached
4. Use `Debug.Write()` / `Debug.WriteLine()` for diagnostic output
5. Check Output window (**Ctrl+Alt+O**) set to "Debug"
6. In Command Palette, run `Reload` → "Reload Command Palette extensions"

Use the `(Package)` launch profile, not `(Unpackaged)`.

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Building without deploying | Use Build > Deploy so the MSIX package is updated |
| Running "(Unpackaged)" profile | Select the "(Package)" launch profile |
| Forgetting to reload extensions | Run `Reload` in Command Palette after deploying |
| CLSID mismatch | Ensure `[Guid]` in .cs matches `ClassId` in Package.appxmanifest (both places) |
| Logging in hot paths | `GetItems()` is called frequently — avoid expensive work or logging here |
