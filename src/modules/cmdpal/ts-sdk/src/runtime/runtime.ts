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
  /**
   * Commands for the current top-level generation. Replaced wholesale each time
   * the host requests top-level commands, so ids that disappear after a refresh
   * are released rather than accumulating.
   */
  private providerScope = new Map<string, ICommand>();
  private providerRoots = new Set<string>();
  /**
   * Commands for the current fallback generation. Replaced wholesale each time
   * the host requests fallback commands.
   */
  private fallbackScope = new Map<string, ICommand>();
  private fallbackRoots = new Set<string>();
  /**
   * Commands resolved on demand (a `provider/getCommand` or
   * `provider/getCommandItem` result, the settings
   * page, or a nested command carried by a command result). Keyed by id, so
   * re-resolving the same id overwrites rather than growing the registry.
   */
  private readonly resolved = new Map<string, ICommand>();
  private readonly resolvedRoots = new Set<string>();
  private readonly pageScopes = new Map<string, PageScope>();
  private readonly fallbacks = new Map<string, IFallbackCommandItem>();
  /**
   * Direct child command ids registered while serializing a page's items or
   * content, keyed by the owning page id. Reconciled on every re-serialization
   * so a child (including a child page) that disappears is retired, and walked
   * recursively when the owner is retired so a whole subtree is released
   * together instead of leaking until process shutdown.
   */
  private readonly pageContentChildren = new Map<string, Set<string>>();
  /**
   * Nested command ids emitted inside a command result (a confirm dialog's
   * primary command, or a toast continuation), keyed by the command or page
   * that produced the result. Accumulated across invocations and walked
   * recursively when the owner is retired so a nested result command cannot
   * outlive its owner.
   */
  private readonly resultChildren = new Map<string, Set<string>>();
  private readonly propertyChildren = new Map<string, Map<string, Set<string>>>();
  private readonly childOwnerCounts = new Map<string, number>();
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
   * routing is race-free. The default target is the on-demand
   * {@link ExtensionRuntime.resolved} map, which holds nested commands emitted
   * while serializing a command result.
   */
  private sink: (command: ICommand) => void = (command) => {
    this.resolved.set(command.id, command);
  };

  constructor(options: ExtensionRuntimeOptions) {
    this.send = options.send;
    this.onDispose = options.onDispose;
    this.reportFatal = options.reportFatal;
    this.serializer = new WireSerializer((command) => {
      this.sink(command);
    }, (ownerId, propertyName, children) => {
      this.reconcilePropertyChildren(ownerId, propertyName, new Set(children));
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

  sendSdkNotification(method: string, params?: unknown): void {
    const serializedParams =
      method === 'command/propChanged' ? this.serializePropertyNotification(params) : params;
    this.sendNotification(method, serializedParams);
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
        case 'provider/getCommandItem':
          return await this.getCommandItem(id, stringField(params, 'commandId') ?? '');
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
      this.providerScope.clear();
      this.fallbackScope.clear();
      this.resolved.clear();
      this.pageScopes.clear();
      this.fallbacks.clear();
      this.pageContentChildren.clear();
      this.resultChildren.clear();
      this.onDispose?.();
    }
  }

  private handleInitialize(id: number | string, params: Record<string, unknown>): void {
    const rawProtocolVersion = params.protocolVersion;
    if (rawProtocolVersion !== undefined && !isValidProtocolVersion(rawProtocolVersion)) {
      // A present-but-malformed version (wrong type or a non-integer number) is
      // a protocol violation, distinct from an absent version, which is legacy.
      const message = `Invalid protocol version: expected an integer, received ${describeValue(
        rawProtocolVersion,
      )}.`;
      this.initState = 'failed';
      this.initError = { code: JsonRpcErrorCode.InvalidRequest, message };
      this.reportFatal?.(1);
      this.respondError(id, JsonRpcErrorCode.InvalidRequest, message);
      return;
    }

    this.hostProtocolVersion =
      rawProtocolVersion === undefined ? undefined : (rawProtocolVersion as number);
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
    const scope = new Map<string, ICommand>();
    const serialized = await this.withMapSink(scope, () => this.serializer.commandItems(items));
    // Replace the previous generation, releasing commands that are gone and
    // recursively retiring the descendant scopes they owned.
    const previous = this.providerScope;
    this.providerScope = scope;
    this.providerRoots = new Set(items.map((item) => item.command.id));
    this.retireMissing(previous.keys(), scope);
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
      const previous = this.fallbackScope;
      this.fallbackScope = new Map();
      this.fallbackRoots = new Set();
      this.fallbacks.clear();
      this.retireMissing(previous.keys(), this.fallbackScope);
      this.respond(id, null);
      return;
    }
    const scope = new Map<string, ICommand>();
    this.fallbacks.clear();
    for (const item of items) {
      this.fallbacks.set(item.command.id, item);
    }
    const serialized = await this.withMapSink(scope, () => this.serializer.commandItems(items));
    // Replace the previous fallback generation, releasing commands that are gone
    // and recursively retiring the descendant scopes they owned.
    const previous = this.fallbackScope;
    this.fallbackScope = scope;
    this.fallbackRoots = new Set(items.map((item) => item.command.id));
    this.retireMissing(previous.keys(), scope);
    this.respond(id, serialized);
  }

  private async getCommand(id: number | string, commandId: string): Promise<void> {
    const command = await this.resolveCommand(commandId);
    const serialized = command
      ? await this.withMapSink(this.resolved, () => this.serializer.command(command))
      : null;
    this.respond(id, serialized);
  }

  private async getCommandItem(id: number | string, commandId: string): Promise<void> {
    const item = (await this.provider?.getCommandItem?.(commandId)) ?? null;
    if (item) {
      this.resolvedRoots.add(item.command.id);
    }
    const serialized = item
      ? await this.withMapSink(this.resolved, () => this.serializer.commandItem(item))
      : null;
    this.respond(id, serialized);
  }

  private getSettings(id: number | string): void {
    const settings = this.provider?.settings ?? null;
    const page = settings?.settingsPage;
    if (!page) {
      this.respond(id, null);
      return;
    }
    this.resolvedRoots.add(page.id);
    // Serialize the full settings page (not just its id) so the host can render
    // it without a second fetch. `serializer.command` registers the page via the
    // active sink, so a later content/form request resolves it.
    this.respond(id, this.serializer.command(page));
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
    this.respond(id, await this.serializeOwnedResult(commandId, result));
  }

  private async getItems(id: number | string, pageId: string): Promise<void> {
    const command = await this.resolveCommand(pageId);
    const page = command ? asListPage(command) : null;
    if (!page) {
      this.respond(id, { items: [], hasMoreItems: false });
      return;
    }
    const items = await page.getItems();
    const scope = this.beginPageScope(pageId);
    const serialized = await this.withScopeSink(scope, () => this.serializer.listItems(items));
    this.reconcilePageContent(pageId, scope);
    this.respond(id, { items: serialized, hasMoreItems: page.hasMoreItems ?? false });
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
    if (!page) {
      this.respond(id, { items: [], hasMoreItems: false });
      return;
    }
    if (page.loadMore) {
      await page.loadMore();
    }
    // Re-serialize the page's current items so the host receives the appended
    // page as a continuation, along with whether further pages remain.
    const items = await page.getItems();
    const scope = this.beginPageScope(pageId);
    const serialized = await this.withScopeSink(scope, () => this.serializer.listItems(items));
    this.reconcilePageContent(pageId, scope);
    this.respond(id, { items: serialized, hasMoreItems: page.hasMoreItems ?? false });
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
        this.respond(id, await this.serializeOwnedResult(pageId, result));
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
    this.respond(id, await this.serializeOwnedResult(pageId, result));
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
    const serialized = await this.withScopeSink(scope, () =>
      Promise.all(content.map((item) => this.serializer.content(item, collector))),
    );
    this.reconcilePageContent(pageId, scope);
    return serialized;
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
      this.providerScope.set(item.command.id, item.command);
      this.providerRoots.add(item.command.id);
    }
    const fallbacks = (await provider.fallbackCommands?.()) ?? null;
    if (fallbacks) {
      for (const item of fallbacks) {
        this.fallbackScope.set(item.command.id, item.command);
        this.fallbackRoots.add(item.command.id);
        this.fallbacks.set(item.command.id, item);
      }
    }
    if (provider.settings?.settingsPage) {
      const page = provider.settings.settingsPage;
      this.resolved.set(page.id, page);
      this.resolvedRoots.add(page.id);
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

  private async withMapSink<T>(
    target: Map<string, ICommand>,
    produce: () => T | Promise<T>,
  ): Promise<T> {
    return this.withSink((command) => {
      target.set(command.id, command);
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
    const direct = this.lookupCommand(commandId);
    if (direct) {
      return direct;
    }
    const command = (await this.provider?.getCommand?.(commandId)) ?? null;
    if (command) {
      this.resolved.set(command.id, command);
      this.resolvedRoots.add(command.id);
      return command;
    }
    if (!this.primed) {
      await this.primeCaches();
      return this.lookupCommand(commandId);
    }
    return null;
  }

  private lookupCommand(commandId: string): ICommand | null {
    return (
      this.providerScope.get(commandId) ??
      this.fallbackScope.get(commandId) ??
      this.resolved.get(commandId) ??
      this.findScopedCommand(commandId)
    );
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

  /**
   * Recursively retires a command id: removes it from the on-demand and page
   * registries and walks its descendant scopes (child page content and nested
   * result commands) so the whole subtree is released together. Safe for ids
   * that own no scope.
   */
  private retire(commandId: string): void {
    this.providerRoots.delete(commandId);
    this.fallbackRoots.delete(commandId);
    this.resolvedRoots.delete(commandId);
    this.providerScope.delete(commandId);
    this.fallbackScope.delete(commandId);
    this.resolved.delete(commandId);
    this.pageScopes.delete(commandId);
    const contentChildren = this.pageContentChildren.get(commandId);
    this.pageContentChildren.delete(commandId);
    const resultChildren = this.resultChildren.get(commandId);
    this.resultChildren.delete(commandId);
    const propertyChildren = this.propertyChildren.get(commandId);
    this.propertyChildren.delete(commandId);
    const childrenToRetire = new Set<string>();
    if (contentChildren) {
      for (const childId of contentChildren) {
        this.removeChildOwner(childId);
        childrenToRetire.add(childId);
      }
    }
    if (resultChildren) {
      for (const childId of resultChildren) {
        if (childId !== commandId) {
          this.removeChildOwner(childId);
          childrenToRetire.add(childId);
        }
      }
    }
    if (propertyChildren) {
      for (const children of propertyChildren.values()) {
        for (const childId of children) {
          if (childId !== commandId) {
            this.removeChildOwner(childId);
            childrenToRetire.add(childId);
          }
        }
      }
    }
    for (const childId of childrenToRetire) {
      this.retireIfUnowned(childId);
    }
  }

  private serializePropertyNotification(params: unknown): unknown {
    const notification = asParams(params);
    const commandId = stringField(notification, 'commandId');
    const propertiesValue = notification.properties;
    if (
      !commandId ||
      typeof propertiesValue !== 'object' ||
      propertiesValue === null ||
      Array.isArray(propertiesValue)
    ) {
      return params;
    }

    const properties = propertiesValue as Record<string, unknown>;
    const serialized: Record<string, unknown> = {};
    for (const [propertyName, value] of Object.entries(properties)) {
      const serializer = new WireSerializer((command) => {
        this.resolved.set(command.id, command);
      }, (ownerId, nestedPropertyName, nestedChildren) => {
        this.reconcilePropertyChildren(ownerId, nestedPropertyName, new Set(nestedChildren));
      });

      serialized[propertyName] = serializer.observableProperty(
        commandId,
        propertyName,
        value,
      );
    }

    return { ...notification, properties: serialized };
  }

  private reconcilePropertyChildren(
    commandId: string,
    propertyName: string,
    current: Set<string>,
  ): void {
    let properties = this.propertyChildren.get(commandId);
    if (!properties) {
      properties = new Map();
      this.propertyChildren.set(commandId, properties);
    }

    const previous = properties.get(propertyName);
    this.replaceChildOwners(previous, current);
    if (current.size > 0) {
      properties.set(propertyName, current);
    } else {
      properties.delete(propertyName);
      if (properties.size === 0) {
        this.propertyChildren.delete(commandId);
      }
    }

    if (previous) {
      for (const childId of previous) {
        if (!current.has(childId)) {
          this.retireIfUnowned(childId);
        }
      }
    }
  }

  /** Retires every id in `ids` that is absent from the surviving `keep` map. */
  private retireMissing(ids: Iterable<string>, keep: ReadonlyMap<string, unknown>): void {
    for (const id of ids) {
      if (!keep.has(id)) {
        this.retireIfUnowned(id);
      }
    }
  }

  private retireIfUnowned(commandId: string): void {
    if (!this.isCommandOwned(commandId)) {
      this.retire(commandId);
    }
  }

  private isCommandOwned(commandId: string): boolean {
    return (
      this.providerRoots.has(commandId) ||
      this.fallbackRoots.has(commandId) ||
      this.resolvedRoots.has(commandId) ||
      (this.childOwnerCounts.get(commandId) ?? 0) > 0
    );
  }

  /**
   * Records the direct children a page just serialized and retires the children
   * from the prior generation that are no longer present, recursively releasing
   * the scopes those children owned.
   */
  private reconcilePageContent(pageId: string, scope: PageScope): void {
    const current = new Set(scope.commands.keys());
    const previous = this.pageContentChildren.get(pageId);
    this.replaceChildOwners(previous, current);
    this.pageContentChildren.set(pageId, current);
    if (previous) {
      for (const childId of previous) {
        if (!current.has(childId)) {
          this.retireIfUnowned(childId);
        }
      }
    }
  }

  /**
   * Serializes a command result, attributing any nested commands it emits (a
   * confirm dialog's primary command, or a toast continuation) to `ownerId` so
   * they are retired together with their owner rather than leaking in the
   * on-demand registry until process shutdown.
   */
  private async serializeOwnedResult(
    ownerId: string,
    result: CommandResult,
  ): Promise<ReturnType<WireSerializer['commandResult']>> {
    const children = new Set<string>();
    const serialized = await this.withSink(
      (command) => {
        this.resolved.set(command.id, command);
        if (command.id !== ownerId) {
          children.add(command.id);
        }
      },
      () => this.serializer.commandResult(result),
    );
    if (children.size > 0) {
      const existing = this.resultChildren.get(ownerId);
      if (existing) {
        for (const childId of children) {
          if (!existing.has(childId)) {
            existing.add(childId);
            this.addChildOwner(childId);
          }
        }
      } else {
        this.resultChildren.set(ownerId, children);
        for (const childId of children) {
          this.addChildOwner(childId);
        }
      }
    }
    return serialized;
  }

  private replaceChildOwners(
    previous: ReadonlySet<string> | undefined,
    current: ReadonlySet<string>,
  ): void {
    if (previous) {
      for (const childId of previous) {
        if (!current.has(childId)) {
          this.removeChildOwner(childId);
        }
      }
    }
    for (const childId of current) {
      if (!previous?.has(childId)) {
        this.addChildOwner(childId);
      }
    }
  }

  private addChildOwner(commandId: string): void {
    this.childOwnerCounts.set(commandId, (this.childOwnerCounts.get(commandId) ?? 0) + 1);
  }

  private removeChildOwner(commandId: string): void {
    const count = this.childOwnerCounts.get(commandId) ?? 0;
    if (count <= 1) {
      this.childOwnerCounts.delete(commandId);
    } else {
      this.childOwnerCounts.set(commandId, count - 1);
    }
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
  if (!(timeoutMs > 0)) {
    // A non-positive bound means dispose immediately: do not await the work.
    // Swallow any later rejection so it does not surface as an unhandled
    // rejection after the caller has already moved on.
    void work.catch(() => undefined);
    return Promise.resolve();
  }
  if (!Number.isFinite(timeoutMs)) {
    // An explicit, non-finite bound (Infinity) means wait without a deadline.
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

function isValidProtocolVersion(value: unknown): boolean {
  return typeof value === 'number' && Number.isInteger(value);
}

function describeValue(value: unknown): string {
  if (typeof value === 'string') {
    return `"${value}"`;
  }
  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }
  return typeof value;
}

// Re-exported so consumers importing the runtime keep access to the result type.
export type { CommandResult };
