// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  CommandProviderBase,
  DynamicListPageBase,
  ContentPageBase,
  InvokableCommandBase,
  ListItemBase,
  ExtensionHost,
  startJsonRpcServer,
  type ICommandItem,
  type IListItem,
  type ICommand,
  type CommandResult,
  type MarkdownContent,
} from '@microsoft/cmdpal-sdk';

// === Commands ===

class SayHelloCommand extends InvokableCommandBase {
  id = 'say-hello';
  name = 'Say Hello';

  invoke(): CommandResult {
    ExtensionHost.log('Hello from the sample JS extension!');
    return { kind: 'showToast', args: { message: 'Hello from JavaScript! 🎉' } };
  }
}

class OpenMarkdownPageCommand extends InvokableCommandBase {
  id = 'open-markdown';
  name = 'View Readme';

  invoke(): CommandResult {
    return { kind: 'goToPage', args: { pageId: 'readme-page', navigationMode: 'push' } };
  }
}

// === Pages ===

class SampleListPage extends DynamicListPageBase {
  id = 'sample-list';
  name = 'Sample List';
  title = 'Sample JS Extension';
  placeholderText = 'Search sample items...';
  showDetails = true;

  private query = '';
  private readonly allItems: IListItem[] = [
    new ListItemBase({
      command: new SayHelloCommand(),
      title: 'Say Hello',
      subtitle: 'Displays a toast message',
      tags: [{ text: 'Action' }],
      section: 'Commands',
    }),
    new ListItemBase({
      command: new OpenMarkdownPageCommand(),
      title: 'View Readme',
      subtitle: 'Shows a markdown content page',
      tags: [{ text: 'Page' }],
      section: 'Pages',
    }),
    new ListItemBase({
      command: { id: 'item-3', name: 'Static Item' },
      title: 'Static Item',
      subtitle: 'This item does not have an action',
      section: 'Other',
    }),
  ];

  setSearchText(text: string): void {
    this.query = text.toLowerCase();
  }

  getItems(): IListItem[] {
    if (!this.query) {
      return this.allItems;
    }

    return this.allItems.filter(
      (item) =>
        item.title.toLowerCase().includes(this.query) ||
        (item.subtitle?.toLowerCase().includes(this.query) ?? false)
    );
  }
}

class ReadmePage extends ContentPageBase {
  id = 'readme-page';
  name = 'Readme';
  title = 'About Sample JS Extension';

  getContent(): MarkdownContent[] {
    return [
      {
        type: 'markdown',
        body: `# Sample JS Extension

This is a sample **JavaScript/TypeScript extension** for Command Palette,
built using the \`@microsoft/cmdpal-sdk\`.

## Features

- 🔍 Dynamic list page with search filtering
- 📝 Markdown content page
- ⚡ Invokable commands with toast notifications
- 📊 Multiple page types demonstrating SDK capabilities

## How It Works

This extension communicates with Command Palette via **JSONRPC over stdio**.
The SDK handles all the protocol details — you just write TypeScript classes.

## Getting Started

\`\`\`typescript
import { CommandProviderBase } from '@microsoft/cmdpal-sdk';

class MyProvider extends CommandProviderBase {
  id = 'my-extension';
  displayName = 'My Extension';
  topLevelCommands() { return [...]; }
}

export function activate(context) {
  return new MyProvider();
}
\`\`\`
`,
      },
    ];
  }
}

// === Provider ===

class SampleJSProvider extends CommandProviderBase {
  id = 'sample-js-extension';
  displayName = 'Sample JS Extension';
  icon = { light: { icon: '\uE943' }, dark: { icon: '\uE943' } };

  private listPage = new SampleListPage();
  private readmePage = new ReadmePage();

  topLevelCommands(): ICommandItem[] { 
    return [
      {
        command: this.listPage,
        title: 'Sample JS Extension',
        subtitle: 'A sample TypeScript/JavaScript extension',
        icon: this.icon,
      },
    ];
  }

  getCommand(id: string): ICommand | null {
    switch (id) {
      case this.listPage.id:
        return this.listPage as unknown as ICommand;
      case this.readmePage.id:
        return this.readmePage as unknown as ICommand;
      case 'say-hello':
        return new SayHelloCommand();
      case 'open-markdown':
        return new OpenMarkdownPageCommand();
      default:
        return null;
    }
  }
}

// === Start JSONRPC Server ===

startJsonRpcServer(() => new SampleJSProvider());
