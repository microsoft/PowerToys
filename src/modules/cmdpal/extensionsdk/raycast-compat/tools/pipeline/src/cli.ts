#!/usr/bin/env node

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * CLI for the Raycast → CmdPal install pipeline.
 *
 * Usage:
 *   node pipeline.js install <extension-name> [--token <github-token>] [--output <dir>]
 *   node pipeline.js uninstall <extension-name> [--dir <install-dir>]
 *   node pipeline.js list [--dir <install-dir>]
 */

import { installRaycastExtension } from './pipeline';
import { listInstalledExtensions, uninstallExtension } from './manage';
import { getDefaultInstallDir } from './stage-install';

// ── Argument parsing ─────────────────────────────────────────────────

interface CliOptions {
  command: 'install' | 'uninstall' | 'list' | 'help';
  extensionName?: string;
  githubToken?: string;
  outputDir?: string;
}

function parseArgs(): CliOptions {
  const args = process.argv.slice(2);

  if (args.length === 0 || args.includes('--help') || args.includes('-h')) {
    return { command: 'help' };
  }

  const command = args[0] as string;
  let extensionName: string | undefined;
  let githubToken: string | undefined;
  let outputDir: string | undefined;

  for (let i = 1; i < args.length; i++) {
    switch (args[i]) {
      case '--token':
      case '-t':
        githubToken = args[++i];
        break;
      case '--output':
      case '--dir':
      case '-o':
        outputDir = args[++i];
        break;
      default:
        if (!args[i].startsWith('-')) {
          extensionName = args[i];
        }
        break;
    }
  }

  if (command === 'list') {
    return { command: 'list', outputDir };
  }

  if (command === 'install' || command === 'uninstall') {
    return { command, extensionName, githubToken, outputDir };
  }

  return { command: 'help' };
}

// ── Commands ─────────────────────────────────────────────────────────

async function cmdInstall(options: CliOptions): Promise<void> {
  if (!options.extensionName) {
    console.error('Error: extension name required');
    console.error('Usage: pipeline install <extension-name>');
    process.exit(1);
  }

  console.log('');
  console.log('═══════════════════════════════════════════════════');
  console.log('  Raycast → CmdPal Install Pipeline');
  console.log('═══════════════════════════════════════════════════');
  console.log('');
  console.log(`Extension: ${options.extensionName}`);
  console.log('');

  const result = await installRaycastExtension({
    extensionName: options.extensionName,
    githubToken: options.githubToken,
    outputDir: options.outputDir,
    onProgress: (stage, detail) => {
      console.log(`  [${stage}] ${detail}`);
    },
  });

  console.log('');
  console.log('─── Stage Summary ────────────────────────────────');
  for (const stage of result.stages) {
    const icon =
      stage.status === 'success'
        ? '✓'
        : stage.status === 'failed'
          ? '✗'
          : '○';
    const duration =
      stage.duration > 0 ? ` (${(stage.duration / 1000).toFixed(1)}s)` : '';
    console.log(`  ${icon} ${stage.name}${duration}`);
    if (stage.detail && stage.status === 'failed') {
      console.log(`    ${stage.detail}`);
    }
  }
  console.log('');

  if (result.success) {
    console.log(`✓ Installed to: ${result.extensionPath}`);
  } else {
    console.error(`✗ Installation failed: ${result.error}`);

    // Provide actionable hints for common failures
    if (result.error && /rate.?limit/i.test(result.error)) {
      if (!options.githubToken && !process.env.GITHUB_TOKEN) {
        console.error('');
        console.error(
          'Hint: Set a GITHUB_TOKEN environment variable to increase the rate limit from 60 to 5,000 requests/hour.',
        );
        console.error(
          '  Create a token at: https://github.com/settings/tokens (public_repo scope)',
        );
      }
    } else if (result.error && /404|not found/i.test(result.error)) {
      console.error('');
      console.error(
        `Hint: Extension "${options.extensionName}" may not exist. Check https://github.com/raycast/extensions/tree/main/extensions`,
      );
    }

    process.exit(1);
  }
}

function cmdUninstall(options: CliOptions): void {
  if (!options.extensionName) {
    console.error('Error: extension name required');
    console.error('Usage: pipeline uninstall <extension-name>');
    process.exit(1);
  }

  const removed = uninstallExtension(options.extensionName, options.outputDir);

  if (removed) {
    console.log(`✓ Uninstalled "${options.extensionName}"`);
  } else {
    console.error(`✗ Extension "${options.extensionName}" not found`);
    process.exit(1);
  }
}

function cmdList(options: CliOptions): void {
  const extensions = listInstalledExtensions(options.outputDir);

  if (extensions.length === 0) {
    console.log('No Raycast-compat extensions installed.');
    const dir = options.outputDir ?? getDefaultInstallDir();
    console.log(`  (looked in: ${dir})`);
    return;
  }

  console.log('');
  console.log('Installed Raycast-compat extensions:');
  console.log('');

  for (const ext of extensions) {
    console.log(`  ${ext.displayName} (${ext.name})`);
    console.log(`    Raycast name: ${ext.raycastName}`);
    console.log(`    Version:      ${ext.version}`);
    console.log(`    Path:         ${ext.path}`);
    console.log('');
  }

  console.log(`Total: ${extensions.length} extension(s)`);
}

function printUsage(): void {
  console.log(`
Raycast → CmdPal Extension Pipeline

Usage:
  pipeline install <name>     Download, build, and install a Raycast extension
  pipeline uninstall <name>   Remove an installed Raycast-compat extension
  pipeline list               List installed Raycast-compat extensions

Install options:
  --token, -t <token>   GitHub personal access token (for rate limits)
  --output, -o <dir>    Override install directory

List/Uninstall options:
  --dir, -o <dir>       Override install directory to scan

Examples:
  node pipeline.js install clipboard-history
  node pipeline.js install color-picker --token ghp_xxxxx
  node pipeline.js uninstall clipboard-history
  node pipeline.js list
`);
}

// ── Entry point ──────────────────────────────────────────────────────

async function main(): Promise<void> {
  const options = parseArgs();

  switch (options.command) {
    case 'install':
      await cmdInstall(options);
      break;
    case 'uninstall':
      cmdUninstall(options);
      break;
    case 'list':
      cmdList(options);
      break;
    case 'help':
    default:
      printUsage();
      break;
  }
}

main().catch((err) => {
  console.error('Fatal error:', err instanceof Error ? err.message : err);
  process.exit(1);
});
