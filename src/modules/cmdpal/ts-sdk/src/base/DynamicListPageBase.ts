// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { IDynamicListPage } from '../types.js';
import { sendNotification } from '../runtime/notifications.js';
import { ListPageBase } from './ListPageBase.js';

/**
 * Base class for a list page that receives the search text as the user types
 * and produces results dynamically.
 *
 * Call {@link DynamicListPageBase.notifyItemsChanged} after updating internal
 * state so the host re-fetches the visible items.
 *
 * @example
 * ```typescript
 * class SearchPage extends DynamicListPageBase {
 *   readonly id = 'search';
 *   readonly name = 'Search';
 *   readonly title = 'Search';
 *   private query = '';
 *
 *   setSearchText(text: string): void {
 *     this.query = text;
 *     this.notifyItemsChanged();
 *   }
 *
 *   getItems(): IListItem[] {
 *     return allItems.filter((item) => item.title.includes(this.query));
 *   }
 * }
 * ```
 */
export abstract class DynamicListPageBase extends ListPageBase implements IDynamicListPage {
  /**
   * Called whenever the search text changes so the page can update its results.
   * Typically stores the query and calls
   * {@link DynamicListPageBase.notifyItemsChanged}.
   *
   * @param text The current text in the search box.
   */
  abstract setSearchText(text: string): void | Promise<void>;

  /**
   * Tells the host that this page's items have changed and should be re-fetched.
   */
  protected notifyItemsChanged(): void {
    sendNotification('listPage/itemsChanged', { pageId: this.id });
  }
}
