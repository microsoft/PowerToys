/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../../nls.js';
import { RunOnceScheduler } from '../../../base/common/async.js';
import { KeyChord } from '../../../base/common/keyCodes.js';
import { Disposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { EditorAction, registerEditorAction, registerEditorContribution } from '../../browser/editorExtensions.js';
import { CursorMoveCommands } from '../../common/controller/cursorMoveCommands.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { OverviewRulerLane } from '../../common/model.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
import { DocumentHighlightProviderRegistry } from '../../common/modes.js';
import { CommonFindController } from '../find/findController.js';
import { MenuId } from '../../../platform/actions/common/actions.js';
import { overviewRulerSelectionHighlightForeground } from '../../../platform/theme/common/colorRegistry.js';
import { themeColorFromId } from '../../../platform/theme/common/themeService.js';
import { ContextKeyExpr } from '../../../platform/contextkey/common/contextkey.js';
export class InsertCursorAbove extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.insertCursorAbove',
            label: nls.localize('mutlicursor.insertAbove', "Add Cursor Above"),
            alias: 'Add Cursor Above',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 512 /* Alt */ | 16 /* UpArrow */,
                linux: {
                    primary: 1024 /* Shift */ | 512 /* Alt */ | 16 /* UpArrow */,
                    secondary: [2048 /* CtrlCmd */ | 1024 /* Shift */ | 16 /* UpArrow */]
                },
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '3_multi',
                title: nls.localize({ key: 'miInsertCursorAbove', comment: ['&& denotes a mnemonic'] }, "&&Add Cursor Above"),
                order: 2
            }
        });
    }
    run(accessor, editor, args) {
        if (!editor.hasModel()) {
            return;
        }
        const useLogicalLine = (args && args.logicalLine === true);
        const viewModel = editor._getViewModel();
        if (viewModel.cursorConfig.readOnly) {
            return;
        }
        viewModel.pushStackElement();
        viewModel.setCursorStates(args.source, 3 /* Explicit */, CursorMoveCommands.addCursorUp(viewModel, viewModel.getCursorStates(), useLogicalLine));
        viewModel.revealTopMostCursor(args.source);
    }
}
export class InsertCursorBelow extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.insertCursorBelow',
            label: nls.localize('mutlicursor.insertBelow', "Add Cursor Below"),
            alias: 'Add Cursor Below',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 512 /* Alt */ | 18 /* DownArrow */,
                linux: {
                    primary: 1024 /* Shift */ | 512 /* Alt */ | 18 /* DownArrow */,
                    secondary: [2048 /* CtrlCmd */ | 1024 /* Shift */ | 18 /* DownArrow */]
                },
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '3_multi',
                title: nls.localize({ key: 'miInsertCursorBelow', comment: ['&& denotes a mnemonic'] }, "A&&dd Cursor Below"),
                order: 3
            }
        });
    }
    run(accessor, editor, args) {
        if (!editor.hasModel()) {
            return;
        }
        const useLogicalLine = (args && args.logicalLine === true);
        const viewModel = editor._getViewModel();
        if (viewModel.cursorConfig.readOnly) {
            return;
        }
        viewModel.pushStackElement();
        viewModel.setCursorStates(args.source, 3 /* Explicit */, CursorMoveCommands.addCursorDown(viewModel, viewModel.getCursorStates(), useLogicalLine));
        viewModel.revealBottomMostCursor(args.source);
    }
}
class InsertCursorAtEndOfEachLineSelected extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.insertCursorAtEndOfEachLineSelected',
            label: nls.localize('mutlicursor.insertAtEndOfEachLineSelected', "Add Cursors to Line Ends"),
            alias: 'Add Cursors to Line Ends',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 1024 /* Shift */ | 512 /* Alt */ | 39 /* KEY_I */,
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '3_multi',
                title: nls.localize({ key: 'miInsertCursorAtEndOfEachLineSelected', comment: ['&& denotes a mnemonic'] }, "Add C&&ursors to Line Ends"),
                order: 4
            }
        });
    }
    getCursorsForSelection(selection, model, result) {
        if (selection.isEmpty()) {
            return;
        }
        for (let i = selection.startLineNumber; i < selection.endLineNumber; i++) {
            let currentLineMaxColumn = model.getLineMaxColumn(i);
            result.push(new Selection(i, currentLineMaxColumn, i, currentLineMaxColumn));
        }
        if (selection.endColumn > 1) {
            result.push(new Selection(selection.endLineNumber, selection.endColumn, selection.endLineNumber, selection.endColumn));
        }
    }
    run(accessor, editor) {
        if (!editor.hasModel()) {
            return;
        }
        const model = editor.getModel();
        const selections = editor.getSelections();
        let newSelections = [];
        selections.forEach((sel) => this.getCursorsForSelection(sel, model, newSelections));
        if (newSelections.length > 0) {
            editor.setSelections(newSelections);
        }
    }
}
class InsertCursorAtEndOfLineSelected extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.addCursorsToBottom',
            label: nls.localize('mutlicursor.addCursorsToBottom', "Add Cursors To Bottom"),
            alias: 'Add Cursors To Bottom',
            precondition: undefined
        });
    }
    run(accessor, editor) {
        if (!editor.hasModel()) {
            return;
        }
        const selections = editor.getSelections();
        const lineCount = editor.getModel().getLineCount();
        let newSelections = [];
        for (let i = selections[0].startLineNumber; i <= lineCount; i++) {
            newSelections.push(new Selection(i, selections[0].startColumn, i, selections[0].endColumn));
        }
        if (newSelections.length > 0) {
            editor.setSelections(newSelections);
        }
    }
}
class InsertCursorAtTopOfLineSelected extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.addCursorsToTop',
            label: nls.localize('mutlicursor.addCursorsToTop', "Add Cursors To Top"),
            alias: 'Add Cursors To Top',
            precondition: undefined
        });
    }
    run(accessor, editor) {
        if (!editor.hasModel()) {
            return;
        }
        const selections = editor.getSelections();
        let newSelections = [];
        for (let i = selections[0].startLineNumber; i >= 1; i--) {
            newSelections.push(new Selection(i, selections[0].startColumn, i, selections[0].endColumn));
        }
        if (newSelections.length > 0) {
            editor.setSelections(newSelections);
        }
    }
}
export class MultiCursorSessionResult {
    constructor(selections, revealRange, revealScrollType) {
        this.selections = selections;
        this.revealRange = revealRange;
        this.revealScrollType = revealScrollType;
    }
}
export class MultiCursorSession {
    constructor(_editor, findController, isDisconnectedFromFindController, searchText, wholeWord, matchCase, currentMatch) {
        this._editor = _editor;
        this.findController = findController;
        this.isDisconnectedFromFindController = isDisconnectedFromFindController;
        this.searchText = searchText;
        this.wholeWord = wholeWord;
        this.matchCase = matchCase;
        this.currentMatch = currentMatch;
    }
    static create(editor, findController) {
        if (!editor.hasModel()) {
            return null;
        }
        const findState = findController.getState();
        // Find widget owns entirely what we search for if:
        //  - focus is not in the editor (i.e. it is in the find widget)
        //  - and the search widget is visible
        //  - and the search string is non-empty
        if (!editor.hasTextFocus() && findState.isRevealed && findState.searchString.length > 0) {
            // Find widget owns what is searched for
            return new MultiCursorSession(editor, findController, false, findState.searchString, findState.wholeWord, findState.matchCase, null);
        }
        // Otherwise, the selection gives the search text, and the find widget gives the search settings
        // The exception is the find state disassociation case: when beginning with a single, collapsed selection
        let isDisconnectedFromFindController = false;
        let wholeWord;
        let matchCase;
        const selections = editor.getSelections();
        if (selections.length === 1 && selections[0].isEmpty()) {
            isDisconnectedFromFindController = true;
            wholeWord = true;
            matchCase = true;
        }
        else {
            wholeWord = findState.wholeWord;
            matchCase = findState.matchCase;
        }
        // Selection owns what is searched for
        const s = editor.getSelection();
        let searchText;
        let currentMatch = null;
        if (s.isEmpty()) {
            // selection is empty => expand to current word
            const word = editor.getConfiguredWordAtPosition(s.getStartPosition());
            if (!word) {
                return null;
            }
            searchText = word.word;
            currentMatch = new Selection(s.startLineNumber, word.startColumn, s.startLineNumber, word.endColumn);
        }
        else {
            searchText = editor.getModel().getValueInRange(s).replace(/\r\n/g, '\n');
        }
        return new MultiCursorSession(editor, findController, isDisconnectedFromFindController, searchText, wholeWord, matchCase, currentMatch);
    }
    addSelectionToNextFindMatch() {
        if (!this._editor.hasModel()) {
            return null;
        }
        const nextMatch = this._getNextMatch();
        if (!nextMatch) {
            return null;
        }
        const allSelections = this._editor.getSelections();
        return new MultiCursorSessionResult(allSelections.concat(nextMatch), nextMatch, 0 /* Smooth */);
    }
    moveSelectionToNextFindMatch() {
        if (!this._editor.hasModel()) {
            return null;
        }
        const nextMatch = this._getNextMatch();
        if (!nextMatch) {
            return null;
        }
        const allSelections = this._editor.getSelections();
        return new MultiCursorSessionResult(allSelections.slice(0, allSelections.length - 1).concat(nextMatch), nextMatch, 0 /* Smooth */);
    }
    _getNextMatch() {
        if (!this._editor.hasModel()) {
            return null;
        }
        if (this.currentMatch) {
            const result = this.currentMatch;
            this.currentMatch = null;
            return result;
        }
        this.findController.highlightFindOptions();
        const allSelections = this._editor.getSelections();
        const lastAddedSelection = allSelections[allSelections.length - 1];
        const nextMatch = this._editor.getModel().findNextMatch(this.searchText, lastAddedSelection.getEndPosition(), false, this.matchCase, this.wholeWord ? this._editor.getOption(110 /* wordSeparators */) : null, false);
        if (!nextMatch) {
            return null;
        }
        return new Selection(nextMatch.range.startLineNumber, nextMatch.range.startColumn, nextMatch.range.endLineNumber, nextMatch.range.endColumn);
    }
    addSelectionToPreviousFindMatch() {
        if (!this._editor.hasModel()) {
            return null;
        }
        const previousMatch = this._getPreviousMatch();
        if (!previousMatch) {
            return null;
        }
        const allSelections = this._editor.getSelections();
        return new MultiCursorSessionResult(allSelections.concat(previousMatch), previousMatch, 0 /* Smooth */);
    }
    moveSelectionToPreviousFindMatch() {
        if (!this._editor.hasModel()) {
            return null;
        }
        const previousMatch = this._getPreviousMatch();
        if (!previousMatch) {
            return null;
        }
        const allSelections = this._editor.getSelections();
        return new MultiCursorSessionResult(allSelections.slice(0, allSelections.length - 1).concat(previousMatch), previousMatch, 0 /* Smooth */);
    }
    _getPreviousMatch() {
        if (!this._editor.hasModel()) {
            return null;
        }
        if (this.currentMatch) {
            const result = this.currentMatch;
            this.currentMatch = null;
            return result;
        }
        this.findController.highlightFindOptions();
        const allSelections = this._editor.getSelections();
        const lastAddedSelection = allSelections[allSelections.length - 1];
        const previousMatch = this._editor.getModel().findPreviousMatch(this.searchText, lastAddedSelection.getStartPosition(), false, this.matchCase, this.wholeWord ? this._editor.getOption(110 /* wordSeparators */) : null, false);
        if (!previousMatch) {
            return null;
        }
        return new Selection(previousMatch.range.startLineNumber, previousMatch.range.startColumn, previousMatch.range.endLineNumber, previousMatch.range.endColumn);
    }
    selectAll() {
        if (!this._editor.hasModel()) {
            return [];
        }
        this.findController.highlightFindOptions();
        return this._editor.getModel().findMatches(this.searchText, true, false, this.matchCase, this.wholeWord ? this._editor.getOption(110 /* wordSeparators */) : null, false, 1073741824 /* MAX_SAFE_SMALL_INTEGER */);
    }
}
export class MultiCursorSelectionController extends Disposable {
    constructor(editor) {
        super();
        this._sessionDispose = this._register(new DisposableStore());
        this._editor = editor;
        this._ignoreSelectionChange = false;
        this._session = null;
    }
    static get(editor) {
        return editor.getContribution(MultiCursorSelectionController.ID);
    }
    dispose() {
        this._endSession();
        super.dispose();
    }
    _beginSessionIfNeeded(findController) {
        if (!this._session) {
            // Create a new session
            const session = MultiCursorSession.create(this._editor, findController);
            if (!session) {
                return;
            }
            this._session = session;
            const newState = { searchString: this._session.searchText };
            if (this._session.isDisconnectedFromFindController) {
                newState.wholeWordOverride = 1 /* True */;
                newState.matchCaseOverride = 1 /* True */;
                newState.isRegexOverride = 2 /* False */;
            }
            findController.getState().change(newState, false);
            this._sessionDispose.add(this._editor.onDidChangeCursorSelection((e) => {
                if (this._ignoreSelectionChange) {
                    return;
                }
                this._endSession();
            }));
            this._sessionDispose.add(this._editor.onDidBlurEditorText(() => {
                this._endSession();
            }));
            this._sessionDispose.add(findController.getState().onFindReplaceStateChange((e) => {
                if (e.matchCase || e.wholeWord) {
                    this._endSession();
                }
            }));
        }
    }
    _endSession() {
        this._sessionDispose.clear();
        if (this._session && this._session.isDisconnectedFromFindController) {
            const newState = {
                wholeWordOverride: 0 /* NotSet */,
                matchCaseOverride: 0 /* NotSet */,
                isRegexOverride: 0 /* NotSet */,
            };
            this._session.findController.getState().change(newState, false);
        }
        this._session = null;
    }
    _setSelections(selections) {
        this._ignoreSelectionChange = true;
        this._editor.setSelections(selections);
        this._ignoreSelectionChange = false;
    }
    _expandEmptyToWord(model, selection) {
        if (!selection.isEmpty()) {
            return selection;
        }
        const word = this._editor.getConfiguredWordAtPosition(selection.getStartPosition());
        if (!word) {
            return selection;
        }
        return new Selection(selection.startLineNumber, word.startColumn, selection.startLineNumber, word.endColumn);
    }
    _applySessionResult(result) {
        if (!result) {
            return;
        }
        this._setSelections(result.selections);
        if (result.revealRange) {
            this._editor.revealRangeInCenterIfOutsideViewport(result.revealRange, result.revealScrollType);
        }
    }
    getSession(findController) {
        return this._session;
    }
    addSelectionToNextFindMatch(findController) {
        if (!this._editor.hasModel()) {
            return;
        }
        if (!this._session) {
            // If there are multiple cursors, handle the case where they do not all select the same text.
            const allSelections = this._editor.getSelections();
            if (allSelections.length > 1) {
                const findState = findController.getState();
                const matchCase = findState.matchCase;
                const selectionsContainSameText = modelRangesContainSameText(this._editor.getModel(), allSelections, matchCase);
                if (!selectionsContainSameText) {
                    const model = this._editor.getModel();
                    let resultingSelections = [];
                    for (let i = 0, len = allSelections.length; i < len; i++) {
                        resultingSelections[i] = this._expandEmptyToWord(model, allSelections[i]);
                    }
                    this._editor.setSelections(resultingSelections);
                    return;
                }
            }
        }
        this._beginSessionIfNeeded(findController);
        if (this._session) {
            this._applySessionResult(this._session.addSelectionToNextFindMatch());
        }
    }
    addSelectionToPreviousFindMatch(findController) {
        this._beginSessionIfNeeded(findController);
        if (this._session) {
            this._applySessionResult(this._session.addSelectionToPreviousFindMatch());
        }
    }
    moveSelectionToNextFindMatch(findController) {
        this._beginSessionIfNeeded(findController);
        if (this._session) {
            this._applySessionResult(this._session.moveSelectionToNextFindMatch());
        }
    }
    moveSelectionToPreviousFindMatch(findController) {
        this._beginSessionIfNeeded(findController);
        if (this._session) {
            this._applySessionResult(this._session.moveSelectionToPreviousFindMatch());
        }
    }
    selectAll(findController) {
        if (!this._editor.hasModel()) {
            return;
        }
        let matches = null;
        const findState = findController.getState();
        // Special case: find widget owns entirely what we search for if:
        // - focus is not in the editor (i.e. it is in the find widget)
        // - and the search widget is visible
        // - and the search string is non-empty
        // - and we're searching for a regex
        if (findState.isRevealed && findState.searchString.length > 0 && findState.isRegex) {
            matches = this._editor.getModel().findMatches(findState.searchString, true, findState.isRegex, findState.matchCase, findState.wholeWord ? this._editor.getOption(110 /* wordSeparators */) : null, false, 1073741824 /* MAX_SAFE_SMALL_INTEGER */);
        }
        else {
            this._beginSessionIfNeeded(findController);
            if (!this._session) {
                return;
            }
            matches = this._session.selectAll();
        }
        if (findState.searchScope) {
            const states = findState.searchScope;
            let inSelection = [];
            matches.forEach((match) => {
                states.forEach((state) => {
                    if (match.range.endLineNumber <= state.endLineNumber && match.range.startLineNumber >= state.startLineNumber) {
                        inSelection.push(match);
                    }
                });
            });
            matches = inSelection;
        }
        if (matches.length > 0) {
            const editorSelection = this._editor.getSelection();
            // Have the primary cursor remain the one where the action was invoked
            for (let i = 0, len = matches.length; i < len; i++) {
                const match = matches[i];
                const intersection = match.range.intersectRanges(editorSelection);
                if (intersection) {
                    // bingo!
                    matches[i] = matches[0];
                    matches[0] = match;
                    break;
                }
            }
            this._setSelections(matches.map(m => new Selection(m.range.startLineNumber, m.range.startColumn, m.range.endLineNumber, m.range.endColumn)));
        }
    }
}
MultiCursorSelectionController.ID = 'editor.contrib.multiCursorController';
export class MultiCursorSelectionControllerAction extends EditorAction {
    run(accessor, editor) {
        const multiCursorController = MultiCursorSelectionController.get(editor);
        if (!multiCursorController) {
            return;
        }
        const findController = CommonFindController.get(editor);
        if (!findController) {
            return;
        }
        this._run(multiCursorController, findController);
    }
}
export class AddSelectionToNextFindMatchAction extends MultiCursorSelectionControllerAction {
    constructor() {
        super({
            id: 'editor.action.addSelectionToNextFindMatch',
            label: nls.localize('addSelectionToNextFindMatch', "Add Selection To Next Find Match"),
            alias: 'Add Selection To Next Find Match',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.focus,
                primary: 2048 /* CtrlCmd */ | 34 /* KEY_D */,
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '3_multi',
                title: nls.localize({ key: 'miAddSelectionToNextFindMatch', comment: ['&& denotes a mnemonic'] }, "Add &&Next Occurrence"),
                order: 5
            }
        });
    }
    _run(multiCursorController, findController) {
        multiCursorController.addSelectionToNextFindMatch(findController);
    }
}
export class AddSelectionToPreviousFindMatchAction extends MultiCursorSelectionControllerAction {
    constructor() {
        super({
            id: 'editor.action.addSelectionToPreviousFindMatch',
            label: nls.localize('addSelectionToPreviousFindMatch', "Add Selection To Previous Find Match"),
            alias: 'Add Selection To Previous Find Match',
            precondition: undefined,
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '3_multi',
                title: nls.localize({ key: 'miAddSelectionToPreviousFindMatch', comment: ['&& denotes a mnemonic'] }, "Add P&&revious Occurrence"),
                order: 6
            }
        });
    }
    _run(multiCursorController, findController) {
        multiCursorController.addSelectionToPreviousFindMatch(findController);
    }
}
export class MoveSelectionToNextFindMatchAction extends MultiCursorSelectionControllerAction {
    constructor() {
        super({
            id: 'editor.action.moveSelectionToNextFindMatch',
            label: nls.localize('moveSelectionToNextFindMatch', "Move Last Selection To Next Find Match"),
            alias: 'Move Last Selection To Next Find Match',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.focus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 34 /* KEY_D */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    _run(multiCursorController, findController) {
        multiCursorController.moveSelectionToNextFindMatch(findController);
    }
}
export class MoveSelectionToPreviousFindMatchAction extends MultiCursorSelectionControllerAction {
    constructor() {
        super({
            id: 'editor.action.moveSelectionToPreviousFindMatch',
            label: nls.localize('moveSelectionToPreviousFindMatch', "Move Last Selection To Previous Find Match"),
            alias: 'Move Last Selection To Previous Find Match',
            precondition: undefined
        });
    }
    _run(multiCursorController, findController) {
        multiCursorController.moveSelectionToPreviousFindMatch(findController);
    }
}
export class SelectHighlightsAction extends MultiCursorSelectionControllerAction {
    constructor() {
        super({
            id: 'editor.action.selectHighlights',
            label: nls.localize('selectAllOccurrencesOfFindMatch', "Select All Occurrences of Find Match"),
            alias: 'Select All Occurrences of Find Match',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.focus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 42 /* KEY_L */,
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '3_multi',
                title: nls.localize({ key: 'miSelectHighlights', comment: ['&& denotes a mnemonic'] }, "Select All &&Occurrences"),
                order: 7
            }
        });
    }
    _run(multiCursorController, findController) {
        multiCursorController.selectAll(findController);
    }
}
export class CompatChangeAll extends MultiCursorSelectionControllerAction {
    constructor() {
        super({
            id: 'editor.action.changeAll',
            label: nls.localize('changeAll.label', "Change All Occurrences"),
            alias: 'Change All Occurrences',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.editorTextFocus),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 60 /* F2 */,
                weight: 100 /* EditorContrib */
            },
            contextMenuOpts: {
                group: '1_modification',
                order: 1.2
            }
        });
    }
    _run(multiCursorController, findController) {
        multiCursorController.selectAll(findController);
    }
}
class SelectionHighlighterState {
    constructor(searchText, matchCase, wordSeparators, modelVersionId) {
        this.searchText = searchText;
        this.matchCase = matchCase;
        this.wordSeparators = wordSeparators;
        this.modelVersionId = modelVersionId;
    }
    /**
     * Everything equals except for `lastWordUnderCursor`
     */
    static softEquals(a, b) {
        if (!a && !b) {
            return true;
        }
        if (!a || !b) {
            return false;
        }
        return (a.searchText === b.searchText
            && a.matchCase === b.matchCase
            && a.wordSeparators === b.wordSeparators
            && a.modelVersionId === b.modelVersionId);
    }
}
export class SelectionHighlighter extends Disposable {
    constructor(editor) {
        super();
        this.editor = editor;
        this._isEnabled = editor.getOption(92 /* selectionHighlight */);
        this.decorations = [];
        this.updateSoon = this._register(new RunOnceScheduler(() => this._update(), 300));
        this.state = null;
        this._register(editor.onDidChangeConfiguration((e) => {
            this._isEnabled = editor.getOption(92 /* selectionHighlight */);
        }));
        this._register(editor.onDidChangeCursorSelection((e) => {
            if (!this._isEnabled) {
                // Early exit if nothing needs to be done!
                // Leave some form of early exit check here if you wish to continue being a cursor position change listener ;)
                return;
            }
            if (e.selection.isEmpty()) {
                if (e.reason === 3 /* Explicit */) {
                    if (this.state) {
                        // no longer valid
                        this._setState(null);
                    }
                    this.updateSoon.schedule();
                }
                else {
                    this._setState(null);
                }
            }
            else {
                this._update();
            }
        }));
        this._register(editor.onDidChangeModel((e) => {
            this._setState(null);
        }));
        this._register(editor.onDidChangeModelContent((e) => {
            if (this._isEnabled) {
                this.updateSoon.schedule();
            }
        }));
        this._register(CommonFindController.get(editor).getState().onFindReplaceStateChange((e) => {
            this._update();
        }));
    }
    _update() {
        this._setState(SelectionHighlighter._createState(this._isEnabled, this.editor));
    }
    static _createState(isEnabled, editor) {
        if (!isEnabled) {
            return null;
        }
        if (!editor.hasModel()) {
            return null;
        }
        const s = editor.getSelection();
        if (s.startLineNumber !== s.endLineNumber) {
            // multiline forbidden for perf reasons
            return null;
        }
        const multiCursorController = MultiCursorSelectionController.get(editor);
        if (!multiCursorController) {
            return null;
        }
        const findController = CommonFindController.get(editor);
        if (!findController) {
            return null;
        }
        let r = multiCursorController.getSession(findController);
        if (!r) {
            const allSelections = editor.getSelections();
            if (allSelections.length > 1) {
                const findState = findController.getState();
                const matchCase = findState.matchCase;
                const selectionsContainSameText = modelRangesContainSameText(editor.getModel(), allSelections, matchCase);
                if (!selectionsContainSameText) {
                    return null;
                }
            }
            r = MultiCursorSession.create(editor, findController);
        }
        if (!r) {
            return null;
        }
        if (r.currentMatch) {
            // This is an empty selection
            // Do not interfere with semantic word highlighting in the no selection case
            return null;
        }
        if (/^[ \t]+$/.test(r.searchText)) {
            // whitespace only selection
            return null;
        }
        if (r.searchText.length > 200) {
            // very long selection
            return null;
        }
        // TODO: better handling of this case
        const findState = findController.getState();
        const caseSensitive = findState.matchCase;
        // Return early if the find widget shows the exact same matches
        if (findState.isRevealed) {
            let findStateSearchString = findState.searchString;
            if (!caseSensitive) {
                findStateSearchString = findStateSearchString.toLowerCase();
            }
            let mySearchString = r.searchText;
            if (!caseSensitive) {
                mySearchString = mySearchString.toLowerCase();
            }
            if (findStateSearchString === mySearchString && r.matchCase === findState.matchCase && r.wholeWord === findState.wholeWord && !findState.isRegex) {
                return null;
            }
        }
        return new SelectionHighlighterState(r.searchText, r.matchCase, r.wholeWord ? editor.getOption(110 /* wordSeparators */) : null, editor.getModel().getVersionId());
    }
    _setState(state) {
        if (SelectionHighlighterState.softEquals(this.state, state)) {
            this.state = state;
            return;
        }
        this.state = state;
        if (!this.state) {
            this.decorations = this.editor.deltaDecorations(this.decorations, []);
            return;
        }
        if (!this.editor.hasModel()) {
            return;
        }
        const model = this.editor.getModel();
        if (model.isTooLargeForTokenization()) {
            // the file is too large, so searching word under cursor in the whole document takes is blocking the UI.
            return;
        }
        const hasFindOccurrences = DocumentHighlightProviderRegistry.has(model) && this.editor.getOption(66 /* occurrencesHighlight */);
        let allMatches = model.findMatches(this.state.searchText, true, false, this.state.matchCase, this.state.wordSeparators, false).map(m => m.range);
        allMatches.sort(Range.compareRangesUsingStarts);
        let selections = this.editor.getSelections();
        selections.sort(Range.compareRangesUsingStarts);
        // do not overlap with selection (issue #64 and #512)
        let matches = [];
        for (let i = 0, j = 0, len = allMatches.length, lenJ = selections.length; i < len;) {
            const match = allMatches[i];
            if (j >= lenJ) {
                // finished all editor selections
                matches.push(match);
                i++;
            }
            else {
                const cmp = Range.compareRangesUsingStarts(match, selections[j]);
                if (cmp < 0) {
                    // match is before sel
                    if (selections[j].isEmpty() || !Range.areIntersecting(match, selections[j])) {
                        matches.push(match);
                    }
                    i++;
                }
                else if (cmp > 0) {
                    // sel is before match
                    j++;
                }
                else {
                    // sel is equal to match
                    i++;
                    j++;
                }
            }
        }
        const decorations = matches.map(r => {
            return {
                range: r,
                // Show in overviewRuler only if model has no semantic highlighting
                options: (hasFindOccurrences ? SelectionHighlighter._SELECTION_HIGHLIGHT : SelectionHighlighter._SELECTION_HIGHLIGHT_OVERVIEW)
            };
        });
        this.decorations = this.editor.deltaDecorations(this.decorations, decorations);
    }
    dispose() {
        this._setState(null);
        super.dispose();
    }
}
SelectionHighlighter.ID = 'editor.contrib.selectionHighlighter';
SelectionHighlighter._SELECTION_HIGHLIGHT_OVERVIEW = ModelDecorationOptions.register({
    stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
    className: 'selectionHighlight',
    overviewRuler: {
        color: themeColorFromId(overviewRulerSelectionHighlightForeground),
        position: OverviewRulerLane.Center
    }
});
SelectionHighlighter._SELECTION_HIGHLIGHT = ModelDecorationOptions.register({
    stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
    className: 'selectionHighlight',
});
function modelRangesContainSameText(model, ranges, matchCase) {
    const selectedText = getValueInRange(model, ranges[0], !matchCase);
    for (let i = 1, len = ranges.length; i < len; i++) {
        const range = ranges[i];
        if (range.isEmpty()) {
            return false;
        }
        const thisSelectedText = getValueInRange(model, range, !matchCase);
        if (selectedText !== thisSelectedText) {
            return false;
        }
    }
    return true;
}
function getValueInRange(model, range, toLowerCase) {
    const text = model.getValueInRange(range);
    return (toLowerCase ? text.toLowerCase() : text);
}
registerEditorContribution(MultiCursorSelectionController.ID, MultiCursorSelectionController);
registerEditorContribution(SelectionHighlighter.ID, SelectionHighlighter);
registerEditorAction(InsertCursorAbove);
registerEditorAction(InsertCursorBelow);
registerEditorAction(InsertCursorAtEndOfEachLineSelected);
registerEditorAction(AddSelectionToNextFindMatchAction);
registerEditorAction(AddSelectionToPreviousFindMatchAction);
registerEditorAction(MoveSelectionToNextFindMatchAction);
registerEditorAction(MoveSelectionToPreviousFindMatchAction);
registerEditorAction(SelectHighlightsAction);
registerEditorAction(CompatChangeAll);
registerEditorAction(InsertCursorAtEndOfLineSelected);
registerEditorAction(InsertCursorAtTopOfLineSelected);
