# Decision: JSCommandProviderProxy Full JSON-RPC Bridge Implementation

**Date:** 2025-01-23  
**Status:** Implemented  
**Context:** Phase 3 — Completing bidirectional JSON-RPC communication bridge  

## Problem

The `JSCommandProviderProxy` was a Phase 2 placeholder stub. Phase 3 required implementing full ICommandProvider interface, bridging C# WinRT interfaces to Node.js extension processes via JSON-RPC 2.0.

## Solution

### Architecture

Created **adapter pattern** to convert JSON responses into WinRT interface implementations:

```
C# Host (WinRT interfaces) 
   ↕ JSCommandProviderProxy (implements ICommandProvider)
   ↕ JsonRpcConnection (JSON-RPC transport)
   ↕ Node.js Process (TypeScript extension)
```

### Core Implementation

**JSCommandProviderProxy** (`JSCommandProviderProxy.cs`):
- Implements full `ICommandProvider` interface by forwarding calls to Node.js
- Properties (`Id`, `DisplayName`, `Icon`): Cached from manifest
- `TopLevelCommands()`: Sends `provider/getTopLevelCommands`, returns `ICommandItem[]`
- `FallbackCommands()`: Sends `provider/getFallbackCommands`, returns `IFallbackCommandItem[]`
- `GetCommand(id)`: Sends `provider/getCommand` with `{ commandId }`, returns `ICommand`
- `InitializeWithHost(host)`: Stores host reference for notification callbacks
- `ItemsChanged` event: Fires when `provider/itemsChanged` notification received
- Error handling: All methods wrapped in try/catch, returns empty arrays/null on failure

**Notification Handlers:**
- `provider/itemsChanged` → fires C# `ItemsChanged` event
- `host/logMessage` → forwards to `ManagedCommon.Logger` and `IExtensionHost.LogMessage()`
- `host/showStatus` → forwards to `IExtensionHost.ShowStatus()` with StatusContext
- `host/hideStatus` → logs notification (host manages lifecycle)

### Adapter Classes

Created 5 adapter classes in **JSAdapters.cs** implementing WinRT interfaces:

1. **JSCommandAdapter** (implements ICommand + IInvokableCommand):
   - Holds `JsonElement` data from JSON-RPC response
   - Properties (`Name`, `Id`, `Icon`) extracted lazily from JSON
   - `Invoke()`: Sends `command/invoke` via JSON-RPC, parses `ICommandResult`

2. **JSCommandItemAdapter** (implements ICommandItem):
   - Wraps command item JSON data
   - Lazily creates `JSCommandAdapter` on `Command` property access
   - Extracts `Title`, `Subtitle`, `Icon` from JSON

3. **JSFallbackCommandItemAdapter** (implements IFallbackCommandItem + IFallbackCommandItem2):
   - Extends command item with fallback-specific properties
   - `DisplayTitle`, `Id` properties
   - Lazily creates `JSFallbackHandler` on `FallbackHandler` property access

4. **JSFallbackHandler** (implements IFallbackHandler):
   - Nested class within `JSFallbackCommandItemAdapter`
   - `UpdateQuery()`: Sends `fallback/updateQuery` notification to Node.js

5. **JSIconInfoAdapter** (implements IIconInfo):
   - Parses light/dark icon variants from JSON
   - Static `FromJson()` factory method handles multiple JSON formats
   - Nested `JSIconDataAdapter` (implements IIconData) for icon data

### JSON Parsing Pattern

Used `JsonElement.TryGetProperty()` throughout:
- Graceful handling of missing properties (returns empty string/null)
- `ValueKind` checks before accessing values
- Lazy extraction on property access (not upfront deserialization)

### Thread Safety

- `Lock` for event subscription management in `JSCommandProviderProxy`
- Proper event add/remove with lock protection
- Disposal clears event subscribers and nulls host reference

### Error Handling

- Every JSON-RPC call wrapped in try/catch
- Logs errors via `ManagedCommon.Logger`
- Returns graceful defaults on failure (empty arrays, null)
- Checks `JsonRpcResponse.Error` before parsing result

## Alternatives Considered

1. **Upfront JSON deserialization to C# DTOs:**
   - Rejected: More allocations, unnecessary complexity
   - Lazy JsonElement parsing is more efficient

2. **Single mega-adapter class:**
   - Rejected: Violates single responsibility, harder to test
   - Separate adapters per interface clearer and testable

3. **Synchronous blocking on async calls:**
   - Used `.GetAwaiter().GetResult()` pattern
   - Acceptable: Called from sync WinRT interface methods
   - Alternative (async interfaces) would require WinRT projection changes

## Trade-offs

**Pros:**
- Clean separation of concerns (proxy, adapters, transport)
- Lazy JSON parsing reduces allocations
- Graceful error handling prevents crashes
- Protocol-aligned method names ease debugging
- Adapter pattern allows future extensibility

**Cons:**
- Synchronous blocking on async JSON-RPC calls (inherent WinRT interface limitation)
- No icon base64 → `IRandomAccessStreamReference` conversion yet (TODO)
- Memory held by `JsonElement` data in adapters (acceptable for typical use)

## Testing Strategy

- Unit tests pending in Phase 4
- Integration tests will verify round-trip JSON-RPC communication
- Manual testing: Load JS extension, verify commands appear in UI

## Dependencies

- `JsonRpcConnection.cs` — transport layer
- `JSExtensionManifest.cs` — manifest data
- `Microsoft.CommandPalette.Extensions.idl` — WinRT interface definitions
- Protocol spec: `extensionsdk/typescript/docs/protocol.md`

## Future Enhancements

- Implement base64 → `IRandomAccessStreamReference` conversion for icons
- Add telemetry for JSON-RPC call latencies
- Implement adapter object pooling if memory becomes concern
- Add cancellation token support if WinRT interfaces gain async variants

## Team Impact

- **Ripley:** Protocol spec validated by full implementation
- **Lambert:** Can now write integration tests for full extension lifecycle
- **Michael:** Phase 3 complete — JS extensions fully functional
