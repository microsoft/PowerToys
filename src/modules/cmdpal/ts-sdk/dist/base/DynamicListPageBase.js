"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.DynamicListPageBase = void 0;
const ListPageBase_1 = require("./ListPageBase");
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
class DynamicListPageBase extends ListPageBase_1.ListPageBase {
    /**
     * Call this when items have changed and the host should re-fetch.
     * The SDK bridge will send a notify/itemsChanged notification.
     */
    notifyItemsChanged() {
        // This will be wired up by the runtime bridge
        // The bridge patches this method to send JSONRPC notifications
    }
}
exports.DynamicListPageBase = DynamicListPageBase;
//# sourceMappingURL=DynamicListPageBase.js.map