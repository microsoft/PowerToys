/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Range } from '../jsonLanguageTypes.js';
export function findLinks(document, doc) {
    var links = [];
    doc.visit(function (node) {
        var _a;
        if (node.type === "property" && node.keyNode.value === "$ref" && ((_a = node.valueNode) === null || _a === void 0 ? void 0 : _a.type) === 'string') {
            var path = node.valueNode.value;
            var targetNode = findTargetNode(doc, path);
            if (targetNode) {
                var targetPos = document.positionAt(targetNode.offset);
                links.push({
                    target: document.uri + "#" + (targetPos.line + 1) + "," + (targetPos.character + 1),
                    range: createRange(document, node.valueNode)
                });
            }
        }
        return true;
    });
    return Promise.resolve(links);
}
function createRange(document, node) {
    return Range.create(document.positionAt(node.offset + 1), document.positionAt(node.offset + node.length - 1));
}
function findTargetNode(doc, path) {
    var tokens = parseJSONPointer(path);
    if (!tokens) {
        return null;
    }
    return findNode(tokens, doc.root);
}
function findNode(pointer, node) {
    if (!node) {
        return null;
    }
    if (pointer.length === 0) {
        return node;
    }
    var token = pointer.shift();
    if (node && node.type === 'object') {
        var propertyNode = node.properties.find(function (propertyNode) { return propertyNode.keyNode.value === token; });
        if (!propertyNode) {
            return null;
        }
        return findNode(pointer, propertyNode.valueNode);
    }
    else if (node && node.type === 'array') {
        if (token.match(/^(0|[1-9][0-9]*)$/)) {
            var index = Number.parseInt(token);
            var arrayItem = node.items[index];
            if (!arrayItem) {
                return null;
            }
            return findNode(pointer, arrayItem);
        }
    }
    return null;
}
function parseJSONPointer(path) {
    if (path === "#") {
        return [];
    }
    if (path[0] !== '#' || path[1] !== '/') {
        return null;
    }
    return path.substring(2).split(/\//).map(unescape);
}
function unescape(str) {
    return str.replace(/~1/g, '/').replace(/~0/g, '~');
}
