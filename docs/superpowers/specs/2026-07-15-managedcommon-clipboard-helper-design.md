# ManagedCommon Clipboard Helper Design

## Summary

Add a production clipboard helper to `ManagedCommon` and migrate ColorPicker to use it. The helper will use the Windows Runtime clipboard APIs, support text, RTF, images, and file-system items, and expose both synchronous and asynchronous APIs. Command Palette and all other modules remain unchanged.

## Goals

- Provide one reusable clipboard implementation in `ManagedCommon`.
- Support reading and writing:
  - Plain text.
  - RTF.
  - Encoded images.
  - Files and folders.
- Provide both WinRT-native and .NET-friendly image and file APIs.
- Allow calls from any apartment by executing clipboard work on an internal STA thread.
- Preserve ColorPicker's requirement that copied text can survive immediate process exit.
- Handle transient clipboard contention without making expected failures exceptional.

## Non-goals

- Migrate CmdPal, Advanced Paste, Peek, Registry Preview, or other existing helpers.
- Support clipboard history, roaming, change notifications, custom formats, or simulated paste.
- Generate a plain-text fallback when writing RTF.
- Accept raw image pixels or perform image encoding.
- Add cancellation tokens to the initial asynchronous API.

## Public API

Add a public static `ManagedCommon.ClipboardHelper` facade.

### Text

```csharp
bool TrySetText(string? text, bool flush = false);
Task<bool> TrySetTextAsync(string? text, bool flush = false);
bool TryGetText(out string? text);
Task<ClipboardReadResult<string>> TryGetTextAsync();
```

### RTF

```csharp
bool TrySetRtf(string? rtf, bool flush = false);
Task<bool> TrySetRtfAsync(string? rtf, bool flush = false);
bool TryGetRtf(out string? rtf);
Task<ClipboardReadResult<string>> TryGetRtfAsync();
```

RTF writes contain only the supplied RTF format. The helper does not derive or add a plain-text representation.

### Images

The WinRT API uses `RandomAccessStreamReference`. The .NET API accepts or returns encoded image data through `Stream`.

```csharp
bool TrySetImage(RandomAccessStreamReference? image, bool flush = false);
Task<bool> TrySetImageAsync(RandomAccessStreamReference? image, bool flush = false);
bool TrySetImage(Stream? encodedImage, bool flush = false);
Task<bool> TrySetImageAsync(Stream? encodedImage, bool flush = false);

bool TryGetImage(out RandomAccessStreamReference? image);
Task<ClipboardReadResult<RandomAccessStreamReference>> TryGetImageAsync();
bool TryGetImageStream(out Stream? encodedImage);
Task<ClipboardReadResult<Stream>> TryGetImageStreamAsync();
```

The `Stream` overload expects an already encoded image such as PNG, JPEG, or BMP. It does not accept raw pixel buffers.

### Files and folders

The WinRT API uses `IStorageItem`. The .NET API uses file-system paths and supports both files and directories.

```csharp
bool TrySetStorageItems(IEnumerable<IStorageItem>? items, bool flush = false);
Task<bool> TrySetStorageItemsAsync(IEnumerable<IStorageItem>? items, bool flush = false);
bool TrySetFilePaths(IEnumerable<string>? paths, bool flush = false);
Task<bool> TrySetFilePathsAsync(IEnumerable<string>? paths, bool flush = false);

bool TryGetStorageItems(out IReadOnlyList<IStorageItem>? items);
Task<ClipboardReadResult<IReadOnlyList<IStorageItem>>> TryGetStorageItemsAsync();
bool TryGetFilePaths(out IReadOnlyList<string>? paths);
Task<ClipboardReadResult<IReadOnlyList<string>>> TryGetFilePathsAsync();
```

### Asynchronous read result

Add a small public readonly generic value type:

```csharp
ClipboardReadResult<T>
```

It exposes `Succeeded` and nullable `Value` properties. A failed or unavailable read has `Succeeded == false` and `Value == null`.

## Architecture

`ClipboardHelper` is a static facade over a single internal service. The internal service contains:

