# 05 - Getting Started: Build Your First CmdPal Extension

This guide walks you through building a CmdPal JavaScript extension from scratch. By the end you will have a working extension with a searchable list page, a content page, and a settings page. It uses the same SDK idioms as the shipped parity sample under `src/modules/cmdpal/ext/SampleJSExtension`, so you can read that project alongside this guide.

## Prerequisites

- Node.js 22 or newer, installed and on your PATH.
- PowerToys with Command Palette enabled.
- A text editor. VS Code is recommended.
- A local copy of the `@microsoft/cmdpal-sdk` package. The SDK is not published to npm yet, so you reference it from the repo at `src/modules/cmdpal/ts-sdk` through a relative `file:` dependency. Build the SDK once (`npm ci && npm run build` inside `ts-sdk`) so that its `dist` folder exists.

## Step 1: Scaffold the project

```bash
mkdir my-first-extension && cd my-first-extension
npm init -y
npm install --save-dev typescript @types/node
```

Add the SDK as a relative dependency. Adjust the path so it points at your checkout of `src/modules/cmdpal/ts-sdk`:

```bash
npm install "@microsoft/cmdpal-sdk@file:../path/to/src/modules/cmdpal/ts-sdk"
```

Create `tsconfig.json`. The SDK is an ES module, so the project uses `NodeNext` module resolution and emits ES modules:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2023"],
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "types": ["node"],

    "strict": true,
    "noImplicitOverride": true,
    "noUncheckedIndexedAccess": true,

    "esModuleInterop": true,
    "verbatimModuleSyntax": true,
    "skipLibCheck": true,

    "outDir": "dist",
    "rootDir": "src"
  },
  "include": ["src"]
}
```

Update `package.json` so it is an ES module, points `main` at the built entry, and carries a `cmdpal` section:

```json
{
  "name": "my-first-extension",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "main": "dist/index.js",
  "cmdpal": {
    "displayName": "My First Extension",
    "icon": "\uE8A5",
    "publisher": "Your Name",
    "debug": true
  },
  "scripts": {
    "build": "tsc -p tsconfig.json"
  },
  "dependencies": {
    "@microsoft/cmdpal-sdk": "file:../path/to/src/modules/cmdpal/ts-sdk"
  },
  "devDependencies": {
    "@types/node": "^22.7.0",
    "typescript": "^5.8.0"
  },
  "engines": {
    "node": ">=22.0.0"
  }
}
```

Because the project uses `NodeNext` resolution, every relative import in your own source must include the `.js` extension (for example `import { MainPage } from './mainPage.js'`). Imports from the `@microsoft/cmdpal-sdk` package use a bare specifier and do not need an extension.

## Step 2: Create a simple command and a list page

Create `src/index.ts`. Commands extend `InvokableCommandBase` and return a `CommandResult`, which is a plain object with a `kind` string and optional `args`. Pages extend `ListPageBase`. The provider extends `CommandProviderBase` and is started with `run`, which takes a factory function:

```typescript
import {
  CommandItemBase,
  CommandProviderBase,
  InvokableCommandBase,
  ListItemBase,
  ListPageBase,
  iconFromGlyph,
  run,
} from '@microsoft/cmdpal-sdk';
import type {
  CommandResult,
  ICommandItem,
  IListItem,
} from '@microsoft/cmdpal-sdk';

// A command that shows a toast. `invoke` returns a CommandResult object.
class GreetCommand extends InvokableCommandBase {
  readonly id = 'greet';
  readonly name = 'Say Hello';

  override invoke(): CommandResult {
    return { kind: 'showToast', args: { message: 'Hello from my extension!' } };
  }
}

// The main list page. `getItems` may be synchronous or return a Promise.
class MainPage extends ListPageBase {
  readonly id = 'main-page';
  readonly name = 'My First Extension';
  readonly title = 'My First Extension';

  override icon = iconFromGlyph('\uE8A5');

  override getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new GreetCommand(),
        title: 'Say Hello',
        subtitle: 'Shows a greeting toast',
        icon: iconFromGlyph('\uE76E'),
      }),
    ];
  }
}

// The provider exposes one top-level command that opens the main page.
class MyProvider extends CommandProviderBase {
  readonly id = 'my-first-extension';
  readonly displayName = 'My First Extension';

  override icon = iconFromGlyph('\uE8A5');

  private readonly mainPage = new MainPage();

  override topLevelCommands(): ICommandItem[] {
    return [
      new CommandItemBase({
        command: this.mainPage,
        title: 'My First Extension',
        subtitle: 'A tutorial extension',
        icon: iconFromGlyph('\uE8A5'),
      }),
    ];
  }
}

