// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * @cmdpal/raycast-compat — Raycast API compatibility layer for CmdPal.
 *
 * This package provides:
 * 1. A custom React reconciler that captures VNode trees (not DOM)
 * 2. Marker components that mimic @raycast/api exports
 * 3. A translator that maps VNode trees → CmdPal SDK types
 */

// Reconciler
export { render, renderToVNodeTree, reconciler } from './reconciler';
export type { VNode, TextVNode, AnyVNode, Container, RenderResult } from './reconciler';
export { isTextVNode, isElementVNode } from './reconciler';

// Marker components (the fake @raycast/api)
export { List, Detail, ActionPanel, Action, Form, Grid } from './components';

// Translator — legacy spike API (plain objects)
export { translateTree } from './translator';
export type {
  TranslatedPage,
  TranslatedListPage,
  TranslatedDetailPage,
  TranslatedListItem,
  TranslatedAction,
} from './translator';

// Translator — full CmdPal SDK-compatible API
export { translateVNode } from './translator';
export {
  RaycastDynamicListPage,
  RaycastContentPage,
  RaycastListItem,
  RaycastMarkdownContent,
} from './translator';
export type {
  IconData,
  IconInfo,
  Tag,
  ContextItem,
  DetailsElement,
  Details,
} from './translator';

// Bridge — ties reconciler + translator to CmdPal's JSON-RPC protocol
export { RaycastBridgeProvider } from './bridge/bridge-provider';
export type {
  RaycastCommandManifest,
  RaycastExtensionManifest,
  RaycastCommandModule,
  NotifyFn,
  PageSnapshot,
} from './bridge/bridge-provider';

// API stubs — non-UI exports from @raycast/api
export {
  // Toast & notifications
  showToast, Toast, ToastStyle,
  // Clipboard
  Clipboard,
  // LocalStorage
  LocalStorage,
  // Environment
  environment, LaunchType, _configureEnvironment,
  // Preferences
  getPreferenceValues, openExtensionPreferences, openCommandPreferences,
  // Navigation & actions
  open, closeMainWindow, popToRoot, launchCommand, confirmAlert,
  // System utilities
  showInFinder, trash, showHUD, getSelectedText,
  getSelectedFinderItems, getApplications, getDefaultApplication, getFrontmostApplication,
  // Icons & colors
  Icon, resolveIcon, Color, ColorDynamic, resolveColor,
  // AI (stub)
  AI, useAI,
  // React hooks
  useNavigation, useCachedPromise, useFetch,
  // Internal bootstrap
  _setStoragePath, _setPreferencesPath,
} from './api-stubs';

export type {
  ToastOptions, ToastAction, LaunchContext, EnvironmentConfig,
  IconKey, ColorKey,
  FileSystemItem, Application,
} from './api-stubs';
