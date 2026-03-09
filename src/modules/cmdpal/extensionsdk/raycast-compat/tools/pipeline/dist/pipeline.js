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
exports.installRaycastExtension = installRaycastExtension;
const stage_download_1 = require("./stage-download");
const stage_validate_1 = require("./stage-validate");
const stage_dependencies_1 = require("./stage-dependencies");
const stage_build_1 = require("./stage-build");
const stage_install_1 = require("./stage-install");
const stage_cleanup_1 = require("./stage-cleanup");
/**
 * Run the full Raycast → CmdPal install pipeline.
 *
 * Downloads a Raycast extension from GitHub, validates it for Windows support,
 * installs npm dependencies, bundles with esbuild (aliasing @raycast/api to
 * the compat layer), then copies the built output to the CmdPal JSExtensions
 * directory.
 */
async function installRaycastExtension(options) {
    const stages = [];
    const progress = options.onProgress ?? (() => { });
    const installDir = options.outputDir ?? (0, stage_install_1.getDefaultInstallDir)();
    let tempDir;
    let buildOutputDir;
    // ── Stage 1: Download ──────────────────────────────────────────────
    progress('download', `Downloading "${options.extensionName}" from GitHub...`);
    const downloadStage = await runStage('download', async () => {
        const result = await (0, stage_download_1.downloadExtension)(options.extensionName, options.githubToken);
        tempDir = result.tempDir;
        return `Downloaded ${result.files.length} files to ${tempDir}`;
    });
    stages.push(downloadStage);
    if (downloadStage.status === 'failed') {
        (0, stage_cleanup_1.cleanup)(tempDir ?? '');
        return fail(stages, downloadStage.detail ?? 'Download failed');
    }
    // ── Stage 2: Validate ──────────────────────────────────────────────
    progress('validate', 'Validating extension...');
    const validateStage = await runStage('validate', async () => {
        const result = (0, stage_validate_1.validateExtension)(tempDir);
        if (!result.valid) {
            throw new Error(result.errors.join('; '));
        }
        if (result.warnings.length > 0) {
            return `Validated with warnings: ${result.warnings.join('; ')}`;
        }
        return 'Validation passed';
    });
    stages.push(validateStage);
    if (validateStage.status === 'failed') {
        (0, stage_cleanup_1.cleanup)(tempDir);
        return fail(stages, validateStage.detail ?? 'Validation failed');
    }
    // ── Stage 3: Dependencies ──────────────────────────────────────────
    progress('dependencies', 'Installing npm dependencies...');
    const depsStage = await runStage('dependencies', async () => {
        const result = await (0, stage_dependencies_1.installDependencies)(tempDir);
        if (!result.success) {
            throw new Error(`npm install failed: ${result.stderr}`);
        }
        return 'Dependencies installed';
    });
    stages.push(depsStage);
    if (depsStage.status === 'failed') {
        (0, stage_cleanup_1.cleanup)(tempDir);
        return fail(stages, depsStage.detail ?? 'Dependency installation failed');
    }
    // ── Stage 4: Build ─────────────────────────────────────────────────
    progress('build', 'Bundling extension with esbuild...');
    // Build output goes to a staging dir inside the temp dir
    const { mkdtempSync } = await Promise.resolve().then(() => __importStar(require('fs')));
    const { join } = await Promise.resolve().then(() => __importStar(require('path')));
    const os = await Promise.resolve().then(() => __importStar(require('os')));
    buildOutputDir = mkdtempSync(join(os.tmpdir(), `raycast-build-${options.extensionName}-`));
    const buildStage = await runStage('build', async () => {
        const result = await (0, stage_build_1.buildExtension)(tempDir, buildOutputDir);
        if (!result.success) {
            throw new Error(result.errors.join('; '));
        }
        return `Built ${result.bundleResult.commands.length} command(s)`;
    });
    stages.push(buildStage);
    if (buildStage.status === 'failed') {
        (0, stage_cleanup_1.cleanup)(tempDir);
        (0, stage_cleanup_1.cleanup)(buildOutputDir);
        return fail(stages, buildStage.detail ?? 'Build failed');
    }
    // ── Stage 5: Install ───────────────────────────────────────────────
    progress('install', `Installing to ${installDir}...`);
    let extensionPath = '';
    const installStage = await runStage('install', async () => {
        const result = (0, stage_install_1.installExtension)(buildOutputDir, installDir);
        if (!result.success) {
            throw new Error(result.errors.join('; '));
        }
        extensionPath = result.extensionPath;
        return `Installed to ${result.extensionPath}`;
    });
    stages.push(installStage);
    if (installStage.status === 'failed') {
        (0, stage_cleanup_1.cleanup)(tempDir);
        (0, stage_cleanup_1.cleanup)(buildOutputDir);
        return fail(stages, installStage.detail ?? 'Installation failed');
    }
    // ── Stage 6: Cleanup ───────────────────────────────────────────────
    progress('cleanup', 'Cleaning up temp files...');
    const cleanupStage = await runStage('cleanup', async () => {
        (0, stage_cleanup_1.cleanup)(tempDir);
        (0, stage_cleanup_1.cleanup)(buildOutputDir);
        return 'Temp directories removed';
    });
    stages.push(cleanupStage);
    // Cleanup failure is non-fatal
    progress('complete', `Extension installed at ${extensionPath}`);
    return {
        success: true,
        extensionPath,
        stages,
    };
}
// ── Helpers ────────────────────────────────────────────────────────────
/** Execute a pipeline stage, capturing timing and errors. */
async function runStage(name, fn) {
    const start = Date.now();
    try {
        const detail = await fn();
        return {
            name,
            status: 'success',
            duration: Date.now() - start,
            detail,
        };
    }
    catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        return {
            name,
            status: 'failed',
            duration: Date.now() - start,
            detail: message,
        };
    }
}
/** Create a failed pipeline result. */
function fail(stages, error) {
    // Mark any remaining expected stages as skipped
    const stageNames = [
        'download',
        'validate',
        'dependencies',
        'build',
        'install',
        'cleanup',
    ];
    const completed = new Set(stages.map((s) => s.name));
    for (const name of stageNames) {
        if (!completed.has(name)) {
            stages.push({ name, status: 'skipped', duration: 0 });
        }
    }
    return { success: false, error, stages };
}
//# sourceMappingURL=pipeline.js.map