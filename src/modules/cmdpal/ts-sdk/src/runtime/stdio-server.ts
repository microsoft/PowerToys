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

import type {
  ICommandProvider,
  ICommandItem,
  IListItem,
  ICommand,
  IFallbackCommandItem,
  CommandResult,
  Content,
  MarkdownContent,
  FormContent,
  TreeContent,
  PlainTextContent,
  ImageContent,
  ContextItem,
} from '../types';
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
let fallbackCommandCache: Map<string, IFallbackCommandItem> = new Map();

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

function patchPageNotifications(pageId: string, page: unknown): void {
  if (page && typeof page === 'object' && 'notifyItemsChanged' in page) {
    (page as { notifyItemsChanged?: () => void }).notifyItemsChanged = () => {
      sendNotification('listPage/itemsChanged', { pageId });
    };
  }
}

function cacheCommand(command: ICommand, cacheId?: string): void {
  commandCache.set(command.id, command);
  if (cacheId && cacheId !== command.id) {
    commandCache.set(cacheId, command);
  }
  patchPageNotifications(command.id, command);
}

async function getCachedCommand(commandId: string): Promise<ICommand | null> {
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

function serializeContextItems(items?: ContextItem[] | null): unknown[] | undefined {
  if (!items || items.length === 0) {
    return undefined;
  }

  return items.map((ctx) => {
    const ctxResult: Record<string, unknown> = {
      command: serializeCommand(ctx.command),
      title: ctx.title,
    };

    if (ctx.subtitle) ctxResult.subtitle = ctx.subtitle;
    if (ctx.icon) ctxResult.icon = ctx.icon;
    if (ctx.isCritical) ctxResult.isCritical = ctx.isCritical;
    if (ctx.requestedShortcut) ctxResult.requestedShortcut = ctx.requestedShortcut;

    return ctxResult;
  });
}

async function serializeContent(content: Content): Promise<unknown> {
  switch (content.type) {
    case 'markdown':
      return { type: 'markdown', body: (content as MarkdownContent).body };
    case 'plainText': {
      const pt = content as PlainTextContent;
      return { type: 'plainText', text: pt.text, fontFamily: pt.fontFamily, wrapWords: pt.wrapWords };
    }
    case 'image': {
      const img = content as ImageContent;
      return { type: 'image', image: img.image, maxWidth: img.maxWidth, maxHeight: img.maxHeight };
    }
    case 'form': {
      const form = content as FormContent;
      return { type: 'form', templateJson: form.templateJson, dataJson: form.dataJson, stateJson: form.stateJson };
    }
    case 'tree': {
      const tree = content as TreeContent;
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
        const commandId = (p as { commandId?: string })?.commandId ?? '';
        const command = await Promise.resolve(provider.getCommand?.(commandId) ?? null);
        if (command) {
          cacheCommand(command, commandId);
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
        if (settings?.settingsPage) {
          cacheCommand(settings.settingsPage);
        }
        sendResponse(id, settings ? { id: (settings as { settingsPage?: { id?: string } }).settingsPage?.id ?? '' } : null);
        break;
      }

      case 'command/invoke': {
        const cmdId = (p as { commandId?: string })?.commandId ?? '';
        const cmd = await getCachedCommand(cmdId);
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
        const page = await getCachedCommand(pageId);
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
        const page = await getCachedCommand(pageId);
        if (page && 'setSearchText' in page && typeof page.setSearchText === 'function') {
          await Promise.resolve((page as { setSearchText: (text: string) => void | Promise<void> }).setSearchText(searchText));
        }
        sendResponse(id, null);
        break;
      }

      case 'listPage/setFilter': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const filterId = (p as { filterId?: string })?.filterId ?? '';
        const page = await getCachedCommand(pageId);
        if (page && 'setFilter' in page && typeof (page as { setFilter?: (value: string) => void | Promise<void> }).setFilter === 'function') {
          await Promise.resolve((page as { setFilter: (value: string) => void | Promise<void> }).setFilter(filterId));
        }
        sendResponse(id, null);
        break;
      }

      case 'fallback/updateQuery': {
        const commandId = (p as { commandId?: string })?.commandId ?? '';
        const query = (p as { query?: string })?.query ?? '';
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
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const page = await getCachedCommand(pageId);
        if (page && 'getContent' in page && typeof page.getContent === 'function') {
          const rawContent = await Promise.resolve((page as { getContent: () => Content[] | Promise<Content[]> }).getContent());
          const serialized = await Promise.all(rawContent.map((item) => serializeContent(item)));
          sendResponse(id, serialized);
        } else {
          sendResponse(id, []);
        }
        break;
      }

      case 'form/submit': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const inputs = (p as { inputs?: string })?.inputs ?? '';
        const data = (p as { data?: string })?.data ?? '';
        const page = await getCachedCommand(pageId);
        if (page && 'getContent' in page && typeof page.getContent === 'function') {
          const content = await Promise.resolve((page as { getContent: () => Content[] | Promise<Content[]> }).getContent());
          const form = content.find(
            (item): item is FormContent => item.type === 'form' && typeof (item as FormContent).submitForm === 'function'
          );
          if (!form) {
            sendError(id, -32601, `Form content not found for page: ${pageId}`);
            break;
          }
          const result = await Promise.resolve(form.submitForm(inputs, data));
          sendResponse(id, serializeCommandResult(result));
        } else if (page && 'submitForm' in page && typeof (page as { submitForm?: (formInputs: string, formData: string) => CommandResult | Promise<CommandResult> }).submitForm === 'function') {
          const result = await Promise.resolve(
            (page as { submitForm: (formInputs: string, formData: string) => CommandResult | Promise<CommandResult> }).submitForm(inputs, data)
          );
          sendResponse(id, serializeCommandResult(result));
        } else {
          sendError(id, -32601, `Form page not found: ${pageId}`);
        }
        break;
      }

      case 'listPage/loadMore': {
        const pageId = (p as { pageId?: string })?.pageId ?? '';
        const page = await getCachedCommand(pageId);
        if (page && 'loadMore' in page && typeof page.loadMore === 'function') {
          await Promise.resolve((page as { loadMore: () => void | Promise<void> }).loadMore());
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

async function handleNotification(notification: JsonRpcNotification): Promise<void> {
  const { method, params } = notification;
  const p = params as Record<string, unknown> | undefined;

  if (method === 'fallback/updateQuery') {
    const commandId = (p as { commandId?: string })?.commandId ?? '';
    const query = (p as { query?: string })?.query ?? '';
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

function serializeCommand(command: ICommand): unknown {
  cacheCommand(command);

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
    if ('isLoading' in command) result.isLoading = (command as Record<string, unknown>).isLoading;
    if ('accentColor' in command) result.accentColor = (command as Record<string, unknown>).accentColor;
    if ('placeholderText' in command) result.placeholderText = (command as Record<string, unknown>).placeholderText;
    if ('showDetails' in command) result.showDetails = (command as Record<string, unknown>).showDetails;
    if ('title' in command) result.title = (command as Record<string, unknown>).title;
    if ('gridProperties' in command) {
      const gp = (command as Record<string, unknown>).gridProperties as Record<string, unknown> | null | undefined;
      if (gp) {
        // C# expects 'layout' property, but TS GridProperties uses 'type'
        result.gridProperties = { ...gp, layout: gp.layout ?? gp.type };
      }
    }
    if ('filters' in command) result.filters = (command as Record<string, unknown>).filters;
    if ('hasMoreItems' in command) result.hasMoreItems = (command as Record<string, unknown>).hasMoreItems;
    if ('emptyContent' in command) result.emptyContent = (command as Record<string, unknown>).emptyContent;
  } else if ('getContent' in command && typeof (command as Record<string, unknown>).getContent === 'function') {
    result._type = 'contentPage';
    if ('title' in command) result.title = (command as Record<string, unknown>).title;
    if ('isLoading' in command) result.isLoading = (command as Record<string, unknown>).isLoading;
    if ('accentColor' in command) result.accentColor = (command as Record<string, unknown>).accentColor;
    if ('details' in command) result.details = (command as Record<string, unknown>).details;
    if ('commands' in command) result.commands = serializeContextItems((command as { commands?: ContextItem[] | null }).commands);
  }

  return result;
}

function serializeCommandItems(items: ICommandItem[]): unknown[] {
  return items.map((item) => {
    const result: Record<string, unknown> = {
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

function serializeListItems(items: IListItem[]): unknown[] {
  return items.map((item) => {
    // Check if this is a separator
    if ('_isSeparator' in item && (item as { _isSeparator?: boolean })._isSeparator) {
      return { _isSeparator: true, title: item.title, section: item.section };
    }

    const result: Record<string, unknown> = {
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

function serializeDetails(details: unknown): unknown {
  if (!details || typeof details !== 'object') return details;
  const d = details as Record<string, unknown>;
  const result: Record<string, unknown> = { ...d };

  // Serialize metadata commands (DetailsCommands contain ICommand[] that need proper serialization)
  if (Array.isArray(d.metadata)) {
    result.metadata = (d.metadata as Array<Record<string, unknown>>).map((element) => {
      const el: Record<string, unknown> = { ...element };
      if (el.data && typeof el.data === 'object') {
        const data = el.data as Record<string, unknown>;
        // Serialize commands within DetailsCommands
        if (Array.isArray(data.commands)) {
          el.data = {
            ...data,
            commands: (data.commands as Array<unknown>).map((cmd) => {
              if (cmd && typeof cmd === 'object' && 'id' in (cmd as Record<string, unknown>)) {
                return serializeCommand(cmd as ICommand);
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

function serializeCommandResult(result: CommandResult | undefined): unknown {
  if (!result) return { kind: 0 }; // Dismiss

  const kindMap: Record<string, number> = {
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
    copyToClipboard(text: string): void {
      sendNotification('host/copyText', { text });
    },
  };
  ExtensionHost.initialize(host);

  // Create provider (may be async)
  const maybePromise = providerFactory();
  const initProvider = async (p: ICommandProvider) => {
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
          processingQueue = processingQueue.then(() => handleNotification(message as JsonRpcNotification));
        }
      } catch {
        // Ignore parse errors
      }
    }
  }
}
