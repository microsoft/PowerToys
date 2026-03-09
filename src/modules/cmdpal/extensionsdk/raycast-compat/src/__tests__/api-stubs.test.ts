// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

import {
  showToast, Toast, ToastStyle,
  Clipboard,
  LocalStorage,
  environment, LaunchType, _configureEnvironment,
  getPreferenceValues, openExtensionPreferences,
  Icon, resolveIcon,
  Color, ColorDynamic, resolveColor,
  AI,
  confirmAlert,
  _setStoragePath, _setPreferencesPath,
} from '../api-stubs';

// ── Toast ──────────────────────────────────────────────────────────────

describe('Toast', () => {
  beforeEach(() => jest.spyOn(console, 'log').mockImplementation());
  afterEach(() => jest.restoreAllMocks());

  it('showToast returns a Toast instance with correct fields', async () => {
    const toast = await showToast({
      style: ToastStyle.Success,
      title: 'Done',
      message: 'All good',
    });
    expect(toast).toBeInstanceOf(Toast);
    expect(toast.style).toBe(ToastStyle.Success);
    expect(toast.title).toBe('Done');
    expect(toast.message).toBe('All good');
  });

  it('showToast supports legacy 3-arg form', async () => {
    const toast = await showToast(ToastStyle.Failure, 'Error', 'Something broke');
    expect(toast.style).toBe(ToastStyle.Failure);
    expect(toast.title).toBe('Error');
    expect(toast.message).toBe('Something broke');
  });

  it('Toast.Style enum has all expected values', () => {
    expect(Toast.Style.Animated).toBe('animated');
    expect(Toast.Style.Success).toBe('success');
    expect(Toast.Style.Failure).toBe('failure');
  });

  it('toast.hide() resolves without error', async () => {
    const toast = await showToast({ title: 'Test' });
    await expect(toast.hide()).resolves.toBeUndefined();
  });
});

// ── LocalStorage ───────────────────────────────────────────────────────

describe('LocalStorage', () => {
  let tempDir: string;

  beforeEach(() => {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'raycast-compat-test-'));
    _setStoragePath(tempDir);
  });

  afterEach(() => {
    fs.rmSync(tempDir, { recursive: true, force: true });
  });

  it('setItem + getItem round-trips a string', async () => {
    await LocalStorage.setItem('key1', 'value1');
    const result = await LocalStorage.getItem('key1');
    expect(result).toBe('value1');
  });

  it('setItem JSON-stringifies non-string values', async () => {
    await LocalStorage.setItem('obj', { nested: true });
    const result = await LocalStorage.getItem('obj');
    expect(result).toBe('{"nested":true}');
  });

  it('getItem returns undefined for missing keys', async () => {
    const result = await LocalStorage.getItem('nonexistent');
    expect(result).toBeUndefined();
  });

  it('removeItem deletes a key', async () => {
    await LocalStorage.setItem('temp', 'data');
    await LocalStorage.removeItem('temp');
    expect(await LocalStorage.getItem('temp')).toBeUndefined();
  });

  it('allItems returns all stored key-value pairs', async () => {
    await LocalStorage.setItem('a', '1');
    await LocalStorage.setItem('b', '2');
    const all = await LocalStorage.allItems();
    expect(all).toEqual({ a: '1', b: '2' });
  });

  it('clear removes all data', async () => {
    await LocalStorage.setItem('x', 'y');
    await LocalStorage.clear();
    expect(await LocalStorage.allItems()).toEqual({});
  });

  it('persists data to disk', async () => {
    await LocalStorage.setItem('persist', 'test');
    const file = path.join(tempDir, 'local-storage.json');
    expect(fs.existsSync(file)).toBe(true);
    const raw = JSON.parse(fs.readFileSync(file, 'utf-8'));
    expect(raw.persist).toBe('test');
  });
});

// ── Environment ────────────────────────────────────────────────────────

