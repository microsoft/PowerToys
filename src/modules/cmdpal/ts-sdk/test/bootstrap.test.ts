// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { afterEach, describe, expect, it } from 'vitest';
import { bootstrap, resolveCliEntry } from '../src/runtime/bootstrap.js';
import { claimProtocolStdout } from '../src/runtime/stdio.js';
import { encodeMessage } from '../src/runtime/framing.js';

type Writer = typeof process.stdout.write;

const originalStdoutWrite = process.stdout.write;
const originalStderrWrite = process.stderr.write;

afterEach(() => {
  process.stdout.write = originalStdoutWrite;
  process.stderr.write = originalStderrWrite;
});

function captureStreams(): { out: string[]; err: string[] } {
  const out: string[] = [];
  const err: string[] = [];
  process.stdout.write = ((chunk: unknown): boolean => {
    out.push(typeof chunk === 'string' ? chunk : String(chunk));
    return true;
  }) as unknown as Writer;
  process.stderr.write = ((chunk: unknown): boolean => {
    err.push(typeof chunk === 'string' ? chunk : String(chunk));
    return true;
  }) as unknown as Writer;
  return { out, err };
}

describe('bootstrap loader', () => {
  it('claims stdout before importing the entry so top-level writes cannot corrupt framing', async () => {
    const { out, err } = captureStreams();
    const entry = new URL('./fixtures/noisy-entry.ts', import.meta.url).href;

    const module = (await bootstrap(entry)) as { loaded?: boolean };
    // The claim installed by bootstrap is still active; take the handle so we
    // can write a protocol frame and then restore the streams.
    const stdout = claimProtocolStdout();
    try {
      stdout.writeRaw(encodeMessage({ jsonrpc: '2.0', id: 7, result: { ok: true } }));

      const stdoutBytes = out.join('');
      // The entry's top-level raw stdout write must not reach the protocol
      // stream; only the framed message should be present.
      expect(stdoutBytes).not.toContain('top-level-stdout-write');
      expect(stdoutBytes.startsWith('Content-Length:')).toBe(true);
      expect(stdoutBytes).toContain('"id":7');
      // The redirected raw write is preserved on stderr.
      expect(err.join('')).toContain('top-level-stdout-write');
      expect(module.loaded).toBe(true);
    } finally {
      stdout.restore();
    }
  });
});

describe('resolveCliEntry', () => {
  it('returns null when no entry is provided', () => {
    expect(resolveCliEntry(['node', 'bootstrap.js'], {})).toBeNull();
  });

  it('reads the entry from the environment when no argument is present', () => {
    const resolved = resolveCliEntry(['node', 'bootstrap.js'], {
      CMDPAL_EXTENSION_ENTRY: 'file:///abs/entry.js',
    });
    expect(resolved).toBe('file:///abs/entry.js');
  });

  it('passes a URL specifier through unchanged', () => {
    expect(resolveCliEntry(['node', 'bootstrap.js', 'file:///abs/entry.js'], {})).toBe(
      'file:///abs/entry.js',
    );
  });

  it('converts a bare filesystem path to a file URL', () => {
    const resolved = resolveCliEntry(['node', 'bootstrap.js', '/abs/entry.js'], {});
    expect(resolved?.startsWith('file://')).toBe(true);
    expect(resolved).toContain('/abs/entry.js');
  });
});
