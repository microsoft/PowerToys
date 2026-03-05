"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ExtensionServer = void 0;
const json_rpc_1 = require("../transport/json-rpc");
const pages_1 = require("./pages");
const types_1 = require("../generated/types");
/**
 * Main entry point for Command Palette extensions.
 * Manages the JSON-RPC server and dispatches requests to the registered provider.
 */
class ExtensionServer {
    /** Register a command provider. */
    static register(provider) {
        ExtensionServer.provider = provider;
    }
    /** Start the JSON-RPC message loop. */
    static start() {
        if (!ExtensionServer.provider) {
            throw new Error('No provider registered. Call ExtensionServer.register() first.');
        }
        const transport = new json_rpc_1.JsonRpcTransport();
        ExtensionServer.transport = transport;
        // Initialize transport on pages registered before start() was called
        for (const page of ExtensionServer.pages.values()) {
            if ('_initializeWithTransport' in page) {
                page._initializeWithTransport(transport);
            }
        }
        ExtensionServer.provider._initializeWithHost(transport);
        ExtensionServer.registerHandlers(transport);
        transport.start();
    }
    static registerHandlers(transport) {
        transport.onRequest('initialize', async () => {
            const hasSettings = ExtensionServer.provider?.settings != null;
            return {
                capabilities: {
                    providesTopLevelCommands: true,
                    providesFallbackCommands: true,
                    providesCommandDetails: true,
                    supportsDynamicPages: true,
                    supportsContentPages: true,
                    supportsForms: true,
                    supportsSettings: hasSettings,
                },
                version: '1.0.0',
            };
        });
        transport.onRequest('dispose', async () => {
            ExtensionServer.provider?.dispose();
            ExtensionServer.cleanup();
            return {};
        });
        transport.onRequest('provider/getTopLevelCommands', async () => {
            const items = ExtensionServer.provider?.topLevelCommands() ?? [];
            for (const item of items) {
                const cmd = item.command;
                if (cmd && cmd.id) {
                    ExtensionServer.commands.set(cmd.id, cmd);
                }
            }
            return items;
        });
        transport.onRequest('provider/getFallbackCommands', async () => {
            const items = ExtensionServer.provider?.fallbackCommands() ?? [];
            for (const item of items) {
                const cmd = item.command;
                if (cmd && cmd.id) {
                    ExtensionServer.commands.set(cmd.id, cmd);
                }
            }
            return items;
        });
        transport.onRequest('provider/getCommand', async (params) => {
            const commandId = params?.commandId;
            if (!commandId) {
                throw new Error('Missing commandId parameter');
            }
            const command = ExtensionServer.provider?.getCommand(commandId);
            if (!command) {
                throw new Error(`Command not found: ${commandId}`);
            }
            ExtensionServer.commands.set(commandId, command);
            if ('_type' in command) {
                const pageType = command._type;
                if (pageType === types_1.PageType.ListPage || pageType === types_1.PageType.DynamicListPage || pageType === types_1.PageType.ContentPage) {
                    ExtensionServer.pages.set(commandId, command);
                    if (ExtensionServer.transport && '_initializeWithTransport' in command) {
                        command._initializeWithTransport(ExtensionServer.transport);
                    }
                }
            }
            return command;
        });
        transport.onRequest('provider/getProperties', async () => {
            return {
                id: ExtensionServer.provider?.id ?? '',
                displayName: ExtensionServer.provider?.displayName ?? '',
                icon: ExtensionServer.provider?.icon,
                frozen: true,
            };
        });
        transport.onRequest('provider/getSettings', async () => {
            const commandSettings = ExtensionServer.provider?.settings;
            if (!commandSettings) {
                return null;
            }
            const settingsPage = commandSettings.settingsPage;
            // Register the settings page so contentPage/getContent and form/submit
            // can interact with it through the existing handlers.
            ExtensionServer.pages.set(settingsPage.id, settingsPage);
            if (ExtensionServer.transport) {
                settingsPage._initializeWithTransport(ExtensionServer.transport);
            }
            // Initialize transport on the form content items
            const content = settingsPage.getContent();
            for (const c of content) {
                if ('_initializeWithTransport' in c) {
                    c._initializeWithTransport(ExtensionServer.transport);
                }
            }
            return settingsPage;
        });
        transport.onRequest('command/invoke', async (params) => {
            const commandId = params?.commandId;
            if (!commandId) {
                throw new Error('Missing commandId parameter');
            }
            let command = ExtensionServer.commands.get(commandId);
            if (!command) {
                command = ExtensionServer.provider?.getCommand(commandId);
                if (command) {
                    ExtensionServer.commands.set(commandId, command);
                }
            }
            if (!command) {
                throw new Error(`Command not found: ${commandId}`);
            }
            if (typeof command.invoke === 'function') {
                const result = command.invoke(params?.sender);
                ExtensionServer._registerResultCommands(result);
                return result;
            }
            throw new Error(`Command ${commandId} is not invokable`);
        });
        transport.onRequest('listPage/getItems', async (params) => {
            const pageId = params?.pageId;
            if (!pageId) {
                throw new Error('Missing pageId parameter');
            }
            const page = ExtensionServer.pages.get(pageId);
            if (!page || !(page instanceof pages_1.ListPage)) {
                throw new Error(`List page not found: ${pageId}`);
            }
            const items = page.getItems();
            ExtensionServer._registerItemCommands(items);
            return { items, totalItems: items.length };
        });
        transport.onRequest('listPage/setSearchText', async (params) => {
            const pageId = params?.pageId;
            const searchText = params?.searchText ?? '';
            if (!pageId) {
                throw new Error('Missing pageId parameter');
            }
            const page = ExtensionServer.pages.get(pageId);
            if (!page || !(page instanceof pages_1.DynamicListPage)) {
                throw new Error(`Dynamic list page not found: ${pageId}`);
            }
            page.setSearchText(searchText);
            const items = page.getItems();
            ExtensionServer._registerItemCommands(items);
            return { updatedItemCount: items.length };
        });
        transport.onRequest('listPage/loadMore', async (params) => {
            const pageId = params?.pageId;
            if (!pageId) {
                throw new Error('Missing pageId parameter');
            }
            const page = ExtensionServer.pages.get(pageId);
            if (!page || !(page instanceof pages_1.ListPage)) {
                throw new Error(`List page not found: ${pageId}`);
            }
            page.loadMore();
            const items = page.getItems();
            ExtensionServer._registerItemCommands(items);
            return { newItemCount: items.length };
        });
        transport.onRequest('listPage/setFilter', async (params) => {
            const pageId = params?.pageId;
            const filterId = params?.filterId ?? '';
            if (!pageId) {
                throw new Error('Missing pageId parameter');
            }
            const page = ExtensionServer.pages.get(pageId);
            if (!page || !(page instanceof pages_1.ListPage)) {
                throw new Error(`List page not found: ${pageId}`);
            }
            if (page.filters) {
                page.filters.currentFilterId = filterId;
            }
            const items = page.getItems();
            ExtensionServer._registerItemCommands(items);
            // Notify C# that items changed so it re-fetches the list
            transport.sendNotification('listPage/itemsChanged', {
                pageId,
                totalItems: items.length,
            });
            return { updatedItemCount: items.length };
        });
        transport.onRequest('contentPage/getContent', async (params) => {
            const pageId = params?.pageId;
            if (!pageId) {
                throw new Error('Missing pageId parameter');
            }
            const page = ExtensionServer.pages.get(pageId);
            if (!page || !(page instanceof pages_1.ContentPage)) {
                throw new Error(`Content page not found: ${pageId}`);
            }
            return { content: page.getContent() };
        });
        transport.onRequest('form/submit', async (params) => {
            const pageId = params?.pageId;
            const inputs = params?.inputs ?? '{}';
            const data = params?.data ?? '{}';
            if (!pageId) {
                throw new Error('Missing pageId parameter');
            }
            const page = ExtensionServer.pages.get(pageId);
            if (!page || !(page instanceof pages_1.ContentPage)) {
                throw new Error(`Content page not found: ${pageId}`);
            }
            const content = page.getContent();
            if (!content || content.length === 0) {
                throw new Error(`No content found for page: ${pageId}`);
            }
            const formContent = content.find((c) => 'submitForm' in c);
            if (!formContent || !('submitForm' in formContent)) {
                throw new Error(`No form content found for page: ${pageId}`);
            }
            return formContent.submitForm(inputs, data);
        });
    }
    /** @internal */
    static _registerPage(page) {
        ExtensionServer.pages.set(page.id, page);
        if (ExtensionServer.transport) {
            page._initializeWithTransport(ExtensionServer.transport);
        }
    }
    /** @internal */
    static _getPage(pageId) {
        return ExtensionServer.pages.get(pageId);
    }
    /** @internal */
    static _registerResultCommands(result) {
        if (!result || typeof result !== 'object') {
            return;
        }
        const args = result.args;
        if (!args || typeof args !== 'object') {
            return;
        }
        const primaryCmd = args.primaryCommand;
        if (primaryCmd && primaryCmd.id) {
            ExtensionServer.commands.set(primaryCmd.id, primaryCmd);
        }
        const cmd = args.command;
        if (cmd && cmd.id) {
            ExtensionServer.commands.set(cmd.id, cmd);
        }
    }
    /** @internal */
    static _registerItemCommands(items) {
        for (const item of items) {
            const cmd = item?.command;
            if (cmd && cmd.id) {
                ExtensionServer.commands.set(cmd.id, cmd);
            }
            const moreCommands = item?.moreCommands;
            if (Array.isArray(moreCommands)) {
                ExtensionServer._registerContextCommands(moreCommands);
            }
        }
    }
    static _registerContextCommands(contextItems) {
        for (const ctxItem of contextItems) {
            const ctxCmd = ctxItem?.command;
            if (ctxCmd && ctxCmd.id) {
                ExtensionServer.commands.set(ctxCmd.id, ctxCmd);
            }
            const nested = ctxItem?.moreCommands;
            if (Array.isArray(nested)) {
                ExtensionServer._registerContextCommands(nested);
            }
        }
    }
    static cleanup() {
        ExtensionServer.pages.clear();
        ExtensionServer.commands.clear();
        if (ExtensionServer.transport) {
            ExtensionServer.transport.stop();
            ExtensionServer.transport = undefined;
        }
    }
}
exports.ExtensionServer = ExtensionServer;
ExtensionServer.pages = new Map();
ExtensionServer.commands = new Map();
//# sourceMappingURL=extension-server.js.map