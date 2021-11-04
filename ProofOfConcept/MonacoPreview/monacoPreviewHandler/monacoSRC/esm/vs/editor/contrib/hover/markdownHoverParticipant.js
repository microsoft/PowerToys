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
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import * as nls from '../../../nls.js';
import * as dom from '../../../base/browser/dom.js';
import { MarkdownString, isEmptyMarkdownString, markedStringsEquals } from '../../../base/common/htmlContent.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import { Range } from '../../common/core/range.js';
import { MarkdownRenderer } from '../../browser/core/markdownRenderer.js';
import { asArray } from '../../../base/common/arrays.js';
import { IOpenerService } from '../../../platform/opener/common/opener.js';
import { IModeService } from '../../common/services/modeService.js';
import { HoverProviderRegistry } from '../../common/modes.js';
import { getHover } from './getHover.js';
import { Position } from '../../common/core/position.js';
const $ = dom.$;
export class MarkdownHover {
    constructor(range, contents) {
        this.range = range;
        this.contents = contents;
    }
    equals(other) {
        if (other instanceof MarkdownHover) {
            return markedStringsEquals(this.contents, other.contents);
        }
        return false;
    }
}
let MarkdownHoverParticipant = class MarkdownHoverParticipant {
    constructor(_editor, _hover, _modeService, _openerService) {
        this._editor = _editor;
        this._hover = _hover;
        this._modeService = _modeService;
        this._openerService = _openerService;
    }
    createLoadingMessage(range) {
        return new MarkdownHover(range, [new MarkdownString().appendText(nls.localize('modesContentHover.loading', "Loading..."))]);
    }
    computeSync(hoverRange, lineDecorations) {
        if (!this._editor.hasModel()) {
            return [];
        }
        const model = this._editor.getModel();
        const lineNumber = hoverRange.startLineNumber;
        const maxColumn = model.getLineMaxColumn(lineNumber);
        const result = [];
        for (const d of lineDecorations) {
            const startColumn = (d.range.startLineNumber === lineNumber) ? d.range.startColumn : 1;
            const endColumn = (d.range.endLineNumber === lineNumber) ? d.range.endColumn : maxColumn;
            const hoverMessage = d.options.hoverMessage;
            if (!hoverMessage || isEmptyMarkdownString(hoverMessage)) {
                continue;
            }
            const range = new Range(hoverRange.startLineNumber, startColumn, hoverRange.startLineNumber, endColumn);
            result.push(new MarkdownHover(range, asArray(hoverMessage)));
        }
        return result;
    }
    computeAsync(range, token) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this._editor.hasModel() || !range) {
                return Promise.resolve([]);
            }
            const model = this._editor.getModel();
            if (!HoverProviderRegistry.has(model)) {
                return Promise.resolve([]);
            }
            const hovers = yield getHover(model, new Position(range.startLineNumber, range.startColumn), token);
            const result = [];
            for (const hover of hovers) {
                if (isEmptyMarkdownString(hover.contents)) {
                    continue;
                }
                const rng = hover.range ? Range.lift(hover.range) : range;
                result.push(new MarkdownHover(rng, hover.contents));
            }
            return result;
        });
    }
    renderHoverParts(hoverParts, fragment) {
        const disposables = new DisposableStore();
        for (const hoverPart of hoverParts) {
            for (const contents of hoverPart.contents) {
                if (isEmptyMarkdownString(contents)) {
                    continue;
                }
                const markdownHoverElement = $('div.hover-row.markdown-hover');
                const hoverContentsElement = dom.append(markdownHoverElement, $('div.hover-contents'));
                const renderer = disposables.add(new MarkdownRenderer({ editor: this._editor }, this._modeService, this._openerService));
                disposables.add(renderer.onDidRenderAsync(() => {
                    hoverContentsElement.className = 'hover-contents code-hover-contents';
                    this._hover.onContentsChanged();
                }));
                const renderedContents = disposables.add(renderer.render(contents));
                hoverContentsElement.appendChild(renderedContents.element);
                fragment.appendChild(markdownHoverElement);
            }
        }
        return disposables;
    }
};
MarkdownHoverParticipant = __decorate([
    __param(2, IModeService),
    __param(3, IOpenerService)
], MarkdownHoverParticipant);
export { MarkdownHoverParticipant };
