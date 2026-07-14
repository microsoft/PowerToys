"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ListPageBase = void 0;
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
class ListPageBase {
    icon = null;
    isLoading = false;
    accentColor = null;
    searchText = '';
    placeholderText = '';
    showDetails = false;
    filters = null;
    gridProperties = null;
    hasMoreItems = false;
    emptyContent = null;
    loadMore() {
        // Override if pagination is supported
    }
}
exports.ListPageBase = ListPageBase;
//# sourceMappingURL=ListPageBase.js.map