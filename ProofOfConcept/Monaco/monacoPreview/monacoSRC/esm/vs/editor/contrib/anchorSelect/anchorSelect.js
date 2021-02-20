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
import './anchorSelect.css';
import { EditorAction, registerEditorAction, registerEditorContribution } from '../../browser/editorExtensions.js';
import { localize } from '../../../nls.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { Selection } from '../../common/core/selection.js';
import { KeyChord } from '../../../base/common/keyCodes.js';
import { RawContextKey, IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { MarkdownString } from '../../../base/common/htmlContent.js';
import { alert } from '../../../base/browser/ui/aria/aria.js';
export const SelectionAnchorSet = new RawContextKey('selectionAnchorSet', false);
let SelectionAnchorController = class SelectionAnchorController {
    constructor(editor, contextKeyService) {
        this.editor = editor;
        this.selectionAnchorSetContextKey = SelectionAnchorSet.bindTo(contextKeyService);
        this.modelChangeListener = editor.onDidChangeModel(() => this.selectionAnchorSetContextKey.reset());
    }
    static get(editor) {
        return editor.getContribution(SelectionAnchorController.ID);
    }
    setSelectionAnchor() {
        if (this.editor.hasModel()) {
            const position = this.editor.getPosition();
            const previousDecorations = this.decorationId ? [this.decorationId] : [];
            const newDecorationId = this.editor.deltaDecorations(previousDecorations, [{
                    range: Selection.fromPositions(position, position),
                    options: {
                        stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
                        hoverMessage: new MarkdownString().appendText(localize('selectionAnchor', "Selection Anchor")),
                        className: 'selection-anchor'
                    }
                }]);
            this.decorationId = newDecorationId[0];
            this.selectionAnchorSetContextKey.set(!!this.decorationId);
            alert(localize('anchorSet', "Anchor set at {0}:{1}", position.lineNumber, position.column));
        }
    }
    goToSelectionAnchor() {
        if (this.editor.hasModel() && this.decorationId) {
            const anchorPosition = this.editor.getModel().getDecorationRange(this.decorationId);
            if (anchorPosition) {
                this.editor.setPosition(anchorPosition.getStartPosition());
            }
        }
    }
    selectFromAnchorToCursor() {
        if (this.editor.hasModel() && this.decorationId) {
            const start = this.editor.getModel().getDecorationRange(this.decorationId);
            if (start) {
                const end = this.editor.getPosition();
                this.editor.setSelection(Selection.fromPositions(start.getStartPosition(), end));
                this.cancelSelectionAnchor();
            }
        }
    }
    cancelSelectionAnchor() {
        if (this.decorationId) {
            this.editor.deltaDecorations([this.decorationId], []);
            this.decorationId = undefined;
            this.selectionAnchorSetContextKey.set(false);
        }
    }
    dispose() {
        this.cancelSelectionAnchor();
        this.modelChangeListener.dispose();
    }
};
SelectionAnchorController.ID = 'editor.contrib.selectionAnchorController';
SelectionAnchorController = __decorate([
    __param(1, IContextKeyService)
], SelectionAnchorController);
class SetSelectionAnchor extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.setSelectionAnchor',
            label: localize('setSelectionAnchor', "Set Selection Anchor"),
            alias: 'Set Selection Anchor',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 32 /* KEY_B */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(_accessor, editor) {
        return __awaiter(this, void 0, void 0, function* () {
            const controller = SelectionAnchorController.get(editor);
            controller.setSelectionAnchor();
        });
    }
}
class GoToSelectionAnchor extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.goToSelectionAnchor',
            label: localize('goToSelectionAnchor', "Go to Selection Anchor"),
            alias: 'Go to Selection Anchor',
            precondition: SelectionAnchorSet,
        });
    }
    run(_accessor, editor) {
        return __awaiter(this, void 0, void 0, function* () {
            const controller = SelectionAnchorController.get(editor);
            controller.goToSelectionAnchor();
        });
    }
}
class SelectFromAnchorToCursor extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.selectFromAnchorToCursor',
            label: localize('selectFromAnchorToCursor', "Select from Anchor to Cursor"),
            alias: 'Select from Anchor to Cursor',
            precondition: SelectionAnchorSet,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 41 /* KEY_K */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(_accessor, editor) {
        return __awaiter(this, void 0, void 0, function* () {
            const controller = SelectionAnchorController.get(editor);
            controller.selectFromAnchorToCursor();
        });
    }
}
class CancelSelectionAnchor extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.cancelSelectionAnchor',
            label: localize('cancelSelectionAnchor', "Cancel Selection Anchor"),
            alias: 'Cancel Selection Anchor',
            precondition: SelectionAnchorSet,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 9 /* Escape */,
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(_accessor, editor) {
        return __awaiter(this, void 0, void 0, function* () {
            const controller = SelectionAnchorController.get(editor);
            controller.cancelSelectionAnchor();
        });
    }
}
registerEditorContribution(SelectionAnchorController.ID, SelectionAnchorController);
registerEditorAction(SetSelectionAnchor);
registerEditorAction(GoToSelectionAnchor);
registerEditorAction(SelectFromAnchorToCursor);
registerEditorAction(CancelSelectionAnchor);
