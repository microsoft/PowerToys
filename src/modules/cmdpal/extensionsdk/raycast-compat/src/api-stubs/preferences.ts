// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast preferences compatibility stub.
 *
 * Maps Raycast's `getPreferenceValues<T>()` and `openExtensionPreferences()`
 * to CmdPal's file-based preferences system.
 *
 * Preferences are read from a JSON file that's populated from the Raycast
 * manifest's `preferences[]` during install (via the manifest translator).
 * The file lives at `<supportPath>/preferences.json`.
 */

import * as fs from 'fs';
import * as path from 'path';

let preferencesPath: string | null = null;
let cachedPrefs: Record<string, unknown> | null = null;

/** Internal: set the preferences file path (called by environment setup). */
export function _setPreferencesPath(supportPath: string): void {
  preferencesPath = path.join(supportPath, 'preferences.json');
  cachedPrefs = null; // Force reload
}

function getPrefsFile(): string {
  if (!preferencesPath) {
    const fallback = path.join(
      process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.',
      'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions', '_raycast-compat',
    );
    preferencesPath = path.join(fallback, 'preferences.json');
  }
  return preferencesPath;
}

function loadPreferences(): Record<string, unknown> {
  if (cachedPrefs !== null) return cachedPrefs;

  const file = getPrefsFile();
  try {
    if (fs.existsSync(file)) {
      const raw = fs.readFileSync(file, 'utf-8');
      cachedPrefs = JSON.parse(raw) as Record<string, unknown>;
    } else {
      cachedPrefs = {};
    }
  } catch {
    console.warn('[Preferences] Failed to read preferences file, using empty defaults');
    cachedPrefs = {};
  }
  return cachedPrefs;
}

/**
 * Returns the extension's preference values.
 *
 * Raycast extensions call this as `getPreferenceValues<T>()` to get typed
 * preferences. We read from the JSON file and return the raw object.
 */
export function getPreferenceValues<T extends Record<string, unknown> = Record<string, unknown>>(): T {
  return loadPreferences() as T;
}

/**
 * Opens the extension's preferences panel.
 * Stub: logs a warning. CmdPal will handle this through the settings UI.
 */
export async function openExtensionPreferences(): Promise<void> {
  console.warn('[Preferences] openExtensionPreferences() is not yet supported in CmdPal');
}

/**
 * Opens the command's preferences panel.
 * Stub: logs a warning. CmdPal will handle this through the settings UI.
 */
export async function openCommandPreferences(): Promise<void> {
  console.warn('[Preferences] openCommandPreferences() is not yet supported in CmdPal');
}
