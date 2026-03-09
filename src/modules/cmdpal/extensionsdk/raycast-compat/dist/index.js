"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.useNavigation = exports.useAI = exports.AI = exports.resolveColor = exports.ColorDynamic = exports.Color = exports.resolveIcon = exports.Icon = exports.getFrontmostApplication = exports.getDefaultApplication = exports.getApplications = exports.getSelectedFinderItems = exports.getSelectedText = exports.showHUD = exports.trash = exports.showInFinder = exports.confirmAlert = exports.launchCommand = exports.popToRoot = exports.closeMainWindow = exports.open = exports.openCommandPreferences = exports.openExtensionPreferences = exports.getPreferenceValues = exports._configureEnvironment = exports.LaunchType = exports.environment = exports.LocalStorage = exports.Clipboard = exports.ToastStyle = exports.Toast = exports.showToast = exports.RaycastBridgeProvider = exports.RaycastMarkdownContent = exports.RaycastListItem = exports.RaycastContentPage = exports.RaycastDynamicListPage = exports.translateVNode = exports.translateTree = exports.Grid = exports.Form = exports.Action = exports.ActionPanel = exports.Detail = exports.List = exports.isElementVNode = exports.isTextVNode = exports.reconciler = exports.renderToVNodeTree = exports.render = void 0;
exports._setPreferencesPath = exports._setStoragePath = exports.useFetch = exports.useCachedPromise = void 0;
/**
 * @cmdpal/raycast-compat — Raycast API compatibility layer for CmdPal.
 *
 * This package provides:
 * 1. A custom React reconciler that captures VNode trees (not DOM)
 * 2. Marker components that mimic @raycast/api exports
 * 3. A translator that maps VNode trees → CmdPal SDK types
 */
