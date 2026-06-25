# Image Resizer — module verification profile

**PT module**: `Image Resizer` (resize images via Explorer right-click; WinUI 3 GUI + headless CLI)
**Source**: `<PT-repo>\src\modules\imageresizer\` (PT repo)
**Settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\Image Resizer\settings.json` (PowerToys-wrapper shape: `{ "properties": { "imageresizer_*": { "value": … } }, "name": "Image Resizer", "version": "1" }`). A legacy `sizes.json` mirrors `imageresizer_sizes`; `image-resizer-settings.json` is `{}` (unused).
**Enable flag**: `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json` → `enabled."Image Resizer"` (runner-owned; restart runner after toggling).
**Logs**: `%LOCALAPPDATA%\Microsoft\PowerToys\Image Resizer\Logs\…`
**Exes**: GUI = `%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.ImageResizer.exe`; **CLI = `%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.ImageResizerCLI.exe`**.
**Context menu**: Win11 packaged `IExplorerCommand` (sparse pkg `ImageResizerContextMenuPackage.msix`, dllmain.cpp) + legacy classic `ImageResizerExt.dll` (`dll/ContextMenuHandler.cpp`). **Shipped caption = "Resize with Image Resizer"** (`IDS_IMAGERESIZER_CONTEXT_MENU_ENTRY`; checklist's "Resize images" is STALE).
**No global hotkey / no Named Event / no DSC for the engine** — entry is the Explorer menu (or direct exe launch).

## The back-door that makes this module ~fully drivable (no Explorer needed)

### `PowerToys.ImageResizerCLI.exe` — the deterministic engine (PREFER for all resize-behavior items)
Shares the exact `ResizeBatch.FromCliOptions` → `ResizeBatch.ProcessAsync` → `ResizeOperation.ExecuteAsync` engine as the GUI (`ui/Cli/ImageResizerCliExecutor.cs:76-108`, `App.xaml.cs:102`). Reads live `Image Resizer\settings.json` then applies CLI overrides (`CliSettingsApplier.cs`).
```
--width/-w  --height/-h  --unit/-u {Centimeter|Inch|Percent|Pixel}  --fit/-f {Fill|Fit|Stretch}
--size <presetIndex>  --shrink-only  --replace/-r  --ignore-orientation  --remove-metadata
--quality/-q  --keep-date-modified  --filename/-n "<%1..%6>"  --destination/-d <dir>
--show-config  --help   <files…>     (also accepts \\.\pipe\<name> and stdin file list)
```
`--show-config` dumps the effective settings (great pre/post check). `-d <dir>` keeps outputs isolated. Assert output dimensions with `[System.Drawing.Image]::FromFile(p)`.
**Caveat**: `--ignore-orientation`/`--shrink-only`/`--replace`/`--keep-date-modified`/`--remove-metadata` are *flags* — they can only set the value **true**; to test the **false** case, temporarily edit `settings.json` (back up + restore).

### Direct GUI launch — for the two UI-only items (gif warning, size-list populated)
`Start-Process PowerToys.ImageResizer.exe "<file>"` opens the window pre-loaded (argv/stdin via `ResizeBatch.FromCommandLine`). Behaviorally identical to the context-menu launch (`dllmain.cpp:219-245` just writes a pipe + launches the same exe). Then drive with `winapp ui`:
- Size selector: `SizeComboBox` → `winapp ui invoke SizeComboBox -w <hwnd>` to expand, then `inspect` shows `itm-<name>-XXXX` ListItems.
- Gif warning: `Message Text "Gif files with animations may not be correctly resized."` InfoBar, bound to `ViewModel.HasGifFiles` (set when any file ends `.gif`).

## Engine facts (verified from source — cite these for the resize items)
- `ResizeFit`: **Fill=0, Fit=1, Stretch=2** (`ResizeFit.cs`). Fit=`min(scaleX,scaleY)`; Fill=`max`+centered-crop; Stretch=independent (`ResizeOperation.cs:449-498`).
- `ResizeUnit`: **Centimeter=0, Inch=1, Percent=2, Pixel=3** (`ResizeUnit.cs`). Inch=`v*dpi`; cm=`v*dpi/2.54`; Percent=`v/100*orig`; Pixel=`v` (`ResizeSize.cs:109-123`). **Outputs depend on image DPI** — read actual DPI and compute expectations from it (a 120-DPI fixture gives 10cm→472px, 4in→480px).
- Filename `%1..%6` → original-name, size-name, selected-W, selected-H, **output**-W, **output**-H (`Settings.cs:229-239`, `ResizeOperation.cs:593-601`).
- ShrinkOnly: if target scale>1, returns `noTransformNeeded` (file copied unchanged) (`ResizeOperation.cs:462-475`).
- KeepDateModified: `SetLastWriteTimeUtc(out, GetLastWriteTimeUtc(src))` (`ResizeOperation.cs:146-149`).
- Replace: `File.Replace(out, src, backup)` then recycle backup — no copy left (`ResizeOperation.cs:151-156`).
- IgnoreOrientation swap: gated by `IgnoreOrientation && !HasAuto && Unit != Percent` (`ResizeOperation.cs:419-444`).

## Recipes — a control/observation map, NOT a per-test-case answer key

> Maps each capability to **which control/CLI flag drives it** and **where the result shows**. CLI flag *names* and fit-mode/unit/field enumerations are stable IR knowledge and stay; concrete flag *values*, fixtures, and expected outputs are per-test-case — design those at runtime.

| # | Capability | Drive (control / CLI flag) | Observe (where the result shows) |
|---|---|---|---|
| 1 | Module disabled → context-menu entry absent | toggle `enabled` off + restart runner; synthetic menu (only valid observer) | "Resize with Image Resizer" absent. Gate: `dllmain.cpp:87-91` (ECS_HIDDEN), `ContextMenuHandler.cpp:70-71,383-385`. Locked desktop → BLK-ENV |
| 2 | Module enabled → entry present (modern + classic), click launches the GUI | synthetic menu + invoke | `Get-PtContextMenuItems` shows "Resize with Image Resizer"; classic "Show more options" too; invoke → `PowerToys.ImageResizer.exe` launches |
| 3 | Remove a built-in size / add a custom size | edit `imageresizer_sizes` (INTEGER Ids!) + launch GUI | `SizeComboBox` reflects the edit (removed gone, custom present) |
| 4 | Resize one / multiple files end-to-end | CLI `--size <id> [files…]` | outputs at the size's Fit dimensions |
| 5 | GIF animation warning on `.gif` input | GUI on a `.gif` | warning InfoBar present (`winapp ui inspect`) |
| 6 | Fit modes (Fill / Fit / Stretch) | CLI `--width --height --fit <mode>` | output shape matches the mode (crop / letterbox / exact) |
| 7 | Unit conversion (cm / inch / percent / pixel) | CLI `--unit <u>` | output px = unit converted at the image's DPI |
| 8 | Custom filename format (`%1`..`%6` fields) | CLI `--filename <fmt>` | output filename follows the format fields |
| 9 | "Keep date modified" | CLI `--keep-date-modified` | output mtime == source mtime (control: without the flag, differs) |
| 10 | "Shrink only" | CLI `--shrink-only` | an already-small image is untouched (control: a large one still shrinks) |
| 11 | "Replace original" | CLI `--replace` | original replaced in place; no `(name) (1)` copy |
| 12 | "Ignore orientation" | settings (false) vs flag (true) | on a portrait target over a landscape image: false→no W/H swap, true→swap |

> **Mapping process**: read the actual checklist item → identify the capability → find its row → drive the named control/flag and design your own inputs + assertions. If no row matches, drive ad-hoc and add a row (capability + control + observation point; no canned inputs).

## Common BLOCKED traps (avoid)
- **Don't mark the resize-behavior items BLOCKED for "needs a real right-click".** The CLI fully drives them with the identical engine; the menu is just the trigger (prove the menu→launch wiring once with the golden path in Recipe 2).
- **PowerShell `ConvertTo-Json` writes computed numbers as doubles (`"Id": 3.0`)** → `System.Text.Json` rejects `imageresizer_sizes` and the app silently falls back to the 4 built-in default presets (Small/Medium/Large/Phone). Cast Ids to `[int]` or regex-strip `\.0`. This bit the remove-size-add-custom item (Recipe 3) the first time.
- **cm/inch outputs depend on the fixture's DPI, not 96.** System.Drawing saves at the session display DPI (here 120). Compute expectations from the actual DPI.
- **Caption is "Resize with Image Resizer", not the checklist's "Resize images"** (both menus). Hard-match the real caption.
- **Idle auto-lock = BLK-ENV for the disabled-absent + enabled-present items (Recipes 1-2)** (synthetic right-click needs foreground). Disable lock/sleep before the run (`references/environment-setup.md`).

## Fixture files needed
None pre-canned. Generate with `System.Drawing`: a landscape (e.g. 1200×800) and portrait (800×1200) JPEG, a small (100×100) PNG, a square (400×400) PNG, a `.gif` (single frame is fine — the warning is extension-based), and 3 identical images for the multi-file batch.

## Source citations
- `ui/Cli/ImageResizerCliExecutor.cs`, `ui/Models/CliOptions.cs`, `ui/Cli/CliSettingsApplier.cs`, `ui/Cli/Commands/ImageResizerRootCommand.cs` — CLI surface + engine reuse.
- `ui/Models/ResizeOperation.cs:419-501,572-617,146-156` — dimension math, filename, keep-date, replace.
- `ui/Models/ResizeSize.cs:78-124`, `ResizeFit.cs`, `ResizeUnit.cs` — unit/fit math + enum order.
- `ui/Properties/Settings.cs:65,105-111,229-239,431-552` — paths, defaults, JSON property names, FileNameFormat.
- `ImageResizerContextMenu/dllmain.cpp:49,79-133,219-245,284` — modern menu title, enable+image gate, launch, caption.
- `dll/ContextMenuHandler.cpp:21,46-48,70-71,383-385` — classic menu caption + enable gate.
- `ui/ViewModels/InputViewModel.cs:76,143`, `ImageResizerXAML/Views/InputPage.xaml:293-299`, `Strings/en-us/Resources.resw:148-149` — gif warning.

## Ceiling
**18/18 PASS** observed (2026-06-09). All 14 resize-behavior items + the enabled-entry-present-in-both-menus + the remove-size + the gif-warning items cleanly driven via CLI/GUI. The disabled-entry-absent case (in both modern + classic menus, with sibling entries remaining and the entry returning on re-enable) verified live once the desktop was unlocked. NB: an idle auto-lock will turn the menu-presence Recipes 1-2 into BLK-ENV — disable lock/sleep up front (`references/environment-setup.md`).

## Don'ts
- Don't expect `Shell.Application.Verbs()` to list the entry — it's a Win11 packaged command (classic verbs are blind; `CoCreate` → `REGDB_E_CLASSNOTREGISTERED`).
- Don't hardcode 96 DPI for cm/inch math.
- Don't write preset Ids as JSON doubles.
- Don't kill processes by name; use `Stop-Process -Id <pid>`.
- Don't forget to restore `enabled."Image Resizer"=true` + restart runner, and revert any `settings.json`/`sizes.json` edits.
