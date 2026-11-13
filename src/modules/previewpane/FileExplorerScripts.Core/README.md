# Explorer PowerScripts managed backend

Explorer owns this library. Its only PowerScripts reference is **PowerScripts.Client**;
trust, policy, runtime selection, read-only input staging, and Activity remain Host responsibilities.
No parameter values are saved, outputs are never launched, and originals are never opened for writing.

## Public API

All types below are in `FileExplorerScripts.Core`, with PowerScripts DTOs from `PowerScripts.Client`.

```csharp
static class ScriptParameters
{
    static IReadOnlyDictionary<string, JsonElement> GetDefaults(PowerScriptInfo script);
    static IReadOnlyDictionary<string, JsonElement> Parse(
        PowerScriptInfo script, IReadOnlyDictionary<string, string?> values);
    static bool CanRunWithDefaults(PowerScriptInfo script);
}

sealed class ExplorerScriptService
{
    const string ConsumerId = "file-explorer";
    ExplorerScriptService(PowerScriptsClient client);
    static void ValidateSelection(IReadOnlyList<string> files);
    static bool IsApplicable(PowerScriptInfo script, IReadOnlyList<string> files);
    Task<IReadOnlyList<PowerScriptInfo>> GetApplicableScriptsAsync(
        IReadOnlyList<string> files, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ExecuteAsync(
        PowerScriptInfo script, IReadOnlyList<string> files,
        IReadOnlyDictionary<string, JsonElement> parameters, string destination,
        CancellationToken cancellationToken = default);
}

static class ResultExporter
{
    static Task<IReadOnlyList<string>> ExportAsync(
        PowerScriptTransformResult result, PowerScriptInfo script,
        IReadOnlyList<string> inputs, string destination,
        CancellationToken cancellationToken = default);
}
```

Construct with `new ExplorerScriptService(new PowerScriptsClient("file-explorer"))`.
Discovery surfaces Host failures instead of silently reporting an empty catalogue.
Execution refreshes discovery, matches the stable ID, and validates **current** selection
and parameter contracts before a single `TransformAsync` invocation.
`IsApplicable` returns false for expected invalid/missing/inaccessible selections;
`ValidateSelection` throws and can be used to explain the reason.

## Supported shapes and limits

* **Input:** files only. Every extension and the entire selection count must match
  `AcceptsFiles`; no image/audio input adaptation, directory, UNC, device, stream,
  relative, traversal, or reparse-point paths (including ancestor folders).
  There must be 1–1024 selected existing local regular files. Paths are limited to
  259 characters and must use absolute drive-letter/backslash spelling.
* **Parameters:** `string`, signed 32-bit `int` with inclusive `Min`/`Max`,
  strict lowercase `true`/`false` booleans, exact ordinal `choice` options,
  and existing local regular `file` paths. JSON ints/bools stay typed.
  An absent key uses its declared default; explicit null/empty means unset
  and is omitted when optional, or rejected when required.
  Required whitespace-only strings are rejected. Unknown keys/types and malformed
  values raise a label-specific `ArgumentException`. Only expected parameter
  validation failures cause `CanRunWithDefaults` to return false.
  Execution omits the entire `input.parameters` property when the refreshed input
  schema does not declare a parameter bag. Explicitly declared bags are preserved
  even when empty, and their defaults/values are still validated. When discovery
  omits the input schema, nonempty parameter metadata enables the legacy bag.
* **Output:** none (empty list), UTF-8 text without BOM
  (`selected-basename - sanitized-script-name.txt`), and copies of up to
  1024 separate produced files preserving safe basenames. Returned originals,
  folders, reparse points, traversal, unsafe/reserved names, and mismatched shapes
  are rejected. Existing files are never replaced.
* **Inline media output:** only structurally validated uncompressed 24/32-bit BMP
  (`image/bmp`, 40-byte DIB, no color table) and PCM WAV (`audio/wav`, one
  16-byte PCM format chunk followed by one data chunk, 8/16/24/32-bit samples).
  MIME and bytes must agree. Collections are limited to 64 items, 16 MiB/item,
  32 MiB total. Other media formats are explicitly refused, not guessed;
  scripts can return other formats as file-backed outputs instead.

The destination must already be a real local folder with no reparse ancestors.
Each output is staged **inside** it using exclusive `FileMode.CreateNew`, flushed,
then atomically moved to a non-overwriting final name. Name collisions alone try
numbered suffixes (4096 attempts maximum); neither scripts nor failed writes are retried.
Each output is atomic, not the entire collection. On export errors an `IOException`,
or on cancellation an `OperationCanceledException`, reports the committed count;
`exception.Data["ExportedPaths"]` is an immutable `IReadOnlyList<string>` of retained,
complete outputs. Staging files are deleted on errors/cancellation. If deletion itself
fails, `TemporaryPath`/`CleanupError` in exception data identify the cleanup failure.
Failures before export begins do not carry partial-output data.
Nonzero Host exit codes raise `InvalidOperationException` with at most 2048 stderr characters.

## Client contract gaps

* `PowerScriptsClient` has no public consumer-ID accessor or per-call override.
  Consequently this constructor cannot verify an injected client's attribution:
  the caller **must** construct it with the literal `"file-explorer"`. The test Host
  verifies that both list and transform actually receive that attribution.
* Discovery and transform are separate requests, without an immutable discovery
  revision/content token. Refreshing and revalidation narrow staleness but cannot
  lock script metadata between requests; Host remains authoritative.
* The Client exposes path-based files rather than an output lease/handle. Export
  validates sources and ancestors and opens read-only streams that disallow concurrent
  file writes/deletion while copying. Path/ancestor checks are not a handle-anchored
  filesystem sandbox against hostile concurrent directory replacement.

## Build and tests

No solution/app/shared WinUI output changes are required.

```powershell
Set-Location C:\PowerToys-10ca\src\modules\previewpane\FileExplorerScripts.Core.Tests
# /restore is only necessary for a new/changed manifest or missing assets.
& C:\PowerToys-10ca\tools\build\build.ps1 -Platform x64 -Configuration Debug -ExtraArgs '/restore'
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' `
  '.\bin\x64\Debug\net10.0-windows10.0.26100.0\FileExplorerScripts.Core.Tests.dll' `
  /Platform:x64 /Logger:trx /ResultsDirectory:TestResults
```

The tests build a tiny isolated fake process Host; they do not build/run the real Host.
Fixtures live below the test output directory, not the system temporary directory.
Symbolic-link tests require Windows Developer Mode or symbolic-link privilege.
Coverage includes selection/count/extension matching, typed/default/required parameters,
fresh attributed discovery/execution, source preservation, concurrent atomic non-overwrite,
directory/reparse rejection, cancellation staging cleanup, partial export status,
text naming/encoding, media validation/refusal, and bounded deterministic fuzzing of
parameter metadata/values, unsafe output source names, path-like script display names,
selection paths, and media bytes.
