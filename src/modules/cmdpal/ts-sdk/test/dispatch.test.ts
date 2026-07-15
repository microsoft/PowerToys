// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import type {
  CommandResult,
  ICommandProvider,
  IFallbackCommandItem,
  IInvokableCommand,
  IListPage,
} from '../src/types.js';
import { ExtensionRuntime } from '../src/runtime/runtime.js';
import {
  JSONRPC_VERSION,
  type JsonRpcMessage,
  type JsonRpcNotification,
  type JsonRpcResponse,
} from '../src/runtime/jsonrpc.js';
import { Settings, ToggleSetting } from '../src/index.js';

interface Harness {
  runtime: ExtensionRuntime;
  sent: JsonRpcMessage[];
  isDisposed: () => boolean;
}

function createHarness(): Harness {
  const sent: JsonRpcMessage[] = [];
  let disposed = false;
  const runtime = new ExtensionRuntime({
    send: (message) => sent.push(message),
    onDispose: () => {
      disposed = true;
    },
  });
  return { runtime, sent, isDisposed: () => disposed };
}

function responseFor(sent: JsonRpcMessage[], id: number): JsonRpcResponse | undefined {
  return sent.find(
    (message): message is JsonRpcResponse =>
      'id' in message && (message as JsonRpcResponse).id === id,
  );
}

function notificationsOf(sent: JsonRpcMessage[], method: string): JsonRpcNotification[] {
  return sent.filter(
    (message): message is JsonRpcNotification =>
      !('id' in message) && (message as JsonRpcNotification).method === method,
  );
}

const invokable: IInvokableCommand = {
  id: 'greet',
  name: 'Greet',
  invoke(): CommandResult {
    return { kind: 'showToast', args: { message: 'hi' } };
  },
};

const listPage: IListPage = {
  id: 'list',
  name: 'List',
  title: 'List',
  getItems() {
    return [{ command: { id: 'item-cmd', name: 'Item' }, title: 'Item One' }];
  },
};

const provider: ICommandProvider = {
  id: 'ext',
  displayName: 'Ext',
  topLevelCommands() {
    return [
      { command: invokable, title: 'Greet' },
      { command: listPage, title: 'Open list' },
    ];
  },
};

describe('ExtensionRuntime request dispatch', () => {
  it('answers initialize with the extension capabilities', async () => {
    const { runtime, sent } = createHarness();
    await runtime.setProvider(provider);

    await runtime.handleRequest({ jsonrpc: JSONRPC_VERSION, id: 1, method: 'initialize' });

    expect(responseFor(sent, 1)?.result).toEqual({ capabilities: ['commands'] });
  });

  it('serializes top-level commands with a pageType for pages', async () => {
    const { runtime, sent } = createHarness();
    await runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'provider/getTopLevelCommands',
    });

    const items = responseFor(sent, 2)?.result as Array<Record<string, unknown>>;
    expect(items).toHaveLength(2);
    expect(items[0]?.command).toMatchObject({ id: 'greet', name: 'Greet' });
    expect(items[0]?.command).not.toHaveProperty('pageType');
    expect(items[1]?.command).toMatchObject({ pageType: 'listPage', title: 'List' });
  });

  it('invokes a cached command and serializes its result', async () => {
    const { runtime, sent } = createHarness();
    await runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 3,
      method: 'command/invoke',
      params: { commandId: 'greet' },
    });

    expect(responseFor(sent, 3)?.result).toEqual({ Kind: 6, Args: { Message: 'hi' } });
  });

  it('returns list page items', async () => {
    const { runtime, sent } = createHarness();
    await runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 4,
      method: 'listPage/getItems',
      params: { pageId: 'list' },
    });

    const result = responseFor(sent, 4)?.result as { items: Array<Record<string, unknown>> };
    expect(result.items).toHaveLength(1);
    expect(result.items[0]).toMatchObject({ id: 'item-cmd', title: 'Item One' });
  });

  it('reports method not found for unknown methods', async () => {
    const { runtime, sent } = createHarness();
    await runtime.setProvider(provider);

    await runtime.handleRequest({ jsonrpc: JSONRPC_VERSION, id: 5, method: 'does/notExist' });

    expect(responseFor(sent, 5)?.error?.code).toBe(-32601);
  });

  it('reports an internal error when no provider is set', async () => {
    const { runtime, sent } = createHarness();

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 6,
      method: 'provider/getTopLevelCommands',
    });

    expect(responseFor(sent, 6)?.error?.code).toBe(-32603);
  });

  it('propagates errors thrown by a command as a JSON-RPC error', async () => {
    const failing: IInvokableCommand = {
      id: 'boom',
      name: 'Boom',
      invoke(): CommandResult {
        throw new Error('kaboom');
      },
    };
    const failingProvider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        return [{ command: failing, title: 'Boom' }];
      },
    };
    const { runtime, sent } = createHarness();
    await runtime.setProvider(failingProvider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 7,
      method: 'command/invoke',
      params: { commandId: 'boom' },
    });

    const error = responseFor(sent, 7)?.error;
    expect(error?.code).toBe(-32603);
    expect(error?.message).toBe('kaboom');
  });
});

