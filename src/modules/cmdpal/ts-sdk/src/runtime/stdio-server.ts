// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * JSONRPC over stdio server for Command Palette extensions.
 * This module handles LSP-style Content-Length framed JSON-RPC 2.0
 * communication directly with the C# host over stdin/stdout.
 *
 * Usage:
 * ```typescript
 * import { startJsonRpcServer } from '@microsoft/cmdpal-sdk/runtime/stdio-server';
 * import { MyProvider } from './provider';
 *
 * startJsonRpcServer(() => new MyProvider());
 * ```
 */

import type { ICommandProvider, ICommandItem, IListItem, ICommand, CommandResult, Content, MarkdownContent } from '../types';
import { ExtensionHost } from './ExtensionHost';

interface JsonRpcRequest {
  jsonrpc: string;
  id: number;
  method: string;
  params?: unknown;
}

interface JsonRpcResponse {
  jsonrpc: string;
  id: number;
  result?: unknown;
  error?: { code: number; message: string; data?: unknown };
}

interface JsonRpcNotification {
  jsonrpc: string;
  method: string;
  params?: unknown;
}

type JsonRpcMessage = JsonRpcRequest | JsonRpcNotification;

let provider: ICommandProvider | null = null;
let commandCache: Map<string, ICommand> = new Map();

function sendMessage(message: JsonRpcResponse | JsonRpcNotification): void {
  const json = JSON.stringify(message);
  const contentBytes = Buffer.from(json, 'utf-8');
  const header = `Content-Length: ${contentBytes.length}\r\n\r\n`;
  // Write header + body as single buffer to avoid interleaving
  const headerBytes = Buffer.from(header, 'ascii');
  const packet = Buffer.concat([headerBytes, contentBytes]);
  process.stdout.write(packet);
}

function sendResponse(id: number, result: unknown): void {
  sendMessage({ jsonrpc: '2.0', id, result: result ?? null });
}

function sendError(id: number, code: number, message: string): void {
  sendMessage({ jsonrpc: '2.0', id, error: { code, message } });
}

export function sendNotification(method: string, params: unknown): void {
  sendMessage({ jsonrpc: '2.0', method, params });
}

