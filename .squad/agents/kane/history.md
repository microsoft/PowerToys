# Kane — History

## Project Context
**Project:** Microsoft PowerToys — Command Palette Module
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation
**Scope:** `src/modules/cmdpal/CommandPalette.slnf`

## Role Context
Kane is the C# Extension Dev, specializing in built-in CmdPal extensions that ship with PowerToys. Primary reference pattern is the WinGet extension which demonstrates DynamicListPage, ListItem, InvokableCommand with progress reporting, and HttpClient-based API integration.

## Learnings

### Rate Limit Error Handling in Extension Store (2026-03)
- **Problem:** GitHub API limits hit 60/hr unauthenticated, users see silent failures
- **Solution:** Three-layer error surfacing:
  1. **Pipeline CLI** (`tools/pipeline/src/cli.ts`): Detect 429/403 errors, suggest GITHUB_TOKEN in `displayError()`
  2. **Install UI** (`InstallExtensionCommand.cs`): `FormatErrorMessage()` parses rate limit/404 errors, shows toast instead of raw output
  3. **Browse UI** (`BrowseExtensionsPage.cs`): Check `github.RateLimit.Remaining == 0`, show GITHUB_TOKEN hint in empty state
- **Rate limits:** 60/hr unauthenticated, 5000/hr with token (100x improvement)
- **Error detection:** Check for "API rate limit" text in message, or 403 status with 0 remaining requests
- **UI Pattern:** Toast for transient failures, empty state message for browsing context
- **Future:** Add environment variable detection in Setup/Preferences to guide users toward proper token setup

### GitHub API Client for Raycast Extensions (2025-07)
- **Location:** `src/modules/cmdpal/extensionsdk/raycast-compat/tools/github-client/`
- **Pattern:** Standalone TypeScript tool package under `tools/`, matching `manifest-translator` sibling layout (own `package.json`, `tsconfig.json`, `jest.config.js`)
- **GitHub Contents API** truncates at ~1000 entries — `listExtensions()` falls back to the Git Trees API for the `raycast/extensions` repo which has 1000+ extensions
- **Git Trees API walk:** resolve ref → commit → root tree → walk path segments to find subtree SHA → then `?recursive=1` for full file listing
- **Windows filtering strategy:** `platforms` field in package.json — if absent/empty, assume all platforms (Windows OK); if present, must include "windows" (case-insensitive)
- **Caching:** In-memory TTL cache (5min default, 10min for expensive Windows-only lists). Cache keys include query parameters for search deduplication
- **Rate limits:** 60/hr unauthenticated, 5000/hr with token. Client reads `x-ratelimit-*` headers and exposes `client.rateLimit`. Throws `RateLimitError` on 403 with 0 remaining
- **Testing:** 31 tests (9 cache, 22 client) — all use mocked `global.fetch`, no network calls. Tests verify Contents→Tree fallback, base64 decoding, 404 caching, rate limit errors, deduplication
- **No external HTTP deps** — uses Node.js 18+ built-in `fetch`
- **Blob downloads batched** in groups of 10 with `Promise.all` to avoid flooding the API

