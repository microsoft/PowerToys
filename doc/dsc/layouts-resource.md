---
description: Reference for the PowerToys FancyZones layouts DSC resource
ms.date:     09/12/2026
ms.topic:    reference
title:       FancyZones Layouts Resource
---

# FancyZones Layouts Resource

## Synopsis

Manages the FancyZones layouts — custom layouts, layout templates, quick
layout hotkeys and default layouts — declaratively through DSC v3.

## Description

The `layouts` resource deploys the FancyZones layout data. While the
[`settings` resource][01] manages the module's settings (for FancyZones: the
enabled state and behavior options such as zone colors and activation keys),
the `layouts` resource manages the layouts themselves, which FancyZones stores
in separate files in `%LOCALAPPDATA%\Microsoft\PowerToys\FancyZones\`:

| Section     | File                    | Content                                                    |
| ----------- | ----------------------- | ---------------------------------------------------------- |
| `custom`    | `custom-layouts.json`   | The custom layouts created in the editor (canvas or grid). |
| `templates` | `layout-templates.json` | The settings of the built-in layout templates.             |
| `hotkeys`   | `layout-hotkeys.json`   | The quick layout hotkeys (`Win+Ctrl+Alt+<number>`).        |
| `defaults`  | `default-layouts.json`  | The layouts applied to new horizontal and vertical monitors. |

Layouts are written with friendly, camel-cased property names and layout
identifiers may be given with or without braces:

```yaml
layouts:
  custom:
    - uuid: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}"
      name: Two columns 70/30
      grid:
        rows: 1
        columns: 2
        rowsPercentage: [10000]
        columnsPercentage: [7000, 3000]
        cellChildMap: [[0, 1]]
  hotkeys:
    - { key: 1, layoutId: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}" }
