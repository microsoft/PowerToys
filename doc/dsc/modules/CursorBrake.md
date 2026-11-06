---
description: DSC configuration reference for PowerToys CursorBrake module
ms.date:     10/13/2026
ms.topic:    reference
title:       CursorBrake Module
---

# CursorBrake Module

## Synopsis

Manages configuration for the CursorBrake utility, which adds resistance
where two monitors meet.

## Description

The `CursorBrake` module configures PowerToys CursorBrake, a Mouse
Utilities sub-module. The cursor stops at an edge shared by two monitors
until you push past it for a set distance. Moving back from the
edge drains the push. Holding Ctrl, or Shift if `cross_modifier` is 1,
crosses freely. Outer edges and single-monitor setups are left untouched.

## Properties

The CursorBrake module supports the following configurable properties.
Each property is set by its JSON key, and its value is wrapped in an object
with a single `value` field (for example, `push_distance: { value: 300 }`).

### push_distance

Sets how far you push past an edge before the cursor crosses, in pixels at
100% scale. The distance scales with the DPI of the monitor the cursor
leaves. `0` turns the resistance off.

**Type:** integer  
**Range:** `0` to `1000` (a hand-edited value is clamped to this range)  
**Default:** `300`

### free_return

Controls whether the cursor can cross straight back over an edge it just
crossed without resistance.

**Type:** boolean  
**Default:** `true`

### edge_filter

Selects which edges between monitors resist the cursor.

**Type:** integer  
**Allowed values:**

- `0` - All edges between monitors
- `1` - Side-by-side only
- `2` - Stacked only

**Default:** `0`

### disable_while_dragging

Controls whether the cursor crosses freely while a mouse button is held.

**Type:** boolean  
**Default:** `false`

### cross_modifier

Selects the key that crosses any edge freely while held.

**Type:** integer  
**Allowed values:**

- `0` - Ctrl
- `1` - Shift

**Default:** `0`

### fullscreen_behavior

Selects what the edges of a monitor do while a full-screen app or game fills
it. A window is full screen when it covers its whole monitor and has no title
bar. A window that covers more than one monitor is not, nor is a tool window
or one of PowerToys' own overlays, such as Screen Ruler and Text Extractor.
Only moves that leave that monitor change; entering it, and moves between
other monitors, behave as usual.

**Type:** integer  
**Allowed values:**

- `0` - Same as other apps
- `1` - Wall: the cursor leaves the monitor only while the `cross_modifier`
  key is held, even while dragging with `disable_while_dragging` on, and
  whatever `edges` sets for that monitor
- `2` - No resistance: the cursor leaves the monitor without resistance,
  walls set in `edges` included

**Default:** `0`

### excluded_apps

Lists apps that turn CursorBrake off while they are in front, walls
included. Put one application name per line, such as `outlook.exe`. Case
does not matter, and lines of only `\` or `/` are ignored. A name matches
when its last place in the app's full path reaches the first character of
the file name or the backslash before it, so `game` matches `game2.exe`
and `games\game` matches `C:\Games\Game.exe`. Otherwise a name matches when
the app's window title contains it, as in Find My Mouse.

**Type:** string  
**Default:** `""`

### edges

Gives single edges their own behavior. Each entry names two monitors and
sets how a crossing between them behaves. An edge without an entry uses
`push_distance` and `edge_filter`. Holding the `cross_modifier` key crosses
every edge, walls included.

**Type:** array of objects  
**Default:** `[]`

Each entry has these fields:

| Field | Type | Meaning |
|---|---|---|
| `monitors` | 2 strings | The two monitors, by monitor ID. |
| `one_way` | boolean, optional | `true` covers only crossings from the first monitor to the second. Absent or `false` covers both directions. |
| `mode` | integer | `0` Custom, `1` Off, `2` Wall. |
| `push_distance` | integer | The distance Custom uses, `0` to `1000`. Off and Wall keep it. |

- **Custom** brakes with the entry's `push_distance`.
- **Off** lets the cursor through, as `edge_filter` does for the edges it
  leaves out.
- **Wall** holds the cursor until the `cross_modifier` key is held.

A one-way entry for a direction outranks a both-ways entry for the same
two monitors. Of two entries for one direction, the first applies.

Entries name monitors that share an edge. Where two monitors meet only at a
corner, a move between them passes the two edges of the first monitor at
that corner and takes the stricter of their behaviors: Wall, then the
larger distance, then Off. A wall on either edge holds the corner.

A monitor with a wall out to any neighbor also keeps the cursor when a move
would leave it for no monitor, such as a gap between monitors, because
Windows can place such a cursor on the walled neighbor. Without a wall out,
Windows handles those moves.

The module reads the first 256 entries and skips an entry with an ID longer
than 512 characters or a field of the wrong type. A field given twice takes
its last value, and unknown fields are ignored. Settings loads the list the
same way. Monitor IDs compare without regard to the case of ASCII letters
only.

A number outside the range of a double, such as `1e400`, makes the module
reject the whole settings file and keep its previous settings, while
Settings drops only that entry. JSON nested deeper than 64 levels fails the
whole file in Settings, which then writes the defaults. Neither shape is one
DSC can write.

Monitor IDs are machine-specific: `\\?\DISPLAY#<EDID code>#<instance>`,
where the instance follows the port the monitor is connected to. Entries
applied on another machine, or for a monitor that is not connected, stay
dormant. Settings removes the entries for a pair of monitors 180 days after
the module last saw them next to each other. For a pair the module never
saw, the 180 days start when the module first reads the entry, and an
entry naming the pair never extends them. While the system clock runs more
than a day behind the last time the module recorded, no pair's 180 days run
out.