async function handleRequest(request: JsonRpcRequest): Promise<void> {
  const { id, method, params } = request;
  const p = params as Record<string, unknown> | undefined;

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
        sendResponse(id, serializeCommandItems(commands));
        break;
      }

      case 'provider/getFallbackCommands': {
        if (!provider) {
          sendError(id, -32603, 'Provider not initialized');
          return;
        }
        const fallbacks = await Promise.resolve(provider.fallbackCommands?.() ?? null);
        sendResponse(id, fallbacks ? serializeCommandItems(fallbacks) : null);
        break;
      }

      case 'provider/getCommand': {
        if (!provider) {
          sendError(id, -32603, 'Provider not initialized');
          return;
        }
        const commandId = (p as { commandId?: string })?.commandId ?? '';
        const command = await Promise.resolve(provider.getCommand?.(commandId) ?? null);
        if (command) {
          commandCache.set(commandId, command);
          sendResponse(id, serializeCommand(command));
        } else {
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
        sendResponse(id, settings ? { id: (settings as { settingsPage?: { id?: string } }).settingsPage?.id ?? '' } : null);
        break;
      }

      case 'command/invoke': {
        const cmdId = (p as { commandId?: string })?.commandId ?? '';
        const cmd = commandCache.get(cmdId) ?? await Promise.resolve(provider?.getCommand?.(cmdId) ?? null);
        if (cmd && 'invoke' in cmd && typeof cmd.invoke === 'function') {
          const result = await Promise.resolve(
            (cmd as { invoke: (sender: unknown) => CommandResult | Promise<CommandResult> }).invoke(null)
          );
          sendResponse(id, serializeCommandResult(result));
        } else {
          sendError(id, -32601, `Command not found or not invokable: ${cmdId}`);
        }
        break;
      }

      case 'listPage/getItems': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const page = commandCache.get(pageId) ?? await Promise.resolve(provider?.getCommand?.(pageId) ?? null);
        if (page && 'getItems' in page && typeof page.getItems === 'function') {
          const items = await Promise.resolve(
            (page as { getItems: () => IListItem[] | Promise<IListItem[]> }).getItems()
          );
          sendResponse(id, { items: serializeListItems(items) });
        } else {
          sendResponse(id, { items: [] });
        }
        break;
      }

      case 'listPage/setSearchText': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const searchText = (p as { searchText?: string })?.searchText ?? '';
        const page = commandCache.get(pageId) ?? provider?.getCommand?.(pageId);
        if (page && 'setSearchText' in page && typeof page.setSearchText === 'function') {
          (page as { setSearchText: (text: string) => void }).setSearchText(searchText);
        }
        sendResponse(id, null);
        break;
      }

      case 'contentPage/getContent': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const page = commandCache.get(pageId) ?? provider?.getCommand?.(pageId);
        if (page && 'getContent' in page && typeof page.getContent === 'function') {
          const content = (page as { getContent: () => Content[] }).getContent();
          sendResponse(id, { content });
        } else {
          sendResponse(id, { content: [] });
        }
        break;
      }

      case 'form/submit': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const inputs = (p as { inputs?: string })?.inputs ?? '';
        const data = (p as { data?: string })?.data ?? '';
        const page = commandCache.get(pageId) ?? provider?.getCommand?.(pageId);
        if (page && 'submitForm' in page && typeof page.submitForm === 'function') {
          const result = (page as { submitForm: (inputs: string, data: string) => CommandResult }).submitForm(inputs, data);
          sendResponse(id, serializeCommandResult(result));
        } else {
          sendError(id, -32601, `Form page not found: ${pageId}`);
        }
        break;
      }

      case 'listPage/loadMore': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const page = commandCache.get(pageId) ?? provider?.getCommand?.(pageId);
        if (page && 'loadMore' in page && typeof page.loadMore === 'function') {
          (page as { loadMore: () => void }).loadMore();
        }
        sendResponse(id, null);
        break;
      }

      default:
        sendError(id, -32601, `Method not found: ${method}`);
    }
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    sendError(id, -32603, message);
  }
}

function handleNotification(notification: JsonRpcNotification): void {
  const { method } = notification;

  if (method === 'dispose') {
    // Clean up and exit
    provider = null;
    commandCache.clear();
    process.exit(0);
  }
}

function serializeCommand(command: ICommand): unknown {
  const result: Record<string, unknown> = {
    id: command.id,
    name: command.name,
    displayName: command.name,
  };

  if (command.icon) {
    result.icon = command.icon;
  }

  // Detect page types
  if ('getItems' in command && typeof (command as Record<string, unknown>).getItems === 'function') {
    if ('setSearchText' in command || 'searchText' in command) {
      result._type = 'dynamicListPage';
    } else {
      result._type = 'listPage';
    }

    // Copy page properties
    if ('placeholderText' in command) result.placeholderText = (command as Record<string, unknown>).placeholderText;
    if ('showDetails' in command) result.showDetails = (command as Record<string, unknown>).showDetails;
    if ('title' in command) result.title = (command as Record<string, unknown>).title;
    if ('gridProperties' in command) result.gridProperties = (command as Record<string, unknown>).gridProperties;
    if ('filters' in command) result.filters = (command as Record<string, unknown>).filters;
  } else if ('getContent' in command && typeof (command as Record<string, unknown>).getContent === 'function') {
    result._type = 'contentPage';
    if ('title' in command) result.title = (command as Record<string, unknown>).title;
  }

  return result;
}

function serializeCommandItems(items: ICommandItem[]): unknown[] {
  return items.map((item) => {
    const result: Record<string, unknown> = {
      title: item.title,
      displayName: item.title,
      subtitle: item.subtitle,
      command: item.command ? serializeCommand(item.command) : undefined,
    };
    if (item.icon) {
      result.icon = item.icon;
    }
    return result;
  });
}

