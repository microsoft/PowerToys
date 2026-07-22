// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { IExtensionHost, MessageState, ProgressState } from '../types.js';

/**
 * Static bridge for talking to the Command Palette host from anywhere in an
 * extension. The runtime wires up the underlying implementation when the
 * JSON-RPC server starts. Calls made before then are no-ops, so this is safe to
 * reference during module initialization and in tests.
 *
 * @example
 * ```typescript
 * ExtensionHost.log('Extension loaded');
 * ExtensionHost.showStatus('Loading...', 'info', { isIndeterminate: true });
 * ExtensionHost.copyToClipboard('Hello, clipboard!');
 * ```
 */
export class ExtensionHost {
  private static instance: IExtensionHost | null = null;

  /** Installs the host implementation. Called by the runtime; internal. */
  static initialize(host: IExtensionHost | null): void {
    ExtensionHost.instance = host;
  }

  /** Whether a host implementation has been installed. */
  static get isInitialized(): boolean {
    return ExtensionHost.instance !== null;
  }

  /**
   * Sends a log message to the host. No-op until the runtime installs a host.
   *
   * @param message Text to log.
   * @param state Severity of the message. Defaults to `info`.
   */
  static log(message: string, state: MessageState = 'info'): void {
    ExtensionHost.instance?.log(message, state);
  }

  /**
   * Shows a status message in the Command Palette status bar. No-op until the
   * runtime installs a host.
   *
   * @param message Text to display.
   * @param state Severity of the message. Defaults to `info`.
   * @param progress Optional progress shown alongside the message.
   * @returns A stable status id for later {@link ExtensionHost.updateStatus} or
   * {@link ExtensionHost.hideStatus} calls, or an empty string when no host is
   * installed yet.
   */
  static showStatus(
    message: string,
    state: MessageState = 'info',
    progress?: ProgressState,
  ): string {
    return ExtensionHost.instance?.showStatus(message, state, progress) ?? '';
  }

  /**
   * Updates a status shown earlier without creating a duplicate. No-op until
   * the runtime installs a host.
   *
   * @param statusId Id returned by {@link ExtensionHost.showStatus}.
   * @param message New text to display.
   * @param state New severity. Defaults to `info`.
   * @param progress New progress, if any.
   */
  static updateStatus(
    statusId: string,
    message: string,
    state: MessageState = 'info',
    progress?: ProgressState,
  ): void {
    ExtensionHost.instance?.updateStatus(statusId, message, state, progress);
  }

  /**
   * Hides a previously shown status message. No-op until the runtime installs a
   * host.
   *
   * @param statusId Id returned by {@link ExtensionHost.showStatus}.
   */
  static hideStatus(statusId: string): void {
    ExtensionHost.instance?.hideStatus(statusId);
  }

  /**
   * Asks the host to copy text to the system clipboard. No-op until the runtime
   * installs a host.
   *
   * @param text Text to place on the clipboard.
   */
  static copyToClipboard(text: string): void {
    ExtensionHost.instance?.copyToClipboard(text);
  }
}
