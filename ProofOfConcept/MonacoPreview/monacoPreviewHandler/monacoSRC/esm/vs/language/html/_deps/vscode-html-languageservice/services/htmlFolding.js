/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { TokenType, FoldingRangeKind } from '../htmlLanguageTypes.js';
import { createScanner } from '../parser/htmlScanner.js';
import { isVoidElement } from '../languageFacts/fact.js';
function limitRanges(ranges, rangeLimit) {
    ranges = ranges.sort(function (r1, r2) {
        var diff = r1.startLine - r2.startLine;
        if (diff === 0) {
            diff = r1.endLine - r2.endLine;
        }
        return diff;
    });
    // compute each range's nesting level in 'nestingLevels'.
    // count the number of ranges for each level in 'nestingLevelCounts'
    var top = void 0;
    var previous = [];
    var nestingLevels = [];
    var nestingLevelCounts = [];
    var setNestingLevel = function (index, level) {
        nestingLevels[index] = level;
        if (level < 30) {
            nestingLevelCounts[level] = (nestingLevelCounts[level] || 0) + 1;
        }
    };
    // compute nesting levels and sanitize
    for (var i = 0; i < ranges.length; i++) {
        var entry = ranges[i];
        if (!top) {
            top = entry;
            setNestingLevel(i, 0);
        }
        else {
            if (entry.startLine > top.startLine) {
                if (entry.endLine <= top.endLine) {
                    previous.push(top);
                    top = entry;
                    setNestingLevel(i, previous.length);
                }
                else if (entry.startLine > top.endLine) {
                    do {
                        top = previous.pop();
                    } while (top && entry.startLine > top.endLine);
                    if (top) {
                        previous.push(top);
                    }
                    top = entry;
                    setNestingLevel(i, previous.length);
                }
            }
        }
    }
    var entries = 0;
    var maxLevel = 0;
    for (var i = 0; i < nestingLevelCounts.length; i++) {
        var n = nestingLevelCounts[i];
        if (n) {
            if (n + entries > rangeLimit) {
                maxLevel = i;
                break;
            }
            entries += n;
        }
    }
    var result = [];
    for (var i = 0; i < ranges.length; i++) {
        var level = nestingLevels[i];
        if (typeof level === 'number') {
            if (level < maxLevel || (level === maxLevel && entries++ < rangeLimit)) {
                result.push(ranges[i]);
            }
        }
    }
    return result;
}
export function getFoldingRanges(document, context) {
    var scanner = createScanner(document.getText());
    var token = scanner.scan();
    var ranges = [];
    var stack = [];
    var lastTagName = null;
    var prevStart = -1;
    function addRange(range) {
        ranges.push(range);
        prevStart = range.startLine;
    }
    while (token !== TokenType.EOS) {
        switch (token) {
            case TokenType.StartTag: {
                var tagName = scanner.getTokenText();
                var startLine = document.positionAt(scanner.getTokenOffset()).line;
                stack.push({ startLine: startLine, tagName: tagName });
                lastTagName = tagName;
                break;
            }
            case TokenType.EndTag: {
                lastTagName = scanner.getTokenText();
                break;
            }
            case TokenType.StartTagClose:
                if (!lastTagName || !isVoidElement(lastTagName)) {
                    break;
                }
            // fallthrough
            case TokenType.EndTagClose:
            case TokenType.StartTagSelfClose: {
                var i = stack.length - 1;
                while (i >= 0 && stack[i].tagName !== lastTagName) {
                    i--;
                }
                if (i >= 0) {
                    var stackElement = stack[i];
                    stack.length = i;
                    var line = document.positionAt(scanner.getTokenOffset()).line;
                    var startLine = stackElement.startLine;
                    var endLine = line - 1;
                    if (endLine > startLine && prevStart !== startLine) {
                        addRange({ startLine: startLine, endLine: endLine });
                    }
                }
                break;
            }
            case TokenType.Comment: {
                var startLine = document.positionAt(scanner.getTokenOffset()).line;
                var text = scanner.getTokenText();
                var m = text.match(/^\s*#(region\b)|(endregion\b)/);
                if (m) {
                    if (m[1]) { // start pattern match
                        stack.push({ startLine: startLine, tagName: '' }); // empty tagName marks region
                    }
                    else {
                        var i = stack.length - 1;
                        while (i >= 0 && stack[i].tagName.length) {
                            i--;
                        }
                        if (i >= 0) {
                            var stackElement = stack[i];
                            stack.length = i;
                            var endLine = startLine;
                            startLine = stackElement.startLine;
                            if (endLine > startLine && prevStart !== startLine) {
                                addRange({ startLine: startLine, endLine: endLine, kind: FoldingRangeKind.Region });
                            }
                        }
                    }
                }
                else {
                    var endLine = document.positionAt(scanner.getTokenOffset() + scanner.getTokenLength()).line;
                    if (startLine < endLine) {
                        addRange({ startLine: startLine, endLine: endLine, kind: FoldingRangeKind.Comment });
                    }
                }
                break;
            }
        }
        token = scanner.scan();
    }
    var rangeLimit = context && context.rangeLimit || Number.MAX_VALUE;
    if (ranges.length > rangeLimit) {
        return limitRanges(ranges, rangeLimit);
    }
    return ranges;
}
