// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mkdtemp, rm, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  Settings,
  ToggleSetting,
  TextSetting,
  ChoiceSetSetting,
  JsonSettingsStore,
} from '../src/index.js';

function cardInputs(settings: Settings): Record<string, Record<string, unknown>> {
  const card = JSON.parse(settings.toTemplateJson()) as { body: Record<string, unknown>[] };
  const byId: Record<string, Record<string, unknown>> = {};
  for (const element of card.body) {
    const type = element.type as string | undefined;
    const id = element.id as string | undefined;
    if (typeof type === 'string' && type.startsWith('Input.') && typeof id === 'string') {
      byId[id] = element;
    }
  }
  return byId;
}

describe('Settings Adaptive Card required inputs', () => {
  it('emits isRequired only for required inputs', () => {
    const settings = new Settings();
    const required = new TextSetting('name', 'Name');
    required.isRequired = true;
    settings.add(required);
    settings.add(new TextSetting('nickname', 'Nickname'));

    const inputs = cardInputs(settings);
    expect(inputs.name?.isRequired).toBe(true);
    expect(inputs.nickname).not.toHaveProperty('isRequired');
  });

  it('emits errorMessage alongside isRequired when provided', () => {
    const settings = new Settings();
    const required = new TextSetting('name', 'Name');
    required.isRequired = true;
    required.errorMessage = 'Name is required';
    settings.add(required);

    const inputs = cardInputs(settings);
    expect(inputs.name?.isRequired).toBe(true);
    expect(inputs.name?.errorMessage).toBe('Name is required');
  });

  it('omits errorMessage when isRequired is not set', () => {
    const settings = new Settings();
    const optional = new TextSetting('name', 'Name');
    optional.errorMessage = 'ignored when optional';
    settings.add(optional);

    const inputs = cardInputs(settings);
    expect(inputs.name).not.toHaveProperty('isRequired');
    expect(inputs.name).not.toHaveProperty('errorMessage');
  });

  it('supports required toggles and choice sets', () => {
    const settings = new Settings();
    const toggle = new ToggleSetting('agree', 'Agree');
    toggle.isRequired = true;
    settings.add(toggle);
    const choice = new ChoiceSetSetting('theme', 'Theme', [
      { title: 'Light', value: 'light' },
      { title: 'Dark', value: 'dark' },
    ]);
    choice.isRequired = true;
    settings.add(choice);

    const inputs = cardInputs(settings);
    expect(inputs.agree?.type).toBe('Input.Toggle');
    expect(inputs.agree?.isRequired).toBe(true);
    expect(inputs.theme?.type).toBe('Input.ChoiceSet');
    expect(inputs.theme?.isRequired).toBe(true);
  });
});

describe('Settings change notification', () => {
  it('fires subscribers and updates values on submit', async () => {
    const settings = new Settings();
    settings.add(new ToggleSetting('dark', 'Dark Mode', false));
    const handler = vi.fn();
    settings.onSettingsChanged(handler);

    await settings.submit({ dark: 'true' });

    expect(settings.getSetting<ToggleSetting>('dark')?.value).toBe(true);
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith(settings);
  });

  it('awaits the onSave hook after raising the change', async () => {
    const settings = new Settings();
    settings.add(new ToggleSetting('dark', 'Dark Mode', false));
    const order: string[] = [];
    settings.onSettingsChanged(() => order.push('changed'));
    settings.onSave = async () => {
      await Promise.resolve();
      order.push('saved');
    };

    await settings.submit({ dark: 'true' });

    expect(order).toEqual(['changed', 'saved']);
  });

  it('surfaces a failure thrown by onSave', async () => {
    const settings = new Settings();
    settings.add(new ToggleSetting('dark', 'Dark Mode', false));
    settings.onSave = () => {
      throw new Error('save failed');
    };

    await expect(settings.submit({ dark: 'true' })).rejects.toThrow('save failed');
  });

  it('stops notifying after unsubscribe', async () => {
    const settings = new Settings();
    settings.add(new ToggleSetting('dark', 'Dark Mode', false));
    const handler = vi.fn();
    const unsubscribe = settings.onSettingsChanged(handler);

    unsubscribe();
    await settings.submit({ dark: 'true' });

    expect(handler).not.toHaveBeenCalled();
  });
});

describe('JsonSettingsStore', () => {
  let dir: string;

  beforeEach(async () => {
    dir = await mkdtemp(join(tmpdir(), 'cmdpal-store-'));
  });

  afterEach(async () => {
    await rm(dir, { recursive: true, force: true });
  });

  it('seeds values from an existing file on load', async () => {
    const filePath = join(dir, 'settings.json');
    await writeFile(filePath, JSON.stringify({ theme: 'dark', count: 3 }), 'utf8');
    const store = new JsonSettingsStore(filePath);

    await store.load();

    expect(store.get<string>('theme')).toBe('dark');
    expect(store.get<number>('count')).toBe(3);
    expect(store.has('theme')).toBe(true);
  });

  it('treats a missing file as an empty store without throwing', async () => {
    const store = new JsonSettingsStore(join(dir, 'does-not-exist.json'));

    await expect(store.load()).resolves.toBeUndefined();
    expect(store.toObject()).toEqual({});
  });

  it('persists values to disk on save', async () => {
    const filePath = join(dir, 'nested', 'settings.json');
    const store = new JsonSettingsStore(filePath);
    store.set('theme', 'light');

    await store.save();

    const written = JSON.parse(await readFile(filePath, 'utf8')) as Record<string, unknown>;
    expect(written).toEqual({ theme: 'light' });
  });
});
