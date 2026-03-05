import { ICommandResult, ICommandSettings, IContent } from '../generated/types';
import { ContentPage } from './pages';
import { FormContent } from './content';
/**
 * Abstract base class for a single typed setting.
 * Mirrors the C# Setting<T> from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export declare abstract class Setting<T> {
    readonly key: string;
    label: string;
    description: string;
    value: T;
    isRequired: boolean;
    errorMessage: string;
    constructor(key: string, label: string, description: string, defaultValue: T);
    /** Produce an Adaptive Card element dictionary for this setting. */
    abstract toDictionary(): Record<string, unknown>;
    /** Update the setting value from a form submission payload. */
    abstract update(payload: Record<string, unknown>): void;
    /** Serialize the current value as a JSON fragment: `"key": <value>`. */
    abstract toState(): string;
    /** Return the data-identifier fragment for the submit action: `"key": "key"`. */
    toDataIdentifier(): string;
}
/**
 * A text input setting rendered as Input.Text in Adaptive Cards.
 */
export declare class TextSetting extends Setting<string> {
    multiline: boolean;
    placeholder: string;
    constructor(key: string, label: string, description: string, defaultValue?: string, options?: {
        multiline?: boolean;
        placeholder?: string;
    });
    toDictionary(): Record<string, unknown>;
    update(payload: Record<string, unknown>): void;
    toState(): string;
}
/**
 * A boolean toggle setting rendered as Input.Toggle in Adaptive Cards.
 * Uses a ColumnSet layout matching the C# ToggleSetting pattern.
 */
export declare class ToggleSetting extends Setting<boolean> {
    constructor(key: string, label: string, description: string, defaultValue?: boolean);
    toDictionary(): Record<string, unknown>;
    update(payload: Record<string, unknown>): void;
    toState(): string;
}
/** A single choice option for ChoiceSetSetting. */
export interface Choice {
    title: string;
    value: string;
}
/**
 * A dropdown choice setting rendered as Input.ChoiceSet in Adaptive Cards.
 */
export declare class ChoiceSetSetting extends Setting<string> {
    choices: Choice[];
    constructor(key: string, label: string, description: string, choices: Choice[], defaultValue?: string);
    toDictionary(): Record<string, unknown>;
    update(payload: Record<string, unknown>): void;
    toState(): string;
}
/** Callback type for settings change events. */
export type SettingsChangedCallback = () => void;
/**
 * Collection of Setting objects that generates Adaptive Card forms.
 * Mirrors the C# Settings class from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export declare class Settings {
    private _settings;
    private _changeCallbacks;
    /** Register a setting in this collection. */
    add<T>(setting: Setting<T>): void;
    /** Get the current value of a setting by key. */
    getValue<T>(key: string): T | undefined;
    /** Subscribe to settings change events. */
    onSettingsChanged(callback: SettingsChangedCallback): void;
    /** Generate Adaptive Card JSON template for all settings. */
    toFormJson(): string;
    /** Serialize all settings state to JSON. */
    toJson(): string;
    /** Update settings from a form submission payload (JSON string or object). */
    update(data: string | Record<string, unknown>): void;
    /** Raise settings changed event. */
    raiseSettingsChanged(): void;
}
/**
 * FormContent subclass that renders a Settings collection as an Adaptive Card form.
 * Mirrors the C# SettingsForm from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export declare class SettingsForm extends FormContent {
    private _settings;
    constructor(settings: Settings);
    get templateJson(): string;
    get dataJson(): string;
    get stateJson(): string;
    submitForm(inputs: string, _data: string): ICommandResult;
}
/**
 * ContentPage subclass that exposes a Settings form as a content page.
 * Mirrors the C# SettingsContentPage from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export declare class SettingsPage extends ContentPage {
    private _settingsForm;
    constructor(settings: Settings);
    getContent(): IContent[];
}
/**
 * Implements ICommandSettings by wrapping a SettingsPage.
 * This is the object returned from CommandProvider.settings.
 */
export declare class CommandSettings implements ICommandSettings {
    readonly settingsPage: SettingsPage;
    constructor(settings: Settings);
}
/**
 * Manages loading and saving Settings to a JSON file.
 * Mirrors the C# JsonSettingsManager from Microsoft.CommandPalette.Extensions.Toolkit.
 */
export declare class JsonSettingsManager {
    readonly settings: Settings;
    filePath: string;
    constructor(settings: Settings, filePath: string);
    /** Load settings from the JSON file. */
    loadSettings(): void;
    /** Save current settings to the JSON file. */
    saveSettings(): void;
}
//# sourceMappingURL=settings.d.ts.map