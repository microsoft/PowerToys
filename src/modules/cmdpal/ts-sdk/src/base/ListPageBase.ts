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
  /** Unique identifier for the page. */
  abstract readonly id: string;
  /** Internal name of the page. */
  abstract readonly name: string;
  /** Title shown at the top of the page. */
  abstract readonly title: string;

  /** Icon shown for the page. */
  icon?: IconInfo | null = null;
  /** Whether the page is currently loading; shows a progress indicator. */
  isLoading?: boolean = false;
  /** Accent color applied to the page. Uses the host default when unset. */
  accentColor?: OptionalColor | null = null;
  /** Current text in the search box. */
  searchText?: string = '';
  /** Placeholder shown in the search box while it is empty. */
  placeholderText?: string = '';
  /** Show the details panel next to the list. */
  showDetails?: boolean = false;
  /** Filter dropdown shown above the list, or `null` for none. */
  filters?: Filters | null = null;
  /** Grid layout settings, or `null` to render a plain list. */
  gridProperties?: GridProperties | null = null;
  /** Whether more items can be loaded (infinite scroll). */
  hasMoreItems?: boolean = false;
  /** Item shown when the list is empty, or `null` for none. */
  emptyContent?: ICommandItem | null = null;

  /**
   * Produces the items to display.
   *
   * @returns The current list items, synchronously or as a promise.
   */
  abstract getItems(): IListItem[] | Promise<IListItem[]>;

  /**
   * Loads the next page of items when {@link ListPageBase.hasMoreItems} is
   * `true`. Does nothing by default; override to support infinite scroll.
   */
  loadMore(): void | Promise<void> {
    // Override when the page supports infinite scroll.
  }
}
