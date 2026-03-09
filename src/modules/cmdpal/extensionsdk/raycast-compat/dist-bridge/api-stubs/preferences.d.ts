/** Internal: set the preferences file path (called by environment setup). */
export declare function _setPreferencesPath(supportPath: string): void;
/**
 * Returns the extension's preference values.
 *
 * Raycast extensions call this as `getPreferenceValues<T>()` to get typed
 * preferences. We read from the JSON file and return the raw object.
 */
export declare function getPreferenceValues<T extends Record<string, unknown> = Record<string, unknown>>(): T;
/**
 * Opens the extension's preferences panel.
 * Stub: logs a warning. CmdPal will handle this through the settings UI.
 */
export declare function openExtensionPreferences(): Promise<void>;
/**
 * Opens the command's preferences panel.
 * Stub: logs a warning. CmdPal will handle this through the settings UI.
 */
export declare function openCommandPreferences(): Promise<void>;
//# sourceMappingURL=preferences.d.ts.map