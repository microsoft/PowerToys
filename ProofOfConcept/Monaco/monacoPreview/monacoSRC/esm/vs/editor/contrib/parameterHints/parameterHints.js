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
import { Disposable } from '../../../base/common/lifecycle.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { ContextKeyExpr } from '../../../platform/contextkey/common/contextkey.js';
import { registerEditorAction, registerEditorContribution, EditorAction, EditorCommand, registerEditorCommand } from '../../browser/editorExtensions.js';
import { ParameterHintsWidget } from './parameterHintsWidget.js';
import { Context } from './provideSignatureHelp.js';
import * as modes from '../../common/modes.js';
let ParameterHintsController = class ParameterHintsController extends Disposable {
    constructor(editor, instantiationService) {
        super();
        this.editor = editor;
        this.widget = this._register(instantiationService.createInstance(ParameterHintsWidget, this.editor));
    }
    static get(editor) {
        return editor.getContribution(ParameterHintsController.ID);
    }
    cancel() {
        this.widget.cancel();
    }
    previous() {
        this.widget.previous();
    }
    next() {
        this.widget.next();
    }
    trigger(context) {
        this.widget.trigger(context);
    }
};
ParameterHintsController.ID = 'editor.controller.parameterHints';
ParameterHintsController = __decorate([
    __param(1, IInstantiationService)
], ParameterHintsController);
export class TriggerParameterHintsAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.triggerParameterHints',
            label: nls.localize('parameterHints.trigger.label', "Trigger Parameter Hints"),
            alias: 'Trigger Parameter Hints',
            precondition: EditorContextKeys.hasSignatureHelpProvider,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 10 /* Space */,
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(accessor, editor) {
        const controller = ParameterHintsController.get(editor);
        if (controller) {
            controller.trigger({
                triggerKind: modes.SignatureHelpTriggerKind.Invoke
            });
        }
    }
}
registerEditorContribution(ParameterHintsController.ID, ParameterHintsController);
registerEditorAction(TriggerParameterHintsAction);
const weight = 100 /* EditorContrib */ + 75;
const ParameterHintsCommand = EditorCommand.bindToContribution(ParameterHintsController.get);
registerEditorCommand(new ParameterHintsCommand({
    id: 'closeParameterHints',
    precondition: Context.Visible,
    handler: x => x.cancel(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.focus,
        primary: 9 /* Escape */,
        secondary: [1024 /* Shift */ | 9 /* Escape */]
    }
}));
registerEditorCommand(new ParameterHintsCommand({
    id: 'showPrevParameterHint',
    precondition: ContextKeyExpr.and(Context.Visible, Context.MultipleSignatures),
    handler: x => x.previous(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.focus,
        primary: 16 /* UpArrow */,
        secondary: [512 /* Alt */ | 16 /* UpArrow */],
        mac: { primary: 16 /* UpArrow */, secondary: [512 /* Alt */ | 16 /* UpArrow */, 256 /* WinCtrl */ | 46 /* KEY_P */] }
    }
}));
registerEditorCommand(new ParameterHintsCommand({
    id: 'showNextParameterHint',
    precondition: ContextKeyExpr.and(Context.Visible, Context.MultipleSignatures),
    handler: x => x.next(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.focus,
        primary: 18 /* DownArrow */,
        secondary: [512 /* Alt */ | 18 /* DownArrow */],
        mac: { primary: 18 /* DownArrow */, secondary: [512 /* Alt */ | 18 /* DownArrow */, 256 /* WinCtrl */ | 44 /* KEY_N */] }
    }
}));
