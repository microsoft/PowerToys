// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ICommandProvider, IExtensionHost, MessageState, ProgressState } from '../types';
import { ExtensionHost } from './ExtensionHost';

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
export function createExtensionHostBridge(
  sendNotification: (method: string, params: unknown) => void,
  extensionId: string
): IExtensionHost {
  const host: IExtensionHost = {
    log(message: string, state: MessageState = 'info'): void {
      sendNotification('host/log', { extensionId, state, message });
    },
    showStatus(message: string, state: MessageState = 'info', progress?: ProgressState): void {
      sendNotification('host/showStatus', { extensionId, context: 'page', message, state, progress });
    },
    hideStatus(messageId: string): void {
      sendNotification('host/hideStatus', { extensionId, messageId });
    },
    copyToClipboard(text: string): void {
      sendNotification('host/copyText', { text });
    },
  };

  // Also initialize the static ExtensionHost for convenience
  ExtensionHost.initialize(host);

  return host;
}

/**
 * Creates a notification sender that extensions can use to signal data changes.
 * The DynamicListPageBase.notifyItemsChanged() method uses this internally.
 */
export function createItemsChangedNotifier(
  sendNotification: (method: string, params: unknown) => void,
  extensionId: string
): (pageId: string, totalItems?: number) => void {
  return (pageId: string, totalItems?: number) => {
    sendNotification('notify/itemsChanged', { extensionId, pageId, totalItems });
  };
}

/**
 * Creates a notification sender for property changes.
 */
export function createPropChangedNotifier(
  sendNotification: (method: string, params: unknown) => void,
  extensionId: string
): (objectId: string, propertyName: string) => void {
  return (objectId: string, propertyName: string) => {
    sendNotification('notify/propChanged', { extensionId, objectId, propertyName });
  };
}
