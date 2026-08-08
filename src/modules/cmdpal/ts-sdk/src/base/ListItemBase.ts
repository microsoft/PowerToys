// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ContextItem, Details, ICommand, IListItem, IconInfo, Tag } from '../types.js';

/** Options for constructing a {@link ListItemBase}. */
export interface ListItemOptions {
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
  /** Colored labels shown on the item. */
  tags?: Tag[];
  /** Rich content shown in the details panel when the item is selected. */
  details?: Details;
  /** Section header text used to visually group items. */
  section?: string;
  /** Text placed in the search box when the item is selected. */
  textToSuggest?: string;
}

/** A concrete, ready-to-use {@link IListItem} built from a plain options bag. */
export class ListItemBase implements IListItem {
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
  /** Colored labels shown on the item. */
  tags?: Tag[];
  /** Rich content shown in the details panel when the item is selected. */
  details?: Details;
  /** Section header text used to visually group items. */
  section?: string;
  /** Text placed in the search box when the item is selected. */
  textToSuggest?: string;

  /**
   * Creates a list item from the given options.
   *
   * @param options Values copied onto the new item.
   */
  constructor(options: ListItemOptions) {
    this.command = options.command;
    this.title = options.title;
    this.subtitle = options.subtitle;
    this.icon = options.icon ?? null;
    this.moreCommands = options.moreCommands;
    this.tags = options.tags;
    this.details = options.details;
    this.section = options.section;
    this.textToSuggest = options.textToSuggest;
  }
}
