# Ash — History

## Project Context
**Project:** Microsoft PowerToys — Command Palette Module
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation + Node.js/TypeScript for JS extensions
**Scope:** `src/modules/cmdpal/CommandPalette.slnf`

## Role Context
Ash is the React/Reconciler Specialist, focused on building the compatibility bridge that allows Raycast extensions to run inside CmdPal. This involves a custom React reconciler that captures VNode trees, a `@raycast/api` shim package, and translation logic from Raycast UI primitives to CmdPal SDK types.

Key architectural decisions from feasibility study:
- Target Windows-tagged Raycast extensions ONLY (reject macOS-only)
- Extension source code is NEVER modified — build tool handles module aliasing
- `@raycast/api` → `@cmdpal/raycast-compat` via esbuild alias
- Custom reconciler captures React trees as VNode objects, translator maps to CmdPal types
- Push-to-pull model: React re-renders → itemsChanged notification → CmdPal re-fetches
- Extensions are open-source on GitHub (raycast/extensions repo), built from source

## Learnings

### 2026-03-04: React Reconciler Spike — Completed
**Files created:**
- `src/modules/cmdpal/extensionsdk/raycast-compat/` — new package root
- `src/reconciler/vnode.ts` — VNode/TextVNode/Container types
- `src/reconciler/host-config.ts` — HostConfig implementation (~20 methods)
- `src/reconciler/reconciler.ts` — ReactReconciler instantiation
- `src/reconciler/render.ts` — render() and renderToVNodeTree() public API
- `src/components/markers.tsx` — Fake @raycast/api marker components (List, Detail, ActionPanel, Action, Form, Grid + sub-components)
- `src/translator/index.ts` — VNode → CmdPal type translator stub (List → TranslatedListPage, Detail → TranslatedDetailPage)
- `src/__tests__/reconciler.test.tsx` — 9 tests covering tree capture, hooks, commit callback, and translation

**Key findings:**
1. **react-reconciler 0.32 + @types/react-reconciler mismatch**: The @types package lags behind the runtime API. `flushSync` was renamed to `flushSyncWork`, `commitUpdate` param order differs, and newer methods like `requestPostPaintCallback` don't exist in types. Solution: use `Record<string, any>` for hostConfig type and cast reconciler calls.
2. **LegacyRoot (mode 0) required for synchronous rendering**: ConcurrentRoot (mode 1) defers work, making the tree empty after `updateContainer`. LegacyRoot ensures the VNode tree is populated synchronously, which is what we need for the pull-model bridge (CmdPal requests data, we need it ready immediately).
3. **Marker components work via createElement(string, ...)**: FC wrappers call `React.createElement(displayName, props, children)` with a string type. The reconciler sees these as host elements and calls `createInstance(displayName, ...)`, producing VNodes with the correct type strings.
4. **React hooks (useState, useEffect) work transparently**: The reconciler doesn't interfere with hook state — React's fiber system manages hooks, and our reconciler just captures the output tree. Confirmed with a test using `useState`.
5. **onCommit callback pattern works for push-to-pull bridge**: `resetAfterCommit` fires after each React commit, allowing the bridge to detect re-renders and emit `itemsChanged` notifications.
6. **VNode children must be `AnyVNode[]` (not `VNode[]`)**: Text nodes can appear as children, so the union type `VNode | TextVNode` is needed throughout.
