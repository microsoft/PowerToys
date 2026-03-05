// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ICommandResult, ICommandSettings, IContent, IContentPage, ContentType, PageType } from '../generated/types';
import { ContentPage } from './pages';
import { FormContent } from './content';
import { CommandResult } from './results';
import * as fs from 'fs';
import * as path from 'path';

// ---------------------------------------------------------------------------
// Setting<T> base and concrete setting types
// ---------------------------------------------------------------------------

/**
 * Abstract base class for a single typed setting.
 * Mirrors the C# Setting<T> from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export abstract class Setting<T> {
  readonly key: string;
  label: string;
  description: string;
  value: T;
  isRequired: boolean = false;
  errorMessage: string = '';

  constructor(key: string, label: string, description: string, defaultValue: T) {
    this.key = key;
    this.label = label;
    this.description = description;
    this.value = defaultValue;
  }

  /** Produce an Adaptive Card element dictionary for this setting. */
  abstract toDictionary(): Record<string, unknown>;

  /** Update the setting value from a form submission payload. */
  abstract update(payload: Record<string, unknown>): void;

  /** Serialize the current value as a JSON fragment: `"key": <value>`. */
  abstract toState(): string;

  /** Return the data-identifier fragment for the submit action: `"key": "key"`. */
  toDataIdentifier(): string {
    return `"${this.key}": "${this.key}"`;
  }
}

/**
 * A text input setting rendered as Input.Text in Adaptive Cards.
 */
export class TextSetting extends Setting<string> {
  multiline: boolean;
  placeholder: string;

  constructor(
    key: string,
    label: string,
    description: string,
    defaultValue: string = '',
    options?: { multiline?: boolean; placeholder?: string },
  ) {
    super(key, label, description, defaultValue);
    this.multiline = options?.multiline ?? false;
    this.placeholder = options?.placeholder ?? '';
  }

  toDictionary(): Record<string, unknown> {
    return {
      type: 'Input.Text',
      title: this.label,
      id: this.key,
      label: this.description,
      value: this.value ?? '',
      isRequired: this.isRequired,
      errorMessage: this.errorMessage,
      isMultiline: this.multiline,
      placeholder: this.placeholder,
    };
  }

  update(payload: Record<string, unknown>): void {
    if (this.key in payload) {
      this.value = String(payload[this.key] ?? '');
    }
  }

  toState(): string {
    const escaped = JSON.stringify(this.value ?? '');
    return `"${this.key}": ${escaped}`;
  }
}

/**
 * A boolean toggle setting rendered as Input.Toggle in Adaptive Cards.
 * Uses a ColumnSet layout matching the C# ToggleSetting pattern.
 */
export class ToggleSetting extends Setting<boolean> {
  constructor(key: string, label: string, description: string, defaultValue: boolean = false) {
    super(key, label, description, defaultValue);
  }

  toDictionary(): Record<string, unknown> {
    return {
      type: 'ColumnSet',
      columns: [
        {
          type: 'Column',
          width: '20px',
          items: [
            {
              type: 'Input.Toggle',
              id: this.key,
              title: '',
              value: this.value ? 'true' : 'false',
            },
          ],
          verticalContentAlignment: 'Center',
        },
        {
          type: 'Column',
          width: 'stretch',
          items: [
            {
              type: 'TextBlock',
              text: this.label,
              wrap: true,
              weight: 'Bolder',
            },
            ...(this.description
              ? [
                  {
                    type: 'TextBlock',
                    text: this.description,
                    wrap: true,
                    size: 'Small',
                    isSubtle: true,
                  },
                ]
              : []),
          ],
          verticalContentAlignment: 'Center',
        },
      ],
    };
  }

  update(payload: Record<string, unknown>): void {
    if (this.key in payload) {
      const val = payload[this.key];
      this.value = val === true || val === 'true';
    }
  }

  toState(): string {
    return `"${this.key}": "${this.value ? 'true' : 'false'}"`;
  }
}

/** A single choice option for ChoiceSetSetting. */
export interface Choice {
  title: string;
  value: string;
}

/**
 * A dropdown choice setting rendered as Input.ChoiceSet in Adaptive Cards.
 */
export class ChoiceSetSetting extends Setting<string> {
  choices: Choice[];

  constructor(
    key: string,
    label: string,
    description: string,
    choices: Choice[],
    defaultValue?: string,
  ) {
    super(key, label, description, defaultValue ?? (choices.length > 0 ? choices[0].value : ''));
    this.choices = choices;
  }

  toDictionary(): Record<string, unknown> {
    return {
      type: 'Input.ChoiceSet',
      title: this.label,
      id: this.key,
      label: this.description,
      choices: this.choices.map((c) => ({ title: c.title, value: c.value })),
      value: this.value ?? '',
      isRequired: this.isRequired,
      errorMessage: this.errorMessage,
    };
  }

  update(payload: Record<string, unknown>): void {
    if (this.key in payload) {
      this.value = String(payload[this.key] ?? '');
    }
  }

  toState(): string {
    const escaped = JSON.stringify(this.value ?? '');
    return `"${this.key}": ${escaped}`;
  }
}

// ---------------------------------------------------------------------------
// Settings collection
// ---------------------------------------------------------------------------

/** Callback type for settings change events. */
export type SettingsChangedCallback = () => void;

