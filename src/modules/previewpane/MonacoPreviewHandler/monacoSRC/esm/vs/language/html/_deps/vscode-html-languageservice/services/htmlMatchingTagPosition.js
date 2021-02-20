/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export function findMatchingTagPosition(document, position, htmlDocument) {
    var offset = document.offsetAt(position);
    var node = htmlDocument.findNodeAt(offset);
    if (!node.tag) {
        return null;
    }
    if (!node.endTagStart) {
        return null;
    }
    // Within open tag, compute close tag
    if (node.start + '<'.length <= offset && offset <= node.start + '<'.length + node.tag.length) {
        var mirrorOffset = (offset - '<'.length - node.start) + node.endTagStart + '</'.length;
        return document.positionAt(mirrorOffset);
    }
    // Within closing tag, compute open tag
    if (node.endTagStart + '</'.length <= offset && offset <= node.endTagStart + '</'.length + node.tag.length) {
        var mirrorOffset = (offset - '</'.length - node.endTagStart) + node.start + '<'.length;
        return document.positionAt(mirrorOffset);
    }
    return null;
}
