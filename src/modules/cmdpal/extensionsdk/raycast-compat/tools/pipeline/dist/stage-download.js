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
exports.downloadExtension = downloadExtension;
/**
 * Stage 1: Download — Fetches all source files for a Raycast extension
 * from the raycast/extensions GitHub repository into a temp directory.
 */
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
const os = __importStar(require("os"));
const raycast_github_client_1 = require("@cmdpal/raycast-github-client");
/**
 * Download a Raycast extension's source files to a temporary directory.
 *
 * Uses the GitHub client's `downloadExtension()` which walks the Git tree
 * and fetches blobs in parallel batches.
 */
async function downloadExtension(extensionName, githubToken) {
    const client = new raycast_github_client_1.RaycastExtensionsClient({
        token: githubToken,
    });
    // Fetch all files from the extension directory
    const extensionFiles = await client.downloadExtension(extensionName);
    if (extensionFiles.length === 0) {
        throw new Error(`No files found for extension "${extensionName}". ` +
            'Verify the name matches an entry in raycast/extensions.');
    }
    // Create a temp directory for the downloaded source
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), `raycast-${extensionName}-`));
    const writtenFiles = [];
    for (const file of extensionFiles) {
        const filePath = path.join(tempDir, file.path);
        const fileDir = path.dirname(filePath);
        fs.mkdirSync(fileDir, { recursive: true });
        fs.writeFileSync(filePath, file.content, 'utf-8');
        writtenFiles.push(file.path);
    }
    return { tempDir, files: writtenFiles };
}
//# sourceMappingURL=stage-download.js.map