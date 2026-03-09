// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Unit tests for the cleanup stage.
 */

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { cleanup } from '../src/stage-cleanup';

describe('stage-cleanup', () => {
  it('removes temp directory and all contents', () => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pipeline-test-cleanup-'));
    fs.writeFileSync(path.join(tempDir, 'file.txt'), 'content');
    fs.mkdirSync(path.join(tempDir, 'sub'));
    fs.writeFileSync(path.join(tempDir, 'sub', 'nested.txt'), 'nested');

    cleanup(tempDir);

    expect(fs.existsSync(tempDir)).toBe(false);
  });

  it('does not throw when directory does not exist', () => {
    expect(() => cleanup('/nonexistent/path')).not.toThrow();
  });

  it('does not throw on empty string', () => {
    expect(() => cleanup('')).not.toThrow();
  });
});
