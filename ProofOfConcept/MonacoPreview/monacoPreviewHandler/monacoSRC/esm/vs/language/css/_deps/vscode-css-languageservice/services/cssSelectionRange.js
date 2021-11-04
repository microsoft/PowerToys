/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import { Range, SelectionRange } from '../cssLanguageTypes.js';
import { NodeType } from '../parser/cssNodes.js';
export function getSelectionRanges(document, positions, stylesheet) {
    function getSelectionRange(position) {
        var applicableRanges = getApplicableRanges(position);
        var current = undefined;
        for (var index = applicableRanges.length - 1; index >= 0; index--) {
            current = SelectionRange.create(Range.create(document.positionAt(applicableRanges[index][0]), document.positionAt(applicableRanges[index][1])), current);
        }
        if (!current) {
            current = SelectionRange.create(Range.create(position, position));
        }
        return current;
    }
    return positions.map(getSelectionRange);
    function getApplicableRanges(position) {
        var offset = document.offsetAt(position);
        var currNode = stylesheet.findChildAtOffset(offset, true);
        if (!currNode) {
            return [];
        }
        var result = [];
        while (currNode) {
            if (currNode.parent &&
                currNode.offset === currNode.parent.offset &&
                currNode.end === currNode.parent.end) {
                currNode = currNode.parent;
                continue;
            }
            // The `{ }` part of `.a { }`
            if (currNode.type === NodeType.Declarations) {
                if (offset > currNode.offset && offset < currNode.end) {
                    // Return `{ }` and the range inside `{` and `}`
                    result.push([currNode.offset + 1, currNode.end - 1]);
                }
            }
            result.push([currNode.offset, currNode.end]);
            currNode = currNode.parent;
        }
        return result;
    }
}
