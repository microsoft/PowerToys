"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.CommandItemBase = void 0;
/**
 * Base class for command items displayed in lists and menus.
 */
class CommandItemBase {
    command;
    title;
    subtitle;
    icon;
    moreCommands;
    constructor(options) {
        this.command = options.command;
        this.title = options.title;
        this.subtitle = options.subtitle;
        this.icon = options.icon ?? null;
        this.moreCommands = options.moreCommands;
    }
}
exports.CommandItemBase = CommandItemBase;
//# sourceMappingURL=CommandItemBase.js.map