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
import { Disposable } from '../../../base/common/lifecycle.js';
import { registerEditorContribution } from '../../browser/editorExtensions.js';
import { ICodeEditorService } from '../../browser/services/codeEditorService.js';
import { IDialogService } from '../../../platform/dialogs/common/dialogs.js';
const ignoreUnusualLineTerminators = 'ignoreUnusualLineTerminators';
function writeIgnoreState(codeEditorService, model, state) {
    codeEditorService.setModelProperty(model.uri, ignoreUnusualLineTerminators, state);
}
function readIgnoreState(codeEditorService, model) {
    return codeEditorService.getModelProperty(model.uri, ignoreUnusualLineTerminators);
}
let UnusualLineTerminatorsDetector = class UnusualLineTerminatorsDetector extends Disposable {
    constructor(_editor, _dialogService, _codeEditorService) {
        super();
        this._editor = _editor;
        this._dialogService = _dialogService;
        this._codeEditorService = _codeEditorService;
        this._config = this._editor.getOption(108 /* unusualLineTerminators */);
        this._register(this._editor.onDidChangeConfiguration((e) => {
            if (e.hasChanged(108 /* unusualLineTerminators */)) {
                this._config = this._editor.getOption(108 /* unusualLineTerminators */);
                this._checkForUnusualLineTerminators();
            }
        }));
        this._register(this._editor.onDidChangeModel(() => {
            this._checkForUnusualLineTerminators();
        }));
        this._register(this._editor.onDidChangeModelContent((e) => {
            if (e.isUndoing) {
                // skip checking in case of undoing
                return;
            }
            this._checkForUnusualLineTerminators();
        }));
    }
    _checkForUnusualLineTerminators() {
        return __awaiter(this, void 0, void 0, function* () {
            if (this._config === 'off') {
                return;
            }
            if (!this._editor.hasModel()) {
                return;
            }
            const model = this._editor.getModel();
            if (!model.mightContainUnusualLineTerminators()) {
                return;
            }
            const ignoreState = readIgnoreState(this._codeEditorService, model);
            if (ignoreState === true) {
                // this model should be ignored
                return;
            }
            if (this._editor.getOption(75 /* readOnly */)) {
                // read only editor => sorry!
                return;
            }
            if (this._config === 'auto') {
                // just do it!
                model.removeUnusualLineTerminators(this._editor.getSelections());
                return;
            }
            const result = yield this._dialogService.confirm({
                title: nls.localize('unusualLineTerminators.title', "Unusual Line Terminators"),
                message: nls.localize('unusualLineTerminators.message', "Detected unusual line terminators"),
                detail: nls.localize('unusualLineTerminators.detail', "This file contains one or more unusual line terminator characters, like Line Separator (LS) or Paragraph Separator (PS).\n\nIt is recommended to remove them from the file. This can be configured via `editor.unusualLineTerminators`."),
                primaryButton: nls.localize('unusualLineTerminators.fix', "Fix this file"),
                secondaryButton: nls.localize('unusualLineTerminators.ignore', "Ignore problem for this file")
            });
            if (!result.confirmed) {
                // this model should be ignored
                writeIgnoreState(this._codeEditorService, model, true);
                return;
            }
            model.removeUnusualLineTerminators(this._editor.getSelections());
        });
    }
};
UnusualLineTerminatorsDetector.ID = 'editor.contrib.unusualLineTerminatorsDetector';
UnusualLineTerminatorsDetector = __decorate([
    __param(1, IDialogService),
    __param(2, ICodeEditorService)
], UnusualLineTerminatorsDetector);
registerEditorContribution(UnusualLineTerminatorsDetector.ID, UnusualLineTerminatorsDetector);
