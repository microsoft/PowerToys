/**
 * Pipeline orchestrator — ties together all stages into a single
 * "install Raycast extension" flow.
 *
 * Stages: download → validate → dependencies → build → install → cleanup
 *
 * Each stage is timed and its result recorded. On failure, subsequent stages
 * are skipped and the temp directory is cleaned up.
 */
import type { PipelineOptions, PipelineResult } from './types';
/**
 * Run the full Raycast → CmdPal install pipeline.
 *
 * Downloads a Raycast extension from GitHub, validates it for Windows support,
 * installs npm dependencies, bundles with esbuild (aliasing @raycast/api to
 * the compat layer), then copies the built output to the CmdPal JSExtensions
 * directory.
 */
export declare function installRaycastExtension(options: PipelineOptions): Promise<PipelineResult>;
//# sourceMappingURL=pipeline.d.ts.map