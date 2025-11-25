# Command Palette Extension Toolkit

The C# toolkit for building your own extension for the Command Palette.

The quickest way to get started building an extension is to install the Command
Palette, and use the "Create a new extension" command. That will set up a
project for you, with the packaging, dependencies, and basic program structure
ready to go.

To view the full docs, you can head over to [our docs site](https://go.microsoft.com/fwlink/?linkid=2310639)

There are samples of just about everything you can do in [the samples project].
Head over there to see basic usage of the APIs.

## Key Features

The Toolkit provides helper classes for common extension patterns:

- **CommandProvider** - Base class for implementing command providers
- **ListPage / ContentPage** - Pre-built page types for displaying content
- **Settings helpers** - Easy-to-use settings management
- **HostSettingsManager** - Access Command Palette's global configuration settings

### Host Settings Awareness

Extensions can read Command Palette's global configuration through
`HostSettingsManager.Current`:

```csharp
using Microsoft.CommandPalette.Extensions.Toolkit;

// Get current host settings
var settings = HostSettingsManager.Current;

// Access configuration values
var hotkey = settings.Hotkey;                // e.g., "Win+Alt+Space"
var disableAnimations = settings.DisableAnimations;
var singleClick = settings.SingleClickActivates;
```

See the `HostSettingsPage` in the samples project for a complete example.

[the samples project]: https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal/ext/SamplePagesExtension
