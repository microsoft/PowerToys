# @microsoft/cmdpal-sdk

TypeScript SDK for building Command Palette extensions.

## Quick Start

```bash
npm install @microsoft/cmdpal-sdk
```

```typescript
import {
  CommandProviderBase,
  ListPageBase,
  ListItemBase,
  type ActivationContext,
} from '@microsoft/cmdpal-sdk';

class MyListPage extends ListPageBase {
  id = 'my-list';
  name = 'My List';
  title = 'My Extension';
  placeholderText = 'Search...';

  getItems() {
    return [
      new ListItemBase({
        command: { id: 'item-1', name: 'Hello World' },
        title: 'Hello World',
        subtitle: 'My first extension item',
      }),
    ];
  }
}

class MyProvider extends CommandProviderBase {
  id = 'my-extension';
  displayName = 'My Extension';

  private page = new MyListPage();

  topLevelCommands() {
    return [{ command: this.page, title: this.page.title }];
  }

  getCommand(id: string) {
    if (id === this.page.id) return this.page;
    return null;
  }
}

export function activate(context: ActivationContext) {
  return new MyProvider();
}
```

## Package.json Configuration

Add a `cmdpal` field to your `package.json`:

```json
{
  "name": "@yourorg/my-extension",
  "version": "1.0.0",
  "cmdpal": {
    "main": "dist/index.js",
    "displayName": "My Extension",
    "minVersion": "0.100.0"
  },
  "main": "dist/index.js"
}
```

## API Overview

### Base Classes

| Class | Description |
|-------|-------------|
| `CommandProviderBase` | Main extension entry point. Provides commands to the palette. |
| `ListPageBase` | A page displaying a searchable list of items. |
| `DynamicListPageBase` | A list page where the extension handles search filtering. |
| `ContentPageBase` | A page displaying rich content (markdown, forms, images). |
| `InvokableCommandBase` | A command that performs an action when invoked. |
| `ListItemBase` | A single item in a list page. |
| `CommandItemBase` | A command item displayed in menus. |

### Runtime

| Export | Description |
|--------|-------------|
| `ExtensionHost` | Static class for logging and status messages. |
| `activate()` | Helper for extension activation. |

### Types

The SDK exports all interfaces matching the Command Palette extension contract:
`ICommand`, `IListPage`, `IDynamicListPage`, `IContentPage`, `ICommandProvider`, etc.

## Extension Lifecycle

1. CmdPal discovers your package in the extensions directory
2. The Node host calls your `activate()` export
3. Your provider's `topLevelCommands()` is called to populate the palette
4. When a user interacts with your commands, corresponding methods are called via JSONRPC

## Logging

```typescript
import { ExtensionHost } from '@microsoft/cmdpal-sdk';

ExtensionHost.log('Something happened');
ExtensionHost.log('Something went wrong', 'error');
ExtensionHost.showStatus('Loading...', 'info', { isIndeterminate: true });
```
