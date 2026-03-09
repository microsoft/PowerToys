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
exports.listInstalledExtensions = listInstalledExtensions;
exports.uninstallExtension = uninstallExtension;
/**
 * Extension management — list installed and uninstall Raycast-compat extensions.
 *
 * Raycast-compat extensions are identified by the presence of a
 * `raycast-compat.json` file with `installedBy: "raycast-pipeline"`.
 */
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
const stage_install_1 = require("./stage-install");
/**
 * List all Raycast-compat extensions installed in the CmdPal JSExtensions dir.
 *
 * Scans each subdirectory for both cmdpal.json and raycast-compat.json.
 * Only returns extensions that have the `installedBy: "raycast-pipeline"` marker.
 */
function listInstalledExtensions(installDir) {
    const baseDir = installDir ?? (0, stage_install_1.getDefaultInstallDir)();
    if (!fs.existsSync(baseDir)) {
        return [];
    }
    const extensions = [];
    const entries = fs.readdirSync(baseDir, { withFileTypes: true });
    for (const entry of entries) {
        if (!entry.isDirectory())
            continue;
        const extDir = path.join(baseDir, entry.name);
        const manifestPath = path.join(extDir, 'cmdpal.json');
        const compatPath = path.join(extDir, 'raycast-compat.json');
        // Must have both cmdpal.json and raycast-compat.json
        if (!fs.existsSync(manifestPath) || !fs.existsSync(compatPath)) {
            continue;
        }
        try {
            const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf-8'));
            const compat = JSON.parse(fs.readFileSync(compatPath, 'utf-8'));
            // Only include extensions installed by the pipeline
            if (compat.installedBy !== 'raycast-pipeline') {
                continue;
            }
            extensions.push({
                name: manifest.name,
                raycastName: compat.raycastOriginalName ?? entry.name,
                displayName: manifest.displayName ?? manifest.name,
                version: manifest.version ?? 'unknown',
                path: extDir,
            });
        }
        catch {
            // Skip directories with invalid manifests
            continue;
        }
    }
    return extensions;
}
/**
 * Uninstall a Raycast-compat extension from CmdPal.
 *
 * Removes the entire extension directory. Accepts either the CmdPal name
 * (prefixed with "raycast-") or the original Raycast extension name.
 *
 * @returns true if the extension was found and removed
 */
function uninstallExtension(extensionName, installDir) {
    const baseDir = installDir ?? (0, stage_install_1.getDefaultInstallDir)();
    // Normalize: try with and without the "raycast-" prefix
    const candidates = [
        extensionName,
        extensionName.startsWith('raycast-')
            ? extensionName
            : `raycast-${extensionName}`,
    ];
    // Also check installed extensions to match by raycast original name
    const installed = listInstalledExtensions(baseDir);
    for (const ext of installed) {
        if (candidates.includes(ext.name) ||
            candidates.includes(ext.raycastName)) {
            try {
                fs.rmSync(ext.path, { recursive: true, force: true });
                return true;
            }
            catch {
                return false;
            }
        }
    }
    // Fallback: try direct directory removal
    for (const name of candidates) {
        const extDir = path.join(baseDir, name);
        if (fs.existsSync(extDir)) {
            try {
                fs.rmSync(extDir, { recursive: true, force: true });
                return true;
            }
            catch {
                return false;
            }
        }
    }
    return false;
}
//# sourceMappingURL=manage.js.map