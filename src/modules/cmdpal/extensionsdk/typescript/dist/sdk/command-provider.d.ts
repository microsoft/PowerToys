import { ICommandProvider, ICommandItem, IFallbackCommandItem, ICommand, IIconInfo, MessageState, IStatusMessage, StatusContext } from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';
import type { CommandSettings } from './settings';
/**
 * Abstract base class for building Command Palette extensions.
 * Extend this class and implement the required abstract properties and methods.
 */
export declare abstract class CommandProvider implements ICommandProvider {
    private _transport?;
    private _host?;
    /**
     * Unique identifier for this extension provider.
     */
    abstract get id(): string;
    /**
     * Display name shown to users.
     */
    abstract get displayName(): string;
    /**
     * Icon for this provider (optional).
     */
    get icon(): IIconInfo | undefined;
    /**
     * Returns top-level commands shown in the command palette.
     * Override to provide your extension's main commands.
     */
    topLevelCommands(): ICommandItem[];
    /**
     * Returns fallback commands that execute when no other command matches.
     * Override to provide fallback functionality.
     */
    fallbackCommands(): IFallbackCommandItem[];
    /**
     * Gets a specific command by ID.
     * Override to provide detailed command information and pages.
     */
    getCommand(id: string): ICommand | undefined;
    /**
     * Returns the settings page for this extension.
     * Override to expose a settings form to users.
     */
    get settings(): CommandSettings | undefined;
    /**
     * Called by the framework to initialize the provider with the host.
     * Do not call this directly.
     */
    _initializeWithHost(transport: JsonRpcTransport): void;
    /**
     * Log a message to the host.
     * @param message The message to log
     * @param state The severity level (default: Info)
     */
    protected log(message: string, state?: MessageState): void;
    /**
     * Show a status message to the user.
     * @param message The status message object
     * @param context The context for the status message
     */
    protected showStatus(message: IStatusMessage, context?: StatusContext): void;
    /**
     * Hide a previously shown status message.
     * @param message The status message to hide
     */
    protected hideStatus(message: IStatusMessage): void;
    /**
     * Notify the host that items have changed.
     * Call this when your top-level commands or fallback commands change.
     * @param totalItems Optional total number of items
     */
    protected notifyItemsChanged(totalItems?: number): void;
    /**
     * Notify the host that a property has changed.
     * @param propertyName The name of the property that changed
     */
    protected notifyPropChanged(propertyName: string): void;
    /**
     * Clean up resources when the extension is disposed.
     * Override to implement custom cleanup logic.
     */
    dispose(): void;
    close(): void;
}
//# sourceMappingURL=command-provider.d.ts.map