// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Dispatches Host to Extension JSON-RPC requests and notifications against a
 * command provider, and serializes the results back to the host. See
 * `03-jsonrpc-protocol.md` for the full method list.
 */

import type {
  ICommand,
  ICommandProvider,
  IContentPage,
  IDynamicListPage,
  IFallbackCommandItem,
  IInvokableCommand,
  IListPage,
} from '../types.js';
import {
  JSONRPC_VERSION,
  JsonRpcErrorCode,
  asParams,
  stringField,
  type JsonRpcMessage,
  type JsonRpcNotification,
  type JsonRpcRequest,
} from './jsonrpc.js';
import { WireSerializer } from './serialize.js';

type MessageSender = (message: JsonRpcMessage) => void;

export interface ExtensionRuntimeOptions {
  /** Sends a JSON-RPC response or notification to the host. */
  send: MessageSender;
  /** Invoked after a `dispose` notification is handled. */
  onDispose?: () => void;
}

function hasCallable(value: object, key: string): boolean {
  return key in value && typeof (value as Record<string, unknown>)[key] === 'function';
}

function asInvokable(command: ICommand): IInvokableCommand | null {
  return hasCallable(command, 'invoke') ? (command as IInvokableCommand) : null;
}

function asListPage(command: ICommand): IListPage | null {
  return hasCallable(command, 'getItems') ? (command as IListPage) : null;
}

function asDynamicListPage(command: ICommand): IDynamicListPage | null {
  return hasCallable(command, 'setSearchText') ? (command as IDynamicListPage) : null;
}

function asContentPage(command: ICommand): IContentPage | null {
  return hasCallable(command, 'getContent') ? (command as IContentPage) : null;
}

interface FilterablePage {
  setFilter(filterId: string): void | Promise<void>;
}

function asFilterablePage(command: ICommand): FilterablePage | null {
  return hasCallable(command, 'setFilter') ? (command as unknown as FilterablePage) : null;
}

/** Handles the extension side of the Command Palette JSON-RPC protocol. */
export class ExtensionRuntime {
  private provider: ICommandProvider | null = null;
  private readonly commands = new Map<string, ICommand>();
  private readonly fallbacks = new Map<string, IFallbackCommandItem>();
  private readonly serializer: WireSerializer;
  private readonly send: MessageSender;
  private readonly onDispose?: () => void;
  private disposed = false;

  constructor(options: ExtensionRuntimeOptions) {
    this.send = options.send;
    this.onDispose = options.onDispose;
    this.serializer = new WireSerializer((command) => this.cacheCommand(command));
  }

  get isDisposed(): boolean {
    return this.disposed;
  }

  /** Installs the provider and eagerly caches its top-level commands. */
  async setProvider(provider: ICommandProvider): Promise<void> {
    this.provider = provider;
    await this.primeCaches();
  }

  /** Handles a single incoming request, always emitting one response. */
  async handleRequest(request: JsonRpcRequest): Promise<void> {
    const { id, method } = request;
    const params = asParams(request.params);

    try {
      switch (method) {
        case 'initialize':
          this.respond(id, { capabilities: ['commands'] });
          return;
        case 'provider/getTopLevelCommands':
          return await this.getTopLevelCommands(id);
        case 'provider/getFallbackCommands':
          return await this.getFallbackCommands(id);
        case 'provider/getCommand':
          return await this.getCommand(id, stringField(params, 'commandId') ?? '');
        case 'provider/getSettings':
          return this.getSettings(id);
        case 'command/invoke':
          return await this.invokeCommand(id, stringField(params, 'commandId') ?? '');
        case 'listPage/getItems':
          return await this.getItems(id, stringField(params, 'pageId') ?? '');
        case 'listPage/setSearchText':
          return await this.setSearchText(
            id,
            stringField(params, 'pageId') ?? '',
            stringField(params, 'searchText') ?? '',
          );
        case 'listPage/setFilter':
          return await this.setFilter(
            id,
            stringField(params, 'pageId') ?? '',
            stringField(params, 'filterId') ?? '',
          );
        case 'listPage/loadMore':
          return await this.loadMore(id, stringField(params, 'pageId') ?? '');
        case 'fallback/updateQuery':
          await this.applyFallbackQuery(
            stringField(params, 'commandId') ?? '',
            stringField(params, 'query') ?? '',
          );
          this.respond(id, null);
          return;
        case 'contentPage/getContent':
          return await this.getContent(id, stringField(params, 'pageId') ?? '');
        case 'form/submit':
          return await this.submitForm(
            id,
            stringField(params, 'pageId') ?? '',
            stringField(params, 'inputs') ?? '',
            stringField(params, 'data') ?? '',
          );
        default:
          this.respondError(id, JsonRpcErrorCode.MethodNotFound, `Method not found: ${method}`);
          return;
      }
    } catch (error) {
      this.respondError(id, JsonRpcErrorCode.InternalError, describeError(error));
    }
  }

  /** Handles a single incoming notification (no response is emitted). */
  async handleNotification(notification: JsonRpcNotification): Promise<void> {
    const { method } = notification;
    const params = asParams(notification.params);

    if (method === 'dispose') {
      this.dispose();
      return;
    }
    if (method === 'fallback/updateQuery') {
      await this.applyFallbackQuery(
        stringField(params, 'commandId') ?? '',
        stringField(params, 'query') ?? '',
      );
    }
  }

  /** Releases the provider and clears cached state. */
  dispose(): void {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    try {
      this.provider?.dispose?.();
    } finally {
      this.provider = null;
      this.commands.clear();
      this.fallbacks.clear();
      this.onDispose?.();
    }
  }

