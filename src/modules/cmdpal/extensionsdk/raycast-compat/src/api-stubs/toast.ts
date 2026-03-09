// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast Toast compatibility stub.
 *
 * Maps Raycast's `showToast()` and `Toast` to CmdPal concepts.
 * For the spike: logs to console and stores state. When CmdPal integration
 * lands, this will use the host's status bar / toast notification mechanism.
 */

export enum ToastStyle {
  Animated = 'animated',
  Success = 'success',
  Failure = 'failure',
}

export interface ToastOptions {
  style?: ToastStyle;
  title: string;
  message?: string;
  primaryAction?: ToastAction;
  secondaryAction?: ToastAction;
}

export interface ToastAction {
  title: string;
  shortcut?: { modifiers: string[]; key: string };
  onAction?: (toast: Toast) => void | Promise<void>;
}

/**
 * Live toast instance. Raycast extensions hold onto these to update
 * toast state (e.g., change from Animated → Success after a fetch).
 */
export class Toast {
  static readonly Style = ToastStyle;

  style: ToastStyle;
  title: string;
  message?: string;
  primaryAction?: ToastAction;
  secondaryAction?: ToastAction;

  constructor(options: ToastOptions) {
    this.style = options.style ?? ToastStyle.Animated;
    this.title = options.title;
    this.message = options.message;
    this.primaryAction = options.primaryAction;
    this.secondaryAction = options.secondaryAction;
  }

  /** Update the toast in-place (Raycast pattern: mutate then call show). */
  async show(): Promise<void> {
    const icon = this.style === ToastStyle.Success ? '✓' :
                 this.style === ToastStyle.Failure ? '✗' : '⟳';
    console.error(`[Toast ${icon}] ${this.title}${this.message ? ': ' + this.message : ''}`);
  }

  async hide(): Promise<void> {
    console.error(`[Toast] Hidden: ${this.title}`);
  }
}

/**
 * Show a toast notification.
 *
 * Raycast signature: `showToast(options)` or `showToast(style, title, message)`.
 */
export async function showToast(optionsOrStyle: ToastOptions | ToastStyle, title?: string, message?: string): Promise<Toast> {
  let options: ToastOptions;

  if (typeof optionsOrStyle === 'string') {
    // Legacy 3-arg form: showToast(style, title, message)
    options = { style: optionsOrStyle, title: title ?? '', message };
  } else {
    options = optionsOrStyle;
  }

  const toast = new Toast(options);
  await toast.show();
  return toast;
}
