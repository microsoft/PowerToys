// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast LocalStorage compatibility stub.
 *
 * File-backed key-value store using a JSON file in the extension's data
 * directory. The `supportPath` from the environment module determines where
 * the storage file lives.
 *
 * Thread-safety note: Node.js is single-threaded, so no locking needed.
 * The file is read lazily on first access and written on every mutation
 * to ensure persistence across process restarts.
 */

import * as fs from 'fs';
import * as path from 'path';

let storePath: string | null = null;
let cache: Record<string, string> | null = null;

/** Internal: set the storage directory (called by environment setup). */
export function _setStoragePath(dir: string): void {
  storePath = path.join(dir, 'local-storage.json');
  cache = null; // Force reload on next access
}

function getStoreFile(): string {
  if (!storePath) {
    // Fallback to temp directory if environment hasn't been configured
    const fallback = path.join(
      process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.',
      'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions', '_raycast-compat',
    );
    storePath = path.join(fallback, 'local-storage.json');
  }
  return storePath;
}

function ensureDir(filePath: string): void {
  const dir = path.dirname(filePath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
}

function load(): Record<string, string> {
  if (cache !== null) return cache;

  const file = getStoreFile();
  try {
    if (fs.existsSync(file)) {
      const raw = fs.readFileSync(file, 'utf-8');
      cache = JSON.parse(raw) as Record<string, string>;
    } else {
      cache = {};
    }
  } catch {
    console.warn('[LocalStorage] Failed to read storage file, starting empty');
    cache = {};
  }
  return cache;
}

function save(): void {
  const file = getStoreFile();
  try {
    ensureDir(file);
    fs.writeFileSync(file, JSON.stringify(cache, null, 2), 'utf-8');
  } catch (err) {
    console.warn('[LocalStorage] Failed to persist storage:', err);
  }
}

export const LocalStorage = {
  /**
   * Retrieve a value by key. Returns `undefined` if not found.
   */
  async getItem(key: string): Promise<string | undefined> {
    const store = load();
    return store[key];
  },

  /**
   * Store a value. Values are JSON-stringified if not already strings.
   */
  async setItem(key: string, value: unknown): Promise<void> {
    const store = load();
    store[key] = typeof value === 'string' ? value : JSON.stringify(value);
    save();
  },

  /**
   * Remove a key from storage.
   */
  async removeItem(key: string): Promise<void> {
    const store = load();
    delete store[key];
    save();
  },

  /**
   * Return all key-value pairs.
   */
  async allItems(): Promise<Record<string, string>> {
    return { ...load() };
  },

  /**
   * Clear all stored data.
   */
  async clear(): Promise<void> {
    cache = {};
    save();
  },
};
