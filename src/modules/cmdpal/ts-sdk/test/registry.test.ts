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
    expect(responseFor(sent, 2)?.result).toEqual({ Kind: 4 });

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
    expect(responseFor(sent, 4)?.result).toEqual({ Kind: 4 });

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
    expect((await invoke('cmd-1'))?.result).toEqual({ Kind: 4 });

    await getTopLevel();
    // The current generation resolves; the retired one is rejected.
    expect((await invoke('cmd-2'))?.result).toEqual({ Kind: 4 });
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
    expect((await invoke('fb-1'))?.result).toEqual({ Kind: 4 });

    await getFallbacks();
    expect((await invoke('fb-2'))?.result).toEqual({ Kind: 4 });
    expect((await invoke('fb-1'))?.error?.code).toBe(JsonRpcErrorCode.MethodNotFound);
  });
});
