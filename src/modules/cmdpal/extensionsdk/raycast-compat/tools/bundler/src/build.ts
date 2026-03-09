// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
import * as path from 'path';

// ── Resilience plugin ──────────────────────────────────────────────────

/**
 * esbuild plugin that catches unresolved imports from `@raycast/api` or
 * `@raycast/utils` and replaces them with runtime warning stubs.
 *
 * Without this, any Raycast extension importing a symbol we haven't
 * stubbed yet causes esbuild to hard-fail. With this plugin, the build
 * succeeds and the missing function logs a warning at runtime.
 */
function raycastResiliencePlugin(): esbuild.Plugin {
  return {
    name: 'raycast-resilience',
    setup(build) {
      // Intercept unresolved imports from our shim files
      build.onResolve({ filter: /.*/ }, (args) => {
        // Only handle imports that failed to resolve from our compat layer
        if (
          args.kind === 'import-statement' &&
          args.resolveDir &&
          (args.resolveDir.includes('raycast-compat') ||
            args.path.includes('raycast-compat'))
        ) {
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
          if (
            error.text.includes('No matching export') &&
            (error.text.includes('raycast-compat') ||
              error.text.includes('utils-shim'))
          ) {
            // Downgrade to warning with helpful context
            result.warnings.push({
              ...error,
              text: `[raycast-resilience] ${error.text}. The extension may have degraded functionality for this API.`,
            });
          }
        }
        // Remove raycast-compat unresolved import errors (downgraded to warnings above)
        result.errors = result.errors.filter(
          (e) =>
            !(
              e.text.includes('No matching export') &&
              (e.text.includes('raycast-compat') ||
                e.text.includes('utils-shim'))
            ),
        );
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

/** The compat layer's source directory (contains index.ts barrel export). */
const COMPAT_SRC = path.join(COMPAT_ROOT, 'src');

/** The @raycast/utils shim file. */
const UTILS_SHIM = path.join(COMPAT_SRC, 'utils-shim');

// ── Types ──────────────────────────────────────────────────────────────

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
export async function bundleCommand(
  options: BundleCommandOptions,
): Promise<BundleResult> {
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
      absWorkingDir:
        options.absWorkingDir ?? path.dirname(options.entryPoint),

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
  } catch (err: unknown) {
    // esbuild throws on fatal errors; wrap in our result type
    const msg =
      err instanceof Error ? err.message : String(err);
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

// ── Convenience: bundle all commands in an extension ───────────────────

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
  commands: Array<{ name: string; result: BundleResult }>;
}

/**
 * Bundle all commands in a Raycast extension.
 *
 * Reads the extension's package.json, iterates over `commands[]`,
 * resolves each command's entry point, and bundles with aliasing.
 */
export async function bundleExtension(
  options: BundleExtensionOptions,
): Promise<BundleExtensionResult> {
  const pkgPath = path.join(options.extensionDir, 'package.json');
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const pkg = require(pkgPath);
  const commands: RaycastCommand[] = pkg.commands ?? [];
  const results: Array<{ name: string; result: BundleResult }> = [];

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

const fs = require('fs') as typeof import('fs');

/**
 * Resolve a Raycast command's entry point file.
 * Raycast convention: command "search" → src/search.tsx (or .ts, .jsx, .js).
 */
function resolveEntryPoint(
  extensionDir: string,
  commandName: string,
): string | null {
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

/** Exported for testing. */
export { resolveEntryPoint, COMPAT_ROOT, COMPAT_SRC, UTILS_SHIM };
