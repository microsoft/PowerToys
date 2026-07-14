import type { IDynamicListPage } from '../types';
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
export declare abstract class DynamicListPageBase extends ListPageBase implements IDynamicListPage {
    abstract setSearchText(text: string): void;
    /**
     * Call this when items have changed and the host should re-fetch.
     * The SDK bridge will send a notify/itemsChanged notification.
     */
    protected notifyItemsChanged(): void;
}
//# sourceMappingURL=DynamicListPageBase.d.ts.map