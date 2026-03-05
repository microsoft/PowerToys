// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ContentPage,
  MarkdownContent,
  IContent,
  IDetails,
  IconInfo,
  tag,
  color,
  details,
  metadataTags,
  metadataLink,
  metadataSeparator,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Markdown page with details panel
// ---------------------------------------------------------------------------

/**
 * Demonstrates a ContentPage with:
 * - Markdown body content
 * - A details panel with metadata (links, separators, colored tags)
 *
 * Similar to WinRT SampleMarkdownDetails.
 */
export class MarkdownDetailsPage extends ContentPage {
  id = 'markdown-details';
  name = 'Markdown + Details';

  details: IDetails = details({
    title: 'Page Metadata',
    body: 'This details panel accompanies the markdown content on the left.',
    heroImage: IconInfo.fromGlyph('📄'),
    metadata: [
      metadataLink('Author', 'TypeScript SDK', ''),
      metadataLink('Repository', 'microsoft/PowerToys', 'https://github.com/microsoft/PowerToys'),
      metadataSeparator(),
      metadataTags('Tags', [
        tag({ text: 'TypeScript', foreground: color(49, 120, 198) }),
        tag({ text: 'SDK', foreground: color(22, 163, 74) }),
        tag({ text: 'Sample', icon: IconInfo.fromGlyph('⭐'), foreground: color(234, 179, 8) }),
      ]),
      metadataSeparator(),
      metadataTags('Version', [
        tag({ text: 'v1.0.0', foreground: color(107, 114, 128) }),
      ]),
    ],
  });

  getContent(): IContent[] {
    const md = new MarkdownContent(
      '# Markdown with Details Panel\n\n' +
      'This page demonstrates a **content page** that combines rich markdown ' +
      'with a **details panel** on the side.\n\n' +
      '## How It Works\n\n' +
      'The `ContentPage` class has a `details` property that accepts an `IDetails` object with:\n\n' +
      '- **Title** — shown at the top of the details panel\n' +
      '- **Body** — markdown text in the panel\n' +
      '- **HeroImage** — icon or image shown prominently\n' +
      '- **Metadata** — array of key-value entries with different data types:\n' +
      '  - **Links** — text with optional URL\n' +
      '  - **Tags** — colored pills with optional icons\n' +
      '  - **Separators** — visual dividers between sections\n\n' +
      '## Code Example\n\n' +
      '```typescript\n' +
      'class MyPage extends ContentPage {\n' +
      '  details: IDetails = {\n' +
      '    Title: \'About\',\n' +
      '    Body: \'Description in **markdown**\',\n' +
      '    Metadata: [\n' +
      '      { Key: \'Author\', Data: { Text: \'Name\', Url: \'\' } },\n' +
      '      { Key: \'Tags\', Data: { Tags: [{ Text: \'v1\', ... }] } },\n' +
      '    ],\n' +
      '  };\n' +
      '}\n' +
      '```\n\n' +
      '> 👈 Check the details panel on the right for a live example of metadata.',
    );
    md.id = 'markdown-details-body';
    return [md];
  }
}
