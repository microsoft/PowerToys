# @microsoft/cmdpal-sdk

TypeScript SDK for building [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extensions in JavaScript or TypeScript.

Extensions built with this SDK run as isolated Node.js processes and talk to the Command Palette host over JSON-RPC 2.0 via stdio, using LSP-style `Content-Length` message framing.

> This package is part of a stacked, multi-phase effort tracked in [microsoft/PowerToys#48707](https://github.com/microsoft/PowerToys/issues/48707). Phase 1 delivers the extension-author-facing SDK only. The C# host side lands in later phases.

## Requirements

- Node.js >= 22
- TypeScript >= 5.8 (for TypeScript authors)

## Installation

```sh
npm install @microsoft/cmdpal-sdk
```

## Quick start

```typescript
import {
  CommandProviderBase,
  ListPageBase,
  InvokableCommandBase,
  ExtensionHost,
  startJsonRpcServer,
  iconFromGlyph,
  type CommandResult,
  type ICommandItem,
  type IListItem,
} from '@microsoft/cmdpal-sdk';

class HelloCommand extends InvokableCommandBase {
  readonly id = 'hello';
  readonly name = 'Say hello';
  override icon = iconFromGlyph('\uE8BD');

  override invoke(): CommandResult {
    ExtensionHost.log('Hello from my extension!');
    return { kind: 'showToast', args: { message: 'Hello!' } };
  }
}

class MyListPage extends ListPageBase {
  readonly id = 'my-page';
  readonly name = 'My commands';
  readonly title = 'My commands';

  override getItems(): IListItem[] {
    return [{ command: new HelloCommand(), title: 'Say hello' }];
  }
}

class MyProvider extends CommandProviderBase {
  readonly id = 'my-extension';
  readonly displayName = 'My Extension';

  override topLevelCommands(): ICommandItem[] {
    return [{ command: new MyListPage(), title: 'My commands' }];
  }
}

startJsonRpcServer(() => new MyProvider());
```

## SDK surface

### Types

`types.ts` mirrors the Command Palette host contracts and the JSON-RPC wire protocol:

- Icons and colors: `IconData`, `IconInfo`, `Color`, `OptionalColor`, `Tag`, `KeyChord`.
- Commands and results: `ICommand`, `IInvokableCommand`, `CommandResult`, `CommandResultKind`, `NavigationMode`.
- Items: `ICommandItem`, `IListItem`, `IFallbackCommandItem`, `ContextItem`.
- Details panel: `Details`, `DetailsElement`, and the `DetailsData` union (`tags`, `link`, `commands`, `separator`).
- Pages: `IPage`, `IListPage`, `IDynamicListPage`, `IContentPage`, `Filters`, `GridProperties`.
- Content: the `Content` union (`markdown`, `form`, `tree`, `plainText`, `image`).
- Settings and host: `ICommandSettings`, `IExtensionHost`, `ICommandProvider`.

### Base classes

`base/` provides ergonomic base classes so authors implement only what they need:

- `CommandProviderBase` is the entry point for an extension.
- `ListPageBase`, `DynamicListPageBase`, and `ContentPageBase` back the page kinds.
- `InvokableCommandBase`, `CommandItemBase`, `ListItemBase`, `FallbackCommandItemBase`, and `Separator` build list content.
- `Settings` (with `ToggleSetting`, `TextSetting`, and `ChoiceSetSetting`) renders an auto-generated settings form.
- Built-in commands: `NoOpCommand`, `OpenUrlCommand`, `CopyTextCommand`, and `ConfirmableCommand`.

### Icon helpers

`helpers.ts` builds `IconInfo` values from the sources an author has on hand:

- `iconFromGlyph(glyph)` for a font glyph character.
- `iconFromBase64(data)` for base64-encoded image bytes.
- `iconFromUrl(url)` (async) to download and encode an image.
- `iconFromFile(path)` (async) to read and encode a local image file.

### Runtime

`runtime/` implements the extension side of the protocol:

- `startJsonRpcServer(factory)` (aliased as `run`) starts the stdio server, wires it to your provider, and runs until the host disconnects.
- `activate(context, factory)` is a convenience activation wrapper.
- `ExtensionHost` is the bridge back to the host (`log`, `showStatus`, `hideStatus`, `copyToClipboard`).
- `sendNotification(method, params)` emits an Extension to Host notification.

The server dispatches every Host to Extension request (`initialize`, `provider/getTopLevelCommands`, `provider/getFallbackCommands`, `provider/getCommand`, `provider/getSettings`, `command/invoke`, `listPage/getItems`, `listPage/setSearchText`, `listPage/setFilter`, `listPage/loadMore`, `fallback/updateQuery`, `contentPage/getContent`, `form/submit`, `dispose`) and can emit Extension to Host notifications (`listPage/itemsChanged`, `command/propChanged`, `host/logMessage`, `host/showStatus`, `host/hideStatus`, `host/copyText`).

## Development

```sh
npm install        # install dev dependencies
npm run build      # type-check and emit to dist/
npm test           # type-check, then run the vitest suite
npm run lint       # eslint
npm run format     # prettier --write
npm run check      # typecheck + lint + format:check + test
```

Only source is committed. The `dist/` output is produced by `npm run build` and is git-ignored.

## License

MIT. Copyright (c) Microsoft Corporation.
