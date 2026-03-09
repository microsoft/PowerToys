// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Extension management — list installed and uninstall Raycast-compat extensions.
 *
 * Raycast-compat extensions are identified by the presence of a
 * `raycast-compat.json` file with `installedBy: "raycast-pipeline"`.
 */

import * as fs from 'fs';
import * as path from 'path';
import { getDefaultInstallDir } from './stage-install';
import type { InstalledExtension, CmdPalManifest } from './types';

/**
 * List all Raycast-compat extensions installed in the CmdPal JSExtensions dir.
 *
 * Scans each subdirectory for both cmdpal.json and raycast-compat.json.
 * Only returns extensions that have the `installedBy: "raycast-pipeline"` marker.
 */
export function listInstalledExtensions(
  installDir?: string,
): InstalledExtension[] {
  const baseDir = installDir ?? getDefaultInstallDir();

  if (!fs.existsSync(baseDir)) {
    return [];
  }

  const extensions: InstalledExtension[] = [];
  const entries = fs.readdirSync(baseDir, { withFileTypes: true });

  for (const entry of entries) {
    if (!entry.isDirectory()) continue;

    const extDir = path.join(baseDir, entry.name);
    const manifestPath = path.join(extDir, 'cmdpal.json');
    const compatPath = path.join(extDir, 'raycast-compat.json');

    // Must have both cmdpal.json and raycast-compat.json
    if (!fs.existsSync(manifestPath) || !fs.existsSync(compatPath)) {
      continue;
    }

    try {
      const manifest: CmdPalManifest = JSON.parse(
        fs.readFileSync(manifestPath, 'utf-8'),
      );
      const compat = JSON.parse(
        fs.readFileSync(compatPath, 'utf-8'),
      ) as { raycastOriginalName?: string; installedBy?: string };

      // Only include extensions installed by the pipeline
      if (compat.installedBy !== 'raycast-pipeline') {
        continue;
      }

      extensions.push({
        name: manifest.name,
        raycastName: compat.raycastOriginalName ?? entry.name,
        displayName: manifest.displayName ?? manifest.name,
        version: manifest.version ?? 'unknown',
        path: extDir,
      });
    } catch {
      // Skip directories with invalid manifests
      continue;
    }
  }

  return extensions;
}

/**
 * Uninstall a Raycast-compat extension from CmdPal.
 *
 * Removes the entire extension directory. Accepts either the CmdPal name
 * (prefixed with "raycast-") or the original Raycast extension name.
 *
 * @returns true if the extension was found and removed
 */
export function uninstallExtension(
  extensionName: string,
  installDir?: string,
): boolean {
  const baseDir = installDir ?? getDefaultInstallDir();

  // Normalize: try with and without the "raycast-" prefix
  const candidates = [
    extensionName,
    extensionName.startsWith('raycast-')
      ? extensionName
      : `raycast-${extensionName}`,
  ];

  // Also check installed extensions to match by raycast original name
  const installed = listInstalledExtensions(baseDir);

  for (const ext of installed) {
    if (
      candidates.includes(ext.name) ||
      candidates.includes(ext.raycastName)
    ) {
      try {
        fs.rmSync(ext.path, { recursive: true, force: true });
        return true;
      } catch {
        return false;
      }
    }
  }

  // Fallback: try direct directory removal
  for (const name of candidates) {
    const extDir = path.join(baseDir, name);
    if (fs.existsSync(extDir)) {
      try {
        fs.rmSync(extDir, { recursive: true, force: true });
        return true;
      } catch {
        return false;
      }
    }
  }

  return false;
}