// Reconciler
var reconciler_1 = require("./reconciler");
Object.defineProperty(exports, "render", { enumerable: true, get: function () { return reconciler_1.render; } });
Object.defineProperty(exports, "renderToVNodeTree", { enumerable: true, get: function () { return reconciler_1.renderToVNodeTree; } });
Object.defineProperty(exports, "reconciler", { enumerable: true, get: function () { return reconciler_1.reconciler; } });
var reconciler_2 = require("./reconciler");
Object.defineProperty(exports, "isTextVNode", { enumerable: true, get: function () { return reconciler_2.isTextVNode; } });
Object.defineProperty(exports, "isElementVNode", { enumerable: true, get: function () { return reconciler_2.isElementVNode; } });
// Marker components (the fake @raycast/api)
var components_1 = require("./components");
Object.defineProperty(exports, "List", { enumerable: true, get: function () { return components_1.List; } });
Object.defineProperty(exports, "Detail", { enumerable: true, get: function () { return components_1.Detail; } });
Object.defineProperty(exports, "ActionPanel", { enumerable: true, get: function () { return components_1.ActionPanel; } });
Object.defineProperty(exports, "Action", { enumerable: true, get: function () { return components_1.Action; } });
Object.defineProperty(exports, "Form", { enumerable: true, get: function () { return components_1.Form; } });
Object.defineProperty(exports, "Grid", { enumerable: true, get: function () { return components_1.Grid; } });
// Translator — legacy spike API (plain objects)
var translator_1 = require("./translator");
Object.defineProperty(exports, "translateTree", { enumerable: true, get: function () { return translator_1.translateTree; } });
// Translator — full CmdPal SDK-compatible API
var translator_2 = require("./translator");
Object.defineProperty(exports, "translateVNode", { enumerable: true, get: function () { return translator_2.translateVNode; } });
var translator_3 = require("./translator");
Object.defineProperty(exports, "RaycastDynamicListPage", { enumerable: true, get: function () { return translator_3.RaycastDynamicListPage; } });
Object.defineProperty(exports, "RaycastContentPage", { enumerable: true, get: function () { return translator_3.RaycastContentPage; } });
Object.defineProperty(exports, "RaycastListItem", { enumerable: true, get: function () { return translator_3.RaycastListItem; } });
Object.defineProperty(exports, "RaycastMarkdownContent", { enumerable: true, get: function () { return translator_3.RaycastMarkdownContent; } });
// Bridge — ties reconciler + translator to CmdPal's JSON-RPC protocol
var bridge_provider_1 = require("./bridge/bridge-provider");
Object.defineProperty(exports, "RaycastBridgeProvider", { enumerable: true, get: function () { return bridge_provider_1.RaycastBridgeProvider; } });
// API stubs — non-UI exports from @raycast/api
var api_stubs_1 = require("./api-stubs");
// Toast & notifications
Object.defineProperty(exports, "showToast", { enumerable: true, get: function () { return api_stubs_1.showToast; } });
Object.defineProperty(exports, "Toast", { enumerable: true, get: function () { return api_stubs_1.Toast; } });
Object.defineProperty(exports, "ToastStyle", { enumerable: true, get: function () { return api_stubs_1.ToastStyle; } });
// Clipboard
Object.defineProperty(exports, "Clipboard", { enumerable: true, get: function () { return api_stubs_1.Clipboard; } });
// LocalStorage
Object.defineProperty(exports, "LocalStorage", { enumerable: true, get: function () { return api_stubs_1.LocalStorage; } });
// Environment
Object.defineProperty(exports, "environment", { enumerable: true, get: function () { return api_stubs_1.environment; } });
Object.defineProperty(exports, "LaunchType", { enumerable: true, get: function () { return api_stubs_1.LaunchType; } });
Object.defineProperty(exports, "_configureEnvironment", { enumerable: true, get: function () { return api_stubs_1._configureEnvironment; } });
// Preferences
Object.defineProperty(exports, "getPreferenceValues", { enumerable: true, get: function () { return api_stubs_1.getPreferenceValues; } });
Object.defineProperty(exports, "openExtensionPreferences", { enumerable: true, get: function () { return api_stubs_1.openExtensionPreferences; } });
Object.defineProperty(exports, "openCommandPreferences", { enumerable: true, get: function () { return api_stubs_1.openCommandPreferences; } });
// Navigation & actions
Object.defineProperty(exports, "open", { enumerable: true, get: function () { return api_stubs_1.open; } });
Object.defineProperty(exports, "closeMainWindow", { enumerable: true, get: function () { return api_stubs_1.closeMainWindow; } });
Object.defineProperty(exports, "popToRoot", { enumerable: true, get: function () { return api_stubs_1.popToRoot; } });
Object.defineProperty(exports, "launchCommand", { enumerable: true, get: function () { return api_stubs_1.launchCommand; } });
Object.defineProperty(exports, "confirmAlert", { enumerable: true, get: function () { return api_stubs_1.confirmAlert; } });
// System utilities
Object.defineProperty(exports, "showInFinder", { enumerable: true, get: function () { return api_stubs_1.showInFinder; } });
Object.defineProperty(exports, "trash", { enumerable: true, get: function () { return api_stubs_1.trash; } });
Object.defineProperty(exports, "showHUD", { enumerable: true, get: function () { return api_stubs_1.showHUD; } });
Object.defineProperty(exports, "getSelectedText", { enumerable: true, get: function () { return api_stubs_1.getSelectedText; } });
Object.defineProperty(exports, "getSelectedFinderItems", { enumerable: true, get: function () { return api_stubs_1.getSelectedFinderItems; } });
Object.defineProperty(exports, "getApplications", { enumerable: true, get: function () { return api_stubs_1.getApplications; } });
Object.defineProperty(exports, "getDefaultApplication", { enumerable: true, get: function () { return api_stubs_1.getDefaultApplication; } });
Object.defineProperty(exports, "getFrontmostApplication", { enumerable: true, get: function () { return api_stubs_1.getFrontmostApplication; } });
// Icons & colors
Object.defineProperty(exports, "Icon", { enumerable: true, get: function () { return api_stubs_1.Icon; } });
Object.defineProperty(exports, "resolveIcon", { enumerable: true, get: function () { return api_stubs_1.resolveIcon; } });
Object.defineProperty(exports, "Color", { enumerable: true, get: function () { return api_stubs_1.Color; } });
Object.defineProperty(exports, "ColorDynamic", { enumerable: true, get: function () { return api_stubs_1.ColorDynamic; } });
Object.defineProperty(exports, "resolveColor", { enumerable: true, get: function () { return api_stubs_1.resolveColor; } });
// AI (stub)
Object.defineProperty(exports, "AI", { enumerable: true, get: function () { return api_stubs_1.AI; } });
Object.defineProperty(exports, "useAI", { enumerable: true, get: function () { return api_stubs_1.useAI; } });
// React hooks
Object.defineProperty(exports, "useNavigation", { enumerable: true, get: function () { return api_stubs_1.useNavigation; } });
Object.defineProperty(exports, "useCachedPromise", { enumerable: true, get: function () { return api_stubs_1.useCachedPromise; } });
Object.defineProperty(exports, "useFetch", { enumerable: true, get: function () { return api_stubs_1.useFetch; } });
// Internal bootstrap
Object.defineProperty(exports, "_setStoragePath", { enumerable: true, get: function () { return api_stubs_1._setStoragePath; } });
Object.defineProperty(exports, "_setPreferencesPath", { enumerable: true, get: function () { return api_stubs_1._setPreferencesPath; } });
//# sourceMappingURL=index.js.map