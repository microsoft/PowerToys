// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ListPage,
  ListItem,
  InvokableCommand,
  CommandResult,
  IListItem,
  ICommandResult,
  ContentSize,
  IconInfo,
  tag,
  color,
  details,
  metadataTags,
  metadataLink,
  metadataSeparator,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Commands for detail items
// ---------------------------------------------------------------------------

class NoOpCommand extends InvokableCommand {
  constructor(id: string, name: string) {
    super();
    this.id = id;
    this.name = name;
  }

  invoke(): ICommandResult {
    return CommandResult.keepOpen();
  }
}

// ---------------------------------------------------------------------------
// Details list page
// ---------------------------------------------------------------------------

/**
 * Demonstrates list items with a details panel showing:
 * - Different detail sizes (small, medium, large)
 * - Hero images
 * - Markdown body text
 * - Metadata with links, separators, colored tags, and tag icons
 */
export class DetailsListPage extends ListPage {
  id = 'details-list';
  name = 'Details Panel Samples';
  placeholderText = 'Browse items with details...';
  showDetails = true;

  getItems(): IListItem[] {
    return [
      // Item with small details
      new ListItem({
        title: 'Small Details',
        subtitle: 'Compact detail panel with minimal metadata',
        icon: IconInfo.fromGlyph('\uE8A0'),
        command: new NoOpCommand('detail-small', 'Small Details'),
        tags: [
          tag({ text: 'Small', toolTip: 'ContentSize.Small' }),
        ],
        details: details({
          title: 'Small Details Panel',
          body: 'This is a **small** details panel with minimal content.',
          heroImage: IconInfo.fromGlyph('\uE8D4'),
          metadata: [],
          size: ContentSize.Small,
        }),
      }),

      // Item with medium details + metadata
      new ListItem({
        title: 'Medium Details with Metadata',
        subtitle: 'Details panel with links, tags, and separators',
        icon: IconInfo.fromGlyph('\uE8A1'),
        command: new NoOpCommand('detail-medium', 'Medium Details'),
        tags: [
          tag({ text: 'Medium', toolTip: 'ContentSize.Medium' }),
        ],
        details: details({
          title: 'Medium Details Panel',
          body: 'This details panel demonstrates **metadata** elements:\n\n- Links to external resources\n- Colored tags with icons\n- Separator lines between sections',
          heroImage: IconInfo.fromGlyph('📋'),
          size: ContentSize.Medium,
          metadata: [
            metadataLink('Author', 'TypeScript SDK Team', ''),
            metadataSeparator(),
            metadataTags('Status', [
              tag({ text: 'Active', icon: IconInfo.fromGlyph('✅'), toolTip: 'Currently active', foreground: color(0, 128, 0) }),
            ]),
            metadataTags('Version', [
              tag({ text: 'v1.0.0', toolTip: 'SDK Version 1.0.0', foreground: color(0, 100, 200) }),
            ]),
          ],
        }),
      }),

      // Item with large details + hero image
      new ListItem({
        title: 'Large Details with Hero Image',
        subtitle: 'Full-size details panel with rich content',
        icon: IconInfo.fromGlyph('\uE8A2'),
        command: new NoOpCommand('detail-large', 'Large Details'),
        tags: [
          tag({ text: 'Large', toolTip: 'ContentSize.Large' }),
        ],
        details: details({
          title: 'Large Details Panel',
          body: '# Rich Content\n\nThis large details panel showcases:\n\n1. **Hero images** at the top\n2. **Markdown** body with formatting\n3. **Metadata** sections with various data types\n\n> Details panels are great for showing additional context without navigating away.',
          heroImage: IconInfo.fromGlyph('🖼️'),
          size: ContentSize.Large,
          metadata: [
            metadataTags('Category', [
              tag({ text: 'Showcase', icon: IconInfo.fromGlyph('⭐'), toolTip: 'Featured showcase item', foreground: color(200, 150, 0) }),
            ]),
            metadataSeparator(),
            metadataLink('Documentation', 'View on GitHub', 'https://github.com/microsoft/PowerToys'),
          ],
        }),
      }),

      // Item with colored tags
      new ListItem({
        title: 'Colored Tags Demo',
        subtitle: 'Tags with custom foreground and background colors',
        icon: IconInfo.fromGlyph('🏷️'),
        command: new NoOpCommand('detail-colors', 'Colored Tags'),
        tags: [
          tag({ text: 'Red', icon: IconInfo.fromGlyph('🔴'), toolTip: 'Red foreground tag', foreground: color(220, 38, 38) }),
          tag({ text: 'Green', icon: IconInfo.fromGlyph('🟢'), toolTip: 'Green foreground tag', foreground: color(22, 163, 74) }),
          tag({ text: 'Blue BG', toolTip: 'Blue background tag', foreground: color(255, 255, 255), background: color(37, 99, 235) }),
        ],
        details: details({
          title: 'Colored Tags',
          body: 'Tags can have custom **foreground** and **background** colors using RGBA values.\n\nSet `hasValue: true` on the color to enable it.',
          metadata: [
            metadataTags('Colors', [
              tag({ text: 'Purple', foreground: color(147, 51, 234) }),
              tag({ text: 'Orange', foreground: color(234, 88, 12) }),
            ]),
          ],
        }),
      }),

      // Item with no details (for contrast)
      new ListItem({
        title: 'Item Without Details',
        subtitle: 'This item has no details panel — the panel stays empty',
        icon: IconInfo.fromGlyph('\uE8BB'),
        command: new NoOpCommand('no-details', 'No Details'),
      }),
    ];
  }
}
