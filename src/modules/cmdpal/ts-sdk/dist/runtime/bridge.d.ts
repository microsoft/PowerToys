import type { IExtensionHost } from '../types';
/**
 * The JSONRPC bridge connects an extension's CommandProvider to the Node host's
 * JSONRPC channel. When the Node host loads an extension, it calls activate(),
 * which returns a CommandProvider. The Node host then uses this provider directly
 * via the protocol handlers (see nodehost/src/protocol.ts).
 *
 * The bridge's role is to:
 * 1. Initialize the ExtensionHost static class with the host callbacks
 * 2. Wire up property change notifications to send JSONRPC notifications
 *
 * In the current architecture, the bridge is thin because the Node host
 * directly invokes methods on the provider. The bridge primarily enables
 * the extension to send notifications back to the C# host.
 */
/**
 * Creates an IExtensionHost that sends notifications over the provided callback functions.
 * This is used by the Node host's extensionLoader to give each extension access to host APIs.
 */
export declare function createExtensionHostBridge(sendNotification: (method: string, params: unknown) => void, extensionId: string): IExtensionHost;
/**
 * Creates a notification sender that extensions can use to signal data changes.
 * The DynamicListPageBase.notifyItemsChanged() method uses this internally.
 */
export declare function createItemsChangedNotifier(sendNotification: (method: string, params: unknown) => void, extensionId: string): (pageId: string, totalItems?: number) => void;
/**
 * Creates a notification sender for property changes.
 */
export declare function createPropChangedNotifier(sendNotification: (method: string, params: unknown) => void, extensionId: string): (objectId: string, propertyName: string) => void;
//# sourceMappingURL=bridge.d.ts.map