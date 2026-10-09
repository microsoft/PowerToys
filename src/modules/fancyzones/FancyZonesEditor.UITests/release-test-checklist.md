## This is for tracking UI-Tests migration progress for FancyZones Editor Module
Refer to [release check list] (https://github.com/microsoft/PowerToys/blob/releaseChecklist/doc/releases/tests-checklist-template.md) for all manual tests.

### Existing Manual Test-cases run by previous PowerToys owner
For existing manual test-cases, we will convert them to UI-Tests and run them in CI and Release pipeline

 * Launch Host File Editor:
- [ ] Open editor from the settings
- [ ] Open editor with a shortcut
- [ ] Create a new layout (grid and canvas)
- [ ] Duplicate a template and a custom layout
- [ ] Delete layout
- [ ] Edit templates (number of zones, spacing, distance to highlight adjacent zones). Verify after reopening the editor that saved settings are kept the same.
- [ ] Edit canvas layout: zones size and position, create or delete zones.
- [ ] Edit grid layout: split, merge, resize zones.
- [ ] Check Save and apply and Cancel buttons behavior after editing.
- [ ] Assign a layout to each monitor.
- [ ] Assign keys to quickly switch layouts (custom layouts only), Win + Ctrl + Alt + number.
- [ ] Assign horizontal and vertical default layouts
- [ ] Test duplicate layout focus
	- Select any layout X in 'Templates' or 'Custom' section by click left mouse button
	- Mouse right button click on any layout Y in 'Templates' or 'Custom' sections
	- Duplicate it by clicking 'Create custom layout' (Templates section) or 'Duplicate' in 'Custom' section
	- Expect the layout Y is duplicated

### Additional UI-Tests cases

- [ ] Add test data and start → verify data is correct (custom layouts, template layouts, defaults, shortcut keys)

- [ ] Create a new canvas - verify layout exists

- [ ] Create a new canvas - cancel - doesn’t exist

- [ ] Create a new grid - verify the layout exists

- [ ] Create a new grid - cancel - doesn’t exist

- [ ] Duplicate template by button (+ check default)

- [ ] Duplicate template by menu (+ check default)

- [ ] Duplicate custom by button (+ check shortcut key and default)

- [ ] Duplicate custom by menu (+ check shortcut key and default)

- [ ] Delete non-applied layout

- [ ] Delete applied layout

- [ ] Delete-cancel

- [ ] Delete from context menu

- [ ] Delete: hotkey released

- [ ] Delete: default layout reset to default-default

- [ ] Edit template and save

- [ ] Edit template and cancel

- [ ] Edit custom and save

- [ ] Edit custom and cancel

- [ ] Edit canvas: add zone

- [ ] Edit canvas: delete zone

- [ ] Edit canvas: move zone

- [ ] Edit canvas: resize zone

- [ ] Edit grid: split zone

- [ ] Edit grid: merge zones

- [ ] Edit grid: move splitter

- [ ] UI Init: assigned layouts selected

- [ ] UI Init: applied default - check params

- [ ] UI Init: assigned custom layout, but id not found

- [ ] Assign the same template but with different params to monitors

- [ ] Assign layout on each monitor

- [ ] Assign custom

- [ ] Assign template

- [ ] Assign shortcut key and save

- [ ] Assign shortcut key and cancel

- [ ] Reset shortcut key and save

- [ ] Reset shortcut key and cancel

- [ ] Set default layout + verify both prev and current after reopening

- [ ] applied-layouts.json keeps info about not connected devices - verify they’re present after closing

- [ ] applied-layouts.json keeps info about other virtual desktops

- [ ] first launch without custom-layouts.json, default-layouts.json, layout-hotkeys.json and layout-templates.json