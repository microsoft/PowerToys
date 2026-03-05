"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.CommandProvider = void 0;
const types_1 = require("../generated/types");
/**
 * Abstract base class for building Command Palette extensions.
 * Extend this class and implement the required abstract properties and methods.
 */
class CommandProvider {
    /**
     * Icon for this provider (optional).
     */
    get icon() {
        return undefined;
    }
    /**
     * Returns top-level commands shown in the command palette.
     * Override to provide your extension's main commands.
     */
    topLevelCommands() {
        return [];
    }
    /**
     * Returns fallback commands that execute when no other command matches.
     * Override to provide fallback functionality.
     */
    fallbackCommands() {
        return [];
    }
    /**
     * Gets a specific command by ID.
     * Override to provide detailed command information and pages.
     */
    getCommand(id) {
        return undefined;
    }
    /**
     * Returns the settings page for this extension.
     * Override to expose a settings form to users.
     */
    get settings() {
        return undefined;
    }
    /**
     * Called by the framework to initialize the provider with the host.
     * Do not call this directly.
     */
    _initializeWithHost(transport) {
        this._transport = transport;
        // Note: Host proxy will be implemented in extension-server.ts
    }
    /**
     * Log a message to the host.
     * @param message The message to log
     * @param state The severity level (default: Info)
     */
    log(message, state = types_1.MessageState.Info) {
        if (!this._transport) {
            return;
        }
        this._transport.sendNotification('host/logMessage', {
            message,
            state,
        });
    }
    /**
     * Show a status message to the user.
     * @param message The status message object
     * @param context The context for the status message
     */
    showStatus(message, context = types_1.StatusContext.Extension) {
        if (!this._transport) {
            return;
        }
        this._transport.sendNotification('host/showStatus', {
            message,
            context,
        });
    }
    /**
     * Hide a previously shown status message.
     * @param message The status message to hide
     */
    hideStatus(message) {
        if (!this._transport) {
            return;
        }
        this._transport.sendNotification('host/hideStatus', {
            message,
        });
    }
    /**
     * Notify the host that items have changed.
     * Call this when your top-level commands or fallback commands change.
     * @param totalItems Optional total number of items
     */
    notifyItemsChanged(totalItems) {
        if (!this._transport) {
            return;
        }
        this._transport.sendNotification('provider/itemsChanged', {
            totalItems: totalItems ?? -1,
        });
    }
    /**
     * Notify the host that a property has changed.
     * @param propertyName The name of the property that changed
     */
    notifyPropChanged(propertyName) {
        if (!this._transport) {
            return;
        }
        this._transport.sendNotification('provider/propChanged', {
            propertyName,
        });
    }
    /**
     * Clean up resources when the extension is disposed.
     * Override to implement custom cleanup logic.
     */
    dispose() {
        // Default: no-op
    }
    close() {
        this.dispose();
    }
}
exports.CommandProvider = CommandProvider;
//# sourceMappingURL=command-provider.js.map