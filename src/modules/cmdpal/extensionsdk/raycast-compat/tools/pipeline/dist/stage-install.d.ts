/** Default CmdPal JSExtensions install directory. */
export declare function getDefaultInstallDir(): string;
export interface InstallResult {
    /** Whether installation succeeded. */
    success: boolean;
    /** Absolute path to the installed extension. */
    extensionPath: string;
    /** Errors encountered. */
    errors: string[];
}
/**
 * Install a built extension by atomically placing it in the CmdPal
 * JSExtensions directory.
 *
 * Strategy (to avoid triggering CmdPal's source file watcher during writes):
 *   1. Read cmdpal.json from the build output to get the extension name
 *   2. Copy all files to a temporary staging directory (<name>.installing)
 *   3. Remove the existing extension directory (if present)
 *   4. Atomically rename the staging directory to the final name
 *   5. Verify the installed cmdpal.json and entry point
 *
 * This ensures that the extension directory transitions from
 * "not present" → "fully populated" in a single rename operation,
 * minimising the window where CmdPal's file watchers see partial state.
 */
export declare function installExtension(buildOutputDir: string, installBaseDir: string): InstallResult;
//# sourceMappingURL=stage-install.d.ts.map