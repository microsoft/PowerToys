// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it, vi } from 'vitest';
import type { CommandResult, Content, IContentPage, ICommandProvider } from '../src/types.js';
import { ExtensionRuntime } from '../src/runtime/runtime.js';
import {
  JSONRPC_VERSION,
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

function providerWith(page: IContentPage): ICommandProvider {
  return {
    id: 'ext',
    displayName: 'Ext',
    topLevelCommands() {
      return [{ command: page, title: page.title ?? page.name }];
    },
  };
}

function formContent(formId: string | undefined, submitForm: () => CommandResult): Content {
  return {
    type: 'form',
    formId,
    templateJson: '{}',
    dataJson: '{}',
    submitForm,
  };
}

describe('form identity and routing', () => {
  it('routes submissions to the handler captured for each formId', async () => {
    const first = vi.fn((): CommandResult => ({ kind: 'goHome' }));
    const second = vi.fn((): CommandResult => ({ kind: 'goBack' }));
    const page: IContentPage = {
      id: 'page',
      name: 'Page',
      title: 'Page',
      getContent(): Content[] {
        return [formContent('first', first), formContent('second', second)];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(providerWith(page));

    // Serialize the page so both forms register.
    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'contentPage/getContent',
      params: { pageId: 'page' },
    });
    const content = responseFor(sent, 1)?.result as Array<Record<string, unknown>>;
    expect(content.map((c) => c.formId)).toEqual(['first', 'second']);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'form/submit',
      params: { pageId: 'page', formId: 'second', inputs: '{}', data: '{}' },
    });

    expect(second).toHaveBeenCalledTimes(1);
    expect(first).not.toHaveBeenCalled();
    expect(responseFor(sent, 2)?.result).toEqual({ Kind: 2 });
  });

  it('registers nested forms inside tree content', async () => {
    const nested = vi.fn((): CommandResult => ({ kind: 'hide' }));
    const page: IContentPage = {
      id: 'page',
      name: 'Page',
      title: 'Page',
      getContent(): Content[] {
        return [
          {
            type: 'tree',
            rootContent: { type: 'markdown', body: 'root' },
            getChildren(): Content[] {
              return [formContent('nested', nested)];
            },
          },
        ];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(providerWith(page));

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'contentPage/getContent',
      params: { pageId: 'page' },
    });

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'form/submit',
      params: { pageId: 'page', formId: 'nested', inputs: '{}', data: '{}' },
    });

    expect(nested).toHaveBeenCalledTimes(1);
    expect(responseFor(sent, 2)?.result).toEqual({ Kind: 3 });
  });

  it('falls back to the first form when the host omits a formId', async () => {
    const first = vi.fn((): CommandResult => ({ kind: 'goHome' }));
    const second = vi.fn((): CommandResult => ({ kind: 'goBack' }));
    const page: IContentPage = {
      id: 'page',
      name: 'Page',
      title: 'Page',
      getContent(): Content[] {
        return [formContent('first', first), formContent('second', second)];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(providerWith(page));

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'form/submit',
      params: { pageId: 'page', inputs: '{}', data: '{}' },
    });

    expect(first).toHaveBeenCalledTimes(1);
    expect(second).not.toHaveBeenCalled();
    expect(responseFor(sent, 1)?.result).toEqual({ Kind: 1 });
  });

  it('assigns a deterministic formId when the author omits one', async () => {
    const page: IContentPage = {
      id: 'page',
      name: 'Page',
      title: 'Page',
      getContent(): Content[] {
        return [formContent(undefined, () => ({ kind: 'goHome' }))];
      },
    };
    const { runtime, sent } = createHarness();
    runtime.setProvider(providerWith(page));

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'contentPage/getContent',
      params: { pageId: 'page' },
    });

    const content = responseFor(sent, 1)?.result as Array<Record<string, unknown>>;
    expect(typeof content[0]?.formId).toBe('string');
    expect((content[0]?.formId as string).length).toBeGreaterThan(0);
  });
});
