// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  Filters,
  GridProperties,
  ICommandItem,
  IListItem,
  IListPage,
  IconInfo,
  OptionalColor,
} from '../types.js';

/**
 * Base class for a page that shows a scrollable, static list of items.
 *
 * @example
 * ```typescript
 * class MyListPage extends ListPageBase {
 *   readonly id = 'my-list';
 *   readonly name = 'My List';
 *   readonly title = 'My List Page';
 *
 *   getItems(): IListItem[] {
 *     return [new ListItemBase({ command: new NoOpCommand(), title: 'Item 1' })];
 *   }
 * }
 * ```
 */
export abstract class ListPageBase implements IListPage {
  abstract readonly id: string;
  abstract readonly name: string;
  abstract readonly title: string;

  icon?: IconInfo | null = null;
  isLoading?: boolean = false;
  accentColor?: OptionalColor | null = null;
  searchText?: string = '';
  placeholderText?: string = '';
  showDetails?: boolean = false;
  filters?: Filters | null = null;
  gridProperties?: GridProperties | null = null;
  hasMoreItems?: boolean = false;
  emptyContent?: ICommandItem | null = null;

  abstract getItems(): IListItem[] | Promise<IListItem[]>;

  loadMore(): void | Promise<void> {
    // Override when the page supports infinite scroll.
  }
}
