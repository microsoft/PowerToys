// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ICommandItem, ICommand, ContextItem, IconInfo } from '../types';

/**
 * Base class for command items displayed in lists and menus.
 */
export class CommandItemBase implements ICommandItem {
  command: ICommand;
  title: string;
  subtitle?: string;
  icon?: IconInfo | null;
  moreCommands?: ContextItem[];

  constructor(options: {
    command: ICommand;
    title: string;
    subtitle?: string;
    icon?: IconInfo | null;
    moreCommands?: ContextItem[];
  }) {
    this.command = options.command;
    this.title = options.title;
    this.subtitle = options.subtitle;
    this.icon = options.icon ?? null;
    this.moreCommands = options.moreCommands;
  }
}
