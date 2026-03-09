// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Shared types for the Raycast → CmdPal install pipeline.
 */

/** Options for running the install pipeline. */
export interface PipelineOptions {
  /** Extension name from the raycast/extensions repository. */
  extensionName: string;
  /** Override the install directory (defaults to CmdPal JSExtensions path). */
  outputDir?: string;
  /** GitHub personal access token for higher rate limits. */
  githubToken?: string;
  /** Progress callback invoked at each pipeline stage. */
  onProgress?: (stage: string, detail: string) => void;
}

/** Result of a completed pipeline run. */
export interface PipelineResult {
  /** Whether all stages completed successfully. */
  success: boolean;
  /** Absolute path where the extension was installed (on success). */
  extensionPath?: string;
  /** Human-readable error message (on failure). */
  error?: string;
  /** Per-stage status and timing information. */
  stages: StageResult[];
}

/** Result of a single pipeline stage. */
export interface StageResult {
  /** Stage name (download, validate, dependencies, build, install, cleanup). */
  name: string;
  /** Whether the stage succeeded, failed, or was skipped. */
  status: 'success' | 'failed' | 'skipped';
  /** Wall-clock duration in milliseconds. */
  duration: number;
  /** Optional detail message (error reason, etc.). */
  detail?: string;
}

/** Information about an installed Raycast-compat extension. */
export interface InstalledExtension {
  /** CmdPal extension name (prefixed with "raycast-"). */
  name: string;
  /** The original Raycast extension name. */
  raycastName: string;
  /** Display name from the manifest. */
  displayName: string;
  /** Version string. */
  version: string;
  /** Absolute path to the extension directory. */
  path: string;
}

/** A CmdPal manifest (subset for pipeline use). */
export interface CmdPalManifest {
  name: string;
  displayName?: string;
  version?: string;
  description?: string;
  icon?: string;
  main: string;
  publisher?: string;
  debug?: boolean;
  debugPort?: number;
  engines?: { node?: string };
  capabilities?: string[];
}
