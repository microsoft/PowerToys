/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import { TokenType, Scanner } from '../parser/cssScanner.js';
import { SCSSScanner, InterpolationFunction } from '../parser/scssScanner.js';
import { LESSScanner } from '../parser/lessScanner.js';
export function getFoldingRanges(document, context) {
    var ranges = computeFoldingRanges(document);
    return limitFoldingRanges(ranges, context);
}
function computeFoldingRanges(document) {
    function getStartLine(t) {
        return document.positionAt(t.offset).line;
    }
    function getEndLine(t) {
        return document.positionAt(t.offset + t.len).line;
    }
    function getScanner() {
        switch (document.languageId) {
            case 'scss':
                return new SCSSScanner();
            case 'less':
                return new LESSScanner();
            default:
                return new Scanner();
        }
    }
    function tokenToRange(t, kind) {
        var startLine = getStartLine(t);
        var endLine = getEndLine(t);
        if (startLine !== endLine) {
            return {
                startLine: startLine,
                endLine: endLine,
                kind: kind
            };
        }
        else {
            return null;
        }
    }
    var ranges = [];
    var delimiterStack = [];
    var scanner = getScanner();
    scanner.ignoreComment = false;
    scanner.setSource(document.getText());
    var token = scanner.scan();
    var prevToken = null;
    var _loop_1 = function () {
        switch (token.type) {
            case TokenType.CurlyL:
            case InterpolationFunction:
                {
                    delimiterStack.push({ line: getStartLine(token), type: 'brace', isStart: true });
                    break;
                }
            case TokenType.CurlyR: {
                if (delimiterStack.length !== 0) {
                    var prevDelimiter = popPrevStartDelimiterOfType(delimiterStack, 'brace');
                    if (!prevDelimiter) {
                        break;
                    }
                    var endLine = getEndLine(token);
                    if (prevDelimiter.type === 'brace') {
                        /**
                         * Other than the case when curly brace is not on a new line by itself, for example
                         * .foo {
                         *   color: red; }
                         * Use endLine minus one to show ending curly brace
                         */
                        if (prevToken && getEndLine(prevToken) !== endLine) {
                            endLine--;
                        }
                        if (prevDelimiter.line !== endLine) {
                            ranges.push({
                                startLine: prevDelimiter.line,
                                endLine: endLine,
                                kind: undefined
                            });
                        }
                    }
                }
                break;
            }
            /**
             * In CSS, there is no single line comment prefixed with //
             * All comments are marked as `Comment`
             */
            case TokenType.Comment: {
                var commentRegionMarkerToDelimiter_1 = function (marker) {
                    if (marker === '#region') {
                        return { line: getStartLine(token), type: 'comment', isStart: true };
                    }
                    else {
                        return { line: getEndLine(token), type: 'comment', isStart: false };
                    }
                };
                var getCurrDelimiter = function (token) {
                    var matches = token.text.match(/^\s*\/\*\s*(#region|#endregion)\b\s*(.*?)\s*\*\//);
                    if (matches) {
                        return commentRegionMarkerToDelimiter_1(matches[1]);
                    }
                    else if (document.languageId === 'scss' || document.languageId === 'less') {
                        var matches_1 = token.text.match(/^\s*\/\/\s*(#region|#endregion)\b\s*(.*?)\s*/);
                        if (matches_1) {
                            return commentRegionMarkerToDelimiter_1(matches_1[1]);
                        }
                    }
                    return null;
                };
                var currDelimiter = getCurrDelimiter(token);
                // /* */ comment region folding
                // All #region and #endregion cases
                if (currDelimiter) {
                    if (currDelimiter.isStart) {
                        delimiterStack.push(currDelimiter);
                    }
                    else {
                        var prevDelimiter = popPrevStartDelimiterOfType(delimiterStack, 'comment');
                        if (!prevDelimiter) {
                            break;
                        }
                        if (prevDelimiter.type === 'comment') {
                            if (prevDelimiter.line !== currDelimiter.line) {
                                ranges.push({
                                    startLine: prevDelimiter.line,
                                    endLine: currDelimiter.line,
                                    kind: 'region'
                                });
                            }
                        }
                    }
                }
                // Multiline comment case
                else {
                    var range = tokenToRange(token, 'comment');
                    if (range) {
                        ranges.push(range);
                    }
                }
                break;
            }
        }
        prevToken = token;
        token = scanner.scan();
    };
    while (token.type !== TokenType.EOF) {
        _loop_1();
    }
    return ranges;
}
function popPrevStartDelimiterOfType(stack, type) {
    if (stack.length === 0) {
        return null;
    }
    for (var i = stack.length - 1; i >= 0; i--) {
        if (stack[i].type === type && stack[i].isStart) {
            return stack.splice(i, 1)[0];
        }
    }
    return null;
}
/**
 * - Sort regions
 * - Remove invalid regions (intersections)
 * - If limit exceeds, only return `rangeLimit` amount of ranges
 */
function limitFoldingRanges(ranges, context) {
    var maxRanges = context && context.rangeLimit || Number.MAX_VALUE;
    var sortedRanges = ranges.sort(function (r1, r2) {
        var diff = r1.startLine - r2.startLine;
        if (diff === 0) {
            diff = r1.endLine - r2.endLine;
        }
        return diff;
    });
    var validRanges = [];
    var prevEndLine = -1;
    sortedRanges.forEach(function (r) {
        if (!(r.startLine < prevEndLine && prevEndLine < r.endLine)) {
            validRanges.push(r);
            prevEndLine = r.endLine;
        }
    });
    if (validRanges.length < maxRanges) {
        return validRanges;
    }
    else {
        return validRanges.slice(0, maxRanges);
    }
}
