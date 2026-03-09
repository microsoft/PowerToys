// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Unit tests for the manage module (list / uninstall).
 */

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { listInstalledExtensions, uninstallExtension } from '../src/manage';

describe('manage', () => {
  let installDir: string;

  beforeEach(() => {
    installDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pipeline-test-manage-'));
  });

  afterEach(() => {
    fs.rmSync(installDir, { recursive: true, force: true });
  });

  function createExtension(
    name: string,
    raycastName: string,
    displayName: string = 'Test Extension',
    version: string = '1.0.0',
    installedBy: string = 'raycast-pipeline',
  ): void {
    const extDir = path.join(installDir, name);
    fs.mkdirSync(extDir, { recursive: true });

    fs.writeFileSync(
      path.join(extDir, 'cmdpal.json'),
      JSON.stringify({
        name,
        displayName,
        version,
        main: 'dist/index.js',
      }),
    );

    fs.writeFileSync(
      path.join(extDir, 'raycast-compat.json'),
      JSON.stringify({
        raycastOriginalName: raycastName,
        installedBy,
        commands: [],
        preferences: [],
        platforms: ['Windows'],
      }),
    );
  }

  describe('listInstalledExtensions', () => {
    it('returns empty array when install dir is empty', () => {
      const result = listInstalledExtensions(installDir);
      expect(result).toEqual([]);
    });

    it('returns empty array when install dir does not exist', () => {
      const result = listInstalledExtensions(path.join(installDir, 'nonexistent'));
      expect(result).toEqual([]);
    });

    it('lists extensions with raycast-pipeline marker', () => {
      createExtension('raycast-clipboard', 'clipboard-history', 'Clipboard History');
      createExtension('raycast-color-picker', 'color-picker', 'Color Picker');

      const result = listInstalledExtensions(installDir);
      expect(result).toHaveLength(2);
      expect(result.map((e) => e.name)).toContain('raycast-clipboard');
      expect(result.map((e) => e.name)).toContain('raycast-color-picker');
    });

    it('skips extensions without raycast-compat.json', () => {
      // Create a native CmdPal extension (no raycast-compat.json)
      const nativeDir = path.join(installDir, 'native-ext');
      fs.mkdirSync(nativeDir);
      fs.writeFileSync(
        path.join(nativeDir, 'cmdpal.json'),
        JSON.stringify({ name: 'native-ext', main: 'dist/index.js' }),
      );

      createExtension('raycast-test', 'test');

      const result = listInstalledExtensions(installDir);
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('raycast-test');
    });

    it('skips extensions not installed by the pipeline', () => {
      createExtension('raycast-manual', 'manual', 'Manual', '1.0.0', 'manual');
      createExtension('raycast-auto', 'auto', 'Auto', '1.0.0', 'raycast-pipeline');

      const result = listInstalledExtensions(installDir);
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('raycast-auto');
    });

    it('includes correct metadata', () => {
      createExtension('raycast-test', 'test-ext', 'Test Extension', '2.0.0');

      const result = listInstalledExtensions(installDir);
      expect(result).toHaveLength(1);
      expect(result[0]).toEqual({
        name: 'raycast-test',
        raycastName: 'test-ext',
        displayName: 'Test Extension',
        version: '2.0.0',
        path: path.join(installDir, 'raycast-test'),
      });
    });

    it('skips directories with invalid JSON', () => {
      const badDir = path.join(installDir, 'bad-ext');
      fs.mkdirSync(badDir);
      fs.writeFileSync(path.join(badDir, 'cmdpal.json'), 'not json');
      fs.writeFileSync(path.join(badDir, 'raycast-compat.json'), 'not json');

      const result = listInstalledExtensions(installDir);
      expect(result).toEqual([]);
    });
  });

  describe('uninstallExtension', () => {
    it('removes extension by CmdPal name', () => {
      createExtension('raycast-test', 'test');

      const removed = uninstallExtension('raycast-test', installDir);
      expect(removed).toBe(true);
      expect(fs.existsSync(path.join(installDir, 'raycast-test'))).toBe(false);
    });

    it('removes extension by Raycast name', () => {
      createExtension('raycast-clipboard', 'clipboard-history');

      const removed = uninstallExtension('clipboard-history', installDir);
      expect(removed).toBe(true);
      expect(fs.existsSync(path.join(installDir, 'raycast-clipboard'))).toBe(false);
    });

    it('auto-prefixes "raycast-" when searching', () => {
      createExtension('raycast-test', 'test');

      const removed = uninstallExtension('test', installDir);
      expect(removed).toBe(true);
    });

    it('returns false when extension not found', () => {
      const removed = uninstallExtension('nonexistent', installDir);
      expect(removed).toBe(false);
    });
  });
});
