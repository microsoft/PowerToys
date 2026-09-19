// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it, vi } from 'vitest';
import type { ICommandProvider } from '../src/types.js';
import { ExtensionRuntime } from '../src/runtime/runtime.js';
import { JsonRpcErrorCode, JSONRPC_VERSION, type JsonRpcMessage } from '../src/runtime/jsonrpc.js';
import { PROTOCOL_VERSION } from '../src/runtime/protocol.js';

interface Harness {
  runtime: ExtensionRuntime;
  sent: JsonRpcMessage[];
  fatal: ReturnType<typeof vi.fn>;
}

function createHarness(): Harness {
  const sent: JsonRpcMessage[] = [];
  const fatal = vi.fn();
  const runtime = new ExtensionRuntime({
    send: (message) => sent.push(message),
    reportFatal: fatal,
  });
  return { runtime, sent, fatal };
}

function responseFor(sent: JsonRpcMessage[], id: number): Record<string, unknown> | undefined {
  return sent.find((m) => 'id' in m && (m as { id?: unknown }).id === id) as
    Record<string, unknown> | undefined;
}

const provider: ICommandProvider = {
  id: 'ext',
  displayName: 'Ext',
  topLevelCommands() {
    return [];
  },
};

describe('initialization failure propagation', () => {
  it('answers initialize with an error when provider creation rejects', async () => {
    const { runtime, sent, fatal } = createHarness();
    runtime.beginInitialization(Promise.reject(new Error('creation boom')));

    await runtime.handleRequest({ jsonrpc: JSONRPC_VERSION, id: 1, method: 'initialize' });

    const response = responseFor(sent, 1);
    expect(response?.error).toMatchObject({ message: 'creation boom' });
    expect(fatal).toHaveBeenCalledWith(1);
  });

  it('answers initialize with an error when initialization throws', async () => {
    const { runtime, sent, fatal } = createHarness();
    runtime.beginInitialization(
      (async (): Promise<ICommandProvider> => {
        throw new Error('init threw');
      })(),
    );

    await runtime.handleRequest({ jsonrpc: JSONRPC_VERSION, id: 1, method: 'initialize' });

    expect(responseFor(sent, 1)?.error).toMatchObject({ message: 'init threw' });
    expect(fatal).toHaveBeenCalledWith(1);
  });

  it('rejects later requests instead of serving a broken provider', async () => {
    const { runtime, sent } = createHarness();
    runtime.beginInitialization(Promise.reject(new Error('creation boom')));

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 2,
      method: 'command/invoke',
      params: { commandId: 'x' },
    });

    expect(responseFor(sent, 2)?.error).toMatchObject({ message: 'creation boom' });
  });
});

describe('handshake and version negotiation', () => {
  it('answers a compatible host with protocol and sdk versions', async () => {
    const { runtime, sent } = createHarness();
    runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'initialize',
      params: { protocolVersion: PROTOCOL_VERSION, hostVersion: '1.2.3' },
    });

    const result = responseFor(sent, 1)?.result as Record<string, unknown>;
    expect(result.protocolVersion).toBe(PROTOCOL_VERSION);
    expect(typeof result.sdkVersion).toBe('string');
    expect(result.capabilities).toEqual(['commands']);
    expect(runtime.negotiatedHostProtocolVersion).toBe(PROTOCOL_VERSION);
    expect(runtime.negotiatedHostVersion).toBe('1.2.3');
  });

  it('treats a missing host protocol version as a compatible legacy host', async () => {
    const { runtime, sent, fatal } = createHarness();
    runtime.setProvider(provider);

    await runtime.handleRequest({ jsonrpc: JSONRPC_VERSION, id: 1, method: 'initialize' });

    const response = responseFor(sent, 1);
    expect(response?.error).toBeUndefined();
    expect((response?.result as Record<string, unknown>).protocolVersion).toBe(PROTOCOL_VERSION);
    expect(runtime.negotiatedHostProtocolVersion).toBeUndefined();
    expect(fatal).not.toHaveBeenCalled();
  });

  it('rejects an incompatible major protocol version', async () => {
    const { runtime, sent, fatal } = createHarness();
    runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'initialize',
      params: { protocolVersion: PROTOCOL_VERSION + 1 },
    });

    const error = responseFor(sent, 1)?.error as { code: number; message: string } | undefined;
    expect(error?.code).toBe(JsonRpcErrorCode.InvalidRequest);
    expect(error?.message).toContain('Incompatible protocol version');
    expect(fatal).toHaveBeenCalledWith(1);
  });

  it('rejects a present-but-malformed protocol version distinctly from an absent one', async () => {
    const { runtime, sent, fatal } = createHarness();
    runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'initialize',
      params: { protocolVersion: 'not-a-number' },
    });

    const error = responseFor(sent, 1)?.error as { code: number; message: string } | undefined;
    expect(error?.code).toBe(JsonRpcErrorCode.InvalidRequest);
    expect(error?.message).toContain('Invalid protocol version');
    expect(error?.message).not.toContain('Incompatible protocol version');
    expect(fatal).toHaveBeenCalledWith(1);
  });

  it('rejects a non-integer numeric protocol version', async () => {
    const { runtime, sent, fatal } = createHarness();
    runtime.setProvider(provider);

    await runtime.handleRequest({
      jsonrpc: JSONRPC_VERSION,
      id: 1,
      method: 'initialize',
      params: { protocolVersion: 1.5 },
    });

    const error = responseFor(sent, 1)?.error as { code: number; message: string } | undefined;
    expect(error?.code).toBe(JsonRpcErrorCode.InvalidRequest);
    expect(error?.message).toContain('Invalid protocol version');
    expect(fatal).toHaveBeenCalledWith(1);
  });
});
