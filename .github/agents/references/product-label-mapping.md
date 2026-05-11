# Product Label Mapping — Overrides & Hints

This file contains **only the non-obvious mappings** between the bug report template
"Area(s) with issue?" dropdown values and `Product-*` labels. Most template values
map directly by prepending `Product-` — only mismatches are listed here.

Labels and template values are discovered dynamically at runtime by the agent.

## Override Mappings (template value ≠ label name)

These template dropdown values have `Product-*` labels with **different names**:

| Template Dropdown Value | Product Label |
|------------------------|---------------|
| ColorPicker | `Product-Color Picker` |
| Command not found | `Product-CommandNotFound` |
| FancyZones Editor | `Product-FancyZones` |
| File Explorer: Preview Pane | `Product-File Explorer` |
| File Explorer: Thumbnail preview | `Product-File Explorer` |
| Hosts File Editor | `Product-Hosts File Editor` |
| Keyboard Manager | `Product-Keyboard Shortcut Manager` |
| Power Display | `Product-PowerDisplay` |
| TextExtractor | `Product-Text Extractor` |
| Screen ruler | `Product-Screen Ruler` |

## Non-Product Template Values

These template values do NOT map to a product label. Use content analysis instead:

| Template Value | Guidance |
|---------------|----------|
| Installer | Consider `Product-General` or infer from context |
| System tray interaction | Consider `Product-Settings` or `Product-General` |
| Welcome / PowerToys Tour window | Consider `Product-General` |

## Keyword Hints for Content Analysis

When the structured field is not available, use these keyword patterns to infer products:

| Keywords / Patterns | Suggested Label |
|--------------------|-----------------|
| CmdPal, cmdpal, command palette, dock | `Product-Command Palette` |
| zones, layout, snap, window arrangement | `Product-FancyZones` |
| grab, move, drag window | `Product-Grab And Move` |
| zoom, screen annotation, draw on screen | `Product-ZoomIt` |
| settings-ui, flyout, quick access, tray | `Product-Settings` |
| paste, clipboard, AI paste | `Product-Advanced Paste` |
| MWB, mouse without borders, cross-machine | `Product-Mouse Without Borders` |
| rename, regex, bulk rename | `Product-PowerRename` |
| peek, file preview, preview pane | `Product-Peek` |
| resize, image resizer, bulk resize | `Product-Image Resizer` |
| theme, dark mode, light switch | `Product-LightSwitch` |
| accent, diacritics, special characters | `Product-Quick Accent` |
| awake, keep awake, caffeine, screen on | `Product-Awake` |
| color picker, eyedropper, hex color | `Product-Color Picker` |
| hosts, hosts file, DNS | `Product-Hosts File Editor` |
| remap, key remap, shortcut remap | `Product-Keyboard Shortcut Manager` |
| mouse highlighter, click highlight | `Product-Mouse Highlighter` |
| mouse jump, teleport mouse | `Product-Mouse Jump` |
| find my mouse, locate cursor | `Product-Find My Mouse` |
| crosshairs, cursor crosshair | `Product-Mouse Pointer Crosshairs` |
| shortcut guide, keyboard overlay | `Product-Shortcut Guide` |
| OCR, text extractor, screen text | `Product-Text Extractor` |
| workspace, save layout, restore windows | `Product-Workspaces` |
| file locksmith, who is using, file lock | `Product-File Locksmith` |
| crop and lock, crop, thumbnail window | `Product-CropAndLock` |
| environment variable, env var, PATH | `Product-Environment Variables` |
| new+, file template, new file | `Product-New+` |
| registry, registry preview, .reg | `Product-Registry Preview` |
| screen ruler, measure, pixel ruler | `Product-Screen Ruler` |
| run, launcher, powertoys run, plugin | `Product-PowerToys Run` |
| command not found, winget, install suggestion | `Product-CommandNotFound` |
| brightness, monitor, display, DDC | `Product-PowerDisplay` |
| cursor wrap, edge wrap, multi-monitor cursor | `Product-Cursor Wrap` |

## PR Title Prefix Conventions

Many PRs use `[ProductName]` prefixes. Common variants:

| Title prefix | Product Label |
|-------------|---------------|
| `[CmdPal]` | `Product-Command Palette` |
| `[PowerDisplay]` | `Product-PowerDisplay` |
| `[ZoomIt]` | `Product-ZoomIt` |
| `[Image Resizer]` | `Product-Image Resizer` |
| `[GPO]` | `Product-General` |
| `[MWB]` | `Product-Mouse Without Borders` |

Most other prefixes match the label directly (e.g., `[FancyZones]` → `Product-FancyZones`).

## Source Directory → Label Mapping

Non-obvious `src/modules/` directory name mappings:

| Directory | Product Label |
|----------|---------------|
| `launcher/` | `Product-PowerToys Run` |
| `MeasureTool/` | `Product-Screen Ruler` |
| `poweraccent/` | `Product-Quick Accent` |
| `PowerOCR/` | `Product-Text Extractor` |
| `previewpane/` | `Product-File Explorer` |
| `interface/` | `Product-General` (runner/settings host) |

Most other directories match by prepending `Product-` to the directory name.

<!-- Valid Product-* labels are discovered dynamically at runtime via gh label list -->
