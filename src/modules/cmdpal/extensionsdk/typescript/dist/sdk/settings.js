"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.JsonSettingsManager = exports.CommandSettings = exports.SettingsPage = exports.SettingsForm = exports.Settings = exports.ChoiceSetSetting = exports.ToggleSetting = exports.TextSetting = exports.Setting = void 0;
const pages_1 = require("./pages");
const content_1 = require("./content");
const results_1 = require("./results");
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
// ---------------------------------------------------------------------------
// Setting<T> base and concrete setting types
// ---------------------------------------------------------------------------
/**
 * Abstract base class for a single typed setting.
 * Mirrors the C# Setting<T> from Microsoft.CommandPalette.Extensions.Toolkit.
 */
class Setting {
    constructor(key, label, description, defaultValue) {
        this.isRequired = false;
        this.errorMessage = '';
        this.key = key;
        this.label = label;
        this.description = description;
        this.value = defaultValue;
    }
    /** Return the data-identifier fragment for the submit action: `"key": "key"`. */
    toDataIdentifier() {
        return `"${this.key}": "${this.key}"`;
    }
}
exports.Setting = Setting;
/**
 * A text input setting rendered as Input.Text in Adaptive Cards.
 */
class TextSetting extends Setting {
    constructor(key, label, description, defaultValue = '', options) {
        super(key, label, description, defaultValue);
        this.multiline = options?.multiline ?? false;
        this.placeholder = options?.placeholder ?? '';
    }
    toDictionary() {
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
    update(payload) {
        if (this.key in payload) {
            this.value = String(payload[this.key] ?? '');
        }
    }
    toState() {
        const escaped = JSON.stringify(this.value ?? '');
        return `"${this.key}": ${escaped}`;
    }
}
exports.TextSetting = TextSetting;
/**
 * A boolean toggle setting rendered as Input.Toggle in Adaptive Cards.
 * Uses a ColumnSet layout matching the C# ToggleSetting pattern.
 */
class ToggleSetting extends Setting {
    constructor(key, label, description, defaultValue = false) {
        super(key, label, description, defaultValue);
    }
    toDictionary() {
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
    update(payload) {
        if (this.key in payload) {
            const val = payload[this.key];
            this.value = val === true || val === 'true';
        }
    }
    toState() {
        return `"${this.key}": "${this.value ? 'true' : 'false'}"`;
    }
}
exports.ToggleSetting = ToggleSetting;
/**
 * A dropdown choice setting rendered as Input.ChoiceSet in Adaptive Cards.
 */
class ChoiceSetSetting extends Setting {
    constructor(key, label, description, choices, defaultValue) {
        super(key, label, description, defaultValue ?? (choices.length > 0 ? choices[0].value : ''));
        this.choices = choices;
    }
    toDictionary() {
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
    update(payload) {
        if (this.key in payload) {
            this.value = String(payload[this.key] ?? '');
        }
    }
    toState() {
        const escaped = JSON.stringify(this.value ?? '');
        return `"${this.key}": ${escaped}`;
    }
}
exports.ChoiceSetSetting = ChoiceSetSetting;
/**
 * Collection of Setting objects that generates Adaptive Card forms.
 * Mirrors the C# Settings class from Microsoft.CommandPalette.Extensions.Toolkit.
 */
class Settings {
    constructor() {
        this._settings = new Map();
        this._changeCallbacks = [];
    }
    /** Register a setting in this collection. */
    add(setting) {
        this._settings.set(setting.key, setting);
    }
    /** Get the current value of a setting by key. */
    getValue(key) {
        const setting = this._settings.get(key);
        return setting ? setting.value : undefined;
    }
    /** Subscribe to settings change events. */
    onSettingsChanged(callback) {
        this._changeCallbacks.push(callback);
    }
    /** Generate Adaptive Card JSON template for all settings. */
    toFormJson() {
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
    toJson() {
        const settings = Array.from(this._settings.values());
        const content = settings.map((s) => s.toState()).join(',\n');
        return `{\n${content}\n}`;
    }
    /** Update settings from a form submission payload (JSON string or object). */
    update(data) {
        let payload;
        if (typeof data === 'string') {
            try {
                payload = JSON.parse(data);
            }
            catch {
                return;
            }
        }
        else {
            payload = data;
        }
        for (const setting of this._settings.values()) {
            setting.update(payload);
        }
    }
    /** Raise settings changed event. */
    raiseSettingsChanged() {
        for (const cb of this._changeCallbacks) {
            cb();
        }
    }
}
exports.Settings = Settings;
// ---------------------------------------------------------------------------
// SettingsForm — FormContent that renders/submits a Settings collection
// ---------------------------------------------------------------------------
/**
 * FormContent subclass that renders a Settings collection as an Adaptive Card form.
 * Mirrors the C# SettingsForm from Microsoft.CommandPalette.Extensions.Toolkit.
 */
class SettingsForm extends content_1.FormContent {
    constructor(settings) {
        super();
        this._settings = settings;
    }
    get templateJson() {
        return this._settings.toFormJson();
    }
    get dataJson() {
        return this._settings.toJson();
    }
    get stateJson() {
        return this._settings.toJson();
    }
    submitForm(inputs, _data) {
        this._settings.update(inputs);
        this._settings.raiseSettingsChanged();
        return results_1.CommandResult.goHome();
    }
}
exports.SettingsForm = SettingsForm;
// ---------------------------------------------------------------------------
// SettingsPage — ContentPage that wraps a SettingsForm
// ---------------------------------------------------------------------------
const SETTINGS_PAGE_ID = '__settings__';
/**
 * ContentPage subclass that exposes a Settings form as a content page.
 * Mirrors the C# SettingsContentPage from Microsoft.CommandPalette.Extensions.Toolkit.
 */
class SettingsPage extends pages_1.ContentPage {
    constructor(settings) {
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
    getContent() {
        return [this._settingsForm];
    }
}
exports.SettingsPage = SettingsPage;
// ---------------------------------------------------------------------------
// CommandSettings — ICommandSettings wrapper around a SettingsPage
// ---------------------------------------------------------------------------
/**
 * Implements ICommandSettings by wrapping a SettingsPage.
 * This is the object returned from CommandProvider.settings.
 */
class CommandSettings {
    constructor(settings) {
        this.settingsPage = new SettingsPage(settings);
    }
}
exports.CommandSettings = CommandSettings;
// ---------------------------------------------------------------------------
// JsonSettingsManager — File-based persistence
// ---------------------------------------------------------------------------
/**
 * Manages loading and saving Settings to a JSON file.
 * Mirrors the C# JsonSettingsManager from Microsoft.CommandPalette.Extensions.Toolkit.
 */
class JsonSettingsManager {
    constructor(settings, filePath) {
        this.settings = settings;
        this.filePath = filePath;
        // Auto-save when settings change
        this.settings.onSettingsChanged(() => {
            this.saveSettings();
        });
    }
    /** Load settings from the JSON file. */
    loadSettings() {
        try {
            if (fs.existsSync(this.filePath)) {
                const content = fs.readFileSync(this.filePath, 'utf-8');
                const data = JSON.parse(content);
                if (data && typeof data === 'object') {
                    this.settings.update(data);
                }
            }
        }
        catch {
            // If file is corrupt or missing, leave defaults
        }
    }
    /** Save current settings to the JSON file. */
    saveSettings() {
        try {
            const dir = path.dirname(this.filePath);
            if (!fs.existsSync(dir)) {
                fs.mkdirSync(dir, { recursive: true });
            }
            // Deep merge: preserve other keys in existing file
            let existing = {};
            try {
                if (fs.existsSync(this.filePath)) {
                    const content = fs.readFileSync(this.filePath, 'utf-8');
                    existing = JSON.parse(content);
                }
            }
            catch {
                existing = {};
            }
            const current = JSON.parse(this.settings.toJson());
            const merged = { ...existing, ...current };
            fs.writeFileSync(this.filePath, JSON.stringify(merged, null, 2), 'utf-8');
        }
        catch {
            // Silently fail on save errors
        }
    }
}
exports.JsonSettingsManager = JsonSettingsManager;
//# sourceMappingURL=settings.js.map