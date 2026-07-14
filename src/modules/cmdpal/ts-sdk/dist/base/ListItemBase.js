"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ListItemBase = void 0;
/**
 * Base class for list items displayed in list pages.
 *
 * @example
 * ```typescript
 * import { ListItemBase } from '@microsoft/cmdpal-sdk';
 *
 * const item = new ListItemBase({
 *   command: { id: 'open-file', name: 'Open File' },
 *   title: 'document.txt',
 *   subtitle: 'Modified 2 hours ago',
 *   tags: [{ text: 'Recent' }],
 *   section: 'Documents',
 * });
 * ```
 */
class ListItemBase {
    command;
    title;
    subtitle;
    icon;
    moreCommands;
    tags;
    details;
    section;
    textToSuggest;
    constructor(options) {
        this.command = options.command;
        this.title = options.title;
        this.subtitle = options.subtitle;
        this.icon = options.icon;
        this.moreCommands = options.moreCommands;
        this.tags = options.tags;
        this.details = options.details;
        this.section = options.section;
        this.textToSuggest = options.textToSuggest;
    }
}
exports.ListItemBase = ListItemBase;
//# sourceMappingURL=ListItemBase.js.map