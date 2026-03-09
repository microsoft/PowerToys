// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast navigation compatibility stubs.
 *
 * These cover the imperative navigation functions that Raycast extensions
 * import from `@raycast/api`. Most are no-ops or console stubs for now —
 * CmdPal's navigation model is declarative (via CommandResult), and the
 * compat runtime will need to bridge imperative calls to declarative results.
 */

import { showToast, ToastStyle } from './toast';

/**
 * Open a URL in the default browser.
 */
export async function open(target: string, application?: string): Promise<void> {
  void application;
  try {
    const { execSync } = await import('child_process');
    execSync(`start "" "${target}"`, { stdio: 'ignore', shell: 'cmd.exe' });
    console.error(`[Navigation] Opened: ${target}`);
  } catch {
    console.warn(`[Navigation] Failed to open: ${target}`);
  }
}

/**
 * Close the main Raycast window.
 * In CmdPal this maps to dismissing the palette.
 */
export async function closeMainWindow(options?: { clearRootSearch?: boolean }): Promise<void> {
  void options;
  console.error('[Navigation] closeMainWindow() — CmdPal will dismiss via CommandResult');
}

/**
 * Pop the current view from the navigation stack.
 * In CmdPal this maps to CommandResult.goBack().
 */
export async function popToRoot(options?: { clearSearchBar?: boolean }): Promise<void> {
  void options;
  console.error('[Navigation] popToRoot() — CmdPal will navigate via CommandResult');
}

/**
 * Launch another Raycast command by name.
 * Stub: not supported in CmdPal spike.
 */
export async function launchCommand(options: {
  name: string;
  type: string;
  extensionName?: string;
  ownerOrAuthorName?: string;
  arguments?: Record<string, string>;
  context?: Record<string, unknown>;
  fallbackText?: string;
}): Promise<void> {
  console.warn(`[Navigation] launchCommand("${options.name}") is not yet supported in CmdPal`);
  await showToast({
    style: ToastStyle.Failure,
    title: 'Not Supported',
    message: `launchCommand("${options.name}") is not available in CmdPal`,
  });
}

/**
 * Confirm an action with a dialog.
 * For the spike: always resolves true (auto-confirms).
 */
export async function confirmAlert(options: {
  title: string;
  message?: string;
  icon?: unknown;
  primaryAction?: { title: string; style?: string; onAction?: () => void };
  dismissAction?: { title: string; style?: string; onAction?: () => void };
  rememberUserChoice?: boolean;
}): Promise<boolean> {
  console.error(`[Alert] ${options.title}: ${options.message ?? '(no message)'} — auto-confirming`);
  return true;
}
