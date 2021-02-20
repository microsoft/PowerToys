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
import { RunOnceScheduler, createCancelablePromise } from '../../../base/common/async.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { registerEditorContribution } from '../../browser/editorExtensions.js';
import { DocumentRangeSemanticTokensProviderRegistry } from '../../common/modes.js';
import { IModelService } from '../../common/services/modelService.js';
import { toMultilineTokens2 } from '../../common/services/semanticTokensProviderStyling.js';
import { IThemeService } from '../../../platform/theme/common/themeService.js';
import { IConfigurationService } from '../../../platform/configuration/common/configuration.js';
import { isSemanticColoringEnabled, SEMANTIC_HIGHLIGHTING_SETTING_ID } from '../../common/services/modelServiceImpl.js';
import { getDocumentRangeSemanticTokensProvider } from '../../common/services/getSemanticTokens.js';
let ViewportSemanticTokensContribution = class ViewportSemanticTokensContribution extends Disposable {
    constructor(editor, _modelService, _themeService, _configurationService) {
        super();
        this._modelService = _modelService;
        this._themeService = _themeService;
        this._configurationService = _configurationService;
        this._editor = editor;
        this._tokenizeViewport = new RunOnceScheduler(() => this._tokenizeViewportNow(), 100);
        this._outstandingRequests = [];
        this._register(this._editor.onDidScrollChange(() => {
            this._tokenizeViewport.schedule();
        }));
        this._register(this._editor.onDidChangeModel(() => {
            this._cancelAll();
            this._tokenizeViewport.schedule();
        }));
        this._register(this._editor.onDidChangeModelContent((e) => {
            this._cancelAll();
            this._tokenizeViewport.schedule();
        }));
        this._register(DocumentRangeSemanticTokensProviderRegistry.onDidChange(() => {
            this._cancelAll();
            this._tokenizeViewport.schedule();
        }));
        this._register(this._configurationService.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration(SEMANTIC_HIGHLIGHTING_SETTING_ID)) {
                this._cancelAll();
                this._tokenizeViewport.schedule();
            }
        }));
        this._register(this._themeService.onDidColorThemeChange(() => {
            this._cancelAll();
            this._tokenizeViewport.schedule();
        }));
    }
    _cancelAll() {
        for (const request of this._outstandingRequests) {
            request.cancel();
        }
        this._outstandingRequests = [];
    }
    _removeOutstandingRequest(req) {
        for (let i = 0, len = this._outstandingRequests.length; i < len; i++) {
            if (this._outstandingRequests[i] === req) {
                this._outstandingRequests.splice(i, 1);
                return;
            }
        }
    }
    _tokenizeViewportNow() {
        if (!this._editor.hasModel()) {
            return;
        }
        const model = this._editor.getModel();
        if (model.hasCompleteSemanticTokens()) {
            return;
        }
        if (!isSemanticColoringEnabled(model, this._themeService, this._configurationService)) {
            if (model.hasSomeSemanticTokens()) {
                model.setSemanticTokens(null, false);
            }
            return;
        }
        const provider = getDocumentRangeSemanticTokensProvider(model);
        if (!provider) {
            if (model.hasSomeSemanticTokens()) {
                model.setSemanticTokens(null, false);
            }
            return;
        }
        const styling = this._modelService.getSemanticTokensProviderStyling(provider);
        const visibleRanges = this._editor.getVisibleRangesPlusViewportAboveBelow();
        this._outstandingRequests = this._outstandingRequests.concat(visibleRanges.map(range => this._requestRange(model, range, provider, styling)));
    }
    _requestRange(model, range, provider, styling) {
        const requestVersionId = model.getVersionId();
        const request = createCancelablePromise(token => Promise.resolve(provider.provideDocumentRangeSemanticTokens(model, range, token)));
        request.then((r) => {
            if (!r || model.isDisposed() || model.getVersionId() !== requestVersionId) {
                return;
            }
            model.setPartialSemanticTokens(range, toMultilineTokens2(r, styling, model.getLanguageIdentifier()));
        }).then(() => this._removeOutstandingRequest(request), () => this._removeOutstandingRequest(request));
        return request;
    }
};
ViewportSemanticTokensContribution.ID = 'editor.contrib.viewportSemanticTokens';
ViewportSemanticTokensContribution = __decorate([
    __param(1, IModelService),
    __param(2, IThemeService),
    __param(3, IConfigurationService)
], ViewportSemanticTokensContribution);
registerEditorContribution(ViewportSemanticTokensContribution.ID, ViewportSemanticTokensContribution);
