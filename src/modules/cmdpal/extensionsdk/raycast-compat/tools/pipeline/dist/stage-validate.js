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
exports.validateExtension = validateExtension;
/**
 * Stage 2: Validate — Checks the downloaded Raycast extension for
 * Windows compatibility and required fields before proceeding.
 */
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
/**
 * Validate a downloaded Raycast extension for CmdPal compatibility.
 *
 * Checks:
 * 1. package.json exists and is parseable
 * 2. Required fields present (name, title, commands)
 * 3. Platform includes "Windows" (Raycast defaults to macOS-only)
 * 4. At least one command entry
 */
function validateExtension(extensionDir) {
    const errors = [];
    const warnings = [];
    // 1. Check package.json exists
    const pkgPath = path.join(extensionDir, 'package.json');
    if (!fs.existsSync(pkgPath)) {
        return {
            valid: false,
            errors: ['package.json not found in extension directory'],
            warnings,
        };
    }
    // 2. Parse package.json
    let manifest;
    try {
        const raw = fs.readFileSync(pkgPath, 'utf-8');
        manifest = JSON.parse(raw);
    }
    catch {
        return {
            valid: false,
            errors: ['Failed to parse package.json — invalid JSON'],
            warnings,
        };
    }
    // 3. Required fields
    if (!manifest.name || manifest.name.trim() === '') {
        errors.push("Missing required field: 'name'");
    }
    if (!manifest.title || manifest.title.trim() === '') {
        errors.push("Missing required field: 'title'");
    }
    if (!manifest.commands || manifest.commands.length === 0) {
        errors.push("Missing required field: 'commands' (must have at least one)");
    }
    // 4. Platform check — Raycast defaults to ["macOS"] when absent
    const platforms = manifest.platforms ?? ['macOS'];
    const hasWindows = platforms.some((p) => p.toLowerCase() === 'windows');
    if (!hasWindows) {
        errors.push(`Platform rejection: extension supports [${platforms.join(', ')}]. ` +
            "'Windows' is required.");
    }
    // 5. Validate individual commands
    if (manifest.commands) {
        for (let i = 0; i < manifest.commands.length; i++) {
            const cmd = manifest.commands[i];
            if (!cmd.name || cmd.name.trim() === '') {
                errors.push(`commands[${i}]: missing required field 'name'`);
            }
        }
    }
    // Warnings for optional but recommended fields
    if (!manifest.description) {
        warnings.push("Missing optional field: 'description'");
    }
    if (!manifest.author && !manifest.owner) {
        warnings.push("Missing optional field: 'author'");
    }
    if (!manifest.icon) {
        warnings.push("Missing optional field: 'icon'");
    }
    if (!manifest.version) {
        warnings.push("Missing optional field: 'version' — will default to 1.0.0");
    }
    return { valid: errors.length === 0, errors, warnings };
}
//# sourceMappingURL=stage-validate.js.map