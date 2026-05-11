# Product Label Mapping

Authoritative mapping from the bug report template "Area(s) with issue?" dropdown values
to `Product-*` labels. Validated against the actual labels in the `microsoft/PowerToys` repository.

## Template Field → Label Mapping

| Template Dropdown Value | Product Label |
|------------------------|---------------|
| Advanced Paste | `Product-Advanced Paste` |
| Always on Top | `Product-Always On Top` |
| Awake | `Product-Awake` |
| ColorPicker | `Product-Color Picker` |
| Command not found | `Product-CommandNotFound` |
| Command Palette | `Product-Command Palette` |
| Crop and Lock | `Product-CropAndLock` |
| Environment Variables | `Product-Environment Variables` |
| FancyZones | `Product-FancyZones` |
| FancyZones Editor | `Product-FancyZones` |
| File Locksmith | `Product-File Locksmith` |
| File Explorer: Preview Pane | `Product-File Explorer` |
| File Explorer: Thumbnail preview | `Product-File Explorer` |
| General | `Product-General` |
| Grab And Move | `Product-Grab And Move` |
| Hosts File Editor | `Product-Hosts File Editor` |
| Image Resizer | `Product-Image Resizer` |
| Keyboard Manager | `Product-Keyboard Shortcut Manager` |
| Light Switch | `Product-LightSwitch` |
| Mouse Utilities | `Product-Mouse Utilities` |
| Mouse Without Borders | `Product-Mouse Without Borders` |
| New+ | `Product-New+` |
| Peek | `Product-Peek` |
| Power Display | `Product-PowerDisplay` |
| PowerRename | `Product-PowerRename` |
| PowerToys Run | `Product-PowerToys Run` |
| Quick Accent | `Product-Quick Accent` |
| Registry Preview | `Product-Registry Preview` |
| Screen ruler | `Product-Screen Ruler` |
| Settings | `Product-Settings` |
| Shortcut Guide | `Product-Shortcut Guide` |
| TextExtractor | `Product-Text Extractor` |
| Workspaces | `Product-Workspaces` |
| ZoomIt | `Product-ZoomIt` |

## Non-Product Areas

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

## All Valid Product Labels (in repo)

For reference, these are the actual `Product-*` labels that exist in the repository:

- `Product-Advanced Paste`
- `Product-Always On Top`
- `Product-Awake`
- `Product-Color Picker`
- `Product-Command Palette`
- `Product-CommandNotFound`
- `Product-CropAndLock`
- `Product-Cursor Wrap`
- `Product-Environment Variables`
- `Product-FancyZones`
- `Product-File Actions Menu`
- `Product-File Explorer`
- `Product-File Locksmith`
- `Product-Find My Mouse`
- `Product-General`
- `Product-Grab And Move`
- `Product-Hosts File Editor`
- `Product-Image Resizer`
- `Product-Keyboard Shortcut Manager`
- `Product-LightSwitch`
- `Product-Mouse Highlighter`
- `Product-Mouse Jump`
- `Product-Mouse Pointer Crosshairs`
- `Product-Mouse Utilities`
- `Product-Mouse Without Borders`
- `Product-New+`
- `Product-Peek`
- `Product-PowerDisplay`
- `Product-PowerRename`
- `Product-PowerToys Run`
- `Product-Quick Accent`
- `Product-Registry Preview`
- `Product-Screen Ruler`
- `Product-Settings`
- `Product-Shortcut Guide`
- `Product-Text Extractor`
- `Product-Workspaces`
- `Product-ZoomIt`
