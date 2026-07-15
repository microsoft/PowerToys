// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Serialization of {@link CommandResult} values to the numeric `Kind` / `Args`
 * wire shape described in `03-jsonrpc-protocol.md`.
 */

import type { CommandResult, CommandResultArgs, CommandResultKind, ICommand } from '../types.js';

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

function readString(args: CommandResultArgs, key: string): string | undefined {
  const value = args[key];
  return typeof value === 'string' ? value : undefined;
}

function readBoolean(args: CommandResultArgs, key: string): boolean | undefined {
  const value = args[key];
  return typeof value === 'boolean' ? value : undefined;
}

function isCommand(value: unknown): value is ICommand {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as ICommand).id === 'string' &&
    typeof (value as ICommand).name === 'string'
  );
}

function isCommandResult(value: unknown): value is CommandResult {
  return (
    typeof value === 'object' && value !== null && typeof (value as CommandResult).kind === 'string'
  );
}

function assign(target: Record<string, unknown>, key: string, value: unknown): void {
  if (value !== undefined) {
    target[key] = value;
  }
}

function serializeArgs(
  kind: CommandResultKind,
  args: CommandResultArgs,
  serializeCommand: CommandSerializer,
): Record<string, unknown> {
  const wire: Record<string, unknown> = {};
  switch (kind) {
    case 'goToPage':
      assign(wire, 'PageId', readString(args, 'pageId'));
      assign(wire, 'NavigationMode', readString(args, 'navigationMode'));
      break;
    case 'showToast': {
      assign(wire, 'Message', readString(args, 'message'));
      const nested = args.result;
      if (isCommandResult(nested)) {
        wire.Result = serializeCommandResult(nested, serializeCommand);
      }
      break;
    }
    case 'confirm': {
      assign(wire, 'Title', readString(args, 'title'));
      assign(wire, 'Description', readString(args, 'description'));
      assign(wire, 'IsPrimaryCommandCritical', readBoolean(args, 'isPrimaryCommandCritical'));
      const primary = args.primaryCommand;
      if (isCommand(primary)) {
        wire.PrimaryCommand = serializeCommand(primary);
      }
      break;
    }
    default:
      break;
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
 */
export function serializeCommandResult(
  result: CommandResult | null | undefined,
  serializeCommand: CommandSerializer = defaultCommandSerializer,
): WireCommandResult {
  if (!result) {
    return { Kind: CommandResultKindValue.dismiss };
  }

  const wire: WireCommandResult = { Kind: kindToNumber(result.kind) };
  if (result.args) {
    const args = serializeArgs(result.kind, result.args, serializeCommand);
    if (Object.keys(args).length > 0) {
      wire.Args = args;
    }
  }
  return wire;
}
