export interface LaunchContext {
    [key: string]: unknown;
}
export declare enum LaunchType {
    UserInitiated = "userInitiated",
    Background = "background"
}
export interface EnvironmentConfig {
    extensionName?: string;
    commandName?: string;
    assetsPath?: string;
    supportPath?: string;
    extensionDir?: string;
    launchType?: LaunchType;
    launchContext?: LaunchContext;
}
/**
 * The environment object exposed to Raycast extensions.
 * Getters ensure values reflect any runtime reconfiguration.
 */
export declare const environment: {
    readonly extensionName: string;
    readonly commandName: string;
    readonly assetsPath: string;
    readonly supportPath: string;
    readonly extensionDir: string;
    /** Always false in CmdPal — extensions run in production mode. */
    readonly isDevelopment: boolean;
    /** Raycast API version compatibility — use latest known. */
    readonly raycastVersion: string;
    readonly launchType: LaunchType;
    readonly launchContext: LaunchContext;
    /**
     * Feature detection. Raycast uses this to check API capabilities.
     * We report true for the subset we support.
     */
    canAccess(api: unknown): boolean;
    /** Raycast's textSize preference (we default to medium). */
    readonly textSize: string;
    /** Raycast's appearance (we follow system). */
    readonly appearance: string;
    /** Raycast's theme string. */
    readonly theme: string;
};
/**
 * Bootstrap call — the compat runtime sets actual values from
 * the CmdPal manifest and runtime context before the extension runs.
 */
export declare function _configureEnvironment(overrides: EnvironmentConfig): void;
//# sourceMappingURL=environment.d.ts.map