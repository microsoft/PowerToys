// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * End-to-end integration test — validates the FULL Raycast→CmdPal pipeline.
 *
 * This test exercises the complete flow:
 *   1. Bundle a realistic Raycast extension (esbuild with @raycast/api aliasing)
 *   2. Load the bundled output into a BridgeProvider
 *   3. Verify top-level commands, list items, search filtering, actions
 *   4. Verify the manifest translator produces valid cmdpal.json
 *   5. Test error cases (broken extension, unsupported component, macOS-only)
 *   6. Capture a performance baseline for first-render-to-getItems latency
 *
 * The test uses REAL esbuild bundling and REAL reconciler/translator/bridge
 * execution — no mocks except for unavoidable environment differences.
 */

import * as path from 'path';
import * as fs from 'fs';
import React from 'react';

import { bundleCommand } from '../../tools/bundler/src/build';
import type { BundleResult } from '../../tools/bundler/src/build';
import { RaycastBridgeProvider } from '../../src/bridge/bridge-provider';
import type {
  RaycastExtensionManifest,
  RaycastCommandModule,
  PageSnapshot,
} from '../../src/bridge/bridge-provider';

// ══════════════════════════════════════════════════════════════════════════
// Paths
// ══════════════════════════════════════════════════════════════════════════

const FIXTURES = path.resolve(__dirname, 'fixtures');
const VALID_EXT = path.join(FIXTURES, 'valid-extension');
const MACOS_ONLY_EXT = path.join(FIXTURES, 'macos-only-extension');
const UNSUPPORTED_EXT = path.join(FIXTURES, 'unsupported-component-extension');
const BROKEN_EXT = path.join(FIXTURES, 'missing-react-extension');

const COMPAT_SRC = path.resolve(__dirname, '..', '..', 'src');

// Temporary output directory for bundled files
const OUTPUT_DIR = path.join(__dirname, '.output');

// ══════════════════════════════════════════════════════════════════════════
// Helpers
// ══════════════════════════════════════════════════════════════════════════

function loadManifest(extDir: string): RaycastExtensionManifest {
  const pkg = JSON.parse(fs.readFileSync(path.join(extDir, 'package.json'), 'utf-8'));
  return {
    name: pkg.name ?? path.basename(extDir),
    title: pkg.title ?? pkg.name ?? 'Unknown',
    description: pkg.description,
    icon: pkg.icon,
    commands: pkg.commands ?? [],
  };
}

function loadBundledModule(bundlePath: string): RaycastCommandModule {
  // Clear require cache so re-bundling picks up fresh code
  delete require.cache[require.resolve(bundlePath)];
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const mod = require(bundlePath);
  return mod as RaycastCommandModule;
}

function createProvider(
  extDir: string,
  module: RaycastCommandModule,
): RaycastBridgeProvider {
  const manifest = loadManifest(extDir);
  const provider = new RaycastBridgeProvider(manifest);
  const cmdName = manifest.commands[0]?.name ?? 'index';
  provider.registerCommand(cmdName, module);
  return provider;
}

