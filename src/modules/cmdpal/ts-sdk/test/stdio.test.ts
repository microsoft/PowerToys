// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { afterEach, describe, expect, it, vi } from 'vitest';
import { claimProtocolStdout } from '../src/runtime/stdio.js';
import { encodeMessage } from '../src/runtime/framing.js';

type Writer = typeof process.stdout.write;

const originalStdoutWrite = process.stdout.write;
const originalStderrWrite = process.stderr.write;

afterEach(() => {
  process.stdout.write = originalStdoutWrite;
  process.stderr.write = originalStderrWrite;
});

/** Redirects the process streams to string sinks so routing can be observed. */
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

describe('claimProtocolStdout', () => {
  it('redirects console logging and direct stdout writes to stderr', () => {
    const { out, err } = captureStreams();
    // console.log is rerouted through console.error, which keeps its own stream
    // reference, so observe it with a spy rather than the stderr sink.
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const stdout = claimProtocolStdout();
    let logged: string[] = [];
    try {
      console.log('log line');
      console.info('info line');
      console.debug('debug line');
      process.stdout.write('direct write');
      logged = errorSpy.mock.calls.map((call) => call.join(' '));
    } finally {
      stdout.restore();
      errorSpy.mockRestore();
    }

    expect(out.join('')).toBe('');
    expect(err.join('')).toContain('direct write');
    expect(logged).toContain('log line');
    expect(logged).toContain('info line');
    expect(logged).toContain('debug line');
  });

  it('writes protocol frames to the real stdout via writeRaw', () => {
    const { out } = captureStreams();
    const stdout = claimProtocolStdout();
    try {
      stdout.writeRaw(encodeMessage({ jsonrpc: '2.0', id: 1, result: null }));
    } finally {
      stdout.restore();
    }
    expect(out.join('')).toContain('Content-Length:');
  });

  it('keeps a provider log off stdout so the next protocol frame is intact', () => {
    const { out, err } = captureStreams();
    const stdout = claimProtocolStdout();
    try {
      // Simulate an extension logging to stdout during initialization.
      process.stdout.write('provider init noise\n');
      console.log('more noise');
      // The transport then writes the next protocol frame.
      stdout.writeRaw(encodeMessage({ jsonrpc: '2.0', id: 42, result: { ok: true } }));
    } finally {
      stdout.restore();
    }

    const stdoutBytes = out.join('');
    expect(stdoutBytes).not.toContain('provider init noise');
    expect(stdoutBytes).not.toContain('more noise');
    expect(stdoutBytes.startsWith('Content-Length:')).toBe(true);
    expect(stdoutBytes).toContain('"id":42');
    expect(err.join('')).toContain('provider init noise');
  });

  it('restores the original stream methods', () => {
    captureStreams();
    const patched = process.stdout.write;
    const stdout = claimProtocolStdout();
    expect(process.stdout.write).not.toBe(patched);
    stdout.restore();
    expect(process.stdout.write).toBe(patched);
  });
});
