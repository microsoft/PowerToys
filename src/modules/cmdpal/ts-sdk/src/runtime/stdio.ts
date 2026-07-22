// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Ownership of the process `stdout` stream.
 *
 * The Command Palette JSON-RPC transport uses `stdout` exclusively for framed
 * protocol messages: any stray bytes written to `stdout` would corrupt the next
 * frame and break the connection. Extensions, however, routinely call
 * `console.log` during startup. {@link claimProtocolStdout} captures the raw
 * `stdout` writer for the transport's own use and then redirects
 * `console.log`, `console.info`, `console.debug`, and any direct
 * `process.stdout.write` call to `stderr`, so author logging is preserved
 * without touching the protocol channel.
 */

/** Handle to the claimed protocol `stdout`, with a way to release it. */
export interface ProtocolStdout {
  /** Writes raw bytes directly to the real `stdout`, bypassing the redirect. */
  writeRaw(chunk: Uint8Array | string): void;
  /** Restores the original `stdout.write` and console methods. */
  restore(): void;
}

type ConsoleMethod = (...args: unknown[]) => void;

/**
 * Claims `stdout` for the protocol. Call this before loading or running any
 * extension author code, so early logging never lands on the protocol stream.
 *
 * @returns A handle whose `writeRaw` writes protocol frames to the real
 * `stdout`, and whose `restore` undoes the redirects during shutdown.
 */
export function claimProtocolStdout(): ProtocolStdout {
  const rawWrite = process.stdout.write.bind(process.stdout);
  const writeRaw = (chunk: Uint8Array | string): void => {
    rawWrite(chunk);
  };

  const originalStdoutWrite = process.stdout.write;
  const originalLog = console.log;
  const originalInfo = console.info;
  const originalDebug = console.debug;

  const stderrWrite = process.stderr.write.bind(process.stderr) as unknown as (
    ...args: unknown[]
  ) => boolean;
  const forwardToStderr = (...args: unknown[]): boolean => stderrWrite(...args);
  process.stdout.write = forwardToStderr as unknown as typeof process.stdout.write;

  const logToStderr: ConsoleMethod = (...args) => {
    console.error(...args);
  };
  console.log = logToStderr;
  console.info = logToStderr;
  console.debug = logToStderr;

  const restore = (): void => {
    process.stdout.write = originalStdoutWrite;
    console.log = originalLog;
    console.info = originalInfo;
    console.debug = originalDebug;
  };

  return { writeRaw, restore };
}
