# Kane — History

## Project Context
**Project:** Microsoft PowerToys — Command Palette Module
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation
**Scope:** `src/modules/cmdpal/CommandPalette.slnf`

## Role Context
Kane is the C# Extension Dev, specializing in built-in CmdPal extensions that ship with PowerToys. Primary reference pattern is the WinGet extension which demonstrates DynamicListPage, ListItem, InvokableCommand with progress reporting, and HttpClient-based API integration.

## Learnings

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
