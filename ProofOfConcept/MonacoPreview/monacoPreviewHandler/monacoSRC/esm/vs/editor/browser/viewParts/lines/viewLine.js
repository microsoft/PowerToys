/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as browser from '../../../../base/browser/browser.js';
import { createFastDomNode } from '../../../../base/browser/fastDomNode.js';
import * as platform from '../../../../base/common/platform.js';
import { RangeUtil } from './rangeUtil.js';
import { HorizontalRange, VisibleRanges } from '../../../common/view/renderingContext.js';
import { LineDecoration } from '../../../common/viewLayout/lineDecorations.js';
import { CharacterMapping, RenderLineInput, renderViewLine, LineRange } from '../../../common/viewLayout/viewLineRenderer.js';
import { ColorScheme } from '../../../../platform/theme/common/theme.js';
import { EditorFontLigatures } from '../../../common/config/editorOptions.js';
const canUseFastRenderedViewLine = (function () {
    if (platform.isNative) {
        // In VSCode we know very well when the zoom level changes
        return true;
    }
    if (platform.isLinux || browser.isFirefox || browser.isSafari) {
        // On Linux, it appears that zooming affects char widths (in pixels), which is unexpected.
        // --
        // Even though we read character widths correctly, having read them at a specific zoom level
        // does not mean they are the same at the current zoom level.
        // --
        // This could be improved if we ever figure out how to get an event when browsers zoom,
        // but until then we have to stick with reading client rects.
        // --
        // The same has been observed with Firefox on Windows7
        // --
        // The same has been oversved with Safari
        return false;
    }
    return true;
})();
let monospaceAssumptionsAreValid = true;
const alwaysRenderInlineSelection = (browser.isEdgeLegacy);
export class DomReadingContext {
    constructor(domNode, endNode) {
        this._domNode = domNode;
        this._clientRectDeltaLeft = 0;
        this._clientRectDeltaLeftRead = false;
        this.endNode = endNode;
    }
    get clientRectDeltaLeft() {
        if (!this._clientRectDeltaLeftRead) {
            this._clientRectDeltaLeftRead = true;
            this._clientRectDeltaLeft = this._domNode.getBoundingClientRect().left;
        }
        return this._clientRectDeltaLeft;
    }
}
export class ViewLineOptions {
    constructor(config, themeType) {
        this.themeType = themeType;
        const options = config.options;
        const fontInfo = options.get(38 /* fontInfo */);
        this.renderWhitespace = options.get(83 /* renderWhitespace */);
        this.renderControlCharacters = options.get(77 /* renderControlCharacters */);
        this.spaceWidth = fontInfo.spaceWidth;
        this.middotWidth = fontInfo.middotWidth;
        this.wsmiddotWidth = fontInfo.wsmiddotWidth;
        this.useMonospaceOptimizations = (fontInfo.isMonospace
            && !options.get(26 /* disableMonospaceOptimizations */));
        this.canUseHalfwidthRightwardsArrow = fontInfo.canUseHalfwidthRightwardsArrow;
        this.lineHeight = options.get(53 /* lineHeight */);
        this.stopRenderingLineAfter = options.get(100 /* stopRenderingLineAfter */);
        this.fontLigatures = options.get(39 /* fontLigatures */);
    }
    equals(other) {
        return (this.themeType === other.themeType
            && this.renderWhitespace === other.renderWhitespace
            && this.renderControlCharacters === other.renderControlCharacters
            && this.spaceWidth === other.spaceWidth
            && this.middotWidth === other.middotWidth
            && this.wsmiddotWidth === other.wsmiddotWidth
            && this.useMonospaceOptimizations === other.useMonospaceOptimizations
            && this.canUseHalfwidthRightwardsArrow === other.canUseHalfwidthRightwardsArrow
            && this.lineHeight === other.lineHeight
            && this.stopRenderingLineAfter === other.stopRenderingLineAfter
            && this.fontLigatures === other.fontLigatures);
    }
}
export class ViewLine {
    constructor(options) {
        this._options = options;
        this._isMaybeInvalid = true;
        this._renderedViewLine = null;
    }
    // --- begin IVisibleLineData
    getDomNode() {
        if (this._renderedViewLine && this._renderedViewLine.domNode) {
            return this._renderedViewLine.domNode.domNode;
        }
        return null;
    }
    setDomNode(domNode) {
        if (this._renderedViewLine) {
            this._renderedViewLine.domNode = createFastDomNode(domNode);
        }
        else {
            throw new Error('I have no rendered view line to set the dom node to...');
        }
    }
    onContentChanged() {
        this._isMaybeInvalid = true;
    }
    onTokensChanged() {
        this._isMaybeInvalid = true;
    }
    onDecorationsChanged() {
        this._isMaybeInvalid = true;
    }
    onOptionsChanged(newOptions) {
        this._isMaybeInvalid = true;
        this._options = newOptions;
    }
    onSelectionChanged() {
        if (alwaysRenderInlineSelection || this._options.themeType === ColorScheme.HIGH_CONTRAST || this._options.renderWhitespace === 'selection') {
            this._isMaybeInvalid = true;
            return true;
        }
        return false;
    }
    renderLine(lineNumber, deltaTop, viewportData, sb) {
        if (this._isMaybeInvalid === false) {
            // it appears that nothing relevant has changed
            return false;
        }
        this._isMaybeInvalid = false;
        const lineData = viewportData.getViewLineRenderingData(lineNumber);
        const options = this._options;
        const actualInlineDecorations = LineDecoration.filter(lineData.inlineDecorations, lineNumber, lineData.minColumn, lineData.maxColumn);
        // Only send selection information when needed for rendering whitespace
        let selectionsOnLine = null;
        if (alwaysRenderInlineSelection || options.themeType === ColorScheme.HIGH_CONTRAST || this._options.renderWhitespace === 'selection') {
            const selections = viewportData.selections;
            for (const selection of selections) {
                if (selection.endLineNumber < lineNumber || selection.startLineNumber > lineNumber) {
                    // Selection does not intersect line
                    continue;
                }
                const startColumn = (selection.startLineNumber === lineNumber ? selection.startColumn : lineData.minColumn);
                const endColumn = (selection.endLineNumber === lineNumber ? selection.endColumn : lineData.maxColumn);
                if (startColumn < endColumn) {
                    if (options.themeType === ColorScheme.HIGH_CONTRAST || this._options.renderWhitespace !== 'selection') {
                        actualInlineDecorations.push(new LineDecoration(startColumn, endColumn, 'inline-selected-text', 0 /* Regular */));
                    }
                    else {
                        if (!selectionsOnLine) {
                            selectionsOnLine = [];
                        }
                        selectionsOnLine.push(new LineRange(startColumn - 1, endColumn - 1));
                    }
                }
            }
        }
        const renderLineInput = new RenderLineInput(options.useMonospaceOptimizations, options.canUseHalfwidthRightwardsArrow, lineData.content, lineData.continuesWithWrappedLine, lineData.isBasicASCII, lineData.containsRTL, lineData.minColumn - 1, lineData.tokens, actualInlineDecorations, lineData.tabSize, lineData.startVisibleColumn, options.spaceWidth, options.middotWidth, options.wsmiddotWidth, options.stopRenderingLineAfter, options.renderWhitespace, options.renderControlCharacters, options.fontLigatures !== EditorFontLigatures.OFF, selectionsOnLine);
        if (this._renderedViewLine && this._renderedViewLine.input.equals(renderLineInput)) {
            // no need to do anything, we have the same render input
            return false;
        }
        sb.appendASCIIString('<div style="top:');
        sb.appendASCIIString(String(deltaTop));
        sb.appendASCIIString('px;height:');
        sb.appendASCIIString(String(this._options.lineHeight));
        sb.appendASCIIString('px;" class="');
        sb.appendASCIIString(ViewLine.CLASS_NAME);
        sb.appendASCIIString('">');
        const output = renderViewLine(renderLineInput, sb);
        sb.appendASCIIString('</div>');
        let renderedViewLine = null;
        if (monospaceAssumptionsAreValid && canUseFastRenderedViewLine && lineData.isBasicASCII && options.useMonospaceOptimizations && output.containsForeignElements === 0 /* None */) {
            if (lineData.content.length < 300 && renderLineInput.lineTokens.getCount() < 100) {
                // Browser rounding errors have been observed in Chrome and IE, so using the fast
                // view line only for short lines. Please test before removing the length check...
                // ---
                // Another rounding error has been observed on Linux in VSCode, where <span> width
                // rounding errors add up to an observable large number...
                // ---
                // Also see another example of rounding errors on Windows in
                // https://github.com/microsoft/vscode/issues/33178
                renderedViewLine = new FastRenderedViewLine(this._renderedViewLine ? this._renderedViewLine.domNode : null, renderLineInput, output.characterMapping);
            }
        }
        if (!renderedViewLine) {
            renderedViewLine = createRenderedLine(this._renderedViewLine ? this._renderedViewLine.domNode : null, renderLineInput, output.characterMapping, output.containsRTL, output.containsForeignElements);
        }
        this._renderedViewLine = renderedViewLine;
        return true;
    }
    layoutLine(lineNumber, deltaTop) {
        if (this._renderedViewLine && this._renderedViewLine.domNode) {
            this._renderedViewLine.domNode.setTop(deltaTop);
            this._renderedViewLine.domNode.setHeight(this._options.lineHeight);
        }
    }
    // --- end IVisibleLineData
    getWidth() {
        if (!this._renderedViewLine) {
            return 0;
        }
        return this._renderedViewLine.getWidth();
    }
    getWidthIsFast() {
        if (!this._renderedViewLine) {
            return true;
        }
        return this._renderedViewLine.getWidthIsFast();
    }
    needsMonospaceFontCheck() {
        if (!this._renderedViewLine) {
            return false;
        }
        return (this._renderedViewLine instanceof FastRenderedViewLine);
    }
    monospaceAssumptionsAreValid() {
        if (!this._renderedViewLine) {
            return monospaceAssumptionsAreValid;
        }
        if (this._renderedViewLine instanceof FastRenderedViewLine) {
            return this._renderedViewLine.monospaceAssumptionsAreValid();
        }
        return monospaceAssumptionsAreValid;
    }
    onMonospaceAssumptionsInvalidated() {
        if (this._renderedViewLine && this._renderedViewLine instanceof FastRenderedViewLine) {
            this._renderedViewLine = this._renderedViewLine.toSlowRenderedLine();
        }
    }
    getVisibleRangesForRange(startColumn, endColumn, context) {
        if (!this._renderedViewLine) {
            return null;
        }
        startColumn = startColumn | 0; // @perf
        endColumn = endColumn | 0; // @perf
        startColumn = Math.min(this._renderedViewLine.input.lineContent.length + 1, Math.max(1, startColumn));
        endColumn = Math.min(this._renderedViewLine.input.lineContent.length + 1, Math.max(1, endColumn));
        const stopRenderingLineAfter = this._renderedViewLine.input.stopRenderingLineAfter | 0; // @perf
        let outsideRenderedLine = false;
        if (stopRenderingLineAfter !== -1 && startColumn > stopRenderingLineAfter + 1 && endColumn > stopRenderingLineAfter + 1) {
            // This range is obviously not visible
            outsideRenderedLine = true;
        }
        if (stopRenderingLineAfter !== -1 && startColumn > stopRenderingLineAfter + 1) {
            startColumn = stopRenderingLineAfter + 1;
        }
        if (stopRenderingLineAfter !== -1 && endColumn > stopRenderingLineAfter + 1) {
            endColumn = stopRenderingLineAfter + 1;
        }
        const horizontalRanges = this._renderedViewLine.getVisibleRangesForRange(startColumn, endColumn, context);
        if (horizontalRanges && horizontalRanges.length > 0) {
            return new VisibleRanges(outsideRenderedLine, horizontalRanges);
        }
        return null;
    }
    getColumnOfNodeOffset(lineNumber, spanNode, offset) {
        if (!this._renderedViewLine) {
            return 1;
        }
        return this._renderedViewLine.getColumnOfNodeOffset(lineNumber, spanNode, offset);
    }
}
ViewLine.CLASS_NAME = 'view-line';
/**
 * A rendered line which is guaranteed to contain only regular ASCII and is rendered with a monospace font.
 */
