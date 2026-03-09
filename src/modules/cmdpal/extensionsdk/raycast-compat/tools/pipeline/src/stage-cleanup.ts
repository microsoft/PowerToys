// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Stage 6: Cleanup — Removes the temporary directory created during download.
 */

import * as fs from 'fs';

/**
 * Recursively remove a temporary directory.
 * Fails silently if the directory doesn't exist or can't be removed.
 */
export function cleanup(tempDir: string): void {
  try {
    fs.rmSync(tempDir, { recursive: true, force: true });
  } catch {
    // Best-effort cleanup — don't fail the pipeline for this
  }
}
