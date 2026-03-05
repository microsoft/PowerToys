import { ICommandResult, ICommandResultArgs, CommandResultKind, NavigationMode, ICommand } from '../generated/types';
/**
 * Helper class for creating command results.
 */
export declare class CommandResult implements ICommandResult {
    kind: CommandResultKind;
    args?: ICommandResultArgs;
    constructor(kind: CommandResultKind, args?: ICommandResultArgs);
    /** Dismiss the command palette. */
    static dismiss(): CommandResult;
    /** Navigate to the home page. */
    static goHome(): CommandResult;
    /** Navigate back to the previous page. */
    static goBack(): CommandResult;
    /** Hide the command palette without dismissing. */
    static hide(): CommandResult;
    /** Keep the command palette open. */
    static keepOpen(): CommandResult;
    /**
     * Navigate to a specific page.
     * @param pageId The ID of the page to navigate to
     * @param mode The navigation mode (default: Push)
     */
    static goToPage(pageId: string, mode?: NavigationMode): CommandResult;
    /**
     * Show a toast notification.
     * @param message The message to display
     * @param dismissAfterMs Optional auto-dismiss time in milliseconds
     */
    static showToast(message: string, dismissAfterMs?: number): CommandResult;
    /**
     * Show a confirmation dialog.
     * @param title The dialog title
     * @param description The dialog description
     * @param primaryCommand The command to execute on confirmation
     */
    static confirm(title: string, description: string, primaryCommand: ICommand): CommandResult;
}
//# sourceMappingURL=results.d.ts.map