### Raycast Extension Store C# Extension (2025-07)
- **Location:** `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.RaycastStore/`
- **Pattern:** Built-in CmdPal extension following WinGet extension template (`DynamicListPage`, `ContentPage`, `CommandProvider`, `ExtensionHostInstance`)
- **Architecture:**
  1. `RaycastStoreCommandProvider` — ICommandProvider entry point, async Node.js detection gates the UI
  2. `BrowseExtensionsPage` — DynamicListPage with queued search chain (WinGet's pattern), progressive batch loading
  3. `ExtensionDetailPage` — ContentPage with generated markdown (metadata, commands, GitHub source link)
  4. `ExtensionListItem` — ListItem with lazy-init Details panel, category tags, author subtitle
  5. `NodeJsRequiredPage` — ContentPage shown when Node.js not found (3 install methods)
  6. `RaycastGitHubClient` — HttpClient-based GitHub API client (Git Trees + Contents API), in-memory TTL caching, rate limit handling, GITHUB_TOKEN support
- **AOT compliance:** System.Text.Json source generators (`RaycastStoreJsonContext`), no LINQ, foreach loops with index, `StringComparison.OrdinalIgnoreCase`
- **StyleCop compliance:** One type per file, readonly fields before non-readonly, CultureInfo on ToString, all classes partial
- **GitHub API strategy:** Git Trees API `?recursive=1` for directory listing, Contents API for individual package.json, base64 decode, batch fetches (groups of 10)
- **Windows filtering:** Same logic as TypeScript client — `platforms` field absent/empty = all platforms OK; if present, must include "windows" case-insensitive
- **Install pipeline:** Stubbed with toast message — depends on build pipeline lib (in progress)
- **Caching:** 10-min TTL for directory listings, 5-min for individual manifests
- **Models:** 8 separate files under `GitHub/` for StyleCop SA1402 compliance (RaycastExtensionInfo, RaycastCommand, RaycastPackageJson, RaycastPackageCommand, GitTreeResponse, GitTreeEntry, GitHubContentResponse, GitHubRateLimit)
- **Build:** Clean build with 0 errors, 0 warnings using `tools/build/build.cmd`

### RaycastStore Extension Registration (2025-07)
- **Registration pattern for built-in CmdPal extensions requires 3 touchpoints:**
  1. **Solution filter** (`CommandPalette.slnf`): Add `.csproj` path to `projects` array
  2. **App.xaml.cs**: Add `using` + `services.AddSingleton<ICommandProvider, ProviderClass>()` in `AddBuiltInCommands()`
  3. **Microsoft.CmdPal.UI.csproj**: Add `<ProjectReference>` to the extension `.csproj`
- Registration goes at the end of `AddBuiltInCommands()` (after `RemoteDesktopCommandProvider`), keeping core extensions first
- Simple providers use generic `AddSingleton<ICommandProvider, T>()` form; WinGet is special-cased with an instance because it calls `SetAllLookup()`
- Build command from extension directory: `& C:\sources\powertoys\tools\build\build.ps1` (PowerShell), not `build.cmd` (cmd.exe path resolution fails in PS)
- Registration verified by clean build — runtime verification requires launching CmdPal

### ext/ vs Extensions/ Directory Layout (2025-07)
- **`ext/` is the canonical source location** for all built-in CmdPal extensions. Git tracks source files here.
- **`Extensions/` is build output only** — contains only `obj/` and `x64/` artifacts, zero files tracked by git.
- **PowerToys.slnx** and **CommandPalette.slnf** both reference `ext/` paths for every built-in extension (Apps, Bookmark, Calc, WinGet, RaycastStore, etc.)
- **Microsoft.CmdPal.UI.csproj** references extensions via `..\ext\<ExtName>\<ExtName>.csproj`
- **Do NOT move extensions from `ext/` to `Extensions/`** — this would break the build and be inconsistent with every other extension.
- The `Extensions/` folder name is misleading but is a build artifact directory, not a source directory.

### RaycastStore Extension Disaster Recovery (2026-03)
- **Problem:** Entire `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.RaycastStore/` source directory was accidentally deleted during a failed directory move.
- **Recovery method:** Decompiled the compiled DLL (`x64/Debug/WinUI3Apps/CmdPal/Microsoft.CmdPal.Ext.RaycastStore.dll`) using `ilspycmd` (dotnet global tool), then cleaned the decompiled output to match the original source conventions.
- **Key decompilation artifacts removed:** WinRT vtable classes, `[WinRTRuntimeClassName]`/`[WinRTExposedType]` attributes (these are generated by CsWinRT at build time), `PrivateImplementationDetails` classes, and `InlineArray` compiler-generated types.
- **Critical .csproj fix:** The decompiled .csproj was unusable (referenced DLLs directly). Reconstructed from the WinGet extension pattern with proper imports: `Common.Dotnet.CsWinRT.props`, `Common.Dotnet.AotCompatibility.props`, `..\Common.ExtDependencies.props`.
- **Nullable enable:** The original project had `<Nullable>enable</Nullable>` — the decompiled code uses `?` annotations extensively, confirming this was needed.
- **Build:** Clean build with 0 errors, 0 warnings after reconstruction.
- **Lesson:** Always keep ilspycmd (`dotnet tool install --global ilspycmd`) available. With a compiled DLL + PDB, full source recovery is possible. The decompiled output needs cleanup but preserves all logic, types, and string literals perfectly.
