// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * @microsoft/cmdpal-sdk
 *
 * TypeScript SDK for building Command Palette extensions.
 * Provides base classes, type definitions, and JSONRPC runtime bridge.
 */

// Type definitions (interfaces and enums)
export type {
  IconData,
  IconInfo,
  Color,
  OptionalColor,
  Tag,
  KeyChord,
  Details,
  DetailsElement,
  DetailsData,
  DetailsTags,
  DetailsLink,
  DetailsCommands,
  DetailsSeparator,
  Filter,
  Filters,
  GridProperties,
  CommandResult,
  CommandResultArgs,
  GoToPageArgs,
  ToastArgs,
  ConfirmationArgs,
  ContextItem,
  Content,
  MarkdownContent,
  FormContent,
  TreeContent,
  PlainTextContent,
  ImageContent,
  ProgressState,
  StatusMessage,
  ActivationContext,
  ICommand,
  ICommandItem,
  IListItem,
  ICommandProvider,
  ICommandSettings,
} from './types';

export {
  type CommandResultKind,
  type NavigationMode,
  type MessageState,
  type StatusContext,
  type ContentType,
  type GridLayoutType,
  type FontFamily,
} from './types';

// Base classes
export { CommandProviderBase } from './base/CommandProviderBase';
export { ListPageBase } from './base/ListPageBase';
export { DynamicListPageBase } from './base/DynamicListPageBase';
export { ContentPageBase } from './base/ContentPageBase';
export { InvokableCommandBase } from './base/InvokableCommandBase';
export { CommandItemBase } from './base/CommandItemBase';
export { ListItemBase } from './base/ListItemBase';

// Runtime
export { ExtensionHost } from './runtime/ExtensionHost';
export { activate } from './runtime/activate';
export { startJsonRpcServer, sendNotification } from './runtime/stdio-server';
