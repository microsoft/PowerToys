// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  ICommandProvider,
  ICommandItem,
  IFallbackCommandItem,
  ICommand,
  ICommandSettings,
  IconInfo,
  IExtensionHost,
} from '../types';

/**
 * Base class for Command Palette extension providers.
 * Extend this class to create an extension that provides commands to the palette.
 *
 * @example
 * ```typescript
 * import { CommandProviderBase, ListPageBase } from '@microsoft/cmdpal-sdk';
 *
 * class MyProvider extends CommandProviderBase {
 *   id = 'my-extension';
 *   displayName = 'My Extension';
 *
 *   topLevelCommands() {
 *     return [{ command: new MyPage(), title: 'My Command', icon: null }];
 *   }
 * }
 * ```
 */
export abstract class CommandProviderBase implements ICommandProvider {
  abstract id: string;
  abstract displayName: string;

  icon?: IconInfo | null = null;
  frozen?: boolean = false;
  settings?: ICommandSettings | null = null;

  protected host?: IExtensionHost;

  abstract topLevelCommands(): Promise<ICommandItem[]> | ICommandItem[];

  fallbackCommands?(): Promise<IFallbackCommandItem[]> | IFallbackCommandItem[] {
    return [];
  }

  getCommand?(id: string): Promise<ICommand | null> | ICommand | null {
    return null;
  }

  initializeWithHost(host: IExtensionHost): void {
    this.host = host;
  }

  dispose(): void {
    // Override to clean up resources
  }
}
