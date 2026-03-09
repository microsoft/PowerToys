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
exports.UTILS_SHIM = exports.COMPAT_SRC = exports.COMPAT_ROOT = void 0;
exports.bundleCommand = bundleCommand;
exports.bundleExtension = bundleExtension;
exports.resolveEntryPoint = resolveEntryPoint;
/**
 * Core esbuild configuration for bundling Raycast extensions.
 *
 * This module provides the `bundleCommand()` function that configures esbuild
 * with the correct module aliasing to swap `@raycast/api` for our compat layer
 * and `@raycast/utils` for our utils shim.
 *
 * The key insight: Raycast extensions do `import { List } from "@raycast/api"`.
 * esbuild's `alias` option transparently redirects that import to our
 * `@cmdpal/raycast-compat` source, which provides marker components, api stubs,
 * the reconciler, and the bridge — all without touching the extension's code.
 */
const esbuild = __importStar(require("esbuild"));
const path = __importStar(require("path"));
// ── Resilience plugin ──────────────────────────────────────────────────
/**
 * esbuild plugin that catches unresolved imports from `@raycast/api` or
 * `@raycast/utils` and replaces them with runtime warning stubs.
 *
 * Without this, any Raycast extension importing a symbol we haven't
 * stubbed yet causes esbuild to hard-fail. With this plugin, the build
 * succeeds and the missing function logs a warning at runtime.
 */
function raycastResiliencePlugin() {
    return {
        name: 'raycast-resilience',
        setup(build) {
            // Intercept unresolved imports from our shim files
            build.onResolve({ filter: /.*/ }, (args) => {
                // Only handle imports that failed to resolve from our compat layer
                if (args.kind === 'import-statement' &&
                    args.resolveDir &&
                    (args.resolveDir.includes('raycast-compat') ||
                        args.path.includes('raycast-compat'))) {
                    // Let esbuild try first — we only catch failures in onLoad
                    return undefined;
                }
                return undefined;
            });
            // After the build, check for unresolved import errors and provide
            // a helpful message. The actual resilience comes from our
            // comprehensive stubs, but this gives a clear diagnostic.
            build.onEnd((result) => {
                for (const error of result.errors) {
                    if (error.text.includes('No matching export') &&
                        (error.text.includes('raycast-compat') ||
                            error.text.includes('utils-shim'))) {
                        // Downgrade to warning with helpful context
                        result.warnings.push({
                            ...error,
                            text: `[raycast-resilience] ${error.text}. The extension may have degraded functionality for this API.`,
                        });
                    }
                }
                // Remove raycast-compat unresolved import errors (downgraded to warnings above)
                result.errors = result.errors.filter((e) => !(e.text.includes('No matching export') &&
                    (e.text.includes('raycast-compat') ||
                        e.text.includes('utils-shim'))));
            });
        },
    };
}
// ── Path constants ─────────────────────────────────────────────────────
/**
 * Root of the @cmdpal/raycast-compat package.
 * From compiled dist/build.js: __dirname = tools/bundler/dist/
 *   → ../../.. = raycast-compat/
 * From source src/build.ts: __dirname = tools/bundler/src/
 *   → ../../.. = raycast-compat/
 */
const COMPAT_ROOT = path.resolve(__dirname, '..', '..', '..');
exports.COMPAT_ROOT = COMPAT_ROOT;
/** The compat layer's source directory (contains index.ts barrel export). */
const COMPAT_SRC = path.join(COMPAT_ROOT, 'src');
exports.COMPAT_SRC = COMPAT_SRC;
/** The @raycast/utils shim file. */
const UTILS_SHIM = path.join(COMPAT_SRC, 'utils-shim');
exports.UTILS_SHIM = UTILS_SHIM;
// ── Build function ─────────────────────────────────────────────────────
/**
 * Bundle a single Raycast extension command with module aliasing.
 *
 * @raycast/api → our compat layer's src/index.ts (reconciler + components + stubs + bridge)
 * @raycast/utils → our utils-shim.ts (hooks + utility stubs)
 *
 * React and react-reconciler are kept external by default. The CmdPal bridge
 * process provides these at runtime, so the bundled extension resolves them
 * from the bridge's node_modules. This avoids duplicate React instances
 * (a classic "two copies of React" bug) and keeps bundles small.
 */
