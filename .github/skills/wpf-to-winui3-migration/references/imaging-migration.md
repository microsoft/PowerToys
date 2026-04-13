# Imaging API Migration

Migrating from WPF (`System.Windows.Media.Imaging` / `PresentationCore.dll`) to WinRT (`Windows.Graphics.Imaging`). Based on the ImageResizer migration.

## Why This Migration Is Required

WinUI 3 apps deployed as self-contained do NOT include `PresentationCore.dll`. Any code using `System.Windows.Media.Imaging` will throw `FileNotFoundException` at runtime. ALL imaging code must use WinRT APIs.

| Purpose | Namespace |
|---------|-----------|
| UI display (`Image.Source`) | `Microsoft.UI.Xaml.Media.Imaging` |
| Image processing (encode/decode/transform) | `Windows.Graphics.Imaging` |

## Architecture Change: Pipeline vs Declarative

The fundamental architecture differs:

**WPF**: In-memory pipeline of bitmap objects. Decode → transform → encode synchronously.
```csharp
var decoder = BitmapDecoder.Create(stream, ...);
var transform = new TransformedBitmap(decoder.Frames[0], new ScaleTransform(...));
var encoder = new JpegBitmapEncoder();
encoder.Frames.Add(BitmapFrame.Create(transform, ...));
encoder.Save(outputStream);
```

**WinRT**: Declarative transform model. Configure transforms on the encoder, which handles pixel manipulation internally. All async.
```csharp
var decoder = await BitmapDecoder.CreateAsync(winrtStream);
var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);
encoder.BitmapTransform.ScaledWidth = newWidth;
encoder.BitmapTransform.ScaledHeight = newHeight;
encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
await encoder.FlushAsync();
```

## Core Type Mapping

### Decoders

| WPF | WinRT | Notes |
|-----|-------|-------|
| `BitmapDecoder.Create(stream, options, cache)` | `BitmapDecoder.CreateAsync(stream)` | Async, auto-detects format |
| `JpegBitmapDecoder` / `PngBitmapDecoder` / etc. | `BitmapDecoder.CreateAsync(stream)` | Single unified decoder |
| `decoder.Frames[0]` | `await decoder.GetFrameAsync(0)` | Async frame access |
| `decoder.Frames.Count` | `decoder.FrameCount` (uint) | `int` → `uint` |
| `decoder.CodecInfo.ContainerFormat` | `decoder.DecoderInformation.CodecId` | Different property path |
| `decoder.Frames[0].PixelWidth` (int) | `decoder.PixelWidth` (uint) | `int` → `uint` |
| `WmpBitmapDecoder` | Not available | WMP/HDP not supported |

### Encoders

| WPF | WinRT | Notes |
|-----|-------|-------|
| `new JpegBitmapEncoder()` | `BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream)` | Async factory |
| `new PngBitmapEncoder()` | `BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream)` | No interlace control |
| `encoder.Frames.Add(frame)` | `encoder.SetSoftwareBitmap(bitmap)` | Different API |
| `encoder.Save(stream)` | `await encoder.FlushAsync()` | Async |

### Encoder Properties (Strongly-Typed → BitmapPropertySet)

WPF had type-specific encoder subclasses. WinRT uses a generic property set:

```csharp
// WPF
case JpegBitmapEncoder jpeg: jpeg.QualityLevel = 85;        // int 1-100
case PngBitmapEncoder png:   png.Interlace = PngInterlaceOption.On;
case TiffBitmapEncoder tiff: tiff.Compression = TiffCompressOption.Lzw;

// WinRT — JPEG quality (float 0.0-1.0)
await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
{
    { "ImageQuality", new BitmapTypedValue(0.85f, PropertyType.Single) }
});

// WinRT — TIFF compression (via BitmapPropertySet at creation time)
var props = new BitmapPropertySet
{
    { "TiffCompressionMethod", new BitmapTypedValue((byte)2, PropertyType.UInt8) }
};
var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, stream, props);
```

**JPEG quality scale change**: WPF int `1-100` → WinRT float `0.0-1.0`. Divide by 100.

