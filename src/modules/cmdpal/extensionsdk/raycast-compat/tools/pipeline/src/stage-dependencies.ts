// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Stage 3: Dependencies — Runs `npm install` in the downloaded extension
 * directory to install its dependencies before bundling.
 */

import { execFile } from 'child_process';
import * as path from 'path';
import * as fs from 'fs';

export interface DependencyResult {
  /** Whether npm install succeeded. */
  success: boolean;
  /** Combined stdout from npm. */
  stdout: string;
  /** Combined stderr from npm. */
  stderr: string;
}

/**
 * Install npm dependencies for a Raycast extension.
 *
 * Shells out to `npm install --production` to install only runtime deps.
 * Assumes Node.js and npm are on PATH (the CmdPal store gate verifies this
 * before allowing Raycast extension installs).
 */
export function installDependencies(
  extensionDir: string,
): Promise<DependencyResult> {
  return new Promise((resolve) => {
    // Verify package.json exists
    const pkgPath = path.join(extensionDir, 'package.json');
    if (!fs.existsSync(pkgPath)) {
      resolve({
        success: false,
        stdout: '',
        stderr: 'package.json not found — cannot install dependencies',
      });
      return;
    }

    // Use npm.cmd on Windows, npm elsewhere
    const npmCmd = process.platform === 'win32' ? 'npm.cmd' : 'npm';

    execFile(
      npmCmd,
      ['install', '--production', '--no-audit', '--no-fund'],
      {
        cwd: extensionDir,
        timeout: 120_000, // 2-minute timeout for npm install
        env: { ...process.env, NODE_ENV: 'production' },
        shell: true, // Required on Windows — .cmd files need cmd.exe to execute
      },
      (error, stdout, stderr) => {
        if (error) {
          resolve({
            success: false,
            stdout: stdout ?? '',
            stderr: stderr ?? error.message,
          });
          return;
        }

        resolve({
          success: true,
          stdout: stdout ?? '',
          stderr: stderr ?? '',
        });
      },
    );
  });
}