class FastRenderedViewLine {
    constructor(domNode, renderLineInput, characterMapping) {
        this.domNode = domNode;
        this.input = renderLineInput;
        this._characterMapping = characterMapping;
        this._charWidth = renderLineInput.spaceWidth;
    }
    getWidth() {
        return this._getCharPosition(this._characterMapping.length);
    }
    getWidthIsFast() {
        return true;
    }
    monospaceAssumptionsAreValid() {
        if (!this.domNode) {
            return monospaceAssumptionsAreValid;
        }
        const expectedWidth = this.getWidth();
        const actualWidth = this.domNode.domNode.firstChild.offsetWidth;
        if (Math.abs(expectedWidth - actualWidth) >= 2) {
            // more than 2px off
            console.warn(`monospace assumptions have been violated, therefore disabling monospace optimizations!`);
            monospaceAssumptionsAreValid = false;
        }
        return monospaceAssumptionsAreValid;
    }
    toSlowRenderedLine() {
        return createRenderedLine(this.domNode, this.input, this._characterMapping, false, 0 /* None */);
    }
    getVisibleRangesForRange(startColumn, endColumn, context) {
        const startPosition = this._getCharPosition(startColumn);
        const endPosition = this._getCharPosition(endColumn);
        return [new HorizontalRange(startPosition, endPosition - startPosition)];
    }
    _getCharPosition(column) {
        const charOffset = this._characterMapping.getAbsoluteOffsets();
        if (charOffset.length === 0) {
            // No characters on this line
            return 0;
        }
        return Math.round(this._charWidth * charOffset[column - 1]);
    }
    getColumnOfNodeOffset(lineNumber, spanNode, offset) {
        const spanNodeTextContentLength = spanNode.textContent.length;
        let spanIndex = -1;
        while (spanNode) {
            spanNode = spanNode.previousSibling;
            spanIndex++;
        }
        const charOffset = this._characterMapping.partDataToCharOffset(spanIndex, spanNodeTextContentLength, offset);
        return charOffset + 1;
    }
}
/**
 * Every time we render a line, we save what we have rendered in an instance of this class.
 */
