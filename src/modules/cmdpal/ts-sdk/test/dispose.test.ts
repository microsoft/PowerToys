// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it, vi } from 'vitest';
import type { ICommandProvider } from '../src/types.js';
import { ExtensionRuntime } from '../src/runtime/runtime.js';

function createRuntime(): { runtime: ExtensionRuntime; onDispose: ReturnType<typeof vi.fn> } {
  const onDispose = vi.fn();
  const runtime = new ExtensionRuntime({ send: () => {}, onDispose });
  return { runtime, onDispose };
}

describe('graceful async disposal', () => {
  it('awaits an asynchronous provider disposal before completing', async () => {
    let disposed = false;
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [],
      async dispose(): Promise<void> {
        await new Promise((resolve) => setTimeout(resolve, 10));
        disposed = true;
      },
    };
    const { runtime, onDispose } = createRuntime();
    runtime.setProvider(provider);

    await runtime.dispose();

    expect(disposed).toBe(true);
    expect(onDispose).toHaveBeenCalledTimes(1);
    expect(runtime.isDisposed).toBe(true);
  });

  it('supports a synchronous provider disposal', async () => {
    const dispose = vi.fn();
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [],
      dispose,
    };
    const { runtime } = createRuntime();
    runtime.setProvider(provider);

    await runtime.dispose();

    expect(dispose).toHaveBeenCalledTimes(1);
  });

  it('does not hang when provider disposal never settles, honoring the timeout', async () => {
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [],
      dispose: () => new Promise<void>(() => {}),
    };
    const { runtime, onDispose } = createRuntime();
    runtime.setProvider(provider);

    const start = Date.now();
    await runtime.dispose(20);
    const elapsed = Date.now() - start;

    expect(elapsed).toBeLessThan(1000);
    expect(onDispose).toHaveBeenCalledTimes(1);
    expect(runtime.isDisposed).toBe(true);
  });

  it('disposes immediately without awaiting when the timeout is zero or negative', async () => {
    let settled = false;
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [],
      // Never settles: a zero timeout must not await this.
      dispose: () =>
        new Promise<void>((resolve) => {
          setTimeout(() => {
            settled = true;
            resolve();
          }, 10_000).unref?.();
        }),
    };
    const { runtime, onDispose } = createRuntime();
    runtime.setProvider(provider);

    const start = Date.now();
    await runtime.dispose(0);
    const elapsed = Date.now() - start;

    // Resolved immediately, before the provider's disposal could settle.
    expect(elapsed).toBeLessThan(1000);
    expect(settled).toBe(false);
    expect(onDispose).toHaveBeenCalledTimes(1);
    expect(runtime.isDisposed).toBe(true);
  });

  it('treats a negative timeout as immediate disposal', async () => {
    const provider: ICommandProvider = {
      id: 'ext',
      displayName: 'Ext',
      topLevelCommands: () => [],
      dispose: () => new Promise<void>(() => {}),
    };
    const { runtime, onDispose } = createRuntime();
    runtime.setProvider(provider);

    const start = Date.now();
    await runtime.dispose(-1);
    const elapsed = Date.now() - start;

    expect(elapsed).toBeLessThan(1000);
    expect(onDispose).toHaveBeenCalledTimes(1);
    expect(runtime.isDisposed).toBe(true);
  });
});
