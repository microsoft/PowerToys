// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  ICommand,
  ICommandItem,
  ICommandProvider,
  ICommandSettings,
  IExtensionHost,
  IFallbackCommandItem,
  IconInfo,
} from '../types.js';

/**
 * Base class for Command Palette extension providers. Extend it to expose
 * commands and pages to the palette.
 *
 * @example
 * ```typescript
 * class MyProvider extends CommandProviderBase {
 *   readonly id = 'my-extension';
 *   readonly displayName = 'My Extension';
 *
 *   topLevelCommands(): ICommandItem[] {
 *     return [new CommandItemBase({ command: new MyCommand(), title: 'Do it' })];
 *   }
 * }
 * ```
 */
export abstract class CommandProviderBase implements ICommandProvider {
  /** Unique identifier for the extension. */
  abstract readonly id: string;
  /** Human-readable name shown for the extension. */
  abstract readonly displayName: string;

  /** Icon shown for the extension. Defaults to none. */
  icon?: IconInfo | null = null;
  /**
   * When `true`, the palette caches commands and does not re-query them.
   * Defaults to `true`, matching the Command Palette toolkit default.
   */
  frozen?: boolean = true;
  /** Settings surface for the extension, or `null` when it has none. */
  settings?: ICommandSettings | null = null;

  /** Host bridge captured in {@link CommandProviderBase.initializeWithHost}. */
  protected host?: IExtensionHost;

  /**
   * Produces the commands shown at the top level of the palette.
   *
   * @returns The top-level command items, synchronously or as a promise.
   */
  abstract topLevelCommands(): ICommandItem[] | Promise<ICommandItem[]>;

  /**
   * Produces fallback commands that receive the search query as the user types.
   * Returns an empty list by default; override to provide fallbacks.
   *
   * @returns The fallback command items, synchronously or as a promise.
   */
  fallbackCommands(): IFallbackCommandItem[] | Promise<IFallbackCommandItem[]> {
    return [];
  }

  /**
   * Resolves a command by id. Returns `null` by default; override when the
   * provider exposes commands that are not returned up front.
   *
   * @param _id Identifier of the command to resolve.
   * @returns The command, or `null` when it is not found.
   */
  getCommand(_id: string): ICommand | null | Promise<ICommand | null> {
    return null;
  }

  /**
   * Captures the host bridge for later use. Override to run additional setup,
   * calling `super.initializeWithHost(host)` to keep {@link CommandProviderBase.host} set.
   *
   * @param host Bridge used to talk back to the palette.
   */
  initializeWithHost(host: IExtensionHost): void {
    this.host = host;
  }

  /** Releases resources before the extension process exits. Override as needed. */
  dispose(): void {
    // Override to release resources before the process exits.
  }
}
