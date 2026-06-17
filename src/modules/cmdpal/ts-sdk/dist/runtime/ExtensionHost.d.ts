import type { IExtensionHost, MessageState, ProgressState } from '../types';
/**
 * Provides access to the Command Palette host from within an extension.
 * Use this to send log messages, show/hide status indicators, copy text, etc.
 *
 * @example
 * ```typescript
 * import { ExtensionHost } from '@microsoft/cmdpal-sdk';
 *
 * ExtensionHost.log('Extension loaded successfully');
 * ExtensionHost.showStatus('Loading data...', 'info', { isIndeterminate: true });
 * ExtensionHost.copyToClipboard('Hello, world!');
 * ```
 */
export declare class ExtensionHost {
    private static _instance;
    /**
     * Initializes the ExtensionHost with the host provided during activation.
     * This is called automatically by the runtime bridge — do not call directly.
     * @internal
     */
    static initialize(host: IExtensionHost): void;
    /**
     * Sends a log message to the Command Palette host.
     */
    static log(message: string, state?: MessageState): void;
    /**
     * Shows a status message in the Command Palette UI.
     */
    static showStatus(message: string, state?: MessageState, progress?: ProgressState): void;
    /**
     * Hides a previously shown status message.
     */
    static hideStatus(messageId: string): void;
    /**
     * Copies text to the system clipboard via the host.
     */
    static copyToClipboard(text: string): void;
    /**
     * Gets whether the host has been initialized.
     */
    static get isInitialized(): boolean;
}
//# sourceMappingURL=ExtensionHost.d.ts.map