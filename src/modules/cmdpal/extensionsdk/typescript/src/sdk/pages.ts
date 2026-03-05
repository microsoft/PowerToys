// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  IListPage,
  IDynamicListPage,
  IContentPage,
  IListItem,
  IContent,
  IFilters,
  IGridProperties,
  ICommandItem,
  IDetails,
  IContextItem,
  PageType,
} from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';

/**
 * Base class for list pages.
 */
export abstract class ListPage implements IListPage {
  readonly _type: string = PageType.ListPage;
  id: string = '';
  name: string = '';
  icon?: any;
  placeholderText: string = '';
  searchText: string = '';
  showDetails: boolean = false;
  hasMoreItems: boolean = false;
  filters?: IFilters;
  gridProperties?: IGridProperties;
  emptyContent?: ICommandItem;

  protected _transport?: JsonRpcTransport;

  PropChanged?: (args: unknown) => void;
  ItemsChanged?: (args: unknown) => void;

  /** Get the items to display on this page. */
  abstract getItems(): IListItem[];

  /** Load more items (for pagination). */
  loadMore(): void {
    // Default: no-op
  }

  protected notifyPropChanged(propertyName: string): void {
    if (this.PropChanged) {
      this.PropChanged({ propertyName });
    }
    if (this._transport) {
      this._transport.sendNotification('page/propChanged', {
        pageId: this.id,
        propertyName,
      });
    }
  }

  protected notifyItemsChanged(totalItems?: number): void {
    if (this.ItemsChanged) {
      this.ItemsChanged({ totalItems: totalItems ?? -1 });
    }

    if (this._transport) {
      this._transport.sendNotification('listPage/itemsChanged', {
        pageId: this.id,
        totalItems: totalItems ?? -1,
      });
    }
  }

  _initializeWithTransport(transport: JsonRpcTransport): void {
    this._transport = transport;
  }

  toJSON(): Record<string, unknown> {
    const result: Record<string, unknown> = {
      _type: this._type,
      id: this.id,
      name: this.name,
      icon: this.icon,
      placeholderText: this.placeholderText,
      searchText: this.searchText,
      showDetails: this.showDetails,
      hasMoreItems: this.hasMoreItems,
      gridProperties: this.gridProperties,
    };

    if (this.filters) {
      result.filters = {
        currentFilterId: this.filters.currentFilterId ?? '',
        filters: this.filters.getFilters(),
      };
    }

    return result;
  }
}

/**
 * Base class for dynamic list pages with search support.
 */
export abstract class DynamicListPage extends ListPage implements IDynamicListPage {
  override readonly _type: string = PageType.DynamicListPage;

  /**
   * Called when the search text changes.
   * @param oldSearch The previous search text
   * @param newSearch The new search text
   */
  abstract updateSearchText(oldSearch: string, newSearch: string): void;

  setSearchText(searchText: string): void {
    const oldSearch = this.searchText;
    this.searchText = searchText;
    this.updateSearchText(oldSearch, searchText);
  }
}

/**
 * Base class for content pages.
 */
export abstract class ContentPage implements IContentPage {
  readonly _type: string = PageType.ContentPage;
  id: string = '';
  name: string = '';
  icon?: any;
  details?: IDetails;
  commands: IContextItem[] = [];

  protected _transport?: JsonRpcTransport;

  PropChanged?: (args: unknown) => void;
  ItemsChanged?: (args: unknown) => void;

  /** Get the content to display on this page. */
  abstract getContent(): IContent[];

  protected notifyPropChanged(propertyName: string): void {
    if (this.PropChanged) {
      this.PropChanged({ propertyName });
    }
    if (this._transport) {
      this._transport.sendNotification('page/propChanged', {
        pageId: this.id,
        propertyName,
      });
    }
  }

  protected notifyItemsChanged(totalItems?: number): void {
    if (this.ItemsChanged) {
      this.ItemsChanged({ totalItems: totalItems ?? -1 });
    }

    if (this._transport) {
      this._transport.sendNotification('listPage/itemsChanged', {
        pageId: this.id,
        totalItems: totalItems ?? -1,
      });
    }
  }

  _initializeWithTransport(transport: JsonRpcTransport): void {
    this._transport = transport;
  }

  toJSON(): Record<string, unknown> {
    return {
      _type: this._type,
      id: this.id,
      name: this.name,
      icon: this.icon,
      details: this.details,
      commands: this.commands,
    };
  }
}
