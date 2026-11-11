# Tray menu

The tray menu provides quick access to pinned Command Palette commands from the system tray.

## Use the tray menu

In Command Palette settings, set the tray icon action to **Open tray menu**.
This is the default.
Select **Open command palette** to restore the previous tray icon behavior.

To pin a command, open its context menu and select **Pin to...** > **Tray menu**.
Commands must have an ID that their provider can resolve.
Home, Dock, and Tray menu pins are independent.
The Dock destination is available when the dock is enabled.

The tray menu shows three buttons per row, with the command title below each icon.
Long titles end with an ellipsis.
Hover over a button to see its full title.
Additional pins create additional rows.
The footer has buttons that open Command Palette and Command Palette settings.
When the window reaches the monitor's available height, the grid scrolls.

Select a command button to run it.
A chevron identifies a page button.
Select a page button to open the page in the tray window.
Back from the first page returns to the tray menu.
Nested pages use the same navigation and command handling as dock pages.
Alt+F4 does nothing in the tray window.

Drag buttons to change their order.
The new order is saved when you drop a button.
Right-click a quick-action button to open its command context menu.
With a button focused, you can also press Shift+F10 or the context menu key.
Select **Unpin from tray menu** to remove it from the tray menu.
The same unpin action remains available in Command Palette.

## Default pins and persistence

The default pins are the Clipboard History page and the command that finds and installs extensions.
If a pinned command is unavailable, its button does not appear.
Unavailable commands remain in the saved list and can appear when their providers become available.
Each command has a 10-second load timeout.
A timeout shows a load error and preserves the saved pin.
An empty saved list stays empty.

`SettingsModel.TrayIconClickAction` controls the tray icon action.
`SettingsModel.TrayPalette.Commands` stores ordered provider and command ID pairs.
Packaged apps use their AppUserModelIDs as command IDs, so their pins do not depend on translated app names.
Unpackaged apps retain their name-and-hash command IDs.
Existing packaged-app pins that use older command IDs must be pinned again.
Windows Settings commands use their `ms-settings:` destinations as IDs.

Reordering changes the slots occupied by visible commands.
It preserves the saved positions of unavailable commands.

## Implementation

- `TrayPaletteWindow` owns the WinUIEx window, grid, tray positioning, and drag events.
- `TrayPaletteViewModel` resolves pins and owns the resulting command item view models.
- `DockPageFlyoutController` routes page commands and results for both dock and tray hosts.
- `FlyoutDockPageHost` preserves the dock's existing Flyout presentation.
- `TrayDockPageHost` presents the same page control inside the tray window.

## Manual checks

1. Open the tray menu with the default pins.
2. Open Clipboard History and the extension gallery from their buttons.
3. Pin a fourth command and confirm that it starts a new row.
4. Pin a page, open it, and use Back to return to the tray menu.
5. Open a nested page and confirm that Back first returns to its parent.
6. Drag a command, restart Command Palette, and confirm the saved order.
7. Remove every pin and confirm that defaults do not return after restart.
8. Switch the tray icon action to **Open command palette** and confirm the previous behavior.
9. Check light dismissal, Escape, keyboard activation, and work-area positioning on each monitor.
10. Open a tile's context menu with the mouse and keyboard, then invoke a command or remove its pin.
11. Press Alt+F4 in the tray menu and in a page. Confirm that the window stays open.
12. Paste from Clipboard History or Calculator and confirm that the previous app receives the text.
13. Disable the tray icon, then enable it. Confirm that the tray menu opens again.
