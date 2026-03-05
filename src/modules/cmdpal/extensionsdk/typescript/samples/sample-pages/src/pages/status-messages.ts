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
  MessageState,
  StatusContext,
  IconInfo,
  tag,
  color,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Status message commands
// ---------------------------------------------------------------------------

class ShowInfoStatusCommand extends InvokableCommand {
  id = 'status-info';
  name = 'Show Info Status';
  private readonly provider: any;

  constructor(provider: any) {
    super();
    this.provider = provider;
  }

  invoke(): ICommandResult {
    this.provider.showStatus(
      {
        Message: 'ℹ️ This is an informational status message',
        State: MessageState.Info,
        Progress: undefined,
      },
      StatusContext.Page,
    );
    return CommandResult.keepOpen();
  }
}

class ShowSuccessStatusCommand extends InvokableCommand {
  id = 'status-success';
  name = 'Show Success Status';
  private readonly provider: any;

  constructor(provider: any) {
    super();
    this.provider = provider;
  }

  invoke(): ICommandResult {
    this.provider.showStatus(
      {
        Message: '✅ Operation completed successfully!',
        State: MessageState.Success,
        Progress: undefined,
      },
      StatusContext.Page,
    );
    return CommandResult.keepOpen();
  }
}

class ShowWarningStatusCommand extends InvokableCommand {
  id = 'status-warning';
  name = 'Show Warning Status';
  private readonly provider: any;

  constructor(provider: any) {
    super();
    this.provider = provider;
  }

  invoke(): ICommandResult {
    this.provider.showStatus(
      {
        Message: '⚠️ Warning: This action cannot be undone',
        State: MessageState.Warning,
        Progress: undefined,
      },
      StatusContext.Page,
    );
    return CommandResult.keepOpen();
  }
}

class ShowErrorStatusCommand extends InvokableCommand {
  id = 'status-error';
  name = 'Show Error Status';
  private readonly provider: any;

  constructor(provider: any) {
    super();
    this.provider = provider;
  }

  invoke(): ICommandResult {
    this.provider.showStatus(
      {
        Message: '❌ An error occurred while processing your request',
        State: MessageState.Error,
        Progress: undefined,
      },
      StatusContext.Page,
    );
    return CommandResult.keepOpen();
  }
}

class ShowProgressStatusCommand extends InvokableCommand {
  id = 'status-progress';
  name = 'Show Progress Status';
  private readonly provider: any;

  constructor(provider: any) {
    super();
    this.provider = provider;
  }

  invoke(): ICommandResult {
    // Show an indeterminate progress status (Progress = undefined means indeterminate)
    this.provider.showStatus(
      {
        Message: '⏳ Processing... (indeterminate progress)',
        State: MessageState.Info,
        Progress: undefined,
      },
      StatusContext.Extension,
    );

    // Auto-hide after 3 seconds
    setTimeout(() => {
      this.provider.hideStatus({
        Message: '⏳ Processing... (indeterminate progress)',
        State: MessageState.Info,
        Progress: undefined,
      });
    }, 3000);

    return CommandResult.keepOpen();
  }
}

class LogMessageCommand extends InvokableCommand {
  id = 'status-log';
  name = 'Log Message';
  private readonly provider: any;

  constructor(provider: any) {
    super();
    this.provider = provider;
  }

  invoke(): ICommandResult {
    this.provider.log('This is a log message from the Status Messages page', MessageState.Info);
    return CommandResult.showToast('📋 Message logged — check CmdPal logs');
  }
}

class HideAllStatusCommand extends InvokableCommand {
  id = 'status-hide-all';
  name = 'Hide All Status Messages';
  private readonly provider: any;

  constructor(provider: any) {
    super();
    this.provider = provider;
  }

  invoke(): ICommandResult {
    // Hide by sending the same message structure
    for (const state of [MessageState.Info, MessageState.Success, MessageState.Warning, MessageState.Error]) {
      this.provider.hideStatus({
        Message: '',
        State: state,
        Progress: undefined,
      });
    }
    return CommandResult.showToast('Status messages hidden');
  }
}

// ---------------------------------------------------------------------------
// Status messages page
// ---------------------------------------------------------------------------

/**
 * Demonstrates status messages and logging:
 * - Info, Success, Warning, Error states
 * - Indeterminate progress
 * - Log messages to CmdPal logger
 * - ShowStatus/HideStatus lifecycle
 *
 * Similar to WinRT SampleStatusMessages.
 */
export class StatusMessagesPage extends ListPage {
  id = 'status-messages';
  name = 'Status Messages';
  placeholderText = 'Show different status types...';

  private _provider: any;

  constructor(provider: any) {
    super();
    this._provider = provider;
  }

  getItems(): IListItem[] {
    return [
      new ListItem({
        title: 'Info Status',
        subtitle: 'Show an informational status message (MessageState.Info)',
        icon: IconInfo.fromGlyph('ℹ️'),
        command: new ShowInfoStatusCommand(this._provider),
        tags: [
          tag({ text: 'Info', foreground: color(59, 130, 246) }),
        ],
      }),
      new ListItem({
        title: 'Success Status',
        subtitle: 'Show a success status message (MessageState.Success)',
        icon: IconInfo.fromGlyph('✅'),
        command: new ShowSuccessStatusCommand(this._provider),
        tags: [
          tag({ text: 'Success', foreground: color(22, 163, 74) }),
        ],
      }),
      new ListItem({
        title: 'Warning Status',
        subtitle: 'Show a warning status message (MessageState.Warning)',
        icon: IconInfo.fromGlyph('⚠️'),
        command: new ShowWarningStatusCommand(this._provider),
        tags: [
          tag({ text: 'Warning', foreground: color(245, 158, 11) }),
        ],
      }),
      new ListItem({
        title: 'Error Status',
        subtitle: 'Show an error status message (MessageState.Error)',
        icon: IconInfo.fromGlyph('❌'),
        command: new ShowErrorStatusCommand(this._provider),
        tags: [
          tag({ text: 'Error', foreground: color(239, 68, 68) }),
        ],
      }),
      new ListItem({
        title: 'Progress Status',
        subtitle: 'Show an indeterminate progress bar (auto-hides in 3s)',
        icon: IconInfo.fromGlyph('⏳'),
        command: new ShowProgressStatusCommand(this._provider),
        tags: [
          tag({ text: 'Progress', foreground: color(147, 51, 234) }),
        ],
      }),
      new ListItem({
        title: 'Log Message',
        subtitle: 'Send a log message to CmdPal logger',
        icon: IconInfo.fromGlyph('📋'),
        command: new LogMessageCommand(this._provider),
        tags: [
          tag({ text: 'Log', foreground: color(107, 114, 128) }),
        ],
      }),
      new ListItem({
        title: 'Hide All Status Messages',
        subtitle: 'Clear all active status messages',
        icon: IconInfo.fromGlyph('\uE711'),
        command: new HideAllStatusCommand(this._provider),
      }),
    ];
  }
}
