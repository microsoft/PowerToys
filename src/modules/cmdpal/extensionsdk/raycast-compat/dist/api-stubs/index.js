"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports._setPreferencesPath = exports._setStoragePath = exports.useFetch = exports.useCachedPromise = exports.useNavigation = exports.useAI = exports.AI = exports.resolveColor = exports.ColorDynamic = exports.Color = exports.resolveIcon = exports.Icon = exports.getFrontmostApplication = exports.getDefaultApplication = exports.getApplications = exports.getSelectedFinderItems = exports.getSelectedText = exports.showHUD = exports.trash = exports.showInFinder = exports.confirmAlert = exports.launchCommand = exports.popToRoot = exports.closeMainWindow = exports.open = exports.openCommandPreferences = exports.openExtensionPreferences = exports.getPreferenceValues = exports._configureEnvironment = exports.LaunchType = exports.environment = exports.LocalStorage = exports.Clipboard = exports.ToastStyle = exports.Toast = exports.showToast = void 0;
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
var toast_1 = require("./toast");
Object.defineProperty(exports, "showToast", { enumerable: true, get: function () { return toast_1.showToast; } });
Object.defineProperty(exports, "Toast", { enumerable: true, get: function () { return toast_1.Toast; } });
Object.defineProperty(exports, "ToastStyle", { enumerable: true, get: function () { return toast_1.ToastStyle; } });
// ── Clipboard ──────────────────────────────────────────────────────────
var clipboard_1 = require("./clipboard");
Object.defineProperty(exports, "Clipboard", { enumerable: true, get: function () { return clipboard_1.Clipboard; } });
// ── LocalStorage ───────────────────────────────────────────────────────
var local_storage_1 = require("./local-storage");
Object.defineProperty(exports, "LocalStorage", { enumerable: true, get: function () { return local_storage_1.LocalStorage; } });
// ── Environment ────────────────────────────────────────────────────────
var environment_1 = require("./environment");
Object.defineProperty(exports, "environment", { enumerable: true, get: function () { return environment_1.environment; } });
Object.defineProperty(exports, "LaunchType", { enumerable: true, get: function () { return environment_1.LaunchType; } });
var environment_2 = require("./environment");
Object.defineProperty(exports, "_configureEnvironment", { enumerable: true, get: function () { return environment_2._configureEnvironment; } });
// ── Preferences ────────────────────────────────────────────────────────
var preferences_1 = require("./preferences");
Object.defineProperty(exports, "getPreferenceValues", { enumerable: true, get: function () { return preferences_1.getPreferenceValues; } });
Object.defineProperty(exports, "openExtensionPreferences", { enumerable: true, get: function () { return preferences_1.openExtensionPreferences; } });
Object.defineProperty(exports, "openCommandPreferences", { enumerable: true, get: function () { return preferences_1.openCommandPreferences; } });
// ── Navigation & actions ───────────────────────────────────────────────
var navigation_1 = require("./navigation");
Object.defineProperty(exports, "open", { enumerable: true, get: function () { return navigation_1.open; } });
Object.defineProperty(exports, "closeMainWindow", { enumerable: true, get: function () { return navigation_1.closeMainWindow; } });
Object.defineProperty(exports, "popToRoot", { enumerable: true, get: function () { return navigation_1.popToRoot; } });
Object.defineProperty(exports, "launchCommand", { enumerable: true, get: function () { return navigation_1.launchCommand; } });
Object.defineProperty(exports, "confirmAlert", { enumerable: true, get: function () { return navigation_1.confirmAlert; } });
// ── System utilities ──────────────────────────────────────────────────
var system_utilities_1 = require("./system-utilities");
Object.defineProperty(exports, "showInFinder", { enumerable: true, get: function () { return system_utilities_1.showInFinder; } });
Object.defineProperty(exports, "trash", { enumerable: true, get: function () { return system_utilities_1.trash; } });
Object.defineProperty(exports, "showHUD", { enumerable: true, get: function () { return system_utilities_1.showHUD; } });
Object.defineProperty(exports, "getSelectedText", { enumerable: true, get: function () { return system_utilities_1.getSelectedText; } });
Object.defineProperty(exports, "getSelectedFinderItems", { enumerable: true, get: function () { return system_utilities_1.getSelectedFinderItems; } });
Object.defineProperty(exports, "getApplications", { enumerable: true, get: function () { return system_utilities_1.getApplications; } });
Object.defineProperty(exports, "getDefaultApplication", { enumerable: true, get: function () { return system_utilities_1.getDefaultApplication; } });
Object.defineProperty(exports, "getFrontmostApplication", { enumerable: true, get: function () { return system_utilities_1.getFrontmostApplication; } });
// ── Icons ──────────────────────────────────────────────────────────────
var icons_1 = require("./icons");
Object.defineProperty(exports, "Icon", { enumerable: true, get: function () { return icons_1.Icon; } });
Object.defineProperty(exports, "resolveIcon", { enumerable: true, get: function () { return icons_1.resolveIcon; } });
// ── Colors ─────────────────────────────────────────────────────────────
var colors_1 = require("./colors");
Object.defineProperty(exports, "Color", { enumerable: true, get: function () { return colors_1.Color; } });
Object.defineProperty(exports, "ColorDynamic", { enumerable: true, get: function () { return colors_1.ColorDynamic; } });
Object.defineProperty(exports, "resolveColor", { enumerable: true, get: function () { return colors_1.resolveColor; } });
// ── AI (stub) ──────────────────────────────────────────────────────────
var ai_1 = require("./ai");
Object.defineProperty(exports, "AI", { enumerable: true, get: function () { return ai_1.AI; } });
Object.defineProperty(exports, "useAI", { enumerable: true, get: function () { return ai_1.useAI; } });
// ── React hooks ────────────────────────────────────────────────────────
var hooks_1 = require("./hooks");
Object.defineProperty(exports, "useNavigation", { enumerable: true, get: function () { return hooks_1.useNavigation; } });
Object.defineProperty(exports, "useCachedPromise", { enumerable: true, get: function () { return hooks_1.useCachedPromise; } });
Object.defineProperty(exports, "useFetch", { enumerable: true, get: function () { return hooks_1.useFetch; } });
// ── Internal bootstrap (used by compat runtime, not by extensions) ────
var local_storage_2 = require("./local-storage");
Object.defineProperty(exports, "_setStoragePath", { enumerable: true, get: function () { return local_storage_2._setStoragePath; } });
var preferences_2 = require("./preferences");
Object.defineProperty(exports, "_setPreferencesPath", { enumerable: true, get: function () { return preferences_2._setPreferencesPath; } });
//# sourceMappingURL=index.js.map