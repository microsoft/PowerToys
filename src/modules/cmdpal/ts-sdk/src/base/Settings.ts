// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  CommandResult,
  Content,
  ContextItem,
  Details,
  FormContent,
  IContentPage,
  ICommandSettings,
  IconInfo,
  OptionalColor,
} from '../types.js';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';

/** Handler invoked when a {@link Settings} collection changes. */
export type SettingsChangedHandler = (settings: Settings) => void;

/** A single choice within a {@link ChoiceSetSetting}. */
export interface SettingChoice {
  /** Label shown for the choice. */
  title: string;
  /** Value stored when the choice is selected. */
  value: string;
}

/** A boolean toggle setting. */
export class ToggleSetting {
  /** Stable key used to store and look up the setting's value. */
  readonly key: string;
  /** Label shown next to the toggle. */
  label: string;
  /** Optional help text shown below the label. */
  description?: string;
  /** Current value of the toggle. */
  value: boolean;
  /** Whether the user must set this value. */
  isRequired?: boolean;
  /** Message shown when a required value is missing. */
  errorMessage?: string;

  /**
   * Creates a toggle setting.
   *
   * @param key Stable key used to store the value.
   * @param label Label shown next to the toggle.
   * @param defaultValue Initial value. Defaults to `false`.
   * @param description Optional help text shown below the label.
   */
  constructor(key: string, label: string, defaultValue = false, description?: string) {
    this.key = key;
    this.label = label;
    this.value = defaultValue;
    this.description = description;
  }
}

/** A free-form text input setting. */
export class TextSetting {
  /** Stable key used to store and look up the setting's value. */
  readonly key: string;
  /** Label shown above the input. */
  label: string;
  /** Optional help text shown below the label. */
  description?: string;
  /** Current text value. */
  value: string;
  /** Placeholder shown while the input is empty. */
  placeholder?: string;
  /** Whether the input allows multiple lines. */
  multiline?: boolean;
  /** Whether the user must set this value. */
  isRequired?: boolean;
  /** Message shown when a required value is missing. */
  errorMessage?: string;

  /**
   * Creates a text setting.
   *
   * @param key Stable key used to store the value.
   * @param label Label shown above the input.
   * @param defaultValue Initial value. Defaults to an empty string.
   * @param description Optional help text shown below the label.
   */
  constructor(key: string, label: string, defaultValue = '', description?: string) {
    this.key = key;
    this.label = label;
    this.value = defaultValue;
    this.description = description;
  }
}

/** A dropdown (choice set) setting. */
export class ChoiceSetSetting {
  /** Stable key used to store and look up the setting's value. */
  readonly key: string;
  /** Label shown above the dropdown. */
  label: string;
  /** Optional help text shown below the label. */
  description?: string;
  /** Value of the currently selected choice. */
  value: string;
  /** The available choices. */
  choices: SettingChoice[];
  /** Whether the user must set this value. */
  isRequired?: boolean;
  /** Message shown when a required value is missing. */
  errorMessage?: string;

  /**
   * Creates a choice set setting.
   *
   * @param key Stable key used to store the value.
   * @param label Label shown above the dropdown.
   * @param choices The available choices.
   * @param defaultValue Initial value. Defaults to the first choice's value.
   * @param description Optional help text shown below the label.
   */
  constructor(
    key: string,
    label: string,
    choices: SettingChoice[],
    defaultValue?: string,
    description?: string,
  ) {
    this.key = key;
    this.label = label;
    this.choices = choices;
    this.value = defaultValue ?? choices[0]?.value ?? '';
    this.description = description;
  }
}

/** Any setting that can live in a {@link Settings} collection. */
export type AnySetting = ToggleSetting | TextSetting | ChoiceSetSetting;

/**
 * A collection of extension settings. Exposes an auto-generated content page
 * (an Adaptive Card form) via {@link Settings.settingsPage}.
 *
 * @example
 * ```typescript
 * const settings = new Settings();
 * settings.add(new ToggleSetting('darkMode', 'Dark Mode', true));
 * const darkMode = settings.getSetting<ToggleSetting>('darkMode')?.value;
 * ```
 */
export class Settings implements ICommandSettings {
  private readonly items: AnySetting[] = [];
  private page?: SettingsPage;
  private readonly changedHandlers = new Set<SettingsChangedHandler>();

