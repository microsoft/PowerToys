# Building Command Palette Extensions with JavaScript/TypeScript

This guide explains how to create, develop, and publish JavaScript/TypeScript extensions for Command Palette using the JSONRPC extension system.

## Prerequisites

- Node.js 22+ (for development; end-users get it automatically)
- npm (comes with Node.js)
- TypeScript 5.8+ (recommended)

## Quick Start

### 1. Create a new extension project

```bash
mkdir my-cmdpal-extension
cd my-cmdpal-extension
npm init -y
npm install @microsoft/cmdpal-sdk
npm install -D typescript @types/node
```

### 2. Configure TypeScript

Create `tsconfig.json`:
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "Node16",
    "moduleResolution": "Node16",
    "outDir": "./dist",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true,
    "declaration": true
  },
  "include": ["src/**/*.ts"]
}
```

### 3. Configure package.json

Add the `cmdpal` field to your `package.json`:

```json
{
  "name": "@yourname/my-extension",
  "version": "1.0.0",
  "main": "dist/index.js",
  "cmdpal": {
    "main": "dist/index.js",
    "displayName": "My Extension",
    "minVersion": "0.100.0"
  },
  "scripts": {
    "build": "tsc",
    "dev": "tsc --watch"
  }
}
```

The `cmdpal.main` field specifies the entry point for your extension. If omitted, the standard `main` field is used as a fallback.

### 4. Write your extension

Create `src/index.ts`:

```typescript
import {
  CommandProviderBase,
  ListPageBase,
  ListItemBase,
  InvokableCommandBase,
  ExtensionHost,
  type ActivationContext,
  type ICommandItem,
  type IListItem,
  type CommandResult,
} from '@microsoft/cmdpal-sdk';

// A command that does something when invoked
class GreetCommand extends InvokableCommandBase {
  id = 'greet';
  name = 'Greet';

  invoke(): CommandResult {
    ExtensionHost.log('User invoked greet!');
    return { kind: 'showToast', args: { message: 'Hello from my extension! 👋' } };
  }
}

// A list page showing items
class MyListPage extends ListPageBase {
  id = 'my-list';
  name = 'My Items';
  title = 'My Extension';
  placeholderText = 'Search items...';

  getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new GreetCommand(),
        title: 'Say Hello',
        subtitle: 'Shows a greeting toast',
        tags: [{ text: 'Action' }],
      }),
    ];
  }
}

// The main provider — this is what CmdPal loads
class MyProvider extends CommandProviderBase {
  id = 'my-extension';
  displayName = 'My Extension';

  private page = new MyListPage();

  topLevelCommands(): ICommandItem[] {
    return [{
      command: this.page,
      title: this.page.title,
      subtitle: 'My awesome extension',
    }];
  }

  getCommand(id: string) {
    if (id === this.page.id) return this.page as any;
    if (id === 'greet') return new GreetCommand();
    return null;
  }
}

