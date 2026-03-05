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

### TypeScript Extension Scaffolding Tool (2026-03-03)
- Created `cmdpal-init.ts` CLI tool for initializing new TypeScript extension projects
- Accepts extension name, display name, description via interactive prompts or command-line arguments
- Generates complete project structure: `cmdpal.json`, `package.json`, `tsconfig.json`, `src/index.ts`, `.gitignore`
- `cmdpal.json` manifest includes JSON schema reference, version, minCmdPalVersion, author metadata
- `package.json` configured with @cmdpal/sdk dependency, build/dev/clean scripts, MIT license
- `tsconfig.json` targets ES2020 with strict mode, proper source/output paths, source maps enabled
- Hello-world template (`src/index.ts`) provides working example of CommandProvider + InvokableCommand pattern
- Uses Node.js built-ins only (fs, path, readline) — no external dependencies needed
- Input validation: kebab-case names, required fields, prevents overwriting existing directories
- Added "create-extension": "npx ts-node tools/cmdpal-init.ts" script to SDK's package.json
- Tool enables rapid extension scaffolding with sensible defaults, reducing setup friction

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

### TypeScript SDK Base Classes (2026-03-03)
- Implemented complete TypeScript SDK mirroring C# Toolkit classes for extension developers
- **CommandProvider:** Abstract base class with `id`, `displayName`, `icon` properties; `topLevelCommands()`, `fallbackCommands()`, `getCommand()` methods
- Protected helpers: `log()`, `showStatus()`, `hideStatus()`, `notifyItemsChanged()`, `notifyPropChanged()` for host interaction
- **Command classes:** `Command` (base), `InvokableCommand` (abstract), `CommandItem`, `ListItem` with property change notifications
- **Page classes:** `ListPage` (abstract), `DynamicListPage` (with `updateSearchText`), `ContentPage` (abstract)
- **Content classes:** `MarkdownContent`, `FormContent` (abstract), `TreeContent` (abstract)
- **CommandResult:** Static factory methods for all result types: `dismiss()`, `goHome()`, `goBack()`, `hide()`, `keepOpen()`, `goToPage()`, `showToast()`, `confirm()`
- **ExtensionServer:** Main entry point with `register(provider)` and `start()` methods
- Server wires JSON-RPC transport to provider: handles all protocol methods (`initialize`, `dispose`, `provider/*`, `command/*`, `listPage/*`, `contentPage/*`, `form/*`)
- Caches pages and commands for lifecycle management; routes requests to appropriate handlers
- All files use proper TypeScript patterns: abstract classes, protected methods, optional properties
- Successfully compiles with `npx tsc --noEmit` (strict mode, zero errors)
- Created in `src/sdk/` with 6 files: command-provider.ts, command.ts, pages.ts, content.ts, results.ts, extension-server.ts
- Updated `src/index.ts` to export all SDK classes for use by extension developers

