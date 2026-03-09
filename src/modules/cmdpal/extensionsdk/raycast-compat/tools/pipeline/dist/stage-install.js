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
exports.getDefaultInstallDir = getDefaultInstallDir;
exports.installExtension = installExtension;
/**
 * Stage 5: Install — Copies the built extension to the CmdPal JSExtensions
 * directory using an atomic rename to avoid triggering hot-reload during
 * incremental file writes.
 */
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
/** Default CmdPal JSExtensions install directory. */
function getDefaultInstallDir() {
    const localAppData = process.env.LOCALAPPDATA;
    if (!localAppData) {
        throw new Error('LOCALAPPDATA environment variable not set. ' +
            'Cannot determine CmdPal extension install directory.');
    }
    return path.join(localAppData, 'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions');
}
/**
 * Install a built extension by atomically placing it in the CmdPal
 * JSExtensions directory.
 *
 * Strategy (to avoid triggering CmdPal's source file watcher during writes):
 *   1. Read cmdpal.json from the build output to get the extension name
 *   2. Copy all files to a temporary staging directory (<name>.installing)
 *   3. Remove the existing extension directory (if present)
 *   4. Atomically rename the staging directory to the final name
 *   5. Verify the installed cmdpal.json and entry point
 *
 * This ensures that the extension directory transitions from
 * "not present" → "fully populated" in a single rename operation,
 * minimising the window where CmdPal's file watchers see partial state.
 */
function installExtension(buildOutputDir, installBaseDir) {
    const errors = [];
    // Read the manifest to get the extension name
    const manifestPath = path.join(buildOutputDir, 'cmdpal.json');
    if (!fs.existsSync(manifestPath)) {
        return {
            success: false,
            extensionPath: '',
            errors: ['cmdpal.json not found in build output'],
        };
    }
    let manifest;
    try {
        manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf-8'));
    }
    catch {
        return {
            success: false,
            extensionPath: '',
            errors: ['Failed to parse cmdpal.json from build output'],
        };
    }
    if (!manifest.name || !manifest.main) {
        return {
            success: false,
            extensionPath: '',
            errors: ['Invalid cmdpal.json: missing required "name" or "main" field'],
        };
    }
    const extDir = path.join(installBaseDir, manifest.name);
    const stagingDir = extDir + '.installing';
    const backupDir = extDir + '.backup';
    // Clean up any leftover staging/backup dirs from previous failed installs
    cleanDir(stagingDir);
    cleanDir(backupDir);
    // Stage: copy all build output to the staging directory
    copyDirRecursive(buildOutputDir, stagingDir);
    // Atomic swap: move old dir out, move staging dir in
    try {
        if (fs.existsSync(extDir)) {
            // Rename existing → backup (fast, same volume)
            fs.renameSync(extDir, backupDir);
        }
        // Rename staging → final target (atomic for the directory watcher)
        fs.renameSync(stagingDir, extDir);
    }
    catch (renameErr) {
        // If rename fails, try to restore backup
        if (fs.existsSync(backupDir) && !fs.existsSync(extDir)) {
            try {
                fs.renameSync(backupDir, extDir);
            }
            catch {
                // Best effort restore
            }
        }
        cleanDir(stagingDir);
        return {
            success: false,
            extensionPath: '',
            errors: [`Failed to install extension: ${renameErr.message}`],
        };
    }
    // Clean up the backup
    cleanDir(backupDir);
    // Verify the installed manifest
    const installedManifest = path.join(extDir, 'cmdpal.json');
    if (!fs.existsSync(installedManifest)) {
        errors.push('cmdpal.json missing after copy — installation may be corrupt');
    }
    else {
        try {
            const check = JSON.parse(fs.readFileSync(installedManifest, 'utf-8'));
            if (!check.name || !check.main) {
                errors.push('Installed cmdpal.json is invalid (missing name or main)');
            }
        }
        catch {
            errors.push('Installed cmdpal.json is not valid JSON');
        }
    }
    // Verify the main entry point exists
    const mainPath = path.join(extDir, manifest.main);
    if (!fs.existsSync(mainPath)) {
        errors.push(`Main entry point "${manifest.main}" not found at ${mainPath}`);
    }
    return {
        success: errors.length === 0,
        extensionPath: extDir,
        errors,
    };
}
/** Remove a directory tree, ignoring errors. */
function cleanDir(dir) {
    try {
        if (fs.existsSync(dir)) {
            fs.rmSync(dir, { recursive: true, force: true });
        }
    }
    catch {
        // Best effort — leftover dirs are harmless
    }
}
/** Recursively copy a directory's contents. */
function copyDirRecursive(src, dest) {
    fs.mkdirSync(dest, { recursive: true });
    const entries = fs.readdirSync(src, { withFileTypes: true });
    for (const entry of entries) {
        const srcPath = path.join(src, entry.name);
        const destPath = path.join(dest, entry.name);
        if (entry.isDirectory()) {
            copyDirRecursive(srcPath, destPath);
        }
        else {
            fs.copyFileSync(srcPath, destPath);
        }
    }
}
//# sourceMappingURL=stage-install.js.map