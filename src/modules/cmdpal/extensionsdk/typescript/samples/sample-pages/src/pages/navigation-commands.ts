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
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Navigation commands
// ---------------------------------------------------------------------------

class DismissCommand extends InvokableCommand {
  id = 'nav-dismiss';
  name = 'Dismiss';

  invoke(): ICommandResult {
    return CommandResult.dismiss();
  }
}

class GoHomeCommand extends InvokableCommand {
  id = 'nav-go-home';
  name = 'Go Home';

  invoke(): ICommandResult {
    return CommandResult.goHome();
  }
}

class GoBackCommand extends InvokableCommand {
  id = 'nav-go-back';
  name = 'Go Back';

  invoke(): ICommandResult {
    return CommandResult.goBack();
  }
}

class HideCommand extends InvokableCommand {
  id = 'nav-hide';
  name = 'Hide';

  invoke(): ICommandResult {
    return CommandResult.hide();
  }
}

class KeepOpenCommand extends InvokableCommand {
  id = 'nav-keep-open';
  name = 'Keep Open';

  invoke(): ICommandResult {
    return CommandResult.keepOpen();
  }
}

class ShowToastCommand extends InvokableCommand {
  id = 'nav-toast';
  name = 'Show Toast';

  invoke(): ICommandResult {
    return CommandResult.showToast('🎉 This is a toast notification from the navigation page!');
  }
}

class ConfirmCommand extends InvokableCommand {
  id = 'nav-confirm';
  name = 'Confirm';

  private _yesCmd: InvokableCommand;
  constructor(yesCmd: InvokableCommand) {
    super();
    this._yesCmd = yesCmd;
  }

  invoke(): ICommandResult {
    return CommandResult.confirm(
      'Are you sure?',
      'This will show a toast confirming your choice.',
      this._yesCmd,
    );
  }
}

class ConfirmYesCommand extends InvokableCommand {
  id = 'nav-confirm-yes';
  name = 'Confirm Yes';

  invoke(): ICommandResult {
    return CommandResult.showToast('✅ You confirmed yes!');
  }
}

// ---------------------------------------------------------------------------
// Navigation commands page
// ---------------------------------------------------------------------------

/**
 * Demonstrates all CommandResult types:
 * - Dismiss (close palette)
 * - GoHome (navigate to root)
 * - GoBack (navigate back)
 * - Hide (hide palette)
 * - KeepOpen (stay on current page)
 * - ShowToast (info message)
 * - Confirm (confirmation dialog)
 *
 * Similar to WinRT SampleNavigationCommands.
 */
export class NavigationCommandsPage extends ListPage {
  id = 'navigation-commands';
  name = 'Navigation Commands';
  placeholderText = 'Try different navigation results...';

  private confirmYes = new ConfirmYesCommand();

  getItems(): IListItem[] {
    return [
      new ListItem({
        title: 'Dismiss',
        subtitle: 'CommandResult.dismiss() — Closes the command palette',
        icon: IconInfo.fromGlyph('\uE711'),
        command: new DismissCommand(),
        tags: [
          tag({ text: 'Kind=0', foreground: color(107, 114, 128) }),
        ],
      }),
      new ListItem({
        title: 'Go Home',
        subtitle: 'CommandResult.goHome() — Navigate to root',
        icon: IconInfo.fromGlyph('\uE80F'),
        command: new GoHomeCommand(),
        tags: [
          tag({ text: 'Kind=1', foreground: color(59, 130, 246) }),
        ],
      }),
      new ListItem({
        title: 'Go Back',
        subtitle: 'CommandResult.goBack() — Navigate to previous page',
        icon: IconInfo.fromGlyph('\uE72B'),
        command: new GoBackCommand(),
        tags: [
          tag({ text: 'Kind=2', foreground: color(59, 130, 246) }),
        ],
      }),
      new ListItem({
        title: 'Hide',
        subtitle: 'CommandResult.hide() — Hide the palette (keeps state)',
        icon: IconInfo.fromGlyph('\uED1A'),
        command: new HideCommand(),
        tags: [
          tag({ text: 'Kind=3', foreground: color(245, 158, 11) }),
        ],
      }),
      new ListItem({
        title: 'Keep Open',
        subtitle: 'CommandResult.keepOpen() — Stay on current page',
        icon: IconInfo.fromGlyph('\uE785'),
        command: new KeepOpenCommand(),
        tags: [
          tag({ text: 'Kind=4', foreground: color(22, 163, 74) }),
        ],
      }),
      new ListItem({
        title: 'Show Toast',
        subtitle: 'CommandResult.showToast(msg) — Display an info bar',
        icon: IconInfo.fromGlyph('\uE7E7'),
        command: new ShowToastCommand(),
        tags: [
          tag({ text: 'Kind=6', foreground: color(234, 179, 8) }),
        ],
      }),
      new ListItem({
        title: 'Confirm',
        subtitle: 'CommandResult.confirm(yes, no, title, desc) — Confirmation dialog',
        icon: IconInfo.fromGlyph('\uE8FB'),
        command: new ConfirmCommand(this.confirmYes),
        tags: [
          tag({ text: 'Kind=7', foreground: color(239, 68, 68) }),
        ],
      }),
    ];
  }
}
