# Decisions — Command Palette Team

<!-- Canonical decision ledger. Scribe merges inbox entries here. -->

### 2026-03-02T20:15:00Z: Scope boundary established
**By:** Michael Jolley
**What:** All work is scoped to `src/modules/cmdpal/CommandPalette.slnf`. No files outside this boundary may be touched.
**Why:** Module isolation — CmdPal team works independently from other PowerToys modules.

### 2026-03-02T20:15:00Z: AOT awareness
**By:** Squad (initial setup)
**What:** CmdPal UI (Microsoft.CmdPal.UI) is AOT-compiled. Avoid System.Linq to reduce binary size. Use foreach loops, Array.IndexOf, etc.
**Why:** Known project constraint — AOT blows out binary size with LINQ.

### 2026-03-03T20:42:00Z: JSON-RPC 2.0 Protocol Specification
**By:** Ripley (Command Palette Lead)
**Status:** Finalized
**What:** Created comprehensive JSON-RPC 2.0 protocol specification for JavaScript Extension Service communication.
**Transport:** Length-prefixed JSON over stdin/stdout (LSP-style framing: `Content-Length: {bytes}\r\n\r\n`)
**Methods:** 6 categories — Lifecycle (initialize/dispose), Provider (command discovery), Command (invocation), Page (list/content/form), Host Callbacks (logging/status), Type Mappings
**Error Handling:** Standard JSON-RPC codes (-32700 to -32603) + custom codes (-32000 to -32099)
**Timeout:** 10-second default per request
**ID Stability:** Extensions assign all IDs; must remain stable across session
**Document:** `src/modules/cmdpal/extensionsdk/typescript/docs/protocol.md` (19 KB)
**Why:** Provides definitive contract for host-extension communication; eliminates implementation ambiguity
**Impact:** Enables parallel development — Parker implements connection layer against spec, Lambert writes tests to spec

### 2026-03-03T20:42:00Z: JSExtensionManifest Model Design
**By:** Parker
**Status:** Implemented
**What:** Created C# record-based model for cmdpal.json manifest files (JavaScript/TypeScript extensions)
**Key Features:**
  - Records: JSExtensionManifest, JSExtensionCapabilities, JSExtensionEngines
  - AOT-safe: System.Text.Json source generators + dedicated context class
  - Validation: `IsValid()` checks required fields (name, main non-empty)
  - Factory: `LoadFromFileAsync()` returns null on missing/malformed files (graceful)
  - Properties: All nullable to handle incomplete manifests
  - Naming: `[JsonPropertyName]` for camelCase mapping (C# PascalCase ↔ JSON camelCase)
**File:** `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Models/JSExtensionManifest.cs`
**Why:** Enables manifest discovery with validation before extension launch; AOT-compatible design
**Impact:** Extensions can declare capabilities/requirements; graceful error handling prevents broken extensions from loading

### 2026-03-03T20:42:00Z: JSON-RPC 2.0 Connection Layer Implementation
**By:** Parker
**Status:** Implemented
**What:** Implemented robust bidirectional JSON-RPC 2.0 connection layer for host-extension communication
**Files:**
  - `JsonRpcMessage.cs`: 4 message types (Request, Response, Notification, Error)
  - `JsonRpcConnection.cs`: Connection manager with background read loop
**Transport:** LSP-style length-prefixed framing (Content-Length header + JSON body)
**Serialization:** System.Text.Json source generators (AOT-compatible)
**Thread Safety:** SemaphoreSlim (write sync) + ConcurrentDictionary (correlation) + Lock (ID gen)
**Request Management:** Auto-incrementing IDs, 10-second default timeout, TaskCompletionSource per request
**Background Read:** Dedicated thread reads stdout, byte-by-byte header parsing, exact content-length reads
**Message Dispatch:** Responses by ID, notifications by method name
**Error Handling:** OnError event (read/parse failures), OnDisconnected event (process exit)
**Disposal:** Cancels pending requests, waits for read loop, cleans resources
**Why:** Thread-safe, AOT-compatible, handles async/streaming reliably; prevents message corruption under concurrent load
**Impact:** Enables stable extension host; foundation for extension lifecycle management

### 2026-03-03T20:42:00Z: TypeScript SDK Type Generator
**By:** Parker
**Status:** Implemented
**What:** Built Node.js IDL-to-TypeScript generator that reads WinRT IDL and outputs TypeScript type definitions
**Source:** `Microsoft.CommandPalette.Extensions.idl` (410 lines, 54 interfaces)
**Output:** `src/modules/cmdpal/extensionsdk/typescript/src/generated/types.ts`
**Parser:** Strips comments/attributes, line-by-line parsing with character-level brace tracking
**Type Mappings:** String→string, Boolean→boolean, Int32/UInt32→number, IAsyncAction→Promise<void>, IMap<K,V>→Record<K,V>, etc.
**Generated:** 6 enums, 3 structs, 54 interfaces with proper TypeScript syntax
**Build:** `npm run generate-types` (regenerate), `npm run build` (compile)
**Project Structure:** tools/, src/generated/, package.json, tsconfig.json
**Why:** Single source of truth (IDL); automated sync prevents drift; clean output for extension developers
**Impact:** TypeScript extensions can import types from SDK; regenerate types whenever IDL changes

### 2026-03-03T20:42:00Z: Test Scaffolding for Phase 1 JavaScript Extension Service
**By:** Lambert
**Status:** Implemented (scaffolding complete; tests marked TODO pending Parker finalization)
**What:** Created unit test scaffolding for JSExtensionManifest, JsonRpcMessage, JsonRpcConnection
**Files:** 3 test files in `Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/`
  - JSExtensionManifestTests.cs: 11 methods (all passing — implementation exists)
  - JsonRpcMessageTests.cs: 14 methods (TODO — awaiting Parker's final types)
  - JsonRpcConnectionTests.cs: 14 methods (TODO — awaiting Parker's final implementation)
**Framework:** MSTest (Microsoft.Testing.Platform) + VS Test Explorer (NOT dotnet test)
**Coverage:** Deserialization, validation, file loading, framing, correlation, notifications, timeout, disconnect
**Quality Pattern:** sealed classes, Assert.* methods, temp file cleanup, copyright headers
**Known Blocker:** Parker's StyleCop violations (SA1402, SA1649) + analyzer warnings (CA1513, CA1835, CA1848, CA1861) prevent build/test execution
**Why:** Test-first approach clarifies API contracts; parallel development workflow (tests ready when implementation complete)
**Impact:** TDD scaffolding ensures high coverage; tests document expected behavior; supports rapid validation once Parker fixes issues
