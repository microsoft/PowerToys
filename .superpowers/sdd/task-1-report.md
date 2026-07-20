# Task 1 Implementation Report: PowerOCR.Core Geometry and Formatting Layer

## Implementation Summary

Task 1 establishes the `PowerOCR.Core` class library and `PowerOCR.Core.UnitTests` test project. All UI-independent geometry models, formatting utilities, and table analysis algorithms were implemented as pure C# with no WPF/WinUI dependencies. The test-driven development cycle was followed strictly.

## Files Changed

| Operation | File |
|-----------|------|
| Created | `src/modules/PowerOCR/PowerOCR.Core/PowerOCR.Core.csproj` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/OcrPoint.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/OcrRect.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/PixelRect.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/DisplayBounds.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/PixelSelection.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/OcrWordData.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/OcrLineData.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/OcrDocument.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Models/OcrCaptureMode.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Formatting/OcrTextFormatter.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Formatting/TableTextFormatter.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core/Geometry/SelectionGeometry.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core.UnitTests/PowerOCR.Core.UnitTests.csproj` |
| Created | `src/modules/PowerOCR/PowerOCR.Core.UnitTests/OcrTextFormatterTests.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core.UnitTests/TableTextFormatterTests.cs` |
| Created | `src/modules/PowerOCR/PowerOCR.Core.UnitTests/SelectionGeometryTests.cs` |
| Modified | `PowerToys.slnx` (lines 872–884 region) |

## RED Phase

### Command

```
cd src\modules\PowerOCR\PowerOCR.Core.UnitTests
..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

### Output (excerpt)

```
error CS0234: The type or namespace name 'Formatting' does not exist in the namespace 'PowerOCR.Core'
error CS0234: The type or namespace name 'Models' does not exist in the namespace 'PowerOCR.Core'
error CS0234: The type or namespace name 'Geometry' does not exist in the namespace 'PowerOCR.Core'
error CS0246: The type or namespace name 'OcrLineData' could not be found

Build FAILED. 7 Error(s)
```

### Why It Failed

The test project compiled against `PowerOCR.Core` (via `ProjectReference`), but the core project contained no source files—no `Models`, `Formatting`, or `Geometry` namespaces existed. This is the correct RED failure: feature missing, not a typo.

**Note:** An initial NuGet restore failure (NETSDK1004 — missing `project.assets.json`) required running `tools\build\build-essentials.cmd` first. This is expected on a first build of a new project; documented here as an operational step, not a code issue.

## GREEN Phase

### Command

```
cd src\modules\PowerOCR\PowerOCR.Core.UnitTests
..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

### Output

```
[BUILD] Local projects built; exiting.
Exit code: 0
```

## Test Results

```
VSTest version 18.7.0 (x64)

  Passed FormatDocument_LatinLanguage_UsesOcrLineText [18 ms]
  Passed FormatDocument_ChineseLanguage_JoinsSingleCharacterWords [9 ms]
  Passed FormatDocument_RightToLeftLanguage_ReversesWordOrderPerLine [1 ms]
  Passed CollapseToSingleLine_EmptyText_ReturnsEmpty [< 1 ms]
  Passed CollapseToSingleLine_MultipleLineEndings_CollapsesWhitespace [3 ms]
  Passed ToPixels_At150Percent_MapsDipToPhysicalPixels [2 ms]
  Passed ToPixels_ReverseDrag_ProducesPositiveClampedRectangle [< 1 ms]
  Passed Format_TwoByTwoGrid_UsesTabsAndNewLines [8 ms]
  Passed Format_SparseSecondRow_PreservesEmptyColumn [< 1 ms]

Test Run Successful.
Total tests: 9
     Passed: 9
 Total time: 1.0675 Seconds
```

## Commit SHA

`144de28171`

```
[yuleng/worktree/powerocr-winui3 144de28171] Extract PowerOCR formatting core
 18 files changed, 534 insertions(+)
```

## Self-Review

**Behavioral accuracy:**
- All 9 tests pass, including Chinese CJK word joining, RTL reversal, 150% DPI scaling, reverse-drag clamping, sparse table, and 2×2 grid.
- `OcrTextFormatter.JoinCjkAwareWords` is `internal` (not `public`), made accessible to the test project via `InternalsVisibleTo` in the `.csproj`, matching the brief's intent.
- `FormatDocument`, `CollapseToSingleLine`, `UsesSpaces` are `public static`.
- All immutable record structs/classes match the brief exactly.

**Conventions applied:**
- Copyright headers (`// Copyright (c) Microsoft Corporation …`) added to all 13 new C# files.
- One type per file (SA1649/SA1402 compliant): the brief's illustrative `OcrGeometry.cs` bundled 8 types; repository StyleCop rules require one type per file, so the models were split into 8 individual files.
- All files compile clean with `TreatWarningsAsErrors=True`.

