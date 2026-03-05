"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ContentPage = exports.DynamicListPage = exports.ListPage = void 0;
const types_1 = require("../generated/types");
/**
 * Base class for list pages.
 */
class ListPage {
    constructor() {
        this._type = types_1.PageType.ListPage;
        this.id = '';
        this.name = '';
        this.placeholderText = '';
        this.searchText = '';
        this.showDetails = false;
        this.hasMoreItems = false;
    }
    /** Load more items (for pagination). */
    loadMore() {
        // Default: no-op
    }
    notifyPropChanged(propertyName) {
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
    notifyItemsChanged(totalItems) {
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
    _initializeWithTransport(transport) {
        this._transport = transport;
    }
    toJSON() {
        const result = {
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
exports.ListPage = ListPage;
/**
 * Base class for dynamic list pages with search support.
 */
class DynamicListPage extends ListPage {
    constructor() {
        super(...arguments);
        this._type = types_1.PageType.DynamicListPage;
    }
    setSearchText(searchText) {
        const oldSearch = this.searchText;
        this.searchText = searchText;
        this.updateSearchText(oldSearch, searchText);
    }
}
exports.DynamicListPage = DynamicListPage;
/**
 * Base class for content pages.
 */
class ContentPage {
    constructor() {
        this._type = types_1.PageType.ContentPage;
        this.id = '';
        this.name = '';
        this.commands = [];
    }
    notifyPropChanged(propertyName) {
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
    notifyItemsChanged(totalItems) {
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
    _initializeWithTransport(transport) {
        this._transport = transport;
    }
    toJSON() {
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
exports.ContentPage = ContentPage;
//# sourceMappingURL=pages.js.map