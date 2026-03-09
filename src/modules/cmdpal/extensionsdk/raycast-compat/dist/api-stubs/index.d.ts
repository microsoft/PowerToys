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
export { showToast, Toast, ToastStyle } from './toast';
export type { ToastOptions, ToastAction } from './toast';
export { Clipboard } from './clipboard';
export { LocalStorage } from './local-storage';
export { environment, LaunchType } from './environment';
export type { LaunchContext, EnvironmentConfig } from './environment';
export { _configureEnvironment } from './environment';
export { getPreferenceValues, openExtensionPreferences, openCommandPreferences } from './preferences';
export { open, closeMainWindow, popToRoot, launchCommand, confirmAlert } from './navigation';
export { showInFinder, trash, showHUD, getSelectedText, getSelectedFinderItems, getApplications, getDefaultApplication, getFrontmostApplication, } from './system-utilities';
export type { FileSystemItem, Application } from './system-utilities';
export { Icon, resolveIcon } from './icons';
export type { IconKey } from './icons';
export { Color, ColorDynamic, resolveColor } from './colors';
export type { ColorKey } from './colors';
export { AI, useAI } from './ai';
export { useNavigation, useCachedPromise, useFetch } from './hooks';
export { _setStoragePath } from './local-storage';
export { _setPreferencesPath } from './preferences';
//# sourceMappingURL=index.d.ts.map