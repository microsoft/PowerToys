# Decision: JSExtensionWrapper Implementation

**Date:** 2025-01-23  
**Status:** Implemented  
**Agent:** Parker

## Context

Phase 2 requires bridging Node.js extensions into the CmdPal host. The host expects `IExtensionWrapper` implementations that manage extension lifecycle and provide access to providers like `ICommandProvider`.

## Decision

Implemented `JSExtensionWrapper` as an adapter that:

1. **Maps manifest to WinRT properties:** Adapts JS manifest fields (name, displayName, version, publisher) to WinRT-style package properties (PackageDisplayName, ExtensionDisplayName, PackageFullName, etc.)

2. **Uses deterministic identifiers:** 
   - `ExtensionUniqueId`: `"js!" + manifest.Name`
   - `ExtensionClassId`: `"js-" + SHA256(manifest.Name)[0..32]`
   - `PackageFullName` / `PackageFamilyName`: `"js!" + manifest.Name`

3. **Spawns Node.js process:** `StartExtensionAsync()` launches `node {manifest.Main}` with stdin/stdout redirected, working directory set to manifest location

4. **Establishes JSON-RPC connection:** Creates `JsonRpcConnection` to communicate with Node process over LSP-style framing

5. **Sends initialize request:** After starting Node, sends `{"method": "initialize", "params": {"extensionId": "..."}}` to let extension know it's active

6. **Monitors process lifecycle:** 
   - Subscribes to `Process.Exited` event
   - Subscribes to `JsonRpcConnection.OnDisconnected`
   - Logs crashes/disconnections
   - Sets not-running state on unexpected exit

7. **Implements disposal:** `SignalDispose()` sends "dispose" notification, waits 2 seconds, then kills Node process

8. **Provides proxy:** `GetProviderAsync<ICommandProvider>()` returns `JSCommandProviderProxy` that will forward calls to Node via JSON-RPC (Phase 3)

9. **No COM object:** `GetExtensionObject()` returns null (JS extensions don't have WinRT objects)

10. **Maps capabilities:** Constructor reads `manifest.Capabilities.Commands` and calls `AddProviderType(ProviderType.Commands)` if true

## Created Files

- **`JSExtensionWrapper.cs`** (339 lines): Main adapter class implementing `IExtensionWrapper`
- **`JSCommandProviderProxy.cs`** (77 lines): Phase 2 stub implementing `ICommandProvider` — to be fleshed out in Phase 3

## Key Implementation Details

### Thread Safety
- Uses `Lock` (not `object`) for state synchronization
- Protects `_nodeProcess`, `_rpcConnection`, `_commandProviderProxy`, `_isDisposed`
- `IsRunning()` checks null state and process exit status under lock

### Process Management
- Spawns with `RedirectStandardInput/Output/Error`, `CreateNoWindow = true`
- Enables `EnableRaisingEvents` to catch crashes
- Kills process tree on disposal: `Kill(entireProcessTree: true)`

### Error Handling
- All failures logged with structured logging (ExtensionName parameter)
- Try-catch around process start, RPC send, disposal
- Graceful degradation: initialization failure → calls `SignalDispose()` and returns
- 2-second timeouts on disposal operations

### Version Parsing
- Parses semantic version string (e.g., "1.2.3")
- Splits on `.`, converts to ushort for Major/Minor/Build
- Defaults to 1.0.0 if invalid

### InstalledDate
- Returns `File.GetCreationTimeUtc()` of cmdpal.json
- Fallback to `DateTimeOffset.UtcNow` if file operations fail

## Consequences

**Positive:**
- Clean separation: wrapper handles lifecycle, proxy handles provider logic
- WinRT compatibility: maps JS concepts to WinRT patterns expected by host
- Robust: handles crashes, disconnections, disposal edge cases
- Testable: all state protected by lock, clear boundaries

**Negative:**
- SHA256 hash for ExtensionClassId may be overkill (could just use name)
- No support for other provider types yet (only `ICommandProvider`)
- 2-second timeouts are arbitrary (could be configurable)

**Phase 3 Work:**
- Flesh out `JSCommandProviderProxy` to forward TopLevelCommands, GetCommand, etc. via JSON-RPC
- Implement command execution logic
- Handle ItemsChanged events from extension
- Add tests for lifecycle management

## Alternatives Considered

1. **GUID from manifest name:** Could use Guid.NewGuid(namespace, name) but SHA256 is simpler and doesn't require System.Guid extensions
2. **Store IExtension mock:** Could implement fake IExtension to satisfy GetExtensionObject(), but returning null is cleaner
3. **One class for wrapper+proxy:** Could merge both into one file, but separation makes Phase 3 easier

## References

- `IExtensionWrapper` interface: defines contract
- `ExtensionWrapper.cs`: WinRT equivalent showing patterns
- `JsonRpcConnection.cs`: RPC layer for communication
- `JSExtensionManifest.cs`: manifest model
