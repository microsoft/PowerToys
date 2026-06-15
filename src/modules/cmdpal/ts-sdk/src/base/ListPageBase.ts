// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  IListPage,
  IListItem,
  IconInfo,
  OptionalColor,
  Filters,
  GridProperties,
  ICommandItem,
} from '../types';

/**
 * Base class for list pages that display a filterable/searchable list of items.
 *
 * @example
 * ```typescript
 * import { ListPageBase, ListItemBase } from '@microsoft/cmdpal-sdk';
 *
 * class MyListPage extends ListPageBase {
 *   id = 'my-list';
 *   name = 'My List';
 *   title = 'My List Page';
 *   placeholderText = 'Search items...';
 *
 *   getItems() {
 *     return [
 *       { command: { id: 'item-1', name: 'Item 1' }, title: 'Item 1', subtitle: 'Description' }
 *     ];
 *   }
 * }
 * ```
 */
export abstract class ListPageBase implements IListPage {
  abstract id: string;
  abstract name: string;
  abstract title: string;

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

  abstract getItems(): Promise<IListItem[]> | IListItem[];

  loadMore?(): Promise<void> | void {
    // Override if pagination is supported
  }
}
