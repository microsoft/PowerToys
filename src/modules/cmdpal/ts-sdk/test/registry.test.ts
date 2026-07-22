// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import type { CommandResult, IListItem, IListPage, ICommandProvider } from '../src/types.js';
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
  return {
    command: {
      id,
      name: id,
      invoke(): CommandResult {
        return { kind: 'keepOpen' };
      },
    },
    title: id,
  };
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
});
