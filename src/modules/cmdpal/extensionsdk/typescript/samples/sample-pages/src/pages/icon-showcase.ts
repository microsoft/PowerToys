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
  IconInfo,
  tag,
  color,
  details,
  metadataTags,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Icon data
// ---------------------------------------------------------------------------

interface IconEntry {
  name: string;
  icon: string;
  category: string;
  codeInfo: string;
}

const ICONS: IconEntry[] = [
  // Segoe Fluent Icons
  { name: 'Settings', icon: '\uE713', category: 'Segoe Fluent', codeInfo: 'U+E713' },
  { name: 'Search', icon: '\uE721', category: 'Segoe Fluent', codeInfo: 'U+E721' },
  { name: 'Home', icon: '\uE80F', category: 'Segoe Fluent', codeInfo: 'U+E80F' },
  { name: 'Favorite', icon: '\uE735', category: 'Segoe Fluent', codeInfo: 'U+E735' },
  { name: 'Mail', icon: '\uE715', category: 'Segoe Fluent', codeInfo: 'U+E715' },
  { name: 'Calendar', icon: '\uE787', category: 'Segoe Fluent', codeInfo: 'U+E787' },
  { name: 'People', icon: '\uE716', category: 'Segoe Fluent', codeInfo: 'U+E716' },
  { name: 'Edit', icon: '\uE70F', category: 'Segoe Fluent', codeInfo: 'U+E70F' },
  { name: 'Delete', icon: '\uE74D', category: 'Segoe Fluent', codeInfo: 'U+E74D' },
  { name: 'Pin', icon: '\uE718', category: 'Segoe Fluent', codeInfo: 'U+E718' },
  { name: 'Copy', icon: '\uE8C8', category: 'Segoe Fluent', codeInfo: 'U+E8C8' },
  { name: 'Globe', icon: '\uE774', category: 'Segoe Fluent', codeInfo: 'U+E774' },

  // Basic Emojis
  { name: 'Smiling Face', icon: '😀', category: 'Emoji', codeInfo: 'U+1F600' },
  { name: 'Heart Eyes', icon: '😍', category: 'Emoji', codeInfo: 'U+1F60D' },
  { name: 'Thinking', icon: '🤔', category: 'Emoji', codeInfo: 'U+1F914' },
  { name: 'Wave', icon: '👋', category: 'Emoji', codeInfo: 'U+1F44B' },
  { name: 'Thumbs Up', icon: '👍', category: 'Emoji', codeInfo: 'U+1F44D' },
  { name: 'Fire', icon: '🔥', category: 'Emoji', codeInfo: 'U+1F525' },
  { name: 'Star', icon: '⭐', category: 'Emoji', codeInfo: 'U+2B50' },
  { name: 'Rocket', icon: '🚀', category: 'Emoji', codeInfo: 'U+1F680' },
  { name: 'Lightning', icon: '⚡', category: 'Emoji', codeInfo: 'U+26A1' },
  { name: 'Wizard', icon: '🧙', category: 'Emoji', codeInfo: 'U+1F9D9' },

  // Complex Emoji Sequences (ZWJ, skin tones)
  { name: 'Family', icon: '👨‍👩‍👧‍👦', category: 'Complex Emoji', codeInfo: 'ZWJ sequence' },
  { name: 'Technologist', icon: '🧑‍💻', category: 'Complex Emoji', codeInfo: 'ZWJ: Person + Laptop' },
  { name: 'Rainbow Flag', icon: '🏳️‍🌈', category: 'Complex Emoji', codeInfo: 'ZWJ: Flag + Rainbow' },
  { name: 'Wave (Medium)', icon: '👋🏽', category: 'Complex Emoji', codeInfo: 'U+1F44B + Skin Tone 4' },
  { name: 'Thumbs Up (Dark)', icon: '👍🏿', category: 'Complex Emoji', codeInfo: 'U+1F44D + Skin Tone 6' },

  // Flag Emojis
  { name: 'US Flag', icon: '🇺🇸', category: 'Flags', codeInfo: 'Regional: U+S' },
  { name: 'UK Flag', icon: '🇬🇧', category: 'Flags', codeInfo: 'Regional: G+B' },
  { name: 'Japan Flag', icon: '🇯🇵', category: 'Flags', codeInfo: 'Regional: J+P' },
  { name: 'Brazil Flag', icon: '🇧🇷', category: 'Flags', codeInfo: 'Regional: B+R' },

  // Keycap Emojis
  { name: 'Keycap 1', icon: '1️⃣', category: 'Keycaps', codeInfo: '1 + VS16 + Keycap' },
  { name: 'Keycap 2', icon: '2️⃣', category: 'Keycaps', codeInfo: '2 + VS16 + Keycap' },
  { name: 'Keycap Hash', icon: '#️⃣', category: 'Keycaps', codeInfo: '# + VS16 + Keycap' },
];

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

class ShowIconInfoCommand extends InvokableCommand {
  private readonly entry: IconEntry;

  constructor(entry: IconEntry) {
    super();
    this.id = `icon-${entry.name.toLowerCase().replace(/\s+/g, '-')}`;
    this.name = entry.name;
    this.entry = entry;
  }

  invoke(): ICommandResult {
    return CommandResult.showToast(
      `${this.entry.icon} ${this.entry.name} — ${this.entry.codeInfo} (${this.entry.category})`,
    );
  }
}

// ---------------------------------------------------------------------------
// Icon showcase page
// ---------------------------------------------------------------------------

/**
 * Demonstrates various icon types:
 * - Segoe Fluent icons (glyph code points)
 * - Basic emojis
 * - Complex emoji sequences (ZWJ, skin tones)
 * - Flag emojis (regional indicators)
 * - Keycap emojis
 *
 * Each item shows the icon and details with code point information.
 */
export class IconShowcasePage extends ListPage {
  id = 'icon-showcase';
  name = 'Icon Showcase';
  placeholderText = 'Browse icon types...';
  showDetails = true;

  getItems(): IListItem[] {
    return ICONS.map(
      (entry) =>
        new ListItem({
          title: `${entry.icon}  ${entry.name}`,
          subtitle: entry.codeInfo,
          icon: IconInfo.fromGlyph(entry.icon),
          command: new ShowIconInfoCommand(entry),
          section: entry.category,
          tags: [
            tag({ text: entry.category, toolTip: `Category: ${entry.category}` }),
          ],
          details: details({
            title: entry.name,
            body: `**Icon:** ${entry.icon}\n\n**Code:** \`${entry.codeInfo}\`\n\n**Category:** ${entry.category}`,
            heroImage: IconInfo.fromGlyph(entry.icon),
            metadata: [
              metadataTags('Code Point', [
                tag({ text: entry.codeInfo, foreground: color(59, 130, 246) }),
              ]),
            ],
          }),
        }),
    );
  }
}