class RenderedViewLine {
    constructor(domNode, renderLineInput, characterMapping, containsRTL, containsForeignElements) {
        this.domNode = domNode;
        this.input = renderLineInput;
        this._characterMapping = characterMapping;
        this._isWhitespaceOnly = /^\s*$/.test(renderLineInput.lineContent);
        this._containsForeignElements = containsForeignElements;
        this._cachedWidth = -1;
        this._pixelOffsetCache = null;
        if (!containsRTL || this._characterMapping.length === 0 /* the line is empty */) {
            this._pixelOffsetCache = new Int32Array(Math.max(2, this._characterMapping.length + 1));
            for (let column = 0, len = this._characterMapping.length; column <= len; column++) {
                this._pixelOffsetCache[column] = -1;
            }
        }
    }
    // --- Reading from the DOM methods
    _getReadingTarget(myDomNode) {
        return myDomNode.domNode.firstChild;
    }
    /**
     * Width of the line in pixels
     */
    getWidth() {
        if (!this.domNode) {
            return 0;
        }
        if (this._cachedWidth === -1) {
            this._cachedWidth = this._getReadingTarget(this.domNode).offsetWidth;
        }
        return this._cachedWidth;
    }
    getWidthIsFast() {
        if (this._cachedWidth === -1) {
            return false;
        }
        return true;
    }
    /**
     * Visible ranges for a model range
     */
    getVisibleRangesForRange(startColumn, endColumn, context) {
        if (!this.domNode) {
            return null;
        }
        if (this._pixelOffsetCache !== null) {
            // the text is LTR
            const startOffset = this._readPixelOffset(this.domNode, startColumn, context);
            if (startOffset === -1) {
                return null;
            }
            const endOffset = this._readPixelOffset(this.domNode, endColumn, context);
            if (endOffset === -1) {
                return null;
            }
            return [new HorizontalRange(startOffset, endOffset - startOffset)];
        }
        return this._readVisibleRangesForRange(this.domNode, startColumn, endColumn, context);
    }
    _readVisibleRangesForRange(domNode, startColumn, endColumn, context) {
        if (startColumn === endColumn) {
            const pixelOffset = this._readPixelOffset(domNode, startColumn, context);
            if (pixelOffset === -1) {
                return null;
            }
            else {
                return [new HorizontalRange(pixelOffset, 0)];
            }
        }
        else {
            return this._readRawVisibleRangesForRange(domNode, startColumn, endColumn, context);
        }
    }
    _readPixelOffset(domNode, column, context) {
        if (this._characterMapping.length === 0) {
            // This line has no content
            if (this._containsForeignElements === 0 /* None */) {
                // We can assume the line is really empty
                return 0;
            }
            if (this._containsForeignElements === 2 /* After */) {
                // We have foreign elements after the (empty) line
                return 0;
            }
            if (this._containsForeignElements === 1 /* Before */) {
                // We have foreign elements before the (empty) line
                return this.getWidth();
            }
            // We have foreign elements before & after the (empty) line
            const readingTarget = this._getReadingTarget(domNode);
            if (readingTarget.firstChild) {
                return readingTarget.firstChild.offsetWidth;
            }
            else {
                return 0;
            }
        }
        if (this._pixelOffsetCache !== null) {
            // the text is LTR
            const cachedPixelOffset = this._pixelOffsetCache[column];
            if (cachedPixelOffset !== -1) {
                return cachedPixelOffset;
            }
            const result = this._actualReadPixelOffset(domNode, column, context);
            this._pixelOffsetCache[column] = result;
            return result;
        }
        return this._actualReadPixelOffset(domNode, column, context);
    }
    _actualReadPixelOffset(domNode, column, context) {
        if (this._characterMapping.length === 0) {
            // This line has no content
            const r = RangeUtil.readHorizontalRanges(this._getReadingTarget(domNode), 0, 0, 0, 0, context.clientRectDeltaLeft, context.endNode);
            if (!r || r.length === 0) {
                return -1;
            }
            return r[0].left;
        }
        if (column === this._characterMapping.length && this._isWhitespaceOnly && this._containsForeignElements === 0 /* None */) {
            // This branch helps in the case of whitespace only lines which have a width set
            return this.getWidth();
        }
        const partData = this._characterMapping.charOffsetToPartData(column - 1);
        const partIndex = CharacterMapping.getPartIndex(partData);
        const charOffsetInPart = CharacterMapping.getCharIndex(partData);
        const r = RangeUtil.readHorizontalRanges(this._getReadingTarget(domNode), partIndex, charOffsetInPart, partIndex, charOffsetInPart, context.clientRectDeltaLeft, context.endNode);
        if (!r || r.length === 0) {
            return -1;
        }
        const result = r[0].left;
        if (this.input.isBasicASCII) {
            const charOffset = this._characterMapping.getAbsoluteOffsets();
            const expectedResult = Math.round(this.input.spaceWidth * charOffset[column - 1]);
            if (Math.abs(expectedResult - result) <= 1) {
                return expectedResult;
            }
        }
        return result;
    }
    _readRawVisibleRangesForRange(domNode, startColumn, endColumn, context) {
        if (startColumn === 1 && endColumn === this._characterMapping.length) {
            // This branch helps IE with bidi text & gives a performance boost to other browsers when reading visible ranges for an entire line
            return [new HorizontalRange(0, this.getWidth())];
        }
        const startPartData = this._characterMapping.charOffsetToPartData(startColumn - 1);
        const startPartIndex = CharacterMapping.getPartIndex(startPartData);
        const startCharOffsetInPart = CharacterMapping.getCharIndex(startPartData);
        const endPartData = this._characterMapping.charOffsetToPartData(endColumn - 1);
        const endPartIndex = CharacterMapping.getPartIndex(endPartData);
        const endCharOffsetInPart = CharacterMapping.getCharIndex(endPartData);
        return RangeUtil.readHorizontalRanges(this._getReadingTarget(domNode), startPartIndex, startCharOffsetInPart, endPartIndex, endCharOffsetInPart, context.clientRectDeltaLeft, context.endNode);
    }
    /**
     * Returns the column for the text found at a specific offset inside a rendered dom node
     */
    getColumnOfNodeOffset(lineNumber, spanNode, offset) {
        const spanNodeTextContentLength = spanNode.textContent.length;
        let spanIndex = -1;
        while (spanNode) {
            spanNode = spanNode.previousSibling;
            spanIndex++;
        }
        const charOffset = this._characterMapping.partDataToCharOffset(spanIndex, spanNodeTextContentLength, offset);
        return charOffset + 1;
    }
}
class WebKitRenderedViewLine extends RenderedViewLine {
    _readVisibleRangesForRange(domNode, startColumn, endColumn, context) {
        const output = super._readVisibleRangesForRange(domNode, startColumn, endColumn, context);
        if (!output || output.length === 0 || startColumn === endColumn || (startColumn === 1 && endColumn === this._characterMapping.length)) {
            return output;
        }
        // WebKit is buggy and returns an expanded range (to contain words in some cases)
        // The last client rect is enlarged (I think)
        if (!this.input.containsRTL) {
            // This is an attempt to patch things up
            // Find position of last column
            const endPixelOffset = this._readPixelOffset(domNode, endColumn, context);
            if (endPixelOffset !== -1) {
                const lastRange = output[output.length - 1];
                if (lastRange.left < endPixelOffset) {
                    // Trim down the width of the last visible range to not go after the last column's position
                    lastRange.width = endPixelOffset - lastRange.left;
                }
            }
        }
        return output;
    }
}
const createRenderedLine = (function () {
    if (browser.isWebKit) {
        return createWebKitRenderedLine;
    }
    return createNormalRenderedLine;
})();
function createWebKitRenderedLine(domNode, renderLineInput, characterMapping, containsRTL, containsForeignElements) {
    return new WebKitRenderedViewLine(domNode, renderLineInput, characterMapping, containsRTL, containsForeignElements);
}
function createNormalRenderedLine(domNode, renderLineInput, characterMapping, containsRTL, containsForeignElements) {
    return new RenderedViewLine(domNode, renderLineInput, characterMapping, containsRTL, containsForeignElements);
}
