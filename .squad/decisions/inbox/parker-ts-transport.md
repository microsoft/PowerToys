# TypeScript JSON-RPC Transport Layer

**Date:** 2025-01-23  
**Author:** Parker (Core/SDK Dev)  
**Status:** Implemented  
**Area:** Command Palette Extension SDK - TypeScript Transport

## Context

The Command Palette extension SDK supports JavaScript/TypeScript extensions that communicate with the C# host via JSON-RPC 2.0 over stdin/stdout. The C# side has `JsonRpcConnection` for this protocol. We needed the extension-side TypeScript counterpart.

## Decision

Implemented `JsonRpcTransport` class in `@cmdpal/sdk` package with the following design:

### Architecture

1. **Transport Layer** (`src/transport/json-rpc.ts`)
   - Reads from `process.stdin`, writes to `process.stdout`
   - LSP-style framing: `Content-Length: N\r\n\r\n{json}`
   - Buffered input parsing with state machine for header/payload extraction
   - Async request handling with automatic response sending
   - Fire-and-forget notification handling

2. **Message Types** (`src/transport/types.ts`)
   - `JsonRpcRequest`, `JsonRpcResponse`, `JsonRpcNotification`, `JsonRpcError`
   - Standard JSON-RPC 2.0 error codes (ParseError, InvalidRequest, MethodNotFound, etc.)
   - TypeScript interfaces with discriminated unions

3. **Public API**
   - `onRequest(method, handler)` - Register async request handler, auto-sends response
   - `onNotification(method, handler)` - Register notification handler
   - `sendNotification(method, params?)` - Send notification to host
   - `sendResponse(id, result?, error?)` - Manual response (usually auto-handled)
   - `start()` / `stop()` - Lifecycle management

### Key Implementation Details

**Input Parsing:**
- Buffer stdin data chunks
- Parse Content-Length headers (case-insensitive per LSP spec)
- Extract exact byte count of JSON payload
- Handle incomplete messages across multiple chunks
- Skip malformed messages with warning (don't crash process)

**Request Handling:**
- Look up registered handler by method name
- Execute handler async, await result
- Auto-send success response with result
- Auto-send error response if handler throws
- Send MethodNotFound if no handler registered

**Notification Handling:**
- Look up registered handler by method name
- Execute synchronously (no response)
- Log warning if no handler or handler throws

**Error Handling:**
- JSON parse errors: log warning, skip message
- Missing Content-Length: skip to next header block
- Handler exceptions: catch and send InternalError response
- No external crash propagation

### Rationale

**No External Dependencies:**
- Uses only Node.js built-ins (process, Buffer)
- Smaller bundle, no dependency management
- Matches C# side's minimal dependencies

**LSP-Style Framing:**
- Well-established protocol (used by VSCode Language Server Protocol)
- Simple header parsing, no complex streaming
- Easy to debug with content length
- Matches C# `JsonRpcConnection` implementation

**Handler Registration Pattern:**
- Familiar event-emitter style API
- Extensions register methods like `transport.onRequest('initialize', async (params) => { ... })`
- Transport handles all protocol mechanics
- Clean separation: business logic vs. protocol

**Auto-Response for Requests:**
- Simplifies extension code (no manual response.send())
- Enforces request/response contract
- Handler just returns result or throws error
- Transport wraps in proper JSON-RPC envelope

**Graceful Error Handling:**
- Malformed messages don't kill process
- Extensions keep running after handler exceptions
- Warnings logged for debugging
- Host can detect hung extensions via timeout

### Alternatives Considered

**1. Use LSP library (vscode-jsonrpc)**
- **Rejected:** Heavy dependency, VSCode-specific
- We need minimal, standalone transport
- Better control over error handling

**2. Manual response sending for all requests**
- **Rejected:** More boilerplate for extension authors
- Easy to forget response or send twice
- Auto-response is safer default

**3. EventEmitter-based API**
- **Rejected:** Less type-safe
- Handler registration with Map is simpler
- No need for full event emitter features

**4. Async notification handlers**
- **Rejected:** Notifications are fire-and-forget by spec
- Should not block or await
- Keep them synchronous for clarity

## Implementation Files

- `src/transport/types.ts` - JSON-RPC message type definitions
- `src/transport/json-rpc.ts` - Main transport implementation (250 lines)
- `src/index.ts` - Re-exports transport alongside generated types

## Testing Strategy

1. **Manual testing:** Create test extension that uses transport
2. **Integration testing:** Wire up to C# `JsonRpcConnection` and verify bidirectional communication
3. **Protocol conformance:** Verify LSP-style framing matches C# side
4. **Error handling:** Test malformed messages, missing handlers, handler exceptions

## Future Enhancements

1. **Request sending:** Add `sendRequest(method, params)` that returns Promise (client-side requests)
2. **Timeout handling:** Add configurable timeout for request handlers
3. **Graceful shutdown:** Coordinate with host on `dispose` notification
4. **Logging levels:** Make warning logging configurable
5. **Metrics:** Track message counts, handler latency

## Related Decisions

- [JSON-RPC 2.0 Connection Layer](../log/2025-01-23-jsonrpc-connection.md) - C# host-side implementation
- [TypeScript SDK Type Generator](../log/2025-01-23-typescript-types.md) - Generated types for protocol messages

## References

- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- [LSP Base Protocol](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#baseProtocol)
- Protocol spec: `src/modules/cmdpal/extensionsdk/typescript/docs/protocol.md`