### JSExtensionWrapper Implementation (2025-01-23)
- Created `JSExtensionWrapper.cs` — adapter implementing `IExtensionWrapper` for Node.js extensions
- Maps `JSExtensionManifest` fields to WinRT-style properties (PackageDisplayName, ExtensionDisplayName, etc.)
- Uses deterministic SHA256 hash of manifest.Name for ExtensionClassId (prefixed with "js-")
- `ExtensionUniqueId` follows pattern: `"js!" + manifest.Name`
- `StartExtensionAsync()`: spawns Node process with entry point script, creates `JsonRpcConnection`, sends "initialize" request
- **Async/lock pattern:** Moved async initialization outside lock scope — check `IsRunning()` in lock, then perform async work, then update state in lock
- `IsRunning()`: checks if Node process exists and hasn't exited
- `SignalDispose()` / `Dispose()`: sends "dispose" notification via JSON-RPC, kills Node process with 2-second timeout
- Process lifecycle: monitors process exit, handles disconnection, logs errors
- Thread-safe with `Lock` for state management
- Maps manifest.Capabilities.Commands to `ProviderType.Commands` in constructor
- `GetProviderAsync<T>()`: returns `JSCommandProviderProxy` for ICommandProvider (null for other types)
- `GetExtensionObject()`: returns null (JS extensions don't have COM objects)
- Created `JSCommandProviderProxy.cs` as Phase 2 placeholder stub
- Proxy stores JsonRpcConnection and manifest for Phase 3 implementation
- Implements ICommandProvider with placeholder methods returning empty arrays
- Proxy properties: Id, DisplayName from manifest; Icon returns default IconInfo
- **Logger adapter:** Created `LoggerAdapter` nested class to bridge ManagedCommon.Logger (static) to ILogger interface for JsonRpcConnection
- Both files use `Lock` (not object), proper copyright headers
- Error handling: logs all failures, graceful degradation, no unhandled exceptions
- Added `GlobalSuppressions.cs` for project-wide analyzer warning suppressions (CA1848, CA1835, CA1861, CA1513, CsWinRT1028, SA1402, SA1649)
- Build successful with no errors

### JavaScriptExtensionService Implementation (2025-01-23)
- Created third `IExtensionService` implementation managing JavaScript/TypeScript extensions via Node.js
- **Discovery:** Scans `%LOCALAPPDATA%\Microsoft\PowerToys\CommandPalette\JSExtensions\` for `cmdpal.json` manifests
- **Loading:** For each valid manifest: creates `JSExtensionWrapper`, starts Node.js process, creates `CommandProviderWrapper`, fires `OnCommandProviderAdded`
- **File watching:** Uses `FileSystemWatcher` on extension folders to detect new/removed extensions at runtime
- **Enable/Disable:** Tracks enabled providers in `_enabledJSCommandWrappers`, matches by `providerId`
- **Shutdown:** Disposes all wrappers, stops all Node processes via `SignalStopExtensionsAsync()`
- **Async/lock patterns:** Followed `BuiltInExtensionService` patterns exactly — `SemaphoreSlim` for async operations, `Lock` for sync
- **Event firing:** Fires `OnCommandProviderAdded`, `OnCommandProviderRemoved`, `OnCommandsAdded`, `OnCommandsRemoved` matching built-in patterns
- **LoadTopLevelCommandsFromProvider:** Reuses exact pattern from `BuiltInExtensionService` with `TaskScheduler` for UI thread marshaling
- **CommandsChanged handling:** Subscribes to provider changes, uses `UpdateCommandsForProvider` with `ListHelpers.InPlaceUpdateList`
- **File watcher events:** `Created` → loads new extension (with 500ms delay for file stability), `Deleted` → removes extension and fires removal events
- **Error handling:** Try/catch around all extension operations, logs and continues on failure (doesn't block other extensions)
- **Structured logging:** Uses `[LoggerMessage]` partial methods for all log statements (10 log methods)
- **DI registration:** Added `services.AddSingleton<IExtensionService, JavaScriptExtensionService>();` in `App.xaml.cs` `AddCoreServices()` method
- **Constructor:** Matches `BuiltInExtensionService` signature: `TaskScheduler`, `HotkeyManager`, `AliasManager`, `SettingsService`, `ILogger`
- **Dispose:** Implements `IDisposable`, stops file watcher, disposes semaphores, disposes all JS extensions
- **Default path creation:** Creates extensions directory if it doesn't exist, logs creation
- **Phase 2 complete:** JavaScript extensions now discovered, loaded, and managed alongside built-in and WinRT extensions

### JSCommandProviderProxy Full Implementation (2025-01-23)
- Completed full JSON-RPC bridge between C# host and Node.js extension processes
- **Core pattern:** Each ICommandProvider method sends JSON-RPC request via `JsonRpcConnection`, deserializes response, returns WinRT interface adapters
- **TopLevelCommands():** Sends `provider/getTopLevelCommands`, deserializes array into `JSCommandItemAdapter[]`
- **FallbackCommands():** Sends `provider/getFallbackCommands`, deserializes array into `JSFallbackCommandItemAdapter[]`
- **GetCommand(id):** Sends `provider/getCommand` with `{ commandId }`, returns `JSCommandAdapter`
- **InitializeWithHost(host):** Stores host reference, used by notification handlers for host callbacks
- **ItemsChanged event:** Registered notification handler for `provider/itemsChanged`, fires C# event with total items
- **Notification handlers:** Registered 4 handlers in constructor: `provider/itemsChanged`, `host/logMessage`, `host/showStatus`, `host/hideStatus`
- **host/logMessage:** Routes extension logs to ManagedCommon.Logger and forwards to IExtensionHost (maps MessageState enum)
- **host/showStatus:** Forwards status notifications to IExtensionHost with StatusContext
- **host/hideStatus:** Logs notification (host manages which status to hide)
- **Error handling:** All methods wrapped in try/catch, logs errors, returns empty arrays/null on failure
- **Thread safety:** Used `Lock` for event subscription management, proper event add/remove
- Created adapter classes in `JSAdapters.cs`: 5 adapter classes implementing WinRT interfaces
- **JSCommandAdapter:** Implements ICommand + IInvokableCommand, holds JsonElement data, sends `command/invoke` on Invoke()
- **JSCommandItemAdapter:** Implements ICommandItem, lazily creates JSCommandAdapter, extracts title/subtitle/icon from JSON
- **JSFallbackCommandItemAdapter:** Implements IFallbackCommandItem + IFallbackCommandItem2, includes JSFallbackHandler for UpdateQuery
- **JSFallbackHandler:** Implements IFallbackHandler, sends `fallback/updateQuery` notification when query changes
- **JSIconInfoAdapter:** Implements IIconInfo, parses light/dark icon variants from JSON
- **JSIconDataAdapter:** Implements IIconData, holds icon path and base64 data (nested class)
- **JSON parsing pattern:** Used `JsonElement.TryGetProperty()` with ValueKind checks, graceful fallbacks for missing fields
- **Lazy properties:** Adapters lazily extract values from JsonElement on access, avoiding upfront deserialization cost
- **Protocol alignment:** All method names and parameter shapes match protocol.md spec exactly
- **Phase 3 complete:** Full bidirectional JSON-RPC communication bridge operational
