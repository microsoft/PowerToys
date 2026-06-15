"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ExtensionHost = void 0;
/**
 * Provides access to the Command Palette host from within an extension.
 * Use this to send log messages, show/hide status indicators, etc.
 *
 * @example
 * ```typescript
 * import { ExtensionHost } from '@microsoft/cmdpal-sdk';
 *
 * ExtensionHost.log('Extension loaded successfully');
 * ExtensionHost.showStatus('Loading data...', 'info', { isIndeterminate: true });
 * ```
 */
class ExtensionHost {
    static _instance = null;
    /**
     * Initializes the ExtensionHost with the host provided during activation.
     * This is called automatically by the runtime bridge — do not call directly.
     * @internal
     */
    static initialize(host) {
        ExtensionHost._instance = host;
    }
    /**
     * Sends a log message to the Command Palette host.
     */
    static log(message, state = 'info') {
        ExtensionHost._instance?.log(message, state);
    }
    /**
     * Shows a status message in the Command Palette UI.
     */
    static showStatus(message, state = 'info', progress) {
        ExtensionHost._instance?.showStatus(message, state, progress);
    }
    /**
     * Hides a previously shown status message.
     */
    static hideStatus(messageId) {
        ExtensionHost._instance?.hideStatus(messageId);
    }
    /**
     * Gets whether the host has been initialized.
     */
    static get isInitialized() {
        return ExtensionHost._instance !== null;
    }
}
exports.ExtensionHost = ExtensionHost;
//# sourceMappingURL=ExtensionHost.js.map