- A lazily created background STA executor.
- An internal backend that wraps `Windows.ApplicationModel.DataTransfer.Clipboard`.
- Data-package construction and conversion helpers.
- Shared retry logic.

The STA executor serializes all clipboard operations. Both synchronous and asynchronous public methods call the same asynchronous core; synchronous methods wait for that operation from the caller thread. The executor completes tasks with continuations scheduled asynchronously so caller continuations do not run on the clipboard thread.

The backend abstraction remains internal. Unit tests can instantiate the internal service with a fake backend without changing the process-wide clipboard.

## Data flow

### Writes

1. Validate and materialize input.
2. Convert .NET-friendly input to WinRT data where required.
3. Enqueue work on the STA executor.
4. Build a `DataPackage`.
5. Call `Clipboard.SetContent`.
6. Call `Clipboard.Flush` when `flush` is `true`.

For a flushed write, a transient failure during `Flush` retries the complete set-and-flush operation.

### Reads

1. Enqueue work on the STA executor.
2. Call `Clipboard.GetContent`.
3. Check the required `StandardDataFormats` value.
4. Read the matching value from `DataPackageView`.
5. Convert WinRT values to .NET-friendly values when required.

A missing format is a normal unsuccessful result and is not retried.

## Image lifetime

The .NET image-write overload copies the input stream from its current position into a helper-owned WinRT in-memory stream. The helper does not close the caller's stream and does not depend on the caller keeping it alive after the method returns.

When `flush` is `false`, the helper-owned stream stays associated with the `DataPackage` until the package is released by the clipboard. When `flush` is `true`, it remains alive until flushing completes.

The .NET image-read overload copies clipboard image data into a new `MemoryStream`, resets its position to zero, and transfers disposal responsibility to the caller.

## File-system conversion

The path overload materializes its input once and resolves each path to either a `StorageFile` or `StorageFolder`. Null, blank, or empty input returns failure without changing the clipboard. Invalid or inaccessible paths surface their normal argument, I/O, or access exceptions rather than being misreported as clipboard contention.

The .NET read overload projects returned storage items to their file-system paths without silently dropping items.

## Retry and error handling

- Maximum attempts: 10.
- Delay between attempts: 10 milliseconds.
- Retried failures:
  - `COMException`.
  - `UnauthorizedAccessException` raised by clipboard access.
- Failure is returned when:
  - Input is null, empty, or contains no items.
  - The requested format is absent.
  - All attempts fail because of clipboard contention.
- Other exceptions propagate immediately.

`ManagedCommon` does not log expected clipboard failures. Callers decide how to report them. ColorPicker logs an error when its write returns `false`.

## ColorPicker migration

- Replace both ColorPicker calls to `ColorPicker.Helpers.ClipboardHelper.CopyToClipboard` with `ManagedCommon.ClipboardHelper.TrySetText(..., flush: true)`.
- Preserve failure logging at the ColorPicker call sites.
- Delete `src/modules/colorPicker/ColorPickerUI/Helpers/ClipboardHelper.cs`.
- Delete the ColorPicker unit tests that target the removed local helper.
- Do not modify CmdPal or any other module.

## Tests

Add `src/common/ManagedCommon.UnitTests/ManagedCommon.UnitTests.csproj` and include it under `/common/Tests/` in `PowerToys.slnx`.

Unit tests use a fake internal clipboard backend and do not modify the real system clipboard. They cover:

- STA execution from STA and MTA callers.
- Operation serialization.
- Synchronous and asynchronous APIs sharing the same core behavior.
- Retry success, retry exhaustion, and immediate propagation of unexpected exceptions.
- Missing-format results.
- Text and RTF package construction and reads.
- WinRT and .NET encoded-image conversions and stream lifetime independence.
- WinRT storage-item and .NET path conversions for files and folders.
- Null, blank, and empty inputs.
- Optional `Flush` behavior.

Existing ColorPicker end-to-end tests remain the real clipboard integration coverage.

## Validation

- Build `ManagedCommon.UnitTests`.
- Run its tests with the repository-supported VSTest workflow.
- Build `ColorPickerUI`.
- Build and run `ColorPickerUI.UnitTests`.
- Confirm the existing ColorPicker end-to-end clipboard scenario remains applicable.
