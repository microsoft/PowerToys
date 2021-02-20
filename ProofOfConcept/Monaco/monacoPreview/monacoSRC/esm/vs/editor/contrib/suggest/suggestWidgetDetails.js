/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
};
import * as nls from '../../../nls.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import * as dom from '../../../base/browser/dom.js';
import { DomScrollableElement } from '../../../base/browser/ui/scrollbar/scrollableElement.js';
import { MarkdownRenderer } from '../../browser/core/markdownRenderer.js';
import { MarkdownString } from '../../../base/common/htmlContent.js';
import { Codicon } from '../../../base/common/codicons.js';
import { Emitter } from '../../../base/common/event.js';
import { ResizableHTMLElement } from './resizable.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
export function canExpandCompletionItem(item) {
    return !!item && Boolean(item.completion.documentation || item.completion.detail && item.completion.detail !== item.completion.label);
}
let SuggestDetailsWidget = class SuggestDetailsWidget {
    constructor(_editor, instaService) {
        this._editor = _editor;
        this._onDidClose = new Emitter();
        this.onDidClose = this._onDidClose.event;
        this._onDidChangeContents = new Emitter();
        this.onDidChangeContents = this._onDidChangeContents.event;
        this._disposables = new DisposableStore();
        this._renderDisposeable = new DisposableStore();
        this._borderWidth = 1;
        this._size = new dom.Dimension(330, 0);
        this.domNode = dom.$('.suggest-details');
        this.domNode.classList.add('no-docs');
        this._markdownRenderer = instaService.createInstance(MarkdownRenderer, { editor: _editor });
        this._body = dom.$('.body');
        this._scrollbar = new DomScrollableElement(this._body, {});
        dom.append(this.domNode, this._scrollbar.getDomNode());
        this._disposables.add(this._scrollbar);
        this._header = dom.append(this._body, dom.$('.header'));
        this._close = dom.append(this._header, dom.$('span' + Codicon.close.cssSelector));
        this._close.title = nls.localize('details.close', "Close");
        this._type = dom.append(this._header, dom.$('p.type'));
        this._docs = dom.append(this._body, dom.$('p.docs'));
        this._configureFont();
        this._disposables.add(this._editor.onDidChangeConfiguration(e => {
            if (e.hasChanged(38 /* fontInfo */)) {
                this._configureFont();
            }
        }));
    }
    dispose() {
        this._disposables.dispose();
        this._renderDisposeable.dispose();
    }
    _configureFont() {
        const options = this._editor.getOptions();
        const fontInfo = options.get(38 /* fontInfo */);
        const fontFamily = fontInfo.fontFamily;
        const fontSize = options.get(102 /* suggestFontSize */) || fontInfo.fontSize;
        const lineHeight = options.get(103 /* suggestLineHeight */) || fontInfo.lineHeight;
        const fontWeight = fontInfo.fontWeight;
        const fontSizePx = `${fontSize}px`;
        const lineHeightPx = `${lineHeight}px`;
        this.domNode.style.fontSize = fontSizePx;
        this.domNode.style.lineHeight = lineHeightPx;
        this.domNode.style.fontWeight = fontWeight;
        this.domNode.style.fontFeatureSettings = fontInfo.fontFeatureSettings;
        this._type.style.fontFamily = fontFamily;
        this._close.style.height = lineHeightPx;
        this._close.style.width = lineHeightPx;
    }
    getLayoutInfo() {
        const lineHeight = this._editor.getOption(103 /* suggestLineHeight */) || this._editor.getOption(38 /* fontInfo */).lineHeight;
        const borderWidth = this._borderWidth;
        const borderHeight = borderWidth * 2;
        return {
            lineHeight,
            borderWidth,
            borderHeight,
            verticalPadding: 22,
            horizontalPadding: 14
        };
    }
    renderLoading() {
        this._type.textContent = nls.localize('loading', "Loading...");
        this._docs.textContent = '';
        this.domNode.classList.remove('no-docs', 'no-type');
        this.layout(this.size.width, this.getLayoutInfo().lineHeight * 2);
        this._onDidChangeContents.fire(this);
    }
    renderItem(item, explainMode) {
        this._renderDisposeable.clear();
        let { detail, documentation } = item.completion;
        if (explainMode) {
            let md = '';
            md += `score: ${item.score[0]}${item.word ? `, compared '${item.completion.filterText && (item.completion.filterText + ' (filterText)') || typeof item.completion.label === 'string' ? item.completion.label : item.completion.label.name}' with '${item.word}'` : ' (no prefix)'}\n`;
            md += `distance: ${item.distance}, see localityBonus-setting\n`;
            md += `index: ${item.idx}, based on ${item.completion.sortText && `sortText: "${item.completion.sortText}"` || 'label'}\n`;
            md += `commit characters: ${item.completion.commitCharacters}\n`;
            documentation = new MarkdownString().appendCodeblock('empty', md);
            detail = `Provider: ${item.provider._debugDisplayName}`;
        }
        if (!explainMode && !canExpandCompletionItem(item)) {
            this.clearContents();
            return;
        }
        this.domNode.classList.remove('no-docs', 'no-type');
        // --- details
        if (detail) {
            const cappedDetail = detail.length > 100000 ? `${detail.substr(0, 100000)}…` : detail;
            this._type.textContent = cappedDetail;
            this._type.title = cappedDetail;
            dom.show(this._type);
            this._type.classList.toggle('auto-wrap', !/\r?\n^\s+/gmi.test(cappedDetail));
        }
        else {
            dom.clearNode(this._type);
            this._type.title = '';
            dom.hide(this._type);
            this.domNode.classList.add('no-type');
        }
        // --- documentation
        dom.clearNode(this._docs);
        if (typeof documentation === 'string') {
            this._docs.classList.remove('markdown-docs');
            this._docs.textContent = documentation;
        }
        else if (documentation) {
            this._docs.classList.add('markdown-docs');
            dom.clearNode(this._docs);
            const renderedContents = this._markdownRenderer.render(documentation);
            this._docs.appendChild(renderedContents.element);
            this._renderDisposeable.add(renderedContents);
            this._renderDisposeable.add(this._markdownRenderer.onDidRenderAsync(() => {
                this.layout(this._size.width, this._type.clientHeight + this._docs.clientHeight);
                this._onDidChangeContents.fire(this);
            }));
        }
        this.domNode.style.userSelect = 'text';
        this.domNode.tabIndex = -1;
        this._close.onmousedown = e => {
            e.preventDefault();
            e.stopPropagation();
        };
        this._close.onclick = e => {
            e.preventDefault();
            e.stopPropagation();
            this._onDidClose.fire();
        };
        this._body.scrollTop = 0;
        this.layout(this._size.width, this._type.clientHeight + this._docs.clientHeight);
        this._onDidChangeContents.fire(this);
    }
    clearContents() {
        this.domNode.classList.add('no-docs');
        this._type.textContent = '';
        this._docs.textContent = '';
    }
    get size() {
        return this._size;
    }
    layout(width, height) {
        const newSize = new dom.Dimension(width, height);
        if (!dom.Dimension.equals(newSize, this._size)) {
            this._size = newSize;
            dom.size(this.domNode, width, height);
        }
        this._scrollbar.scanDomNode();
    }
    scrollDown(much = 8) {
        this._body.scrollTop += much;
    }
    scrollUp(much = 8) {
        this._body.scrollTop -= much;
    }
    scrollTop() {
        this._body.scrollTop = 0;
    }
    scrollBottom() {
        this._body.scrollTop = this._body.scrollHeight;
    }
    pageDown() {
        this.scrollDown(80);
    }
    pageUp() {
        this.scrollUp(80);
    }
    set borderWidth(width) {
        this._borderWidth = width;
    }
    get borderWidth() {
        return this._borderWidth;
    }
};
SuggestDetailsWidget = __decorate([
    __param(1, IInstantiationService)
], SuggestDetailsWidget);
export { SuggestDetailsWidget };
export class SuggestDetailsOverlay {
    constructor(widget, _editor) {
        this.widget = widget;
        this._editor = _editor;
        this._disposables = new DisposableStore();
        this._added = false;
        this._resizable = new ResizableHTMLElement();
        this._resizable.domNode.classList.add('suggest-details-container');
        this._resizable.domNode.appendChild(widget.domNode);
        this._resizable.enableSashes(false, true, true, false);
        let topLeftNow;
        let sizeNow;
        let deltaTop = 0;
        let deltaLeft = 0;
        this._disposables.add(this._resizable.onDidWillResize(() => {
            topLeftNow = this._topLeft;
            sizeNow = this._resizable.size;
        }));
        this._disposables.add(this._resizable.onDidResize(e => {
            if (topLeftNow && sizeNow) {
                this.widget.layout(e.dimension.width, e.dimension.height);
                let updateTopLeft = false;
                if (e.west) {
                    deltaLeft = sizeNow.width - e.dimension.width;
                    updateTopLeft = true;
                }
                if (e.north) {
                    deltaTop = sizeNow.height - e.dimension.height;
                    updateTopLeft = true;
                }
                if (updateTopLeft) {
                    this._applyTopLeft({
                        top: topLeftNow.top + deltaTop,
                        left: topLeftNow.left + deltaLeft,
                    });
                }
            }
            if (e.done) {
                topLeftNow = undefined;
                sizeNow = undefined;
                deltaTop = 0;
                deltaLeft = 0;
                this._userSize = e.dimension;
            }
        }));
        this._disposables.add(this.widget.onDidChangeContents(() => {
            var _a;
            if (this._anchorBox) {
                this._placeAtAnchor(this._anchorBox, (_a = this._userSize) !== null && _a !== void 0 ? _a : this.widget.size);
            }
        }));
    }
    dispose() {
        this._disposables.dispose();
        this.hide();
    }
    getId() {
        return 'suggest.details';
    }
    getDomNode() {
        return this._resizable.domNode;
    }
    getPosition() {
        return null;
    }
    show() {
        if (!this._added) {
            this._editor.addOverlayWidget(this);
            this.getDomNode().style.position = 'fixed';
            this._added = true;
        }
    }
    hide(sessionEnded = false) {
        if (this._added) {
            this._editor.removeOverlayWidget(this);
            this._added = false;
            this._anchorBox = undefined;
            this._topLeft = undefined;
        }
        if (sessionEnded) {
            this._userSize = undefined;
            this.widget.clearContents();
        }
    }
    placeAtAnchor(anchor) {
        var _a;
        const anchorBox = dom.getDomNodePagePosition(anchor);
        this._anchorBox = anchorBox;
        this._placeAtAnchor(this._anchorBox, (_a = this._userSize) !== null && _a !== void 0 ? _a : this.widget.size);
    }
    _placeAtAnchor(anchorBox, size) {
        const bodyBox = dom.getClientArea(document.body);
        const info = this.widget.getLayoutInfo();
        let maxSizeTop;
        let maxSizeBottom;
        let minSize = new dom.Dimension(220, 2 * info.lineHeight);
        let left = 0;
        let top = anchorBox.top;
        let bottom = anchorBox.top + anchorBox.height - info.borderHeight;
        let alignAtTop;
        let alignEast;
        // position: EAST, west, south
        let width = bodyBox.width - (anchorBox.left + anchorBox.width + info.borderWidth + info.horizontalPadding);
        left = -info.borderWidth + anchorBox.left + anchorBox.width;
        alignEast = true;
        maxSizeTop = new dom.Dimension(width, bodyBox.height - anchorBox.top - info.borderHeight - info.verticalPadding);
        maxSizeBottom = maxSizeTop.with(undefined, anchorBox.top + anchorBox.height - info.borderHeight - info.verticalPadding);
        // find a better place if the widget is wider than there is space available
        if (size.width > width) {
            // position: east, WEST, south
            if (anchorBox.left > width) {
                // pos = SuggestDetailsPosition.West;
                width = anchorBox.left - info.borderWidth - info.horizontalPadding;
                alignEast = false;
                left = Math.max(info.horizontalPadding, anchorBox.left - size.width - info.borderWidth);
                maxSizeTop = maxSizeTop.with(width);
                maxSizeBottom = maxSizeTop.with(undefined, maxSizeBottom.height);
            }
            // position: east, west, SOUTH
            if (anchorBox.width > width * 1.3 && bodyBox.height - (anchorBox.top + anchorBox.height) > anchorBox.height) {
                width = anchorBox.width;
                left = anchorBox.left;
                top = -info.borderWidth + anchorBox.top + anchorBox.height;
                maxSizeTop = new dom.Dimension(anchorBox.width - info.borderHeight, bodyBox.height - anchorBox.top - anchorBox.height - info.verticalPadding);
                maxSizeBottom = maxSizeTop.with(undefined, anchorBox.top - info.verticalPadding);
                minSize = minSize.with(maxSizeTop.width);
            }
        }
        // top/bottom placement
        let height = size.height;
        let maxHeight = Math.max(maxSizeTop.height, maxSizeBottom.height);
        if (height > maxHeight) {
            height = maxHeight;
        }
        let maxSize;
        if (height <= maxSizeTop.height) {
            alignAtTop = true;
            maxSize = maxSizeTop;
        }
        else {
            alignAtTop = false;
            maxSize = maxSizeBottom;
        }
        this._applyTopLeft({ left, top: alignAtTop ? top : bottom - height });
        this.getDomNode().style.position = 'fixed';
        this._resizable.enableSashes(!alignAtTop, alignEast, alignAtTop, !alignEast);
        this._resizable.minSize = minSize;
        this._resizable.maxSize = maxSize;
        this._resizable.layout(height, Math.min(maxSize.width, size.width));
        this.widget.layout(this._resizable.size.width, this._resizable.size.height);
    }
    _applyTopLeft(topLeft) {
        this._topLeft = topLeft;
        this.getDomNode().style.left = `${this._topLeft.left}px`;
        this.getDomNode().style.top = `${this._topLeft.top}px`;
    }
}
