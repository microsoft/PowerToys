"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.Separator = void 0;
/**
 * A separator item that can be used in list pages as a visual divider.
 * When placed in a list, it appears as a section header or divider line.
 */
class Separator {
    command;
    title;
    subtitle;
    icon;
    moreCommands;
    tags;
    details;
    section;
    textToSuggest;
    /** Marker for serialization to identify this as a separator */
    _isSeparator = true;
    constructor(title = '') {
        this.title = title;
        this.command = { id: `separator-${title}`, name: '' };
    }
}
exports.Separator = Separator;
//# sourceMappingURL=Separator.js.map