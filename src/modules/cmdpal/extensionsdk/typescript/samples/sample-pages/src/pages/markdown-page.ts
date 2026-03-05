// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ContentPage,
  MarkdownContent,
  IContent,
} from '@cmdpal/sdk';

/**
 * Demonstrates a content page with:
 * - Multiple markdown sections
 * - Rich formatting (headings, lists, code blocks, tables, links)
 */
export class MarkdownDemoPage extends ContentPage {
  id = 'markdown-demo';
  name = 'Markdown Showcase';

  commands = [];

  getContent(): IContent[] {
    return [
      new MarkdownContent(
        '# 📄 Markdown Content Page\n\n' +
        'This page demonstrates how TypeScript extensions can render **rich content** ' +
        'using Markdown. Each section below shows a different Markdown feature.\n\n' +
        '---',
      ),
      new MarkdownContent(
        '## Text Formatting\n\n' +
        'You can use **bold**, *italic*, ~~strikethrough~~, and `inline code`.\n\n' +
        '> 💡 **Tip:** Block quotes are great for highlighting important information.\n\n' +
        '> **Note:** Nested quotes work too!\n' +
        '> > This is a nested block quote.',
      ),
      new MarkdownContent(
        '## Lists\n\n' +
        '### Unordered\n' +
        '- First item\n' +
        '- Second item\n' +
        '  - Nested item A\n' +
        '  - Nested item B\n' +
        '- Third item\n\n' +
        '### Ordered\n' +
        '1. Step one\n' +
        '2. Step two\n' +
        '3. Step three\n\n' +
        '### Task List\n' +
        '- [x] Create TypeScript SDK\n' +
        '- [x] Build sample extension\n' +
        '- [ ] World domination',
      ),
      new MarkdownContent(
        '## Code Blocks\n\n' +
        '```typescript\n' +
        'import { ExtensionServer, CommandProvider } from "@cmdpal/sdk";\n\n' +
        'class MyProvider extends CommandProvider {\n' +
        '  get id() { return "my-extension"; }\n' +
        '  get displayName() { return "My Extension"; }\n' +
        '}\n\n' +
        'ExtensionServer.register(new MyProvider());\n' +
        'ExtensionServer.start();\n' +
        '```',
      ),
      new MarkdownContent(
        '## Tables\n\n' +
        '| Feature | TypeScript SDK | WinRT SDK |\n' +
        '|---------|:-------------:|:---------:|\n' +
        '| List Pages | ✅ | ✅ |\n' +
        '| Dynamic Lists | ✅ | ✅ |\n' +
        '| Content Pages | ✅ | ✅ |\n' +
        '| Forms | ✅ | ✅ |\n' +
        '| Toasts | ✅ | ✅ |\n' +
        '| Hot Reload | ✅ | ❌ |',
      ),
      new MarkdownContent(
        '## Links\n\n' +
        '- [PowerToys on GitHub](https://github.com/microsoft/PowerToys)\n' +
        '- [Command Palette Docs](https://learn.microsoft.com/windows/powertoys/cmd-palette)\n' +
        '- [TypeScript](https://www.typescriptlang.org/)',
      ),
    ];
  }
}
