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
var _a;
import { renderMarkdown } from '../../../base/browser/markdownRenderer.js';
import { IOpenerService } from '../../../platform/opener/common/opener.js';
import { IModeService } from '../../common/services/modeService.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { tokenizeToString } from '../../common/modes/textToHtmlTokenizer.js';
import { Emitter } from '../../../base/common/event.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import { TokenizationRegistry } from '../../common/modes.js';
/**
 * Markdown renderer that can render codeblocks with the editor mechanics. This
 * renderer should always be preferred.
 */
let MarkdownRenderer = class MarkdownRenderer {
    constructor(_options, _modeService, _openerService) {
        this._options = _options;
        this._modeService = _modeService;
        this._openerService = _openerService;
        this._onDidRenderAsync = new Emitter();
        this.onDidRenderAsync = this._onDidRenderAsync.event;
    }
    dispose() {
        this._onDidRenderAsync.dispose();
    }
    render(markdown, options, markedOptions) {
        const disposeables = new DisposableStore();
        let element;
        if (!markdown) {
            element = document.createElement('span');
        }
        else {
            element = renderMarkdown(markdown, Object.assign(Object.assign({}, this._getRenderOptions(disposeables)), options), markedOptions);
        }
        return {
            element,
            dispose: () => disposeables.dispose()
        };
    }
    _getRenderOptions(disposeables) {
        return {
            baseUrl: this._options.baseUrl,
            codeBlockRenderer: (languageAlias, value) => __awaiter(this, void 0, void 0, function* () {
                var _a, _b, _c, _d;
                // In markdown,
                // it is possible that we stumble upon language aliases (e.g.js instead of javascript)
                // it is possible no alias is given in which case we fall back to the current editor lang
                let modeId;
                if (languageAlias) {
                    modeId = this._modeService.getModeIdForLanguageName(languageAlias);
                }
                else if (this._options.editor) {
                    modeId = (_a = this._options.editor.getModel()) === null || _a === void 0 ? void 0 : _a.getLanguageIdentifier().language;
                }
                if (!modeId) {
                    modeId = 'plaintext';
                }
                this._modeService.triggerMode(modeId);
                const tokenization = (_b = yield TokenizationRegistry.getPromise(modeId)) !== null && _b !== void 0 ? _b : undefined;
                const element = document.createElement('span');
                element.innerHTML = ((_d = (_c = MarkdownRenderer._ttpTokenizer) === null || _c === void 0 ? void 0 : _c.createHTML(value, tokenization)) !== null && _d !== void 0 ? _d : tokenizeToString(value, tokenization));
                // use "good" font
                let fontFamily = this._options.codeBlockFontFamily;
                if (this._options.editor) {
                    fontFamily = this._options.editor.getOption(38 /* fontInfo */).fontFamily;
                }
                if (fontFamily) {
                    element.style.fontFamily = fontFamily;
                }
                return element;
            }),
            asyncRenderCallback: () => this._onDidRenderAsync.fire(),
            actionHandler: {
                callback: (content) => this._openerService.open(content, { fromUserGesture: true, allowContributedOpeners: true }).catch(onUnexpectedError),
                disposeables
            }
        };
    }
};
MarkdownRenderer._ttpTokenizer = (_a = window.trustedTypes) === null || _a === void 0 ? void 0 : _a.createPolicy('tokenizeToString', {
    createHTML(value, tokenizer) {
        return tokenizeToString(value, tokenizer);
    }
});
MarkdownRenderer = __decorate([
    __param(1, IModeService),
    __param(2, IOpenerService)
], MarkdownRenderer);
export { MarkdownRenderer };
