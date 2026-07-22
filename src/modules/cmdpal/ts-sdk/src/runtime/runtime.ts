// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Dispatches Host to Extension JSON-RPC requests and notifications against a
 * command provider, and serializes the results back to the host. See
 * `03-jsonrpc-protocol.md` for the full method list.
 */

import type {
  CommandResult,
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
  numberField,
  stringField,
  type JsonRpcMessage,
  type JsonRpcNotification,
  type JsonRpcRequest,
} from './jsonrpc.js';
import { getSdkVersion, isProtocolCompatible, PROTOCOL_VERSION } from './protocol.js';
import { WireSerializer, type FormCollector, type FormSubmitHandler } from './serialize.js';

type MessageSender = (message: JsonRpcMessage) => void;

/** Default bound, in milliseconds, for awaiting provider disposal on shutdown. */
export const DEFAULT_DISPOSE_TIMEOUT_MS = 5000;

export interface ExtensionRuntimeOptions {
  /** Sends a JSON-RPC response or notification to the host. */
  send: MessageSender;
  /** Invoked after a `dispose` notification is handled. */
  onDispose?: () => void;
  /**
   * Reports a fatal, non-recoverable condition (such as a failed provider
   * initialization) so the process can exit with a non-zero code. Called with
   * the intended exit code.
   */
  reportFatal?: (code: number) => void;
}

/** Lifecycle state of provider initialization. */
type InitState = 'pending' | 'ready' | 'failed';

/** Commands and forms registered while serializing one page's content. */
interface PageScope {
  generation: number;
  commands: Map<string, ICommand>;
  forms: Map<string, FormSubmitHandler>;
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
  private readonly providerCommands = new Map<string, ICommand>();
  private readonly pageScopes = new Map<string, PageScope>();
  private readonly fallbacks = new Map<string, IFallbackCommandItem>();
  private readonly serializer: WireSerializer;
  private readonly send: MessageSender;
  private readonly onDispose?: () => void;
  private readonly reportFatal?: (code: number) => void;
  private disposed = false;
  private primed = false;

  private initState: InitState = 'pending';
  private initError: { code: number; message: string } | null = null;
  private initSettled: Promise<void> = Promise.resolve();
  private hostProtocolVersion: number | undefined = undefined;
  private hostVersion: string | undefined = undefined;

  /**
   * Where the serializer registers each command it encounters. Reassigned for
   * the duration of a page serialization so page-scoped commands land in that
   * page's scope; the server processes one message at a time, so this mutable
   * routing is race-free.
   */
  private sink: (command: ICommand) => void = (command) => {
    this.providerCommands.set(command.id, command);
  };

  constructor(options: ExtensionRuntimeOptions) {
    this.send = options.send;
    this.onDispose = options.onDispose;
    this.reportFatal = options.reportFatal;
    this.serializer = new WireSerializer((command) => {
      this.sink(command);
    });
  }

  get isDisposed(): boolean {
    return this.disposed;
  }

  /** Protocol version advertised by the host, or `undefined` for a legacy host. */
  get negotiatedHostProtocolVersion(): number | undefined {
    return this.hostProtocolVersion;
  }

  /** Host application version advertised during initialize, if any. */
  get negotiatedHostVersion(): string | undefined {
    return this.hostVersion;
  }

  /**
   * Installs the provider synchronously and marks initialization ready. Used by
   * tests and callers that already hold a constructed provider. Commands are
   * cached lazily as the host requests them, so the provider's command
   * factories are not called here.
   */
  setProvider(provider: ICommandProvider): void {
    this.provider = provider;
    this.primed = false;
    this.initState = 'ready';
    this.initError = null;
    this.initSettled = Promise.resolve();
  }

  /**
   * Begins asynchronous provider initialization. The runtime awaits `init`
   * before handling any request. If it rejects, the runtime records a failed
   * state, reports a fatal exit code, answers `initialize` with a JSON-RPC
   * error, and rejects later requests instead of registering a broken provider.
   */
  beginInitialization(init: Promise<ICommandProvider>): void {
    this.initState = 'pending';
    this.initError = null;
    this.initSettled = init.then(
      (provider) => {
        this.provider = provider;
        this.primed = false;
        this.initState = 'ready';
      },
      (error: unknown) => {
        this.initState = 'failed';
        this.initError = { code: JsonRpcErrorCode.InternalError, message: describeError(error) };
        this.reportFatal?.(1);
      },
    );
  }

