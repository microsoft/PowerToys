// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { IInvokableCommand, CommandResult, IconInfo } from '../types';

/**
 * Base class for commands that can be invoked (executed) by the user.
 *
 * @example
 * ```typescript
 * import { InvokableCommandBase } from '@microsoft/cmdpal-sdk';
 *
 * class CopyCommand extends InvokableCommandBase {
 *   id = 'copy-text';
 *   name = 'Copy to Clipboard';
 *
 *   async invoke() {
 *     // Perform the action
 *     return { kind: 'showToast', args: { message: 'Copied!' } };
 *   }
 * }
 * ```
 */
export abstract class InvokableCommandBase implements IInvokableCommand {
  abstract id: string;
  abstract name: string;

  icon?: IconInfo | null = null;

  abstract invoke(): Promise<CommandResult> | CommandResult;
}
