# Tray Palette

Tray Palette provides quick access to pinned Command Palette commands from the system tray.

## Use quick actions

In Command Palette settings, set the tray icon action to **Open quick actions**.
This is the default.
Select **Open command palette** to restore the previous tray icon behavior.

To pin a command, open its context menu and select **Pin to...** > **Tray menu**.
Commands must have an ID that their provider can resolve.
Home, Dock, and Tray menu pins are independent.
The Dock destination is available when the dock is enabled.

Quick actions show three buttons per row, with the command title below each icon.
Long titles end with an ellipsis.
Hover over a button to see its full title.
Additional pins create additional rows.
When the window reaches the monitor's available height, the grid scrolls.

Select a command button to run it.
A chevron identifies a page button.
Select a page button to open the page in the tray window.
Back from the first page returns to quick actions.
Nested pages use the same navigation and command handling as dock pages.

Drag buttons to change their order.
The new order is saved when you drop a button.
To remove a pin, find the command in Command Palette and select **Unpin from tray menu**.

## Default pins and persistence

The default pins are Network, Bluetooth, and Snipping Tool.
If Snipping Tool is unavailable, its button does not appear.
Unavailable commands remain in the saved list and can appear when their providers become available.
An empty saved list stays empty.

`SettingsModel.TrayIconClickAction` controls the tray icon action.
`SettingsModel.TrayPalette.Commands` stores ordered provider and command ID pairs.
Packaged apps use an `aumid:` alias so their pins do not depend on translated app names.
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

1. Open quick actions with the default pins.
2. Invoke Network, Bluetooth, and Snipping Tool when available.
3. Pin a fourth command and confirm that it starts a new row.
4. Pin a page, open it, and use Back to return to quick actions.
5. Open a nested page and confirm that Back first returns to its parent.
6. Drag a command, restart Command Palette, and confirm the saved order.
7. Remove every pin and confirm that defaults do not return after restart.
8. Switch the tray icon action to **Open command palette** and confirm the previous behavior.
9. Check light dismissal, Escape, keyboard activation, and work-area positioning on each monitor.
