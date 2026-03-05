# TypeScript SDK Base Classes

**Date:** 2026-03-03  
**Author:** Parker (Core/SDK Dev)  
**Status:** Implemented  
**Impact:** High — Enables TypeScript extension development with C#-style API

## Context

The Command Palette extension SDK needed TypeScript base classes to mirror the C# Toolkit, making it easy for JavaScript/TypeScript developers to build extensions with a familiar object-oriented pattern.

## Decision

Implemented complete SDK base class hierarchy in `src/modules/cmdpal/extensionsdk/typescript/src/sdk/`:

1. **CommandProvider** (`command-provider.ts`)
   - Abstract base class with required `id` and `displayName` getters
   - Optional `icon`, `topLevelCommands()`, `fallbackCommands()`, `getCommand(id)`
   - Protected host interaction: `log()`, `showStatus()`, `hideStatus()`
   - Protected notifications: `notifyItemsChanged()`, `notifyPropChanged()`
   - Internal `_initializeWithHost(transport)` for framework setup

2. **Command Classes** (`command.ts`)
   - `Command`: Base class with `id`, `name`, `icon`, property change notifications
   - `InvokableCommand`: Abstract class with `invoke(sender?)` method
   - `CommandItem`: UI representation with `title`, `subtitle`, `command`, `moreCommands`
   - `ListItem`: Extends CommandItem with `tags`, `details`, `section`, `textToSuggest`

3. **Page Classes** (`pages.ts`)
   - `ListPage`: Abstract base for list-based pages, implements `getItems()`, `loadMore()`
   - `DynamicListPage`: Extends ListPage with `updateSearchText(old, new)` for search
   - `ContentPage`: Abstract base for content pages, implements `getContent()`

4. **Content Classes** (`content.ts`)
   - `MarkdownContent`: Simple wrapper with `body` property
   - `FormContent`: Abstract class with `submitForm(inputs, data)` method
   - `TreeContent`: Abstract class with `getChildren()` for hierarchical content

5. **Command Results** (`results.ts`)
   - `CommandResult` with static factory methods:
     - `dismiss()`, `goHome()`, `goBack()`, `hide()`, `keepOpen()`
     - `goToPage(pageId, mode?)`, `showToast(message, dismissAfterMs?)`
     - `confirm(title, description, primaryCommand)`

6. **Extension Server** (`extension-server.ts`)
   - Main entry point: `register(provider)` and `start()`
   - Wires JSON-RPC transport to provider methods
   - Handles all protocol methods: lifecycle, provider, command, page, form
   - Manages page/command caching and lifecycle
   - Routes requests to appropriate handlers with error handling

## Rationale

### Object-Oriented Design
- Mirrors C# Toolkit API for consistency across languages
- Uses abstract base classes and inheritance familiar to C# developers
- Property change notifications match WinRT/XAML patterns

### Developer Experience
- Simple `register()` and `start()` entry point
- Protected methods for host interaction hide JSON-RPC complexity
- Static factory methods for CommandResult reduce boilerplate
- Abstract methods force implementation of required functionality

### Type Safety
- All classes typed to generated WinRT interfaces
- Compilation verified with `tsc --noEmit` (strict mode, zero errors)
- IntelliSense support for extension developers in VS Code

### Framework Integration
- ExtensionServer handles all JSON-RPC protocol details
- Automatic routing between protocol methods and provider/page methods
- Page and command caching managed transparently
- Property/items change notifications bridged to host

## Alternatives Considered

1. **Functional API**: Rejected — OOP better matches C# Toolkit patterns
2. **Manual JSON-RPC handling**: Rejected — too error-prone for extension authors
3. **Different class names**: Rejected — consistency with C# Toolkit is critical

## Implementation Notes

- Created 6 TypeScript files in `src/sdk/` directory
- Updated `src/index.ts` to export all SDK classes
- All classes use proper TypeScript patterns: abstract classes, protected methods, optional properties
- Followed C# naming conventions where possible (but camelCase for JavaScript convention)
- Handled interface mapping: TypeScript methods call JavaScript-friendly versions (e.g., `TopLevelCommands()` → `topLevelCommands()`)

## Verification

- TypeScript compilation succeeds with strict mode enabled
- No errors from `npx tsc --noEmit`
- All generated type interfaces properly implemented
- Protocol methods match specification in `docs/protocol.md`

## Next Steps

1. Create example extension using new SDK base classes
2. Write SDK documentation with usage examples
3. Add JSDoc comments to all public methods
4. Consider adding helper utilities (icon builders, tag builders, etc.)

## Related Decisions

- [TypeScript Type Generator](../archive/typescript-type-generator.md) — Generated type interfaces
- [JSON-RPC Protocol](../archive/json-rpc-protocol.md) — Transport layer
- [JavaScript Extension Architecture](../archive/javascript-extension-architecture.md) — Overall design

## References

- C# Toolkit: `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/`
- Protocol spec: `src/modules/cmdpal/extensionsdk/typescript/docs/protocol.md`
- Generated types: `src/modules/cmdpal/extensionsdk/typescript/src/generated/types.ts`