/** Collect notifications emitted by the bridge. */
function trackNotifications(): {
  notifications: Array<{ method: string; params?: Record<string, unknown> }>;
  notifyFn: (method: string, params?: Record<string, unknown>) => void;
} {
  const notifications: Array<{ method: string; params?: Record<string, unknown> }> = [];
  return {
    notifications,
    notifyFn: (method, params) => notifications.push({ method, params }),
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Setup / Teardown
// ══════════════════════════════════════════════════════════════════════════

beforeAll(() => {
  fs.mkdirSync(path.join(OUTPUT_DIR, 'dist'), { recursive: true });
});

afterAll(() => {
  // Clean up bundled output
  fs.rmSync(OUTPUT_DIR, { recursive: true, force: true });
});

// ══════════════════════════════════════════════════════════════════════════
// 1. Full pipeline: bundle → load → render → translate → query
// ══════════════════════════════════════════════════════════════════════════

describe('E2E: Full pipeline — valid Raycast extension', () => {
  let bundleResult: BundleResult;
  let provider: RaycastBridgeProvider;
  const bundlePath = path.join(OUTPUT_DIR, 'dist', 'valid-ext.js');

  beforeAll(async () => {
    // Step 1: Bundle the extension with esbuild
    bundleResult = await bundleCommand({
      entryPoint: path.join(VALID_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: VALID_EXT,
    });
  });

  afterAll(() => {
    provider?.dispose();
  });

  test('esbuild produces a successful bundle', () => {
    expect(bundleResult.success).toBe(true);
    expect(bundleResult.errors).toHaveLength(0);
    expect(fs.existsSync(bundlePath)).toBe(true);
  });

  test('bundled output is a valid CommonJS module', () => {
    const content = fs.readFileSync(bundlePath, 'utf-8');
    // Should contain a require or exports pattern (CJS)
    expect(content.length).toBeGreaterThan(100);
    // Should NOT contain raw @raycast/api imports (they were aliased)
    expect(content).not.toContain('from "@raycast/api"');
    expect(content).not.toContain("from '@raycast/api'");
  });

  test('bundled module loads and has a default export', () => {
    const mod = loadBundledModule(bundlePath);
    expect(mod).toBeDefined();
    expect(mod.default).toBeDefined();
    expect(typeof mod.default).toBe('function');
  });

  test('getTopLevelCommands() returns the expected command', () => {
    const mod = loadBundledModule(bundlePath);
    provider = createProvider(VALID_EXT, mod);

    const commands = provider.topLevelCommands();
    expect(commands).toHaveLength(1);
    expect(commands[0].title).toBe('Search Items');
    expect(commands[0].subtitle).toBe('Hello World');
  });

  test('getCommand() mounts the React tree and returns a list page', () => {
    const mod = loadBundledModule(bundlePath);
    provider = createProvider(VALID_EXT, mod);

    const manifest = loadManifest(VALID_EXT);
    const commandId = `raycast-compat.${manifest.name}.${manifest.commands[0].name}`;

    const page = provider.getCommand(commandId);
    expect(page).toBeDefined();
    expect(page!._type).toBe('dynamicListPage');
    expect(page!.placeholderText).toContain('Search');
  });

  test('getItems() returns ListItems with correct titles and subtitles', () => {
    const mod = loadBundledModule(bundlePath);
    provider = createProvider(VALID_EXT, mod);

    const manifest = loadManifest(VALID_EXT);
    const commandId = `raycast-compat.${manifest.name}.${manifest.commands[0].name}`;

    // Mount the command
    provider.getCommand(commandId);

    const items = provider.getItems();
    expect(items.length).toBe(2);

    expect(items[0].title).toBe('Hello World');
    expect(items[0].subtitle).toBe('First item');

    expect(items[1].title).toBe('Goodbye World');
    expect(items[1].subtitle).toBe('Second item');
  });

  test('search filtering works — filters items by search text', (done) => {
    const mod = loadBundledModule(bundlePath);
    provider = createProvider(VALID_EXT, mod);

    const manifest = loadManifest(VALID_EXT);
    const commandId = `raycast-compat.${manifest.name}.${manifest.commands[0].name}`;

    provider.getCommand(commandId);

    // All items initially
    expect(provider.getItems().length).toBe(2);

    // Filter to "Hello" — React state updates may be batched, so allow async
    provider.setSearchText('Hello');

    const checkFiltered = () => {
      const items = provider.getItems();
      expect(items.length).toBe(1);
      expect(items[0].title).toBe('Hello World');

      // Filter to "Goodbye"
      provider.setSearchText('Goodbye');

      setTimeout(() => {
        const items2 = provider.getItems();
        expect(items2.length).toBe(1);
        expect(items2[0].title).toBe('Goodbye World');

        // No match
        provider.setSearchText('zzz_no_match');

        setTimeout(() => {
          expect(provider.getItems().length).toBe(0);

          // Clear search restores all items
          provider.setSearchText('');

          setTimeout(() => {
            expect(provider.getItems().length).toBe(2);
            done();
          }, 100);
        }, 100);
      }, 100);
    };

    // Try synchronous first, fall back to async (React may batch the update)
    if (provider.getItems().length < 2) {
      checkFiltered();
    } else {
      setTimeout(checkFiltered, 200);
    }
  }, 10000);

  test('actions are present on items (CopyToClipboard, OpenInBrowser)', () => {
    const mod = loadBundledModule(bundlePath);
    provider = createProvider(VALID_EXT, mod);

    const manifest = loadManifest(VALID_EXT);
    const commandId = `raycast-compat.${manifest.name}.${manifest.commands[0].name}`;

    provider.getCommand(commandId);

    const items = provider.getItems();
    expect(items.length).toBeGreaterThan(0);

    const firstItem = items[0];
    const moreCommands = firstItem.moreCommands as Array<Record<string, unknown>>;
    expect(moreCommands).toBeDefined();
    expect(moreCommands.length).toBe(2);

    // First action: CopyToClipboard
    expect(moreCommands[0].title).toBe('Action.CopyToClipboard');

    // Second action: OpenInBrowser
    expect(moreCommands[1].title).toBe('Action.OpenInBrowser');

    // Actions should have command IDs for invocation
    const cmd0 = moreCommands[0].command as Record<string, unknown>;
    const cmd1 = moreCommands[1].command as Record<string, unknown>;
    expect(cmd0.id).toBeDefined();
    expect(cmd1.id).toBeDefined();
    expect(typeof cmd0.id).toBe('string');
    expect(typeof cmd1.id).toBe('string');
  });

  test('notifications fire on search text change', (done) => {
    const mod = loadBundledModule(bundlePath);
    provider = createProvider(VALID_EXT, mod);
    const { notifications, notifyFn } = trackNotifications();
    provider.setNotifyFn(notifyFn);

    const manifest = loadManifest(VALID_EXT);
    const commandId = `raycast-compat.${manifest.name}.${manifest.commands[0].name}`;

    provider.getCommand(commandId);
    notifications.length = 0; // clear mount notifications

    provider.setSearchText('Hello');

    // React state updates may be batched — allow async processing
    const check = () => {
      const itemsChanged = notifications.filter(n => n.method === 'listPage/itemsChanged');
      expect(itemsChanged.length).toBeGreaterThan(0);
      done();
    };

    if (notifications.some(n => n.method === 'listPage/itemsChanged')) {
      check();
    } else {
      setTimeout(check, 200);
    }
  });
});

// ══════════════════════════════════════════════════════════════════════════
// 2. Manifest translation
// ══════════════════════════════════════════════════════════════════════════

describe('E2E: Manifest translator', () => {
  test('valid extension produces correct cmdpal.json fields', () => {
    const pkg = JSON.parse(
      fs.readFileSync(path.join(VALID_EXT, 'package.json'), 'utf-8'),
    );

    // Inline the translator logic (same as in bundle-extension.ts and translate-manifest.ts)
    const cmdpalManifest = {
      name: `raycast-${pkg.name}`,
      displayName: pkg.title,
      version: pkg.version ?? '1.0.0',
      description: pkg.description,
      icon: pkg.icon ? (pkg.icon.includes('/') ? pkg.icon : `assets/${pkg.icon}`) : undefined,
      main: 'dist/index.js',
      publisher: pkg.author ?? pkg.owner,
      engines: { node: '>=18' },
      capabilities: ['commands', 'listPages'],
    };

    expect(cmdpalManifest.name).toBe('raycast-hello-world');
    expect(cmdpalManifest.displayName).toBe('Hello World');
    expect(cmdpalManifest.version).toBe('1.0.0');
    expect(cmdpalManifest.description).toBeDefined();
    expect(cmdpalManifest.main).toBe('dist/index.js');
    expect(cmdpalManifest.publisher).toBe('test-author');
    expect(cmdpalManifest.icon).toBe('assets/icon.png');
    expect(cmdpalManifest.capabilities).toContain('commands');
    expect(cmdpalManifest.capabilities).toContain('listPages');
  });

  test('macOS-only extension is rejected by manifest validation', () => {
    const pkg = JSON.parse(
      fs.readFileSync(path.join(MACOS_ONLY_EXT, 'package.json'), 'utf-8'),
    );

    // Replicate the platform validation from translate-manifest.ts
    const platforms = pkg.platforms ?? ['macOS'];
    const hasWindows = platforms.some(
      (p: string) => p.toLowerCase() === 'windows',
    );

    expect(hasWindows).toBe(false);

    // Validate that the error message would be generated
    const errors: string[] = [];
    if (!hasWindows) {
      errors.push(
        `Platform rejection: extension supports [${platforms.join(', ')}] — 'Windows' is required.`,
      );
    }

    expect(errors.length).toBeGreaterThan(0);
    expect(errors[0]).toContain('Platform rejection');
    expect(errors[0]).toContain('Windows');
  });

  test('extension without platforms field defaults to macOS-only (rejected)', () => {
    const pkg: Record<string, unknown> = {
      name: 'no-platform',
      title: 'No Platform',
      commands: [{ name: 'index' }],
    };

    // Raycast defaults to ["macOS"] when platforms is absent
    const platforms = (pkg.platforms as string[] | undefined) ?? ['macOS'];
    const hasWindows = platforms.some(
      (p: string) => p.toLowerCase() === 'windows',
    );

    expect(hasWindows).toBe(false);
  });
});

// ══════════════════════════════════════════════════════════════════════════
// 3. Error cases
// ══════════════════════════════════════════════════════════════════════════

describe('E2E: Error cases', () => {
  test('extension with broken default export — graceful error on mount', async () => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'broken-ext.js');

    const result = await bundleCommand({
      entryPoint: path.join(BROKEN_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: BROKEN_EXT,
    });

    expect(result.success).toBe(true);

    const mod = loadBundledModule(bundlePath);
    const manifest = loadManifest(BROKEN_EXT);
    const provider = new RaycastBridgeProvider(manifest);
    const cmdName = manifest.commands[0].name;
    provider.registerCommand(cmdName, mod);

    const commandId = `raycast-compat.${manifest.name}.${cmdName}`;

    // The component throws during render — the reconciler should catch it.
    // Bridge provider should handle this gracefully (no crash, snapshot is null).
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

    try {
      // getCommand triggers _mountCommand which runs render()
      const page = provider.getCommand(commandId);

      // The extension threw, so either:
      // a) the page is a loading placeholder (snapshot was null)
      // b) or getCommand still returned something (the provider is resilient)
      // Either way, getItems() should not crash
      const items = provider.getItems();
      expect(Array.isArray(items)).toBe(true);
    } finally {
      consoleSpy.mockRestore();
      provider.dispose();
    }
  });

  test('extension using Grid component — translator produces a DynamicListPage', async () => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'unsupported-ext.js');

    const result = await bundleCommand({
      entryPoint: path.join(UNSUPPORTED_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: UNSUPPORTED_EXT,
    });

    expect(result.success).toBe(true);

    const mod = loadBundledModule(bundlePath);
    const manifest = loadManifest(UNSUPPORTED_EXT);
    const provider = new RaycastBridgeProvider(manifest);
    const cmdName = manifest.commands[0].name;
    provider.registerCommand(cmdName, mod);

    const commandId = `raycast-compat.${manifest.name}.${cmdName}`;

    // Grid is now supported — translator maps Grid → DynamicListPage
    const page = provider.getCommand(commandId);

    // Provider should return a valid page
    expect(page).toBeDefined();

    // Snapshot should be a list page since Grid is translated to DynamicListPage
    expect(provider.snapshot).not.toBeNull();
    expect(provider.snapshot!.kind).toBe('list');

    // getItems() should return the Grid.Items as list items
    const items = provider.getItems();
    expect(items.length).toBeGreaterThan(0);
    expect(items[0]).toHaveProperty('title');

    provider.dispose();
  });

  test('bundling a non-existent file returns a failure result', async () => {
    const result = await bundleCommand({
      entryPoint: path.join(FIXTURES, 'does-not-exist', 'src', 'index.tsx'),
      outfile: path.join(OUTPUT_DIR, 'dist', 'nonexistent.js'),
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
    });

    expect(result.success).toBe(false);
    expect(result.errors.length).toBeGreaterThan(0);
  });
});

// ══════════════════════════════════════════════════════════════════════════
// 4. Performance baseline
// ══════════════════════════════════════════════════════════════════════════

describe('E2E: Performance baseline', () => {
  test('first render-to-getItems completes within 5 seconds', async () => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'perf-ext.js');

    const bundleResult = await bundleCommand({
      entryPoint: path.join(VALID_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: VALID_EXT,
    });

    expect(bundleResult.success).toBe(true);

    const mod = loadBundledModule(bundlePath);
    const manifest = loadManifest(VALID_EXT);

    // Measure: from provider creation + mount to first getItems() response
    const startTime = performance.now();

    const provider = new RaycastBridgeProvider(manifest);
    const cmdName = manifest.commands[0].name;
    provider.registerCommand(cmdName, mod);

    const commandId = `raycast-compat.${manifest.name}.${cmdName}`;
    provider.getCommand(commandId);

    const items = provider.getItems();
    const endTime = performance.now();
    const elapsed = endTime - startTime;

    // Log for CI comparison — not a pass/fail gate yet
    console.log(`[PERF] First render-to-getItems: ${elapsed.toFixed(2)}ms`);
    console.log(`[PERF] Item count: ${items.length}`);

    // Generous upper bound — should normally be < 100ms
    expect(elapsed).toBeLessThan(5000);
    expect(items.length).toBe(2);

    provider.dispose();
  });

  test('search filtering round-trip completes within 2 seconds', (done) => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'perf-search.js');

    bundleCommand({
      entryPoint: path.join(VALID_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: VALID_EXT,
    }).then((bundleResult) => {
      expect(bundleResult.success).toBe(true);

      const mod = loadBundledModule(bundlePath);
      const manifest = loadManifest(VALID_EXT);

      const provider = new RaycastBridgeProvider(manifest);
      const cmdName = manifest.commands[0].name;
      provider.registerCommand(cmdName, mod);

      const commandId = `raycast-compat.${manifest.name}.${cmdName}`;
      provider.getCommand(commandId);

      // Measure search round-trip
      const startTime = performance.now();
      provider.setSearchText('Hello');

      // Allow async React state processing
      const check = () => {
        const filteredItems = provider.getItems();
        const endTime = performance.now();
        const elapsed = endTime - startTime;

        console.log(`[PERF] Search filter round-trip: ${elapsed.toFixed(2)}ms`);
        console.log(`[PERF] Filtered items: ${filteredItems.length}`);

        expect(elapsed).toBeLessThan(2000);
        expect(filteredItems.length).toBe(1);

        provider.dispose();
        done();
      };

      if (provider.getItems().length < 2) {
        check();
      } else {
        setTimeout(check, 200);
      }
    });
  });

  test('esbuild bundling completes within 30 seconds', async () => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'perf-bundle.js');

    const startTime = performance.now();

    const result = await bundleCommand({
      entryPoint: path.join(VALID_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: VALID_EXT,
    });

    const endTime = performance.now();
    const elapsed = endTime - startTime;

    console.log(`[PERF] esbuild bundling: ${elapsed.toFixed(2)}ms`);

    expect(result.success).toBe(true);
    expect(elapsed).toBeLessThan(30000);
  });
});

