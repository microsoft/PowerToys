# Decisions Log

## Wave 2 Decisions (2026-03-04 — 2026-03-05)

### 2026-03-05: Rate Limit Error Handling — User-Friendly Messages

**Author:** Kane (C# Extension Dev)  
**Status:** Implemented  
**Scope:** `tools/pipeline/src/cli.ts`, `Microsoft.CmdPal.Ext.RaycastStore/Pages/`

**Decision:** Surface GitHub API rate limit errors gracefully to users instead of failing silently. Three touchpoints:

1. **Pipeline CLI** (`cli.ts`): Detect 429/403 errors with "rate limit" text, suggest GITHUB_TOKEN in `displayError()`
2. **Install UI** (`InstallExtensionCommand.cs`): Added `FormatErrorMessage()` to parse rate limit/404 errors, show user-friendly toast instead of raw pipeline output
3. **Browse UI** (`BrowseExtensionsPage.cs`): Check `github.RateLimit.Remaining == 0` and show GITHUB_TOKEN hint in empty state

**Rationale:**
- GitHub API limits: 60/hr unauthenticated, 5000/hr with GITHUB_TOKEN
- Rate limiting is common during browsing without proper token setup
- Users deserve actionable guidance, not generic "failed" messages
- Consistent error handling across install and browse flows

**Impact:** Users now see "Rate limit exceeded — set GITHUB_TOKEN environment variable" instead of silent failures. Improves discoverability of extension store feature.

---

### 2026-03-05: VNode → CmdPal Translator — Standalone Classes

**Author:** Ash (React/Reconciler Specialist)  
**Status:** Implemented  
**Scope:** `src/modules/cmdpal/extensionsdk/raycast-compat/src/translator/`

**Decision:** Use standalone lightweight classes (`RaycastDynamicListPage`, `RaycastContentPage`, `RaycastListItem`, `RaycastMarkdownContent`) that implement the same interface shape as the SDK classes, rather than extending the SDK abstract bases.

**Rationale:**
1. **No transport dependency**: SDK bases require `JsonRpcTransport` injection and have `protected` methods that complicate testing.
2. **Cross-package isolation**: `raycast-compat` stays independent of TypeScript SDK package.
3. **Test compatibility**: Tests check `._type`, `.getItems()`, `.getContent()` — not `instanceof`. Duck typing works.
4. **Bridge wrapping**: Future bridge layer can wrap these lightweight objects with SDK transport hooks.

**Key Mappings:**
| Raycast VNode | CmdPal Output | Key Fields |
|---|---|---|
| `List` | `RaycastDynamicListPage` | `_type='dynamicListPage'`, `name`, `placeholderText`, `getItems()` |
| `List.Item` | `RaycastListItem` | `title`, `subtitle`, `icon`, `section`, `tags`, `moreCommands` |
| `Detail` | `RaycastContentPage` | `_type='contentPage'`, `name`, `getContent()` |
| `Detail.Markdown` | `RaycastMarkdownContent` | `type='markdown'`, `body` |
| `ActionPanel/Action` | `ContextItem[]` | `title`, `icon` on `moreCommands` |
| `Form` | `RaycastContentPage` | Stub: form fields as markdown content |
| Unknown | `null` | Graceful fallback |

**API:** `import { translateVNode } from '@cmdpal/raycast-compat'; const page = translateVNode(vnodeTree);`

**Impact:** Lambert's 34 test specs now pass. 13 reconciler-dependent specs remain pending. Bridge layer must wrap classes with SDK transport hooks.

---

### 2026-03-05: Raycast API Stub Architecture

**Author:** Parker (Core/SDK Dev)  
**Status:** Implemented (spike)  
**Scope:** `src/modules/cmdpal/extensionsdk/raycast-compat/src/api-stubs/`

**Key Decisions:**

1. **One-file-per-category, barrel export**: Each Raycast API category gets its own file. Extensions import from barrel (`index.ts`).
2. **Bootstrap via internal functions**: `_configureEnvironment()`, `_setStoragePath()`, `_setPreferencesPath()` called by runtime before user code runs.
3. **LocalStorage is file-backed JSON**: Simplest persistence at `<supportPath>/local-storage.json`. In-memory cache avoids repeated disk reads.
4. **Icon mapping: Segoe MDL2 + emoji**: ~170 mappings to Windows glyphs or emoji. Unknown icons get generic fallback.
5. **AI features throw, not fail silently**: `AI.ask()` throws clear error, lets extension error handling take over.
6. **Hooks are real React hooks**: `useCachedPromise` and `useFetch` use actual `useState`/`useEffect` so they work inside reconciler.
7. **Navigation is stub-only (for now)**: `closeMainWindow()`, `popToRoot()`, `launchCommand()` are console stubs. Full bridge is separate task.

**Stubs Implemented:** toast, clipboard, localStorage, environment, preferences, icons, colors, navigation, AI, hooks.

**Impact:** Ash's hooks work in captured tree. Manifest translator preferences flow: translator → installer → stubs.

---

### 2026-03-05: GitHub API Client for Raycast Extension Browsing

**Author:** Kane  
**Status:** Implemented  
**Scope:** `tools/github-client/`

**Key Decisions:**

1. **Git Trees API fallback**: Contents API truncates at ~1000 entries; `listExtensions()` auto-falls back to Trees API.
2. **Windows compatibility heuristic**: If `platforms` absent or empty → assume all platforms. If present → must include "windows" (case-insensitive).
3. **No external HTTP deps**: Uses Node.js 18+ built-in `fetch`.
4. **In-memory TTL cache**: 5min default; 10min for expensive Windows-only filtering (batch-fetches in groups of 10).
5. **Standalone package**: Own `package.json`/`tsconfig`/`jest`, follows `manifest-translator` pattern.

**Features:** List extensions, fetch manifests, search by keyword, download source, filter Windows-compatible.

**Impact:** Enables Raycast Extension Store feature to discover and filter 1000+ extensions efficiently. C# extension can pre-index during build.

---

### 2026-03-05: Pipeline Architecture for Raycast Extension Installation

**Author:** Parker (Core/SDK Dev)  
**Status:** Implemented  
**Scope:** `tools/pipeline/`

**Decision:** Built a 6-stage pipeline as a standalone TypeScript library with sibling `file:` dependencies on the github-client and bundler packages.

**Key choices:**

1. **Install marker in `raycast-compat.json`**: Added `installedBy: "raycast-pipeline"` field so `listInstalledExtensions()` can distinguish pipeline-managed extensions from manually-installed or native CmdPal extensions.

2. **Fail-fast with full cleanup**: Any stage failure immediately stops the pipeline and cleans up temp dirs. Cleanup itself is non-fatal (best-effort), preventing half-installed extensions.

3. **npm shell-out instead of programmatic**: Using `npm install` shell command is simpler and matches developer expectations. CmdPal store gate verifies Node.js is already installed.

4. **Staging build directory**: Build output goes to a separate temp dir (not inside download temp), then copied to final install location. Failed installs leave no partial state in JSExtensions/.

5. **Dual name lookup for uninstall**: `uninstallExtension()` accepts either the CmdPal name (`raycast-clipboard`) or the Raycast name (`clipboard-history`), checking installed extensions' metadata for both.

**6-stage flow:**
| Stage | Purpose | Key Logic |
|-------|---------|-----------|
| download | Fetch extension source from GitHub | GitHub client + tree fallback |
| validate | Check `raycast-compat.json` + manifest schema | JSON schema validation |
| dependencies | Install npm dependencies | `npm install` shell-out |
| build | Run build script (tsc, webpack, etc.) | Read `package.json` build script |
| install | Copy built output to JSExtensions/ | Atomic copy from staging dir |
| cleanup | Remove temp directories | Best-effort; non-fatal on error |

**API:**
- `installRaycastExtension(name, options)` → Promise<PipelineResult>
- `uninstallExtension(nameOrCmdpalName)` → Promise<void>
- `listInstalledExtensions()` → Promise<Extension[]>
- `onProgress` callback for real-time UI updates

**Test coverage:** 47 tests across 6 suites (all passing)

**Impact:** Store UI and future tools can call `installRaycastExtension()` with just an extension name and get a fully installed extension. Pipeline result objects support detailed error reporting per stage.

---

## Wave 1 Decisions (2026-03-03 — 2026-03-04)

*(To be backfilled from previous session logs if needed)*
