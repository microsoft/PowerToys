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

  /** Sends a log message to the host. */
  static log(message: string, state: MessageState = 'info'): void {
    ExtensionHost.instance?.log(message, state);
  }

  /** Shows a status message in the Command Palette status bar. */
  static showStatus(message: string, state: MessageState = 'info', progress?: ProgressState): void {
    ExtensionHost.instance?.showStatus(message, state, progress);
  }

  /** Hides a previously shown status message. */
  static hideStatus(messageId: string): void {
    ExtensionHost.instance?.hideStatus(messageId);
  }

  /** Asks the host to copy text to the system clipboard. */
  static copyToClipboard(text: string): void {
    ExtensionHost.instance?.copyToClipboard(text);
  }
}
