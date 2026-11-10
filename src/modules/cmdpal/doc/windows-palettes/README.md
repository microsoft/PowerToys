# Windows Palettes provider

The Windows Palettes provider is a built-in Command Palette provider.
It translates sources from the `Windows.UI.Palettes` API into Command Palette pages and commands.

Command Palette registers the provider only when version 2.0 of the API is available.
If the API is not available, the provider is absent from settings and command lists.
Command Palette initializes the catalog on a worker thread after startup.

The provider uses these build projects:

- `Windows.UI.Palettes` compiles the vendored IDL into a WinMD file.
- `Windows.UI.Palettes.Projection` uses CsWinRT to generate the C# projection.
- `Microsoft.CmdPal.Ext.WindowsPalettes` contains the in-process provider.

For implementation details, read these documents:

- [WUP to CmdPal patterns](wup-to-cmdpal-patterns.md)
- [WUP to CmdPal reference](wup-to-cmdpal-reference.md)
