"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
const cmdpal_sdk_1 = require("@microsoft/cmdpal-sdk");
// === Commands ===
class SayHelloCommand extends cmdpal_sdk_1.InvokableCommandBase {
    id = 'say-hello';
    name = 'Say Hello';
    invoke() {
        cmdpal_sdk_1.ExtensionHost.log('Hello from the sample JS extension!');
        return { kind: 'showToast', args: { message: 'Hello from JavaScript! 🎉' } };
    }
}
class OpenMarkdownPageCommand extends cmdpal_sdk_1.InvokableCommandBase {
    id = 'open-markdown';
    name = 'View Readme';
    invoke() {
        return { kind: 'goToPage', args: { pageId: 'readme-page', navigationMode: 'push' } };
    }
}
// === Pages ===
class SampleListPage extends cmdpal_sdk_1.DynamicListPageBase {
    id = 'sample-list';
    name = 'Sample List';
    title = 'Sample JS Extension';
    placeholderText = 'Search sample items...';
    showDetails = true;
    query = '';
    allItems = [
        new cmdpal_sdk_1.ListItemBase({
            command: new SayHelloCommand(),
            title: 'Say Hello',
            subtitle: 'Displays a toast message',
            tags: [{ text: 'Action' }],
            section: 'Commands',
        }),
        new cmdpal_sdk_1.ListItemBase({
            command: new OpenMarkdownPageCommand(),
            title: 'View Readme',
            subtitle: 'Shows a markdown content page',
            tags: [{ text: 'Page' }],
            section: 'Pages',
        }),
        new cmdpal_sdk_1.ListItemBase({
            command: { id: 'item-3', name: 'Static Item' },
            title: 'Static Item',
            subtitle: 'This item does not have an action',
            section: 'Other',
        }),
    ];
    setSearchText(text) {
        this.query = text.toLowerCase();
    }
    getItems() {
        if (!this.query) {
            return this.allItems;
        }
        return this.allItems.filter((item) => item.title.toLowerCase().includes(this.query) ||
            (item.subtitle?.toLowerCase().includes(this.query) ?? false));
    }
}
class ReadmePage extends cmdpal_sdk_1.ContentPageBase {
    id = 'readme-page';
    name = 'Readme';
    title = 'About Sample JS Extension';
    getContent() {
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
class SampleJSProvider extends cmdpal_sdk_1.CommandProviderBase {
    id = 'sample-js-extension';
    displayName = 'Sample JS Extension';
    icon = { light: { icon: '\uE943' }, dark: { icon: '\uE943' } };
    listPage = new SampleListPage();
    readmePage = new ReadmePage();
    topLevelCommands() {
        return [
            {
                command: this.listPage,
                title: 'Sample JS Extension',
                subtitle: 'A sample TypeScript/JavaScript extension',
                icon: this.icon,
            },
        ];
    }
    getCommand(id) {
        switch (id) {
            case this.listPage.id:
                return this.listPage;
            case this.readmePage.id:
                return this.readmePage;
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
(0, cmdpal_sdk_1.startJsonRpcServer)(() => new SampleJSProvider());
