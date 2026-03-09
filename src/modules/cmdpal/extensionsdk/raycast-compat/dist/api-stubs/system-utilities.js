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
exports.showInFinder = showInFinder;
exports.trash = trash;
exports.showHUD = showHUD;
exports.getSelectedText = getSelectedText;
exports.getSelectedFinderItems = getSelectedFinderItems;
exports.getApplications = getApplications;
exports.getDefaultApplication = getDefaultApplication;
exports.getFrontmostApplication = getFrontmostApplication;
/**
 * Raycast system utility compatibility stubs.
 *
 * These cover system-level functions from `@raycast/api` like file
 * operations, application queries, and HUD display. On Windows we
 * provide real implementations where possible (e.g., `showInFinder`
 * → `explorer /select,`), otherwise stubs with helpful warnings.
 */
const toast_1 = require("./toast");
// ── File system utilities ──────────────────────────────────────────────
/**
 * Reveal a file or folder in the system file manager.
 *
 * macOS: Finder → `open -R <path>`
 * Windows: Explorer → `explorer /select,"<path>"`
 */
async function showInFinder(path) {
    try {
        const { execSync } = await Promise.resolve().then(() => __importStar(require('child_process')));
        // explorer /select, highlights the item in its parent folder
        execSync(`explorer /select,"${path.replace(/\//g, '\\')}"`, {
            stdio: 'ignore',
            shell: 'cmd.exe',
        });
        console.log(`[SystemUtils] Revealed in Explorer: ${path}`);
    }
    catch {
        console.warn(`[SystemUtils] Failed to reveal in Explorer: ${path}`);
    }
}
/**
 * Move a file or folder to the system trash/recycle bin.
 *
 * macOS: Finder trash
 * Windows: Recycle Bin via PowerShell
 */
async function trash(path) {
    const paths = Array.isArray(path) ? path : [path];
    try {
        const { execSync } = await Promise.resolve().then(() => __importStar(require('child_process')));
        for (const p of paths) {
            const escaped = p.replace(/'/g, "''");
            execSync(`powershell -NoProfile -Command "Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile('${escaped}', 'OnlyErrorDialogs', 'SendToRecycleBin')"`, { stdio: 'ignore' });
        }
        console.log(`[SystemUtils] Moved to Recycle Bin: ${paths.join(', ')}`);
    }
    catch {
        console.warn(`[SystemUtils] Failed to move to Recycle Bin: ${paths.join(', ')}`);
    }
}
// ── HUD display ────────────────────────────────────────────────────────
/**
 * Show a brief heads-up display message.
 * Maps to a CmdPal toast with auto-dismiss style.
 */
async function showHUD(title) {
    console.log(`[HUD] ${title}`);
    await (0, toast_1.showToast)({ style: toast_1.ToastStyle.Success, title });
}
// ── Text selection ─────────────────────────────────────────────────────
/**
 * Get the currently selected text in the frontmost application.
 * Stub: not supported on Windows in the CmdPal compat layer.
 */
async function getSelectedText() {
    console.warn('[SystemUtils] getSelectedText() is not supported in CmdPal');
    return '';
}
/**
 * Get the files/folders currently selected in the file manager.
 * Stub: not supported in CmdPal.
 */
async function getSelectedFinderItems() {
    console.warn('[SystemUtils] getSelectedFinderItems() is not supported in CmdPal');
    return [];
}
/**
 * Get a list of installed applications.
 * Stub: returns an empty array.
 */
async function getApplications() {
    console.warn('[SystemUtils] getApplications() is not yet implemented in CmdPal');
    return [];
}
/**
 * Get the default application for a file type or URL scheme.
 * Stub: returns a placeholder.
 */
async function getDefaultApplication(pathOrUrl) {
    void pathOrUrl;
    console.warn('[SystemUtils] getDefaultApplication() is not yet implemented in CmdPal');
    return { name: 'Default', path: '' };
}
/**
 * Get the frontmost (focused) application.
 * Stub: returns a placeholder.
 */
async function getFrontmostApplication() {
    console.warn('[SystemUtils] getFrontmostApplication() is not yet implemented in CmdPal');
    return { name: 'Unknown', path: '' };
}
//# sourceMappingURL=system-utilities.js.map