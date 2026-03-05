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

### 2026-03-04: VNode → CmdPal Translator — Completed
**Files created/modified:**
- `src/translator/translate-vnode.ts` — Full VNode → CmdPal SDK type translator (List, Detail, Form, ActionPanel, unknown-type handling)
- `src/translator/index.ts` — Updated to re-export `translateVNode` alongside legacy `translateTree`
- `src/index.ts` — Updated exports for new translator API and types
- `jest.config.js` — Added `__tests__/` root for Lambert's spec tests
- `__tests__/translator.test.ts` — Wired to real `translateVNode` import (22 tests pass)
- `__tests__/edge-cases.test.ts` — Wired translator + softened reconciler stub (12 translator tests pass)

**Key findings:**
1. **Standalone classes beat SDK inheritance**: The CmdPal SDK classes (`DynamicListPage`, `ContentPage`) are abstract with `protected` members and transport dependencies. Creating lightweight standalone classes (`RaycastDynamicListPage`, `RaycastContentPage`) that implement the same *interface shape* is cleaner. Tests check `._type`, `.getItems()`, `.getContent()` — not `instanceof`. The bridge layer can wrap these later.
2. **Icon mapping: string → `{ light: { icon: str }, dark: { icon: str } }`**: Raycast passes icons as strings (paths, emoji, or Icon enum values). CmdPal's `IIconInfo` needs `{ light: IIconData, dark: IIconData }`. Emoji icons work because CmdPal's `IIconData.icon` accepts any string glyph.
3. **Accessories → Tags mapping**: Raycast `accessories` (array of `{ text?, icon?, tooltip? }`) map to CmdPal's `ITag[]`. Each accessory with text becomes a tag. Icon-only accessories get empty-string text tags.
4. **Dual markdown source pattern**: Raycast uses *both* `Detail.props.markdown` (legacy) and `<Detail.Markdown content="..." />` (child component). The translator checks children first, falls back to `props.markdown`.
5. **Null children in VNode arrays**: React conditional rendering (`{condition && <Item />}`) can produce null entries in children arrays. The translator's `isVNode()` guard handles this gracefully.
6. **Form → ContentPage stub**: Forms produce a ContentPage with markdown-rendered field descriptions. Full FormContent integration (templateJson/dataJson/stateJson) will come with the bridge layer.
7. **`translateVNode` uses broad input type `{ type: string; props: Record<string, unknown>; children: unknown[] }`**: This accepts both the reconciler's `VNode` (children: `AnyVNode[]`) and test spec VNodes (children: `VNode[]`) without type conflicts.

**Test results:** 34 translator tests pass (22 translator.test.ts + 12 edge-cases.test.ts), 9 existing reconciler spike tests still pass. 13 reconciler-dependent edge-case specs remain pending (expected — they need full reconciler wiring).
