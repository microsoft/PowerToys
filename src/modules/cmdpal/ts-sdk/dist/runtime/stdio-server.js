"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.sendNotification = sendNotification;
exports.startJsonRpcServer = startJsonRpcServer;
const ExtensionHost_1 = require("./ExtensionHost");
let provider = null;
let commandCache = new Map();
let fallbackCommandCache = new Map();
function sendMessage(message) {
    const json = JSON.stringify(message);
    const contentBytes = Buffer.from(json, 'utf-8');
    const header = `Content-Length: ${contentBytes.length}\r\n\r\n`;
    // Write header + body as single buffer to avoid interleaving
    const headerBytes = Buffer.from(header, 'ascii');
    const packet = Buffer.concat([headerBytes, contentBytes]);
    process.stdout.write(packet);
}
function sendResponse(id, result) {
    sendMessage({ jsonrpc: '2.0', id, result: result ?? null });
}
function sendError(id, code, message) {
    sendMessage({ jsonrpc: '2.0', id, error: { code, message } });
}
function sendNotification(method, params) {
    sendMessage({ jsonrpc: '2.0', method, params });
}
function patchPageNotifications(pageId, page) {
    if (page && typeof page === 'object' && 'notifyItemsChanged' in page) {
        page.notifyItemsChanged = () => {
            sendNotification('listPage/itemsChanged', { pageId });
        };
    }
}
function cacheCommand(command, cacheId) {
    commandCache.set(command.id, command);
    if (cacheId && cacheId !== command.id) {
        commandCache.set(cacheId, command);
    }
    patchPageNotifications(command.id, command);
}
async function getCachedCommand(commandId) {
    const cached = commandCache.get(commandId);
    if (cached) {
        return cached;
    }
    const command = await Promise.resolve(provider?.getCommand?.(commandId) ?? null);
    if (command) {
        cacheCommand(command, commandId);
    }
    return command;
}
function serializeContextItems(items) {
    if (!items || items.length === 0) {
        return undefined;
    }
    return items.map((ctx) => {
        const ctxResult = {
            command: serializeCommand(ctx.command),
            title: ctx.title,
        };
        if (ctx.subtitle)
            ctxResult.subtitle = ctx.subtitle;
        if (ctx.icon)
            ctxResult.icon = ctx.icon;
        if (ctx.isCritical)
            ctxResult.isCritical = ctx.isCritical;
        if (ctx.requestedShortcut)
            ctxResult.requestedShortcut = ctx.requestedShortcut;
        return ctxResult;
    });
}
async function serializeContent(content) {
    switch (content.type) {
        case 'markdown':
            return { type: 'markdown', body: content.body };
        case 'plainText': {
            const pt = content;
            return { type: 'plainText', text: pt.text, fontFamily: pt.fontFamily, wrapWords: pt.wrapWords };
        }
        case 'image': {
            const img = content;
            return { type: 'image', image: img.image, maxWidth: img.maxWidth, maxHeight: img.maxHeight };
        }
        case 'form': {
            const form = content;
            return { type: 'form', templateJson: form.templateJson, dataJson: form.dataJson, stateJson: form.stateJson };
        }
        case 'tree': {
            const tree = content;
            const children = await Promise.resolve(tree.getChildren());
            return {
                type: 'tree',
                rootContent: await serializeContent(tree.rootContent),
                children: await Promise.all(children.map((child) => serializeContent(child))),
            };
        }
        default:
            return content;
    }
}
async function handleRequest(request) {
    const { id, method, params } = request;
    const p = params;
    try {
        switch (method) {
            case 'initialize': {
                // Extension initialization
                sendResponse(id, { capabilities: ['commands'] });
                break;
            }
            case 'provider/getTopLevelCommands': {
                if (!provider) {
                    sendError(id, -32603, 'Provider not initialized');
                    return;
                }
                const commands = await Promise.resolve(provider.topLevelCommands());
                for (const item of commands) {
                    if (item.command) {
                        cacheCommand(item.command);
                    }
                }
                sendResponse(id, serializeCommandItems(commands));
                break;
            }
            case 'provider/getFallbackCommands': {
                if (!provider) {
                    sendError(id, -32603, 'Provider not initialized');
                    return;
                }
                const fallbacks = await Promise.resolve(provider.fallbackCommands?.() ?? null);
                if (fallbacks) {
                    for (const item of fallbacks) {
                        cacheCommand(item.command);
                        fallbackCommandCache.set(item.command.id, item);
                    }
                }
                sendResponse(id, fallbacks ? serializeCommandItems(fallbacks) : null);
                break;
            }
            case 'provider/getCommand': {
                if (!provider) {
                    sendError(id, -32603, 'Provider not initialized');
                    return;
                }
                const commandId = p?.commandId ?? '';
                const command = await Promise.resolve(provider.getCommand?.(commandId) ?? null);
                if (command) {
                    cacheCommand(command, commandId);
                    sendResponse(id, serializeCommand(command));
                }
                else {
                    sendResponse(id, null);
                }
                break;
            }
            case 'provider/getSettings': {
                if (!provider) {
                    sendError(id, -32603, 'Provider not initialized');
                    return;
                }
                const settings = provider.settings ?? null;
                if (settings?.settingsPage) {
                    cacheCommand(settings.settingsPage);
                }
                sendResponse(id, settings ? { id: settings.settingsPage?.id ?? '' } : null);
                break;
            }
            case 'command/invoke': {
                const cmdId = p?.commandId ?? '';
                const cmd = await getCachedCommand(cmdId);
                if (cmd && 'invoke' in cmd && typeof cmd.invoke === 'function') {
                    const result = await Promise.resolve(cmd.invoke(null));
                    sendResponse(id, serializeCommandResult(result));
                }
                else {
                    sendError(id, -32601, `Command not found or not invokable: ${cmdId}`);
                }
                break;
            }
            case 'listPage/getItems': {
                const pageId = p?.pageId ?? '';
                const page = await getCachedCommand(pageId);
                if (page && 'getItems' in page && typeof page.getItems === 'function') {
                    const items = await Promise.resolve(page.getItems());
                    sendResponse(id, { items: serializeListItems(items) });
                }
                else {
                    sendResponse(id, { items: [] });
                }
                break;
            }
            case 'listPage/setSearchText': {
                const pageId = p?.pageId ?? '';
                const searchText = p?.searchText ?? '';
                const page = await getCachedCommand(pageId);
                if (page && 'setSearchText' in page && typeof page.setSearchText === 'function') {
                    await Promise.resolve(page.setSearchText(searchText));
                }
                sendResponse(id, null);
                break;
            }
            case 'listPage/setFilter': {
                const pageId = p?.pageId ?? '';
                const filterId = p?.filterId ?? '';
                const page = await getCachedCommand(pageId);
                if (page && 'setFilter' in page && typeof page.setFilter === 'function') {
                    await Promise.resolve(page.setFilter(filterId));
                }
                sendResponse(id, null);
                break;
            }
            case 'fallback/updateQuery': {
                const commandId = p?.commandId ?? '';
                const query = p?.query ?? '';
                const item = fallbackCommandCache.get(commandId);
                if (item?.fallbackHandler && typeof item.fallbackHandler.updateQuery === 'function') {
                    await Promise.resolve(item.fallbackHandler.updateQuery(query));
                    // Notify the host that the fallback item's properties changed
                    sendNotification('command/propChanged', { commandId, properties: { displayTitle: item.displayTitle ?? item.title } });
                }
                sendResponse(id, null);
                break;
            }
            case 'contentPage/getContent': {
                const pageId = p?.pageId ?? '';
                const page = await getCachedCommand(pageId);
                if (page && 'getContent' in page && typeof page.getContent === 'function') {
                    const rawContent = await Promise.resolve(page.getContent());
                    const serialized = await Promise.all(rawContent.map((item) => serializeContent(item)));
                    sendResponse(id, serialized);
                }
                else {
                    sendResponse(id, []);
                }
                break;
            }
            case 'form/submit': {
                const pageId = p?.pageId ?? '';
                const inputs = p?.inputs ?? '';
                const data = p?.data ?? '';
                const page = await getCachedCommand(pageId);
                if (page && 'getContent' in page && typeof page.getContent === 'function') {
                    const content = await Promise.resolve(page.getContent());
                    const form = content.find((item) => item.type === 'form' && typeof item.submitForm === 'function');
                    if (!form) {
                        sendError(id, -32601, `Form content not found for page: ${pageId}`);
                        break;
                    }
                    const result = await Promise.resolve(form.submitForm(inputs, data));
                    sendResponse(id, serializeCommandResult(result));
                }
                else if (page && 'submitForm' in page && typeof page.submitForm === 'function') {
                    const result = await Promise.resolve(page.submitForm(inputs, data));
                    sendResponse(id, serializeCommandResult(result));
                }
                else {
                    sendError(id, -32601, `Form page not found: ${pageId}`);
                }
                break;
            }
            case 'listPage/loadMore': {
                const pageId = p?.pageId ?? '';
                const page = await getCachedCommand(pageId);
                if (page && 'loadMore' in page && typeof page.loadMore === 'function') {
                    await Promise.resolve(page.loadMore());
                }
                sendResponse(id, null);
                break;
            }
            default:
                sendError(id, -32601, `Method not found: ${method}`);
        }
    }
    catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        sendError(id, -32603, message);
    }
}
async function handleNotification(notification) {
    const { method, params } = notification;
    const p = params;
    if (method === 'fallback/updateQuery') {
        const commandId = p?.commandId ?? '';
        const query = p?.query ?? '';
        const item = fallbackCommandCache.get(commandId);
        if (item?.fallbackHandler && typeof item.fallbackHandler.updateQuery === 'function') {
            await Promise.resolve(item.fallbackHandler.updateQuery(query));
            // Notify the host that the fallback item's properties changed
            sendNotification('command/propChanged', { commandId, properties: { displayTitle: item.displayTitle ?? item.title } });
        }
        return;
    }
    if (method === 'dispose') {
        provider = null;
        commandCache.clear();
        fallbackCommandCache.clear();
        process.exit(0);
    }
}
function serializeCommand(command) {
    cacheCommand(command);
    const result = {
        id: command.id,
        name: command.name,
        displayName: command.name,
    };
    if (command.icon) {
        result.icon = command.icon;
    }
    // Detect page types
    if ('getItems' in command && typeof command.getItems === 'function') {
        if ('setSearchText' in command || 'searchText' in command) {
            result._type = 'dynamicListPage';
        }
        else {
            result._type = 'listPage';
        }
        // Copy page properties
        if ('isLoading' in command)
            result.isLoading = command.isLoading;
        if ('accentColor' in command)
            result.accentColor = command.accentColor;
        if ('placeholderText' in command)
            result.placeholderText = command.placeholderText;
        if ('showDetails' in command)
            result.showDetails = command.showDetails;
        if ('title' in command)
            result.title = command.title;
        if ('gridProperties' in command) {
            const gp = command.gridProperties;
            if (gp) {
                // C# expects 'layout' property, but TS GridProperties uses 'type'
                result.gridProperties = { ...gp, layout: gp.layout ?? gp.type };
            }
        }
        if ('filters' in command)
            result.filters = command.filters;
        if ('hasMoreItems' in command)
            result.hasMoreItems = command.hasMoreItems;
        if ('emptyContent' in command)
            result.emptyContent = command.emptyContent;
    }
    else if ('getContent' in command && typeof command.getContent === 'function') {
        result._type = 'contentPage';
        if ('title' in command)
            result.title = command.title;
        if ('isLoading' in command)
            result.isLoading = command.isLoading;
        if ('accentColor' in command)
            result.accentColor = command.accentColor;
        if ('details' in command)
            result.details = command.details;
        if ('commands' in command)
            result.commands = serializeContextItems(command.commands);
    }
    return result;
}
function serializeCommandItems(items) {
    return items.map((item) => {
        const result = {
            id: item.command.id,
            title: item.title,
            displayName: item.title,
            subtitle: item.subtitle,
            command: item.command ? serializeCommand(item.command) : undefined,
        };
        if (item.icon) {
            result.icon = item.icon;
        }
        if ('displayTitle' in item && item.displayTitle) {
            result.displayTitle = item.displayTitle;
        }
        if (item.moreCommands && item.moreCommands.length > 0) {
            result.moreCommands = serializeContextItems(item.moreCommands);
        }
        return result;
    });
}
function serializeListItems(items) {
    return items.map((item) => {
        // Check if this is a separator
        if ('_isSeparator' in item && item._isSeparator) {
            return { _isSeparator: true, title: item.title, section: item.section };
        }
        const result = {
            title: item.title,
            displayName: item.title,
            subtitle: item.subtitle,
            section: item.section,
            tags: item.tags,
            textToSuggest: item.textToSuggest,
            command: item.command ? serializeCommand(item.command) : undefined,
        };
        if (item.icon) {
            result.icon = item.icon;
        }
        if (item.details) {
            result.details = serializeDetails(item.details);
        }
        if (item.moreCommands && item.moreCommands.length > 0) {
            result.moreCommands = serializeContextItems(item.moreCommands);
        }
        return result;
    });
}
function serializeDetails(details) {
    if (!details || typeof details !== 'object')
        return details;
    const d = details;
    const result = { ...d };
    // Serialize metadata commands (DetailsCommands contain ICommand[] that need proper serialization)
    if (Array.isArray(d.metadata)) {
        result.metadata = d.metadata.map((element) => {
            const el = { ...element };
            if (el.data && typeof el.data === 'object') {
                const data = el.data;
                // Serialize commands within DetailsCommands
                if (Array.isArray(data.commands)) {
                    el.data = {
                        ...data,
                        commands: data.commands.map((cmd) => {
                            if (cmd && typeof cmd === 'object' && 'id' in cmd) {
                                return serializeCommand(cmd);
                            }
                            return cmd;
                        }),
                    };
                }
            }
            return el;
        });
    }
    return result;
}
function serializeCommandResult(result) {
    if (!result)
        return { kind: 0 }; // Dismiss
    const kindMap = {
        dismiss: 0,
        goHome: 1,
        goBack: 2,
        hide: 3,
        keepOpen: 4,
        goToPage: 5,
        showToast: 6,
        confirm: 7,
    };
    const kind = typeof result.kind === 'string' ? (kindMap[result.kind] ?? 0) : 0;
    const response = { Kind: kind };
    if (result.args) {
        response.Args = result.args;
    }
    return response;
}
/**
 * Starts the JSONRPC stdio server with the given provider factory.
 * This function never returns — it runs the read loop until the process is terminated.
 */
