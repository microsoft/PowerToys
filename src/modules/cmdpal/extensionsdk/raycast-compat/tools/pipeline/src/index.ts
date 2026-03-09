// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Public API barrel export for the pipeline library.
 */

export { installRaycastExtension } from './pipeline';
export { listInstalledExtensions, uninstallExtension } from './manage';
export { getDefaultInstallDir } from './stage-install';
export type {
  PipelineOptions,
  PipelineResult,
  StageResult,
  InstalledExtension,
  CmdPalManifest,
} from './types';
