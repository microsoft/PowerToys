// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Stage 1: Download — Fetches all source files for a Raycast extension
 * from the raycast/extensions GitHub repository into a temp directory.
 */

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  RaycastExtensionsClient,
  type ExtensionFile,
} from '@cmdpal/raycast-github-client';

export interface DownloadResult {
  /** Temporary directory containing the downloaded extension source. */
  tempDir: string;
  /** List of files that were written. */
  files: string[];
}

/**
 * Download a Raycast extension's source files to a temporary directory.
 *
 * Uses the GitHub client's `downloadExtension()` which walks the Git tree
 * and fetches blobs in parallel batches.
 */
export async function downloadExtension(
  extensionName: string,
  githubToken?: string,
): Promise<DownloadResult> {
  const client = new RaycastExtensionsClient({
    token: githubToken,
  });

  // Fetch all files from the extension directory
  const extensionFiles: ExtensionFile[] =
    await client.downloadExtension(extensionName);

  if (extensionFiles.length === 0) {
    throw new Error(
      `No files found for extension "${extensionName}". ` +
      'Verify the name matches an entry in raycast/extensions.',
    );
  }

  // Create a temp directory for the downloaded source
  const tempDir = fs.mkdtempSync(
    path.join(os.tmpdir(), `raycast-${extensionName}-`),
  );

  const writtenFiles: string[] = [];

  for (const file of extensionFiles) {
    const filePath = path.join(tempDir, file.path);
    const fileDir = path.dirname(filePath);

    fs.mkdirSync(fileDir, { recursive: true });
    fs.writeFileSync(filePath, file.content, 'utf-8');
    writtenFiles.push(file.path);
  }

  return { tempDir, files: writtenFiles };
}