  /**
   * Optional hook run after a settings submission is applied. Throwing (or
   * rejecting) surfaces the failure to the caller as a JSON-RPC error, so an
   * author can signal that persistence failed. Mirrors saving in the C#
   * Command Palette toolkit.
   */
  onSave?: (settings: Settings) => void | Promise<void>;

  /** Auto-generated content page that renders the settings as a form. */
  get settingsPage(): IContentPage {
    this.page ??= new SettingsPage(this);
    return this.page;
  }

  /**
   * Subscribes to change notifications raised after a settings submission is
   * applied. Mirrors the C# toolkit's `Settings.SettingsChanged` event.
   *
   * @param handler Called with this collection after each applied submission.
   * @returns A function that removes the subscription when called.
   */
  onSettingsChanged(handler: SettingsChangedHandler): () => void {
    this.changedHandlers.add(handler);
    return () => {
      this.changedHandlers.delete(handler);
    };
  }

  /**
   * Adds a setting to the collection.
   *
   * @param setting The setting to add.
   * @returns This collection, so calls can be chained.
   */
  add(setting: AnySetting): this {
    this.items.push(setting);
    return this;
  }

  /**
   * Looks up a setting by key.
   *
   * @typeParam T Expected setting type.
   * @param key Key of the setting to find.
   * @returns The setting, or `undefined` when no setting has that key.
   */
  getSetting<T extends AnySetting>(key: string): T | undefined {
    return this.items.find((setting) => setting.key === key) as T | undefined;
  }

  /**
   * Returns a copy of every setting in the collection.
   *
   * @returns A new array containing all settings.
   */
  getAllSettings(): AnySetting[] {
    return [...this.items];
  }

  /**
   * Applies a map of raw form input values back onto the settings.
   *
   * @param inputs Map of setting key to its raw string value.
   */
  update(inputs: Record<string, string>): void {
    for (const setting of this.items) {
      const raw = inputs[setting.key];
      if (raw === undefined) {
        continue;
      }
      if (setting instanceof ToggleSetting) {
        setting.value = raw === 'true';
      } else {
        setting.value = raw;
      }
    }
  }

  /**
   * Applies a settings submission: updates values, raises the
   * {@link Settings.onSettingsChanged} subscribers, and awaits the
   * {@link Settings.onSave} hook. Used by the auto-generated settings page.
   *
   * @param inputs Map of setting key to its raw string value.
   */
  async submit(inputs: Record<string, string>): Promise<void> {
    this.update(inputs);
    for (const handler of this.changedHandlers) {
      handler(this);
    }
    await this.onSave?.(this);
  }

  /** Builds the Adaptive Card template JSON for the settings form. */
  toTemplateJson(): string {
    const body: unknown[] = [];

    for (const setting of this.items) {
      body.push({
        type: 'TextBlock',
        text: setting.label,
        weight: 'bolder',
        spacing: 'medium',
      });
      if (setting.description) {
        body.push({
          type: 'TextBlock',
          text: setting.description,
          isSubtle: true,
          wrap: true,
          spacing: 'none',
        });
      }

      if (setting instanceof ToggleSetting) {
        const input: Record<string, unknown> = {
          type: 'Input.Toggle',
          id: setting.key,
          title: '',
          value: String(setting.value),
          valueOn: 'true',
          valueOff: 'false',
        };
        applyRequired(input, setting);
        body.push(input);
      } else if (setting instanceof TextSetting) {
        const input: Record<string, unknown> = {
          type: 'Input.Text',
          id: setting.key,
          placeholder: setting.placeholder ?? '',
          value: setting.value,
          isMultiline: setting.multiline ?? false,
        };
        applyRequired(input, setting);
        body.push(input);
      } else {
        const input: Record<string, unknown> = {
          type: 'Input.ChoiceSet',
          id: setting.key,
          value: setting.value,
          choices: setting.choices.map((choice) => ({
            title: choice.title,
            value: choice.value,
          })),
        };
        applyRequired(input, setting);
        body.push(input);
      }
    }

    body.push({
      type: 'ActionSet',
      actions: [{ type: 'Action.Submit', title: 'Save' }],
    });

    return JSON.stringify({ type: 'AdaptiveCard', version: '1.5', body });
  }

  /** Builds the data JSON that carries the current setting values. */
  toDataJson(): string {
    const data: Record<string, string> = {};
    for (const setting of this.items) {
      data[setting.key] = String(setting.value);
    }
    return JSON.stringify(data);
  }
}

