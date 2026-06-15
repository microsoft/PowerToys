import type { IListPage, IListItem, IconInfo, OptionalColor, Filters, GridProperties, ICommandItem } from '../types';
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
export declare abstract class ListPageBase implements IListPage {
    abstract id: string;
    abstract name: string;
    abstract title: string;
    icon?: IconInfo | null;
    isLoading?: boolean;
    accentColor?: OptionalColor | null;
    searchText?: string;
    placeholderText?: string;
    showDetails?: boolean;
    filters?: Filters | null;
    gridProperties?: GridProperties | null;
    hasMoreItems?: boolean;
    emptyContent?: ICommandItem | null;
    abstract getItems(): Promise<IListItem[]> | IListItem[];
    loadMore?(): Promise<void> | void;
}
//# sourceMappingURL=ListPageBase.d.ts.map