"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.createExtensionHostBridge = createExtensionHostBridge;
exports.createItemsChangedNotifier = createItemsChangedNotifier;
exports.createPropChangedNotifier = createPropChangedNotifier;
const ExtensionHost_1 = require("./ExtensionHost");
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
function createExtensionHostBridge(sendNotification, extensionId) {
    const host = {
        log(message, state = 'info') {
            sendNotification('host/log', { extensionId, state, message });
        },
        showStatus(message, state = 'info', progress) {
            sendNotification('host/showStatus', { extensionId, context: 'page', message, state, progress });
        },
        hideStatus(messageId) {
            sendNotification('host/hideStatus', { extensionId, messageId });
        },
    };
    // Also initialize the static ExtensionHost for convenience
    ExtensionHost_1.ExtensionHost.initialize(host);
    return host;
}
/**
 * Creates a notification sender that extensions can use to signal data changes.
 * The DynamicListPageBase.notifyItemsChanged() method uses this internally.
 */
function createItemsChangedNotifier(sendNotification, extensionId) {
    return (pageId, totalItems) => {
        sendNotification('notify/itemsChanged', { extensionId, pageId, totalItems });
    };
}
/**
 * Creates a notification sender for property changes.
 */
function createPropChangedNotifier(sendNotification, extensionId) {
    return (objectId, propertyName) => {
        sendNotification('notify/propChanged', { extensionId, objectId, propertyName });
    };
}
//# sourceMappingURL=bridge.js.map