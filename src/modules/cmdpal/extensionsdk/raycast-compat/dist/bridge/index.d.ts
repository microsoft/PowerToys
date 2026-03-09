import { RaycastBridgeProvider } from './bridge-provider';
export { RaycastBridgeProvider } from './bridge-provider';
export type { RaycastCommandManifest, RaycastExtensionManifest, RaycastCommandModule, NotifyFn, PageSnapshot, } from './bridge-provider';
type RequestHandler = (params: unknown) => Promise<unknown>;
declare class BridgeTransport {
    private handlers;
    private buffer;
    private nextLen;
    onRequest(method: string, handler: RequestHandler): void;
    sendNotification(method: string, params?: unknown): void;
    start(): void;
    private _tryParse;
    private _handleRequest;
    private _findHeaderEnd;
    private _write;
}
/**
 * Boot the bridge. Can be called programmatically (for testing) or
 * runs automatically when this file is the entry point.
 */
export declare function boot(options?: {
    extensionDir: string;
    command?: string;
    /** Override transport for testing (skip stdin/stdout). */
    transport?: BridgeTransport;
}): {
    provider: RaycastBridgeProvider;
    transport: BridgeTransport;
};
//# sourceMappingURL=index.d.ts.map