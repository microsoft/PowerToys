/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var _a;
import { createStringBuilder } from '../../common/core/stringBuilder.js';
import * as strings from '../../../base/common/strings.js';
import { Configuration } from '../config/configuration.js';
import { LineBreakData } from '../../common/viewModel/viewModel.js';
const ttPolicy = (_a = window.trustedTypes) === null || _a === void 0 ? void 0 : _a.createPolicy('domLineBreaksComputer', { createHTML: value => value });
export class DOMLineBreaksComputerFactory {
    static create() {
        return new DOMLineBreaksComputerFactory();
    }
    constructor() {
    }
    createLineBreaksComputer(fontInfo, tabSize, wrappingColumn, wrappingIndent) {
        tabSize = tabSize | 0; //@perf
        wrappingColumn = +wrappingColumn; //@perf
        let requests = [];
        return {
            addRequest: (lineText, previousLineBreakData) => {
                requests.push(lineText);
            },
            finalize: () => {
                return createLineBreaks(requests, fontInfo, tabSize, wrappingColumn, wrappingIndent);
            }
        };
    }
}
function createLineBreaks(requests, fontInfo, tabSize, firstLineBreakColumn, wrappingIndent) {
    var _a;
    if (firstLineBreakColumn === -1) {
        const result = [];
        for (let i = 0, len = requests.length; i < len; i++) {
            result[i] = null;
        }
        return result;
    }
    const overallWidth = Math.round(firstLineBreakColumn * fontInfo.typicalHalfwidthCharacterWidth);
    // Cannot respect WrappingIndent.Indent and WrappingIndent.DeepIndent because that would require
    // two dom layouts, in order to first set the width of the first line, and then set the width of the wrapped lines
    if (wrappingIndent === 2 /* Indent */ || wrappingIndent === 3 /* DeepIndent */) {
        wrappingIndent = 1 /* Same */;
    }
    const containerDomNode = document.createElement('div');
    Configuration.applyFontInfoSlow(containerDomNode, fontInfo);
    const sb = createStringBuilder(10000);
    const firstNonWhitespaceIndices = [];
    const wrappedTextIndentLengths = [];
    const renderLineContents = [];
    const allCharOffsets = [];
    const allVisibleColumns = [];
    for (let i = 0; i < requests.length; i++) {
        const lineContent = requests[i];
        let firstNonWhitespaceIndex = 0;
        let wrappedTextIndentLength = 0;
        let width = overallWidth;
        if (wrappingIndent !== 0 /* None */) {
            firstNonWhitespaceIndex = strings.firstNonWhitespaceIndex(lineContent);
            if (firstNonWhitespaceIndex === -1) {
                // all whitespace line
                firstNonWhitespaceIndex = 0;
            }
            else {
                // Track existing indent
                for (let i = 0; i < firstNonWhitespaceIndex; i++) {
                    const charWidth = (lineContent.charCodeAt(i) === 9 /* Tab */
                        ? (tabSize - (wrappedTextIndentLength % tabSize))
                        : 1);
                    wrappedTextIndentLength += charWidth;
                }
                const indentWidth = Math.ceil(fontInfo.spaceWidth * wrappedTextIndentLength);
                // Force sticking to beginning of line if no character would fit except for the indentation
                if (indentWidth + fontInfo.typicalFullwidthCharacterWidth > overallWidth) {
                    firstNonWhitespaceIndex = 0;
                    wrappedTextIndentLength = 0;
                }
                else {
                    width = overallWidth - indentWidth;
                }
            }
        }
        const renderLineContent = lineContent.substr(firstNonWhitespaceIndex);
        const tmp = renderLine(renderLineContent, wrappedTextIndentLength, tabSize, width, sb);
        firstNonWhitespaceIndices[i] = firstNonWhitespaceIndex;
        wrappedTextIndentLengths[i] = wrappedTextIndentLength;
        renderLineContents[i] = renderLineContent;
        allCharOffsets[i] = tmp[0];
        allVisibleColumns[i] = tmp[1];
    }
    const html = sb.build();
    const trustedhtml = (_a = ttPolicy === null || ttPolicy === void 0 ? void 0 : ttPolicy.createHTML(html)) !== null && _a !== void 0 ? _a : html;
    containerDomNode.innerHTML = trustedhtml;
    containerDomNode.style.position = 'absolute';
    containerDomNode.style.top = '10000';
    containerDomNode.style.wordWrap = 'break-word';
    document.body.appendChild(containerDomNode);
    let range = document.createRange();
    const lineDomNodes = Array.prototype.slice.call(containerDomNode.children, 0);
    let result = [];
    for (let i = 0; i < requests.length; i++) {
        const lineDomNode = lineDomNodes[i];
        const breakOffsets = readLineBreaks(range, lineDomNode, renderLineContents[i], allCharOffsets[i]);
        if (breakOffsets === null) {
            result[i] = null;
            continue;
        }
        const firstNonWhitespaceIndex = firstNonWhitespaceIndices[i];
        const wrappedTextIndentLength = wrappedTextIndentLengths[i];
        const visibleColumns = allVisibleColumns[i];
        const breakOffsetsVisibleColumn = [];
        for (let j = 0, len = breakOffsets.length; j < len; j++) {
            breakOffsetsVisibleColumn[j] = visibleColumns[breakOffsets[j]];
        }
        if (firstNonWhitespaceIndex !== 0) {
            // All break offsets are relative to the renderLineContent, make them absolute again
            for (let j = 0, len = breakOffsets.length; j < len; j++) {
                breakOffsets[j] += firstNonWhitespaceIndex;
            }
        }
        result[i] = new LineBreakData(breakOffsets, breakOffsetsVisibleColumn, wrappedTextIndentLength);
    }
    document.body.removeChild(containerDomNode);
    return result;
}
function renderLine(lineContent, initialVisibleColumn, tabSize, width, sb) {
    sb.appendASCIIString('<div style="width:');
    sb.appendASCIIString(String(width));
    sb.appendASCIIString('px;">');
    // if (containsRTL) {
    // 	sb.appendASCIIString('" dir="ltr');
    // }
    const len = lineContent.length;
    let visibleColumn = initialVisibleColumn;
    let charOffset = 0;
    let charOffsets = [];
    let visibleColumns = [];
    let nextCharCode = (0 < len ? lineContent.charCodeAt(0) : 0 /* Null */);
    sb.appendASCIIString('<span>');
    for (let charIndex = 0; charIndex < len; charIndex++) {
        if (charIndex !== 0 && charIndex % 16384 /* SPAN_MODULO_LIMIT */ === 0) {
            sb.appendASCIIString('</span><span>');
        }
        charOffsets[charIndex] = charOffset;
        visibleColumns[charIndex] = visibleColumn;
        const charCode = nextCharCode;
        nextCharCode = (charIndex + 1 < len ? lineContent.charCodeAt(charIndex + 1) : 0 /* Null */);
        let producedCharacters = 1;
        let charWidth = 1;
        switch (charCode) {
            case 9 /* Tab */:
                producedCharacters = (tabSize - (visibleColumn % tabSize));
                charWidth = producedCharacters;
                for (let space = 1; space <= producedCharacters; space++) {
                    if (space < producedCharacters) {
                        sb.write1(0xA0); // &nbsp;
                    }
                    else {
                        sb.appendASCII(32 /* Space */);
                    }
                }
                break;
            case 32 /* Space */:
                if (nextCharCode === 32 /* Space */) {
                    sb.write1(0xA0); // &nbsp;
                }
                else {
                    sb.appendASCII(32 /* Space */);
                }
                break;
            case 60 /* LessThan */:
                sb.appendASCIIString('&lt;');
                break;
            case 62 /* GreaterThan */:
                sb.appendASCIIString('&gt;');
                break;
            case 38 /* Ampersand */:
                sb.appendASCIIString('&amp;');
                break;
            case 0 /* Null */:
                sb.appendASCIIString('&#00;');
                break;
            case 65279 /* UTF8_BOM */:
            case 8232 /* LINE_SEPARATOR */:
            case 8233 /* PARAGRAPH_SEPARATOR */:
            case 133 /* NEXT_LINE */:
                sb.write1(0xFFFD);
                break;
            default:
                if (strings.isFullWidthCharacter(charCode)) {
                    charWidth++;
                }
                // if (renderControlCharacters && charCode < 32) {
                // 	sb.write1(9216 + charCode);
                // } else {
                sb.write1(charCode);
            // }
        }
        charOffset += producedCharacters;
        visibleColumn += charWidth;
    }
    sb.appendASCIIString('</span>');
    charOffsets[lineContent.length] = charOffset;
    visibleColumns[lineContent.length] = visibleColumn;
    sb.appendASCIIString('</div>');
    return [charOffsets, visibleColumns];
}
function readLineBreaks(range, lineDomNode, lineContent, charOffsets) {
    if (lineContent.length <= 1) {
        return null;
    }
    const spans = Array.prototype.slice.call(lineDomNode.children, 0);
    const breakOffsets = [];
    try {
        discoverBreaks(range, spans, charOffsets, 0, null, lineContent.length - 1, null, breakOffsets);
    }
    catch (err) {
        console.log(err);
        return null;
    }
    if (breakOffsets.length === 0) {
        return null;
    }
    breakOffsets.push(lineContent.length);
    return breakOffsets;
}
function discoverBreaks(range, spans, charOffsets, low, lowRects, high, highRects, result) {
    if (low === high) {
        return;
    }
    lowRects = lowRects || readClientRect(range, spans, charOffsets[low], charOffsets[low + 1]);
    highRects = highRects || readClientRect(range, spans, charOffsets[high], charOffsets[high + 1]);
    if (Math.abs(lowRects[0].top - highRects[0].top) <= 0.1) {
        // same line
        return;
    }
    // there is at least one line break between these two offsets
    if (low + 1 === high) {
        // the two characters are adjacent, so the line break must be exactly between them
        result.push(high);
        return;
    }
    const mid = low + ((high - low) / 2) | 0;
    const midRects = readClientRect(range, spans, charOffsets[mid], charOffsets[mid + 1]);
    discoverBreaks(range, spans, charOffsets, low, lowRects, mid, midRects, result);
    discoverBreaks(range, spans, charOffsets, mid, midRects, high, highRects, result);
}
function readClientRect(range, spans, startOffset, endOffset) {
    range.setStart(spans[(startOffset / 16384 /* SPAN_MODULO_LIMIT */) | 0].firstChild, startOffset % 16384 /* SPAN_MODULO_LIMIT */);
    range.setEnd(spans[(endOffset / 16384 /* SPAN_MODULO_LIMIT */) | 0].firstChild, endOffset % 16384 /* SPAN_MODULO_LIMIT */);
    return range.getClientRects();
}