### Bitmap Types

| WPF | WinRT | Notes |
|-----|-------|-------|
| `BitmapSource` | `SoftwareBitmap` | Central pixel-data type |
| `BitmapImage` | `BitmapImage` (in `Microsoft.UI.Xaml.Media.Imaging`) | UI display only |
| `FormatConvertedBitmap` | `SoftwareBitmap.Convert()` | |
| `TransformedBitmap` + `ScaleTransform` | `BitmapTransform` via encoder | Declarative |
| `CroppedBitmap` | `BitmapTransform.Bounds` | |

### Metadata

| WPF | WinRT | Notes |
|-----|-------|-------|
| `BitmapMetadata` | `BitmapProperties` | Different API surface |
| `BitmapMetadata.Clone()` | No equivalent | Cannot selectively clone |
| Selective metadata removal | Not supported | All-or-nothing only |

**Two encoder creation strategies for metadata:**
- `CreateForTranscodingAsync()` — preserves ALL metadata from source
- `CreateAsync()` — creates fresh encoder with NO metadata

This eliminated ~258 lines of manual metadata manipulation code (`BitmapMetadataExtension.cs`) in ImageResizer.

### Interpolation Modes

| WPF `BitmapScalingMode` | WinRT `BitmapInterpolationMode` |
|------------------------|-------------------------------|
| `HighQuality` / `Fant` | `Fant` |
| `Linear` | `Linear` |
| `NearestNeighbor` | `NearestNeighbor` |
| `Unspecified` / `LowQuality` | `Linear` |

## Stream Interop

WinRT imaging requires `IRandomAccessStream` instead of `System.IO.Stream`:

```csharp
using var stream = File.OpenRead(path);
var winrtStream = stream.AsRandomAccessStream();  // Extension method
var decoder = await BitmapDecoder.CreateAsync(winrtStream);
```

**Critical**: For transcode, seek the input stream back to 0 before creating the encoder:
```csharp
winrtStream.Seek(0);
var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);
```

## CodecHelper Pattern (from ImageResizer)

WPF stored container format GUIDs in `settings.json`. WinRT uses different codec IDs. Create a `CodecHelper` to bridge them:

```csharp
internal static class CodecHelper
{
    // Maps WPF container format GUIDs (stored in settings JSON) to WinRT encoder IDs
    private static readonly Dictionary<Guid, Guid> LegacyGuidToEncoderId = new()
    {
        [new Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057")] = BitmapEncoder.JpegEncoderId,
        [new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf")] = BitmapEncoder.PngEncoderId,
        [new Guid("0af1d87e-fcfe-4188-bdeb-a7906471cbe3")] = BitmapEncoder.BmpEncoderId,
        [new Guid("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3")] = BitmapEncoder.TiffEncoderId,
        [new Guid("1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5")] = BitmapEncoder.GifEncoderId,
    };

    // Maps decoder IDs to corresponding encoder IDs
    private static readonly Dictionary<Guid, Guid> DecoderIdToEncoderId = new()
    {
        [BitmapDecoder.JpegDecoderId] = BitmapEncoder.JpegEncoderId,
        [BitmapDecoder.PngDecoderId] = BitmapEncoder.PngEncoderId,
        // ...
    };

    public static Guid GetEncoderIdFromLegacyGuid(Guid legacyGuid)
        => LegacyGuidToEncoderId.GetValueOrDefault(legacyGuid, Guid.Empty);

    public static Guid GetEncoderIdForDecoder(BitmapDecoder decoder)
        => DecoderIdToEncoderId.GetValueOrDefault(decoder.DecoderInformation.CodecId, Guid.Empty);
}
```

This preserves backward compatibility with existing `settings.json` files that contain WPF-era GUIDs.

## ImagingEnums Pattern (from ImageResizer)

WPF-specific enums (`PngInterlaceOption`, `TiffCompressOption`) from `System.Windows.Media.Imaging` are used in settings JSON. Create custom enums with identical integer values for backward-compatible deserialization:

