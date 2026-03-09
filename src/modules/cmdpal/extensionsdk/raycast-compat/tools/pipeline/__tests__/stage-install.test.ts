// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Unit tests for the install stage.
 */

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { installExtension, getDefaultInstallDir } from '../src/stage-install';

describe('stage-install', () => {
  let buildDir: string;
  let installDir: string;

  beforeEach(() => {
    buildDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pipeline-test-build-'));
    installDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pipeline-test-install-'));
  });

  afterEach(() => {
    fs.rmSync(buildDir, { recursive: true, force: true });
    fs.rmSync(installDir, { recursive: true, force: true });
  });

  function writeBuildOutput(name: string, main: string): void {
    // Write cmdpal.json
    fs.writeFileSync(
      path.join(buildDir, 'cmdpal.json'),
      JSON.stringify({ name, main, displayName: 'Test' }),
    );

    // Write the main entry point
    const mainDir = path.dirname(path.join(buildDir, main));
    fs.mkdirSync(mainDir, { recursive: true });
    fs.writeFileSync(path.join(buildDir, main), 'module.exports = {};');
  }

  it('installs extension to correct directory', () => {
    writeBuildOutput('raycast-test', 'dist/index.js');

    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(true);
    expect(result.extensionPath).toBe(path.join(installDir, 'raycast-test'));
    expect(fs.existsSync(path.join(installDir, 'raycast-test', 'cmdpal.json'))).toBe(true);
    expect(fs.existsSync(path.join(installDir, 'raycast-test', 'dist', 'index.js'))).toBe(true);
  });

  it('copies assets subdirectory', () => {
    writeBuildOutput('raycast-test', 'dist/index.js');

    // Add an asset
    const assetsDir = path.join(buildDir, 'assets');
    fs.mkdirSync(assetsDir);
    fs.writeFileSync(path.join(assetsDir, 'icon.png'), 'PNG');

    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(true);
    expect(fs.existsSync(path.join(installDir, 'raycast-test', 'assets', 'icon.png'))).toBe(true);
  });

  it('fails when cmdpal.json is missing', () => {
    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining('cmdpal.json not found'));
  });

  it('fails when cmdpal.json has no name', () => {
    fs.writeFileSync(
      path.join(buildDir, 'cmdpal.json'),
      JSON.stringify({ main: 'dist/index.js' }),
    );

    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining('missing required'));
  });

  it('fails when cmdpal.json has no main', () => {
    fs.writeFileSync(
      path.join(buildDir, 'cmdpal.json'),
      JSON.stringify({ name: 'test' }),
    );

    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining('missing required'));
  });

  it('reports error when main entry point file is missing', () => {
    fs.writeFileSync(
      path.join(buildDir, 'cmdpal.json'),
      JSON.stringify({ name: 'raycast-test', main: 'dist/index.js' }),
    );
    // Don't create the actual dist/index.js file

    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining('Main entry point'));
  });

  it('overwrites existing installation', () => {
    writeBuildOutput('raycast-test', 'dist/index.js');

    // First install
    installExtension(buildDir, installDir);

    // Modify build output
    fs.writeFileSync(path.join(buildDir, 'dist', 'index.js'), 'module.exports = { v: 2 };');

    // Second install
    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(true);

    const content = fs.readFileSync(
      path.join(installDir, 'raycast-test', 'dist', 'index.js'),
      'utf-8',
    );
    expect(content).toContain('v: 2');
  });

  it('cleans up staging directory on success', () => {
    writeBuildOutput('raycast-test', 'dist/index.js');

    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(true);

    // No leftover staging or backup dirs
    expect(fs.existsSync(path.join(installDir, 'raycast-test.installing'))).toBe(false);
    expect(fs.existsSync(path.join(installDir, 'raycast-test.backup'))).toBe(false);
  });

  it('cleans up leftover staging dir from previous failed install', () => {
    writeBuildOutput('raycast-test', 'dist/index.js');

    // Simulate a leftover staging dir from a previous failed install
    const leftover = path.join(installDir, 'raycast-test.installing');
    fs.mkdirSync(leftover, { recursive: true });
    fs.writeFileSync(path.join(leftover, 'stale.txt'), 'stale');

    const result = installExtension(buildDir, installDir);
    expect(result.success).toBe(true);
    expect(fs.existsSync(leftover)).toBe(false);
  });
});

describe('getDefaultInstallDir', () => {
  it('returns a path under LOCALAPPDATA', () => {
    const dir = getDefaultInstallDir();
    expect(dir).toContain('JSExtensions');
    expect(dir).toContain('CommandPalette');
  });
});
