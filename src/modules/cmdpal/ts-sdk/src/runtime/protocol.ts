// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Protocol version metadata for the Command Palette JSON-RPC handshake.
 *
 * `protocolVersion` is a single integer that names the major wire version the
 * SDK speaks. It is exchanged during `initialize` (see the versioned handshake
 * in `03-jsonrpc-protocol.md`). A host that advertises a different integer is
 * incompatible; a host that advertises none is treated as legacy and
 * compatible, so the SDK keeps working against hosts that predate versioning.
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

/** The wire protocol major version this SDK implements. v1 = 1. */
export const PROTOCOL_VERSION = 1;

/**
 * Whether a host-advertised protocol version is compatible with this SDK. A
 * missing (`undefined`) version is legacy and compatible; any present version
 * must match {@link PROTOCOL_VERSION} exactly, since the integer is the major.
 */
export function isProtocolCompatible(hostProtocolVersion: number | undefined): boolean {
  return hostProtocolVersion === undefined || hostProtocolVersion === PROTOCOL_VERSION;
}

let cachedSdkVersion: string | null = null;

/**
 * Reads the SDK version from the package manifest. Cached after the first read;
 * falls back to `'0.0.0'` when the manifest cannot be located or parsed so the
 * handshake never fails on a version lookup.
 */
export function getSdkVersion(): string {
  cachedSdkVersion ??= readSdkVersion() ?? '0.0.0';
  return cachedSdkVersion;
}

function readSdkVersion(): string | null {
  try {
    const here = dirname(fileURLToPath(import.meta.url));
    // Both the compiled layout (dist/runtime/protocol.js) and the source layout
    // (src/runtime/protocol.ts) place package.json two directories up.
    const manifestPath = join(here, '..', '..', 'package.json');
    const parsed: unknown = JSON.parse(readFileSync(manifestPath, 'utf8'));
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      typeof (parsed as { version?: unknown }).version === 'string'
    ) {
      return (parsed as { version: string }).version;
    }
  } catch {
    // Fall through to the caller's default.
  }
  return null;
}
