# Introduction
Super FancyZones! Just like FancyZones except super.

# Getting Started
Grab FancyZones.exe from the bin directory or \\\\wexfs\users\bretan\proto\superfancyzones and run it

## Dragging windows
* While dragging a window around, zones will appear that the window can be dropped in. Dropping the window in a zone will position it in the zone.
* While dragging a window, you can hit number keys to cycle the active zone set. Eg, drag a window around and hit the '3' key will change to a zone set with 3 zones.

## Hotkeys
* Win+~ - opens the zone viewer/editor
* Win+ctrl+number - cycle through layouts with the corresponding number of zones (only if Override snap hotkeys setting is enabled)
* Win+left arrow, Win+right arrow - move foreground window between zones

## Zone Viewer/Editor (Win + ~)
* Hitting a number key cycles through layouts matching the number of zones (eg 3 cycles through layouts with 3 zones)
* R resets the focused monitor back to defaults
* C clears the current layout so you can start fresh
* W opens a dialog to choose wallpaper
* Left click moves the zone to the top
* Right click moves the zone to the bottom

### E enters editor mode (hit E or Escape to exit editor mode)
* Left/Right/Up/Down arrows adjust the grid spacing
* PgUp/PgDn adjust grid margins
* Ctrl+left click splits the clicked zone in half horizontally
* Ctrl+right click splits the clicked zone in half vertically

# Options
### Default Drag Mode
* None - don't do anything when dragging windows around (shift enters normal mode, ctrl enters adjusted mode)
* Normal - show zones when dragging windows around (shift disables, ctrl enters adjusted mode)
* Adjusted - show zones when dragging windows around with an accelerated cursor

### Display change
* Move windows - automatically move windows around when display changes

### Virtual Desktop change
* Move windows - automatically move windows around when virtual desktop changes
* Change wallpaper - use custom wallpaper per-monitor per-desktop
* Flash zones - flash zones on each monitor

### Miscellanious
* Override snap hotkeys - steal hotkeys normally used by shell (win+left/right, win+ctrl+num)
* Colorful zones - use colored zones in zone viewer

# Known issues
* See open bugs for full list of issues
* Win+left and Win+right don't move between monitors
* If you use Virtual Desktops, make sure to perform at least one virtual desktop switch before launching the fancyzones (it relies on a volatile regkey that explorer writes)
* Sometimes you have to click on a zone viewer window before it gets keyboard focus when opening views with Win+~
* Quickly switching virtual desktops with win+ctrl+arrow hotkeys can crash fancyzones