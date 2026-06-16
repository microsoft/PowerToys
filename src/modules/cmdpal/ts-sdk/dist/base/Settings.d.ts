import type { ICommandSettings, IContentPage } from '../types';
/**
 * A toggle (boolean) setting.
 */
export declare class ToggleSetting {
    readonly key: string;
    label: string;
    description?: string;
    value: boolean;
    isRequired?: boolean;
    constructor(key: string, label: string, defaultValue?: boolean, description?: string);
}
/**
 * A text input setting.
 */
export declare class TextSetting {
    readonly key: string;
    label: string;
    description?: string;
    value: string;
    placeholder?: string;
    multiline?: boolean;
    isRequired?: boolean;
    constructor(key: string, label: string, defaultValue?: string, description?: string);
}
/**
 * A choice set (dropdown) setting.
 */
export declare class ChoiceSetSetting {
    readonly key: string;
    label: string;
    description?: string;
    value: string;
    choices: Array<{
        title: string;
        value: string;
    }>;
    isRequired?: boolean;
    constructor(key: string, label: string, choices: Array<{
        title: string;
        value: string;
    }>, defaultValue?: string, description?: string);
}
type AnySetting = ToggleSetting | TextSetting | ChoiceSetSetting;
/**
 * Container for extension settings. Generates an Adaptive Card form for the settings page.
 */
export declare class Settings implements ICommandSettings {
    private readonly _settings;
    private _settingsPage?;
    get settingsPage(): IContentPage;
    add(setting: AnySetting): this;
    getSetting<T extends AnySetting>(key: string): T | undefined;
    getAllSettings(): AnySetting[];
    /**
     * Update settings from a form submission (key-value map from Adaptive Card inputs).
     */
    update(inputs: Record<string, string>): void;
    /**
     * Generate the Adaptive Card template JSON for the settings form.
     */
    toTemplateJson(): string;
    /**
     * Generate the data JSON (current values).
     */
    toDataJson(): string;
}
export {};
//# sourceMappingURL=Settings.d.ts.map