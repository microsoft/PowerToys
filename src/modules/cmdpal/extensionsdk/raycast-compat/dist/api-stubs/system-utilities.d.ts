/**
 * Reveal a file or folder in the system file manager.
 *
 * macOS: Finder → `open -R <path>`
 * Windows: Explorer → `explorer /select,"<path>"`
 */
export declare function showInFinder(path: string): Promise<void>;
/**
 * Move a file or folder to the system trash/recycle bin.
 *
 * macOS: Finder trash
 * Windows: Recycle Bin via PowerShell
 */
export declare function trash(path: string | string[]): Promise<void>;
/**
 * Show a brief heads-up display message.
 * Maps to a CmdPal toast with auto-dismiss style.
 */
export declare function showHUD(title: string): Promise<void>;
/**
 * Get the currently selected text in the frontmost application.
 * Stub: not supported on Windows in the CmdPal compat layer.
 */
export declare function getSelectedText(): Promise<string>;
export interface FileSystemItem {
    path: string;
}
/**
 * Get the files/folders currently selected in the file manager.
 * Stub: not supported in CmdPal.
 */
export declare function getSelectedFinderItems(): Promise<FileSystemItem[]>;
export interface Application {
    name: string;
    path: string;
    bundleId?: string;
}
/**
 * Get a list of installed applications.
 * Stub: returns an empty array.
 */
export declare function getApplications(): Promise<Application[]>;
/**
 * Get the default application for a file type or URL scheme.
 * Stub: returns a placeholder.
 */
export declare function getDefaultApplication(pathOrUrl: string): Promise<Application>;
/**
 * Get the frontmost (focused) application.
 * Stub: returns a placeholder.
 */
export declare function getFrontmostApplication(): Promise<Application>;
//# sourceMappingURL=system-utilities.d.ts.map