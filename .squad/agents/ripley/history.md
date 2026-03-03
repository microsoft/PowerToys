# Ripley — History

## Project Context
**Project:** PowerToys Command Palette
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation
**Scope:** `src/modules/cmdpal/CommandPalette.slnf` only

## Core Context
- CmdPal is an extensible command launcher with WinRT extension SDK
- 16 built-in extensions (Apps, Calculator, Shell, WebSearch, WinGet, etc.)
- AOT-compiled UI — avoid System.Linq, minimize reflection
- MVVM architecture: ViewModels separate from XAML views
- C++ keyboard hook service for global activation

## Learnings
<!-- Append new learnings below this line -->

### Cross-Agent: Parker's JSON-RPC Implementation (2026-03-03)
- Parker completed JsonRpcMessage.cs and JsonRpcConnection.cs implementing the protocol spec
- Uses System.Text.Json source generators for AOT compatibility
- Thread-safe: SemaphoreSlim + ConcurrentDictionary + Lock pattern
- Background read loop handles LSP framing with byte-by-byte header parsing
- 10-second default timeout with proper cancellation propagation
- **Blocker:** StyleCop violations (SA1402, SA1649) prevent test execution — Parker to fix
- Impact: Protocol spec successfully translated to production code

### JSON-RPC Protocol Specification (2024-Q1)
- Documented JSON-RPC 2.0 with LSP-style length-prefixed framing over stdin/stdout
- Protocol covers 6 primary method categories: Lifecycle (initialize/dispose), Provider (command discovery/metadata), Command (invocation), Page (list/content/form), Host Callbacks (logging/status), and Type Mappings
- Established 10-second default timeout for requests; extensions must be responsive
- ID stability required across requests: extensions assign IDs, host uses them for references
- Error handling uses standard JSON-RPC codes (-32700 to -32603) plus custom codes (-32000 to -32099)
- Complete message flow example provided from initialization through command execution and page interaction
