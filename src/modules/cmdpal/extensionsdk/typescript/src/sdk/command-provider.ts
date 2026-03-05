// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ICommandProvider,
  ICommandItem,
  IFallbackCommandItem,
  ICommand,
  IIconInfo,
  IExtensionHost,
  MessageState,
  IStatusMessage,
  StatusContext,
} from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';
import type { CommandSettings } from './settings';

/**
 * Abstract base class for building Command Palette extensions.
 * Extend this class and implement the required abstract properties and methods.
 */
export abstract class CommandProvider implements ICommandProvider {
  private _transport?: JsonRpcTransport;
  private _host?: IExtensionHost;

  /**
   * Unique identifier for this extension provider.
   */
  abstract get id(): string;

  /**
   * Display name shown to users.
   */
  abstract get displayName(): string;

  /**
   * Icon for this provider (optional).
   */
  get icon(): IIconInfo | undefined {
    return undefined;
  }

  /**
   * Returns top-level commands shown in the command palette.
   * Override to provide your extension's main commands.
   */
  topLevelCommands(): ICommandItem[] {
    return [];
  }

  /**
   * Returns fallback commands that execute when no other command matches.
   * Override to provide fallback functionality.
   */
  fallbackCommands(): IFallbackCommandItem[] {
    return [];
  }

  /**
   * Gets a specific command by ID.
   * Override to provide detailed command information and pages.
   */
  getCommand(id: string): ICommand | undefined {
    return undefined;
  }

  /**
   * Returns the settings page for this extension.
   * Override to expose a settings form to users.
   */
  get settings(): CommandSettings | undefined {
    return undefined;
  }

  /**
   * Called by the framework to initialize the provider with the host.
   * Do not call this directly.
   */
  _initializeWithHost(transport: JsonRpcTransport): void {
    this._transport = transport;
    // Note: Host proxy will be implemented in extension-server.ts
  }

  /**
   * Log a message to the host.
   * @param message The message to log
   * @param state The severity level (default: Info)
   */
  protected log(message: string, state: MessageState = MessageState.Info): void {
    if (!this._transport) {
      return;
    }

    this._transport.sendNotification('host/logMessage', {
      message,
      state,
    });
  }

  /**
   * Show a status message to the user.
   * @param message The status message object
   * @param context The context for the status message
   */
  protected showStatus(message: IStatusMessage, context: StatusContext = StatusContext.Extension): void {
    if (!this._transport) {
      return;
    }

    this._transport.sendNotification('host/showStatus', {
      message,
      context,
    });
  }

  /**
   * Hide a previously shown status message.
   * @param message The status message to hide
   */
  protected hideStatus(message: IStatusMessage): void {
    if (!this._transport) {
      return;
    }

    this._transport.sendNotification('host/hideStatus', {
      message,
    });
  }

  /**
   * Notify the host that items have changed.
   * Call this when your top-level commands or fallback commands change.
   * @param totalItems Optional total number of items
   */
  protected notifyItemsChanged(totalItems?: number): void {
    if (!this._transport) {
      return;
    }

    this._transport.sendNotification('provider/itemsChanged', {
      totalItems: totalItems ?? -1,
    });
  }

  /**
   * Notify the host that a property has changed.
   * @param propertyName The name of the property that changed
   */
  protected notifyPropChanged(propertyName: string): void {
    if (!this._transport) {
      return;
    }

    this._transport.sendNotification('provider/propChanged', {
      propertyName,
    });
  }

  /**
   * Clean up resources when the extension is disposed.
   * Override to implement custom cleanup logic.
   */
  dispose(): void {
    // Default: no-op
  }

  close(): void {
    this.dispose();
  }
}