/**
 * Collection of Setting objects that generates Adaptive Card forms.
 * Mirrors the C# Settings class from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export class Settings {
  private _settings: Map<string, Setting<unknown>> = new Map();
  private _changeCallbacks: SettingsChangedCallback[] = [];

  /** Register a setting in this collection. */
  add<T>(setting: Setting<T>): void {
    this._settings.set(setting.key, setting as unknown as Setting<unknown>);
  }

  /** Get the current value of a setting by key. */
  getValue<T>(key: string): T | undefined {
    const setting = this._settings.get(key);
    return setting ? (setting.value as T) : undefined;
  }

  /** Subscribe to settings change events. */
  onSettingsChanged(callback: SettingsChangedCallback): void {
    this._changeCallbacks.push(callback);
  }

  /** Generate Adaptive Card JSON template for all settings. */
  toFormJson(): string {
    const settings = Array.from(this._settings.values());
    const bodies = settings.map((s) => JSON.stringify(s.toDictionary())).join(',');
    const datas = settings.map((s) => s.toDataIdentifier()).join(',');

    return `{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
      ${bodies}
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Save",
      "data": {
        ${datas}
      }
    }
  ]
}`;
  }

  /** Serialize all settings state to JSON. */
  toJson(): string {
    const settings = Array.from(this._settings.values());
    const content = settings.map((s) => s.toState()).join(',\n');
    return `{\n${content}\n}`;
  }

  /** Update settings from a form submission payload (JSON string or object). */
  update(data: string | Record<string, unknown>): void {
    let payload: Record<string, unknown>;
    if (typeof data === 'string') {
      try {
        payload = JSON.parse(data);
      } catch {
        return;
      }
    } else {
      payload = data;
    }

    for (const setting of this._settings.values()) {
      setting.update(payload);
    }
  }

  /** Raise settings changed event. */
  raiseSettingsChanged(): void {
    for (const cb of this._changeCallbacks) {
      cb();
    }
  }
}

// ---------------------------------------------------------------------------
// SettingsForm — FormContent that renders/submits a Settings collection
// ---------------------------------------------------------------------------

/**
 * FormContent subclass that renders a Settings collection as an Adaptive Card form.
 * Mirrors the C# SettingsForm from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export class SettingsForm extends FormContent {
  private _settings: Settings;

  constructor(settings: Settings) {
    super();
    this._settings = settings;
  }

  get templateJson(): string {
    return this._settings.toFormJson();
  }

  get dataJson(): string {
    return this._settings.toJson();
  }

  get stateJson(): string {
    return this._settings.toJson();
  }

  submitForm(inputs: string, _data: string): ICommandResult {
    this._settings.update(inputs);
    this._settings.raiseSettingsChanged();
    return CommandResult.goHome();
  }
}

// ---------------------------------------------------------------------------
// SettingsPage — ContentPage that wraps a SettingsForm
// ---------------------------------------------------------------------------

const SETTINGS_PAGE_ID = '__settings__';

/**
 * ContentPage subclass that exposes a Settings form as a content page.
 * Mirrors the C# SettingsContentPage from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export class SettingsPage extends ContentPage {
  private _settingsForm: SettingsForm;

  constructor(settings: Settings) {
    super();
    this.id = SETTINGS_PAGE_ID;
    this.name = 'Settings';
    this.icon = { value: '\uE713', isEmoji: false };
    this._settingsForm = new SettingsForm(settings);
    this._settingsForm.id = `${SETTINGS_PAGE_ID}_form`;

    settings.onSettingsChanged(() => {
      this.notifyItemsChanged();
    });
  }

  getContent(): IContent[] {
    return [this._settingsForm];
  }
}

// ---------------------------------------------------------------------------
// CommandSettings — ICommandSettings wrapper around a SettingsPage
// ---------------------------------------------------------------------------

/**
 * Implements ICommandSettings by wrapping a SettingsPage.
 * This is the object returned from CommandProvider.settings.
 */
export class CommandSettings implements ICommandSettings {
  readonly settingsPage: SettingsPage;

  constructor(settings: Settings) {
    this.settingsPage = new SettingsPage(settings);
  }
}

// ---------------------------------------------------------------------------
// JsonSettingsManager — File-based persistence
// ---------------------------------------------------------------------------

/**
 * Manages loading and saving Settings to a JSON file.
 * Mirrors the C# JsonSettingsManager from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export class JsonSettingsManager {
  readonly settings: Settings;
  filePath: string;

  constructor(settings: Settings, filePath: string) {
    this.settings = settings;
    this.filePath = filePath;

    // Auto-save when settings change
    this.settings.onSettingsChanged(() => {
      this.saveSettings();
    });
  }

  /** Load settings from the JSON file. */
  loadSettings(): void {
    try {
      if (fs.existsSync(this.filePath)) {
        const content = fs.readFileSync(this.filePath, 'utf-8');
        const data = JSON.parse(content);
        if (data && typeof data === 'object') {
          this.settings.update(data);
        }
      }
    } catch {
      // If file is corrupt or missing, leave defaults
    }
  }

  /** Save current settings to the JSON file. */
  saveSettings(): void {
    try {
      const dir = path.dirname(this.filePath);
      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
      }

      // Deep merge: preserve other keys in existing file
      let existing: Record<string, unknown> = {};
      try {
        if (fs.existsSync(this.filePath)) {
          const content = fs.readFileSync(this.filePath, 'utf-8');
          existing = JSON.parse(content);
        }
      } catch {
        existing = {};
      }

      const current = JSON.parse(this.settings.toJson());
      const merged = { ...existing, ...current };
      fs.writeFileSync(this.filePath, JSON.stringify(merged, null, 2), 'utf-8');
    } catch {
      // Silently fail on save errors
    }
  }
}