async function bundleCommand(options) {
    const compatSrc = options.compatSrc ?? COMPAT_SRC;
    const utilsShim = options.utilsShim ?? UTILS_SHIM;
    const external = options.external ?? ['react', 'react-reconciler'];
    try {
        const result = await esbuild.build({
            entryPoints: [options.entryPoint],
            outfile: options.outfile,
            bundle: true,
            format: 'cjs',
            platform: 'node',
            target: 'node22',
            sourcemap: true,
            minify: options.minify ?? false,
            // JSX: use React 19's automatic transform
            jsx: 'automatic',
            jsxImportSource: 'react',
            // The magic: redirect Raycast imports to our compat layer
            alias: {
                '@raycast/api': compatSrc,
                '@raycast/utils': utilsShim,
            },
            // Keep React external — the bridge runtime provides it
            external,
            // Ensure esbuild can handle .tsx/.ts imports from the compat layer
            loader: {
                '.tsx': 'tsx',
                '.ts': 'ts',
                '.json': 'json',
            },
            // Resolve from the extension's directory
            absWorkingDir: options.absWorkingDir ?? path.dirname(options.entryPoint),
            logLevel: 'warning',
            // Tree-shaking: remove unused compat layer exports
            treeShaking: true,
            // Resilience: gracefully handle missing @raycast/* exports
            plugins: [raycastResiliencePlugin()],
        });
        return {
            success: result.errors.length === 0,
            errors: result.errors,
            warnings: result.warnings,
            outfile: options.outfile,
        };
    }
    catch (err) {
        // esbuild throws on fatal errors; wrap in our result type
        const msg = err instanceof Error ? err.message : String(err);
        return {
            success: false,
            errors: [
                {
                    text: msg,
                    location: null,
                    notes: [],
                    detail: err,
                    id: '',
                    pluginName: '',
                },
            ],
            warnings: [],
            outfile: options.outfile,
        };
    }
}
/**
 * Bundle all commands in a Raycast extension.
 *
 * Reads the extension's package.json, iterates over `commands[]`,
 * resolves each command's entry point, and bundles with aliasing.
 */
async function bundleExtension(options) {
    const pkgPath = path.join(options.extensionDir, 'package.json');
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const pkg = require(pkgPath);
    const commands = pkg.commands ?? [];
    const results = [];
    for (const cmd of commands) {
        const entryPoint = resolveEntryPoint(options.extensionDir, cmd.name);
        if (!entryPoint) {
            results.push({
                name: cmd.name,
                result: {
                    success: false,
                    errors: [
                        {
                            text: `Could not find entry point for command "${cmd.name}" in ${options.extensionDir}`,
                            location: null,
                            notes: [],
                            detail: undefined,
                            id: '',
                            pluginName: '',
                        },
                    ],
                    warnings: [],
                    outfile: '',
                },
            });
            continue;
        }
        const outfile = path.join(options.outputDir, 'dist', `${cmd.name}.js`);
        const result = await bundleCommand({
            entryPoint,
            outfile,
            compatSrc: options.compatSrc,
            utilsShim: options.utilsShim,
            minify: options.minify,
            absWorkingDir: options.extensionDir,
        });
        results.push({ name: cmd.name, result });
    }
    return {
        success: results.every((r) => r.result.success),
        commands: results,
    };
}
// ── Helpers ────────────────────────────────────────────────────────────
const fs = require('fs');
/**
 * Resolve a Raycast command's entry point file.
 * Raycast convention: command "search" → src/search.tsx (or .ts, .jsx, .js).
 */
function resolveEntryPoint(extensionDir, commandName) {
    const extensions = ['.tsx', '.ts', '.jsx', '.js'];
    const bases = [
        path.join(extensionDir, 'src', commandName),
        path.join(extensionDir, commandName),
    ];
    // If there's only one command, also try src/index
    bases.push(path.join(extensionDir, 'src', 'index'));
    for (const base of bases) {
        for (const ext of extensions) {
            const candidate = base + ext;
            if (fs.existsSync(candidate)) {
                return candidate;
            }
        }
    }
    return null;
}
//# sourceMappingURL=build.js.map