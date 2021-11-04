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
import { DisposableStore } from '../../../base/common/lifecycle.js';
import { EditorCommand, registerEditorCommand, registerEditorContribution } from '../../browser/editorExtensions.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { showSimpleSuggestions } from '../suggest/suggest.js';
import { ContextKeyExpr, IContextKeyService, RawContextKey } from '../../../platform/contextkey/common/contextkey.js';
import { ILogService } from '../../../platform/log/common/log.js';
import { SnippetSession } from './snippetSession.js';
const _defaultOptions = {
    overwriteBefore: 0,
    overwriteAfter: 0,
    undoStopBefore: true,
    undoStopAfter: true,
    adjustWhitespace: true,
    clipboardText: undefined,
    overtypingCapturer: undefined
};
let SnippetController2 = class SnippetController2 {
    constructor(_editor, _logService, contextKeyService) {
        this._editor = _editor;
        this._logService = _logService;
        this._snippetListener = new DisposableStore();
        this._modelVersionId = -1;
        this._inSnippet = SnippetController2.InSnippetMode.bindTo(contextKeyService);
        this._hasNextTabstop = SnippetController2.HasNextTabstop.bindTo(contextKeyService);
        this._hasPrevTabstop = SnippetController2.HasPrevTabstop.bindTo(contextKeyService);
    }
    static get(editor) {
        return editor.getContribution(SnippetController2.ID);
    }
    dispose() {
        var _a;
        this._inSnippet.reset();
        this._hasPrevTabstop.reset();
        this._hasNextTabstop.reset();
        (_a = this._session) === null || _a === void 0 ? void 0 : _a.dispose();
        this._snippetListener.dispose();
    }
    insert(template, opts) {
        // this is here to find out more about the yet-not-understood
        // error that sometimes happens when we fail to inserted a nested
        // snippet
        try {
            this._doInsert(template, typeof opts === 'undefined' ? _defaultOptions : Object.assign(Object.assign({}, _defaultOptions), opts));
        }
        catch (e) {
            this.cancel();
            this._logService.error(e);
            this._logService.error('snippet_error');
            this._logService.error('insert_template=', template);
            this._logService.error('existing_template=', this._session ? this._session._logInfo() : '<no_session>');
        }
    }
    _doInsert(template, opts) {
        if (!this._editor.hasModel()) {
            return;
        }
        // don't listen while inserting the snippet
        // as that is the inflight state causing cancelation
        this._snippetListener.clear();
        if (opts.undoStopBefore) {
            this._editor.getModel().pushStackElement();
        }
        if (!this._session) {
            this._modelVersionId = this._editor.getModel().getAlternativeVersionId();
            this._session = new SnippetSession(this._editor, template, opts);
            this._session.insert();
        }
        else {
            this._session.merge(template, opts);
        }
        if (opts.undoStopAfter) {
            this._editor.getModel().pushStackElement();
        }
        this._updateState();
        this._snippetListener.add(this._editor.onDidChangeModelContent(e => e.isFlush && this.cancel()));
        this._snippetListener.add(this._editor.onDidChangeModel(() => this.cancel()));
        this._snippetListener.add(this._editor.onDidChangeCursorSelection(() => this._updateState()));
    }
    _updateState() {
        if (!this._session || !this._editor.hasModel()) {
            // canceled in the meanwhile
            return;
        }
        if (this._modelVersionId === this._editor.getModel().getAlternativeVersionId()) {
            // undo until the 'before' state happened
            // and makes use cancel snippet mode
            return this.cancel();
        }
        if (!this._session.hasPlaceholder) {
            // don't listen for selection changes and don't
            // update context keys when the snippet is plain text
            return this.cancel();
        }
        if (this._session.isAtLastPlaceholder || !this._session.isSelectionWithinPlaceholders()) {
            return this.cancel();
        }
        this._inSnippet.set(true);
        this._hasPrevTabstop.set(!this._session.isAtFirstPlaceholder);
        this._hasNextTabstop.set(!this._session.isAtLastPlaceholder);
        this._handleChoice();
    }
    _handleChoice() {
        if (!this._session || !this._editor.hasModel()) {
            this._currentChoice = undefined;
            return;
        }
        const { choice } = this._session;
        if (!choice) {
            this._currentChoice = undefined;
            return;
        }
        if (this._currentChoice !== choice) {
            this._currentChoice = choice;
            this._editor.setSelections(this._editor.getSelections()
                .map(s => Selection.fromPositions(s.getStartPosition())));
            const [first] = choice.options;
            showSimpleSuggestions(this._editor, choice.options.map((option, i) => {
                // let before = choice.options.slice(0, i);
                // let after = choice.options.slice(i);
                return {
                    kind: 13 /* Value */,
                    label: option.value,
                    insertText: option.value,
                    // insertText: `\${1|${after.concat(before).join(',')}|}$0`,
                    // snippetType: 'textmate',
                    sortText: 'a'.repeat(i + 1),
                    range: Range.fromPositions(this._editor.getPosition(), this._editor.getPosition().delta(0, first.value.length))
                };
            }));
        }
    }
    finish() {
        while (this._inSnippet.get()) {
            this.next();
        }
    }
    cancel(resetSelection = false) {
        var _a;
        this._inSnippet.reset();
        this._hasPrevTabstop.reset();
        this._hasNextTabstop.reset();
        this._snippetListener.clear();
        (_a = this._session) === null || _a === void 0 ? void 0 : _a.dispose();
        this._session = undefined;
        this._modelVersionId = -1;
        if (resetSelection) {
            // reset selection to the primary cursor when being asked
            // for. this happens when explicitly cancelling snippet mode,
            // e.g. when pressing ESC
            this._editor.setSelections([this._editor.getSelection()]);
        }
    }
    prev() {
        if (this._session) {
            this._session.prev();
        }
        this._updateState();
    }
    next() {
        if (this._session) {
            this._session.next();
        }
        this._updateState();
    }
    isInSnippet() {
        return Boolean(this._inSnippet.get());
    }
};
SnippetController2.ID = 'snippetController2';
SnippetController2.InSnippetMode = new RawContextKey('inSnippetMode', false);
SnippetController2.HasNextTabstop = new RawContextKey('hasNextTabstop', false);
SnippetController2.HasPrevTabstop = new RawContextKey('hasPrevTabstop', false);
SnippetController2 = __decorate([
    __param(1, ILogService),
    __param(2, IContextKeyService)
], SnippetController2);
export { SnippetController2 };
registerEditorContribution(SnippetController2.ID, SnippetController2);
const CommandCtor = EditorCommand.bindToContribution(SnippetController2.get);
registerEditorCommand(new CommandCtor({
    id: 'jumpToNextSnippetPlaceholder',
    precondition: ContextKeyExpr.and(SnippetController2.InSnippetMode, SnippetController2.HasNextTabstop),
    handler: ctrl => ctrl.next(),
    kbOpts: {
        weight: 100 /* EditorContrib */ + 30,
        kbExpr: EditorContextKeys.editorTextFocus,
        primary: 2 /* Tab */
    }
}));
registerEditorCommand(new CommandCtor({
    id: 'jumpToPrevSnippetPlaceholder',
    precondition: ContextKeyExpr.and(SnippetController2.InSnippetMode, SnippetController2.HasPrevTabstop),
    handler: ctrl => ctrl.prev(),
    kbOpts: {
        weight: 100 /* EditorContrib */ + 30,
        kbExpr: EditorContextKeys.editorTextFocus,
        primary: 1024 /* Shift */ | 2 /* Tab */
    }
}));
registerEditorCommand(new CommandCtor({
    id: 'leaveSnippet',
    precondition: SnippetController2.InSnippetMode,
    handler: ctrl => ctrl.cancel(true),
    kbOpts: {
        weight: 100 /* EditorContrib */ + 30,
        kbExpr: EditorContextKeys.editorTextFocus,
        primary: 9 /* Escape */,
        secondary: [1024 /* Shift */ | 9 /* Escape */]
    }
}));
registerEditorCommand(new CommandCtor({
    id: 'acceptSnippet',
    precondition: SnippetController2.InSnippetMode,
    handler: ctrl => ctrl.finish(),
    // kbOpts: {
    // 	weight: KeybindingWeight.EditorContrib + 30,
    // 	kbExpr: EditorContextKeys.textFocus,
    // 	primary: KeyCode.Enter,
    // }
}));
