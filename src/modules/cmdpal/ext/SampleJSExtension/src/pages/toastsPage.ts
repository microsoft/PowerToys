// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { DynamicListPageBase, InvokableCommandBase, ListItemBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { CommandResult, IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';
import { ShowToastCommand, StatusMessageCommand } from '../commands/statusCommands.js';

/** Shows a toast that keeps the palette open, carrying a caller-supplied message. */
class KeepOpenToastCommand extends InvokableCommandBase {
  readonly id: string;
  readonly name: string;

  private readonly message: string;

  constructor(id: string, name: string, message: string) {
    super();
    this.id = id;
    this.name = name;
    this.message = message;
  }

  override invoke(): CommandResult {
    return {
      kind: 'showToast',
      args: { message: this.message, result: { kind: 'keepOpen' } },
    };
  }
}

/**
 * Demonstrates `showToast` command results and lets the user send a custom
 * toast typed into the search box. Mirrors the C# `SampleToastsPage`.
 *
 * Not-yet-supported in the JS protocol: `ToastArgs` carries a `message` and an
 * optional follow-up `result` only. The C# toast icon and action button
 * (`IToastArgs2.Icon` / `IToastArgs2.Command`) have no JS equivalent, so those
 * variants are omitted.
 */
export class SampleToastsPage extends DynamicListPageBase {
  readonly id = 'sample-toasts-page';
  readonly name = 'Toast Notifications';
  readonly title = 'Toast Notification Samples';

  override icon = icon('\uE789');
  override placeholderText = 'Type a custom message and press Enter...';

  override setSearchText(text: string): void {
    this.searchText = text;
    this.notifyItemsChanged();
  }

  override getItems(): IListItem[] {
    const query = (this.searchText ?? '').trim();

    const customItem =
      query.length > 0
        ? new ListItemBase({
            command: new KeepOpenToastCommand('custom-toast', 'Send custom toast', query),
            title: `Show toast: "${query}"`,
            subtitle: 'Uses a showToast result and keeps the palette open',
            icon: icon('\uE724'),
          })
        : new ListItemBase({
            command: new NoOpCommand('toast-hint'),
            title: 'Type a message above to send a custom toast',
            subtitle: "Start typing - the first item becomes a 'Show toast' action",
            icon: icon('\uE8BD'),
          });

    return [
      customItem,
      new ListItemBase({
        command: new ShowToastCommand('Hello from the Command Palette!', 'short-toast'),
        title: 'Short toast (dismisses the palette)',
        subtitle: 'A showToast result with the default dismiss follow-up',
        icon: icon('\uE91C'),
      }),
      new ListItemBase({
        command: new KeepOpenToastCommand(
          'keep-open-toast',
          'Show toast (keep palette open)',
          'The palette stays open - press Enter again to re-fire.',
        ),
        title: 'Short toast (keeps the palette open)',
        subtitle: 'ToastArgs.result = keepOpen',
        icon: icon('\uE8A7'),
      }),
      new ListItemBase({
        command: new KeepOpenToastCommand(
          'long-toast',
          'Show long toast',
          'This is a much longer toast message designed to verify that the banner inside the transparent toast window wraps gracefully across multiple lines without clipping its drop shadow or its slide-in animation.',
        ),
        title: 'Long, wrapping toast',
        subtitle: 'Verifies multi-line wrapping inside the banner',
        icon: icon('\uE7C3'),
      }),
      new ListItemBase({
        command: new StatusMessageCommand(
          'This is an in-page status message',
          'success',
          'toast-status',
        ),
        title: 'In-page status message (different path)',
        subtitle: 'Uses the host status bridge - renders inline, NOT in the toast window',
        icon: icon('\uE7BA'),
      }),
    ];
  }
}
