// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it, vi } from 'vitest';
import type {
  CommandResult,
  IContentPage,
  ICommand,
  ICommandProvider,
  ICommandItem,
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
import { setNotificationSink } from '../src/runtime/notifications.js';
import { ListPageBase } from '../src/base/ListPageBase.js';
import { ContentPageBase } from '../src/base/ContentPageBase.js';
import { CommandItemBase } from '../src/base/CommandItemBase.js';

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

const providerWithCommandItem: ICommandProvider = {
  ...provider,
  getCommandItem(id) {
    return id === 'pinned'
      ? {
          command: { id: 'pinned', name: 'Pinned' },
          title: 'Pinned',
          subtitle: 'From anywhere',
          moreCommands: [],
        }
      : null;
  },
};

describe('ExtensionRuntime request dispatch', () => {
  it('answers initialize with the extension capabilities', async () => {
    const { runtime, sent } = createHarness();
    await runtime.setProvider(provider);

    await runtime.handleRequest({ jsonrpc: JSONRPC_VERSION, id: 1, method: 'initialize' });

    const initResult = responseFor(sent, 1)?.result as Record<string, unknown>;
    expect(initResult).toMatchObject({
      protocolVersion: 1,
      capabilities: ['commands'],
      provider: { id: 'ext', displayName: 'Ext', frozen: true },
    });
    expect(typeof initResult.sdkVersion).toBe('string');
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

    expect(responseFor(sent, 3)?.result).toEqual({ kind: 6, args: { message: 'hi' } });
  });

  it('returns a full command item by id', async () => {
    const { runtime, sent } = createHarness();
    await runtime.setProvider(providerWithCommandItem);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 8,
      method: 'provider/getCommandItem',
      params: { commandId: 'pinned' },
    });

    expect(responseFor(sent, 8)?.result).toEqual({
      id: 'pinned',
      title: 'Pinned',
      subtitle: 'From anywhere',
      command: { id: 'pinned', name: 'Pinned' },
    });
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

  it('applies filter selections to standard list pages', async () => {
    class FilterPage extends ListPageBase {
      readonly id = 'filtered';
      readonly name = 'Filtered';
      readonly title = 'Filtered';
      override filters = {
        currentFilterId: 'all',
        filters: [
          { id: 'all', name: 'All' },
          { id: 'active', name: 'Active' },
        ],
      };

      getItems() {
        return [];
      }
    }

    const page = new FilterPage();
    const { runtime } = createHarness();
    runtime.setProvider({
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [{ command: page, title: 'Filtered' }],
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'provider/getTopLevelCommands',
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'listPage/setFilter',
      params: { pageId: 'filtered', filterId: 'active' },
    });

    expect(page.filters.currentFilterId).toBe('active');
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
  it('sends the current typed property value for observable SDK models', () => {
    const notifications: Array<{ method: string; params: unknown }> = [];
    setNotificationSink((method, params) => notifications.push({ method, params }));

    class LoadingPage extends ListPageBase {
      readonly id = 'loading';
      readonly name = 'Loading';
      readonly title = 'Loading';
      override isLoading = false;

      getItems() {
        return [];
      }

      setLoading(value: boolean): void {
        this.isLoading = value;
        this.notifyPropChanged('isLoading');
      }
    }

    const page = new LoadingPage();
    page.setLoading(true);

    expect(notifications).toEqual([
      {
        method: 'command/propChanged',
        params: { commandId: 'loading', properties: { isLoading: true } },
      },
    ]);
    setNotificationSink(null);
  });

  it('keeps an item notification identity stable when its command changes', () => {
    const notifications: Array<{ method: string; params: unknown }> = [];
    setNotificationSink((method, params) => notifications.push({ method, params }));

    class MutableItem extends CommandItemBase {
      replaceCommand(command: ICommand): void {
        this.command = command;
        this.notifyPropChanged('command');
      }
    }

    const item = new MutableItem({
      command: { id: 'initial-command', name: 'Initial' },
      title: 'Item',
    });
    item.replaceCommand({ id: 'replacement-command', name: 'Replacement' });

    expect(notifications).toEqual([
      {
        method: 'command/propChanged',
        params: {
          commandId: 'initial-command',
          properties: {
            command: { id: 'replacement-command', name: 'Replacement' },
          },
        },
      },
    ]);
    setNotificationSink(null);
  });

  it('serializes and registers commands carried by property changes', async () => {
    class MutablePage extends ListPageBase {
      readonly id = 'mutable-page';
      readonly name = 'Mutable';
      readonly title = 'Mutable';

      getItems() {
        return [];
      }

      setEmptyContent(item: ICommandItem): void {
        this.emptyContent = item;
        this.notifyPropChanged('emptyContent');
      }
    }

    const initialChild: IContentPage = {
      id: 'initial-child',
      name: 'Initial child',
      title: 'Initial child',
      getContent: () => [],
    };
    const page = new MutablePage();
    page.emptyContent = { command: initialChild, title: 'Initial child' };
    const { runtime, sent } = createHarness();
    runtime.setProvider({
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [{ command: page, title: 'Mutable' }],
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'provider/getTopLevelCommands',
    });
    setNotificationSink((method, params) => runtime.sendSdkNotification(method, params));

    const updatedChild: IContentPage = {
      id: 'updated-child',
      name: 'Updated child',
      title: 'Updated child',
      getContent: () => [],
    };
    page.setEmptyContent({
      command: updatedChild,
      title: 'Updated child',
    });

    const changed = notificationsOf(sent, 'command/propChanged');
    expect(changed.at(-1)?.params).toEqual({
      commandId: 'mutable-page',
      properties: {
        emptyContent: {
          id: 'updated-child',
          title: 'Updated child',
          command: {
            id: 'updated-child',
            name: 'Updated child',
            pageType: 'contentPage',
            title: 'Updated child',
          },
        },
      },
    });

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'provider/getCommand',
      params: { commandId: 'updated-child' },
    });
    expect(responseFor(sent, 2)?.result).toMatchObject({
      id: 'updated-child',
      pageType: 'contentPage',
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 3,
      method: 'provider/getCommand',
      params: { commandId: 'initial-child' },
    });
    expect(responseFor(sent, 3)?.result).toBeNull();
    setNotificationSink(null);
  });

  it('keeps a command registered while another property still references it', async () => {
    const sharedCommand: IContentPage = {
      id: 'shared-child',
      name: 'Shared child',
      title: 'Shared child',
      getContent: () => [],
    };

    class MutableContentPage extends ContentPageBase {
      readonly id = 'content-owner';
      readonly name = 'Content owner';
      readonly title = 'Content owner';
      override commands = [{ command: sharedCommand, title: 'Shared child' }];
      override details = {
        metadata: [
          {
            key: 'Actions',
            data: { type: 'commands' as const, commands: [sharedCommand] },
          },
        ],
      };

      getContent() {
        return [];
      }

      clearCommands(): void {
        this.commands = [];
        this.notifyPropChanged('commands');
      }
    }

    const page = new MutableContentPage();
    const { runtime, sent } = createHarness();
    runtime.setProvider({
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [{ command: page, title: 'Content owner' }],
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'provider/getTopLevelCommands',
    });
    setNotificationSink((method, params) => runtime.sendSdkNotification(method, params));

    page.clearCommands();
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'provider/getCommand',
      params: { commandId: 'shared-child' },
    });

    expect(responseFor(sent, 2)?.result).toMatchObject({
      id: 'shared-child',
      pageType: 'contentPage',
    });
    setNotificationSink(null);
  });

  it('keeps a command registered while another owner still references it', async () => {
    const sharedCommand: IContentPage = {
      id: 'cross-owner-child',
      name: 'Cross owner child',
      title: 'Cross owner child',
      getContent: () => [],
    };

    class SharedOwnerPage extends ContentPageBase {
      readonly name = 'Shared owner';
      readonly title = 'Shared owner';
      override commands = [{ command: sharedCommand, title: 'Cross owner child' }];

      constructor(readonly id: string) {
        super();
      }

      getContent() {
        return [];
      }

      clearCommands(): void {
        this.commands = [];
        this.notifyPropChanged('commands');
      }
    }

    const first = new SharedOwnerPage('first-owner');
    const second = new SharedOwnerPage('second-owner');
    const { runtime, sent } = createHarness();
    runtime.setProvider({
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [
        { command: first, title: 'First owner' },
        { command: second, title: 'Second owner' },
      ],
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'provider/getTopLevelCommands',
    });
    setNotificationSink((method, params) => runtime.sendSdkNotification(method, params));

    first.clearCommands();
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'provider/getCommand',
      params: { commandId: 'cross-owner-child' },
    });

    expect(responseFor(sent, 2)?.result).toMatchObject({
      id: 'cross-owner-child',
      pageType: 'contentPage',
    });
    setNotificationSink(null);
  });

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
    expect(responseFor(sent, 1)?.result).toEqual({
      id: '__settings__',
      name: 'Settings',
      pageType: 'contentPage',
      title: 'Extension Settings',
      isLoading: false,
    });

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
    expect(responseFor(sent, 3)?.result).toEqual({ kind: 1 });
    expect(settings.getSetting<ToggleSetting>('dark')?.value).toBe(true);
  });
});

