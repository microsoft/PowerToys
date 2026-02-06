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
