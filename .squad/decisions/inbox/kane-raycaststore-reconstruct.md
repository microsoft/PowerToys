# Decision: DLL Decompilation as Disaster Recovery

**By:** Kane (C# Extension Dev)
**Date:** 2026-03-05
**Status:** Executed

## What
Reconstructed the entire `Microsoft.CmdPal.Ext.RaycastStore` extension from its compiled DLL using `ilspycmd` decompilation after the source directory was accidentally deleted.

## How
1. Installed `ilspycmd` via `dotnet tool install --global ilspycmd`
2. Decompiled the DLL with `-p` (project) flag to get full source structure
3. Cleaned decompiled output: removed WinRT vtable artifacts, replaced DLL references with proper project references, added copyright headers, restored `partial` class keywords
4. Reconstructed the `.csproj` from the WinGet extension template (proper imports, nullable enable, output path)
5. Build verified with 0 errors, 0 warnings

## Why
Source files were the only copy — no backup existed. The compiled DLL in the build output preserved all logic perfectly.

## Impact
- Full source recovery achieved — 28 files across 4 directories
- Build clean, all types/logic preserved from original
- No functional changes from the pre-deletion codebase
