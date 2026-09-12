// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Stable, collision-resistant identifiers derived from a command's full
 * payload. Generated ids hash the entire payload rather than a truncated
 * prefix, so two commands that merely share a leading substring still receive
 * distinct ids.
 */

import { createHash } from 'node:crypto';

/**
 * Produces a short, stable id from an arbitrary payload. The payload is
 * JSON-serialized and hashed with SHA-256; the first 16 hex characters are
 * used, which is ample to avoid collisions across a single provider's commands.
 *
 * @param prefix Namespace prefix for the id, for example `'copy-text'`.
 * @param payload Values that fully define the command's identity.
 */
export function stableId(prefix: string, payload: unknown): string {
  const serialized = JSON.stringify(payload);
  const digest = createHash('sha256').update(serialized).digest('hex').slice(0, 16);
  return `${prefix}:${digest}`;
}
