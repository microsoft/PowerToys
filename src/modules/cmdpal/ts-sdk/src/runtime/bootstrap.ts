// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Bootstrap loader for Command Palette extensions.
 *
 * ES module static imports are hoisted and evaluated before the importing
 * module's body runs. If an extension entry statically imports the SDK and its
 * own dependencies, a stray top-level `stdout` write in any of those modules (a
 * forgotten `console.log`, for example) executes before {@link
 * startJsonRpcServer} can guard the stream, and those raw bytes corrupt the
 * JSON-RPC framing on `fd1`.
 *
 * The bootstrap loader is the real process entry point. It claims `stdout` for
 * the protocol first, so `console.log`/`console.info`/`console.debug` and any
 * direct `process.stdout.write` are redirected to `stderr` before any extension
 * code runs. Only then does it dynamically import the extension entry. Because
 * the import is dynamic (`await import(...)`), the extension and its transitive
 * dependencies are evaluated after the redirect is installed, so their top-level
 * side effects can never reach the protocol channel.
 */

import { pathToFileURL } from 'node:url';
import { claimProtocolStdout } from './stdio.js';

/**
 * Claims `stdout` for the protocol, then dynamically imports the extension
 * entry. Use this as the extension's process entry point instead of importing
 * the entry directly.
 *
 * @param entry Module specifier or file URL of the extension entry to load.
 * The entry is expected to call {@link startJsonRpcServer} (or `run`) itself.
 * @returns The imported entry module's namespace.
 */
export async function bootstrap(entry: string): Promise<unknown> {
  // Claim stdout before importing anything the extension brings in, so a
  // top-level log in the entry or a transitive dependency cannot land a raw
  // byte on the protocol channel.
  claimProtocolStdout();
  return import(entry);
}

/**
 * Resolves the extension entry for command-line invocation. The entry is read
 * from the first process argument, falling back to the `CMDPAL_EXTENSION_ENTRY`
 * environment variable. A bare filesystem path is converted to a `file:` URL so
 * the dynamic import resolves it as a module rather than a package specifier.
 *
 * @returns The resolved entry specifier, or `null` when none was provided.
 */
export function resolveCliEntry(argv: readonly string[], env: NodeJS.ProcessEnv): string | null {
  const raw = argv[2] ?? env.CMDPAL_EXTENSION_ENTRY;
  if (raw === undefined || raw.length === 0) {
    return null;
  }
  if (/^[a-zA-Z][a-zA-Z\d+.-]*:/.test(raw)) {
    // Already a URL (for example a file: URL); import it as-is.
    return raw;
  }
  return pathToFileURL(raw).href;
}

/**
 * Whether this module is the process entry point (invoked as `node
 * bootstrap.js ...`) rather than imported as a library.
 */
function isMainModule(): boolean {
  const entryArg = process.argv[1];
  if (entryArg === undefined) {
    return false;
  }
  return import.meta.url === pathToFileURL(entryArg).href;
}

/**
 * Runs the bootstrap from the command line, exiting with a non-zero code when
 * no entry is provided or the entry fails to load.
 */
async function runFromCli(): Promise<void> {
  const entry = resolveCliEntry(process.argv, process.env);
  if (entry === null) {
    process.stderr.write(
      'cmdpal-sdk: no extension entry provided. Pass a path as the first argument or set ' +
        'CMDPAL_EXTENSION_ENTRY.\n',
    );
    process.exitCode = 1;
    return;
  }
  try {
    await bootstrap(entry);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    process.stderr.write(`cmdpal-sdk: failed to load extension entry "${entry}": ${message}\n`);
    process.exitCode = 1;
  }
}

if (isMainModule()) {
  void runFromCli();
}
