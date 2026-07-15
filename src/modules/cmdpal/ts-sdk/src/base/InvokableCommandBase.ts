// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { CommandResult, IInvokableCommand, IconInfo } from '../types.js';

/**
 * Base class for a command that performs an action when invoked.
 *
 * @example
 * ```typescript
 * class GreetCommand extends InvokableCommandBase {
 *   readonly id = 'greet';
 *   readonly name = 'Say hello';
 *
 *   invoke(): CommandResult {
 *     return { kind: 'showToast', args: { message: 'Hello!' } };
 *   }
 * }
 * ```
 */
export abstract class InvokableCommandBase implements IInvokableCommand {
  abstract readonly id: string;
  abstract readonly name: string;

  icon?: IconInfo | null = null;

  abstract invoke(): CommandResult | Promise<CommandResult>;
}