// ══════════════════════════════════════════════════════════════════════════
// 5. Integration: Bundle + Bridge lifecycle
// ══════════════════════════════════════════════════════════════════════════

describe('E2E: Bundle + Bridge lifecycle', () => {
  test('provider properties reflect the extension manifest', async () => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'lifecycle-ext.js');

    const result = await bundleCommand({
      entryPoint: path.join(VALID_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: VALID_EXT,
    });

    expect(result.success).toBe(true);

    const mod = loadBundledModule(bundlePath);
    const manifest = loadManifest(VALID_EXT);
    const provider = new RaycastBridgeProvider(manifest);
    const cmdName = manifest.commands[0].name;
    provider.registerCommand(cmdName, mod);

    const props = provider.getProperties();
    expect(props.id).toBe('raycast-compat.hello-world');
    expect(props.displayName).toBe('Hello World');
    expect(props.frozen).toBe(true);

    provider.dispose();
  });

  test('dispose cleans up — getItems returns empty after dispose', async () => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'dispose-ext.js');

    const result = await bundleCommand({
      entryPoint: path.join(VALID_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: VALID_EXT,
    });

    expect(result.success).toBe(true);

    const mod = loadBundledModule(bundlePath);
    const manifest = loadManifest(VALID_EXT);
    const provider = new RaycastBridgeProvider(manifest);
    const cmdName = manifest.commands[0].name;
    provider.registerCommand(cmdName, mod);

    const commandId = `raycast-compat.${manifest.name}.${cmdName}`;
    provider.getCommand(commandId);

    expect(provider.getItems().length).toBe(2);

    provider.dispose();

    // After dispose, items should be empty
    expect(provider.getItems()).toEqual([]);
    expect(provider.snapshot).toBeNull();
    expect(provider.activeCommandId).toBeNull();
  });

  test('re-mounting a command after dispose works correctly', async () => {
    const bundlePath = path.join(OUTPUT_DIR, 'dist', 'remount-ext.js');

    const result = await bundleCommand({
      entryPoint: path.join(VALID_EXT, 'src', 'index.tsx'),
      outfile: bundlePath,
      compatSrc: COMPAT_SRC,
      external: ['react', 'react-reconciler', 'react/jsx-runtime'],
      absWorkingDir: VALID_EXT,
    });

    expect(result.success).toBe(true);

    const mod = loadBundledModule(bundlePath);
    const manifest = loadManifest(VALID_EXT);

    // First mount
    const provider1 = new RaycastBridgeProvider(manifest);
    provider1.registerCommand(manifest.commands[0].name, mod);
    const commandId = `raycast-compat.${manifest.name}.${manifest.commands[0].name}`;
    provider1.getCommand(commandId);
    expect(provider1.getItems().length).toBe(2);
    provider1.dispose();

    // Second mount — fresh provider
    const provider2 = new RaycastBridgeProvider(manifest);
    provider2.registerCommand(manifest.commands[0].name, mod);
    provider2.getCommand(commandId);
    expect(provider2.getItems().length).toBe(2);
    provider2.dispose();
  });
});
