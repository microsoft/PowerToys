// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast environment compatibility stub.
 *
 * Provides runtime environment information that Raycast extensions
 * read via `import { environment } from "@raycast/api"`.
 *
 * Values are populated from the CmdPal manifest and runtime context.
 * Call `_configureEnvironment()` during extension bootstrap to set
 * actual values; until then, safe defaults are used.
 */

import * as path from 'path';

export interface LaunchContext {
  [key: string]: unknown;
}

export enum LaunchType {
  UserInitiated = 'userInitiated',
  Background = 'background',
}

export interface EnvironmentConfig {
  extensionName?: string;
  commandName?: string;
  assetsPath?: string;
  supportPath?: string;
  extensionDir?: string;
  launchType?: LaunchType;
  launchContext?: LaunchContext;
}

const defaultBasePath = path.join(
  process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.',
  'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions',
);

// Mutable config — populated by _configureEnvironment()
const config: Required<EnvironmentConfig> = {
  extensionName: 'unknown',
  commandName: 'default',
  assetsPath: path.join(defaultBasePath, '_raycast-compat', 'assets'),
  supportPath: path.join(defaultBasePath, '_raycast-compat', 'data'),
  extensionDir: path.join(defaultBasePath, '_raycast-compat'),
  launchType: LaunchType.UserInitiated,
  launchContext: {},
};

/**
 * The environment object exposed to Raycast extensions.
 * Getters ensure values reflect any runtime reconfiguration.
 */
export const environment = {
  get extensionName(): string { return config.extensionName; },
  get commandName(): string { return config.commandName; },
  get assetsPath(): string { return config.assetsPath; },
  get supportPath(): string { return config.supportPath; },
  get extensionDir(): string { return config.extensionDir; },

  /** Always false in CmdPal — extensions run in production mode. */
  get isDevelopment(): boolean { return false; },

  /** Raycast API version compatibility — use latest known. */
  get raycastVersion(): string { return '1.80.0'; },

  get launchType(): LaunchType { return config.launchType; },
  get launchContext(): LaunchContext { return config.launchContext; },

  /**
   * Feature detection. Raycast uses this to check API capabilities.
   * We report true for the subset we support.
   */
  canAccess(api: unknown): boolean {
    // For now, all shimmed APIs are "accessible"
    if (api === undefined || api === null) return false;
    return true;
  },

  /** Raycast's textSize preference (we default to medium). */
  get textSize(): string { return 'medium'; },

  /** Raycast's appearance (we follow system). */
  get appearance(): string { return 'light'; },

  /** Raycast's theme string. */
  get theme(): string { return 'raycast-default'; },
};

/**
 * Bootstrap call — the compat runtime sets actual values from
 * the CmdPal manifest and runtime context before the extension runs.
 */
export function _configureEnvironment(overrides: EnvironmentConfig): void {
  if (overrides.extensionName !== undefined) config.extensionName = overrides.extensionName;
  if (overrides.commandName !== undefined) config.commandName = overrides.commandName;
  if (overrides.assetsPath !== undefined) config.assetsPath = overrides.assetsPath;
  if (overrides.supportPath !== undefined) config.supportPath = overrides.supportPath;
  if (overrides.extensionDir !== undefined) config.extensionDir = overrides.extensionDir;
  if (overrides.launchType !== undefined) config.launchType = overrides.launchType;
  if (overrides.launchContext !== undefined) config.launchContext = overrides.launchContext;
}