  /** Handles a single incoming request, always emitting one response. */
  async handleRequest(request: JsonRpcRequest): Promise<void> {
    const { id, method } = request;
    const params = asParams(request.params);

    try {
      await this.initSettled;

      if (method === 'initialize') {
        this.handleInitialize(id, params);
        return;
      }

      if (this.initState === 'failed') {
        this.respondError(
          id,
          this.initError?.code ?? JsonRpcErrorCode.InternalError,
          this.initError?.message ?? 'Extension failed to initialize',
        );
        return;
      }

      switch (method) {
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
            stringField(params, 'formId'),
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
      await this.dispose(numberField(params, 'timeoutMs') ?? DEFAULT_DISPOSE_TIMEOUT_MS);
      return;
    }
    if (method === 'fallback/updateQuery') {
      await this.applyFallbackQuery(
        stringField(params, 'commandId') ?? '',
        stringField(params, 'query') ?? '',
      );
    }
  }

  /**
   * Releases the provider and clears cached state. Awaits the provider's
   * `dispose`, which may be asynchronous, within a bounded timeout so a slow or
   * hung disposal cannot block process exit indefinitely.
   *
   * @param timeoutMs Upper bound, in milliseconds, on awaiting provider
   * disposal. Defaults to {@link DEFAULT_DISPOSE_TIMEOUT_MS}.
   */
  async dispose(timeoutMs: number = DEFAULT_DISPOSE_TIMEOUT_MS): Promise<void> {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    const provider = this.provider;
    try {
      if (provider?.dispose) {
        await withTimeout(Promise.resolve(provider.dispose()), timeoutMs);
      }
    } catch (error) {
      process.stderr.write(`cmdpal-sdk: provider disposal failed: ${describeError(error)}\n`);
    } finally {
      this.provider = null;
      this.providerCommands.clear();
      this.pageScopes.clear();
      this.fallbacks.clear();
      this.onDispose?.();
    }
  }

  private handleInitialize(id: number | string, params: Record<string, unknown>): void {
    this.hostProtocolVersion = numberField(params, 'protocolVersion');
    this.hostVersion = stringField(params, 'hostVersion');

    if (!isProtocolCompatible(this.hostProtocolVersion)) {
      const message =
        `Incompatible protocol version: host advertises ${String(this.hostProtocolVersion)}, ` +
        `SDK implements ${String(PROTOCOL_VERSION)}.`;
      this.initState = 'failed';
      this.initError = { code: JsonRpcErrorCode.InvalidRequest, message };
      this.reportFatal?.(1);
      this.respondError(id, JsonRpcErrorCode.InvalidRequest, message);
      return;
    }

    if (this.initState === 'failed' && this.initError) {
      this.respondError(id, this.initError.code, this.initError.message);
      return;
    }

    this.respond(id, this.buildInitializeResult());
  }

  private buildInitializeResult(): Record<string, unknown> {
    const result: Record<string, unknown> = {
      protocolVersion: PROTOCOL_VERSION,
      sdkVersion: getSdkVersion(),
      capabilities: ['commands'],
    };
    const provider = this.providerMetadata();
    if (provider) {
      result.provider = provider;
    }
    return result;
  }

  private providerMetadata(): Record<string, unknown> | null {
    const provider = this.provider;
    if (!provider) {
      return null;
    }
    const metadata: Record<string, unknown> = {
      id: provider.id,
      displayName: provider.displayName,
      frozen: provider.frozen ?? true,
    };
    if (provider.icon) {
      metadata.icon = provider.icon;
    }
    return metadata;
  }

  private async getTopLevelCommands(id: number | string): Promise<void> {
    const provider = this.provider;
    if (!provider) {
      this.respondError(id, JsonRpcErrorCode.InternalError, 'Provider not initialized');
      return;
    }
    const items = await provider.topLevelCommands();
    const serialized = await this.withProviderSink(() => this.serializer.commandItems(items));
    this.respond(id, serialized);
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
      this.fallbacks.set(item.command.id, item);
    }
    const serialized = await this.withProviderSink(() => this.serializer.commandItems(items));
    this.respond(id, serialized);
  }

  private async getCommand(id: number | string, commandId: string): Promise<void> {
    const command = await this.resolveCommand(commandId);
    const serialized = command
      ? await this.withProviderSink(() => this.serializer.command(command))
      : null;
    this.respond(id, serialized);
  }

  private getSettings(id: number | string): void {
    const settings = this.provider?.settings ?? null;
    if (!settings?.settingsPage) {
      this.respond(id, null);
      return;
    }
    this.registerProviderCommand(settings.settingsPage);
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
    const scope = this.beginPageScope(pageId);
    const serialized = await this.withScopeSink(scope, () => this.serializer.listItems(items));
    this.respond(id, { items: serialized });
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
    const serialized = await this.serializePageContent(pageId);
    this.respond(id, serialized ?? []);
  }

