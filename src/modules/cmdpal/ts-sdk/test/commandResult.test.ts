// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import type { CommandResult, CommandResultKind } from '../src/types.js';
import {
  CommandResultKindValue,
  InvalidCommandResultError,
  kindToNumber,
  serializeCommandResult,
} from '../src/runtime/commandResult.js';

const expectedKindValues: Record<CommandResultKind, number> = {
  dismiss: 0,
  goHome: 1,
  goBack: 2,
  hide: 3,
  keepOpen: 4,
  goToPage: 5,
  showToast: 6,
  confirm: 7,
};

describe('CommandResult kind mapping', () => {
  it('maps every kind to its documented numeric value', () => {
    for (const [kind, value] of Object.entries(expectedKindValues)) {
      expect(kindToNumber(kind as CommandResultKind)).toBe(value);
      expect(CommandResultKindValue[kind as CommandResultKind]).toBe(value);
    }
  });

  it('defaults a missing result to Dismiss (0)', () => {
    expect(serializeCommandResult(undefined)).toEqual({ Kind: 0 });
    expect(serializeCommandResult(null)).toEqual({ Kind: 0 });
  });

  it('omits Args for argument-less kinds', () => {
    expect(serializeCommandResult({ kind: 'keepOpen' })).toEqual({ Kind: 4 });
    expect(serializeCommandResult({ kind: 'goBack' })).toEqual({ Kind: 2 });
  });

  it('serializes a toast message under the PascalCase wire key', () => {
    const result: CommandResult = { kind: 'showToast', args: { message: 'Done!' } };
    expect(serializeCommandResult(result)).toEqual({ Kind: 6, Args: { Message: 'Done!' } });
  });

  it('serializes goToPage arguments', () => {
    const result: CommandResult = {
      kind: 'goToPage',
      args: { pageId: 'settings', navigationMode: 'push' },
    };
    expect(serializeCommandResult(result)).toEqual({
      Kind: 5,
      Args: { PageId: 'settings', NavigationMode: 'push' },
    });
  });

  it('serializes each valid navigation mode', () => {
    for (const mode of ['push', 'goBack', 'goHome'] as const) {
      const result: CommandResult = {
        kind: 'goToPage',
        args: { pageId: 'p', navigationMode: mode },
      };
      expect(serializeCommandResult(result)).toEqual({
        Kind: 5,
        Args: { PageId: 'p', NavigationMode: mode },
      });
    }
  });

  it('serializes a nested toast result recursively', () => {
    const result: CommandResult = {
      kind: 'showToast',
      args: { message: 'Saved', result: { kind: 'goHome' } },
    };
    expect(serializeCommandResult(result)).toEqual({
      Kind: 6,
      Args: { Message: 'Saved', Result: { Kind: 1 } },
    });
  });

  it('serializes confirm arguments and its primary command', () => {
    const result: CommandResult = {
      kind: 'confirm',
      args: {
        title: 'Delete?',
        description: 'This cannot be undone.',
        isPrimaryCommandCritical: true,
        primaryCommand: { id: 'del', name: 'Delete' },
      },
    };
    const wire = serializeCommandResult(result, (command) => ({ id: command.id }));
    expect(wire).toEqual({
      Kind: 7,
      Args: {
        Title: 'Delete?',
        Description: 'This cannot be undone.',
        IsPrimaryCommandCritical: true,
        PrimaryCommand: { id: 'del' },
      },
    });
  });
});

describe('CommandResult validation of untyped shapes', () => {
  // These inputs come from plain JavaScript callers with no compile-time types,
  // so the serializer must reject them loudly rather than silently drop fields.
  it('rejects an unknown kind', () => {
    expect(() => serializeCommandResult({ kind: 'explode' } as unknown as CommandResult)).toThrow(
      InvalidCommandResultError,
    );
  });

  it('rejects foreign args on an argument-less kind', () => {
    expect(() =>
      serializeCommandResult({
        kind: 'goHome',
        args: { message: 'no' },
      } as unknown as CommandResult),
    ).toThrow(InvalidCommandResultError);
  });

  it('rejects goToPage without a pageId', () => {
    expect(() =>
      serializeCommandResult({ kind: 'goToPage', args: {} } as unknown as CommandResult),
    ).toThrow(InvalidCommandResultError);
  });

  it('rejects goToPage with an unknown navigation mode', () => {
    expect(() =>
      serializeCommandResult({
        kind: 'goToPage',
        args: { pageId: 'p', navigationMode: 'sideways' },
      } as unknown as CommandResult),
    ).toThrow(InvalidCommandResultError);
  });

  it('rejects showToast without a message', () => {
    expect(() =>
      serializeCommandResult({
        kind: 'showToast',
        args: { text: 'oops' },
      } as unknown as CommandResult),
    ).toThrow(InvalidCommandResultError);
  });

  it('rejects a nested toast continuation that is not a result', () => {
    expect(() =>
      serializeCommandResult({
        kind: 'showToast',
        args: { message: 'x', result: { foo: 1 } },
      } as unknown as CommandResult),
    ).toThrow(InvalidCommandResultError);
  });

  it('rejects confirm with a non-boolean critical flag', () => {
    expect(() =>
      serializeCommandResult({
        kind: 'confirm',
        args: { title: 't', description: 'd', isPrimaryCommandCritical: 'yes' },
      } as unknown as CommandResult),
    ).toThrow(InvalidCommandResultError);
  });
});
