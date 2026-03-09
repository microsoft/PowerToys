#!/usr/bin/env node

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * CLI tool to bundle a Raycast extension for CmdPal.
 *
 * Usage:
 *   node bundle-extension.js --input <raycast-ext-dir> --output <cmdpal-ext-dir>
 *
 * Steps:
 *   1. Read the Raycast extension's package.json
 *   2. Determine entry points from the commands[] array
 *   3. Run esbuild with @raycast/api → compat layer aliasing
 *   4. Translate the manifest (package.json → cmdpal.json)
 *   5. Copy assets to the output directory
 *   6. Report success/failure
 */

import * as path from 'path';
import * as fs from 'fs';
import { bundleExtension } from './build';
import type { BundleExtensionResult } from './build';

// ── Types ──────────────────────────────────────────────────────────────

interface CliArgs {
  input: string;
  output: string;
  minify: boolean;
}

interface RaycastCommand {
  name: string;
  title?: string;
  description?: string;
  mode?: string;
  icon?: string;
  keywords?: string[];
}

interface RaycastManifest {
  name?: string;
  title?: string;
  description?: string;
  icon?: string;
  author?: string;
  owner?: string;
  version?: string;
  commands?: RaycastCommand[];
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

// ── Argument parsing ───────────────────────────────────────────────────

function parseArgs(): CliArgs {
  const args = process.argv.slice(2);
  let input = '';
  let output = '';
  let minify = false;

  for (let i = 0; i < args.length; i++) {
    switch (args[i]) {
      case '--input':
      case '-i':
        input = args[++i];
        break;
      case '--output':
      case '-o':
        output = args[++i];
        break;
      case '--minify':
        minify = true;
        break;
      case '--help':
      case '-h':
        printUsage();
        process.exit(0);
    }
  }

  if (!input || !output) {
    printUsage();
    process.exit(1);
  }

  return {
    input: path.resolve(input),
    output: path.resolve(output),
    minify,
  };
}

function printUsage(): void {
  console.log(`
Usage: bundle-extension --input <raycast-ext-dir> --output <cmdpal-ext-dir>

Options:
  --input, -i    Path to the Raycast extension directory (must contain package.json)
  --output, -o   Path to the CmdPal extension output directory
  --minify       Minify the bundled output
  --help, -h     Show this help message

Example:
  node bundle-extension.js --input ./extensions/my-ext --output ./dist/my-ext
`);
}

// ── Manifest translation (inline, lightweight) ─────────────────────────

function translateManifest(pkg: RaycastManifest): CmdPalManifest {
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

  // First command becomes the main entry point
  const mainCommand = commands[0]?.name ?? 'index';

  return {
    name: `raycast-${pkg.name ?? 'unknown'}`,
    displayName: pkg.title ?? pkg.name,
    version: pkg.version ?? '1.0.0',
    description: pkg.description,
    icon,
    main: `dist/${mainCommand}.js`,
    publisher: pkg.author ?? pkg.owner,
    engines: { node: '>=22' },
    capabilities: Array.from(capabilities),
  };
}

// ── Asset copying ──────────────────────────────────────────────────────

function copyAssets(inputDir: string, outputDir: string): void {
  const assetsDir = path.join(inputDir, 'assets');
  if (!fs.existsSync(assetsDir)) return;

  const outAssets = path.join(outputDir, 'assets');
  fs.mkdirSync(outAssets, { recursive: true });

  const files = fs.readdirSync(assetsDir);
  for (const file of files) {
    const src = path.join(assetsDir, file);
    const dest = path.join(outAssets, file);
    if (fs.statSync(src).isFile()) {
      fs.copyFileSync(src, dest);
    }
  }
}

// ── Reporter ───────────────────────────────────────────────────────────

function reportResults(result: BundleExtensionResult): void {
  console.log('');
  console.log('═══════════════════════════════════════════════════');
  console.log('  Raycast → CmdPal Extension Bundler');
  console.log('═══════════════════════════════════════════════════');
  console.log('');

  for (const cmd of result.commands) {
    const status = cmd.result.success ? '✓' : '✗';
    console.log(`  ${status} ${cmd.name}`);

    if (cmd.result.success) {
      console.log(`    → ${cmd.result.outfile}`);
    }

    for (const err of cmd.result.errors) {
      console.error(`    ERROR: ${err.text}`);
    }
    for (const warn of cmd.result.warnings) {
      console.warn(`    WARN: ${warn.text}`);
    }
  }

  console.log('');
  if (result.success) {
    console.log('  Bundle complete ✓');
  } else {
    console.error('  Bundle failed ✗');
  }
  console.log('');
}

// ── Main ───────────────────────────────────────────────────────────────

async function main(): Promise<void> {
  const args = parseArgs();

  // 1. Validate input
  const pkgPath = path.join(args.input, 'package.json');
  if (!fs.existsSync(pkgPath)) {
    console.error(`Error: No package.json found at ${pkgPath}`);
    process.exit(1);
  }

  const pkg: RaycastManifest = JSON.parse(
    fs.readFileSync(pkgPath, 'utf-8'),
  );

  console.log(`Bundling: ${pkg.title ?? pkg.name ?? 'unknown extension'}`);
  console.log(`  Input:  ${args.input}`);
  console.log(`  Output: ${args.output}`);

  // 2. Create output directory
  fs.mkdirSync(path.join(args.output, 'dist'), { recursive: true });

  // 3. Bundle all commands
  const result = await bundleExtension({
    extensionDir: args.input,
    outputDir: args.output,
    minify: args.minify,
  });

  // 4. Translate and write manifest
  const cmdpalManifest = translateManifest(pkg);
  const manifestPath = path.join(args.output, 'cmdpal.json');
  fs.writeFileSync(manifestPath, JSON.stringify(cmdpalManifest, null, 2));
  console.log(`  Manifest: ${manifestPath}`);

  // 5. Copy assets
  copyAssets(args.input, args.output);

  // 6. Report
  reportResults(result);

  process.exit(result.success ? 0 : 1);
}

main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});
