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
import { RawContextKey, IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { Disposable } from '../../../base/common/lifecycle.js';
let WordContextKey = class WordContextKey extends Disposable {
    constructor(_editor, contextKeyService) {
        super();
        this._editor = _editor;
        this._enabled = false;
        this._ckAtEnd = WordContextKey.AtEnd.bindTo(contextKeyService);
        this._register(this._editor.onDidChangeConfiguration(e => e.hasChanged(106 /* tabCompletion */) && this._update()));
        this._update();
    }
    dispose() {
        var _a;
        super.dispose();
        (_a = this._selectionListener) === null || _a === void 0 ? void 0 : _a.dispose();
        this._ckAtEnd.reset();
    }
    _update() {
        // only update this when tab completions are enabled
        const enabled = this._editor.getOption(106 /* tabCompletion */) === 'on';
        if (this._enabled === enabled) {
            return;
        }
        this._enabled = enabled;
        if (this._enabled) {
            const checkForWordEnd = () => {
                if (!this._editor.hasModel()) {
                    this._ckAtEnd.set(false);
                    return;
                }
                const model = this._editor.getModel();
                const selection = this._editor.getSelection();
                const word = model.getWordAtPosition(selection.getStartPosition());
                if (!word) {
                    this._ckAtEnd.set(false);
                    return;
                }
                this._ckAtEnd.set(word.endColumn === selection.getStartPosition().column);
            };
            this._selectionListener = this._editor.onDidChangeCursorSelection(checkForWordEnd);
            checkForWordEnd();
        }
        else if (this._selectionListener) {
            this._ckAtEnd.reset();
            this._selectionListener.dispose();
            this._selectionListener = undefined;
        }
    }
};
WordContextKey.AtEnd = new RawContextKey('atEndOfWord', false);
WordContextKey = __decorate([
    __param(1, IContextKeyService)
], WordContextKey);
export { WordContextKey };
