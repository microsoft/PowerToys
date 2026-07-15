// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ContextItem, ICommand, ICommandItem, IconInfo } from '../types.js';

export interface CommandItemOptions {
  command: ICommand;
  title: string;
  subtitle?: string;
  icon?: IconInfo | null;
  moreCommands?: ContextItem[];
}

/** A concrete, ready-to-use {@link ICommandItem} built from a plain options bag. */
export class CommandItemBase implements ICommandItem {
  command: ICommand;
  title: string;
  subtitle?: string;
  icon?: IconInfo | null;
  moreCommands?: ContextItem[];

  constructor(options: CommandItemOptions) {
    this.command = options.command;
    this.title = options.title;
    this.subtitle = options.subtitle;
    this.icon = options.icon ?? null;
    this.moreCommands = options.moreCommands;
  }
}