describe('environment', () => {
  it('has expected default values', () => {
    expect(environment.isDevelopment).toBe(false);
    expect(environment.canAccess({})).toBe(true);
    expect(environment.canAccess(null)).toBe(false);
    expect(typeof environment.raycastVersion).toBe('string');
  });

  it('_configureEnvironment updates values', () => {
    _configureEnvironment({
      extensionName: 'my-ext',
      commandName: 'my-cmd',
    });
    expect(environment.extensionName).toBe('my-ext');
    expect(environment.commandName).toBe('my-cmd');
  });

  it('LaunchType enum has expected values', () => {
    expect(LaunchType.UserInitiated).toBe('userInitiated');
    expect(LaunchType.Background).toBe('background');
  });
});

// ── Preferences ────────────────────────────────────────────────────────

describe('Preferences', () => {
  let tempDir: string;

  beforeEach(() => {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'raycast-prefs-test-'));
    _setPreferencesPath(tempDir);
  });

  afterEach(() => {
    fs.rmSync(tempDir, { recursive: true, force: true });
  });

  it('returns empty object when no preferences file exists', () => {
    const prefs = getPreferenceValues();
    expect(prefs).toEqual({});
  });

  it('reads preferences from JSON file', () => {
    const prefsFile = path.join(tempDir, 'preferences.json');
    fs.writeFileSync(prefsFile, JSON.stringify({ apiKey: 'abc123', theme: 'dark' }));
    // Reset cache by re-setting path
    _setPreferencesPath(tempDir);
    const prefs = getPreferenceValues<{ apiKey: string; theme: string }>();
    expect(prefs.apiKey).toBe('abc123');
    expect(prefs.theme).toBe('dark');
  });

  it('openExtensionPreferences is a no-op', async () => {
    jest.spyOn(console, 'warn').mockImplementation();
    await expect(openExtensionPreferences()).resolves.toBeUndefined();
    jest.restoreAllMocks();
  });
});

// ── Icons ──────────────────────────────────────────────────────────────

describe('Icon', () => {
  it('has expected common icons', () => {
    expect(typeof Icon.Star).toBe('string');
    expect(typeof Icon.Trash).toBe('string');
    expect(typeof Icon.MagnifyingGlass).toBe('string');
    expect(typeof Icon.Globe).toBe('string');
    expect(typeof Icon.Calendar).toBe('string');
  });

  it('resolveIcon handles string values', () => {
    expect(resolveIcon('\uE734')).toBe('\uE734');
  });

  it('resolveIcon handles Image.source pattern', () => {
    expect(resolveIcon({ source: Icon.Star })).toBe(Icon.Star);
  });

  it('resolveIcon falls back for unknown types', () => {
    expect(resolveIcon(42)).toBe('\uE8A5');
    expect(resolveIcon(undefined)).toBe('\uE8A5');
  });
});

// ── Colors ─────────────────────────────────────────────────────────────

describe('Color', () => {
  it('has expected color values as hex strings', () => {
    expect(Color.Blue).toMatch(/^#[0-9A-Fa-f]{6}$/);
    expect(Color.Red).toMatch(/^#[0-9A-Fa-f]{6}$/);
    expect(Color.Green).toMatch(/^#[0-9A-Fa-f]{6}$/);
  });

  it('ColorDynamic returns light color', () => {
    expect(ColorDynamic('#FFF', '#000')).toBe('#FFF');
  });

  it('resolveColor handles hex strings', () => {
    expect(resolveColor('#FF0000')).toBe('#FF0000');
  });

  it('resolveColor handles light/dark objects', () => {
    expect(resolveColor({ light: '#FFF', dark: '#000' })).toBe('#FFF');
  });

  it('resolveColor returns undefined for non-color types', () => {
    expect(resolveColor(42)).toBeUndefined();
    expect(resolveColor(null)).toBeUndefined();
  });
});

// ── AI ─────────────────────────────────────────────────────────────────

describe('AI', () => {
  it('AI.ask throws with clear error message', async () => {
    jest.spyOn(console, 'warn').mockImplementation();
    await expect(AI.ask('Hello')).rejects.toThrow('not available in CmdPal');
    jest.restoreAllMocks();
  });
});

// ── confirmAlert ───────────────────────────────────────────────────────

describe('confirmAlert', () => {
  it('auto-confirms and returns true', async () => {
    jest.spyOn(console, 'log').mockImplementation();
    const result = await confirmAlert({ title: 'Delete?', message: 'Are you sure?' });
    expect(result).toBe(true);
    jest.restoreAllMocks();
  });
});
