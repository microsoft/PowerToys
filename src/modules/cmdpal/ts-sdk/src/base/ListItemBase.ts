// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { IListItem, ICommand, ContextItem, IconInfo, Tag, Details } from '../types';

/**
 * Base class for list items displayed in list pages.
 *
 * @example
 * ```typescript
 * import { ListItemBase } from '@microsoft/cmdpal-sdk';
 *
 * const item = new ListItemBase({
 *   command: { id: 'open-file', name: 'Open File' },
 *   title: 'document.txt',
 *   subtitle: 'Modified 2 hours ago',
 *   tags: [{ text: 'Recent' }],
 *   section: 'Documents',
 * });
 * ```
 */
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

  constructor(options: {
    command: ICommand;
    title: string;
    subtitle?: string;
    icon?: IconInfo | null;
    moreCommands?: ContextItem[];
    tags?: Tag[];
    details?: Details;
    section?: string;
    textToSuggest?: string;
  }) {
    this.command = options.command;
    this.title = options.title;
    this.subtitle = options.subtitle;
    this.icon = options.icon;
    this.moreCommands = options.moreCommands;
    this.tags = options.tags;
    this.details = options.details;
    this.section = options.section;
    this.textToSuggest = options.textToSuggest;
  }
}
