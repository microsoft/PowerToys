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
import * as esbuild from 'esbuild';
/**
 * Root of the @cmdpal/raycast-compat package.
 * From compiled dist/build.js: __dirname = tools/bundler/dist/
 *   → ../../.. = raycast-compat/
 * From source src/build.ts: __dirname = tools/bundler/src/
 *   → ../../.. = raycast-compat/
 */
declare const COMPAT_ROOT: string;
/** The compat layer's source directory (contains index.ts barrel export). */
declare const COMPAT_SRC: string;
/** The @raycast/utils shim file. */
declare const UTILS_SHIM: string;
export interface BundleCommandOptions {
    /** Absolute path to the command's entry point (e.g., src/search.tsx). */
    entryPoint: string;
    /** Absolute path to the output JS file (e.g., dist/search.js). */
    outfile: string;
    /** Override the compat layer source directory (for testing). */
    compatSrc?: string;
    /** Override the utils shim path (for testing). */
    utilsShim?: string;
    /** Packages to mark as external. Defaults to ['react', 'react-reconciler']. */
    external?: string[];
    /** Enable minification. Default: false. */
    minify?: boolean;
    /** Working directory for resolving relative imports. Defaults to entryPoint's dir. */
    absWorkingDir?: string;
}
export interface BundleResult {
    /** Whether the build succeeded without errors. */
    success: boolean;
    /** esbuild error messages, if any. */
    errors: esbuild.Message[];
    /** esbuild warning messages, if any. */
    warnings: esbuild.Message[];
    /** Absolute path to the output file. */
    outfile: string;
}
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
export declare function bundleCommand(options: BundleCommandOptions): Promise<BundleResult>;
export interface RaycastCommand {
    name: string;
    title?: string;
    mode?: string;
}
export interface BundleExtensionOptions {
    /** Path to the Raycast extension root (contains package.json). */
    extensionDir: string;
    /** Output directory for bundled files. */
    outputDir: string;
    /** Override compat source for testing. */
    compatSrc?: string;
    /** Override utils shim for testing. */
    utilsShim?: string;
    /** Enable minification. */
    minify?: boolean;
}
export interface BundleExtensionResult {
    /** Whether all commands bundled successfully. */
    success: boolean;
    /** Per-command results. */
    commands: Array<{
        name: string;
        result: BundleResult;
    }>;
}
/**
 * Bundle all commands in a Raycast extension.
 *
 * Reads the extension's package.json, iterates over `commands[]`,
 * resolves each command's entry point, and bundles with aliasing.
 */
export declare function bundleExtension(options: BundleExtensionOptions): Promise<BundleExtensionResult>;
/**
 * Resolve a Raycast command's entry point file.
 * Raycast convention: command "search" → src/search.tsx (or .ts, .jsx, .js).
 */
declare function resolveEntryPoint(extensionDir: string, commandName: string): string | null;
/** Exported for testing. */
export { resolveEntryPoint, COMPAT_ROOT, COMPAT_SRC, UTILS_SHIM };
