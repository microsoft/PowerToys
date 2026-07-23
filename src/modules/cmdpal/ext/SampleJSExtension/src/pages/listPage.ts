// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ConfirmableCommand,
  ListItemBase,
  ListPageBase,
  NoOpCommand,
  OpenUrlCommand,
} from '@microsoft/cmdpal-sdk';
import type { ContextItem, IListItem, KeyChord } from '@microsoft/cmdpal-sdk';
import { icon, tag } from '../util.js';
import { SampleMarkdownPage } from './markdownPages.js';
import { SampleListPageWithDetails } from './detailsPage.js';
import {
  IndeterminateProgressMessageCommand,
  SendMessageCommand,
  SingleMessageCommand,
  StatusMessageCommand,
} from '../commands/statusCommands.js';

// Modifier bitmask values used by KeyChord: Ctrl = 1, Alt = 2, Shift = 4, Win = 8.
const CTRL = 1;

function keyChord(modifiers: number, vkey: number): KeyChord {
  return { modifiers, vkey, scanCode: 0 };
}

/**
 * A basic list page that demonstrates navigation, links, tags, status
 * messages, confirmation dialogs, and a nested context menu. Mirrors the C#
 * `SampleListPage`.
 *
 * Not-yet-supported in the JS protocol and therefore omitted here:
 *  - `IExtendedAttributesProvider` command properties (the C# "I have
 *    properties" items).
 *  - The Win32 foreground-window command (no P/Invoke from an isolated Node
 *    process).
 */
export class SampleListPage extends ListPageBase {
  readonly id = 'sample-list-page';
  readonly name = 'Sample List Page';
  readonly title = 'Sample List Page';

  override icon = icon('\uEA37');

  override getItems(): IListItem[] {
    const secondCommand = new StatusMessageCommand(
      'Secondary command invoked',
      'warning',
      'ctx-secondary',
    );
    secondCommand.name = 'Secondary command';
    secondCommand.icon = icon('\uF147');

    const thirdCommand = new StatusMessageCommand('Third command invoked', 'error', 'ctx-third');
    thirdCommand.name = 'Do 3';
    thirdCommand.icon = icon('\uF148');

    const deeperCommand = new StatusMessageCommand(
      'Second-level command invoked',
      'info',
      'ctx-deeper',
    );
    deeperCommand.name = 'A command one level down';
    deeperCommand.icon = icon('\uF149');

    const deepestCommand = new StatusMessageCommand(
      'You reached the deepest command',
      'success',
      'ctx-deepest',
    );
    deepestCommand.name = 'The deepest command';
    deepestCommand.icon = icon('\uF14A');

    const primaryContext = new StatusMessageCommand(
      'Primary command invoked',
      'info',
      'ctx-primary',
    );
    primaryContext.name = 'Primary command';
    primaryContext.icon = icon('\uF146');

    // `moreCommands` on a context item nests a sub-menu, and each nested item
    // can nest again. Here "We can go deeper..." opens a second level, which in
    // turn opens a third, demonstrating recursive context menus end to end.
    const moreCommands: ContextItem[] = [
      {
        command: secondCommand,
        title: "I'm a second command",
        requestedShortcut: keyChord(CTRL, 0x31),
      },
      {
        command: thirdCommand,
        title: 'We can go deeper...',
        icon: icon('\uF148'),
        requestedShortcut: keyChord(CTRL, 0x32),
        moreCommands: [
          {
            command: deeperCommand,
            title: 'Another level down',
            icon: icon('\uF149'),
            moreCommands: [
              {
                command: deepestCommand,
                title: 'The deepest level',
                icon: icon('\uF14A'),
              },
            ],
          },
        ],
      },
    ];

    const confirmOnce = new ConfirmableCommand({
      id: 'confirm-once',
      name: 'Confirm',
      title: 'You can set a title for the dialog',
      description: 'Are you really sure you want to do the thing?',
      primaryCommand: new StatusMessageCommand('The dialog was confirmed', 'info', 'confirmed'),
    });

    const confirmTwice = new ConfirmableCommand({
      id: 'confirm-twice',
      name: 'How sure are you?',
      title: 'You can ask twice too',
      description: "You probably don't want to though, that'd be annoying.",
      primaryCommand: confirmOnce,
    });

    return [
      new ListItemBase({
        command: new NoOpCommand('basic-item'),
        title: 'This is a basic item in the list',
        subtitle: "I don't do anything though",
      }),
      new ListItemBase({
        command: new SampleListPageWithDetails(),
        title: 'This item will take you to another page',
        subtitle: 'This allows for nested lists of items',
      }),
      new ListItemBase({
        command: new OpenUrlCommand('https://github.com/microsoft/powertoys'),
        title: 'Or you can go to links',
        subtitle: 'This takes you to the PowerToys repo on GitHub',
      }),
      new ListItemBase({
        command: new SampleMarkdownPage(),
        title: 'Items can have tags',
        subtitle: "and I'll take you to a page with markdown content",
        tags: [tag('Sample Tag')],
      }),
      new ListItemBase({
        command: primaryContext,
        title: 'You can add context menu items too. Press Ctrl+K',
        subtitle: 'Try pressing Ctrl+1 with me selected',
        icon: icon('\uE712'),
        moreCommands,
      }),
      new ListItemBase({
        command: new SendMessageCommand(),
        title: 'I send lots of messages',
        subtitle: 'Status messages can be used to provide feedback to the user in-app',
      }),
      new ListItemBase({
        command: new SingleMessageCommand(),
        title: 'I send a single message',
        subtitle: 'This demonstrates both showing and hiding a single message',
      }),
      new ListItemBase({
        command: new IndeterminateProgressMessageCommand(),
        title: 'Do a thing with a spinner',
        subtitle:
          'Messages can have progress spinners, to indicate something is happening in the background',
      }),
      new ListItemBase({
        command: confirmOnce,
        title: 'Confirm before doing something',
      }),
      new ListItemBase({
        command: confirmTwice,
        title: 'Confirm twice before doing something',
      }),
    ];
  }
}
