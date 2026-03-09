// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Stage 2: Validate — Checks the downloaded Raycast extension for
 * Windows compatibility and required fields before proceeding.
 */

import * as fs from 'fs';
import * as path from 'path';

export interface ValidationResult {
  /** Whether validation passed. */
  valid: boolean;
  /** Blocking errors that prevent installation. */
  errors: string[];
  /** Non-blocking warnings. */
  warnings: string[];
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
  preferences?: unknown[];
  platforms?: string[];
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
}

/**
 * Validate a downloaded Raycast extension for CmdPal compatibility.
 *
 * Checks:
 * 1. package.json exists and is parseable
 * 2. Required fields present (name, title, commands)
 * 3. Platform includes "Windows" (Raycast defaults to macOS-only)
 * 4. At least one command entry
 */
export function validateExtension(extensionDir: string): ValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];

  // 1. Check package.json exists
  const pkgPath = path.join(extensionDir, 'package.json');
  if (!fs.existsSync(pkgPath)) {
    return {
      valid: false,
      errors: ['package.json not found in extension directory'],
      warnings,
    };
  }

  // 2. Parse package.json
  let manifest: RaycastManifest;
  try {
    const raw = fs.readFileSync(pkgPath, 'utf-8');
    manifest = JSON.parse(raw) as RaycastManifest;
  } catch {
    return {
      valid: false,
      errors: ['Failed to parse package.json — invalid JSON'],
      warnings,
    };
  }

  // 3. Required fields
  if (!manifest.name || manifest.name.trim() === '') {
    errors.push("Missing required field: 'name'");
  }

  if (!manifest.title || manifest.title.trim() === '') {
    errors.push("Missing required field: 'title'");
  }

  if (!manifest.commands || manifest.commands.length === 0) {
    errors.push("Missing required field: 'commands' (must have at least one)");
  }

  // 4. Platform check — Raycast defaults to ["macOS"] when absent
  const platforms = manifest.platforms ?? ['macOS'];
  const hasWindows = platforms.some(
    (p) => p.toLowerCase() === 'windows',
  );

  if (!hasWindows) {
    errors.push(
      `Platform rejection: extension supports [${platforms.join(', ')}]. ` +
      "'Windows' is required.",
    );
  }

  // 5. Validate individual commands
  if (manifest.commands) {
    for (let i = 0; i < manifest.commands.length; i++) {
      const cmd = manifest.commands[i];
      if (!cmd.name || cmd.name.trim() === '') {
        errors.push(`commands[${i}]: missing required field 'name'`);
      }
    }
  }

  // Warnings for optional but recommended fields
  if (!manifest.description) {
    warnings.push("Missing optional field: 'description'");
  }

  if (!manifest.author && !manifest.owner) {
    warnings.push("Missing optional field: 'author'");
  }

  if (!manifest.icon) {
    warnings.push("Missing optional field: 'icon'");
  }

  if (!manifest.version) {
    warnings.push("Missing optional field: 'version' — will default to 1.0.0");
  }

  return { valid: errors.length === 0, errors, warnings };
}
