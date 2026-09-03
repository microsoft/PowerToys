// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import type {
  CommandResult,
  IInvokableCommand,
  IListItem,
  IListPage,
  ICommandProvider,
} from '../src/types.js';
import { ExtensionRuntime } from '../src/runtime/runtime.js';
import {
  JSONRPC_VERSION,
  JsonRpcErrorCode,
  type JsonRpcMessage,
  type JsonRpcResponse,
} from '../src/runtime/jsonrpc.js';

function createHarness(): { runtime: ExtensionRuntime; sent: JsonRpcMessage[] } {
  const sent: JsonRpcMessage[] = [];
  const runtime = new ExtensionRuntime({ send: (message) => sent.push(message) });
  return { runtime, sent };
}

function responseFor(sent: JsonRpcMessage[], id: number): JsonRpcResponse | undefined {
  return sent.find(
    (message): message is JsonRpcResponse =>
      'id' in message && (message as JsonRpcResponse).id === id,
  );
}

function item(id: string): IListItem {
  const command: IInvokableCommand = {
    id,
    name: id,
    invoke(): CommandResult {
      return { kind: 'keepOpen' };
    },
  };
  return { command, title: id };
}

describe('bounded command registry eviction', () => {
  it('retires page-scoped commands that disappear after a refresh', async () => {
    let generation = 0;
    const page: IListPage = {
      id: 'list',
      name: 'List',
      title: 'List',
      getItems(): IListItem[] {
        generation += 1;
        return generation === 1 ? [item('cmd-a')] : [item('cmd-b')];
      },
    };
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        return [{ command: page, title: 'List' }];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(provider);

    // First fetch registers cmd-a; invoking it succeeds.
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'listPage/getItems',
      params: { pageId: 'list' },
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'command/invoke',
      params: { commandId: 'cmd-a' },
    });
    expect(responseFor(sent, 2)?.result).toEqual({ kind: 4 });

    // Refresh replaces the item set; cmd-a is no longer present.
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 3,
      method: 'listPage/getItems',
      params: { pageId: 'list' },
    });

    // cmd-b resolves...
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 4,
      method: 'command/invoke',
      params: { commandId: 'cmd-b' },
    });
    expect(responseFor(sent, 4)?.result).toEqual({ kind: 4 });

    // ...but the retired cmd-a is rejected with a protocol error.
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 5,
      method: 'command/invoke',
      params: { commandId: 'cmd-a' },
    });
    expect(responseFor(sent, 5)?.error?.code).toBe(JsonRpcErrorCode.MethodNotFound);
  });

  it('retires provider-level commands that disappear across top-level refreshes', async () => {
    let generation = 0;
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        generation += 1;
        return [{ command: item(`cmd-${String(generation)}`).command, title: 'C' }];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(provider);

    let messageId = 0;
    const getTopLevel = async (): Promise<void> => {
      messageId += 1;
      await runtime.handleRequest({
        jsonrpc: JSONRPC_VERSION,
        id: messageId,
        method: 'provider/getTopLevelCommands',
      });
    };
    const invoke = async (commandId: string): Promise<JsonRpcResponse | undefined> => {
      messageId += 1;
      const thisId = messageId;
      await runtime.handleRequest({
        jsonrpc: JSONRPC_VERSION,
        id: thisId,
        method: 'command/invoke',
        params: { commandId },
      });
      return responseFor(sent, thisId);
    };

    // Walk several generations; each refresh must retire the prior id.
    await getTopLevel();
    expect((await invoke('cmd-1'))?.result).toEqual({ kind: 4 });

    await getTopLevel();
    // The current generation resolves; the retired one is rejected.
    expect((await invoke('cmd-2'))?.result).toEqual({ kind: 4 });
    expect((await invoke('cmd-1'))?.error?.code).toBe(JsonRpcErrorCode.MethodNotFound);
  });

  it('retires fallback commands that disappear across fallback refreshes', async () => {
    let generation = 0;
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        return [];
      },
      fallbackCommands() {
        generation += 1;
        const command = item(`fb-${String(generation)}`).command;
        return [{ command, title: 'Fallback' }];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(provider);

    let messageId = 0;
    const getFallbacks = async (): Promise<void> => {
      messageId += 1;
      await runtime.handleRequest({
        jsonrpc: JSONRPC_VERSION,
        id: messageId,
        method: 'provider/getFallbackCommands',
      });
    };
    const invoke = async (commandId: string): Promise<JsonRpcResponse | undefined> => {
      messageId += 1;
      const thisId = messageId;
      await runtime.handleRequest({
        jsonrpc: JSONRPC_VERSION,
        id: thisId,
        method: 'command/invoke',
        params: { commandId },
      });
      return responseFor(sent, thisId);
    };

    await getFallbacks();
    expect((await invoke('fb-1'))?.result).toEqual({ kind: 4 });

    await getFallbacks();
    expect((await invoke('fb-2'))?.result).toEqual({ kind: 4 });
    expect((await invoke('fb-1'))?.error?.code).toBe(JsonRpcErrorCode.MethodNotFound);
  });

  it('retires fallback commands when a refresh returns null', async () => {
    let includeFallback = true;
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        return [];
      },
      fallbackCommands() {
        return includeFallback ? [{ command: item('fb').command, title: 'Fallback' }] : null;
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'provider/getFallbackCommands',
    });
    includeFallback = false;
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'provider/getFallbackCommands',
    });
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 3,
      method: 'command/invoke',
      params: { commandId: 'fb' },
    });

    expect(responseFor(sent, 3)?.error?.code).toBe(JsonRpcErrorCode.MethodNotFound);
  });
});

