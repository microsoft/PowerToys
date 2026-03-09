/**
 * @cmdpal/raycast-compat — Raycast API compatibility layer for CmdPal.
 *
 * This package provides:
 * 1. A custom React reconciler that captures VNode trees (not DOM)
 * 2. Marker components that mimic @raycast/api exports
 * 3. A translator that maps VNode trees → CmdPal SDK types
 */
export { render, renderToVNodeTree, reconciler } from './reconciler';
export type { VNode, TextVNode, AnyVNode, Container, RenderResult } from './reconciler';
export { isTextVNode, isElementVNode } from './reconciler';
export { List, Detail, ActionPanel, Action, Form, Grid } from './components';
export { translateTree } from './translator';
export type { TranslatedPage, TranslatedListPage, TranslatedDetailPage, TranslatedListItem, TranslatedAction, } from './translator';
export { translateVNode } from './translator';
export { RaycastDynamicListPage, RaycastContentPage, RaycastListItem, RaycastMarkdownContent, } from './translator';
export type { IconData, IconInfo, Tag, ContextItem, DetailsElement, Details, } from './translator';
export { RaycastBridgeProvider } from './bridge/bridge-provider';
export type { RaycastCommandManifest, RaycastExtensionManifest, RaycastCommandModule, NotifyFn, PageSnapshot, } from './bridge/bridge-provider';
export { showToast, Toast, ToastStyle, Clipboard, LocalStorage, environment, LaunchType, _configureEnvironment, getPreferenceValues, openExtensionPreferences, openCommandPreferences, open, closeMainWindow, popToRoot, launchCommand, confirmAlert, showInFinder, trash, showHUD, getSelectedText, getSelectedFinderItems, getApplications, getDefaultApplication, getFrontmostApplication, Icon, resolveIcon, Color, ColorDynamic, resolveColor, AI, useAI, useNavigation, useCachedPromise, useFetch, _setStoragePath, _setPreferencesPath, } from './api-stubs';
export type { ToastOptions, ToastAction, LaunchContext, EnvironmentConfig, IconKey, ColorKey, FileSystemItem, Application, } from './api-stubs';
//# sourceMappingURL=index.d.ts.map