/** Auto-generated content page that renders and saves a {@link Settings} form. */
class SettingsPage implements IContentPage {
  readonly id = '__settings__';
  readonly name = 'Settings';
  readonly title = 'Extension Settings';
  icon?: IconInfo | null = null;
  isLoading?: boolean = false;
  accentColor?: OptionalColor | null = null;
  details?: Details | null = null;
  commands?: ContextItem[] = [];

  private readonly settings: Settings;

  constructor(settings: Settings) {
    this.settings = settings;
  }

  getContent(): Content[] {
    const form: FormContent = {
      type: 'form',
      formId: 'settings',
      templateJson: this.settings.toTemplateJson(),
      dataJson: this.settings.toDataJson(),
      submitForm: async (inputs: string): Promise<CommandResult> => {
        await this.settings.submit(parseInputs(inputs));
        return { kind: 'goHome' };
      },
    };
    return [form];
  }
}

function applyRequired(input: Record<string, unknown>, setting: AnySetting): void {
  if (setting.isRequired) {
    input.isRequired = true;
    if (setting.errorMessage !== undefined) {
      input.errorMessage = setting.errorMessage;
    }
  }
}

function parseInputs(inputs: string): Record<string, string> {
  try {
    const parsed: unknown = JSON.parse(inputs);
    if (parsed && typeof parsed === 'object') {
      const result: Record<string, string> = {};
      for (const [key, value] of Object.entries(parsed as Record<string, unknown>)) {
        result[key] = String(value);
      }
      return result;
    }
  } catch {
    // Ignore malformed input and treat it as an empty submission.
  }
  return {};
}

function isFileNotFound(error: unknown): boolean {
  return (
    typeof error === 'object' && error !== null && (error as { code?: unknown }).code === 'ENOENT'
  );
}

/**
 * A JSON-backed key/value store for an extension's own settings file. Mirrors
 * the C# toolkit's `JsonSettingsManager`: it owns a `filePath` and reads and
 * writes that file as JSON via {@link JsonSettingsStore.load} and
 * {@link JsonSettingsStore.save}. Point `filePath` at a location inside the
 * extension's own folder.
 *
 * @example
 * ```typescript
 * const store = new JsonSettingsStore(join(__dirname, 'settings.json'));
 * await store.load();
 * const theme = store.get<string>('theme') ?? 'system';
 * store.set('theme', 'dark');
 * await store.save();
 * ```
 */
export class JsonSettingsStore {
  /** Absolute path to the JSON file backing this store. */
  readonly filePath: string;

  private values: Record<string, unknown> = {};

  /**
   * Creates a store backed by a JSON file.
   *
   * @param filePath Path to the JSON file, inside the extension's own folder.
   */
  constructor(filePath: string) {
    this.filePath = filePath;
  }

  /**
   * Reads and parses the backing file, seeding the in-memory values. A missing
   * file is treated as an empty store rather than an error.
   */
  async load(): Promise<void> {
    try {
      const raw = await readFile(this.filePath, 'utf8');
      const parsed: unknown = JSON.parse(raw);
      this.values =
        parsed && typeof parsed === 'object' ? { ...(parsed as Record<string, unknown>) } : {};
    } catch (error) {
      if (isFileNotFound(error)) {
        this.values = {};
        return;
      }
      throw error;
    }
  }

  /** Writes the current values to the backing file, creating its folder. */
  async save(): Promise<void> {
    await mkdir(dirname(this.filePath), { recursive: true });
    await writeFile(this.filePath, JSON.stringify(this.values, null, 2), 'utf8');
  }

  /**
   * Reads a value by key.
   *
   * @typeParam T Expected value type.
   * @param key Key to read.
   * @returns The value, or `undefined` when the key is absent.
   */
  get<T>(key: string): T | undefined {
    return this.values[key] as T | undefined;
  }

  /**
   * Sets a value by key. Call {@link JsonSettingsStore.save} to persist.
   *
   * @param key Key to set.
   * @param value Value to store.
   */
  set(key: string, value: unknown): void {
    this.values[key] = value;
  }

  /**
   * Whether a key is present in the store.
   *
   * @param key Key to check.
   */
  has(key: string): boolean {
    return Object.prototype.hasOwnProperty.call(this.values, key);
  }

  /** Returns a shallow copy of every stored value. */
  toObject(): Record<string, unknown> {
    return { ...this.values };
  }
}
