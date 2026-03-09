// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Pipeline orchestrator — ties together all stages into a single
 * "install Raycast extension" flow.
 *
 * Stages: download → validate → dependencies → build → install → cleanup
 *
 * Each stage is timed and its result recorded. On failure, subsequent stages
 * are skipped and the temp directory is cleaned up.
 */

import type { PipelineOptions, PipelineResult, StageResult } from './types';
import { downloadExtension } from './stage-download';
import { validateExtension } from './stage-validate';
import { installDependencies } from './stage-dependencies';
import { buildExtension } from './stage-build';
import { installExtension, getDefaultInstallDir } from './stage-install';
import { cleanup } from './stage-cleanup';

/**
 * Run the full Raycast → CmdPal install pipeline.
 *
 * Downloads a Raycast extension from GitHub, validates it for Windows support,
 * installs npm dependencies, bundles with esbuild (aliasing @raycast/api to
 * the compat layer), then copies the built output to the CmdPal JSExtensions
 * directory.
 */
export async function installRaycastExtension(
  options: PipelineOptions,
): Promise<PipelineResult> {
  const stages: StageResult[] = [];
  const progress = options.onProgress ?? (() => {});
  const installDir = options.outputDir ?? getDefaultInstallDir();

  let tempDir: string | undefined;
  let buildOutputDir: string | undefined;

  // ── Stage 1: Download ──────────────────────────────────────────────
  progress('download', `Downloading "${options.extensionName}" from GitHub...`);
  const downloadStage = await runStage('download', async () => {
    const result = await downloadExtension(
      options.extensionName,
      options.githubToken,
    );
    tempDir = result.tempDir;
    return `Downloaded ${result.files.length} files to ${tempDir}`;
  });
  stages.push(downloadStage);

  if (downloadStage.status === 'failed') {
    cleanup(tempDir ?? '');
    return fail(stages, downloadStage.detail ?? 'Download failed');
  }

  // ── Stage 2: Validate ──────────────────────────────────────────────
  progress('validate', 'Validating extension...');
  const validateStage = await runStage('validate', async () => {
    const result = validateExtension(tempDir!);
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
    cleanup(tempDir!);
    return fail(stages, validateStage.detail ?? 'Validation failed');
  }

  // ── Stage 3: Dependencies ──────────────────────────────────────────
  progress('dependencies', 'Installing npm dependencies...');
  const depsStage = await runStage('dependencies', async () => {
    const result = await installDependencies(tempDir!);
    if (!result.success) {
      throw new Error(`npm install failed: ${result.stderr}`);
    }
    return 'Dependencies installed';
  });
  stages.push(depsStage);

  if (depsStage.status === 'failed') {
    cleanup(tempDir!);
    return fail(stages, depsStage.detail ?? 'Dependency installation failed');
  }

  // ── Stage 4: Build ─────────────────────────────────────────────────
  progress('build', 'Bundling extension with esbuild...');

  // Build output goes to a staging dir inside the temp dir
  const { mkdtempSync } = await import('fs');
  const { join } = await import('path');
  const os = await import('os');
  buildOutputDir = mkdtempSync(
    join(os.tmpdir(), `raycast-build-${options.extensionName}-`),
  );

  const buildStage = await runStage('build', async () => {
    const result = await buildExtension(tempDir!, buildOutputDir!);
    if (!result.success) {
      throw new Error(result.errors.join('; '));
    }
    return `Built ${result.bundleResult.commands.length} command(s)`;
  });
  stages.push(buildStage);

  if (buildStage.status === 'failed') {
    cleanup(tempDir!);
    cleanup(buildOutputDir);
    return fail(stages, buildStage.detail ?? 'Build failed');
  }

  // ── Stage 5: Install ───────────────────────────────────────────────
  progress('install', `Installing to ${installDir}...`);
  let extensionPath = '';
  const installStage = await runStage('install', async () => {
    const result = installExtension(buildOutputDir!, installDir);
    if (!result.success) {
      throw new Error(result.errors.join('; '));
    }
    extensionPath = result.extensionPath;
    return `Installed to ${result.extensionPath}`;
  });
  stages.push(installStage);

  if (installStage.status === 'failed') {
    cleanup(tempDir!);
    cleanup(buildOutputDir);
    return fail(stages, installStage.detail ?? 'Installation failed');
  }

  // ── Stage 6: Cleanup ───────────────────────────────────────────────
  progress('cleanup', 'Cleaning up temp files...');
  const cleanupStage = await runStage('cleanup', async () => {
    cleanup(tempDir!);
    cleanup(buildOutputDir!);
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
async function runStage(
  name: string,
  fn: () => Promise<string>,
): Promise<StageResult> {
  const start = Date.now();
  try {
    const detail = await fn();
    return {
      name,
      status: 'success',
      duration: Date.now() - start,
      detail,
    };
  } catch (err: unknown) {
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
function fail(stages: StageResult[], error: string): PipelineResult {
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