```csharp
// Replace System.Windows.Media.Imaging.PngInterlaceOption
public enum PngInterlaceOption { Default = 0, On = 1, Off = 2 }

// Replace System.Windows.Media.Imaging.TiffCompressOption
public enum TiffCompressOption { Default = 0, None = 1, Ccitt3 = 2, Ccitt4 = 3, Lzw = 4, Rle = 5, Zip = 6 }
```

## Async Migration Patterns

### Method Signatures

All imaging operations become async:

| Before | After |
|--------|-------|
| `void Execute(file, settings)` | `async Task ExecuteAsync(file, settings)` |
| `IEnumerable<Error> Process()` | `async Task<IEnumerable<Error>> ProcessAsync()` |

### Parallel Processing

```csharp
// WPF (synchronous)
Parallel.ForEach(Files, new ParallelOptions { MaxDegreeOfParallelism = ... },
    (file, state, i) => { Execute(file, settings); });

// WinRT (async)
await Parallel.ForEachAsync(Files, new ParallelOptions { MaxDegreeOfParallelism = ... },
    async (file, ct) => { await ExecuteAsync(file, settings); });
```

### CLI Async Bridge

CLI entry points must bridge async to sync:
```csharp
return RunSilentModeAsync(cliOptions).GetAwaiter().GetResult();
```

### Task.Factory.StartNew → Task.Run

```csharp
// WPF
_ = Task.Factory.StartNew(StartExecutingWork, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

// WinUI 3
_ = Task.Run(() => StartExecutingWorkAsync());
```

## SoftwareBitmap as Interface Type

When modules expose imaging interfaces (e.g., AI super-resolution), change parameter/return types:

```csharp
// WPF
BitmapSource ApplySuperResolution(BitmapSource source, int scale, string filePath);

// WinRT
SoftwareBitmap ApplySuperResolution(SoftwareBitmap source, int scale, string filePath);
```

This eliminates manual `BitmapSource ↔ SoftwareBitmap` conversion code (unsafe `IMemoryBufferByteAccess` COM interop).

## MultiFrame Image Handling

```csharp
// WinRT multi-frame encode (e.g., multi-page TIFF, animated GIF)
for (uint i = 0; i < decoder.FrameCount; i++)
{
    if (i > 0)
        await encoder.GoToNextFrameAsync();

    var frame = await decoder.GetFrameAsync(i);
    var bitmap = await frame.GetSoftwareBitmapAsync(
        frame.BitmapPixelFormat,
        BitmapAlphaMode.Premultiplied,
        transform,
        ExifOrientationMode.IgnoreExifOrientation,
        ColorManagementMode.DoNotColorManage);
    encoder.SetSoftwareBitmap(bitmap);
}
await encoder.FlushAsync();
```

## int → uint for Pixel Dimensions

WinRT uses `uint` for all pixel dimensions. This affects:
- `decoder.PixelWidth` / `decoder.PixelHeight` — `uint`
- `BitmapTransform.ScaledWidth` / `ScaledHeight` — `uint`
- `SoftwareBitmap` constructor — `uint` parameters
- Test assertions: `Assert.AreEqual(96, ...)` → `Assert.AreEqual(96u, ...)`

## Display SoftwareBitmap in UI

```csharp
var source = new SoftwareBitmapSource();
// Must convert to Bgra8/Premultiplied for display
if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
    bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
{
    bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
}
await source.SetBitmapAsync(bitmap);
myImage.Source = source;
```

## Known Limitations

| Feature | WPF | WinRT | Impact |
|---------|-----|-------|--------|
| PNG interlace | `PngBitmapEncoder.Interlace` | Not available | Always non-interlaced |
| Metadata stripping | Selective via `BitmapMetadata.Clone()` | All-or-nothing | Orientation EXIF also removed |
| Pixel formats | Many (`Pbgra32`, `Bgr24`, `Indexed8`, ...) | Primarily `Bgra8`, `Rgba8`, `Gray8/16` | Convert to `Bgra8` |
| WMP/HDP format | `WmpBitmapDecoder` | Not available | Not supported |
| Pixel differences | WPF scaler | `BitmapInterpolationMode.Fant` | Not bit-identical |
