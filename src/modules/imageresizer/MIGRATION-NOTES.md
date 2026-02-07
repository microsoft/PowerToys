# ImageResizer: WPF to WinRT Imaging Migration Notes

## Overview

The ImageResizer module's core image processing was migrated from WPF (`System.Windows.Media.Imaging` / `PresentationCore.dll`) to WinRT (`Windows.Graphics.Imaging`). The UI had already been migrated to WinUI 3, but the imaging pipeline still relied on WPF APIs, causing runtime failures when deployed via the installer (missing `PresentationCore.dll` in self-contained WinUI 3 deployment).

## Known Limitations

### PNG Interlace Mode Not Configurable

**Setting**: `PngInterlaceOption` (values: `Default`, `On`, `Off`)

**Behavior change**: The WinRT `BitmapEncoder` does not expose an API to configure PNG interlace mode. The `PngInterlaceOption` setting is preserved in `settings.json` for backward compatibility, but it has **no effect** on the encoded PNG output. WinRT's PNG encoder always uses its default interlace behavior (non-interlaced).

**Why**: WPF's `PngBitmapEncoder` had an `Interlace` property that mapped to the Adam7 interlacing algorithm, which enables progressive image loading. WinRT's `BitmapEncoder` does not expose an equivalent property, and there is no `BitmapPropertySet` key to control PNG interlacing.

**Impact**: Minimal. PNG interlacing is primarily useful for slow network transfers (progressive rendering in browsers). For local file resizing, it has negligible impact. The Settings UI still displays the option for JSON compatibility, but changing it will not alter the output.

### Metadata Stripping Behavior Change

**Setting**: `RemoveMetadata = true`

**Old behavior (WPF)**: Metadata was selectively stripped — most EXIF properties were removed, but `System.Photo.Orientation` was preserved to maintain correct image display orientation.

**New behavior (WinRT)**: All metadata is stripped when `RemoveMetadata = true`. The new implementation uses `BitmapEncoder.CreateAsync()` (fresh encoder with no metadata) instead of cloning and selectively clearing metadata. This means orientation EXIF data is also removed.

**Why**: WPF had `BitmapMetadata.Clone()` which allowed selective property removal. WinRT's `BitmapProperties` API does not provide an equivalent way to bulk-remove metadata while preserving specific fields after using `CreateForTranscodingAsync`. The simplest correct approach is to use a fresh encoder, which strips everything.

**Impact**: Images with EXIF orientation tags that are resized with `RemoveMetadata = true` may display rotated in viewers that rely on EXIF orientation rather than actual pixel orientation. This matches the user's explicit intent to remove metadata.

### Pixel-Level Differences

WinRT's `BitmapInterpolationMode.Fant` may produce slightly different pixel values compared to WPF's internal scaling algorithm. Both are high-quality interpolation methods, but they are not guaranteed to produce bit-identical output. This is expected and does not affect visual quality.

## Installer / Build Pipeline Issues

### Satellite Assembly References Removed from Resources.wxs

**Problem**: After migrating to WinUI 3, the WiX installer failed with `WIX0103` errors — it could not find `PowerToys.ImageResizer.resources.dll` satellite assemblies in `WinUI3Apps/{locale}/` directories.

**Root cause**: WPF uses `.resx` files which compile into satellite assemblies (`.resources.dll`). WinUI 3 uses `.resw` files which compile into `.pri` files. The installer's `Resources.wxs` still referenced the old satellite assembly pattern.

**Fix**: Removed the ImageResizer satellite assembly component, `WinUI3AppsInstallFolder` from the `ParentDirectory` foreach loop, and the corresponding `RemoveFolder` entry from `Resources.wxs`.

### Phantom Root-Level Build Artifacts

**Problem**: `PowerToys.ImageResizer.exe`, `.deps.json`, and `.runtimeconfig.json` appear in the root output directory (`x64/Release/`) even though the project's `OutputPath` is `WinUI3Apps/`. This caused the installer to include an incomplete, non-functional copy (missing the managed `.dll`) and the ESRP signing check to fail.

**Root cause**: `ImageResizerCLI` (`OutputType=Exe`, `SelfContained=true`) has a `<ProjectReference>` to `ImageResizerUI` (`OutputType=WinExe`, `SelfContained=true`). When MSBuild processes an Exe→WinExe dependency between two self-contained projects, it copies the referenced project's apphost (`.exe`) and runtime config files to the root output directory as a side effect. This is the **only** Exe→WinExe `ProjectReference` in the entire PowerToys codebase — other CLI tools (e.g., FancyZonesCLI) correctly reference a shared Library project instead.

**Temporary fix**: `generateAllFileComponents.ps1` strips the leaked ImageResizer files from the `BaseApplicationsFiles` list after generation. The signing config (`ESRPSigning_core.json`) only references `WinUI3Apps\\` paths.

**TODO**: Refactor the dependency — extract shared CLI logic from `ImageResizerUI` (`ui/Cli/` folder) into a separate Library project, so `ImageResizerCLI` references a Library instead of a WinExe. This follows the pattern used by `FancyZonesCLI` → `FancyZonesEditorCommon`.