run(() => new MyProvider());
```

A few things worth noting:

- `id`, `name`, `title`, and `displayName` are declared as plain class fields that satisfy the abstract members on the base classes. You do not need `override` when you are implementing an abstract member, but you do need `override` on members that already have an implementation on the base class, such as `icon`, `invoke`, and `getItems`.
- `CommandResult.kind` is one of `dismiss`, `goHome`, `goBack`, `hide`, `keepOpen`, `goToPage`, `showToast`, or `confirm`. There is no enum to import. You write the string directly.
- `run` accepts a factory (`() => new MyProvider()`) rather than an instance, so the host can construct the provider at the right point in startup.

## Step 3: Build and install for local testing

```bash
npm run build
```

Then link the built extension into the CmdPal extensions directory. A directory junction lets CmdPal load the folder while you keep editing in place:

```powershell
$extensionsDir = "$env:LOCALAPPDATA\Microsoft\PowerToys\CmdPal\JSExtensions"
New-Item -ItemType Directory -Force -Path $extensionsDir | Out-Null
New-Item -ItemType Junction -Path "$extensionsDir\my-first-extension" -Target (Resolve-Path .)
```

Open CmdPal. You should see "My First Extension" in the list. Select it to open your page, then run "Say Hello" to see the toast. For discovery to succeed the folder must contain a `package.json` with a `cmdpal` section, a non-empty `name`, and a `main` (or `cmdpal.main`) that resolves to a file that exists, which is why you build before installing.

## Step 4: Add a searchable (dynamic) list page

A dynamic list page filters its items as the user types. Extend `DynamicListPageBase`, read `this.searchText`, and rebuild the items in `getItems`:

```typescript
import { DynamicListPageBase, ListItemBase } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';

const fruits = [
  { title: 'Apple', emoji: '\u{1F34E}' },
  { title: 'Banana', emoji: '\u{1F34C}' },
  { title: 'Cherry', emoji: '\u{1F352}' },
  { title: 'Dragon Fruit', emoji: '\u{1F409}' },
  { title: 'Elderberry', emoji: '\u{1FAD0}' },
];

class SearchablePage extends DynamicListPageBase {
  readonly id = 'searchable-page';
  readonly name = 'Fruit Search';
  readonly title = 'Fruit Search';

  override placeholderText = 'Search fruits...';

  override setSearchText(text: string): void {
    this.searchText = text;
    // Tell the host the items changed so it re-queries getItems.
    this.notifyItemsChanged();
  }

  override getItems(): IListItem[] {
    const query = (this.searchText ?? '').toLowerCase();
    return fruits
      .filter((item) => item.title.toLowerCase().includes(query))
      .map(
        (item) =>
          new ListItemBase({
            command: new GreetCommand(),
            title: `${item.emoji} ${item.title}`,
            subtitle: 'A delicious fruit',
          }),
      );
  }
}
```

Add the searchable page to the main page by returning another `ListItemBase` whose `command` is `new SearchablePage()`.

## Step 5: Add a content page with markdown

Content pages render blocks of rich content. Extend `ContentPageBase` and return an array of `Content` objects from `getContent`. Each markdown block is a plain object with `type: 'markdown'` and a `body` string:

```typescript
import { ContentPageBase } from '@microsoft/cmdpal-sdk';
import type { Content } from '@microsoft/cmdpal-sdk';

class AboutPage extends ContentPageBase {
  readonly id = 'about-page';
  readonly name = 'About';
  readonly title = 'About';

  override getContent(): Content[] {
    return [
      {
        type: 'markdown',
        body: [
          '# My First Extension',
          '',
          'This extension was built with the CmdPal TypeScript SDK.',
          '',
          '## Features',
          '- Simple commands with toast notifications',
          '- Searchable lists with dynamic filtering',
          '- Rich content with markdown',
        ].join('\n'),
      },
    ];
  }
}
```

The `Content` union also covers forms (`type: 'form'`), images (`type: 'image'`), plain text (`type: 'plainText'`), and a tree (`type: 'tree'`). See the TypeScript SDK reference for the full shape of each.

## Step 6: Add a settings page

Settings are built with the `Settings` helper plus the individual setting types. A settings page is a content page that renders the settings as a form:

```typescript
import {
  ChoiceSetSetting,
  ContentPageBase,
  ExtensionHost,
  Settings,
  TextSetting,
  ToggleSetting,
} from '@microsoft/cmdpal-sdk';
import type { Content, SettingChoice } from '@microsoft/cmdpal-sdk';

const choices: SettingChoice[] = [
  { title: 'Small', value: 'small' },
  { title: 'Medium', value: 'medium' },
  { title: 'Large', value: 'large' },
];

class SettingsPage extends ContentPageBase {
  readonly id = 'settings';
  readonly name = 'Settings';
  readonly title = 'Settings';

  private readonly settings = new Settings();

  constructor() {
    super();
    this.settings.add(
      new ToggleSetting('showEmoji', 'Show emoji in results', true, 'Adds an emoji to each item'),
    );
    this.settings.add(
      new TextSetting('greeting', 'Custom greeting', 'Hello', 'Used by the Say Hello command'),
    );
    this.settings.add(
      new ChoiceSetSetting('size', 'Result size', choices, 'medium', 'Preferred item size'),
    );
  }