```

The only supported module is `FancyZones`. The DSC resource type is
`Microsoft.PowerToys/FancyZonesLayouts`.

> **Important — replace semantics per section:** Every section is optional. A
> section that is present replaces the **whole** layout file it maps to with
> the declared state: layouts created in the FancyZones editor that are not
> part of the configuration are removed. A section that is omitted is left
> unchanged (not managed). Use `export` to capture the current layouts before
> switching a machine to declarative management.

Files that are machine-specific are not managed by this resource: the layouts
applied to each monitor (`applied-layouts.json`, keyed by monitor serial
numbers and virtual desktops) and the per-application zone history
(`app-zone-history.json`).

When the layouts are applied while PowerToys is running, FancyZones picks up
the changed files immediately; otherwise the layouts take effect the next time
PowerToys starts. Note that the layouts are only used when the FancyZones
utility is enabled (see the [FancyZones module][02]).

## Layouts schema

The `layouts` object is required. Each of its sections is optional.

### `custom[]` — custom layouts

| Property | Type   | Required | Description                                                                                                          |
|----------|--------|----------|----------------------------------------------------------------------------------------------------------------------|
| `uuid`   | string | yes      | The layout identifier, a GUID with or without braces. Must be unique. See [Layout identifiers](#layout-identifiers). |
| `name`   | string | yes      | The layout name shown in the editor.                                                                                 |
| `canvas` | object | one of   | Canvas layout definition (see below).                                                                                |
| `grid`   | object | one of   | Grid layout definition (see below).                                                                                  |

Exactly one of `canvas` or `grid` must be set; it determines the layout type.

#### `canvas` object

| Property            | Type     | Required | Description                                                                              |
|---------------------|----------|----------|------------------------------------------------------------------------------------------|
| `refWidth`          | integer  | yes      | Width, in pixels, of the work area the zones are defined for.                            |
| `refHeight`         | integer  | yes      | Height, in pixels, of the work area the zones are defined for.                           |
| `zones`             | object[] | yes      | The zones (1 to 128), each with `x`, `y`, `width` and `height` in pixels.                |
| `sensitivityRadius` | integer  | no       | Distance from a zone edge at which the zone is highlighted while dragging. Default `20`. |

Zones are scaled from the reference work area to the actual work area of the
monitor they are applied to.

#### `grid` object

| Property            | Type        | Required | Description                                                                                                    |
|---------------------|-------------|----------|----------------------------------------------------------------------------------------------------------------|
| `rows`              | integer     | yes      | Number of rows.                                                                                                |
| `columns`           | integer     | yes      | Number of columns.                                                                                             |
| `rowsPercentage`    | integer[]   | yes      | Height of each row in hundredths of a percent; one value per row, summing to `10000`.                          |
| `columnsPercentage` | integer[]   | yes      | Width of each column in hundredths of a percent; one value per column, summing to `10000`.                     |
| `cellChildMap`      | integer[][] | yes      | Zone index of each cell, one array per row with one value per column. Cells with the same index form one zone. |
| `showSpacing`       | boolean     | no       | Whether space is left between the zones. Default `true`.                                                       |
| `spacing`           | integer     | no       | Space between the zones, in pixels. Default `16`.                                                              |
| `sensitivityRadius` | integer     | no       | Distance from a zone edge at which the zone is highlighted while dragging. Default `20`.                       |

For example, a 70/30 two-column layout has `rows: 1`, `columns: 2`,
`rowsPercentage: [10000]`, `columnsPercentage: [7000, 3000]` and
`cellChildMap: [[0, 1]]`.

### `templates[]` — layout templates

| Property            | Type    | Required | Description                                                                                     |
|---------------------|---------|----------|-------------------------------------------------------------------------------------------------|
| `type`              | string  | yes      | `blank`, `focus`, `rows`, `columns`, `grid`, or `priority-grid`. One entry per type.            |
| `zoneCount`         | integer | no       | Number of zones. Default `3`.                                                                   |
| `showSpacing`       | boolean | no       | Whether space is left between the zones. Default `true`. Not applicable to `blank` and `focus`. |
| `spacing`           | integer | no       | Space between the zones, in pixels. Default `16`. Not applicable to `blank` and `focus`.        |
| `sensitivityRadius` | integer | no       | Distance from a zone edge at which the zone is highlighted. Default `20`.                       |

### `hotkeys[]` — quick layout hotkeys

| Property   | Type    | Required | Description                                                                                                            |
|------------|---------|----------|------------------------------------------------------------------------------------------------------------------------|
| `key`      | integer | yes      | The number key, `0` to `9`. Each key can be assigned to one layout.                                                    |
| `layoutId` | string  | yes      | The `uuid` of the custom layout to apply. Each layout can have one key. See [Layout identifiers](#layout-identifiers). |

When the configuration also defines `custom`, every `layoutId` must refer to
one of the declared custom layouts. When it does not, a reference to a layout
that does not exist on the machine is reported as a warning.

### `defaults` — default layouts

An object with the optional members `horizontal` and `vertical`, one per
monitor orientation. Each member is a layout:

| Property            | Type    | Required | Description                                                                                                                                |
|---------------------|---------|----------|--------------------------------------------------------------------------------------------------------------------------------------------|
| `type`              | string  | yes      | A template type (`blank`, `focus`, `rows`, `columns`, `grid`, `priority-grid`) or `custom`.                                                |
| `uuid`              | string  | custom   | The identifier of the custom layout. Required when `type` is `custom`, not allowed otherwise.                                              |
| `zoneCount`         | integer | no       | Number of zones of a template layout. Default `3`.                                                                                         |
| `showSpacing`       | boolean | no       | Whether space is left between the zones. Default `true` for grid templates; for a custom layout the setting of the referenced grid layout. |
| `spacing`           | integer | no       | Space between the zones, in pixels. Default `16` for grid templates; for a custom layout the setting of the referenced grid layout.        |
| `sensitivityRadius` | integer | no       | Distance from a zone edge at which the zone is highlighted. Default `20` for template layouts.                                             |

### Layout identifiers

Every custom layout is identified by a GUID (`uuid`). Hotkeys (`layoutId`)
and custom default layouts (`uuid`) refer to a custom layout by that GUID;
built-in templates have no identifier and are referred to by `type`.

Where the GUID comes from depends on what you are doing:

- **Declaring new layouts in the configuration:** choose any new GUID, for
  example with `New-Guid` in PowerShell or `uuidgen`, use it as the `uuid` of
  the custom layout, and use the same value wherever the layout is referenced.
  FancyZones does not generate identifiers for layouts deployed this way.
- **Referencing layouts that already exist on the machine** (created in the
  FancyZones editor, or deployed earlier): read the identifier from the current
  state. Any of the following shows it:

  ```powershell
  # The uuid of each custom layout, in the same shape as the configuration
  PowerToys.DSC.exe export --resource 'layouts' --module FancyZones

  # The FancyZones command line tool lists the layouts with their identifiers
  PowerToys.FancyZones.CLI.exe get-layouts
  ```

  The identifier is also the `uuid` property of the layout in
  `%LOCALAPPDATA%\Microsoft\PowerToys\FancyZones\custom-layouts.json`.

Identifiers may be written with or without braces and in any letter case; they
are normalized to the form the editor writes (upper-case, with braces), so
`5c4f1a20-9b3e-4c7d-8e2f-1a2b3c4d5e6f` and
`{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}` denote the same layout.

### Comparison

Omitted optional values are filled with the defaults listed above before the
desired state is compared with the current state. The order of the custom
layouts only affects the editor list and is not compared.

## Common operations

```powershell
# Get or export the current layouts
PowerToys.DSC.exe get --resource 'layouts' --module FancyZones
PowerToys.DSC.exe export --resource 'layouts' --module FancyZones

