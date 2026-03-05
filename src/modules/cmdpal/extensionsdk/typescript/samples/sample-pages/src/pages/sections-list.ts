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
  IContextItem,
  IconInfo,
  tag,
  color,
  sectionHeader,
  contextItem,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Commands
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
// Sections list page
// ---------------------------------------------------------------------------

/**
 * Demonstrates list items grouped by sections.
 * Uses `sectionHeader()` to insert separator items before each group.
 * The UI renders these as non-interactive section headings.
 */
export class SectionsPage extends ListPage {
  id = 'sections-list';
  name = 'Sectioned List';
  placeholderText = 'Browse sectioned items...';

  getItems(): IListItem[] {
    return [
      // ── Getting Started ──────────────────────────────────────
      sectionHeader('🚀 Getting Started'),
      new ListItem({
        title: 'Installation Guide',
        subtitle: 'How to install the TypeScript SDK',
        icon: IconInfo.fromGlyph('\uE896'),
        command: new NoOpCommand('install-guide', 'Installation Guide'),
        section: '🚀 Getting Started',
      }),
      new ListItem({
        title: 'First Extension',
        subtitle: 'Create your first extension in 5 minutes',
        icon: IconInfo.fromGlyph('\uE8A5'),
        command: new NoOpCommand('first-ext', 'First Extension'),
        section: '🚀 Getting Started',
      }),
      new ListItem({
        title: 'Project Structure',
        subtitle: 'Recommended folder layout for extensions',
        icon: IconInfo.fromGlyph('\uE8B7'),
        command: new NoOpCommand('project-struct', 'Project Structure'),
        section: '🚀 Getting Started',
      }),

      // ── Core Concepts ────────────────────────────────────────
      sectionHeader('📚 Core Concepts'),
      new ListItem({
        title: 'Commands',
        subtitle: 'InvokableCommand and CommandResult',
        icon: IconInfo.fromGlyph('\uE756'),
        command: new NoOpCommand('concept-cmds', 'Commands'),
        section: '📚 Core Concepts',
      }),
      new ListItem({
        title: 'Pages',
        subtitle: 'ListPage, DynamicListPage, ContentPage',
        icon: IconInfo.fromGlyph('\uE7C3'),
        command: new NoOpCommand('concept-pages', 'Pages'),
        section: '📚 Core Concepts',
      }),
      new ListItem({
        title: 'Content Types',
        subtitle: 'MarkdownContent, FormContent, TreeContent',
        icon: IconInfo.fromGlyph('\uECA5'),
        command: new NoOpCommand('concept-content', 'Content Types'),
        section: '📚 Core Concepts',
      }),
      new ListItem({
        title: 'Tags & Details',
        subtitle: 'Metadata, colored tags, detail panels',
        icon: IconInfo.fromGlyph('🏷️'),
        command: new NoOpCommand('concept-tags', 'Tags & Details'),
        section: '📚 Core Concepts',
      }),
      new ListItem({
        title: 'Navigation',
        subtitle: 'GoToPage, GoBack, GoHome',
        icon: IconInfo.fromGlyph('\uE72A'),
        command: new NoOpCommand('concept-nav', 'Navigation'),
        section: '📚 Core Concepts',
      }),

      // ── Advanced Topics ──────────────────────────────────────
      sectionHeader('🔧 Advanced Topics'),
      new ListItem({
        title: 'Dynamic Search',
        subtitle: 'Real-time filtering with DynamicListPage',
        icon: IconInfo.fromGlyph('\uE721'),
        command: new NoOpCommand('adv-search', 'Dynamic Search'),
        section: '🔧 Advanced Topics',
      }),
      new ListItem({
        title: 'Form Handling',
        subtitle: 'Adaptive Cards and form submission',
        icon: IconInfo.fromGlyph('\uE9D5'),
        command: new NoOpCommand('adv-forms', 'Form Handling'),
        section: '🔧 Advanced Topics',
      }),
      new ListItem({
        title: 'Tree Content',
        subtitle: 'Nested hierarchical content structures',
        icon: IconInfo.fromGlyph('\uE8D5'),
        command: new NoOpCommand('adv-tree', 'Tree Content'),
        section: '🔧 Advanced Topics',
      }),
      new ListItem({
        title: 'Context Menus',
        subtitle: 'Right-click to see nested context menus',
        icon: IconInfo.fromGlyph('\uE712'),
        command: new NoOpCommand('adv-ctx', 'Context Menus'),
        section: '🔧 Advanced Topics',
        moreCommands: [
          contextItem({
            title: 'Edit',
            icon: IconInfo.fromGlyph('\uE70F'),
            command: new NoOpCommand('ctx-edit', 'Edit'),
            moreCommands: [
              contextItem({
                title: 'Edit in Notepad',
                icon: IconInfo.fromGlyph('\uE70F'),
                command: new NoOpCommand('ctx-edit-notepad', 'Edit in Notepad'),
              }),
              contextItem({
                title: 'Edit in VS Code',
                icon: IconInfo.fromGlyph('\uE943'),
                command: new NoOpCommand('ctx-edit-vscode', 'Edit in VS Code'),
              }),
              contextItem({
                title: 'Edit in Terminal',
                icon: IconInfo.fromGlyph('\uE756'),
                command: new NoOpCommand('ctx-edit-terminal', 'Edit in Terminal'),
              }),
            ],
          }),
          contextItem({
            title: 'Share',
            icon: IconInfo.fromGlyph('\uE72D'),
            command: new NoOpCommand('ctx-share', 'Share'),
            moreCommands: [
              contextItem({
                title: 'Copy Link',
                icon: IconInfo.fromGlyph('\uE71B'),
                command: new NoOpCommand('ctx-share-link', 'Copy Link'),
              }),
              contextItem({
                title: 'Email',
                icon: IconInfo.fromGlyph('\uE715'),
                command: new NoOpCommand('ctx-share-email', 'Email'),
              }),
            ],
          }),
          contextItem({
            title: 'Delete',
            icon: IconInfo.fromGlyph('\uE74D'),
            command: new NoOpCommand('ctx-delete', 'Delete'),
            isCritical: true,
          }),
        ],
      }),

      // ── API Reference ────────────────────────────────────────
      sectionHeader('📖 API Reference'),
      new ListItem({
        title: 'CommandProvider',
        subtitle: 'Base class for extension providers',
        icon: IconInfo.fromGlyph('\uE943'),
        command: new NoOpCommand('api-provider', 'CommandProvider'),
        section: '📖 API Reference',
        tags: [
          tag({ text: 'Class', toolTip: 'Abstract class', foreground: color(147, 51, 234) }),
        ],
      }),
      new ListItem({
        title: 'ExtensionServer',
        subtitle: 'Static server for registering and starting extensions',
        icon: IconInfo.fromGlyph('\uE968'),
        command: new NoOpCommand('api-server', 'ExtensionServer'),
        section: '📖 API Reference',
        tags: [
          tag({ text: 'Static', toolTip: 'Static class', foreground: color(59, 130, 246) }),
        ],
      }),
      new ListItem({
        title: 'CommandResult',
        subtitle: 'Result type for command invocations',
        icon: IconInfo.fromGlyph('\uE73E'),
        command: new NoOpCommand('api-result', 'CommandResult'),
        section: '📖 API Reference',
        tags: [
          tag({ text: 'Class', toolTip: 'Result class', foreground: color(147, 51, 234) }),
        ],
      }),
    ];
  }
}
