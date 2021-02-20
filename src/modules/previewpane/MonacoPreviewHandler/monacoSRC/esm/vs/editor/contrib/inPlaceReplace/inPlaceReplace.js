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
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { registerEditorAction, EditorAction, registerEditorContribution } from '../../browser/editorExtensions.js';
import { IEditorWorkerService } from '../../common/services/editorWorkerService.js';
import { InPlaceReplaceCommand } from './inPlaceReplaceCommand.js';
import { EditorState } from '../../browser/core/editorState.js';
import { registerThemingParticipant } from '../../../platform/theme/common/themeService.js';
import { editorBracketMatchBorder } from '../../common/view/editorColorRegistry.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
import { createCancelablePromise, timeout } from '../../../base/common/async.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
let InPlaceReplaceController = class InPlaceReplaceController {
    constructor(editor, editorWorkerService) {
        this.decorationIds = [];
        this.editor = editor;
        this.editorWorkerService = editorWorkerService;
    }
    static get(editor) {
        return editor.getContribution(InPlaceReplaceController.ID);
    }
    dispose() {
    }
    run(source, up) {
        // cancel any pending request
        if (this.currentRequest) {
            this.currentRequest.cancel();
        }
        const editorSelection = this.editor.getSelection();
        const model = this.editor.getModel();
        if (!model || !editorSelection) {
            return undefined;
        }
        let selection = editorSelection;
        if (selection.startLineNumber !== selection.endLineNumber) {
            // Can't accept multiline selection
            return undefined;
        }
        const state = new EditorState(this.editor, 1 /* Value */ | 4 /* Position */);
        const modelURI = model.uri;
        if (!this.editorWorkerService.canNavigateValueSet(modelURI)) {
            return Promise.resolve(undefined);
        }
        this.currentRequest = createCancelablePromise(token => this.editorWorkerService.navigateValueSet(modelURI, selection, up));
        return this.currentRequest.then(result => {
            if (!result || !result.range || !result.value) {
                // No proper result
                return;
            }
            if (!state.validate(this.editor)) {
                // state has changed
                return;
            }
            // Selection
            let editRange = Range.lift(result.range);
            let highlightRange = result.range;
            let diff = result.value.length - (selection.endColumn - selection.startColumn);
            // highlight
            highlightRange = {
                startLineNumber: highlightRange.startLineNumber,
                startColumn: highlightRange.startColumn,
                endLineNumber: highlightRange.endLineNumber,
                endColumn: highlightRange.startColumn + result.value.length
            };
            if (diff > 1) {
                selection = new Selection(selection.startLineNumber, selection.startColumn, selection.endLineNumber, selection.endColumn + diff - 1);
            }
            // Insert new text
            const command = new InPlaceReplaceCommand(editRange, selection, result.value);
            this.editor.pushUndoStop();
            this.editor.executeCommand(source, command);
            this.editor.pushUndoStop();
            // add decoration
            this.decorationIds = this.editor.deltaDecorations(this.decorationIds, [{
                    range: highlightRange,
                    options: InPlaceReplaceController.DECORATION
                }]);
            // remove decoration after delay
            if (this.decorationRemover) {
                this.decorationRemover.cancel();
            }
            this.decorationRemover = timeout(350);
            this.decorationRemover.then(() => this.decorationIds = this.editor.deltaDecorations(this.decorationIds, [])).catch(onUnexpectedError);
        }).catch(onUnexpectedError);
    }
};
InPlaceReplaceController.ID = 'editor.contrib.inPlaceReplaceController';
InPlaceReplaceController.DECORATION = ModelDecorationOptions.register({
    className: 'valueSetReplacement'
});
InPlaceReplaceController = __decorate([
    __param(1, IEditorWorkerService)
], InPlaceReplaceController);
class InPlaceReplaceUp extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.inPlaceReplace.up',
            label: nls.localize('InPlaceReplaceAction.previous.label', "Replace with Previous Value"),
            alias: 'Replace with Previous Value',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 82 /* US_COMMA */,
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(accessor, editor) {
        const controller = InPlaceReplaceController.get(editor);
        if (!controller) {
            return Promise.resolve(undefined);
        }
        return controller.run(this.id, true);
    }
}
class InPlaceReplaceDown extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.inPlaceReplace.down',
            label: nls.localize('InPlaceReplaceAction.next.label', "Replace with Next Value"),
            alias: 'Replace with Next Value',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 84 /* US_DOT */,
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(accessor, editor) {
        const controller = InPlaceReplaceController.get(editor);
        if (!controller) {
            return Promise.resolve(undefined);
        }
        return controller.run(this.id, false);
    }
}
registerEditorContribution(InPlaceReplaceController.ID, InPlaceReplaceController);
registerEditorAction(InPlaceReplaceUp);
registerEditorAction(InPlaceReplaceDown);
registerThemingParticipant((theme, collector) => {
    const border = theme.getColor(editorBracketMatchBorder);
    if (border) {
        collector.addRule(`.monaco-editor.vs .valueSetReplacement { outline: solid 2px ${border}; }`);
    }
});
