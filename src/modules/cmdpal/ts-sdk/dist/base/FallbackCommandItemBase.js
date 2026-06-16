"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.FallbackCommandItemBase = void 0;
/**
 * Base class for fallback command items that appear when the user types
 * from the home page. These provide search/filter results as the user types.
 *
 * @example
 * ```typescript
 * class WebSearchFallback extends FallbackCommandItemBase {
 *   command = new WebSearchCommand()
 *   title = 'Search the web'
 *
 *   updateQuery(query: string) {
 *     this.displayTitle = `Search for "${query}"`
 *   }
 * }
 * ```
 */
class FallbackCommandItemBase {
    subtitle;
    icon;
    moreCommands;
    displayTitle;
    get fallbackHandler() {
        return this;
    }
}
exports.FallbackCommandItemBase = FallbackCommandItemBase;
//# sourceMappingURL=FallbackCommandItemBase.js.map