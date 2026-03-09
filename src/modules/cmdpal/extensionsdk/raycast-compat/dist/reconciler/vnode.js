"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.isTextVNode = isTextVNode;
exports.isElementVNode = isElementVNode;
function isTextVNode(node) {
    return node.type === '#text';
}
function isElementVNode(node) {
    return node.type !== '#text';
}
//# sourceMappingURL=vnode.js.map