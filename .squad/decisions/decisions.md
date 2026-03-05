# Decisions Log

## Wave 2 Decisions (2026-03-04 — 2026-03-05)

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

## Wave 1 Decisions (2026-03-03 — 2026-03-04)

*(To be backfilled from previous session logs if needed)*
