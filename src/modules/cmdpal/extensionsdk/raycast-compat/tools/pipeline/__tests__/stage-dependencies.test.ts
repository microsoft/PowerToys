// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Unit tests for the dependencies stage.
 */

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as child_process from 'child_process';
import { installDependencies } from '../src/stage-dependencies';

// Mock child_process.execFile to avoid actually running npm
jest.mock('child_process');

const mockExecFile = child_process.execFile as unknown as jest.Mock;

describe('stage-dependencies', () => {
  let tempDir: string;

  beforeEach(() => {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pipeline-test-deps-'));
    jest.resetAllMocks();
  });

  afterEach(() => {
    fs.rmSync(tempDir, { recursive: true, force: true });
  });

  it('calls npm install with correct arguments', async () => {
    fs.writeFileSync(
      path.join(tempDir, 'package.json'),
      JSON.stringify({ name: 'test' }),
    );

    mockExecFile.mockImplementation(
      (_cmd: string, _args: string[], _opts: unknown, cb: Function) => {
        cb(null, 'added 5 packages', '');
      },
    );

    const result = await installDependencies(tempDir);

    expect(result.success).toBe(true);
    expect(result.stdout).toBe('added 5 packages');
    expect(mockExecFile).toHaveBeenCalledTimes(1);

    const [cmd, args, opts] = mockExecFile.mock.calls[0];
    expect(cmd).toMatch(/npm/);
    expect(args).toContain('install');
    expect(args).toContain('--production');
    expect(args).toContain('--no-audit');
    expect(args).toContain('--no-fund');
    expect(opts.cwd).toBe(tempDir);
  });

  it('reports failure when npm install fails', async () => {
    fs.writeFileSync(
      path.join(tempDir, 'package.json'),
      JSON.stringify({ name: 'test' }),
    );

    mockExecFile.mockImplementation(
      (_cmd: string, _args: string[], _opts: unknown, cb: Function) => {
        cb(new Error('npm ERR! 404 Not Found'), '', 'npm ERR! 404 Not Found');
      },
    );

    const result = await installDependencies(tempDir);

    expect(result.success).toBe(false);
    expect(result.stderr).toContain('404 Not Found');
  });

  it('fails when package.json is missing', async () => {
    const result = await installDependencies(tempDir);

    expect(result.success).toBe(false);
    expect(result.stderr).toContain('package.json not found');
  });

  it('uses npm.cmd on Windows', async () => {
    fs.writeFileSync(
      path.join(tempDir, 'package.json'),
      JSON.stringify({ name: 'test' }),
    );

    const originalPlatform = Object.getOwnPropertyDescriptor(process, 'platform');
    Object.defineProperty(process, 'platform', { value: 'win32' });

    mockExecFile.mockImplementation(
      (_cmd: string, _args: string[], _opts: unknown, cb: Function) => {
        cb(null, 'ok', '');
      },
    );

    await installDependencies(tempDir);

    const [cmd] = mockExecFile.mock.calls[0];
    expect(cmd).toBe('npm.cmd');

    // Restore platform
    if (originalPlatform) {
      Object.defineProperty(process, 'platform', originalPlatform);
    }
  });
});
