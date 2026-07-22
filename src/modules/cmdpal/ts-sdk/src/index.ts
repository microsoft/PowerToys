// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * `@microsoft/cmdpal-sdk`
 *
 * TypeScript SDK for building PowerToys Command Palette extensions that run as
 * isolated Node.js processes and talk to the host over JSON-RPC 2.0 via stdio.
 */

// Type surface.
export type * from './types.js';

// Icon helpers.
export { iconFromGlyph, iconFromBase64, iconFromFile, iconFromUrl } from './helpers.js';

// Base classes.
export { CommandProviderBase } from './base/CommandProviderBase.js';
export { ListPageBase } from './base/ListPageBase.js';
export { DynamicListPageBase } from './base/DynamicListPageBase.js';
export { ContentPageBase } from './base/ContentPageBase.js';
export { InvokableCommandBase } from './base/InvokableCommandBase.js';
export { CommandItemBase, type CommandItemOptions } from './base/CommandItemBase.js';
export { ListItemBase, type ListItemOptions } from './base/ListItemBase.js';
export { FallbackCommandItemBase } from './base/FallbackCommandItemBase.js';
export { Separator } from './base/Separator.js';

// Settings.
export {
  Settings,
  ToggleSetting,
  TextSetting,
  ChoiceSetSetting,
  JsonSettingsStore,
  type AnySetting,
  type SettingChoice,
  type SettingsChangedHandler,
} from './base/Settings.js';

// Built-in commands.
export {
  NoOpCommand,
  OpenUrlCommand,
  CopyTextCommand,
  ConfirmableCommand,
  type ConfirmableCommandOptions,
} from './base/commands.js';

// Runtime.
export { ExtensionHost } from './runtime/ExtensionHost.js';
export {
  startJsonRpcServer,
  run,
  activate,
  sendNotification,
  type ProviderFactory,
} from './runtime/server.js';
export { PROTOCOL_VERSION, getSdkVersion, isProtocolCompatible } from './runtime/protocol.js';
export { InvalidCommandResultError } from './runtime/commandResult.js';
