import { IListPage, IDynamicListPage, IContentPage, IListItem, IContent, IFilters, IGridProperties, ICommandItem, IDetails, IContextItem } from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';
/**
 * Base class for list pages.
 */
export declare abstract class ListPage implements IListPage {
    readonly _type: string;
    id: string;
    name: string;
    icon?: any;
    placeholderText: string;
    searchText: string;
    showDetails: boolean;
    hasMoreItems: boolean;
    filters?: IFilters;
    gridProperties?: IGridProperties;
    emptyContent?: ICommandItem;
    protected _transport?: JsonRpcTransport;
    PropChanged?: (args: unknown) => void;
    ItemsChanged?: (args: unknown) => void;
    /** Get the items to display on this page. */
    abstract getItems(): IListItem[];
    /** Load more items (for pagination). */
    loadMore(): void;
    protected notifyPropChanged(propertyName: string): void;
    protected notifyItemsChanged(totalItems?: number): void;
    _initializeWithTransport(transport: JsonRpcTransport): void;
    toJSON(): Record<string, unknown>;
}
/**
 * Base class for dynamic list pages with search support.
 */
export declare abstract class DynamicListPage extends ListPage implements IDynamicListPage {
    readonly _type: string;
    /**
     * Called when the search text changes.
     * @param oldSearch The previous search text
     * @param newSearch The new search text
     */
    abstract updateSearchText(oldSearch: string, newSearch: string): void;
    setSearchText(searchText: string): void;
}
/**
 * Base class for content pages.
 */
export declare abstract class ContentPage implements IContentPage {
    readonly _type: string;
    id: string;
    name: string;
    icon?: any;
    details?: IDetails;
    commands: IContextItem[];
    protected _transport?: JsonRpcTransport;
    PropChanged?: (args: unknown) => void;
    ItemsChanged?: (args: unknown) => void;
    /** Get the content to display on this page. */
    abstract getContent(): IContent[];
    protected notifyPropChanged(propertyName: string): void;
    protected notifyItemsChanged(totalItems?: number): void;
    _initializeWithTransport(transport: JsonRpcTransport): void;
    toJSON(): Record<string, unknown>;
}
//# sourceMappingURL=pages.d.ts.map