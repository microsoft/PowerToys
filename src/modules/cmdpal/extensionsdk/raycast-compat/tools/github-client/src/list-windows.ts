#!/usr/bin/env node
// Copyright (c) Microsoft Corporation
// Licensed under the MIT License.

/**
 * Sample script: list Windows-compatible Raycast extensions.
 *
 * Usage:
 *   GITHUB_TOKEN=ghp_xxx node dist/list-windows.js [--limit N] [--search query]
 *
 * Without GITHUB_TOKEN, API rate limit is 60 requests/hour.
 */

import { RaycastExtensionsClient, RateLimitError } from './client';

async function main(): Promise<void> {
  const args = process.argv.slice(2);
  const limitIdx = args.indexOf('--limit');
  const limit = limitIdx >= 0 ? parseInt(args[limitIdx + 1], 10) : 20;
  const searchIdx = args.indexOf('--search');
  const search = searchIdx >= 0 ? args[searchIdx + 1] : undefined;

  const client = new RaycastExtensionsClient();

  const tokenType = process.env.GITHUB_TOKEN ? 'authenticated' : 'unauthenticated';
  console.log(`GitHub API mode: ${tokenType}`);

  try {
    if (search) {
      console.log(`\nSearching for "${search}"...`);
      const results = await client.searchExtensions(search, {
        windowsOnly: true,
        limit,
      });
      console.log(`Found ${results.length} Windows-compatible results:\n`);
      for (const ext of results) {
        const manifest = await client.getManifest(ext.name);
        console.log(
          `  ${ext.name} — ${manifest?.title ?? '(no title)'}: ${manifest?.description ?? '(no description)'}`,
        );
      }
    } else {
      console.log(`\nListing extensions (first ${limit})...`);
      const all = await client.listExtensions();
      console.log(`Total extensions in repo: ${all.length}`);

      const sample = all.slice(0, limit);
      let windowsCount = 0;
      for (const ext of sample) {
        const manifest = await client.getManifest(ext.name);
        if (manifest) {
          const isWindows = client.isWindowsCompatible(manifest);
          if (isWindows) windowsCount++;
          const marker = isWindows ? '✓' : '✗';
          console.log(
            `  [${marker}] ${ext.name} — ${manifest.title ?? '(no title)'}`,
          );
        } else {
          console.log(`  [?] ${ext.name} — (no manifest)`);
        }
      }
      console.log(
        `\nWindows-compatible: ${windowsCount}/${sample.length} checked`,
      );
    }

    if (client.rateLimit) {
      console.log(
        `\nRate limit: ${client.rateLimit.remaining}/${client.rateLimit.limit} remaining (resets ${client.rateLimit.reset.toLocaleTimeString()})`,
      );
    }
  } catch (err) {
    if (err instanceof RateLimitError) {
      console.error(`\n⚠ Rate limited! Resets at ${err.resetAt.toISOString()}`);
      console.error('Set GITHUB_TOKEN for 5000 req/hour (vs 60 unauthenticated).');
      process.exit(1);
    }
    throw err;
  }
}

main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});
