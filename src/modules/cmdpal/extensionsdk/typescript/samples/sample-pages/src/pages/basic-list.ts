// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ListPage,
  ListItem,
  InvokableCommand,
  CommandResult,
  IListItem,
  IContextItem,
  ICommandResult,
  IconInfo,
  tag,
  color,
  details,
  metadataTags,
  contextItem,
  ContentSize,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

class OpenLinkCommand extends InvokableCommand {
  private readonly url: string;

  constructor(id: string, name: string, url: string) {
    super();
    this.id = id;
    this.name = name;
    this.url = url;
  }

  invoke(): ICommandResult {
    // In a real extension, you'd open the URL via the host.
    // For the sample, show a toast with the URL.
    return CommandResult.showToast(`Opening: ${this.url}`);
  }
}

class CopyTextCommand extends InvokableCommand {
  private readonly text: string;

  constructor(id: string, label: string, text: string) {
    super();
    this.id = id;
    this.name = label;
    this.text = text;
  }

  invoke(): ICommandResult {
    // In a real extension, you'd use a clipboard API.
    // For the sample, show a toast indicating what would be copied.
    return CommandResult.showToast(`Copied "${this.text}" to clipboard`);
  }
}

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
// Basic list page
// ---------------------------------------------------------------------------

/**
 * Demonstrates a basic list page with:
 * - Items with tags (plain and colored)
 * - Items with details panels (title, body, metadata)
 * - Items with context menus (MoreCommands)
 * - Various command results (toasts, dismiss, keep open)
 */
export class BasicListPage extends ListPage {
  id = 'basic-list';
  name = 'Basic List Page';
  placeholderText = 'Browse sample list items...';
  showDetails = true;

  getItems(): IListItem[] {
    return [
      // 1) Simple item with tags
      new ListItem({
        title: 'Item with Tags',
        subtitle: 'Demonstrates colored tag badges',
        icon: IconInfo.fromGlyph('\uE8EC'),
        command: new NoOpCommand('item-tags', 'Item with Tags'),
        tags: [
          tag({ text: 'TypeScript', foreground: color(49, 120, 198) }),
          tag('SDK'),
          tag({ text: 'New', foreground: color(16, 124, 16), background: color(223, 246, 221) }),
        ],
      }),

      // 2) Item with a details panel
      this.createItemWithDetails(),

      // 3) Item with context menu
      this.createItemWithContextMenu(),

      // 4) Item that shows a toast
      new ListItem({
        title: 'Show a Toast',
        subtitle: 'Click to see an inline toast notification',
        icon: IconInfo.fromGlyph('\uE7E7'),
        command: (() => {
          const cmd = new InvokableCommandImpl(
            'toast-item',
            'Show Toast',
            () => CommandResult.showToast('Hello from TypeScript! 🎉'),
          );
          return cmd;
        })(),
      }),

      // 5) Item that triggers a confirmation dialog
      new ListItem({
        title: 'Confirmation Dialog',
        subtitle: 'Click to see a confirmation prompt',
        icon: IconInfo.fromGlyph('\uE783'),
        command: (() => {
          const primaryCmd = new InvokableCommandImpl(
            'confirm-yes',
            'Yes, do it!',
            () => CommandResult.showToast('Confirmed! Action executed.'),
          );
          const cmd = new InvokableCommandImpl(
            'confirm-item',
            'Confirmation Dialog',
            () =>
              CommandResult.confirm(
                'Are you sure?',
                'This will execute a sample action that cannot be undone.',
                primaryCmd,
              ),
          );
          return cmd;
        })(),
      }),

      // 6) Item that navigates back
      new ListItem({
        title: 'Go Back',
        subtitle: 'Navigates to the previous page',
        icon: IconInfo.fromGlyph('\uE72B'),
        command: new InvokableCommandImpl(
          'go-back',
          'Go Back',
          () => CommandResult.goBack(),
        ),
      }),
    ];
  }

  private createItemWithDetails(): ListItem {
    const itemDetails = details({
      title: 'TypeScript SDK',
      body: 'This extension was built with the **@cmdpal/sdk** TypeScript package.\n\n' +
        'It demonstrates how JavaScript/TypeScript extensions can provide the same ' +
        'rich experiences as native WinRT extensions.',
      heroImage: IconInfo.fromGlyph('\uE943'),
      size: ContentSize.Small,
      metadata: [
        metadataTags('Language', [tag({ text: 'TypeScript', foreground: color(49, 120, 198) })], IconInfo.fromGlyph('\uE943')),
        metadataTags('Version', [tag('1.0.0')]),
        metadataTags('Author', [tag('PowerToys Team')], IconInfo.fromGlyph('\uE77B')),
      ],
    });

    return new ListItem({
      title: 'Item with Details Panel',
      subtitle: 'Select to see the details pane on the right',
      icon: IconInfo.fromGlyph('\uE946'),
      command: new NoOpCommand('item-details', 'Item with Details'),
      details: itemDetails,
      tags: [tag('Details')],
    });
  }

  private createItemWithContextMenu(): ListItem {
    const nestedItems: IContextItem[] = [
      contextItem({
        title: 'As Markdown',
        icon: IconInfo.fromGlyph('\uE8A5'),
        command: new CopyTextCommand('ctx-copy-md', 'Copy as Markdown', '**Context Menu Item**'),
      }),
      contextItem({
        title: 'As Plain Text',
        icon: IconInfo.fromGlyph('\uE8C8'),
        command: new CopyTextCommand('ctx-copy-txt', 'Copy as Text', 'Context Menu Item'),
      }),
    ];

    const contextItems: IContextItem[] = [
      contextItem({
        title: 'Copy',
        icon: IconInfo.fromGlyph('\uE8C8'),
        command: new CopyTextCommand('ctx-copy', 'Copy Name', 'Context Menu Item'),
        moreCommands: nestedItems,
      }),
      contextItem({
        title: 'Open Docs',
        icon: IconInfo.fromGlyph('\uE8A7'),
        command: new OpenLinkCommand('ctx-open', 'Open Docs', 'https://github.com/microsoft/PowerToys'),
      }),
    ];

    return new ListItem({
      title: 'Item with Context Menu',
      subtitle: 'Right-click or use the ... menu — "Copy" has a nested submenu',
      icon: IconInfo.fromGlyph('\uE712'),
      command: new NoOpCommand('item-context', 'Item with Context Menu'),
      moreCommands: contextItems,
      tags: [tag('ContextMenu')],
    });
  }
}

// ---------------------------------------------------------------------------
// Inline invokable command helper
// ---------------------------------------------------------------------------

class InvokableCommandImpl extends InvokableCommand {
  private readonly _invoke: () => ICommandResult;

  constructor(
    id: string,
    name: string,
    invokeFn: () => ICommandResult,
  ) {
    super();
    this.id = id;
    this.name = name;
    this._invoke = invokeFn;
  }

  invoke(): ICommandResult {
    return this._invoke();
  }
}
