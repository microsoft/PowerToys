// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ContextItem, Details, ICommand, IListItem, IconInfo, Tag } from '../types.js';

export interface ListItemOptions {
  command: ICommand;
  title: string;
  subtitle?: string;
  icon?: IconInfo | null;
  moreCommands?: ContextItem[];
  tags?: Tag[];
  details?: Details;
  section?: string;
  textToSuggest?: string;
}

/** A concrete, ready-to-use {@link IListItem} built from a plain options bag. */
export class ListItemBase implements IListItem {
  command: ICommand;
  title: string;
  subtitle?: string;
  icon?: IconInfo | null;
  moreCommands?: ContextItem[];
  tags?: Tag[];
  details?: Details;
  section?: string;
  textToSuggest?: string;

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
