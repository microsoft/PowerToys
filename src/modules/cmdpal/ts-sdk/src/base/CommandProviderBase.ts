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
  abstract readonly id: string;
  abstract readonly displayName: string;

  icon?: IconInfo | null = null;
  frozen?: boolean = false;
  settings?: ICommandSettings | null = null;

  protected host?: IExtensionHost;

  abstract topLevelCommands(): ICommandItem[] | Promise<ICommandItem[]>;

  fallbackCommands(): IFallbackCommandItem[] | Promise<IFallbackCommandItem[]> {
    return [];
  }

  getCommand(_id: string): ICommand | null | Promise<ICommand | null> {
    return null;
  }

  initializeWithHost(host: IExtensionHost): void {
    this.host = host;
  }

  dispose(): void {
    // Override to release resources before the process exits.
  }
}
