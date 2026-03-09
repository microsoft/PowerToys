"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports._setPreferencesPath = _setPreferencesPath;
exports.getPreferenceValues = getPreferenceValues;
exports.openExtensionPreferences = openExtensionPreferences;
exports.openCommandPreferences = openCommandPreferences;
/**
 * Raycast preferences compatibility stub.
 *
 * Maps Raycast's `getPreferenceValues<T>()` and `openExtensionPreferences()`
 * to CmdPal's file-based preferences system.
 *
 * Preferences are read from a JSON file that's populated from the Raycast
 * manifest's `preferences[]` during install (via the manifest translator).
 * The file lives at `<supportPath>/preferences.json`.
 */
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
let preferencesPath = null;
let cachedPrefs = null;
/** Internal: set the preferences file path (called by environment setup). */
function _setPreferencesPath(supportPath) {
    preferencesPath = path.join(supportPath, 'preferences.json');
    cachedPrefs = null; // Force reload
}
function getPrefsFile() {
    if (!preferencesPath) {
        const fallback = path.join(process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.', 'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions', '_raycast-compat');
        preferencesPath = path.join(fallback, 'preferences.json');
    }
    return preferencesPath;
}
function loadPreferences() {
    if (cachedPrefs !== null)
        return cachedPrefs;
    const file = getPrefsFile();
    try {
        if (fs.existsSync(file)) {
            const raw = fs.readFileSync(file, 'utf-8');
            cachedPrefs = JSON.parse(raw);
        }
        else {
            cachedPrefs = {};
        }
    }
    catch {
        console.warn('[Preferences] Failed to read preferences file, using empty defaults');
        cachedPrefs = {};
    }
    return cachedPrefs;
}
/**
 * Returns the extension's preference values.
 *
 * Raycast extensions call this as `getPreferenceValues<T>()` to get typed
 * preferences. We read from the JSON file and return the raw object.
 */
function getPreferenceValues() {
    return loadPreferences();
}
/**
 * Opens the extension's preferences panel.
 * Stub: logs a warning. CmdPal will handle this through the settings UI.
 */
async function openExtensionPreferences() {
    console.warn('[Preferences] openExtensionPreferences() is not yet supported in CmdPal');
}
/**
 * Opens the command's preferences panel.
 * Stub: logs a warning. CmdPal will handle this through the settings UI.
 */
async function openCommandPreferences() {
    console.warn('[Preferences] openCommandPreferences() is not yet supported in CmdPal');
}
//# sourceMappingURL=preferences.js.map