describe('ExtensionRuntime cache priming', () => {
  it('does not call provider factories during setProvider', async () => {
    const topLevel = vi.fn(() => []);
    const fallback = vi.fn(() => []);
    const lazyProvider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: topLevel,
      fallbackCommands: fallback,
    };
    const { runtime } = createHarness();

    await runtime.setProvider(lazyProvider);

    expect(topLevel).not.toHaveBeenCalled();
    expect(fallback).not.toHaveBeenCalled();
  });

  it('calls each factory once across the normal startup requests', async () => {
    const invoke = vi.fn((): CommandResult => ({ kind: 'dismiss' }));
    const command: IInvokableCommand = { id: 'run', name: 'Run', invoke };
    const topLevel = vi.fn(() => [{ command, title: 'Run' }]);
    const fallback = vi.fn(() => []);
    const countingProvider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: topLevel,
      fallbackCommands: fallback,
    };
    const { runtime } = createHarness();
    await runtime.setProvider(countingProvider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'provider/getTopLevelCommands',
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'provider/getFallbackCommands',
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 3,
      method: 'command/invoke',
      params: { commandId: 'run' },
    });

    expect(topLevel).toHaveBeenCalledTimes(1);
    expect(fallback).toHaveBeenCalledTimes(1);
    expect(invoke).toHaveBeenCalledTimes(1);
  });
});
