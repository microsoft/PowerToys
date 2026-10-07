# Display Profiles proposal

Related issue: [#48286 — Power Display: Display Profiles for Windows Display Configuration Switching](https://github.com/microsoft/PowerToys/issues/48286)

## Summary

Display Profiles is a proposed display-topology workflow for PowerToys Settings, with an optional standalone Command Palette extension as a complementary access point. The technical identifier remains `MonitorPower` for compatibility. The feature is intended to complement Power Display, not duplicate it.

Power Display already covers per-monitor DDC/CI and VCP controls such as brightness, contrast, volume, input source, rotation, color temperature, power state, and profiles for those monitor settings.

Display Profiles focuses on Windows display setup workflows:

- "Play on TV"
- "Back to PC"
- choose which displays should stay active
- enable or disable displays
- restore primary monitor and layout
- optionally integrate with Windows display topology APIs or a profile-oriented backend

The primary PowerToys surface is a dedicated Settings page backed by a lightweight runtime host for activation shortcuts and the profile selector. A standalone Command Palette extension provides complementary access to the same profiles while the feature is not yet available in a published PowerToys build.

Both surfaces reuse `MonitorPower.Core` and the profile store at `%LOCALAPPDATA%\MonitorPower\profiles`; neither duplicates display-topology or persistence logic. The standalone extension does not register another global keyboard or controller listener.

Per `CONTRIBUTING.md`, the implementation and final product placement still require maintainer agreement through the linked issue before an upstream feature PR is accepted.

## Problem

Users with a desktop monitor setup plus a TV often need to switch between normal desktop use and gaming or media use. Today that workflow commonly involves several manual steps:

- opening Windows display settings
- changing active displays
- changing the primary display
- applying a saved topology through an external utility
- turning unneeded displays off or disabling them

Power Display handles monitor controls, but it does not target higher-level Windows display topology workflows such as active display sets, PC/TV layout restoration, or primary display changes.

## Proposed direction

Use Settings as the primary integration point for inspecting the current topology, creating profiles, configuring activation, and viewing diagnostics. The runtime host provides a quick selector that can be opened with a keyboard shortcut or controller chord.

A standalone Command Palette extension named **Display Profiles** lists the same user-defined and built-in profiles. It remains independently deployable so users can access the workflow with a published PowerToys installation before the built-in module is accepted and released.

Example actions:

- `Play on TV`
- `Back to PC`
- `Apply selected display profile`
- `Choose active monitors`

`MonitorPower.Core` is the shared implementation for display discovery, topology changes, profile validation, persistence, and application. Settings, the runtime host, and the standalone Command Palette extension consume that shared implementation.

The implementation does not depend on local scripts, machine-specific mappings, or third-party binaries.

## Command Palette shape

The existing Command Palette extension layout under `src/modules/cmdpal/ext/` uses standalone .NET projects. `SamplePagesExtension` shows the relevant shape:

- a project under `src/modules/cmdpal/ext/<ExtensionName>/`
- a `CommandProvider`
- top-level command items returned by `TopLevelCommands()`
- extension registration through manifest and COM-visible extension entry points
- packaging through the extension project manifest and MSIX tooling

`Microsoft.CmdPal.Ext.PowerToys` is also a relevant reference because it already exposes PowerToys module commands through Command Palette and includes module-specific command items, fallback commands, and settings/state refresh behavior.

If accepted for implementation, the likely project location would be:

```text
src/modules/cmdpal/ext/MonitorPowerExtension/
```

The standalone extension remains intentionally focused:

- one top-level `Display Profiles` command;
- a list page backed by the shared profile store;
- built-in actions for all displays and the primary display only;
- commands to create, edit, delete, and apply profiles;
- clear boundaries that avoid brightness, contrast, volume, input source, color temperature, monitor power-state sliders, and other Power Display responsibilities.

## Prototype evidence

A local prototype outside the PowerToys repo validates the basic planning model:

```text
ToolEnabled       : True
KeepOnDdcIndexes  : {3}
TurnOffDdcIndexes : {1, 2, 4}
```

This means the user selects one or more monitors to keep active/on, and the planner derives the monitors that would be turned off or disabled. The prototype is intentionally not copied into PowerToys because it uses local scripts and machine-specific monitor mappings.

## Non-goals

- Reimplementing Power Display.
- Creating a second monitor-control flyout.
- Adding DDC/CI sliders to Command Palette.
- Shipping a dependency on local prototype scripts.
- Bundling third-party binaries such as MultiMonitorTool.

## Open questions

- Should topology profile actions belong in Power Display profiles instead of a Command Palette extension?
- Is there an existing internal PowerToys display abstraction that should own topology changes?
- Should this live under `Microsoft.CmdPal.Ext.PowerToys` as commands for a PowerToys module, or as a separate extension project under `src/modules/cmdpal/ext/`?
- Would maintainers prefer this as a built-in extension, a sample extension, or an external extension?
- What Windows display APIs should be preferred for active display sets, primary monitor, and layout restoration?
- What safety checks are required before disabling displays from PowerToys?

## Upstream process

Discussion should continue in issue #48286 before the built-in Settings/runtime integration is proposed for merge. Maintainers should confirm whether display-topology profiles belong in Power Display or remain a separate Display Profiles module, and whether the standalone Command Palette extension should be distributed independently or included in the repository.

Before a PR is opened, the implementation must pass the complete PowerToys build, relevant unit tests, localization validation, spell-check, and manual multi-monitor safety testing.
