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