  private async getTopLevelCommands(id: number | string): Promise<void> {
    const provider = this.provider;
    if (!provider) {
      this.respondError(id, JsonRpcErrorCode.InternalError, 'Provider not initialized');
      return;
    }
    const items = await provider.topLevelCommands();
    this.respond(id, this.serializer.commandItems(items));
  }

  private async getFallbackCommands(id: number | string): Promise<void> {
    const provider = this.provider;
    if (!provider) {
      this.respondError(id, JsonRpcErrorCode.InternalError, 'Provider not initialized');
      return;
    }
    const items = (await provider.fallbackCommands?.()) ?? null;
    if (!items) {
      this.respond(id, null);
      return;
    }
    for (const item of items) {
      this.cacheCommand(item.command);
      this.fallbacks.set(item.command.id, item);
    }
    this.respond(id, this.serializer.commandItems(items));
  }

  private async getCommand(id: number | string, commandId: string): Promise<void> {
    const command = await this.resolveCommand(commandId);
    this.respond(id, command ? this.serializer.command(command) : null);
  }

  private getSettings(id: number | string): void {
    const settings = this.provider?.settings ?? null;
    if (!settings?.settingsPage) {
      this.respond(id, null);
      return;
    }
    this.cacheCommand(settings.settingsPage);
    this.respond(id, { id: settings.settingsPage.id });
  }

  private async invokeCommand(id: number | string, commandId: string): Promise<void> {
    const command = await this.resolveCommand(commandId);
    const invokable = command ? asInvokable(command) : null;
    if (!invokable) {
      this.respondError(
        id,
        JsonRpcErrorCode.MethodNotFound,
        `Command not found or not invokable: ${commandId}`,
      );
      return;
    }
    const result = await invokable.invoke();
    this.respond(id, this.serializer.commandResult(result));
  }

  private async getItems(id: number | string, pageId: string): Promise<void> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asListPage(command) : null;
    if (!page) {
      this.respond(id, { items: [] });
      return;
    }
    const items = await page.getItems();
    this.respond(id, { items: this.serializer.listItems(items) });
  }

  private async setSearchText(
    id: number | string,
    pageId: string,
    searchText: string,
  ): Promise<void> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asDynamicListPage(command) : null;
    if (page) {
      await page.setSearchText(searchText);
    }
    this.respond(id, null);
  }

  private async setFilter(id: number | string, pageId: string, filterId: string): Promise<void> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asFilterablePage(command) : null;
    if (page) {
      await page.setFilter(filterId);
    }
    this.respond(id, null);
  }

  private async loadMore(id: number | string, pageId: string): Promise<void> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asListPage(command) : null;
    if (page?.loadMore) {
      await page.loadMore();
    }
    this.respond(id, null);
  }

  private async getContent(id: number | string, pageId: string): Promise<void> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asContentPage(command) : null;
    if (!page) {
      this.respond(id, []);
      return;
    }
    const content = await page.getContent();
    const serialized = await Promise.all(content.map((item) => this.serializer.content(item)));
    this.respond(id, serialized);
  }

  private async submitForm(
    id: number | string,
    pageId: string,
    inputs: string,
    data: string,
  ): Promise<void> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asContentPage(command) : null;
    if (!page) {
      this.respondError(id, JsonRpcErrorCode.MethodNotFound, `Form page not found: ${pageId}`);
      return;
    }
    const content = await page.getContent();
    const form = content.find((item) => item.type === 'form');
    if (!form) {
      this.respondError(id, JsonRpcErrorCode.MethodNotFound, `Form content not found: ${pageId}`);
      return;
    }
    const result = await form.submitForm(inputs, data);
    this.respond(id, this.serializer.commandResult(result));
  }

  private async applyFallbackQuery(commandId: string, query: string): Promise<void> {
    const item = this.fallbacks.get(commandId);
    if (!item?.fallbackHandler) {
      return;
    }
    await item.fallbackHandler.updateQuery(query);
    this.sendNotification('command/propChanged', {
      commandId,
      properties: { displayTitle: item.displayTitle ?? item.title },
    });
  }

  private async primeCaches(): Promise<void> {
    const provider = this.provider;
    if (!provider) {
      return;
    }
    const commands = await provider.topLevelCommands();
    for (const item of commands) {
      this.cacheCommand(item.command);
    }
    const fallbacks = (await provider.fallbackCommands?.()) ?? null;
    if (fallbacks) {
      for (const item of fallbacks) {
        this.cacheCommand(item.command);
        this.fallbacks.set(item.command.id, item);
      }
    }
    if (provider.settings?.settingsPage) {
      this.cacheCommand(provider.settings.settingsPage);
    }
  }

  private cacheCommand(command: ICommand): void {
    this.commands.set(command.id, command);
  }

  private async resolveCommand(commandId: string): Promise<ICommand | null> {
    const cached = this.commands.get(commandId);
    if (cached) {
      return cached;
    }
    const command = (await this.provider?.getCommand?.(commandId)) ?? null;
    if (command) {
      this.cacheCommand(command);
    }
    return command;
  }

  private respond(id: number | string, result: unknown): void {
    this.send({ jsonrpc: JSONRPC_VERSION, id, result: result ?? null });
  }

  private respondError(id: number | string, code: number, message: string): void {
    this.send({ jsonrpc: JSONRPC_VERSION, id, error: { code, message } });
  }

  private sendNotification(method: string, params: unknown): void {
    this.send({ jsonrpc: JSONRPC_VERSION, method, params });
  }
}

function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
