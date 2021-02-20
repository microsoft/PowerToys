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
import { alert } from '../../../base/browser/ui/aria/aria.js';
import { isNonEmptyArray } from '../../../base/common/arrays.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { SimpleKeybinding } from '../../../base/common/keyCodes.js';
import { dispose, DisposableStore, toDisposable, MutableDisposable } from '../../../base/common/lifecycle.js';
import { StableEditorScrollState } from '../../browser/core/editorState.js';
import { EditorAction, EditorCommand, registerEditorAction, registerEditorCommand, registerEditorContribution } from '../../browser/editorExtensions.js';
import { EditOperation } from '../../common/core/editOperation.js';
import { Range } from '../../common/core/range.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { SnippetController2 } from '../snippet/snippetController2.js';
import { SnippetParser } from '../snippet/snippetParser.js';
import { ISuggestMemoryService } from './suggestMemory.js';
import * as nls from '../../../nls.js';
import { ICommandService, CommandsRegistry } from '../../../platform/commands/common/commands.js';
import { ContextKeyExpr, IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { KeybindingsRegistry } from '../../../platform/keybinding/common/keybindingsRegistry.js';
import { Context as SuggestContext, suggestWidgetStatusbarMenu } from './suggest.js';
import { SuggestAlternatives } from './suggestAlternatives.js';
import { SuggestModel } from './suggestModel.js';
import { SuggestWidget } from './suggestWidget.js';
import { WordContextKey } from './wordContextKey.js';
import { Event } from '../../../base/common/event.js';
import { IdleValue } from '../../../base/common/async.js';
import { isObject, assertType } from '../../../base/common/types.js';
import { CommitCharacterController } from './suggestCommitCharacters.js';
import { OvertypingCapturer } from './suggestOvertypingCapturer.js';
import { Position } from '../../common/core/position.js';
import * as platform from '../../../base/common/platform.js';
import { MenuRegistry } from '../../../platform/actions/common/actions.js';
import { CancellationTokenSource } from '../../../base/common/cancellation.js';
import { ILogService } from '../../../platform/log/common/log.js';
import { StopWatch } from '../../../base/common/stopwatch.js';
// sticky suggest widget which doesn't disappear on focus out and such
let _sticky = false;
// _sticky = Boolean("true"); // done "weirdly" so that a lint warning prevents you from pushing this
class LineSuffix {
    constructor(_model, _position) {
        this._model = _model;
        this._position = _position;
        // spy on what's happening right of the cursor. two cases:
        // 1. end of line -> check that it's still end of line
        // 2. mid of line -> add a marker and compute the delta
        const maxColumn = _model.getLineMaxColumn(_position.lineNumber);
        if (maxColumn !== _position.column) {
            const offset = _model.getOffsetAt(_position);
            const end = _model.getPositionAt(offset + 1);
            this._marker = _model.deltaDecorations([], [{
                    range: Range.fromPositions(_position, end),
                    options: { stickiness: 1 /* NeverGrowsWhenTypingAtEdges */ }
                }]);
        }
    }
    dispose() {
        if (this._marker && !this._model.isDisposed()) {
            this._model.deltaDecorations(this._marker, []);
        }
    }
    delta(position) {
        if (this._model.isDisposed() || this._position.lineNumber !== position.lineNumber) {
            // bail out early if things seems fishy
            return 0;
        }
        // read the marker (in case suggest was triggered at line end) or compare
        // the cursor to the line end.
        if (this._marker) {
            const range = this._model.getDecorationRange(this._marker[0]);
            const end = this._model.getOffsetAt(range.getStartPosition());
            return end - this._model.getOffsetAt(position);
        }
        else {
            return this._model.getLineMaxColumn(position.lineNumber) - position.column;
        }
    }
}
let SuggestController = class SuggestController {
    constructor(editor, _memoryService, _commandService, _contextKeyService, _instantiationService, _logService) {
        this._memoryService = _memoryService;
        this._commandService = _commandService;
        this._contextKeyService = _contextKeyService;
        this._instantiationService = _instantiationService;
        this._logService = _logService;
        this._lineSuffix = new MutableDisposable();
        this._toDispose = new DisposableStore();
        this.editor = editor;
        this.model = _instantiationService.createInstance(SuggestModel, this.editor);
        // context key: update insert/replace mode
        const ctxInsertMode = SuggestContext.InsertMode.bindTo(_contextKeyService);
        ctxInsertMode.set(editor.getOption(101 /* suggest */).insertMode);
        this.model.onDidTrigger(() => ctxInsertMode.set(editor.getOption(101 /* suggest */).insertMode));
        this.widget = this._toDispose.add(new IdleValue(() => {
            const widget = this._instantiationService.createInstance(SuggestWidget, this.editor);
            this._toDispose.add(widget);
            this._toDispose.add(widget.onDidSelect(item => this._insertSuggestion(item, 0), this));
            // Wire up logic to accept a suggestion on certain characters
            const commitCharacterController = new CommitCharacterController(this.editor, widget, item => this._insertSuggestion(item, 2 /* NoAfterUndoStop */));
            this._toDispose.add(commitCharacterController);
            this._toDispose.add(this.model.onDidSuggest(e => {
                if (e.completionModel.items.length === 0) {
                    commitCharacterController.reset();
                }
            }));
            // Wire up makes text edit context key
            const ctxMakesTextEdit = SuggestContext.MakesTextEdit.bindTo(this._contextKeyService);
            const ctxHasInsertAndReplace = SuggestContext.HasInsertAndReplaceRange.bindTo(this._contextKeyService);
            const ctxCanResolve = SuggestContext.CanResolve.bindTo(this._contextKeyService);
            this._toDispose.add(toDisposable(() => {
                ctxMakesTextEdit.reset();
                ctxHasInsertAndReplace.reset();
                ctxCanResolve.reset();
            }));
            this._toDispose.add(widget.onDidFocus(({ item }) => {
                // (ctx: makesTextEdit)
                const position = this.editor.getPosition();
                const startColumn = item.editStart.column;
                const endColumn = position.column;
                let value = true;
                if (this.editor.getOption(1 /* acceptSuggestionOnEnter */) === 'smart'
                    && this.model.state === 2 /* Auto */
                    && !item.completion.command
                    && !item.completion.additionalTextEdits
                    && !(item.completion.insertTextRules & 4 /* InsertAsSnippet */)
                    && endColumn - startColumn === item.completion.insertText.length) {
                    const oldText = this.editor.getModel().getValueInRange({
                        startLineNumber: position.lineNumber,
                        startColumn,
                        endLineNumber: position.lineNumber,
                        endColumn
                    });
                    value = oldText !== item.completion.insertText;
                }
                ctxMakesTextEdit.set(value);
                // (ctx: hasInsertAndReplaceRange)
                ctxHasInsertAndReplace.set(!Position.equals(item.editInsertEnd, item.editReplaceEnd));
                // (ctx: canResolve)
                ctxCanResolve.set(Boolean(item.provider.resolveCompletionItem) || Boolean(item.completion.documentation) || item.completion.detail !== item.completion.label);
            }));
            this._toDispose.add(widget.onDetailsKeyDown(e => {
                // cmd + c on macOS, ctrl + c on Win / Linux
                if (e.toKeybinding().equals(new SimpleKeybinding(true, false, false, false, 33 /* KEY_C */)) ||
                    (platform.isMacintosh && e.toKeybinding().equals(new SimpleKeybinding(false, false, false, true, 33 /* KEY_C */)))) {
                    e.stopPropagation();
                    return;
                }
                if (!e.toKeybinding().isModifierKey()) {
                    this.editor.focus();
                }
            }));
            return widget;
        }));
        // Wire up text overtyping capture
        this._overtypingCapturer = this._toDispose.add(new IdleValue(() => {
            return this._toDispose.add(new OvertypingCapturer(this.editor, this.model));
        }));
        this._alternatives = this._toDispose.add(new IdleValue(() => {
            return this._toDispose.add(new SuggestAlternatives(this.editor, this._contextKeyService));
        }));
        this._toDispose.add(_instantiationService.createInstance(WordContextKey, editor));
        this._toDispose.add(this.model.onDidTrigger(e => {
            this.widget.value.showTriggered(e.auto, e.shy ? 250 : 50);
            this._lineSuffix.value = new LineSuffix(this.editor.getModel(), e.position);
        }));
        this._toDispose.add(this.model.onDidSuggest(e => {
            if (!e.shy) {
                let index = this._memoryService.select(this.editor.getModel(), this.editor.getPosition(), e.completionModel.items);
                this.widget.value.showSuggestions(e.completionModel, index, e.isFrozen, e.auto);
            }
        }));
        this._toDispose.add(this.model.onDidCancel(e => {
            if (!e.retrigger) {
                this.widget.value.hideWidget();
            }
        }));
        this._toDispose.add(this.editor.onDidBlurEditorWidget(() => {
            if (!_sticky) {
                this.model.cancel();
                this.model.clear();
            }
        }));
        // Manage the acceptSuggestionsOnEnter context key
        let acceptSuggestionsOnEnter = SuggestContext.AcceptSuggestionsOnEnter.bindTo(_contextKeyService);
        let updateFromConfig = () => {
            const acceptSuggestionOnEnter = this.editor.getOption(1 /* acceptSuggestionOnEnter */);
            acceptSuggestionsOnEnter.set(acceptSuggestionOnEnter === 'on' || acceptSuggestionOnEnter === 'smart');
        };
        this._toDispose.add(this.editor.onDidChangeConfiguration(() => updateFromConfig()));
        updateFromConfig();
    }
    static get(editor) {
        return editor.getContribution(SuggestController.ID);
    }
    dispose() {
        this._alternatives.dispose();
        this._toDispose.dispose();
        this.widget.dispose();
        this.model.dispose();
        this._lineSuffix.dispose();
    }
    _insertSuggestion(event, flags) {
        if (!event || !event.item) {
            this._alternatives.value.reset();
            this.model.cancel();
            this.model.clear();
            return;
        }
        if (!this.editor.hasModel()) {
            return;
        }
        const model = this.editor.getModel();
        const modelVersionNow = model.getAlternativeVersionId();
        const { item } = event;
        //
        const tasks = [];
        const cts = new CancellationTokenSource();
        // pushing undo stops *before* additional text edits and
        // *after* the main edit
        if (!(flags & 1 /* NoBeforeUndoStop */)) {
            this.editor.pushUndoStop();
        }
        // compute overwrite[Before|After] deltas BEFORE applying extra edits
        const info = this.getOverwriteInfo(item, Boolean(flags & 8 /* AlternativeOverwriteConfig */));
        // keep item in memory
        this._memoryService.memorize(model, this.editor.getPosition(), item);
        if (Array.isArray(item.completion.additionalTextEdits)) {
            // sync additional edits
            const scrollState = StableEditorScrollState.capture(this.editor);
            this.editor.executeEdits('suggestController.additionalTextEdits.sync', item.completion.additionalTextEdits.map(edit => EditOperation.replace(Range.lift(edit.range), edit.text)));
            scrollState.restoreRelativeVerticalPositionOfCursor(this.editor);
        }
        else if (!item.isResolved) {
            // async additional edits
            const sw = new StopWatch(true);
            let position;
            const docListener = model.onDidChangeContent(e => {
                if (e.isFlush) {
                    cts.cancel();
                    docListener.dispose();
                    return;
                }
                for (let change of e.changes) {
                    const thisPosition = Range.getEndPosition(change.range);
                    if (!position || Position.isBefore(thisPosition, position)) {
                        position = thisPosition;
                    }
                }
            });
            let oldFlags = flags;
            flags |= 2 /* NoAfterUndoStop */;
            let didType = false;
            let typeListener = this.editor.onWillType(() => {
                typeListener.dispose();
                didType = true;
                if (!(oldFlags & 2 /* NoAfterUndoStop */)) {
                    this.editor.pushUndoStop();
                }
            });
            tasks.push(item.resolve(cts.token).then(() => {
                if (!item.completion.additionalTextEdits || cts.token.isCancellationRequested) {
                    return false;
                }
                if (position && item.completion.additionalTextEdits.some(edit => Position.isBefore(position, Range.getStartPosition(edit.range)))) {
                    return false;
                }
                if (didType) {
                    this.editor.pushUndoStop();
                }
                const scrollState = StableEditorScrollState.capture(this.editor);
                this.editor.executeEdits('suggestController.additionalTextEdits.async', item.completion.additionalTextEdits.map(edit => EditOperation.replace(Range.lift(edit.range), edit.text)));
                scrollState.restoreRelativeVerticalPositionOfCursor(this.editor);
                if (didType || !(oldFlags & 2 /* NoAfterUndoStop */)) {
                    this.editor.pushUndoStop();
                }
                return true;
            }).then(applied => {
                this._logService.trace('[suggest] async resolving of edits DONE (ms, applied?)', sw.elapsed(), applied);
                docListener.dispose();
                typeListener.dispose();
            }));
        }
        let { insertText } = item.completion;
        if (!(item.completion.insertTextRules & 4 /* InsertAsSnippet */)) {
            insertText = SnippetParser.escape(insertText);
        }
        SnippetController2.get(this.editor).insert(insertText, {
            overwriteBefore: info.overwriteBefore,
            overwriteAfter: info.overwriteAfter,
            undoStopBefore: false,
            undoStopAfter: false,
            adjustWhitespace: !(item.completion.insertTextRules & 1 /* KeepWhitespace */),
            clipboardText: event.model.clipboardText,
            overtypingCapturer: this._overtypingCapturer.value
        });
        if (!(flags & 2 /* NoAfterUndoStop */)) {
            this.editor.pushUndoStop();
        }
        if (!item.completion.command) {
            // done
            this.model.cancel();
        }
        else if (item.completion.command.id === TriggerSuggestAction.id) {
            // retigger
            this.model.trigger({ auto: true, shy: false }, true);
        }
        else {
            // exec command, done
            tasks.push(this._commandService.executeCommand(item.completion.command.id, ...(item.completion.command.arguments ? [...item.completion.command.arguments] : [])).catch(onUnexpectedError));
            this.model.cancel();
        }
        if (flags & 4 /* KeepAlternativeSuggestions */) {
            this._alternatives.value.set(event, next => {
                // cancel resolving of additional edits
                cts.cancel();
                // this is not so pretty. when inserting the 'next'
                // suggestion we undo until we are at the state at
                // which we were before inserting the previous suggestion...
                while (model.canUndo()) {
                    if (modelVersionNow !== model.getAlternativeVersionId()) {
                        model.undo();
                    }
                    this._insertSuggestion(next, 1 /* NoBeforeUndoStop */ | 2 /* NoAfterUndoStop */ | (flags & 8 /* AlternativeOverwriteConfig */ ? 8 /* AlternativeOverwriteConfig */ : 0));
                    break;
                }
            });
        }
        this._alertCompletionItem(item);
        // clear only now - after all tasks are done
        Promise.all(tasks).finally(() => {
            this.model.clear();
            cts.dispose();
        });
    }
    getOverwriteInfo(item, toggleMode) {
        assertType(this.editor.hasModel());
        let replace = this.editor.getOption(101 /* suggest */).insertMode === 'replace';
        if (toggleMode) {
            replace = !replace;
        }
        const overwriteBefore = item.position.column - item.editStart.column;
        const overwriteAfter = (replace ? item.editReplaceEnd.column : item.editInsertEnd.column) - item.position.column;
        const columnDelta = this.editor.getPosition().column - item.position.column;
        const suffixDelta = this._lineSuffix.value ? this._lineSuffix.value.delta(this.editor.getPosition()) : 0;
        return {
            overwriteBefore: overwriteBefore + columnDelta,
            overwriteAfter: overwriteAfter + suffixDelta
        };
    }
    _alertCompletionItem({ completion: suggestion }) {
        const textLabel = typeof suggestion.label === 'string' ? suggestion.label : suggestion.label.name;
        if (isNonEmptyArray(suggestion.additionalTextEdits)) {
            let msg = nls.localize('aria.alert.snippet', "Accepting '{0}' made {1} additional edits", textLabel, suggestion.additionalTextEdits.length);
            alert(msg);
        }
    }
    triggerSuggest(onlyFrom) {
        if (this.editor.hasModel()) {
            this.model.trigger({ auto: false, shy: false }, false, onlyFrom);
            this.editor.revealLine(this.editor.getPosition().lineNumber, 0 /* Smooth */);
            this.editor.focus();
        }
    }
    triggerSuggestAndAcceptBest(arg) {
        if (!this.editor.hasModel()) {
            return;
        }
        const positionNow = this.editor.getPosition();
        const fallback = () => {
            if (positionNow.equals(this.editor.getPosition())) {
                this._commandService.executeCommand(arg.fallback);
            }
        };
        const makesTextEdit = (item) => {
            if (item.completion.insertTextRules & 4 /* InsertAsSnippet */ || item.completion.additionalTextEdits) {
                // snippet, other editor -> makes edit
                return true;
            }
            const position = this.editor.getPosition();
            const startColumn = item.editStart.column;
            const endColumn = position.column;
            if (endColumn - startColumn !== item.completion.insertText.length) {
                // unequal lengths -> makes edit
                return true;
            }
            const textNow = this.editor.getModel().getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn,
                endLineNumber: position.lineNumber,
                endColumn
            });
            // unequal text -> makes edit
            return textNow !== item.completion.insertText;
        };
        Event.once(this.model.onDidTrigger)(_ => {
            // wait for trigger because only then the cancel-event is trustworthy
            let listener = [];
            Event.any(this.model.onDidTrigger, this.model.onDidCancel)(() => {
                // retrigger or cancel -> try to type default text
                dispose(listener);
                fallback();
            }, undefined, listener);
            this.model.onDidSuggest(({ completionModel }) => {
                dispose(listener);
                if (completionModel.items.length === 0) {
                    fallback();
                    return;
                }
                const index = this._memoryService.select(this.editor.getModel(), this.editor.getPosition(), completionModel.items);
                const item = completionModel.items[index];
                if (!makesTextEdit(item)) {
                    fallback();
                    return;
                }
                this.editor.pushUndoStop();
                this._insertSuggestion({ index, item, model: completionModel }, 4 /* KeepAlternativeSuggestions */ | 1 /* NoBeforeUndoStop */ | 2 /* NoAfterUndoStop */);
            }, undefined, listener);
        });
        this.model.trigger({ auto: false, shy: true });
        this.editor.revealLine(positionNow.lineNumber, 0 /* Smooth */);
        this.editor.focus();
    }
    acceptSelectedSuggestion(keepAlternativeSuggestions, alternativeOverwriteConfig) {
        const item = this.widget.value.getFocusedItem();
        let flags = 0;
        if (keepAlternativeSuggestions) {
            flags |= 4 /* KeepAlternativeSuggestions */;
        }
        if (alternativeOverwriteConfig) {
            flags |= 8 /* AlternativeOverwriteConfig */;
        }
        this._insertSuggestion(item, flags);
    }
    acceptNextSuggestion() {
        this._alternatives.value.next();
    }
    acceptPrevSuggestion() {
        this._alternatives.value.prev();
    }
    cancelSuggestWidget() {
        this.model.cancel();
        this.model.clear();
        this.widget.value.hideWidget();
    }
    selectNextSuggestion() {
        this.widget.value.selectNext();
    }
    selectNextPageSuggestion() {
        this.widget.value.selectNextPage();
    }
    selectLastSuggestion() {
        this.widget.value.selectLast();
    }
    selectPrevSuggestion() {
        this.widget.value.selectPrevious();
    }
    selectPrevPageSuggestion() {
        this.widget.value.selectPreviousPage();
    }
    selectFirstSuggestion() {
        this.widget.value.selectFirst();
    }
    toggleSuggestionDetails() {
        this.widget.value.toggleDetails();
    }
    toggleExplainMode() {
        this.widget.value.toggleExplainMode();
    }
    toggleSuggestionFocus() {
        this.widget.value.toggleDetailsFocus();
    }
    resetWidgetSize() {
        this.widget.value.resetPersistedSize();
    }
};
SuggestController.ID = 'editor.contrib.suggestController';
SuggestController = __decorate([
    __param(1, ISuggestMemoryService),
    __param(2, ICommandService),
    __param(3, IContextKeyService),
    __param(4, IInstantiationService),
    __param(5, ILogService)
], SuggestController);
export { SuggestController };
export class TriggerSuggestAction extends EditorAction {
    constructor() {
        super({
            id: TriggerSuggestAction.id,
            label: nls.localize('suggest.trigger.label', "Trigger Suggest"),
            alias: 'Trigger Suggest',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.hasCompletionItemProvider),
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 2048 /* CtrlCmd */ | 10 /* Space */,
                secondary: [2048 /* CtrlCmd */ | 39 /* KEY_I */],
                mac: { primary: 256 /* WinCtrl */ | 10 /* Space */, secondary: [512 /* Alt */ | 9 /* Escape */, 2048 /* CtrlCmd */ | 39 /* KEY_I */] },
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(accessor, editor) {
        const controller = SuggestController.get(editor);
        if (!controller) {
            return;
        }
        controller.triggerSuggest();
    }
}
TriggerSuggestAction.id = 'editor.action.triggerSuggest';
registerEditorContribution(SuggestController.ID, SuggestController);
registerEditorAction(TriggerSuggestAction);
const weight = 100 /* EditorContrib */ + 90;
const SuggestCommand = EditorCommand.bindToContribution(SuggestController.get);
registerEditorCommand(new SuggestCommand({
    id: 'acceptSelectedSuggestion',
    precondition: SuggestContext.Visible,
    handler(x) {
        x.acceptSelectedSuggestion(true, false);
    }
}));
// normal tab
KeybindingsRegistry.registerKeybindingRule({
    id: 'acceptSelectedSuggestion',
    when: ContextKeyExpr.and(SuggestContext.Visible, EditorContextKeys.textInputFocus),
    primary: 2 /* Tab */,
    weight
});
// accept on enter has special rules
KeybindingsRegistry.registerKeybindingRule({
    id: 'acceptSelectedSuggestion',
    when: ContextKeyExpr.and(SuggestContext.Visible, EditorContextKeys.textInputFocus, SuggestContext.AcceptSuggestionsOnEnter, SuggestContext.MakesTextEdit),
    primary: 3 /* Enter */,
    weight,
});
MenuRegistry.appendMenuItem(suggestWidgetStatusbarMenu, {
    command: { id: 'acceptSelectedSuggestion', title: nls.localize('accept.insert', "Insert") },
    group: 'left',
    order: 1,
    when: SuggestContext.HasInsertAndReplaceRange.toNegated()
});
MenuRegistry.appendMenuItem(suggestWidgetStatusbarMenu, {
    command: { id: 'acceptSelectedSuggestion', title: nls.localize('accept.insert', "Insert") },
    group: 'left',
    order: 1,
    when: ContextKeyExpr.and(SuggestContext.HasInsertAndReplaceRange, SuggestContext.InsertMode.isEqualTo('insert'))
});
MenuRegistry.appendMenuItem(suggestWidgetStatusbarMenu, {
    command: { id: 'acceptSelectedSuggestion', title: nls.localize('accept.replace', "Replace") },
    group: 'left',
    order: 1,
    when: ContextKeyExpr.and(SuggestContext.HasInsertAndReplaceRange, SuggestContext.InsertMode.isEqualTo('replace'))
});
registerEditorCommand(new SuggestCommand({
    id: 'acceptAlternativeSelectedSuggestion',
    precondition: ContextKeyExpr.and(SuggestContext.Visible, EditorContextKeys.textInputFocus),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 1024 /* Shift */ | 3 /* Enter */,
        secondary: [1024 /* Shift */ | 2 /* Tab */],
    },
    handler(x) {
        x.acceptSelectedSuggestion(false, true);
    },
    menuOpts: [{
            menuId: suggestWidgetStatusbarMenu,
            group: 'left',
            order: 2,
            when: ContextKeyExpr.and(SuggestContext.HasInsertAndReplaceRange, SuggestContext.InsertMode.isEqualTo('insert')),
            title: nls.localize('accept.replace', "Replace")
        }, {
            menuId: suggestWidgetStatusbarMenu,
            group: 'left',
            order: 2,
            when: ContextKeyExpr.and(SuggestContext.HasInsertAndReplaceRange, SuggestContext.InsertMode.isEqualTo('replace')),
            title: nls.localize('accept.insert', "Insert")
        }]
}));
// continue to support the old command
CommandsRegistry.registerCommandAlias('acceptSelectedSuggestionOnEnter', 'acceptSelectedSuggestion');
registerEditorCommand(new SuggestCommand({
    id: 'hideSuggestWidget',
    precondition: SuggestContext.Visible,
    handler: x => x.cancelSuggestWidget(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 9 /* Escape */,
        secondary: [1024 /* Shift */ | 9 /* Escape */]
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'selectNextSuggestion',
    precondition: ContextKeyExpr.and(SuggestContext.Visible, SuggestContext.MultipleSuggestions),
    handler: c => c.selectNextSuggestion(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 18 /* DownArrow */,
        secondary: [2048 /* CtrlCmd */ | 18 /* DownArrow */],
        mac: { primary: 18 /* DownArrow */, secondary: [2048 /* CtrlCmd */ | 18 /* DownArrow */, 256 /* WinCtrl */ | 44 /* KEY_N */] }
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'selectNextPageSuggestion',
    precondition: ContextKeyExpr.and(SuggestContext.Visible, SuggestContext.MultipleSuggestions),
    handler: c => c.selectNextPageSuggestion(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 12 /* PageDown */,
        secondary: [2048 /* CtrlCmd */ | 12 /* PageDown */]
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'selectLastSuggestion',
    precondition: ContextKeyExpr.and(SuggestContext.Visible, SuggestContext.MultipleSuggestions),
    handler: c => c.selectLastSuggestion()
}));
registerEditorCommand(new SuggestCommand({
    id: 'selectPrevSuggestion',
    precondition: ContextKeyExpr.and(SuggestContext.Visible, SuggestContext.MultipleSuggestions),
    handler: c => c.selectPrevSuggestion(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 16 /* UpArrow */,
        secondary: [2048 /* CtrlCmd */ | 16 /* UpArrow */],
        mac: { primary: 16 /* UpArrow */, secondary: [2048 /* CtrlCmd */ | 16 /* UpArrow */, 256 /* WinCtrl */ | 46 /* KEY_P */] }
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'selectPrevPageSuggestion',
    precondition: ContextKeyExpr.and(SuggestContext.Visible, SuggestContext.MultipleSuggestions),
    handler: c => c.selectPrevPageSuggestion(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 11 /* PageUp */,
        secondary: [2048 /* CtrlCmd */ | 11 /* PageUp */]
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'selectFirstSuggestion',
    precondition: ContextKeyExpr.and(SuggestContext.Visible, SuggestContext.MultipleSuggestions),
    handler: c => c.selectFirstSuggestion()
}));
registerEditorCommand(new SuggestCommand({
    id: 'toggleSuggestionDetails',
    precondition: SuggestContext.Visible,
    handler: x => x.toggleSuggestionDetails(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 2048 /* CtrlCmd */ | 10 /* Space */,
        mac: { primary: 256 /* WinCtrl */ | 10 /* Space */ }
    },
    menuOpts: [{
            menuId: suggestWidgetStatusbarMenu,
            group: 'right',
            order: 1,
            when: ContextKeyExpr.and(SuggestContext.DetailsVisible, SuggestContext.CanResolve),
            title: nls.localize('detail.more', "show less")
        }, {
            menuId: suggestWidgetStatusbarMenu,
            group: 'right',
            order: 1,
            when: ContextKeyExpr.and(SuggestContext.DetailsVisible.toNegated(), SuggestContext.CanResolve),
            title: nls.localize('detail.less', "show more")
        }]
}));
registerEditorCommand(new SuggestCommand({
    id: 'toggleExplainMode',
    precondition: SuggestContext.Visible,
    handler: x => x.toggleExplainMode(),
    kbOpts: {
        weight: 100 /* EditorContrib */,
        primary: 2048 /* CtrlCmd */ | 85 /* US_SLASH */,
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'toggleSuggestionFocus',
    precondition: SuggestContext.Visible,
    handler: x => x.toggleSuggestionFocus(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 2048 /* CtrlCmd */ | 512 /* Alt */ | 10 /* Space */,
        mac: { primary: 256 /* WinCtrl */ | 512 /* Alt */ | 10 /* Space */ }
    }
}));
//#region tab completions
registerEditorCommand(new SuggestCommand({
    id: 'insertBestCompletion',
    precondition: ContextKeyExpr.and(EditorContextKeys.textInputFocus, ContextKeyExpr.equals('config.editor.tabCompletion', 'on'), WordContextKey.AtEnd, SuggestContext.Visible.toNegated(), SuggestAlternatives.OtherSuggestions.toNegated(), SnippetController2.InSnippetMode.toNegated()),
    handler: (x, arg) => {
        x.triggerSuggestAndAcceptBest(isObject(arg) ? Object.assign({ fallback: 'tab' }, arg) : { fallback: 'tab' });
    },
    kbOpts: {
        weight,
        primary: 2 /* Tab */
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'insertNextSuggestion',
    precondition: ContextKeyExpr.and(EditorContextKeys.textInputFocus, ContextKeyExpr.equals('config.editor.tabCompletion', 'on'), SuggestAlternatives.OtherSuggestions, SuggestContext.Visible.toNegated(), SnippetController2.InSnippetMode.toNegated()),
    handler: x => x.acceptNextSuggestion(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 2 /* Tab */
    }
}));
registerEditorCommand(new SuggestCommand({
    id: 'insertPrevSuggestion',
    precondition: ContextKeyExpr.and(EditorContextKeys.textInputFocus, ContextKeyExpr.equals('config.editor.tabCompletion', 'on'), SuggestAlternatives.OtherSuggestions, SuggestContext.Visible.toNegated(), SnippetController2.InSnippetMode.toNegated()),
    handler: x => x.acceptPrevSuggestion(),
    kbOpts: {
        weight: weight,
        kbExpr: EditorContextKeys.textInputFocus,
        primary: 1024 /* Shift */ | 2 /* Tab */
    }
}));
registerEditorAction(class extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.resetSuggestSize',
            label: nls.localize('suggest.reset.label', "Reset Suggest Widget Size"),
            alias: 'Reset Suggest Widget Size',
            precondition: undefined
        });
    }
    run(_accessor, editor) {
        SuggestController.get(editor).resetWidgetSize();
    }
});
