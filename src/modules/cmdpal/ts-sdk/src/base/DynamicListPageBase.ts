// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { IDynamicListPage, IListItem } from '../types';
import { ListPageBase } from './ListPageBase';

/**
 * Base class for dynamic list pages where the host sends search text
 * and the extension filters/fetches results dynamically.
 *
 * @example
 * ```typescript
 * import { DynamicListPageBase } from '@microsoft/cmdpal-sdk';
 *
 * class SearchPage extends DynamicListPageBase {
 *   id = 'search';
 *   name = 'Search';
 *   title = 'Search';
 *   private query = '';
 *
 *   setSearchText(text: string) {
 *     this.query = text;
 *     this.notifyItemsChanged();
 *   }
 *
 *   async getItems() {
 *     return await fetchResults(this.query);
 *   }
 * }
 * ```
 */
export abstract class DynamicListPageBase extends ListPageBase implements IDynamicListPage {
  abstract setSearchText(text: string): void;

  /**
   * Call this when items have changed and the host should re-fetch.
   * The runtime patches this method to send a listPage/itemsChanged notification.
   */
  protected notifyItemsChanged(): void {
    // This will be wired up by the runtime bridge
    // The bridge patches this method to send JSONRPC notifications
  }
}
