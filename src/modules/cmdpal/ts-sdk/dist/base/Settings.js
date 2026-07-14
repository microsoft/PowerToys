"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.Settings = exports.ChoiceSetSetting = exports.TextSetting = exports.ToggleSetting = void 0;
/**
 * A toggle (boolean) setting.
 */
class ToggleSetting {
    key;
    label;
    description;
    value;
    isRequired;
    constructor(key, label, defaultValue = false, description) {
        this.key = key;
        this.label = label;
        this.value = defaultValue;
        this.description = description;
    }
}
exports.ToggleSetting = ToggleSetting;
/**
 * A text input setting.
 */
class TextSetting {
    key;
    label;
    description;
    value;
    placeholder;
    multiline;
    isRequired;
    constructor(key, label, defaultValue = '', description) {
        this.key = key;
        this.label = label;
        this.value = defaultValue;
        this.description = description;
    }
}
exports.TextSetting = TextSetting;
/**
 * A choice set (dropdown) setting.
 */
class ChoiceSetSetting {
    key;
    label;
    description;
    value;
    choices;
    isRequired;
    constructor(key, label, choices, defaultValue, description) {
        this.key = key;
        this.label = label;
        this.choices = choices;
        this.value = defaultValue ?? (choices.length > 0 ? choices[0].value : '');
        this.description = description;
    }
}
exports.ChoiceSetSetting = ChoiceSetSetting;
/**
 * Container for extension settings. Generates an Adaptive Card form for the settings page.
 */
class Settings {
    _settings = [];
    _settingsPage;
    get settingsPage() {
        if (!this._settingsPage) {
            this._settingsPage = new SettingsPage(this);
        }
        return this._settingsPage;
    }
    add(setting) {
        this._settings.push(setting);
        return this;
    }
    getSetting(key) {
        return this._settings.find(s => s.key === key);
    }
    getAllSettings() {
        return [...this._settings];
    }
    /**
     * Update settings from a form submission (key-value map from Adaptive Card inputs).
     */
    update(inputs) {
        for (const setting of this._settings) {
            if (inputs[setting.key] !== undefined) {
                if (setting instanceof ToggleSetting) {
                    setting.value = inputs[setting.key] === 'true';
                }
                else {
                    setting.value = inputs[setting.key];
                }
            }
        }
    }
    /**
     * Generate the Adaptive Card template JSON for the settings form.
     */
    toTemplateJson() {
        const body = [];
        for (const setting of this._settings) {
            if (setting.label) {
                body.push({
                    type: 'TextBlock',
                    text: setting.label,
                    weight: 'bolder',
                    spacing: 'medium',
                });
            }
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
                body.push({
                    type: 'Input.Toggle',
                    id: setting.key,
                    title: '',
                    value: String(setting.value),
                    valueOn: 'true',
                    valueOff: 'false',
                });
            }
            else if (setting instanceof TextSetting) {
                body.push({
                    type: 'Input.Text',
                    id: setting.key,
                    placeholder: setting.placeholder ?? '',
                    value: setting.value,
                    isMultiline: setting.multiline ?? false,
                });
            }
            else if (setting instanceof ChoiceSetSetting) {
                body.push({
                    type: 'Input.ChoiceSet',
                    id: setting.key,
                    value: setting.value,
                    choices: setting.choices.map(c => ({ title: c.title, value: c.value })),
                });
            }
        }
        body.push({
            type: 'ActionSet',
            actions: [{ type: 'Action.Submit', title: 'Save' }],
        });
        return JSON.stringify({ type: 'AdaptiveCard', version: '1.5', body });
    }
    /**
     * Generate the data JSON (current values).
     */
    toDataJson() {
        const data = {};
        for (const setting of this._settings) {
            data[setting.key] = String(setting.value);
        }
        return JSON.stringify(data);
    }
}
exports.Settings = Settings;
/**
 * Internal settings page that renders the settings form.
 */
class SettingsPage {
    id = '__settings__';
    name = 'Settings';
    title = 'Extension Settings';
    icon = null;
    isLoading = false;
    accentColor = null;
    details = null;
    commands = [];
    settings;
    constructor(settings) {
        this.settings = settings;
    }
    getContent() {
        const form = {
            type: 'form',
            templateJson: this.settings.toTemplateJson(),
            dataJson: this.settings.toDataJson(),
            submitForm: (inputs, _data) => {
                let parsed = {};
                try {
                    const p = JSON.parse(inputs);
                    if (p && typeof p === 'object') {
                        parsed = p;
                    }
                }
                catch {
                    // inputs was not valid JSON; keep empty
                }
                this.settings.update(parsed);
                return { kind: 'showToast', args: { message: 'Settings saved' } };
            },
        };
        return [form];
    }
}
//# sourceMappingURL=Settings.js.map