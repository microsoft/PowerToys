# Decision: VNode → CmdPal Translator — Standalone Classes

**Author:** Ash (React/Reconciler Specialist)
**Date:** 2026-03-04
**Status:** Implemented
**Scope:** `src/modules/cmdpal/extensionsdk/raycast-compat/src/translator/`

## Context

The translator converts Raycast VNode trees (captured by the reconciler) into CmdPal SDK-compatible objects. The CmdPal TypeScript SDK provides abstract base classes (`DynamicListPage`, `ContentPage`, `ListItem`, `MarkdownContent`) that extensions normally subclass.

## Decision

**Use standalone lightweight classes** (`RaycastDynamicListPage`, `RaycastContentPage`, `RaycastListItem`, `RaycastMarkdownContent`) that implement the same interface shape as the SDK classes, rather than extending the SDK abstract bases.

## Rationale

1. **No transport dependency**: SDK bases require `JsonRpcTransport` injection and have `protected` methods (`notifyPropChanged`, `_initializeWithTransport`) that complicate testing. Our classes are plain data containers.
2. **Cross-package isolation**: The `raycast-compat` package stays independent of the TypeScript SDK package. No npm dependency needed.
3. **Test compatibility**: Tests check `._type`, `.getItems()`, `.getContent()` — not `instanceof`. Duck typing works.
4. **Bridge wrapping**: The future bridge layer can wrap these lightweight objects with SDK transport hooks when connecting to CmdPal's JSON-RPC runtime.

## Key Mappings

| Raycast VNode | CmdPal Output | Key Fields |
|---|---|---|
| `List` | `RaycastDynamicListPage` | `_type='dynamicListPage'`, `name`, `placeholderText`, `getItems()` |
| `List.Item` | `RaycastListItem` | `title`, `subtitle`, `icon`, `section`, `tags`, `moreCommands` |
| `Detail` | `RaycastContentPage` | `_type='contentPage'`, `name`, `getContent()` |
| `Detail.Markdown` | `RaycastMarkdownContent` | `type='markdown'`, `body` |
| `ActionPanel/Action` | `ContextItem[]` | `title`, `icon` on `moreCommands` |
| `Form` | `RaycastContentPage` | Stub: form fields as markdown content |
| Unknown | `null` | Graceful fallback |

## Impact on Other Team Members

- **Lambert**: 34 of his test specs now pass. 13 reconciler-dependent specs remain pending.
- **Bridge layer**: Must wrap `RaycastDynamicListPage`/`RaycastContentPage` with SDK transport hooks when connecting to CmdPal runtime.
- **Form support**: Currently stubbed as markdown. Full `FormContent` (templateJson/dataJson) integration deferred to bridge layer work.

## API

```typescript
import { translateVNode } from '@cmdpal/raycast-compat';

const page = translateVNode(vnodeTree);
// Returns RaycastDynamicListPage | RaycastContentPage | null
```
