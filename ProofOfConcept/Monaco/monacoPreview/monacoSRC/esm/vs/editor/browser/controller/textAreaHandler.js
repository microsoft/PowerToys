/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './textAreaHandler.css';
import * as nls from '../../../nls.js';
import * as browser from '../../../base/browser/browser.js';
import { createFastDomNode } from '../../../base/browser/fastDomNode.js';
import * as platform from '../../../base/common/platform.js';
import * as strings from '../../../base/common/strings.js';
import { Configuration } from '../config/configuration.js';
import { CopyOptions, TextAreaInput } from './textAreaInput.js';
import { PagedScreenReaderStrategy, TextAreaState } from './textAreaState.js';
import { PartFingerprints, ViewPart } from '../view/viewPart.js';
import { LineNumbersOverlay } from '../viewParts/lineNumbers/lineNumbers.js';
import { Margin } from '../viewParts/margin/margin.js';
import { EditorOptions } from '../../common/config/editorOptions.js';
import { getMapForWordSeparators } from '../../common/controller/wordCharacterClassifier.js';
import { Position } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { MOUSE_CURSOR_TEXT_CSS_CLASS_NAME } from '../../../base/browser/ui/mouseCursor/mouseCursor.js';
class VisibleTextAreaData {
    constructor(top, left, width) {
        this.top = top;
        this.left = left;
        this.width = width;
    }
    setWidth(width) {
        return new VisibleTextAreaData(this.top, this.left, width);
    }
}
const canUseZeroSizeTextarea = (browser.isFirefox);
export class TextAreaHandler extends ViewPart {
    constructor(context, viewController, viewHelper) {
        super(context);
        // --- end view API
        this._primaryCursorPosition = new Position(1, 1);
        this._primaryCursorVisibleRange = null;
        this._viewController = viewController;
        this._viewHelper = viewHelper;
        this._scrollLeft = 0;
        this._scrollTop = 0;
        const options = this._context.configuration.options;
        const layoutInfo = options.get(124 /* layoutInfo */);
        this._setAccessibilityOptions(options);
        this._contentLeft = layoutInfo.contentLeft;
        this._contentWidth = layoutInfo.contentWidth;
        this._contentHeight = layoutInfo.height;
        this._fontInfo = options.get(38 /* fontInfo */);
        this._lineHeight = options.get(53 /* lineHeight */);
        this._emptySelectionClipboard = options.get(28 /* emptySelectionClipboard */);
        this._copyWithSyntaxHighlighting = options.get(18 /* copyWithSyntaxHighlighting */);
        this._visibleTextArea = null;
        this._selections = [new Selection(1, 1, 1, 1)];
        this._modelSelections = [new Selection(1, 1, 1, 1)];
        this._lastRenderPosition = null;
        // Text Area (The focus will always be in the textarea when the cursor is blinking)
        this.textArea = createFastDomNode(document.createElement('textarea'));
        PartFingerprints.write(this.textArea, 6 /* TextArea */);
        this.textArea.setClassName(`inputarea ${MOUSE_CURSOR_TEXT_CSS_CLASS_NAME}`);
        this.textArea.setAttribute('wrap', 'off');
        this.textArea.setAttribute('autocorrect', 'off');
        this.textArea.setAttribute('autocapitalize', 'off');
        this.textArea.setAttribute('autocomplete', 'off');
        this.textArea.setAttribute('spellcheck', 'false');
        this.textArea.setAttribute('aria-label', this._getAriaLabel(options));
        this.textArea.setAttribute('tabindex', String(options.get(107 /* tabIndex */)));
        this.textArea.setAttribute('role', 'textbox');
        this.textArea.setAttribute('aria-roledescription', nls.localize('editor', "editor"));
        this.textArea.setAttribute('aria-multiline', 'true');
        this.textArea.setAttribute('aria-haspopup', 'false');
        this.textArea.setAttribute('aria-autocomplete', 'both');
        if (platform.isWeb && options.get(75 /* readOnly */)) {
            this.textArea.setAttribute('readonly', 'true');
        }
        this.textAreaCover = createFastDomNode(document.createElement('div'));
        this.textAreaCover.setPosition('absolute');
        const simpleModel = {
            getLineCount: () => {
                return this._context.model.getLineCount();
            },
            getLineMaxColumn: (lineNumber) => {
                return this._context.model.getLineMaxColumn(lineNumber);
            },
            getValueInRange: (range, eol) => {
                return this._context.model.getValueInRange(range, eol);
            }
        };
        const textAreaInputHost = {
            getDataToCopy: (generateHTML) => {
                const rawTextToCopy = this._context.model.getPlainTextToCopy(this._modelSelections, this._emptySelectionClipboard, platform.isWindows);
                const newLineCharacter = this._context.model.getEOL();
                const isFromEmptySelection = (this._emptySelectionClipboard && this._modelSelections.length === 1 && this._modelSelections[0].isEmpty());
                const multicursorText = (Array.isArray(rawTextToCopy) ? rawTextToCopy : null);
                const text = (Array.isArray(rawTextToCopy) ? rawTextToCopy.join(newLineCharacter) : rawTextToCopy);
                let html = undefined;
                let mode = null;
                if (generateHTML) {
                    if (CopyOptions.forceCopyWithSyntaxHighlighting || (this._copyWithSyntaxHighlighting && text.length < 65536)) {
                        const richText = this._context.model.getRichTextToCopy(this._modelSelections, this._emptySelectionClipboard);
                        if (richText) {
                            html = richText.html;
                            mode = richText.mode;
                        }
                    }
                }
                return {
                    isFromEmptySelection,
                    multicursorText,
                    text,
                    html,
                    mode
                };
            },
            getScreenReaderContent: (currentState) => {
                if (this._accessibilitySupport === 1 /* Disabled */) {
                    // We know for a fact that a screen reader is not attached
                    // On OSX, we write the character before the cursor to allow for "long-press" composition
                    // Also on OSX, we write the word before the cursor to allow for the Accessibility Keyboard to give good hints
                    if (platform.isMacintosh) {
                        const selection = this._selections[0];
                        if (selection.isEmpty()) {
                            const position = selection.getStartPosition();
                            let textBefore = this._getWordBeforePosition(position);
                            if (textBefore.length === 0) {
                                textBefore = this._getCharacterBeforePosition(position);
                            }
                            if (textBefore.length > 0) {
                                return new TextAreaState(textBefore, textBefore.length, textBefore.length, position, position);
                            }
                        }
                    }
                    return TextAreaState.EMPTY;
                }
                return PagedScreenReaderStrategy.fromEditorSelection(currentState, simpleModel, this._selections[0], this._accessibilityPageSize, this._accessibilitySupport === 0 /* Unknown */);
            },
            deduceModelPosition: (viewAnchorPosition, deltaOffset, lineFeedCnt) => {
                return this._context.model.deduceModelPositionRelativeToViewPosition(viewAnchorPosition, deltaOffset, lineFeedCnt);
            }
        };
        this._textAreaInput = this._register(new TextAreaInput(textAreaInputHost, this.textArea));
        this._register(this._textAreaInput.onKeyDown((e) => {
            this._viewController.emitKeyDown(e);
        }));
        this._register(this._textAreaInput.onKeyUp((e) => {
            this._viewController.emitKeyUp(e);
        }));
        this._register(this._textAreaInput.onPaste((e) => {
            let pasteOnNewLine = false;
            let multicursorText = null;
            let mode = null;
            if (e.metadata) {
                pasteOnNewLine = (this._emptySelectionClipboard && !!e.metadata.isFromEmptySelection);
                multicursorText = (typeof e.metadata.multicursorText !== 'undefined' ? e.metadata.multicursorText : null);
                mode = e.metadata.mode;
            }
            this._viewController.paste(e.text, pasteOnNewLine, multicursorText, mode);
        }));
        this._register(this._textAreaInput.onCut(() => {
            this._viewController.cut();
        }));
        this._register(this._textAreaInput.onType((e) => {
            if (e.replaceCharCnt) {
                this._viewController.replacePreviousChar(e.text, e.replaceCharCnt);
            }
            else {
                this._viewController.type(e.text);
            }
        }));
        this._register(this._textAreaInput.onSelectionChangeRequest((modelSelection) => {
            this._viewController.setSelection(modelSelection);
        }));
        this._register(this._textAreaInput.onCompositionStart((e) => {
            const lineNumber = this._selections[0].startLineNumber;
            const column = this._selections[0].startColumn - (e.moveOneCharacterLeft ? 1 : 0);
            this._context.model.revealRange('keyboard', true, new Range(lineNumber, column, lineNumber, column), 0 /* Simple */, 1 /* Immediate */);
            // Find range pixel position
            const visibleRange = this._viewHelper.visibleRangeForPositionRelativeToEditor(lineNumber, column);
            if (visibleRange) {
                this._visibleTextArea = new VisibleTextAreaData(this._context.viewLayout.getVerticalOffsetForLineNumber(lineNumber), visibleRange.left, canUseZeroSizeTextarea ? 0 : 1);
                this._render();
            }
            // Show the textarea
            this.textArea.setClassName(`inputarea ${MOUSE_CURSOR_TEXT_CSS_CLASS_NAME} ime-input`);
            this._viewController.compositionStart();
            this._context.model.onCompositionStart();
        }));
        this._register(this._textAreaInput.onCompositionUpdate((e) => {
            // adjust width by its size
            this._visibleTextArea = this._visibleTextArea.setWidth(measureText(e.data, this._fontInfo));
            this._render();
        }));
        this._register(this._textAreaInput.onCompositionEnd(() => {
            this._visibleTextArea = null;
            this._render();
            this.textArea.setClassName(`inputarea ${MOUSE_CURSOR_TEXT_CSS_CLASS_NAME}`);
            this._viewController.compositionEnd();
            this._context.model.onCompositionEnd();
        }));
        this._register(this._textAreaInput.onFocus(() => {
            this._context.model.setHasFocus(true);
        }));
        this._register(this._textAreaInput.onBlur(() => {
            this._context.model.setHasFocus(false);
        }));
    }
    dispose() {
        super.dispose();
    }
    _getWordBeforePosition(position) {
        const lineContent = this._context.model.getLineContent(position.lineNumber);
        const wordSeparators = getMapForWordSeparators(this._context.configuration.options.get(110 /* wordSeparators */));
        let column = position.column;
        let distance = 0;
        while (column > 1) {
            const charCode = lineContent.charCodeAt(column - 2);
            const charClass = wordSeparators.get(charCode);
            if (charClass !== 0 /* Regular */ || distance > 50) {
                return lineContent.substring(column - 1, position.column - 1);
            }
            distance++;
            column--;
        }
        return lineContent.substring(0, position.column - 1);
    }
    _getCharacterBeforePosition(position) {
        if (position.column > 1) {
            const lineContent = this._context.model.getLineContent(position.lineNumber);
            const charBefore = lineContent.charAt(position.column - 2);
            if (!strings.isHighSurrogate(charBefore.charCodeAt(0))) {
                return charBefore;
            }
        }
        return '';
    }
    _getAriaLabel(options) {
        const accessibilitySupport = options.get(2 /* accessibilitySupport */);
        if (accessibilitySupport === 1 /* Disabled */) {
            return nls.localize('accessibilityOffAriaLabel', "The editor is not accessible at this time. Press {0} for options.", platform.isLinux ? 'Shift+Alt+F1' : 'Alt+F1');
        }
        return options.get(4 /* ariaLabel */);
    }
    _setAccessibilityOptions(options) {
        this._accessibilitySupport = options.get(2 /* accessibilitySupport */);
        const accessibilityPageSize = options.get(3 /* accessibilityPageSize */);
        if (this._accessibilitySupport === 2 /* Enabled */ && accessibilityPageSize === EditorOptions.accessibilityPageSize.defaultValue) {
            // If a screen reader is attached and the default value is not set we shuold automatically increase the page size to 100 for a better experience
            // If we put more than 100 lines the nvda can not handle this https://github.com/microsoft/vscode/issues/89717
            this._accessibilityPageSize = 100;
        }
        else {
            this._accessibilityPageSize = accessibilityPageSize;
        }
    }
    // --- begin event handlers
    onConfigurationChanged(e) {
        const options = this._context.configuration.options;
        const layoutInfo = options.get(124 /* layoutInfo */);
        this._setAccessibilityOptions(options);
        this._contentLeft = layoutInfo.contentLeft;
        this._contentWidth = layoutInfo.contentWidth;
        this._contentHeight = layoutInfo.height;
        this._fontInfo = options.get(38 /* fontInfo */);
        this._lineHeight = options.get(53 /* lineHeight */);
        this._emptySelectionClipboard = options.get(28 /* emptySelectionClipboard */);
        this._copyWithSyntaxHighlighting = options.get(18 /* copyWithSyntaxHighlighting */);
        this.textArea.setAttribute('aria-label', this._getAriaLabel(options));
        this.textArea.setAttribute('tabindex', String(options.get(107 /* tabIndex */)));
        if (platform.isWeb && e.hasChanged(75 /* readOnly */)) {
            if (options.get(75 /* readOnly */)) {
                this.textArea.setAttribute('readonly', 'true');
            }
            else {
                this.textArea.removeAttribute('readonly');
            }
        }
        if (e.hasChanged(2 /* accessibilitySupport */)) {
            this._textAreaInput.writeScreenReaderContent('strategy changed');
        }
        return true;
    }
    onCursorStateChanged(e) {
        this._selections = e.selections.slice(0);
        this._modelSelections = e.modelSelections.slice(0);
        this._textAreaInput.writeScreenReaderContent('selection changed');
        return true;
    }
    onDecorationsChanged(e) {
        // true for inline decorations that can end up relayouting text
        return true;
    }
    onFlushed(e) {
        return true;
    }
    onLinesChanged(e) {
        return true;
    }
    onLinesDeleted(e) {
        return true;
    }
    onLinesInserted(e) {
        return true;
    }
    onScrollChanged(e) {
        this._scrollLeft = e.scrollLeft;
        this._scrollTop = e.scrollTop;
        return true;
    }
    onZonesChanged(e) {
        return true;
    }
    // --- end event handlers
    // --- begin view API
    isFocused() {
        return this._textAreaInput.isFocused();
    }
    focusTextArea() {
        this._textAreaInput.focusTextArea();
    }
    getLastRenderData() {
        return this._lastRenderPosition;
    }
    setAriaOptions(options) {
        if (options.activeDescendant) {
            this.textArea.setAttribute('aria-haspopup', 'true');
            this.textArea.setAttribute('aria-autocomplete', 'list');
            this.textArea.setAttribute('aria-activedescendant', options.activeDescendant);
        }
        else {
            this.textArea.setAttribute('aria-haspopup', 'false');
            this.textArea.setAttribute('aria-autocomplete', 'both');
            this.textArea.removeAttribute('aria-activedescendant');
        }
        if (options.role) {
            this.textArea.setAttribute('role', options.role);
        }
    }
    prepareRender(ctx) {
        this._primaryCursorPosition = new Position(this._selections[0].positionLineNumber, this._selections[0].positionColumn);
        this._primaryCursorVisibleRange = ctx.visibleRangeForPosition(this._primaryCursorPosition);
    }
    render(ctx) {
        this._textAreaInput.writeScreenReaderContent('render');
        this._render();
    }
    _render() {
        if (this._visibleTextArea) {
            // The text area is visible for composition reasons
            this._renderInsideEditor(null, this._visibleTextArea.top - this._scrollTop, this._contentLeft + this._visibleTextArea.left - this._scrollLeft, this._visibleTextArea.width, this._lineHeight);
            return;
        }
        if (!this._primaryCursorVisibleRange) {
            // The primary cursor is outside the viewport => place textarea to the top left
            this._renderAtTopLeft();
            return;
        }
        const left = this._contentLeft + this._primaryCursorVisibleRange.left - this._scrollLeft;
        if (left < this._contentLeft || left > this._contentLeft + this._contentWidth) {
            // cursor is outside the viewport
            this._renderAtTopLeft();
            return;
        }
        const top = this._context.viewLayout.getVerticalOffsetForLineNumber(this._selections[0].positionLineNumber) - this._scrollTop;
        if (top < 0 || top > this._contentHeight) {
            // cursor is outside the viewport
            this._renderAtTopLeft();
            return;
        }
        // The primary cursor is in the viewport (at least vertically) => place textarea on the cursor
        if (platform.isMacintosh) {
            // For the popup emoji input, we will make the text area as high as the line height
            // We will also make the fontSize and lineHeight the correct dimensions to help with the placement of these pickers
            this._renderInsideEditor(this._primaryCursorPosition, top, left, canUseZeroSizeTextarea ? 0 : 1, this._lineHeight);
            return;
        }
        this._renderInsideEditor(this._primaryCursorPosition, top, left, canUseZeroSizeTextarea ? 0 : 1, canUseZeroSizeTextarea ? 0 : 1);
    }
    _renderInsideEditor(renderedPosition, top, left, width, height) {
        this._lastRenderPosition = renderedPosition;
        const ta = this.textArea;
        const tac = this.textAreaCover;
        Configuration.applyFontInfo(ta, this._fontInfo);
        ta.setTop(top);
        ta.setLeft(left);
        ta.setWidth(width);
        ta.setHeight(height);
        tac.setTop(0);
        tac.setLeft(0);
        tac.setWidth(0);
        tac.setHeight(0);
    }
    _renderAtTopLeft() {
        this._lastRenderPosition = null;
        const ta = this.textArea;
        const tac = this.textAreaCover;
        Configuration.applyFontInfo(ta, this._fontInfo);
        ta.setTop(0);
        ta.setLeft(0);
        tac.setTop(0);
        tac.setLeft(0);
        if (canUseZeroSizeTextarea) {
            ta.setWidth(0);
            ta.setHeight(0);
            tac.setWidth(0);
            tac.setHeight(0);
            return;
        }
        // (in WebKit the textarea is 1px by 1px because it cannot handle input to a 0x0 textarea)
        // specifically, when doing Korean IME, setting the textarea to 0x0 breaks IME badly.
        ta.setWidth(1);
        ta.setHeight(1);
        tac.setWidth(1);
        tac.setHeight(1);
        const options = this._context.configuration.options;
        if (options.get(44 /* glyphMargin */)) {
            tac.setClassName('monaco-editor-background textAreaCover ' + Margin.OUTER_CLASS_NAME);
        }
        else {
            if (options.get(54 /* lineNumbers */).renderType !== 0 /* Off */) {
                tac.setClassName('monaco-editor-background textAreaCover ' + LineNumbersOverlay.CLASS_NAME);
            }
            else {
                tac.setClassName('monaco-editor-background textAreaCover');
            }
        }
    }
}
function measureText(text, fontInfo) {
    // adjust width by its size
    const canvasElem = document.createElement('canvas');
    const context = canvasElem.getContext('2d');
    context.font = createFontString(fontInfo);
    const metrics = context.measureText(text);
    if (browser.isFirefox) {
        return metrics.width + 2; // +2 for Japanese...
    }
    else {
        return metrics.width;
    }
}
function createFontString(bareFontInfo) {
    return doCreateFontString('normal', bareFontInfo.fontWeight, bareFontInfo.fontSize, bareFontInfo.lineHeight, bareFontInfo.fontFamily);
}
function doCreateFontString(fontStyle, fontWeight, fontSize, lineHeight, fontFamily) {
    // The full font syntax is:
    // style | variant | weight | stretch | size/line-height | fontFamily
    // (https://developer.mozilla.org/en-US/docs/Web/CSS/font)
    // But it appears Edge and IE11 cannot properly parse `stretch`.
    return `${fontStyle} normal ${fontWeight} ${fontSize}px / ${lineHeight}px ${fontFamily}`;
}