## Examples

### Example 1 - Configure strength with direct execution

This example sets a higher strength.

```powershell
$config = @{
    settings = @{
        properties = @{
            push_distance = @{ value = 500 }
        }
        name = "CursorBrake"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module CursorBrake --input $config
```

### Example 2 - Brake only side-by-side edges with DSC

This example limits the resistance to edges between monitors placed next to
each other and uses Shift to cross freely.

```bash
dsc config set --file cursorbrake-side-by-side.dsc.yaml
```

```yaml
# cursorbrake-side-by-side.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure CursorBrake edges
    type: Microsoft.PowerToys/CursorBrakeSettings
    properties:
      settings:
        properties:
          edge_filter:
            value: 1
          cross_modifier:
            value: 1
        name: CursorBrake
        version: 1.0
```

### Example 3 - Wall one edge in one direction

This example keeps the cursor from leaving the second monitor for the
first unless the `cross_modifier` key is held. The other direction keeps the global
strength. Replace the IDs with the ones on your machine. The module lists
the connected monitors and their IDs in `detected-edges.json`, next to its
`settings.json`.

```yaml
# cursorbrake-one-way-wall.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Wall one edge
    type: Microsoft.PowerToys/CursorBrakeSettings
    properties:
      settings:
        properties:
          edges:
            value:
              - monitors:
                  - \\?\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354
                  - \\?\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353
                one_way: true
                mode: 2
                push_distance: 300
        name: CursorBrake
        version: 1.0
```

### Example 4 - Keep the cursor in full-screen games

This example walls every edge of a monitor while a full-screen app or game
fills it, and leaves CursorBrake off while a remote desktop client is in
front.

```yaml
# cursorbrake-full-screen.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Wall full-screen apps
    type: Microsoft.PowerToys/CursorBrakeSettings
    properties:
      settings:
        properties:
          fullscreen_behavior:
            value: 1
          excluded_apps:
            value: "mstsc.exe"
        name: CursorBrake
        version: 1.0
```

### Example 5 - Install and configure with WinGet

This example installs PowerToys and configures CursorBrake to brake every
crossing, including the way back.

```bash
winget configure winget-cursorbrake.yaml
```

```yaml
# winget-cursorbrake.yaml
$schema: https://raw.githubusercontent.com/PowerShell/DSC/main/schemas/2023/08/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Install PowerToys
    type: Microsoft.WinGet.DSC/WinGetPackage
    properties:
      id: Microsoft.PowerToys
      source: winget

  - name: Configure CursorBrake
    type: Microsoft.PowerToys/CursorBrakeSettings
    properties:
      settings:
        properties:
          push_distance:
            value: 300
          free_return:
            value: false
        name: CursorBrake
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [FindMyMouse][03]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./FindMyMouse.md
