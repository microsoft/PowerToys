# Decision: JavaScriptExtensionService Implementation

**Date:** 2025-01-23  
**Agent:** Parker (Core/SDK Dev)  
**Requested by:** Michael Jolley  
**Status:** Implemented

## Context

Third phase of JavaScript/TypeScript extension support for Command Palette. After implementing the manifest model, JSON-RPC connection layer, and JSExtensionWrapper, we needed a service to discover, load, and manage JavaScript extensions alongside built-in and WinRT extensions.

## Decision

Implemented `JavaScriptExtensionService` as the third `IExtensionService` implementation, following the exact patterns established by `BuiltInExtensionService`.

### Key Design Choices

#### 1. **Discovery Strategy**
- **Default path:** `%LOCALAPPDATA%\Microsoft\PowerToys\CommandPalette\JSExtensions\`
- **Manifest location:** Each subdirectory must contain `cmdpal.json`
- **Validation:** Uses `JSExtensionManifest.LoadFromFileAsync()` with built-in validation
- **Future:** Settings service can provide additional paths (Phase 3)

#### 2. **Lifecycle Management**
- **Load time:** `SignalStartExtensionsAsync()` discovers and loads all extensions
- **Runtime discovery:** `FileSystemWatcher` detects new/removed extension directories
- **Shutdown:** `SignalStopExtensionsAsync()` disposes all wrappers and stops Node processes
- **Enable/Disable:** Per-provider control matching built-in extension behavior

#### 3. **Async/Lock Patterns**
Followed `BuiltInExtensionService` patterns exactly:
- **SemaphoreSlim** for async-protected collections:
  - `_getJSCommandWrappersLock` — protects `_jsCommandWrappers`
  - `_getEnabledJSCommandWrappersLock` — protects `_enabledJSCommandWrappers`
  - `_getTopLevelCommandsLock` — protects `_topLevelCommands`
- **Lock** for sync-only collections:
  - `_extensionsLock` — protects `_jsExtensions`

#### 4. **File Watching**
- **NotifyFilter:** `NotifyFilters.DirectoryName` only (new/removed folders)
- **Events:** `Created` → load extension (500ms delay for stability), `Deleted` → remove extension
- **Error handling:** Try/catch around all watcher operations, logs and continues on failure
- **Cleanup:** Disposes watcher in `SignalStopExtensionsAsync()` and `Dispose()`

#### 5. **Error Handling**
- **Per-extension isolation:** Failure to load one extension doesn't block others
- **Graceful degradation:** Missing Node.js, invalid manifests, startup failures all logged and skipped
- **Structured logging:** 10 `[LoggerMessage]` methods for consistent log formatting

#### 6. **Event Firing**
Matches `BuiltInExtensionService` event patterns exactly:
- **OnCommandProviderAdded** — fired after provider wrapper created and commands loaded
- **OnCommandProviderRemoved** — fired after provider removed from all collections
- **OnCommandsAdded** — fired after commands added to `_topLevelCommands`
- **OnCommandsRemoved** — fired after commands removed from `_topLevelCommands`

#### 7. **Command Provider Integration**
- Uses `CommandProviderWrapper(extensionWrapper, ...)` constructor (same as WinRT extensions)
- Reuses `LoadTopLevelCommandsFromProvider()` pattern with TaskScheduler for UI thread
- Subscribes to `CommandsChanged` events for dynamic command updates
- Uses `ListHelpers.InPlaceUpdateList()` for atomic list updates

### File Structure

```
src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/
├── Services/
│   ├── IExtensionService.cs              (interface)
│   ├── BuiltInExtensionService.cs        (reference implementation)
│   ├── WinRTExtensionService.cs          (existing)
│   └── JavaScriptExtensionService.cs     (new)
└── Models/
    ├── JSExtensionManifest.cs            (Phase 1)
    ├── JSExtensionWrapper.cs             (Phase 2)
    └── JSCommandProviderProxy.cs         (Phase 2 stub)

