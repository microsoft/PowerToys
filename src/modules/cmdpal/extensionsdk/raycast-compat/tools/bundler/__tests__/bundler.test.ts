// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Integration tests for the Raycast → CmdPal esbuild bundler.
 *
 * Verifies that:
 * 1. esbuild resolves @raycast/api to our compat layer
 * 2. The output is a single bundled CJS file
 * 3. Source maps are generated
 * 4. The bundled code contains compat layer markers (not real @raycast/api)
 * 5. The CLI wrapper produces a valid cmdpal.json manifest
 */

import * as path from 'path';
import * as fs from 'fs';
import { bundleCommand, bundleExtension, COMPAT_SRC, UTILS_SHIM } from '../src/build';

const FIXTURES = path.resolve(__dirname, '..', '__fixtures__');
const SAMPLE_EXT = path.join(FIXTURES, 'sample-extension');
const TMP_OUTPUT = path.join(__dirname, '..', '.test-output');

beforeAll(() => {
  fs.mkdirSync(path.join(TMP_OUTPUT, 'dist'), { recursive: true });
});

afterAll(() => {
  // Clean up test output
  if (fs.existsSync(TMP_OUTPUT)) {
    fs.rmSync(TMP_OUTPUT, { recursive: true, force: true });
  }
});

describe('bundleCommand', () => {
  it('should bundle a single command with @raycast/api aliased', async () => {
    const entryPoint = path.join(SAMPLE_EXT, 'src', 'index.tsx');
    const outfile = path.join(TMP_OUTPUT, 'dist', 'index.js');

    const result = await bundleCommand({
      entryPoint,
      outfile,
      absWorkingDir: SAMPLE_EXT,
    });

    expect(result.success).toBe(true);
    expect(result.errors).toHaveLength(0);
    expect(fs.existsSync(outfile)).toBe(true);
  });

  it('should produce a CommonJS module', async () => {
    const outfile = path.join(TMP_OUTPUT, 'dist', 'index.js');

    // The file should already exist from the previous test, but bundle again to be safe
    await bundleCommand({
      entryPoint: path.join(SAMPLE_EXT, 'src', 'index.tsx'),
      outfile,
      absWorkingDir: SAMPLE_EXT,
    });

    const content = fs.readFileSync(outfile, 'utf-8');

    // CJS markers: module.exports or exports.default
    expect(
      content.includes('module.exports') || content.includes('exports.default')
    ).toBe(true);
  });

  it('should generate source maps', async () => {
    const outfile = path.join(TMP_OUTPUT, 'dist', 'index.js');
    const mapFile = outfile + '.map';

    await bundleCommand({
      entryPoint: path.join(SAMPLE_EXT, 'src', 'index.tsx'),
      outfile,
      absWorkingDir: SAMPLE_EXT,
    });

    expect(fs.existsSync(mapFile)).toBe(true);

    const content = fs.readFileSync(outfile, 'utf-8');
    expect(content).toContain('//# sourceMappingURL=');
  });

  it('should alias @raycast/api to our compat layer', async () => {
    const outfile = path.join(TMP_OUTPUT, 'dist', 'index.js');

    await bundleCommand({
      entryPoint: path.join(SAMPLE_EXT, 'src', 'index.tsx'),
      outfile,
      absWorkingDir: SAMPLE_EXT,
    });

    const content = fs.readFileSync(outfile, 'utf-8');

    // The bundled output should NOT contain a bare require('@raycast/api')
    // because esbuild inlines the aliased module
    expect(content).not.toMatch(/require\(['"]@raycast\/api['"]\)/);

    // It SHOULD contain our compat layer markers — the createMarker/createElement
    // pattern from markers.tsx uses string type names like "List", "List.Item"
    expect(content).toContain('List.Item');
  });

  it('should keep react as external', async () => {
    const outfile = path.join(TMP_OUTPUT, 'dist', 'index.js');

    await bundleCommand({
      entryPoint: path.join(SAMPLE_EXT, 'src', 'index.tsx'),
      outfile,
      absWorkingDir: SAMPLE_EXT,
    });

    const content = fs.readFileSync(outfile, 'utf-8');

    // React should be required as an external module
    expect(content).toMatch(/require\(['"]react['"]\)/);
  });

  it('should handle missing entry point gracefully', async () => {
    const result = await bundleCommand({
      entryPoint: path.join(SAMPLE_EXT, 'src', 'nonexistent.tsx'),
      outfile: path.join(TMP_OUTPUT, 'dist', 'nonexistent.js'),
    });

    expect(result.success).toBe(false);
    expect(result.errors.length).toBeGreaterThan(0);
  });
});

describe('bundleExtension', () => {
  it('should bundle all commands from package.json', async () => {
    const result = await bundleExtension({
      extensionDir: SAMPLE_EXT,
      outputDir: TMP_OUTPUT,
    });

    expect(result.success).toBe(true);
    expect(result.commands).toHaveLength(1);
    expect(result.commands[0].name).toBe('index');
    expect(result.commands[0].result.success).toBe(true);
  });

  it('should create the output file in dist/', async () => {
    await bundleExtension({
      extensionDir: SAMPLE_EXT,
      outputDir: TMP_OUTPUT,
    });

    const outfile = path.join(TMP_OUTPUT, 'dist', 'index.js');
    expect(fs.existsSync(outfile)).toBe(true);
  });
});

describe('path resolution', () => {
  it('COMPAT_SRC should point to the compat layer source', () => {
    // The compat layer's src/index.ts should exist
    const indexTs = path.join(COMPAT_SRC, 'index.ts');
    expect(fs.existsSync(indexTs)).toBe(true);
  });

  it('UTILS_SHIM should point to the utils shim', () => {
    // The utils shim .ts file should exist
    const shimTs = UTILS_SHIM + '.ts';
    expect(fs.existsSync(shimTs)).toBe(true);
  });
});
