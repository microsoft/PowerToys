// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Stage 4: Build — Bundles the Raycast extension using esbuild with
 * @raycast/api → compat layer aliasing, then generates the CmdPal manifest.
 */

import * as fs from 'fs';
import * as path from 'path';
import { bundleExtension, COMPAT_ROOT, type BundleExtensionResult } from '@cmdpal/raycast-bundler';

export interface BuildResult {
  /** Whether the build succeeded. */
  success: boolean;
  /** Bundler result details. */
  bundleResult: BundleExtensionResult;
  /** Path to the generated cmdpal.json. */
  manifestPath: string;
  /** Errors encountered during building. */
  errors: string[];
}

interface RaycastManifest {
  name?: string;
  title?: string;
  description?: string;
  icon?: string;
  author?: string;
  owner?: string;
  version?: string;
  commands?: Array<{ name: string; title?: string; mode?: string }>;
  preferences?: Array<{ name: string; type: string; [key: string]: unknown }>;
  platforms?: string[];
}

interface CmdPalManifest {
  name: string;
  displayName?: string;
  version?: string;
  description?: string;
  icon?: string;
  main: string;
  publisher?: string;
  engines?: { node?: string };
  capabilities?: string[];
}

interface RaycastCompatMetadata {
  raycastOriginalName: string;
  commands: Array<{ name: string; [key: string]: unknown }>;
  preferences: Array<{ name: string; [key: string]: unknown }>;
  platforms: string[];
  installedBy: string;
}

/**
 * Build a Raycast extension: bundle with esbuild + generate manifests.
 *
 * 1. Runs the bundler (esbuild with @raycast/api aliasing) on all commands
 * 2. Translates package.json → cmdpal.json
 * 3. Generates raycast-compat.json for the runtime compat layer
 * 4. Copies assets
 */
export async function buildExtension(
  extensionDir: string,
  outputDir: string,
): Promise<BuildResult> {
  const errors: string[] = [];

  // Read the Raycast manifest
  const pkgPath = path.join(extensionDir, 'package.json');
  const pkg: RaycastManifest = JSON.parse(
    fs.readFileSync(pkgPath, 'utf-8'),
  );

  // Ensure output dist directory exists
  fs.mkdirSync(path.join(outputDir, 'dist'), { recursive: true });

  // 1. Bundle all commands via esbuild
  const bundleResult = await bundleExtension({
    extensionDir,
    outputDir,
  });

  if (!bundleResult.success) {
    for (const cmd of bundleResult.commands) {
      for (const err of cmd.result.errors) {
        errors.push(`Bundle error in "${cmd.name}": ${err.text}`);
      }
    }
  }

  // 2. Translate manifest
  const commands = pkg.commands ?? [];
  const capabilities = new Set<string>(['commands']);
  for (const cmd of commands) {
    if (cmd.mode === 'view') {
      capabilities.add('listPages');
    }
  }

  const icon = pkg.icon
    ? pkg.icon.includes('/') || pkg.icon.includes('\\')
      ? pkg.icon
      : `assets/${pkg.icon}`
    : undefined;

  const firstCommand = commands[0]?.name ?? 'index';

  const cmdpalManifest: CmdPalManifest = {
    name: `raycast-${pkg.name ?? 'unknown'}`,
    displayName: pkg.title ?? pkg.name,
    version: pkg.version ?? '1.0.0',
    description: pkg.description,
    icon,
    main: 'index.js',
    publisher: pkg.author ?? pkg.owner,
    engines: { node: '>=22' },
    capabilities: Array.from(capabilities),
  };

  // 3. Write cmdpal.json
  const manifestPath = path.join(outputDir, 'cmdpal.json');
  fs.writeFileSync(
    manifestPath,
    JSON.stringify(cmdpalManifest, null, 2) + '\n',
    'utf-8',
  );

  // 4. Write raycast-compat.json (runtime metadata + install marker)
  const compatMetadata: RaycastCompatMetadata = {
    raycastOriginalName: pkg.name ?? '',
    commands: commands as Array<{ name: string; [key: string]: unknown }>,
    preferences: (pkg.preferences ?? []) as Array<{ name: string; [key: string]: unknown }>,
    platforms: pkg.platforms ?? ['macOS'],
    installedBy: 'raycast-pipeline',
  };

  fs.writeFileSync(
    path.join(outputDir, 'raycast-compat.json'),
    JSON.stringify(compatMetadata, null, 2) + '\n',
    'utf-8',
  );

  // 5. Copy assets
  const assetsDir = path.join(extensionDir, 'assets');
  if (fs.existsSync(assetsDir)) {
    const outAssets = path.join(outputDir, 'assets');
    fs.mkdirSync(outAssets, { recursive: true });

    const files = fs.readdirSync(assetsDir);
    for (const file of files) {
      const src = path.join(assetsDir, file);
      if (fs.statSync(src).isFile()) {
        fs.copyFileSync(src, path.join(outAssets, file));
      }
    }
  }

  // 6. Copy the bridge runtime from the compat layer dist/
  copyBridgeRuntime(outputDir);

  // 7. Copy React and react-reconciler to the output node_modules/
  copyRuntimeDependencies(outputDir);

  // 8. Generate wrapper entry point (index.js) that boots the bridge
  generateWrapperEntryPoint(outputDir, firstCommand);

  return {
    success: bundleResult.success && errors.length === 0,
    bundleResult,
    manifestPath,
    errors,
  };
}

