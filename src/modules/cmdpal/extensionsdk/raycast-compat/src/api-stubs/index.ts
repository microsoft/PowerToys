// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Barrel export for all Raycast API compatibility stubs.
 *
 * This module re-exports everything a Raycast extension might import from
 * `@raycast/api`. When esbuild aliases `@raycast/api` to this package,
 * the extension's imports resolve here.
 *
 * Organized by category:
 * - Toast & notifications
 * - Clipboard
 * - LocalStorage
 * - Environment
 * - Preferences
 * - Navigation & actions
 * - Icons & colors
 * - AI (stub/unsupported)
 * - React hooks
 */

// ── Toast & notifications ──────────────────────────────────────────────
export { showToast, Toast, ToastStyle } from './toast';
export type { ToastOptions, ToastAction } from './toast';

// ── Clipboard ──────────────────────────────────────────────────────────
export { Clipboard } from './clipboard';

// ── LocalStorage ───────────────────────────────────────────────────────
export { LocalStorage } from './local-storage';

// ── Environment ────────────────────────────────────────────────────────
export { environment, LaunchType } from './environment';
export type { LaunchContext, EnvironmentConfig } from './environment';
export { _configureEnvironment } from './environment';

// ── Preferences ────────────────────────────────────────────────────────
export { getPreferenceValues, openExtensionPreferences, openCommandPreferences } from './preferences';

// ── Navigation & actions ───────────────────────────────────────────────
export { open, closeMainWindow, popToRoot, launchCommand, confirmAlert } from './navigation';

// ── System utilities ──────────────────────────────────────────────────
export {
  showInFinder,
  trash,
  showHUD,
  getSelectedText,
  getSelectedFinderItems,
  getApplications,
  getDefaultApplication,
  getFrontmostApplication,
} from './system-utilities';
export type { FileSystemItem, Application } from './system-utilities';

// ── Icons ──────────────────────────────────────────────────────────────
export { Icon, resolveIcon } from './icons';
export type { IconKey } from './icons';

// ── Colors ─────────────────────────────────────────────────────────────
export { Color, ColorDynamic, resolveColor } from './colors';
export type { ColorKey } from './colors';

// ── AI (stub) ──────────────────────────────────────────────────────────
export { AI, useAI } from './ai';

// ── React hooks ────────────────────────────────────────────────────────
export { useNavigation, useCachedPromise, useFetch } from './hooks';

// ── Internal bootstrap (used by compat runtime, not by extensions) ────
export { _setStoragePath } from './local-storage';
export { _setPreferencesPath } from './preferences';