function serializeListItems(items: IListItem[]): unknown[] {
  return items.map((item) => {
    const result: Record<string, unknown> = {
      title: item.title,
      displayName: item.title,
      subtitle: item.subtitle,
      section: item.section,
      tags: item.tags,
      details: item.details,
      textToSuggest: item.textToSuggest,
      command: item.command ? serializeCommand(item.command) : undefined,
    };
    if (item.icon) {
      result.icon = item.icon;
    }
    return result;
  });
}

function serializeCommandResult(result: CommandResult | undefined): unknown {
  if (!result) return { kind: 0 }; // Dismiss

  const kindMap: Record<string, number> = {
    dismiss: 0,
    goHome: 1,
    hide: 2,
    goToPage: 3,
    showToast: 4,
    keepOpen: 5,
    goBack: 6,
    confirm: 7,
  };

  const kind = typeof result.kind === 'string' ? (kindMap[result.kind] ?? 0) : 0;
  const response: Record<string, unknown> = { Kind: kind };

  if (result.args) {
    response.Args = result.args;
  }

  return response;
}

/**
 * Starts the JSONRPC stdio server with the given provider factory.
 * This function never returns — it runs the read loop until the process is terminated.
 */
export function startJsonRpcServer(
  providerFactory: () => ICommandProvider | Promise<ICommandProvider>
): void {
  // Initialize the ExtensionHost with notification sender
  const host = {
    log(message: string, state: string = 'info'): void {
      const stateMap: Record<string, number> = { trace: 0, debug: 1, info: 2, warning: 3, error: 4 };
      sendNotification('host/logMessage', { message, state: stateMap[state] ?? 2 });
    },
    showStatus(message: string, state: string = 'info', progress?: unknown): void {
      const stateMap: Record<string, number> = { trace: 0, debug: 1, info: 2, warning: 3, error: 4 };
      sendNotification('host/showStatus', {
        message: { Message: message, State: stateMap[state] ?? 2 },
        context: 'extension',
      });
    },
    hideStatus(messageId: string): void {
      sendNotification('host/hideStatus', { message: { Message: messageId, State: 2 } });
    },
  };
  ExtensionHost.initialize(host);

  // Create provider (may be async)
  const maybePromise = providerFactory();
  const initProvider = async (p: ICommandProvider) => {
    provider = p;
    // Cache the provider's pages from topLevelCommands
    const commands = await Promise.resolve(provider.topLevelCommands());
    for (const item of commands) {
      if (item.command) {
        commandCache.set(item.command.id, item.command);
      }
    }
  };

  // The processing queue ensures requests are serialized AND that initProvider
  // completes before any request is handled.
  let processingQueue: Promise<void> = maybePromise instanceof Promise
    ? maybePromise.then(initProvider)
    : initProvider(maybePromise);

  // Start reading stdin with LSP framing
  let buffer = Buffer.alloc(0);

  process.stdin.on('data', (chunk: Buffer) => {
    buffer = Buffer.concat([buffer, chunk]);
    processBuffer();
  });

  process.stdin.on('end', () => {
    process.exit(0);
  });

  function processBuffer(): void {
    while (true) {
      // Look for header terminator
      const headerEnd = buffer.indexOf('\r\n\r\n');
      if (headerEnd === -1) break;

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

      if (buffer.length < messageEnd) break; // Not enough data yet

      const messageStr = buffer.subarray(messageStart, messageEnd).toString('utf-8');
      buffer = buffer.subarray(messageEnd);

      try {
        const message = JSON.parse(messageStr) as JsonRpcMessage;
        if ('id' in message && typeof message.id === 'number') {
          // Chain async handling to ensure requests are processed serially
          processingQueue = processingQueue.then(() => handleRequest(message as JsonRpcRequest));
        } else {
          handleNotification(message as JsonRpcNotification);
        }
      } catch {
        // Ignore parse errors
      }
    }
  }
}