# Apply layouts (only the sections present in the input are replaced)
$input = '{"layouts":{"hotkeys":[{"key":1,"layoutId":"{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}"}]}}'
PowerToys.DSC.exe set --resource 'layouts' --module FancyZones --input $input

# Test whether the current layouts match the desired state
PowerToys.DSC.exe test --resource 'layouts' --module FancyZones --input $input

# Get the JSON schema of the resource
PowerToys.DSC.exe schema --resource 'layouts' --module FancyZones
```

## Examples

### Example 1 - Deploy layouts with Microsoft DSC

Save the following configuration as `fancyzones-layouts.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Deploy FancyZones layouts
    type: Microsoft.PowerToys/FancyZonesLayouts
    properties:
      layouts:
        custom:
          - uuid: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}"
            name: Two columns 70/30
            grid:
              rows: 1
              columns: 2
              rowsPercentage: [10000]
              columnsPercentage: [7000, 3000]
              cellChildMap: [[0, 1]]
              spacing: 8
          - uuid: "{8F0B6D3E-2C41-4A5B-9D7E-0F1A2B3C4D5E}"
            name: Reference window
            canvas:
              refWidth: 1920
              refHeight: 1080
              zones:
                - { x: 0, y: 0, width: 1280, height: 1080 }
                - { x: 1280, y: 0, width: 640, height: 1080 }
        templates:
          - { type: priority-grid, zoneCount: 3, showSpacing: true, spacing: 16 }
          - { type: focus, zoneCount: 4 }
        hotkeys:
          - { key: 1, layoutId: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}" }
          - { key: 2, layoutId: "{8F0B6D3E-2C41-4A5B-9D7E-0F1A2B3C4D5E}" }
        defaults:
          horizontal: { type: custom, uuid: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}" }
          vertical: { type: rows, zoneCount: 2 }
```

Apply the configuration with Microsoft DSC:

```bash
dsc config set --file fancyzones-layouts.dsc.config.yaml
```

### Example 2 - Install PowerToys and deploy layouts with WinGet

Save the following configuration as `fancyzones-layouts.dsc.config.winget`:

```yaml
# yaml-language-server: $schema=https://aka.ms/configuration-dsc-schema/0.2
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

  - name: Enable FancyZones
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          Enabled: true
        name: FancyZones
        version: 1.0

  - name: Deploy FancyZones layouts
    type: Microsoft.PowerToys/FancyZonesLayouts
    properties:
      layouts:
        custom:
          - uuid: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}"
            name: IDE and browser
            grid:
              rows: 1
              columns: 2
              rowsPercentage: [10000]
              columnsPercentage: [6000, 4000]
              cellChildMap: [[0, 1]]
        hotkeys:
          - { key: 1, layoutId: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}" }
```

Apply the configuration with WinGet:

```bash
winget configure fancyzones-layouts.dsc.config.winget
```

### Example 3 - Manage only the quick layout hotkeys

Sections that are not part of the configuration are left unchanged, so the
custom layouts created in the editor are kept:

```yaml
resources:
  - name: Assign quick layout hotkeys
    type: Microsoft.PowerToys/FancyZonesLayouts
    properties:
      layouts:
        hotkeys:
          - { key: 1, layoutId: "{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}" }
```

### Example 4 - Capture existing layouts

Export the current layouts on a reference machine, then reuse them in a
configuration document:

```powershell
PowerToys.DSC.exe export --resource 'layouts' --module FancyZones
# {"layouts":{"custom":[{"uuid":"{5C4F1A20-9B3E-4C7D-8E2F-1A2B3C4D5E6F}","name":"Two columns 70/30","grid":{...}}],"templates":[...],"hotkeys":[...],"defaults":{...}}}
```

## See also

- [FancyZones Module][02]
- [Settings Resource Reference][01]
- [PowerToys DSC Overview][03]

<!-- Link reference definitions -->
[01]: ./settings-resource.md
[02]: ./modules/FancyZones.md
[03]: ./overview.md
