/**
 * Open a URL in the default browser.
 */
export declare function open(target: string, application?: string): Promise<void>;
/**
 * Close the main Raycast window.
 * In CmdPal this maps to dismissing the palette.
 */
export declare function closeMainWindow(options?: {
    clearRootSearch?: boolean;
}): Promise<void>;
/**
 * Pop the current view from the navigation stack.
 * In CmdPal this maps to CommandResult.goBack().
 */
export declare function popToRoot(options?: {
    clearSearchBar?: boolean;
}): Promise<void>;
/**
 * Launch another Raycast command by name.
 * Stub: not supported in CmdPal spike.
 */
export declare function launchCommand(options: {
    name: string;
    type: string;
    extensionName?: string;
    ownerOrAuthorName?: string;
    arguments?: Record<string, string>;
    context?: Record<string, unknown>;
    fallbackText?: string;
}): Promise<void>;
/**
 * Confirm an action with a dialog.
 * For the spike: always resolves true (auto-confirms).
 */
export declare function confirmAlert(options: {
    title: string;
    message?: string;
    icon?: unknown;
    primaryAction?: {
        title: string;
        style?: string;
        onAction?: () => void;
    };
    dismissAction?: {
        title: string;
        style?: string;
        onAction?: () => void;
    };
    rememberUserChoice?: boolean;
}): Promise<boolean>;
//# sourceMappingURL=navigation.d.ts.map