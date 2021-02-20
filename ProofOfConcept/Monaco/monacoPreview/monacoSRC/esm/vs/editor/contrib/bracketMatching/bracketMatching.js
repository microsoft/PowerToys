/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './bracketMatching.css';
import * as nls from '../../../nls.js';
import { RunOnceScheduler } from '../../../base/common/async.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { EditorAction, registerEditorAction, registerEditorContribution } from '../../browser/editorExtensions.js';
import { Position } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { OverviewRulerLane } from '../../common/model.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
import { editorBracketMatchBackground, editorBracketMatchBorder } from '../../common/view/editorColorRegistry.js';
import { registerColor } from '../../../platform/theme/common/colorRegistry.js';
import { registerThemingParticipant, themeColorFromId } from '../../../platform/theme/common/themeService.js';
import { MenuRegistry, MenuId } from '../../../platform/actions/common/actions.js';
const overviewRulerBracketMatchForeground = registerColor('editorOverviewRuler.bracketMatchForeground', { dark: '#A0A0A0', light: '#A0A0A0', hc: '#A0A0A0' }, nls.localize('overviewRulerBracketMatchForeground', 'Overview ruler marker color for matching brackets.'));
class JumpToBracketAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.jumpToBracket',
            label: nls.localize('smartSelect.jumpBracket', "Go to Bracket"),
            alias: 'Go to Bracket',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 88 /* US_BACKSLASH */,
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(accessor, editor) {
        let controller = BracketMatchingController.get(editor);
        if (!controller) {
            return;
        }
        controller.jumpToBracket();
    }
}
class SelectToBracketAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.selectToBracket',
            label: nls.localize('smartSelect.selectToBracket', "Select to Bracket"),
            alias: 'Select to Bracket',
            precondition: undefined,
            description: {
                description: `Select to Bracket`,
                args: [{
                        name: 'args',
                        schema: {
                            type: 'object',
                            properties: {
                                'selectBrackets': {
                                    type: 'boolean',
                                    default: true
                                }
                            },
                        }
                    }]
            }
        });
    }
    run(accessor, editor, args) {
        const controller = BracketMatchingController.get(editor);
        if (!controller) {
            return;
        }
        let selectBrackets = true;
        if (args && args.selectBrackets === false) {
            selectBrackets = false;
        }
        controller.selectToBracket(selectBrackets);
    }
}
class BracketsData {
    constructor(position, brackets, options) {
        this.position = position;
        this.brackets = brackets;
        this.options = options;
    }
}
export class BracketMatchingController extends Disposable {
    constructor(editor) {
        super();
        this._editor = editor;
        this._lastBracketsData = [];
        this._lastVersionId = 0;
        this._decorations = [];
        this._updateBracketsSoon = this._register(new RunOnceScheduler(() => this._updateBrackets(), 50));
        this._matchBrackets = this._editor.getOption(58 /* matchBrackets */);
        this._updateBracketsSoon.schedule();
        this._register(editor.onDidChangeCursorPosition((e) => {
            if (this._matchBrackets === 'never') {
                // Early exit if nothing needs to be done!
                // Leave some form of early exit check here if you wish to continue being a cursor position change listener ;)
                return;
            }
            this._updateBracketsSoon.schedule();
        }));
        this._register(editor.onDidChangeModelContent((e) => {
            this._updateBracketsSoon.schedule();
        }));
        this._register(editor.onDidChangeModel((e) => {
            this._lastBracketsData = [];
            this._decorations = [];
            this._updateBracketsSoon.schedule();
        }));
        this._register(editor.onDidChangeModelLanguageConfiguration((e) => {
            this._lastBracketsData = [];
            this._updateBracketsSoon.schedule();
        }));
        this._register(editor.onDidChangeConfiguration((e) => {
            if (e.hasChanged(58 /* matchBrackets */)) {
                this._matchBrackets = this._editor.getOption(58 /* matchBrackets */);
                this._decorations = this._editor.deltaDecorations(this._decorations, []);
                this._lastBracketsData = [];
                this._lastVersionId = 0;
                this._updateBracketsSoon.schedule();
            }
        }));
    }
    static get(editor) {
        return editor.getContribution(BracketMatchingController.ID);
    }
    jumpToBracket() {
        if (!this._editor.hasModel()) {
            return;
        }
        const model = this._editor.getModel();
        const newSelections = this._editor.getSelections().map(selection => {
            const position = selection.getStartPosition();
            // find matching brackets if position is on a bracket
            const brackets = model.matchBracket(position);
            let newCursorPosition = null;
            if (brackets) {
                if (brackets[0].containsPosition(position)) {
                    newCursorPosition = brackets[1].getStartPosition();
                }
                else if (brackets[1].containsPosition(position)) {
                    newCursorPosition = brackets[0].getStartPosition();
                }
            }
            else {
                // find the enclosing brackets if the position isn't on a matching bracket
                const enclosingBrackets = model.findEnclosingBrackets(position);
                if (enclosingBrackets) {
                    newCursorPosition = enclosingBrackets[0].getStartPosition();
                }
                else {
                    // no enclosing brackets, try the very first next bracket
                    const nextBracket = model.findNextBracket(position);
                    if (nextBracket && nextBracket.range) {
                        newCursorPosition = nextBracket.range.getStartPosition();
                    }
                }
            }
            if (newCursorPosition) {
                return new Selection(newCursorPosition.lineNumber, newCursorPosition.column, newCursorPosition.lineNumber, newCursorPosition.column);
            }
            return new Selection(position.lineNumber, position.column, position.lineNumber, position.column);
        });
        this._editor.setSelections(newSelections);
        this._editor.revealRange(newSelections[0]);
    }
    selectToBracket(selectBrackets) {
        if (!this._editor.hasModel()) {
            return;
        }
        const model = this._editor.getModel();
        const newSelections = [];
        this._editor.getSelections().forEach(selection => {
            const position = selection.getStartPosition();
            let brackets = model.matchBracket(position);
            if (!brackets) {
                brackets = model.findEnclosingBrackets(position);
                if (!brackets) {
                    const nextBracket = model.findNextBracket(position);
                    if (nextBracket && nextBracket.range) {
                        brackets = model.matchBracket(nextBracket.range.getStartPosition());
                    }
                }
            }
            let selectFrom = null;
            let selectTo = null;
            if (brackets) {
                brackets.sort(Range.compareRangesUsingStarts);
                const [open, close] = brackets;
                selectFrom = selectBrackets ? open.getStartPosition() : open.getEndPosition();
                selectTo = selectBrackets ? close.getEndPosition() : close.getStartPosition();
            }
            if (selectFrom && selectTo) {
                newSelections.push(new Selection(selectFrom.lineNumber, selectFrom.column, selectTo.lineNumber, selectTo.column));
            }
        });
        if (newSelections.length > 0) {
            this._editor.setSelections(newSelections);
            this._editor.revealRange(newSelections[0]);
        }
    }
    _updateBrackets() {
        if (this._matchBrackets === 'never') {
            return;
        }
        this._recomputeBrackets();
        let newDecorations = [], newDecorationsLen = 0;
        for (const bracketData of this._lastBracketsData) {
            let brackets = bracketData.brackets;
            if (brackets) {
                newDecorations[newDecorationsLen++] = { range: brackets[0], options: bracketData.options };
                newDecorations[newDecorationsLen++] = { range: brackets[1], options: bracketData.options };
            }
        }
        this._decorations = this._editor.deltaDecorations(this._decorations, newDecorations);
    }
    _recomputeBrackets() {
        if (!this._editor.hasModel()) {
            // no model => no brackets!
            this._lastBracketsData = [];
            this._lastVersionId = 0;
            return;
        }
        const selections = this._editor.getSelections();
        if (selections.length > 100) {
            // no bracket matching for high numbers of selections
            this._lastBracketsData = [];
            this._lastVersionId = 0;
            return;
        }
        const model = this._editor.getModel();
        const versionId = model.getVersionId();
        let previousData = [];
        if (this._lastVersionId === versionId) {
            // use the previous data only if the model is at the same version id
            previousData = this._lastBracketsData;
        }
        let positions = [], positionsLen = 0;
        for (let i = 0, len = selections.length; i < len; i++) {
            let selection = selections[i];
            if (selection.isEmpty()) {
                // will bracket match a cursor only if the selection is collapsed
                positions[positionsLen++] = selection.getStartPosition();
            }
        }
        // sort positions for `previousData` cache hits
        if (positions.length > 1) {
            positions.sort(Position.compare);
        }
        let newData = [], newDataLen = 0;
        let previousIndex = 0, previousLen = previousData.length;
        for (let i = 0, len = positions.length; i < len; i++) {
            let position = positions[i];
            while (previousIndex < previousLen && previousData[previousIndex].position.isBefore(position)) {
                previousIndex++;
            }
            if (previousIndex < previousLen && previousData[previousIndex].position.equals(position)) {
                newData[newDataLen++] = previousData[previousIndex];
            }
            else {
                let brackets = model.matchBracket(position);
                let options = BracketMatchingController._DECORATION_OPTIONS_WITH_OVERVIEW_RULER;
                if (!brackets && this._matchBrackets === 'always') {
                    brackets = model.findEnclosingBrackets(position, 20 /* give at most 20ms to compute */);
                    options = BracketMatchingController._DECORATION_OPTIONS_WITHOUT_OVERVIEW_RULER;
                }
                newData[newDataLen++] = new BracketsData(position, brackets, options);
            }
        }
        this._lastBracketsData = newData;
        this._lastVersionId = versionId;
    }
}
BracketMatchingController.ID = 'editor.contrib.bracketMatchingController';
BracketMatchingController._DECORATION_OPTIONS_WITH_OVERVIEW_RULER = ModelDecorationOptions.register({
    stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
    className: 'bracket-match',
    overviewRuler: {
        color: themeColorFromId(overviewRulerBracketMatchForeground),
        position: OverviewRulerLane.Center
    }
});
BracketMatchingController._DECORATION_OPTIONS_WITHOUT_OVERVIEW_RULER = ModelDecorationOptions.register({
    stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
    className: 'bracket-match'
});
registerEditorContribution(BracketMatchingController.ID, BracketMatchingController);
registerEditorAction(SelectToBracketAction);
registerEditorAction(JumpToBracketAction);
registerThemingParticipant((theme, collector) => {
    const bracketMatchBackground = theme.getColor(editorBracketMatchBackground);
    if (bracketMatchBackground) {
        collector.addRule(`.monaco-editor .bracket-match { background-color: ${bracketMatchBackground}; }`);
    }
    const bracketMatchBorder = theme.getColor(editorBracketMatchBorder);
    if (bracketMatchBorder) {
        collector.addRule(`.monaco-editor .bracket-match { border: 1px solid ${bracketMatchBorder}; }`);
    }
});
// Go to menu
MenuRegistry.appendMenuItem(MenuId.MenubarGoMenu, {
    group: '5_infile_nav',
    command: {
        id: 'editor.action.jumpToBracket',
        title: nls.localize({ key: 'miGoToBracket', comment: ['&& denotes a mnemonic'] }, "Go to &&Bracket")
    },
    order: 2
});