src/modules/cmdpal/Microsoft.CmdPal.UI/
└── App.xaml.cs                           (DI registration updated)
```

### DI Registration

Added to `AddCoreServices()` in `App.xaml.cs`:
```csharp
services.AddSingleton<IExtensionService, JavaScriptExtensionService>();
```

Registered alongside:
- `BuiltInExtensionService` — in-process C# extensions
- `WinRTExtensionService` — COM-activated WinRT extensions
- `JavaScriptExtensionService` — Node.js process extensions

## Alternatives Considered

### Alternative 1: Combined ExtensionService with Type Parameter
**Rejected:** Each service has fundamentally different discovery and lifecycle management. Separation maintains clarity and follows existing architecture.

### Alternative 2: Custom Event Args Instead of IEnumerable
**Rejected:** Matches existing `IExtensionService` interface contract. Changing would require updating all consumers.

### Alternative 3: Polling Instead of FileSystemWatcher
**Rejected:** FileSystemWatcher provides immediate notification with lower resource usage. Error handling addresses reliability concerns.

### Alternative 4: Single Lock for All Collections
**Rejected:** Follows `BuiltInExtensionService` pattern of separate locks per collection to minimize contention and match async/sync usage patterns.

## Consequences

### Positive
- JavaScript extensions now fully integrated into extension architecture
- Runtime discovery enables hot-loading of new extensions without restart
- Error isolation prevents one bad extension from breaking others
- Follows established patterns, making codebase easier to understand
- Structured logging provides clear diagnostics for extension issues

### Negative
- Requires Node.js installed on user machine
- File watcher adds minimal runtime overhead
- Additional service increases startup time slightly (mitigated by async loading)

### Neutral
- Default extensions path is hard-coded (can be extended via settings in future)
- 500ms delay on new extension detection (balance between file stability and responsiveness)

## Implementation Notes

### Constructor Dependencies
Same as `BuiltInExtensionService`:
```csharp
public JavaScriptExtensionService(
    TaskScheduler taskScheduler,
    HotkeyManager hotkeyManager,
    AliasManager aliasManager,
    SettingsService settingsService,
    ILogger logger)
```

### Structured Logging Methods
- `Log_LoadingJSExtensionsTook` — timing
- `Log_CreatedExtensionsDirectory` — path creation
- `Log_FailedToCreateExtensionsDirectory` — path error
- `Log_InvalidManifest` — validation failure
- `Log_FailedToStartJSExtension` — Node.js startup failure
- `Log_NoCommandProvider` — missing ICommandProvider
- `Log_LoadedJSExtension` — successful load
- `Log_FailedToLoadExtension` — general load error
- `Log_StartedFileWatcher` — watcher initialization
- `Log_FailedToStartFileWatcher` — watcher error
- `Log_FailedToStopExtension` — shutdown error

### Error Recovery
- Missing Node.js: Logs error, skips extension, continues discovery
- Invalid manifest: Logs warning, skips directory, continues discovery
- Process crash: `OnDisconnected` event logs warning, marks extension as stopped
- File watcher failure: Logs error, continues without runtime discovery

## Follow-up Actions

### Phase 3 (Future)
1. **JSCommandProviderProxy implementation** — populate proxy with actual JSON-RPC calls
2. **Settings UI integration** — allow users to add/remove extension paths
3. **Extension marketplace** — discover extensions from public registry
4. **Hot reload** — detect manifest changes and reload running extensions
5. **Permission model** — define what Node.js extensions can/cannot access

### Testing
1. Unit tests for discovery logic (mocked file system)
2. Integration tests for Node.js process lifecycle
3. End-to-end tests with sample TypeScript extensions
4. File watcher tests (create/delete directories)

### Documentation
1. Extension author guide (manifest schema, entry point requirements)
2. Deployment guide (where to place extensions)
3. Troubleshooting guide (common errors, log locations)

## Related Decisions
- `parker-manifest-model.md` — JSExtensionManifest structure (Phase 1)
- `parker-jsonrpc-connection.md` — C# JSON-RPC transport (Phase 1)
- `parker-ts-transport.md` — TypeScript JSON-RPC transport (Phase 1)
- `parker-js-wrapper.md` — JSExtensionWrapper implementation (Phase 2)

## Approval
**Status:** Implemented and integrated  
**Reviewer:** Michael Jolley (team lead)  
**Build Status:** Successful, no errors
