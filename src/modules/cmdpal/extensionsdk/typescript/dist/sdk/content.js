"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.TreeContent = exports.FormContent = exports.MarkdownContent = void 0;
const types_1 = require("../generated/types");
/**
 * Markdown content for displaying rich text.
 */
class MarkdownContent {
    constructor(body) {
        this.type = types_1.ContentType.Markdown;
        this.id = '';
        this.body = '';
        if (body !== undefined) {
            this.body = body;
        }
    }
    _initializeWithTransport(transport) {
        this._transport = transport;
    }
    notifyPropChanged(propertyName) {
        if (this.PropChanged) {
            this.PropChanged({ propertyName });
        }
        if (this._transport) {
            this._transport.sendNotification('content/propChanged', {
                contentId: this.id,
                propertyName,
            });
        }
    }
}
exports.MarkdownContent = MarkdownContent;
/**
 * Form content for user input.
 */
class FormContent {
    constructor() {
        this.type = types_1.ContentType.Form;
        this.id = '';
    }
    /** Ensure getter-based properties are included in JSON serialization. */
    toJSON() {
        return {
            type: this.type,
            id: this.id,
            templateJson: this.templateJson,
            dataJson: this.dataJson,
            stateJson: this.stateJson,
        };
    }
    _initializeWithTransport(transport) {
        this._transport = transport;
    }
    notifyPropChanged(propertyName) {
        if (this.PropChanged) {
            this.PropChanged({ propertyName });
        }
        if (this._transport) {
            this._transport.sendNotification('content/propChanged', {
                contentId: this.id,
                propertyName,
            });
        }
    }
}
exports.FormContent = FormContent;
/**
 * Tree content for hierarchical data.
 */
class TreeContent {
    constructor() {
        this.type = types_1.ContentType.Tree;
        this.id = '';
    }
    /** Ensure children are included in JSON serialization. */
    toJSON() {
        return {
            type: this.type,
            id: this.id,
            children: this.getChildren(),
        };
    }
    _initializeWithTransport(transport) {
        this._transport = transport;
    }
    notifyPropChanged(propertyName) {
        if (this.PropChanged) {
            this.PropChanged({ propertyName });
        }
        if (this._transport) {
            this._transport.sendNotification('content/propChanged', {
                contentId: this.id,
                propertyName,
            });
        }
    }
    notifyItemsChanged(totalItems) {
        if (this.ItemsChanged) {
            this.ItemsChanged({ totalItems: totalItems ?? -1 });
        }
    }
}
exports.TreeContent = TreeContent;
//# sourceMappingURL=content.js.map