  private async submitForm(
    id: number | string,
    pageId: string,
    formId: string | undefined,
    inputs: string,
    data: string,
  ): Promise<void> {
    if (formId) {
      let handler = this.pageScopes.get(pageId)?.forms.get(formId);
      if (!handler) {
        // The page may not have been serialized yet, or its content changed;
        // (re)serialize so the current forms are registered, then retry.
        await this.serializePageContent(pageId);
        handler = this.pageScopes.get(pageId)?.forms.get(formId);
      }
      if (handler) {
        const result = await handler(inputs, data);
        this.respond(id, this.serializer.commandResult(result));
        return;
      }
      this.respondError(id, JsonRpcErrorCode.MethodNotFound, `Form not found: ${pageId}/${formId}`);
      return;
    }

    // Fallback for a host that does not yet send a formId: preserve today's
    // behavior by submitting the first form on the page.
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

  /**
   * Serializes a content page's content into a fresh page scope, registering
   * its commands and form handlers. Returns `null` when the id does not resolve
   * to a content page.
   */
  private async serializePageContent(pageId: string): Promise<Record<string, unknown>[] | null> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asContentPage(command) : null;
    if (!page) {
      return null;
    }
    const content = await page.getContent();
    const scope = this.beginPageScope(pageId);
    const collector = createFormCollector(scope);
    return this.withScopeSink(scope, () =>
      Promise.all(content.map((item) => this.serializer.content(item, collector))),
    );
  }

  private async applyFallbackQuery(commandId: string, query: string): Promise<void> {
    let item = this.fallbacks.get(commandId);
    if (!item && !this.primed) {
      await this.primeCaches();
      item = this.fallbacks.get(commandId);
    }
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
    if (this.primed) {
      return;
    }
    this.primed = true;
    const provider = this.provider;
    if (!provider) {
      return;
    }
    const commands = await provider.topLevelCommands();
    for (const item of commands) {
      this.registerProviderCommand(item.command);
    }
    const fallbacks = (await provider.fallbackCommands?.()) ?? null;
    if (fallbacks) {
      for (const item of fallbacks) {
        this.registerProviderCommand(item.command);
        this.fallbacks.set(item.command.id, item);
      }
    }
    if (provider.settings?.settingsPage) {
      this.registerProviderCommand(provider.settings.settingsPage);
    }
  }

  private beginPageScope(pageId: string): PageScope {
    const previous = this.pageScopes.get(pageId);
    const scope: PageScope = {
      generation: (previous?.generation ?? 0) + 1,
      commands: new Map(),
      forms: new Map(),
    };
    this.pageScopes.set(pageId, scope);
    return scope;
  }

  private registerProviderCommand(command: ICommand): void {
    this.providerCommands.set(command.id, command);
  }

  private async withProviderSink<T>(produce: () => T | Promise<T>): Promise<T> {
    return this.withSink((command) => {
      this.providerCommands.set(command.id, command);
    }, produce);
  }

  private async withScopeSink<T>(scope: PageScope, produce: () => T | Promise<T>): Promise<T> {
    const seen = new Set<string>();
    return this.withSink((command) => {
      this.registerUnique(scope.commands, seen, command);
    }, produce);
  }

  private async withSink<T>(
    sink: (command: ICommand) => void,
    produce: () => T | Promise<T>,
  ): Promise<T> {
    const previous = this.sink;
    this.sink = sink;
    try {
      return await produce();
    } finally {
      this.sink = previous;
    }
  }

  private registerUnique(
    target: Map<string, ICommand>,
    seen: Set<string>,
    command: ICommand,
  ): void {
    if (seen.has(command.id)) {
      const existing = target.get(command.id);
      if (existing && existing !== command) {
        process.stderr.write(
          `cmdpal-sdk: duplicate command id "${command.id}" in one response; keeping the first.\n`,
        );
      }
      return;
    }
    seen.add(command.id);
    target.set(command.id, command);
  }

  private async resolveCommand(commandId: string): Promise<ICommand | null> {
    const cached = this.providerCommands.get(commandId);
    if (cached) {
      return cached;
    }
    const scoped = this.findScopedCommand(commandId);
    if (scoped) {
      return scoped;
    }
    const command = (await this.provider?.getCommand?.(commandId)) ?? null;
    if (command) {
      this.registerProviderCommand(command);
      return command;
    }
    if (!this.primed) {
      await this.primeCaches();
      return this.providerCommands.get(commandId) ?? this.findScopedCommand(commandId);
    }
    return null;
  }

  private findScopedCommand(commandId: string): ICommand | null {
    for (const scope of this.pageScopes.values()) {
      const command = scope.commands.get(commandId);
      if (command) {
        return command;
      }
    }
    return null;
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

function createFormCollector(scope: PageScope): FormCollector {
  let counter = 0;
  return {
    nextId(): string {
      const id = `form-${String(counter)}`;
      counter += 1;
      return id;
    },
    register(formId: string, handler: FormSubmitHandler): void {
      scope.forms.set(formId, handler);
    },
  };
}

function withTimeout(work: Promise<void>, timeoutMs: number): Promise<void> {
  if (!(timeoutMs > 0) || !Number.isFinite(timeoutMs)) {
    return work;
  }
  return new Promise<void>((resolve, reject) => {
    const timer = setTimeout(() => {
      resolve();
    }, timeoutMs);
    timer.unref?.();
    work.then(
      () => {
        clearTimeout(timer);
        resolve();
      },
      (error: unknown) => {
        clearTimeout(timer);
        reject(error instanceof Error ? error : new Error(String(error)));
      },
    );
  });
}

function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

// Re-exported so consumers importing the runtime keep access to the result type.
export type { CommandResult };
