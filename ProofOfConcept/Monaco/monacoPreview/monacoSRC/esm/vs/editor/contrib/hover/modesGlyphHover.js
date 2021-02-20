/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { $ } from '../../../base/browser/dom.js';
import { isEmptyMarkdownString } from '../../../base/common/htmlContent.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import { HoverOperation } from './hoverOperation.js';
import { GlyphHoverWidget } from './hoverWidgets.js';
import { MarkdownRenderer } from '../../browser/core/markdownRenderer.js';
import { NullOpenerService } from '../../../platform/opener/common/opener.js';
import { asArray } from '../../../base/common/arrays.js';
class MarginComputer {
    constructor(editor) {
        this._editor = editor;
        this._lineNumber = -1;
        this._result = [];
    }
    setLineNumber(lineNumber) {
        this._lineNumber = lineNumber;
        this._result = [];
    }
    clearResult() {
        this._result = [];
    }
    computeSync() {
        const toHoverMessage = (contents) => {
            return {
                value: contents
            };
        };
        const lineDecorations = this._editor.getLineDecorations(this._lineNumber);
        const result = [];
        if (!lineDecorations) {
            return result;
        }
        for (const d of lineDecorations) {
            if (!d.options.glyphMarginClassName) {
                continue;
            }
            const hoverMessage = d.options.glyphMarginHoverMessage;
            if (!hoverMessage || isEmptyMarkdownString(hoverMessage)) {
                continue;
            }
            result.push(...asArray(hoverMessage).map(toHoverMessage));
        }
        return result;
    }
    onResult(result, isFromSynchronousComputation) {
        this._result = this._result.concat(result);
    }
    getResult() {
        return this._result;
    }
    getResultWithLoadingMessage() {
        return this.getResult();
    }
}
export class ModesGlyphHoverWidget extends GlyphHoverWidget {
    constructor(editor, modeService, openerService = NullOpenerService) {
        super(ModesGlyphHoverWidget.ID, editor);
        this._renderDisposeables = this._register(new DisposableStore());
        this._messages = [];
        this._lastLineNumber = -1;
        this._markdownRenderer = this._register(new MarkdownRenderer({ editor: this._editor }, modeService, openerService));
        this._computer = new MarginComputer(this._editor);
        this._hoverOperation = new HoverOperation(this._computer, (result) => this._withResult(result), undefined, (result) => this._withResult(result), 300);
    }
    dispose() {
        this._hoverOperation.cancel();
        super.dispose();
    }
    onModelDecorationsChanged() {
        if (this.isVisible) {
            // The decorations have changed and the hover is visible,
            // we need to recompute the displayed text
            this._hoverOperation.cancel();
            this._computer.clearResult();
            this._hoverOperation.start(0 /* Delayed */);
        }
    }
    startShowingAt(lineNumber) {
        if (this._lastLineNumber === lineNumber) {
            // We have to show the widget at the exact same line number as before, so no work is needed
            return;
        }
        this._hoverOperation.cancel();
        this.hide();
        this._lastLineNumber = lineNumber;
        this._computer.setLineNumber(lineNumber);
        this._hoverOperation.start(0 /* Delayed */);
    }
    hide() {
        this._lastLineNumber = -1;
        this._hoverOperation.cancel();
        super.hide();
    }
    _withResult(result) {
        this._messages = result;
        if (this._messages.length > 0) {
            this._renderMessages(this._lastLineNumber, this._messages);
        }
        else {
            this.hide();
        }
    }
    _renderMessages(lineNumber, messages) {
        this._renderDisposeables.clear();
        const fragment = document.createDocumentFragment();
        for (const msg of messages) {
            const renderedContents = this._markdownRenderer.render(msg.value);
            this._renderDisposeables.add(renderedContents);
            fragment.appendChild($('div.hover-row', undefined, renderedContents.element));
        }
        this.updateContents(fragment);
        this.showAt(lineNumber);
    }
}
ModesGlyphHoverWidget.ID = 'editor.contrib.modesGlyphHoverWidget';
