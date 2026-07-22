// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ContextItem, ICommand, ICommandItem, IconInfo } from '../types.js';

/** Options for constructing a {@link CommandItemBase}. */
export interface CommandItemOptions {
  /** The command run when the item is activated. */
  command: ICommand;
  /** Primary text shown for the item. */
  title: string;
  /** Secondary text shown below the title. */
  subtitle?: string;
  /** Icon shown next to the item. Falls back to the command's icon when unset. */
  icon?: IconInfo | null;
  /** Right-click / overflow menu actions. */
  moreCommands?: ContextItem[];
}

/** A concrete, ready-to-use {@link ICommandItem} built from a plain options bag. */
export class CommandItemBase implements ICommandItem {
  /** The command run when the item is activated. */
  command: ICommand;
  /** Primary text shown for the item. */
  title: string;
  /** Secondary text shown below the title. */
  subtitle?: string;
  /** Icon shown next to the item. */
  icon?: IconInfo | null;
  /** Right-click / overflow menu actions. */
  moreCommands?: ContextItem[];

  /**
   * Creates a command item from the given options.
   *
   * @param options Values copied onto the new item.
   */
  constructor(options: CommandItemOptions) {
    this.command = options.command;
    this.title = options.title;
    this.subtitle = options.subtitle;
    this.icon = options.icon ?? null;
    this.moreCommands = options.moreCommands;
  }
}