## Deviations from Brief

| Deviation | Reason |
|-----------|--------|
| `OcrGeometry.cs` (8 types in one file) replaced by 8 individual files (`OcrPoint.cs`, `OcrRect.cs`, `PixelRect.cs`, `DisplayBounds.cs`, `PixelSelection.cs`, `OcrWordData.cs`, `OcrLineData.cs`, `OcrDocument.cs`) | StyleCop SA1402 (one type per file) and SA1649 (file name must match first type) are enforced as errors by `TreatWarningsAsErrors=True`. The brief's illustrative bundled file would have caused build failures. Behavior is identical; only file organization differs. |
| NuGet restore (`build-essentials.cmd`) required before first build | Expected for a new project; not a code deviation. |

## Concerns

1. **`Common.Dotnet.AotCompatibility.props` in Core csproj** — The brief requires importing this props file (enabling `IsAotCompatible=true`, `CsWinRTAotOptimizerEnabled=true`, `CsWinRTAotWarningLevel=2`). These settings are appropriate for a shared library with no UI dependencies, and compiled cleanly. No AOT-incompatible patterns were introduced.

2. **RTL test uses English words** — The `FormatDocument_RightToLeftLanguage_ReversesWordOrderPerLine` test uses the `ar-SA` culture tag but English words `"one two"` → `"two one"`. This exactly mirrors the brief's test case; the test validates word-order reversal for RTL cultures regardless of script, which is the intended behavior. The production OCR engine would supply actual Arabic text.

3. **`TableTextFormatter` trailing-row trimming** — Empty columns at the end of each row are trimmed (per brief's implementation). The sparse-row test `"\tB2"` passes because only the trailing empty entries are removed, not leading ones. This is correct.

## Task 1 Review Fix

### Fix

- Added a regression test for `ja-JP` to prove adjacent Japanese single-character words are joined without spaces.
- Updated `PowerOCR.Core\Formatting\OcrTextFormatter.cs` so Japanese language tags use `StartsWith("ja", StringComparison.OrdinalIgnoreCase)` instead of an exact `"ja"` comparison.

### RED

**Command**

```powershell
Set-Location 'C:\Users\yuleng\source\repos\PowerToys-PowerOCR-WinUI3\src\modules\PowerOCR\PowerOCR.Core.UnitTests'
..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

**Output**

```text
[BUILD] Local projects built; exiting.
```

```powershell
$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
& "$vs\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" "C:\Users\yuleng\source\repos\PowerToys-PowerOCR-WinUI3\x64\Debug\tests\PowerOCR.Core.UnitTests\PowerToys.PowerOCR.Core.UnitTests.dll" /Platform:x64 /InIsolation
```

```text
  Failed FormatDocument_JapaneseLanguage_JoinsSingleCharacterWords [33 ms]
  Error Message:
   Assert.AreEqual failed. Expected:<日本>. Actual:<日 本>.
Test Run Failed.
```

### GREEN

**Command**

```powershell
Set-Location 'C:\Users\yuleng\source\repos\PowerToys-PowerOCR-WinUI3\src\modules\PowerOCR\PowerOCR.Core.UnitTests'
..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

**Output**

```text
[BUILD] Local projects built; exiting.
```

```powershell
$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
& "$vs\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" "C:\Users\yuleng\source\repos\PowerToys-PowerOCR-WinUI3\x64\Debug\tests\PowerOCR.Core.UnitTests\PowerToys.PowerOCR.Core.UnitTests.dll" /Platform:x64 /InIsolation
```

```text
Test Run Successful.
Total tests: 10
     Passed: 10
```

### Files Changed

- `src/modules/PowerOCR/PowerOCR.Core/Formatting/OcrTextFormatter.cs`
- `src/modules/PowerOCR/PowerOCR.Core.UnitTests/OcrTextFormatterTests.cs`

### Commit SHA

- `808f25c3e2`

### Self-Review

- Scope stayed narrow: only Japanese BCP-47 handling changed.
- The regression test covers the exact `ja-JP` case from the review finding.
- No reviewer minor suggestions were addressed.
