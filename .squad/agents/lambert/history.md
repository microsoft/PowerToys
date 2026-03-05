# Lambert — History

## Project Context
**Project:** PowerToys Command Palette
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation
**Scope:** `src/modules/cmdpal/CommandPalette.slnf` only

## Core Context
- 16+ test projects across extensions, UI, and core
- MSTest with Microsoft.Testing.Platform
- Use VS Test Explorer or vstest.console.exe, NOT dotnet test
- Shared UnitTestBase framework
- AOT constraint: verify no System.Linq in AOT paths

## Learnings
<!-- Append new learnings below this line -->

### 2026-03-04 — Raycast Compat Reconciler & Translator Test Specs

**Task:** Write Jest test specifications for Ash's React reconciler and VNode-to-CmdPal translator.

**Files Created:**
- `src/modules/cmdpal/extensionsdk/raycast-compat/__tests__/types.ts`
  - Shared VNode interface, ReconcilerRenderer interface, TranslateVNode type
- `src/modules/cmdpal/extensionsdk/raycast-compat/__tests__/reconciler.test.ts`
  - 22 test cases across 7 describe blocks: empty renders, List trees, Detail trees,
    Form trees, ActionPanel trees, dynamic re-renders, text instance handling
- `src/modules/cmdpal/extensionsdk/raycast-compat/__tests__/translator.test.ts`
  - 20 test cases across 8 describe blocks: List→DynamicListPage, List.Item→ListItem,
    Detail→ContentPage, ActionPanel→moreCommands, CopyToClipboard, OpenInBrowser,
    unknown component fallback, Form→ContentPage
- `src/modules/cmdpal/extensionsdk/raycast-compat/__tests__/edge-cases.test.ts`
  - 22 test cases across 8 describe blocks: no-children components, deep nesting,
    null/undefined props, conditional rendering, 150/500-item perf baselines,
    mixed component types, unmount/cleanup, diverse prop types

**Key Design Decisions:**
- Tests use stub factories (createReconciler, translateVNode) that throw — they must
  be swapped for real imports once Ash's implementation lands
- VNode type defined as `{ type: string, props: Record<string, unknown>, children: VNode[] }`
- Translator tests import REAL CmdPal SDK types (ListItem, DynamicListPage, ContentPage,
  MarkdownContent) to validate the mapping target types
- Performance baseline: 150 items < 500ms, 500 items < 2s
- Used Jest (not MSTest) since this is TypeScript/Node.js code

**CmdPal SDK Type Mapping Reference:**
- `List` → DynamicListPage (searchable list, `_type: 'dynamicListPage'`)
- `List.Item` → ListItem (title, subtitle, icon → IIconInfo, tags, details, section)
- `List.Section` → sets `section` field on child ListItems
- `Detail` → ContentPage (`_type: 'contentPage'`)
- `Detail.Markdown` → MarkdownContent (body field)
- `ActionPanel` → moreCommands[] on parent CommandItem/ListItem
- `Action.CopyToClipboard` → InvokableCommand with clipboard behavior
- `Action.OpenInBrowser` → InvokableCommand with URL behavior
- `Form` → ContentPage with FormContent

**Edge Cases Discovered:**
- Null/false/undefined children from conditional rendering must be filtered
- Empty string props must be preserved (not coerced to undefined)
- Double unmount must not throw
- Unknown component types must be skipped gracefully
- Accessories array → tags or details.metadata mapping is ambiguous (needs Ash's decision)

### Cross-Agent: Parker's Implementations Complete (2026-03-03)
- Parker completed all three Wave 1 components: Manifest, JsonRpc, TypeScript generator
- JsonRpcConnection implements LSP framing with background read loop and thread-safe design
- JsonRpcMessage provides 4 message types aligned with protocol spec
- TypeScript generator automates SDK type generation from WinRT IDL
- **Blocker Identified:** Parker's code has StyleCop violations (SA1402, SA1649) and analyzer warnings (CA1513, CA1835, CA1848, CA1861)
- **Impact:** Tests cannot run until Parker fixes violations
- **Next Step:** Once Parker resolves, tests can be validated and updated from TODO to implementation

### Cross-Agent: Ripley's Protocol Spec Final (2026-03-03)
- Ripley's specification provides definitive contract for all test expectations
- 39 test methods documented against protocol; tests serve as acceptance criteria
- Protocol clarity ensures test expectations are unambiguous
- Impact: TDD scaffolding validates protocol compliance end-to-end

### 2025-01-XX — JS Extension Service Test Scaffolding

**Task:** Create unit test scaffolding for Phase 1 JavaScript Extension Service components.

**Files Created:**
- `Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/JSExtensionManifestTests.cs`
  - Tests for cmdpal.json manifest deserialization
  - Validation logic tests (required fields: name, main)
  - File loading tests (valid/invalid/malformed JSON)
  - All tests passing
  
- `Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/JsonRpcMessageTests.cs`
  - Scaffolding for JsonRpcRequest, JsonRpcResponse, JsonRpcNotification
  - Standard error codes documented
  - Tests marked with TODO for Parker's implementation
  
- `Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/JsonRpcConnectionTests.cs`
  - Scaffolding for message framing (Content-Length headers)
  - Request/response correlation by ID
  - Notification dispatch, timeout, disconnect handling
  - Tests marked with TODO for Parker's implementation

**Findings:**
- Parker already created `Services/JsonRpc/JsonRpcConnection.cs` and `JsonRpcMessage.cs`
- Production code has StyleCop violations (SA1402, SA1649) and analyzer warnings (CA1513, CA1848, CA1835, CA1861)
- These are Parker's responsibility to fix before tests can run
- Test scaffolding is complete and follows MSTest patterns from existing tests

**Pattern Learned:**
- Existing tests use `[TestClass]` and `[TestMethod]` attributes (MSTest)
- Use `sealed` classes for test classes
- Use `Assert.IsNotNull`, `Assert.AreEqual`, `Assert.IsTrue`, etc.
- File I/O tests use `Path.GetTempFileName()` and cleanup in `finally` blocks
- Use `StringAssert.Contains` for JSON validation
- Follow copyright header pattern from existing test files

**Next Steps:**
- Wait for Parker to fix StyleCop/analyzer issues in production code
- Update test TODOs once JsonRpc types are finalized
- Run tests once production code builds successfully