describe('recursive scope retirement', () => {
  it('recursively retires child-page command scopes when the owner page is retired', async () => {
    const childPage: IListPage = {
      id: 'child',
      name: 'Child',
      title: 'Child',
      getItems(): IListItem[] {
        return [item('grandchild-cmd')];
      },
    };
    const parentPage: IListPage = {
      id: 'parent',
      name: 'Parent',
      title: 'Parent',
      getItems(): IListItem[] {
        return [{ command: childPage, title: 'Child' }];
      },
    };
    let generation = 0;
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        generation += 1;
        // The parent page is present on the first refresh and gone afterwards.
        return generation === 1 ? [{ command: parentPage, title: 'Parent' }] : [];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(provider);

    let messageId = 0;
    const request = async (method: string, params?: Record<string, unknown>): Promise<void> => {
      messageId += 1;
      await runtime.handleRequest({ jsonrpc: JSONRPC_VERSION, id: messageId, method, params });
    };
    const invoke = async (commandId: string): Promise<JsonRpcResponse | undefined> => {
      messageId += 1;
      const thisId = messageId;
      await runtime.handleRequest({
        jsonrpc: JSONRPC_VERSION,
        id: thisId,
        method: 'command/invoke',
        params: { commandId },
      });
      return responseFor(sent, thisId);
    };

    // Register the parent page, then walk two levels deep so the grandchild
    // command lands in the child page's scope.
    await request('provider/getTopLevelCommands');
    await request('listPage/getItems', { pageId: 'parent' });
    await request('listPage/getItems', { pageId: 'child' });
    expect((await invoke('grandchild-cmd'))?.result).toEqual({ kind: 4 });

    // Refreshing top-level retires the parent page, which must recursively
    // retire the child page scope and the grandchild command nested inside it.
    await request('provider/getTopLevelCommands');
    expect((await invoke('grandchild-cmd'))?.error?.code).toBe(JsonRpcErrorCode.MethodNotFound);
  });

  it('retires nested result commands when the command that produced them is retired', async () => {
    const nested: IInvokableCommand = {
      id: 'nested-primary',
      name: 'Nested primary',
      invoke(): CommandResult {
        return { kind: 'keepOpen' };
      },
    };
    const opener: IInvokableCommand = {
      id: 'opener',
      name: 'Opener',
      invoke(): CommandResult {
        return {
          kind: 'confirm',
          args: { title: 'Confirm', description: 'Proceed?', primaryCommand: nested },
        };
      },
    };
    let generation = 0;
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands() {
        generation += 1;
        return generation === 1 ? [{ command: opener, title: 'Opener' }] : [];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(provider);

    let messageId = 0;
    const getTopLevel = async (): Promise<void> => {
      messageId += 1;
      await runtime.handleRequest({
        jsonrpc: JSONRPC_VERSION,
        id: messageId,
        method: 'provider/getTopLevelCommands',
      });
    };
    const invoke = async (commandId: string): Promise<JsonRpcResponse | undefined> => {
      messageId += 1;
      const thisId = messageId;
      await runtime.handleRequest({
        jsonrpc: JSONRPC_VERSION,
        id: thisId,
        method: 'command/invoke',
        params: { commandId },
      });
      return responseFor(sent, thisId);
    };

    // Invoking the opener registers its confirm dialog's primary command, which
    // is then invocable on its own.
    await getTopLevel();
    await invoke('opener');
    expect((await invoke('nested-primary'))?.result).toEqual({ kind: 4 });

    // Retiring the opener across a refresh must retire the nested command too.
    await getTopLevel();
    expect((await invoke('nested-primary'))?.error?.code).toBe(JsonRpcErrorCode.MethodNotFound);
  });
});
