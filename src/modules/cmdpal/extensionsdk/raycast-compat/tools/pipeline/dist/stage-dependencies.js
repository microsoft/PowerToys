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
exports.installDependencies = installDependencies;
/**
 * Stage 3: Dependencies — Runs `npm install` in the downloaded extension
 * directory to install its dependencies before bundling.
 */
const child_process_1 = require("child_process");
const path = __importStar(require("path"));
const fs = __importStar(require("fs"));
/**
 * Install npm dependencies for a Raycast extension.
 *
 * Shells out to `npm install --production` to install only runtime deps.
 * Assumes Node.js and npm are on PATH (the CmdPal store gate verifies this
 * before allowing Raycast extension installs).
 */
function installDependencies(extensionDir) {
    return new Promise((resolve) => {
        // Verify package.json exists
        const pkgPath = path.join(extensionDir, 'package.json');
        if (!fs.existsSync(pkgPath)) {
            resolve({
                success: false,
                stdout: '',
                stderr: 'package.json not found — cannot install dependencies',
            });
            return;
        }
        // Use npm.cmd on Windows, npm elsewhere
        const npmCmd = process.platform === 'win32' ? 'npm.cmd' : 'npm';
        (0, child_process_1.execFile)(npmCmd, ['install', '--production', '--no-audit', '--no-fund'], {
            cwd: extensionDir,
            timeout: 120_000, // 2-minute timeout for npm install
            env: { ...process.env, NODE_ENV: 'production' },
            shell: true, // Required on Windows — .cmd files need cmd.exe to execute
        }, (error, stdout, stderr) => {
            if (error) {
                resolve({
                    success: false,
                    stdout: stdout ?? '',
                    stderr: stderr ?? error.message,
                });
                return;
            }
            resolve({
                success: true,
                stdout: stdout ?? '',
                stderr: stderr ?? '',
            });
        });
    });
}
//# sourceMappingURL=stage-dependencies.js.map