// Entry point — CmdPal calls this to activate your extension
export function activate(context: ActivationContext) {
  ExtensionHost.log(`Extension activated in ${context.extensionDirectory}`);
  return new MyProvider();
}
```

### 5. Build and test

```bash
npm run build
```

## Extension Architecture

### How it works

```
┌─────────────────────┐      JSONRPC/stdio       ┌──────────────────┐
│   Command Palette   │ ◄───────────────────────► │   Node.js Host   │
│   (C# / WinUI)     │                           │                  │
│                     │                           │  ┌────────────┐  │
│  JSONRPCExtension-  │  provider/getCommands ──► │  │ Extension1 │  │
│  Service            │  ◄── { items: [...] }     │  └────────────┘  │
│                     │                           │  ┌────────────┐  │
│                     │  command/invoke ────────► │  │ Extension2 │  │
│                     │  ◄── { kind: "toast" }    │  └────────────┘  │
└─────────────────────┘                           └──────────────────┘
```

1. CmdPal spawns a single Node.js host process at startup
2. The host loads all installed JS extensions
3. CmdPal communicates with extensions via JSONRPC over stdio
4. Your extension responds to requests and can send notifications back

### Entry Point

Your extension must export an `activate` function:

```typescript
export function activate(context: ActivationContext): CommandProvider {
  return new MyProvider();
}
```

The `context` provides:
- `extensionId` — Your extension's unique identifier
- `extensionDirectory` — Absolute path to your extension's install directory

### CommandProvider

The `CommandProvider` is the root of your extension. It must implement:

| Method | Description |
|--------|-------------|
| `topLevelCommands()` | Returns items shown in the main palette |
| `fallbackCommands()` | (Optional) Returns fallback search handlers |
| `getCommand(id)` | (Optional) Returns a command/page by ID |
| `initializeWithHost(host)` | (Optional) Called with the extension host API |
| `dispose()` | (Optional) Cleanup when extension is unloaded |

## Page Types

### ListPage

Displays a searchable list of items:

```typescript
class MyList extends ListPageBase {
  id = 'my-list';
  name = 'My List';
  title = 'Items';
  placeholderText = 'Search...';
  showDetails = true;

  getItems(): IListItem[] {
    return [/* items */];
  }
}
```

### DynamicListPage

A list where your extension handles filtering:

```typescript
class SearchPage extends DynamicListPageBase {
  id = 'search';
  name = 'Search';
  title = 'Search';
  private query = '';

  setSearchText(text: string) {
    this.query = text;
    // Items will be re-fetched automatically
  }

  async getItems() {
    return await myApi.search(this.query);
  }
}
```

### ContentPage

Displays rich content (markdown, forms, images):

```typescript
class AboutPage extends ContentPageBase {
  id = 'about';
  name = 'About';
  title = 'About My Extension';

  getContent(): Content[] {
    return [
      { type: 'markdown', body: '# Hello\n\nThis is **markdown** content.' },
      { type: 'image', image: { light: { icon: 'https://...' } }, maxWidth: 300 },
    ];
  }
}
```

## Commands

### InvokableCommand

A command that performs an action:

```typescript
class CopyCommand extends InvokableCommandBase {
  id = 'copy';
  name = 'Copy to Clipboard';

  async invoke(): Promise<CommandResult> {
    // Do your action here
    return { kind: 'showToast', args: { message: 'Copied!' } };
  }
}
```

### CommandResult

After invoking, return a result to control navigation:

| Kind | Description |
|------|-------------|
| `dismiss` | Close the palette |
| `goHome` | Return to the main page, keep palette open |
| `goBack` | Go back one page |
| `hide` | Hide palette but keep current page |
| `keepOpen` | Do nothing (stay on current page) |
| `goToPage` | Navigate to another page (`args: { pageId, navigationMode }`) |
| `showToast` | Show a toast message (`args: { message }`) |
| `confirm` | Show a confirmation dialog |

## Logging & Status

Use `ExtensionHost` to communicate with CmdPal:

```typescript
import { ExtensionHost } from '@microsoft/cmdpal-sdk';

// Logging (visible in CmdPal logs)
ExtensionHost.log('Something happened');
ExtensionHost.log('Something failed', 'error');

// Status bar messages
ExtensionHost.showStatus('Loading data...', 'info', { isIndeterminate: true });
ExtensionHost.hideStatus('loading-id');
```

## Publishing to the Gallery

### 1. Publish to npm

```bash
npm publish --access public
```

### 2. Submit to the gallery

Create a PR to [microsoft/CmdPal-Extensions](https://github.com/microsoft/CmdPal-Extensions) adding your extension to the gallery manifest:

```json
{
  "id": "yourname.my-extension",
  "title": "My Extension",
  "shortDescription": "A brief description",
  "description": "Full description of what your extension does...",
  "author": { "name": "Your Name", "url": "https://github.com/yourname" },
  "installSources": [
    { "type": "npm", "id": "@yourname/my-extension" }
  ],
  "iconUrl": "https://raw.githubusercontent.com/.../icon.png",
  "tags": ["productivity"],
  "categories": ["utilities-and-tools"],
  "addedAt": "2026-01-01"
}
```

### Install source types

| Type | Description | Required fields |
|------|-------------|-----------------|
| `npm` | npm package | `id` (package name) |
| `winget` | WinGet package | `id` (winget ID) |
| `msstore` | Microsoft Store | `id` (store product ID) |

## Extension Settings

Extensions can provide a settings page by implementing `settings` on the provider:

```typescript
class SettingsPage extends ContentPageBase {
  id = 'settings';
  name = 'Settings';
  title = 'My Extension Settings';

  getContent(): Content[] {
    return [{
      type: 'form',
      templateJson: JSON.stringify({
        type: 'AdaptiveCard',
        body: [
          { type: 'Input.Text', id: 'apiKey', label: 'API Key' }
        ],
        actions: [{ type: 'Action.Submit', title: 'Save' }]
      }),
      dataJson: JSON.stringify({ apiKey: '' }),
      submitForm(inputs: string, data: string) {
        // Save settings
        return { kind: 'showToast', args: { message: 'Settings saved!' } };
      }
    }];
  }
}

class MyProvider extends CommandProviderBase {
  // ...
  settings = { settingsPage: new SettingsPage() };
}
```

## Notifications (Push Updates)

Extensions can notify CmdPal when data changes, triggering a re-fetch:

```typescript
// In DynamicListPageBase subclasses:
this.notifyItemsChanged(); // triggers page/getItems re-call

// Or use the connection directly (advanced):
// The SDK wires this up through the JSONRPC bridge
```

## File Structure

A typical extension project:

```
my-extension/
├── package.json          # npm config + cmdpal field
├── tsconfig.json         # TypeScript config
├── src/
│   └── index.ts          # Extension entry point
├── dist/                 # Compiled output (git-ignored)
│   └── index.js
└── README.md
```

## Troubleshooting

### Extension not loading?
1. Check that `cmdpal.main` (or `main`) in package.json points to a valid JS file
2. Verify your `activate()` function returns a valid `CommandProvider`
3. Check CmdPal logs for error messages from your extension

### Commands not appearing?
1. Ensure `topLevelCommands()` returns at least one item
2. Verify the extension is enabled in CmdPal settings
3. Check that the `getCommand(id)` method returns pages/commands referenced by your items

### Logs
Extension logs are written to:
`%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\logs\`

## API Reference

See the [TypeScript SDK README](../ts-sdk/README.md) for the complete API surface.

See the [JSONRPC Protocol Specification](../docs/jsonrpc-protocol.md) for the wire protocol details.
