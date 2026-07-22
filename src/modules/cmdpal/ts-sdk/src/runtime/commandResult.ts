// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Serialization of {@link CommandResult} values to the numeric `Kind` / `Args`
 * wire shape described in `03-jsonrpc-protocol.md`.
 *
 * The serializer is strict: it validates that each result carries exactly the
 * arguments its kind requires and throws a descriptive error for malformed
 * shapes produced by untyped JavaScript callers, rather than silently dropping
 * fields. The emitted bytes are identical to the previous serializer for every
 * valid input.
 */

import type {
  CommandResult,
  CommandResultKind,
  ConfirmationArgs,
  GoToPageArgs,
  ICommand,
  ToastArgs,
} from '../types.js';

/** Numeric wire value for each {@link CommandResultKind}. */
export const CommandResultKindValue: Record<CommandResultKind, number> = {
  dismiss: 0,
  goHome: 1,
  goBack: 2,
  hide: 3,
  keepOpen: 4,
  goToPage: 5,
  showToast: 6,
  confirm: 7,
};

/** Maps a {@link CommandResultKind} to its numeric wire value. */
export function kindToNumber(kind: CommandResultKind): number {
  return CommandResultKindValue[kind];
}

/** Serialized wire form of a {@link CommandResult}. */
export interface WireCommandResult {
  Kind: number;
  Args?: unknown;
}

/** Serializes a command reference, used for nested result arguments. */
export type CommandSerializer = (command: ICommand) => unknown;

/** Error thrown when a {@link CommandResult} has an invalid shape. */
export class InvalidCommandResultError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'InvalidCommandResultError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function isCommand(value: unknown): value is ICommand {
  return isRecord(value) && typeof value.id === 'string' && typeof value.name === 'string';
}

function requireArgs(kind: CommandResultKind, result: CommandResult): Record<string, unknown> {
  const args = (result as { args?: unknown }).args;
  if (!isRecord(args)) {
    throw new InvalidCommandResultError(`Command result "${kind}" requires an args object.`);
  }
  return args;
}

function requireString(
  kind: CommandResultKind,
  args: Record<string, unknown>,
  key: string,
): string {
  const value = args[key];
  if (typeof value !== 'string') {
    throw new InvalidCommandResultError(
      `Command result "${kind}" requires a string "${key}" argument.`,
    );
  }
  return value;
}

function rejectForeignArgs(kind: CommandResultKind, result: CommandResult): void {
  if ((result as { args?: unknown }).args !== undefined) {
    throw new InvalidCommandResultError(`Command result "${kind}" does not accept args.`);
  }
}

function serializeGoToPage(args: Record<string, unknown>): Record<string, unknown> {
  const typed = args as Partial<GoToPageArgs>;
  const wire: Record<string, unknown> = { PageId: requireString('goToPage', args, 'pageId') };
  if (typed.navigationMode !== undefined) {
    wire.NavigationMode = requireString('goToPage', args, 'navigationMode');
  }
  return wire;
}

function serializeToast(
  args: Record<string, unknown>,
  serializeCommand: CommandSerializer,
): Record<string, unknown> {
  const typed = args as Partial<ToastArgs>;
  const wire: Record<string, unknown> = { Message: requireString('showToast', args, 'message') };
  if (typed.result !== undefined) {
    if (!isRecord(typed.result) || typeof (typed.result as CommandResult).kind !== 'string') {
      throw new InvalidCommandResultError(
        'Command result "showToast" requires a valid "result" continuation.',
      );
    }
    wire.Result = serializeCommandResult(typed.result, serializeCommand);
  }
  return wire;
}

function serializeConfirm(
  args: Record<string, unknown>,
  serializeCommand: CommandSerializer,
): Record<string, unknown> {
  const typed = args as Partial<ConfirmationArgs>;
  const wire: Record<string, unknown> = {
    Title: requireString('confirm', args, 'title'),
    Description: requireString('confirm', args, 'description'),
  };
  if (typed.isPrimaryCommandCritical !== undefined) {
    if (typeof typed.isPrimaryCommandCritical !== 'boolean') {
      throw new InvalidCommandResultError(
        'Command result "confirm" requires a boolean "isPrimaryCommandCritical" argument.',
      );
    }
    wire.IsPrimaryCommandCritical = typed.isPrimaryCommandCritical;
  }
  if (typed.primaryCommand !== undefined) {
    if (!isCommand(typed.primaryCommand)) {
      throw new InvalidCommandResultError(
        'Command result "confirm" requires a valid "primaryCommand" argument.',
      );
    }
    wire.PrimaryCommand = serializeCommand(typed.primaryCommand);
  }
  return wire;
}

const defaultCommandSerializer: CommandSerializer = (command) => ({
  id: command.id,
  name: command.name,
  icon: command.icon ?? undefined,
});

/**
 * Serializes a {@link CommandResult} to its `{ Kind, Args }` wire form. When a
 * result carries a nested command (a confirm dialog's primary command), the
 * optional {@link CommandSerializer} is used to serialize it; a minimal
 * serializer is used when none is supplied.
 *
 * @throws InvalidCommandResultError when the result's shape does not match its
 * kind, for example a `goToPage` result missing its `pageId`, or an argless
 * kind such as `goHome` carrying an `args` object.
 */
export function serializeCommandResult(
  result: CommandResult | null | undefined,
  serializeCommand: CommandSerializer = defaultCommandSerializer,
): WireCommandResult {
  if (!result) {
    return { Kind: CommandResultKindValue.dismiss };
  }

  const kind = (result as CommandResult).kind;
  if (typeof kind !== 'string' || !(kind in CommandResultKindValue)) {
    throw new InvalidCommandResultError(`Unknown command result kind: ${String(kind)}`);
  }

  switch (kind) {
    case 'dismiss':
    case 'goHome':
    case 'goBack':
    case 'hide':
    case 'keepOpen':
      rejectForeignArgs(kind, result);
      return { Kind: kindToNumber(kind) };
    case 'goToPage':
      return { Kind: kindToNumber(kind), Args: serializeGoToPage(requireArgs(kind, result)) };
    case 'showToast':
      return {
        Kind: kindToNumber(kind),
        Args: serializeToast(requireArgs(kind, result), serializeCommand),
      };
    case 'confirm':
      return {
        Kind: kindToNumber(kind),
        Args: serializeConfirm(requireArgs(kind, result), serializeCommand),
      };
    default:
      throw new InvalidCommandResultError(`Unknown command result kind: ${String(kind)}`);
  }
}
