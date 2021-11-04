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
import { isNonEmptyArray } from '../../../base/common/arrays.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { KeyChord } from '../../../base/common/keyCodes.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import { EditorAction, registerEditorAction, registerEditorContribution } from '../../browser/editorExtensions.js';
import { ICodeEditorService } from '../../browser/services/codeEditorService.js';
import { CharacterSet } from '../../common/core/characterClassifier.js';
import { Range } from '../../common/core/range.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { DocumentRangeFormattingEditProviderRegistry, OnTypeFormattingEditProviderRegistry } from '../../common/modes.js';
import { IEditorWorkerService } from '../../common/services/editorWorkerService.js';
import { getOnTypeFormattingEdits, alertFormattingEdits, formatDocumentRangesWithSelectedProvider, formatDocumentWithSelectedProvider } from './format.js';
import { FormattingEdit } from './formattingEdit.js';
import * as nls from '../../../nls.js';
import { CommandsRegistry, ICommandService } from '../../../platform/commands/common/commands.js';
import { ContextKeyExpr } from '../../../platform/contextkey/common/contextkey.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { Progress, IEditorProgressService } from '../../../platform/progress/common/progress.js';
let FormatOnType = class FormatOnType {
    constructor(editor, _workerService) {
        this._workerService = _workerService;
        this._callOnDispose = new DisposableStore();
        this._callOnModel = new DisposableStore();
        this._editor = editor;
        this._callOnDispose.add(editor.onDidChangeConfiguration(() => this._update()));
        this._callOnDispose.add(editor.onDidChangeModel(() => this._update()));
        this._callOnDispose.add(editor.onDidChangeModelLanguage(() => this._update()));
        this._callOnDispose.add(OnTypeFormattingEditProviderRegistry.onDidChange(this._update, this));
    }
    dispose() {
        this._callOnDispose.dispose();
        this._callOnModel.dispose();
    }
    _update() {
        // clean up
        this._callOnModel.clear();
        // we are disabled
        if (!this._editor.getOption(43 /* formatOnType */)) {
            return;
        }
        // no model
        if (!this._editor.hasModel()) {
            return;
        }
        const model = this._editor.getModel();
        // no support
        const [support] = OnTypeFormattingEditProviderRegistry.ordered(model);
        if (!support || !support.autoFormatTriggerCharacters) {
            return;
        }
        // register typing listeners that will trigger the format
        let triggerChars = new CharacterSet();
        for (let ch of support.autoFormatTriggerCharacters) {
            triggerChars.add(ch.charCodeAt(0));
        }
        this._callOnModel.add(this._editor.onDidType((text) => {
            let lastCharCode = text.charCodeAt(text.length - 1);
            if (triggerChars.has(lastCharCode)) {
                this._trigger(String.fromCharCode(lastCharCode));
            }
        }));
    }
    _trigger(ch) {
        if (!this._editor.hasModel()) {
            return;
        }
        if (this._editor.getSelections().length > 1) {
            return;
        }
        const model = this._editor.getModel();
        const position = this._editor.getPosition();
        let canceled = false;
        // install a listener that checks if edits happens before the
        // position on which we format right now. If so, we won't
        // apply the format edits
        const unbind = this._editor.onDidChangeModelContent((e) => {
            if (e.isFlush) {
                // a model.setValue() was called
                // cancel only once
                canceled = true;
                unbind.dispose();
                return;
            }
            for (let i = 0, len = e.changes.length; i < len; i++) {
                const change = e.changes[i];
                if (change.range.endLineNumber <= position.lineNumber) {
                    // cancel only once
                    canceled = true;
                    unbind.dispose();
                    return;
                }
            }
        });
        getOnTypeFormattingEdits(this._workerService, model, position, ch, model.getFormattingOptions()).then(edits => {
            unbind.dispose();
            if (canceled) {
                return;
            }
            if (isNonEmptyArray(edits)) {
                FormattingEdit.execute(this._editor, edits, true);
                alertFormattingEdits(edits);
            }
        }, (err) => {
            unbind.dispose();
            throw err;
        });
    }
};
FormatOnType.ID = 'editor.contrib.autoFormat';
FormatOnType = __decorate([
    __param(1, IEditorWorkerService)
], FormatOnType);
let FormatOnPaste = class FormatOnPaste {
    constructor(editor, _instantiationService) {
        this.editor = editor;
        this._instantiationService = _instantiationService;
        this._callOnDispose = new DisposableStore();
        this._callOnModel = new DisposableStore();
        this._callOnDispose.add(editor.onDidChangeConfiguration(() => this._update()));
        this._callOnDispose.add(editor.onDidChangeModel(() => this._update()));
        this._callOnDispose.add(editor.onDidChangeModelLanguage(() => this._update()));
        this._callOnDispose.add(DocumentRangeFormattingEditProviderRegistry.onDidChange(this._update, this));
    }
    dispose() {
        this._callOnDispose.dispose();
        this._callOnModel.dispose();
    }
    _update() {
        // clean up
        this._callOnModel.clear();
        // we are disabled
        if (!this.editor.getOption(42 /* formatOnPaste */)) {
            return;
        }
        // no model
        if (!this.editor.hasModel()) {
            return;
        }
        // no formatter
        if (!DocumentRangeFormattingEditProviderRegistry.has(this.editor.getModel())) {
            return;
        }
        this._callOnModel.add(this.editor.onDidPaste(({ range }) => this._trigger(range)));
    }
    _trigger(range) {
        if (!this.editor.hasModel()) {
            return;
        }
        if (this.editor.getSelections().length > 1) {
            return;
        }
        this._instantiationService.invokeFunction(formatDocumentRangesWithSelectedProvider, this.editor, range, 2 /* Silent */, Progress.None, CancellationToken.None).catch(onUnexpectedError);
    }
};
FormatOnPaste.ID = 'editor.contrib.formatOnPaste';
FormatOnPaste = __decorate([
    __param(1, IInstantiationService)
], FormatOnPaste);
class FormatDocumentAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.formatDocument',
            label: nls.localize('formatDocument.label', "Format Document"),
            alias: 'Format Document',
            precondition: ContextKeyExpr.and(EditorContextKeys.notInCompositeEditor, EditorContextKeys.writable, EditorContextKeys.hasDocumentFormattingProvider),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 1024 /* Shift */ | 512 /* Alt */ | 36 /* KEY_F */,
                linux: { primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 39 /* KEY_I */ },
                weight: 100 /* EditorContrib */
            },
            contextMenuOpts: {
                group: '1_modification',
                order: 1.3
            }
        });
    }
    run(accessor, editor) {
        return __awaiter(this, void 0, void 0, function* () {
            if (editor.hasModel()) {
                const instaService = accessor.get(IInstantiationService);
                const progressService = accessor.get(IEditorProgressService);
                yield progressService.showWhile(instaService.invokeFunction(formatDocumentWithSelectedProvider, editor, 1 /* Explicit */, Progress.None, CancellationToken.None), 250);
            }
        });
    }
}
class FormatSelectionAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.formatSelection',
            label: nls.localize('formatSelection.label', "Format Selection"),
            alias: 'Format Selection',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.hasDocumentSelectionFormattingProvider),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 36 /* KEY_F */),
                weight: 100 /* EditorContrib */
            },
            contextMenuOpts: {
                when: EditorContextKeys.hasNonEmptySelection,
                group: '1_modification',
                order: 1.31
            }
        });
    }
    run(accessor, editor) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!editor.hasModel()) {
                return;
            }
            const instaService = accessor.get(IInstantiationService);
            const model = editor.getModel();
            const ranges = editor.getSelections().map(range => {
                return range.isEmpty()
                    ? new Range(range.startLineNumber, 1, range.startLineNumber, model.getLineMaxColumn(range.startLineNumber))
                    : range;
            });
            const progressService = accessor.get(IEditorProgressService);
            yield progressService.showWhile(instaService.invokeFunction(formatDocumentRangesWithSelectedProvider, editor, ranges, 1 /* Explicit */, Progress.None, CancellationToken.None), 250);
        });
    }
}
registerEditorContribution(FormatOnType.ID, FormatOnType);
registerEditorContribution(FormatOnPaste.ID, FormatOnPaste);
registerEditorAction(FormatDocumentAction);
registerEditorAction(FormatSelectionAction);
// this is the old format action that does both (format document OR format selection)
// and we keep it here such that existing keybinding configurations etc will still work
CommandsRegistry.registerCommand('editor.action.format', (accessor) => __awaiter(void 0, void 0, void 0, function* () {
    const editor = accessor.get(ICodeEditorService).getFocusedCodeEditor();
    if (!editor || !editor.hasModel()) {
        return;
    }
    const commandService = accessor.get(ICommandService);
    if (editor.getSelection().isEmpty()) {
        yield commandService.executeCommand('editor.action.formatDocument');
    }
    else {
        yield commandService.executeCommand('editor.action.formatSelection');
    }
}));