describe('ExtensionRuntime notification dispatch', () => {
  it('disposes the runtime and the provider on a dispose notification', async () => {
    let providerDisposed = false;
    const disposableProvider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        return [];
      },
      dispose() {
        providerDisposed = true;
      },
    };
    const harness = createHarness();
    await harness.runtime.setProvider(disposableProvider);

    await harness.runtime.handleNotification({ jsonrpc: JSONRPC_VERSION, method: 'dispose' });

    expect(harness.runtime.isDisposed).toBe(true);
    expect(harness.isDisposed()).toBe(true);
    expect(providerDisposed).toBe(true);
  });

  it('updates a fallback query and emits command/propChanged', async () => {
    const fallbackItem: IFallbackCommandItem = {
      command: { id: 'fb', name: 'Fallback' },
      title: 'Fallback',
      displayTitle: 'Fallback',
      fallbackHandler: {
        updateQuery(query: string): void {
          fallbackItem.displayTitle = `Search: ${query}`;
        },
      },
    };
    const fallbackProvider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        return [];
      },
      fallbackCommands() {
        return [fallbackItem];
      },
    };
    const { runtime, sent } = createHarness();
    await runtime.setProvider(fallbackProvider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'fallback/updateQuery',
      params: { commandId: 'fb', query: 'abc' },
    });

    expect(responseFor(sent, 1)?.result).toBeNull();
    const changed = notificationsOf(sent, 'command/propChanged');
    expect(changed).toHaveLength(1);
    expect(changed[0]?.params).toEqual({
      commandId: 'fb',
      properties: { displayTitle: 'Search: abc' },
    });
  });
});

describe('ExtensionRuntime settings integration', () => {
  it('exposes settings, serves the form, and applies a submission', async () => {
    const settings = new Settings();
    settings.add(new ToggleSetting('dark', 'Dark Mode', false));
    const settingsProvider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      settings,
      topLevelCommands() {
        return [];
      },
    };
    const { runtime, sent } = createHarness();
    await runtime.setProvider(settingsProvider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'provider/getSettings',
    });
    expect(responseFor(sent, 1)?.result).toEqual({ id: '__settings__' });

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'contentPage/getContent',
      params: { pageId: '__settings__' },
    });
    const content = responseFor(sent, 2)?.result as Array<Record<string, unknown>>;
    expect(content[0]?.type).toBe('form');

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 3,
      method: 'form/submit',
      params: { pageId: '__settings__', inputs: JSON.stringify({ dark: 'true' }), data: '{}' },
    });
    expect(responseFor(sent, 3)?.result).toEqual({ Kind: 6, Args: { Message: 'Settings saved' } });
    expect(settings.getSetting<ToggleSetting>('dark')?.value).toBe(true);
  });
});