function startJsonRpcServer(providerFactory) {
    // Initialize the ExtensionHost with notification sender
    const host = {
        log(message, state = 'info') {
            const stateMap = { trace: 0, debug: 1, info: 2, warning: 3, error: 4 };
            sendNotification('host/logMessage', { message, state: stateMap[state] ?? 2 });
        },
        showStatus(message, state = 'info', progress) {
            const stateMap = { trace: 0, debug: 1, info: 2, warning: 3, error: 4 };
            sendNotification('host/showStatus', {
                message: { Message: message, State: stateMap[state] ?? 2 },
                context: 'extension',
            });
        },
        hideStatus(messageId) {
            sendNotification('host/hideStatus', { message: { Message: messageId, State: 2 } });
        },
        copyToClipboard(text) {
            sendNotification('host/copyText', { text });
        },
    };
    ExtensionHost_1.ExtensionHost.initialize(host);
    // Create provider (may be async)
    const maybePromise = providerFactory();
    const initProvider = async (p) => {
        provider = p;
        fallbackCommandCache.clear();
        // Cache the provider's pages from topLevelCommands
        const commands = await Promise.resolve(provider.topLevelCommands());
        for (const item of commands) {
            if (item.command) {
                cacheCommand(item.command);
            }
        }
        const fallbacks = await Promise.resolve(provider.fallbackCommands?.() ?? null);
        if (fallbacks) {
            for (const item of fallbacks) {
                cacheCommand(item.command);
                fallbackCommandCache.set(item.command.id, item);
            }
        }
        if (provider.settings?.settingsPage) {
            cacheCommand(provider.settings.settingsPage);
        }
    };
    // The processing queue ensures requests are serialized AND that initProvider
    // completes before any request is handled.
    let processingQueue = maybePromise instanceof Promise
        ? maybePromise.then(initProvider)
        : initProvider(maybePromise);
    // Start reading stdin with LSP framing
    let buffer = Buffer.alloc(0);
    process.stdin.on('data', (chunk) => {
        buffer = Buffer.concat([buffer, chunk]);
        processBuffer();
    });
    process.stdin.on('end', () => {
        process.exit(0);
    });
    function processBuffer() {
        while (true) {
            // Look for header terminator
            const headerEnd = buffer.indexOf('\r\n\r\n');
            if (headerEnd === -1)
                break;
            const headerStr = buffer.subarray(0, headerEnd).toString('ascii');
            const match = headerStr.match(/Content-Length:\s*(\d+)/i);
            if (!match) {
                // Malformed header; skip to after the \r\n\r\n
                buffer = buffer.subarray(headerEnd + 4);
                continue;
            }
            const contentLength = parseInt(match[1], 10);
            const messageStart = headerEnd + 4;
            const messageEnd = messageStart + contentLength;
            if (buffer.length < messageEnd)
                break; // Not enough data yet
            const messageStr = buffer.subarray(messageStart, messageEnd).toString('utf-8');
            buffer = buffer.subarray(messageEnd);
            try {
                const message = JSON.parse(messageStr);
                if ('id' in message && typeof message.id === 'number') {
                    // Chain async handling to ensure requests are processed serially
                    processingQueue = processingQueue.then(() => handleRequest(message));
                }
                else {
                    processingQueue = processingQueue.then(() => handleNotification(message));
                }
            }
            catch {
                // Ignore parse errors
            }
        }
    }
}
//# sourceMappingURL=stdio-server.js.map