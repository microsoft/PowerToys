import type { InstalledExtension } from './types';
/**
 * List all Raycast-compat extensions installed in the CmdPal JSExtensions dir.
 *
 * Scans each subdirectory for both cmdpal.json and raycast-compat.json.
 * Only returns extensions that have the `installedBy: "raycast-pipeline"` marker.
 */
export declare function listInstalledExtensions(installDir?: string): InstalledExtension[];
/**
 * Uninstall a Raycast-compat extension from CmdPal.
 *
 * Removes the entire extension directory. Accepts either the CmdPal name
 * (prefixed with "raycast-") or the original Raycast extension name.
 *
 * @returns true if the extension was found and removed
 */
export declare function uninstallExtension(extensionName: string, installDir?: string): boolean;
//# sourceMappingURL=manage.d.ts.map