  override getContent(): Content[] | Promise<Content[]> {
    const greeting = this.settings.getSetting<TextSetting>('greeting')?.value;
    ExtensionHost.log(`Current greeting is ${greeting}`);
    return this.settings.settingsPage.getContent();
  }
}
```

Expose the settings page like any other page, by returning a `ListItemBase` whose `command` is `new SettingsPage()`.

## Step 7: Add context commands, tags, and details

List items can carry a details pane, tags, and a flattened context menu. The built-in commands `OpenUrlCommand` and `CopyTextCommand` cover common actions. `OpenUrlCommand` takes the URL first, then an optional name. `CopyTextCommand` takes the text, an optional name, and an optional toast message:

```typescript
import {
  CopyTextCommand,
  ListItemBase,
  OpenUrlCommand,
} from '@microsoft/cmdpal-sdk';
import type { ContextItem, IListItem, Tag } from '@microsoft/cmdpal-sdk';

function webTag(text: string): Tag {
  return { text, foreground: { hasValue: true, color: { r: 100, g: 200, b: 255, a: 255 } } };
}

// Inside getItems():
const copyUrl = new CopyTextCommand('https://github.com', 'Copy URL');

const moreCommands: ContextItem[] = [
  { command: copyUrl, title: 'Copy URL', icon: iconFromGlyph('\uE8C8') },
];

const item: IListItem = new ListItemBase({
  command: new OpenUrlCommand('https://github.com', 'Open GitHub'),
  title: 'GitHub',
  subtitle: 'Open GitHub in your browser',
  icon: iconFromGlyph('\uE774'),
  tags: [webTag('Web')],
  details: {
    title: 'GitHub',
    body: 'The world\'s leading software development platform.',
    metadata: [
      { key: 'URL', data: { type: 'link', link: 'https://github.com', text: 'github.com' } },
      { key: 'Topics', data: { type: 'tags', tags: [{ text: 'Development' }, { text: 'Git' }] } },
    ],
  },
  moreCommands,
});
```

A note on context menus: the JS protocol flattens context menus to a single level. `ContextItem` has no `moreCommands` of its own, so a deeply nested C# menu becomes one flat list in a JS extension.

## Step 8: Rebuild and test

```bash
npm run build
```

CmdPal watches each extension directory for `*.js` changes and hot-reloads within about 500 milliseconds, so after `tsc` finishes your extension reloads automatically. Changes under `node_modules` are ignored by the watcher.

## Debugging tips

### Enable debug mode

Set `"debug": true` in the `cmdpal` section of your `package.json`. The Node.js process then starts with `--inspect`, which lets you attach a debugger. You can pin the port with `"debugPort"`; otherwise ports are auto-assigned starting at 9229.

### Attach the VS Code debugger

Add an attach configuration to `.vscode/launch.json`:

```json
{
  "type": "node",
  "request": "attach",
  "name": "Attach to CmdPal Extension",
  "port": 9229,
  "restart": true,
  "skipFiles": ["<node_internals>/**"]
}
```

### View logs and status

Use `ExtensionHost` to send messages to the CmdPal log and to show inline status:

```typescript
import { ExtensionHost } from '@microsoft/cmdpal-sdk';

ExtensionHost.log('Fetching items...');
// showStatus returns a stable id; keep it to hide the same status later.
const statusId = ExtensionHost.showStatus('Working...', 'info', { isIndeterminate: true });
ExtensionHost.hideStatus(statusId);
```

### Common issues

| Issue | Solution |
|-------|----------|
| Extension not showing | Check that `package.json` has a `cmdpal` section, a non-empty `name`, and a `main` that resolves to a built file. Build before installing. |
| Blank page | Check that `getItems` or `getContent` returns data. |
| Command does nothing | Ensure `invoke` returns a valid `CommandResult`, for example `{ kind: 'showToast', args: { message: '...' } }`. |
| Images not loading | Use `iconFromUrl` or `iconFromBase64`. Only absolute file paths are supported for `file://` sources. |
| Import fails at runtime | Relative imports in your own code must end with `.js`, because the project uses `NodeNext` module resolution. |

## Next steps

- Read the [TypeScript SDK Reference](./02-typescript-sdk.md) for the full API.
- Read the [JSON-RPC Protocol Specification](./03-jsonrpc-protocol.md) to understand the wire format.
- Read the [Manifest and Packaging](./04-manifest-packaging.md) guide for the manifest fields and install layout.
- Explore the [parity sample](../../ext/SampleJSExtension/) for a comprehensive, buildable example that mirrors the C# `SamplePagesExtension`.
- Read the [Architecture Overview](./01-architecture.md) for how it all fits together.
