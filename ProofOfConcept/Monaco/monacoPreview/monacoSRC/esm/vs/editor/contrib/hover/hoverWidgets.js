/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Widget } from '../../../base/browser/ui/widget.js';
export class GlyphHoverWidget extends Widget {
    constructor(id, editor) {
        super();
        this._id = id;
        this._editor = editor;
        this._isVisible = false;
        this._domNode = document.createElement('div');
        this._domNode.className = 'monaco-hover hidden';
        this._domNode.setAttribute('aria-hidden', 'true');
        this._domNode.setAttribute('role', 'tooltip');
        this._showAtLineNumber = -1;
        this._register(this._editor.onDidChangeConfiguration((e) => {
            if (e.hasChanged(38 /* fontInfo */)) {
                this.updateFont();
            }
        }));
        this._editor.addOverlayWidget(this);
    }
    get isVisible() {
        return this._isVisible;
    }
    set isVisible(value) {
        this._isVisible = value;
        this._domNode.classList.toggle('hidden', !this._isVisible);
    }
    getId() {
        return this._id;
    }
    getDomNode() {
        return this._domNode;
    }
    showAt(lineNumber) {
        this._showAtLineNumber = lineNumber;
        if (!this.isVisible) {
            this.isVisible = true;
        }
        const editorLayout = this._editor.getLayoutInfo();
        const topForLineNumber = this._editor.getTopForLineNumber(this._showAtLineNumber);
        const editorScrollTop = this._editor.getScrollTop();
        const lineHeight = this._editor.getOption(53 /* lineHeight */);
        const nodeHeight = this._domNode.clientHeight;
        const top = topForLineNumber - editorScrollTop - ((nodeHeight - lineHeight) / 2);
        this._domNode.style.left = `${editorLayout.glyphMarginLeft + editorLayout.glyphMarginWidth}px`;
        this._domNode.style.top = `${Math.max(Math.round(top), 0)}px`;
    }
    hide() {
        if (!this.isVisible) {
            return;
        }
        this.isVisible = false;
    }
    getPosition() {
        return null;
    }
    dispose() {
        this._editor.removeOverlayWidget(this);
        super.dispose();
    }
    updateFont() {
        const codeTags = Array.prototype.slice.call(this._domNode.getElementsByTagName('code'));
        const codeClasses = Array.prototype.slice.call(this._domNode.getElementsByClassName('code'));
        [...codeTags, ...codeClasses].forEach(node => this._editor.applyFontInfo(node));
    }
    updateContents(node) {
        this._domNode.textContent = '';
        this._domNode.appendChild(node);
        this.updateFont();
    }
}