// ── Bridge + runtime dependency helpers ────────────────────────────────

/**
 * Copy the compiled bridge runtime from the compat layer's dist/ into
 * the build output's bridge/ directory. Also copies the supporting
 * compat modules (api-stubs, reconciler, translator, components) that
 * the bridge imports via relative paths.
 */
function copyBridgeRuntime(outputDir: string): void {
  const compatDist = path.join(COMPAT_ROOT, 'dist');
  const outBridge = path.join(outputDir, 'bridge');
  fs.mkdirSync(outBridge, { recursive: true });

  // Copy bridge/*.js files
  const bridgeSrc = path.join(compatDist, 'bridge');
  if (fs.existsSync(bridgeSrc)) {
    copyDirRecursive(bridgeSrc, outBridge);
  }

  // The bridge imports ../api-stubs, ../reconciler, ../translator, ../components
  // via relative paths from bridge/index.js. Copy those directories too.
  const supportDirs = ['api-stubs', 'reconciler', 'translator', 'components', 'utils-shim.js', 'utils-shim.js.map'];
  for (const name of supportDirs) {
    const src = path.join(compatDist, name);
    if (fs.existsSync(src)) {
      const dest = path.join(outputDir, 'bridge', '..', name);
      if (fs.statSync(src).isDirectory()) {
        copyDirRecursive(src, dest);
      } else {
        fs.copyFileSync(src, dest);
      }
    }
  }
}

/**
 * Copy react and react-reconciler from the compat layer's node_modules
 * to the output directory's node_modules. This ensures `require('react')`
 * resolves at runtime when the bridge loads the bundled extension.
 */
function copyRuntimeDependencies(outputDir: string): void {
  const compatNodeModules = path.join(COMPAT_ROOT, 'node_modules');
  const outNodeModules = path.join(outputDir, 'node_modules');
  fs.mkdirSync(outNodeModules, { recursive: true });

  const deps = ['react', 'react-reconciler', 'scheduler'];
  for (const dep of deps) {
    const src = path.join(compatNodeModules, dep);
    const dest = path.join(outNodeModules, dep);
    if (fs.existsSync(src)) {
      copyDirRecursive(src, dest);
    }
  }
}

/**
 * Generate a small index.js wrapper that boots the bridge runtime.
 * CmdPal runs `node "index.js"` and this wrapper auto-detects paths.
 */
function generateWrapperEntryPoint(
  outputDir: string,
  defaultCommand: string,
): void {
  const wrapper = `// Auto-generated bridge wrapper — do not edit
'use strict';
const bridge = require('./bridge/index');
const { transport } = bridge.boot({ extensionDir: __dirname, command: '${defaultCommand}' });
transport.start();
`;
  fs.writeFileSync(path.join(outputDir, 'index.js'), wrapper, 'utf-8');
}

/** Recursively copy a directory. */
function copyDirRecursive(src: string, dest: string): void {
  fs.mkdirSync(dest, { recursive: true });
  const entries = fs.readdirSync(src, { withFileTypes: true });
  for (const entry of entries) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDirRecursive(srcPath, destPath);
    } else {
      fs.copyFileSync(srcPath, destPath);
    }
  }
}
