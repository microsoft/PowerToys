// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  InvokableCommand,
  CommandResult,
  ICommandResult,
} from '@cmdpal/sdk';

/**
 * Command that shows a toast notification.
 */
export class ToastDemoCommand extends InvokableCommand {
  id = 'toast-demo';
  name = 'Show Toast';

  invoke(): ICommandResult {
    return CommandResult.showToast('🎉 Hello from the TypeScript SDK!');
  }
}

/**
 * Command that shows a confirmation dialog, then a toast on confirm.
 */
export class ConfirmDemoCommand extends InvokableCommand {
  id = 'confirm-demo';
  name = 'Confirm Action';

  invoke(): ICommandResult {
    const onConfirm = new ToastDemoCommand();
    return CommandResult.confirm(
      'Confirm Action',
      'Are you sure you want to proceed? This will show a toast notification.',
      onConfirm,
    );
  }
}
