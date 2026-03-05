import { CommandProvider } from './command-provider';
import { ListPage, ContentPage } from './pages';
/**
 * Main entry point for Command Palette extensions.
 * Manages the JSON-RPC server and dispatches requests to the registered provider.
 */
export declare class ExtensionServer {
    private static provider?;
    private static transport?;
    private static pages;
    private static commands;
    /** Register a command provider. */
    static register(provider: CommandProvider): void;
    /** Start the JSON-RPC message loop. */
    static start(): void;
    private static registerHandlers;
    /** @internal */
    static _registerPage(page: ListPage | ContentPage): void;
    /** @internal */
    static _getPage(pageId: string): ListPage | ContentPage | undefined;
    /** @internal */
    static _registerResultCommands(result: any): void;
    /** @internal */
    static _registerItemCommands(items: any[]): void;
    private static _registerContextCommands;
    private static cleanup;
}
//# sourceMappingURL=extension-server.d.ts.map