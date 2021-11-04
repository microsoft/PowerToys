/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { createScanner } from '../parser/htmlScanner.js';
import { parse } from '../parser/htmlParser.js';
import { TokenType, Range, SelectionRange } from '../htmlLanguageTypes.js';
export function getSelectionRanges(document, positions) {
    function getSelectionRange(position) {
        var applicableRanges = getApplicableRanges(document, position);
        var prev = undefined;
        var current = undefined;
        for (var index = applicableRanges.length - 1; index >= 0; index--) {
            var range = applicableRanges[index];
            if (!prev || range[0] !== prev[0] || range[1] !== prev[1]) {
                current = SelectionRange.create(Range.create(document.positionAt(applicableRanges[index][0]), document.positionAt(applicableRanges[index][1])), current);
            }
            prev = range;
        }
        if (!current) {
            current = SelectionRange.create(Range.create(position, position));
        }
        return current;
    }
    return positions.map(getSelectionRange);
}
function getApplicableRanges(document, position) {
    var htmlDoc = parse(document.getText());
    var currOffset = document.offsetAt(position);
    var currNode = htmlDoc.findNodeAt(currOffset);
    var result = getAllParentTagRanges(currNode);
    // Self-closing or void elements
    if (currNode.startTagEnd && !currNode.endTagStart) {
        // THe rare case of unmatching tag pairs like <div></div1>
        if (currNode.startTagEnd !== currNode.end) {
            return [[currNode.start, currNode.end]];
        }
        var closeRange = Range.create(document.positionAt(currNode.startTagEnd - 2), document.positionAt(currNode.startTagEnd));
        var closeText = document.getText(closeRange);
        // Self-closing element
        if (closeText === '/>') {
            result.unshift([currNode.start + 1, currNode.startTagEnd - 2]);
        }
        // Void element
        else {
            result.unshift([currNode.start + 1, currNode.startTagEnd - 1]);
        }
        var attributeLevelRanges = getAttributeLevelRanges(document, currNode, currOffset);
        result = attributeLevelRanges.concat(result);
        return result;
    }
    if (!currNode.startTagEnd || !currNode.endTagStart) {
        return result;
    }
    /**
     * For html like
     * `<div class="foo">bar</div>`
     */
    result.unshift([currNode.start, currNode.end]);
    /**
     * Cursor inside `<div class="foo">`
     */
    if (currNode.start < currOffset && currOffset < currNode.startTagEnd) {
        result.unshift([currNode.start + 1, currNode.startTagEnd - 1]);
        var attributeLevelRanges = getAttributeLevelRanges(document, currNode, currOffset);
        result = attributeLevelRanges.concat(result);
        return result;
    }
    /**
     * Cursor inside `bar`
     */
    else if (currNode.startTagEnd <= currOffset && currOffset <= currNode.endTagStart) {
        result.unshift([currNode.startTagEnd, currNode.endTagStart]);
        return result;
    }
    /**
     * Cursor inside `</div>`
     */
    else {
        // `div` inside `</div>`
        if (currOffset >= currNode.endTagStart + 2) {
            result.unshift([currNode.endTagStart + 2, currNode.end - 1]);
        }
        return result;
    }
}
function getAllParentTagRanges(initialNode) {
    var currNode = initialNode;
    var getNodeRanges = function (n) {
        if (n.startTagEnd && n.endTagStart && n.startTagEnd < n.endTagStart) {
            return [
                [n.startTagEnd, n.endTagStart],
                [n.start, n.end]
            ];
        }
        return [
            [n.start, n.end]
        ];
    };
    var result = [];
    while (currNode.parent) {
        currNode = currNode.parent;
        getNodeRanges(currNode).forEach(function (r) { return result.push(r); });
    }
    return result;
}
function getAttributeLevelRanges(document, currNode, currOffset) {
    var currNodeRange = Range.create(document.positionAt(currNode.start), document.positionAt(currNode.end));
    var currNodeText = document.getText(currNodeRange);
    var relativeOffset = currOffset - currNode.start;
    /**
     * Tag level semantic selection
     */
    var scanner = createScanner(currNodeText);
    var token = scanner.scan();
    /**
     * For text like
     * <div class="foo">bar</div>
     */
    var positionOffset = currNode.start;
    var result = [];
    var isInsideAttribute = false;
    var attrStart = -1;
    while (token !== TokenType.EOS) {
        switch (token) {
            case TokenType.AttributeName: {
                if (relativeOffset < scanner.getTokenOffset()) {
                    isInsideAttribute = false;
                    break;
                }
                if (relativeOffset <= scanner.getTokenEnd()) {
                    // `class`
                    result.unshift([scanner.getTokenOffset(), scanner.getTokenEnd()]);
                }
                isInsideAttribute = true;
                attrStart = scanner.getTokenOffset();
                break;
            }
            case TokenType.AttributeValue: {
                if (!isInsideAttribute) {
                    break;
                }
                var valueText = scanner.getTokenText();
                if (relativeOffset < scanner.getTokenOffset()) {
                    // `class="foo"`
                    result.push([attrStart, scanner.getTokenEnd()]);
                    break;
                }
                if (relativeOffset >= scanner.getTokenOffset() && relativeOffset <= scanner.getTokenEnd()) {
                    // `"foo"`
                    result.unshift([scanner.getTokenOffset(), scanner.getTokenEnd()]);
                    // `foo`
                    if ((valueText[0] === "\"" && valueText[valueText.length - 1] === "\"") || (valueText[0] === "'" && valueText[valueText.length - 1] === "'")) {
                        if (relativeOffset >= scanner.getTokenOffset() + 1 && relativeOffset <= scanner.getTokenEnd() - 1) {
                            result.unshift([scanner.getTokenOffset() + 1, scanner.getTokenEnd() - 1]);
                        }
                    }
                    // `class="foo"`
                    result.push([attrStart, scanner.getTokenEnd()]);
                }
                break;
            }
        }
        token = scanner.scan();
    }
    return result.map(function (pair) {
        return [pair[0] + positionOffset, pair[1] + positionOffset];
    });
}
