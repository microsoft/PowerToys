// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ContentPage,
  MarkdownContent,
  IContent,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Multi-markdown content page
// ---------------------------------------------------------------------------

/**
 * Demonstrates a content page with multiple MarkdownContent blocks.
 * Similar to a blog or forum thread with multiple posts/comments.
 */
export class MultiMarkdownPage extends ContentPage {
  id = 'multi-markdown';
  name = 'Multiple Markdown Bodies';

  getContent(): IContent[] {
    const intro = new MarkdownContent(
      '# Welcome to the TypeScript SDK\n\n' +
      'This page demonstrates **multiple markdown bodies** rendered on a single content page. ' +
      'Each section is an independent `MarkdownContent` block that gets rendered sequentially.\n\n' +
      '---',
    );
    intro.id = 'intro';

    const features = new MarkdownContent(
      '## ✨ Key Features\n\n' +
      '| Feature | Description |\n' +
      '|---------|-------------|\n' +
      '| **ListPage** | Standard list with items, tags, and details |\n' +
      '| **DynamicListPage** | Live search and filter support |\n' +
      '| **ContentPage** | Rich markdown and form content |\n' +
      '| **TreeContent** | Nested hierarchical content |\n' +
      '| **Adaptive Cards** | Form inputs with validation |\n\n' +
      'Each feature is demonstrated in a separate sample page accessible from the hub.\n\n' +
      '---',
    );
    features.id = 'features';

    const codeExample = new MarkdownContent(
      '## 💻 Quick Start\n\n' +
      'Creating a basic extension:\n\n' +
      '```typescript\n' +
      'import { CommandProvider, ListPage, ListItem, ExtensionServer } from \'@cmdpal/sdk\';\n\n' +
      'class MyPage extends ListPage {\n' +
      '  id = \'my-page\';\n' +
      '  name = \'My Extension\';\n\n' +
      '  getItems() {\n' +
      '    return [\n' +
      '      new ListItem({\n' +
      '        title: \'Hello World\',\n' +
      '        subtitle: \'My first item\',\n' +
      '        command: new MyCommand(),\n' +
      '      }),\n' +
      '    ];\n' +
      '  }\n' +
      '}\n' +
      '```\n\n' +
      '---',
    );
    codeExample.id = 'code-example';

    const closing = new MarkdownContent(
      '## 📝 Notes\n\n' +
      '> Each `MarkdownContent` block is rendered as a separate section on the page. ' +
      'This pattern works well for:\n' +
      '> - Blog-style posts with comments\n' +
      '> - Documentation with code examples\n' +
      '> - Multi-section reports\n\n' +
      '*This concludes the multi-markdown demo.*',
    );
    closing.id = 'closing';

    return [intro, features, codeExample, closing];
  }
}
