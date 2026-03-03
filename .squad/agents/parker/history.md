# Parker — History

## Project Context
**Project:** PowerToys Command Palette
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation
**Scope:** `src/modules/cmdpal/CommandPalette.slnf` only

## Core Context
- Extension SDK: WinRT interfaces in `extensionsdk/Microsoft.CommandPalette.Extensions/`
- Extension Toolkit: C# helpers in `extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/`
- 16 built-in extensions in `Exts/` (Apps, Bookmarks, Calculator, etc.)
- C++ keyboard hook: `CmdPalKeyboardService/`
- C++ module interface: `CmdPalModuleInterface/`
- Common utilities: `Microsoft.CmdPal.Common/`

## Learnings
<!-- Append new learnings below this line -->

### Cross-Agent: Lambert's Test Scaffolding (2026-03-03)
- Lambert created 39 unit tests across 3 test files
- JSExtensionManifestTests (11 methods): all passing, manifest validation verified
- JsonRpcMessageTests (14 methods): scaffolding ready, marked TODO pending type finalization
- JsonRpcConnectionTests (14 methods): scaffolding ready, marked TODO pending implementation review
- **Blocker Found:** My StyleCop violations (SA1402, SA1649) + analyzer warnings prevent build/test execution
- **Action:** Must fix violations before Lambert can run tests
- Impact: TDD scaffolding drives API design clarity; tests provide immediate feedback loop

### Cross-Agent: Ripley's Protocol Specification (2026-03-03)
- Ripley finalized comprehensive JSON-RPC 2.0 protocol specification
- Protocol defines all method categories, error codes, timeouts, type mappings
- My JsonRpcConnection implementation aligns with LSP framing specified
- My TypeScript generator aligned with protocol's type system
- Impact: Clear contract reduced ambiguity; parallel development enabled

### JSExtensionManifest Model (2025-01-23)
- Created C# model for cmdpal.json manifest files (JavaScript/TypeScript extensions)
- Used System.Text.Json source generators with dedicated `JSExtensionManifestJsonContext` for AOT compatibility
- Implemented `LoadFromFileAsync` with validation (`name` and `main` required)
- Created three record types: `JSExtensionManifest`, `JSExtensionCapabilities`, `JSExtensionEngines`
- Used `[JsonPropertyName]` for camelCase mapping (C# uses PascalCase)
- Followed ViewModels namespace convention: `Microsoft.CmdPal.UI.ViewModels.Models`

### JSON-RPC 2.0 Connection Layer (2025-01-23)
- Implemented LSP-style length-prefixed framing ("Content-Length: N\r\n\r\n" + JSON payload)
- Used `System.Text.Json` source generators (`JsonRpcSerializerContext`) for AOT-safe serialization
- Created four message types: `JsonRpcRequest`, `JsonRpcResponse`, `JsonRpcNotification`, `JsonRpcError`
- Implemented thread-safe bidirectional communication with `SemaphoreSlim` for writes and `Lock` for ID generation
- Used `ConcurrentDictionary` for request/response correlation and notification handler registration
- Background read loop on dedicated thread with cancellation token support
- 10-second default timeout on requests with proper cancellation propagation
- Graceful disposal: cancels pending requests, waits for read loop, cleans up resources
- Full error handling with `OnError` and `OnDisconnected` events for process lifecycle management

### TypeScript SDK Type Generator (2025-01-23)
- Built IDL-to-TypeScript generator that reads `Microsoft.CommandPalette.Extensions.idl` (410 lines, 54 interfaces)
- Parser handles WinRT IDL format: enums, structs, interfaces with `requires` (extends), properties with `{ get; }` / `{ get; set; }`, methods, events
- Type mappings: String→string, Boolean→boolean, Int32/UInt8/UInt32→number, IInspectable/Object→unknown, Uri→string, IAsyncAction→Promise<void>, IMap<K,V>→Record<K,V>
- Strips WinRT attributes (`[contract(...)]`, `[uuid(...)]`) and comments from IDL before parsing
- Line-by-line parser with proper brace counting to handle multi-line interfaces and empty interfaces (`{}` or `{ }`)
- Maps Windows.Foundation.IClosable→IClosable, Windows.Storage.Streams.IRandomAccessStreamReference→string
- Removes `Microsoft.CommandPalette.Extensions.` namespace prefix from type references
- Generates clean TypeScript with readonly modifiers, proper extends clauses, and export keywords
- Output: `src/generated/types.ts` with 6 enums, 3 structs (as interfaces), 54 interfaces
- Created complete package structure: tools/idl-to-ts.ts, package.json, tsconfig.json, tools/tsconfig.json
- Generator script path: `src/modules/cmdpal/extensionsdk/typescript/tools/idl-to-ts.ts`
- Build script: `npm run generate-types` to regenerate, `npm run build` to compile to dist/

### TypeScript JSON-RPC Transport Layer (2025-01-23)
- Implemented extension-side JSON-RPC transport in TypeScript as counterpart to C# `JsonRpcConnection`
- Uses LSP-style framing: reads from stdin, writes to stdout with "Content-Length: N\r\n\r\n" headers
- Created `JsonRpcTransport` class with request/notification handler registration (`onRequest`, `onNotification`)
- Outgoing methods: `sendNotification(method, params?)`, `sendResponse(id, result?, error?)`
- Buffered stdin reading with proper parsing of Content-Length headers (case-insensitive per spec)
- Routes incoming requests to registered handlers, automatically sends responses or error responses
- Routes incoming notifications to registered handlers (fire-and-forget)
- Graceful error handling: malformed messages logged and skipped, JSON parse errors don't crash process
- No external dependencies: uses only Node.js built-ins (process, Buffer)
- Created `transport/types.ts` with JSON-RPC 2.0 message interfaces and standard error codes
- Updated `src/index.ts` to export transport layer alongside generated types
- Successfully compiles with existing `tsconfig.json` (strict mode, ES2020 target)
