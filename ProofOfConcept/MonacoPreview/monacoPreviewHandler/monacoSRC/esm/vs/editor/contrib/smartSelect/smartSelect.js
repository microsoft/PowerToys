/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import * as arrays from '../../../base/common/arrays.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { EditorAction, registerEditorAction, registerEditorContribution, registerModelCommand } from '../../browser/editorExtensions.js';
import { Position } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import * as modes from '../../common/modes.js';
import * as nls from '../../../nls.js';
import { MenuId } from '../../../platform/actions/common/actions.js';
import { WordSelectionRangeProvider } from './wordSelections.js';
import { BracketSelectionRangeProvider } from './bracketSelections.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
import { onUnexpectedExternalError } from '../../../base/common/errors.js';
class SelectionRanges {
    constructor(index, ranges) {
        this.index = index;
        this.ranges = ranges;
    }
    mov(fwd) {
        let index = this.index + (fwd ? 1 : -1);
        if (index < 0 || index >= this.ranges.length) {
            return this;
        }
        const res = new SelectionRanges(index, this.ranges);
        if (res.ranges[index].equalsRange(this.ranges[this.index])) {
            // next range equals this range, retry with next-next
            return res.mov(fwd);
        }
        return res;
    }
}
class SmartSelectController {
    constructor(_editor) {
        this._editor = _editor;
        this._ignoreSelection = false;
    }
    static get(editor) {
        return editor.getContribution(SmartSelectController.ID);
    }
    dispose() {
        var _a;
        (_a = this._selectionListener) === null || _a === void 0 ? void 0 : _a.dispose();
    }
    run(forward) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this._editor.hasModel()) {
                return;
            }
            const selections = this._editor.getSelections();
            const model = this._editor.getModel();
            if (!modes.SelectionRangeRegistry.has(model)) {
                return;
            }
            if (!this._state) {
                yield provideSelectionRanges(model, selections.map(s => s.getPosition()), this._editor.getOption(97 /* smartSelect */), CancellationToken.None).then(ranges => {
                    var _a;
                    if (!arrays.isNonEmptyArray(ranges) || ranges.length !== selections.length) {
                        // invalid result
                        return;
                    }
                    if (!this._editor.hasModel() || !arrays.equals(this._editor.getSelections(), selections, (a, b) => a.equalsSelection(b))) {
                        // invalid editor state
                        return;
                    }
                    for (let i = 0; i < ranges.length; i++) {
                        ranges[i] = ranges[i].filter(range => {
                            // filter ranges inside the selection
                            return range.containsPosition(selections[i].getStartPosition()) && range.containsPosition(selections[i].getEndPosition());
                        });
                        // prepend current selection
                        ranges[i].unshift(selections[i]);
                    }
                    this._state = ranges.map(ranges => new SelectionRanges(0, ranges));
                    // listen to caret move and forget about state
                    (_a = this._selectionListener) === null || _a === void 0 ? void 0 : _a.dispose();
                    this._selectionListener = this._editor.onDidChangeCursorPosition(() => {
                        var _a;
                        if (!this._ignoreSelection) {
                            (_a = this._selectionListener) === null || _a === void 0 ? void 0 : _a.dispose();
                            this._state = undefined;
                        }
                    });
                });
            }
            if (!this._state) {
                // no state
                return;
            }
            this._state = this._state.map(state => state.mov(forward));
            const newSelections = this._state.map(state => Selection.fromPositions(state.ranges[state.index].getStartPosition(), state.ranges[state.index].getEndPosition()));
            this._ignoreSelection = true;
            try {
                this._editor.setSelections(newSelections);
            }
            finally {
                this._ignoreSelection = false;
            }
        });
    }
}
SmartSelectController.ID = 'editor.contrib.smartSelectController';
class AbstractSmartSelect extends EditorAction {
    constructor(forward, opts) {
        super(opts);
        this._forward = forward;
    }
    run(_accessor, editor) {
        return __awaiter(this, void 0, void 0, function* () {
            let controller = SmartSelectController.get(editor);
            if (controller) {
                yield controller.run(this._forward);
            }
        });
    }
}
class GrowSelectionAction extends AbstractSmartSelect {
    constructor() {
        super(true, {
            id: 'editor.action.smartSelect.expand',
            label: nls.localize('smartSelect.expand', "Expand Selection"),
            alias: 'Expand Selection',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 1024 /* Shift */ | 512 /* Alt */ | 17 /* RightArrow */,
                mac: {
                    primary: 2048 /* CtrlCmd */ | 256 /* WinCtrl */ | 1024 /* Shift */ | 17 /* RightArrow */,
                    secondary: [256 /* WinCtrl */ | 1024 /* Shift */ | 17 /* RightArrow */],
                },
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '1_basic',
                title: nls.localize({ key: 'miSmartSelectGrow', comment: ['&& denotes a mnemonic'] }, "&&Expand Selection"),
                order: 2
            }
        });
    }
}
// renamed command id
CommandsRegistry.registerCommandAlias('editor.action.smartSelect.grow', 'editor.action.smartSelect.expand');
class ShrinkSelectionAction extends AbstractSmartSelect {
    constructor() {
        super(false, {
            id: 'editor.action.smartSelect.shrink',
            label: nls.localize('smartSelect.shrink', "Shrink Selection"),
            alias: 'Shrink Selection',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 1024 /* Shift */ | 512 /* Alt */ | 15 /* LeftArrow */,
                mac: {
                    primary: 2048 /* CtrlCmd */ | 256 /* WinCtrl */ | 1024 /* Shift */ | 15 /* LeftArrow */,
                    secondary: [256 /* WinCtrl */ | 1024 /* Shift */ | 15 /* LeftArrow */],
                },
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarSelectionMenu,
                group: '1_basic',
                title: nls.localize({ key: 'miSmartSelectShrink', comment: ['&& denotes a mnemonic'] }, "&&Shrink Selection"),
                order: 3
            }
        });
    }
}
registerEditorContribution(SmartSelectController.ID, SmartSelectController);
registerEditorAction(GrowSelectionAction);
registerEditorAction(ShrinkSelectionAction);
// word selection
modes.SelectionRangeRegistry.register('*', new WordSelectionRangeProvider());
export function provideSelectionRanges(model, positions, options, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const providers = modes.SelectionRangeRegistry.all(model);
        if (providers.length === 1) {
            // add word selection and bracket selection when no provider exists
            providers.unshift(new BracketSelectionRangeProvider());
        }
        let work = [];
        let allRawRanges = [];
        for (const provider of providers) {
            work.push(Promise.resolve(provider.provideSelectionRanges(model, positions, token)).then(allProviderRanges => {
                if (arrays.isNonEmptyArray(allProviderRanges) && allProviderRanges.length === positions.length) {
                    for (let i = 0; i < positions.length; i++) {
                        if (!allRawRanges[i]) {
                            allRawRanges[i] = [];
                        }
                        for (const oneProviderRanges of allProviderRanges[i]) {
                            if (Range.isIRange(oneProviderRanges.range) && Range.containsPosition(oneProviderRanges.range, positions[i])) {
                                allRawRanges[i].push(Range.lift(oneProviderRanges.range));
                            }
                        }
                    }
                }
            }, onUnexpectedExternalError));
        }
        yield Promise.all(work);
        return allRawRanges.map(oneRawRanges => {
            if (oneRawRanges.length === 0) {
                return [];
            }
            // sort all by start/end position
            oneRawRanges.sort((a, b) => {
                if (Position.isBefore(a.getStartPosition(), b.getStartPosition())) {
                    return 1;
                }
                else if (Position.isBefore(b.getStartPosition(), a.getStartPosition())) {
                    return -1;
                }
                else if (Position.isBefore(a.getEndPosition(), b.getEndPosition())) {
                    return -1;
                }
                else if (Position.isBefore(b.getEndPosition(), a.getEndPosition())) {
                    return 1;
                }
                else {
                    return 0;
                }
            });
            // remove ranges that don't contain the former range or that are equal to the
            // former range
            let oneRanges = [];
            let last;
            for (const range of oneRawRanges) {
                if (!last || (Range.containsRange(range, last) && !Range.equalsRange(range, last))) {
                    oneRanges.push(range);
                    last = range;
                }
            }
            if (!options.selectLeadingAndTrailingWhitespace) {
                return oneRanges;
            }
            // add ranges that expand trivia at line starts and ends whenever a range
            // wraps onto the a new line
            let oneRangesWithTrivia = [oneRanges[0]];
            for (let i = 1; i < oneRanges.length; i++) {
                const prev = oneRanges[i - 1];
                const cur = oneRanges[i];
                if (cur.startLineNumber !== prev.startLineNumber || cur.endLineNumber !== prev.endLineNumber) {
                    // add line/block range without leading/failing whitespace
                    const rangeNoWhitespace = new Range(prev.startLineNumber, model.getLineFirstNonWhitespaceColumn(prev.startLineNumber), prev.endLineNumber, model.getLineLastNonWhitespaceColumn(prev.endLineNumber));
                    if (rangeNoWhitespace.containsRange(prev) && !rangeNoWhitespace.equalsRange(prev) && cur.containsRange(rangeNoWhitespace) && !cur.equalsRange(rangeNoWhitespace)) {
                        oneRangesWithTrivia.push(rangeNoWhitespace);
                    }
                    // add line/block range
                    const rangeFull = new Range(prev.startLineNumber, 1, prev.endLineNumber, model.getLineMaxColumn(prev.endLineNumber));
                    if (rangeFull.containsRange(prev) && !rangeFull.equalsRange(rangeNoWhitespace) && cur.containsRange(rangeFull) && !cur.equalsRange(rangeFull)) {
                        oneRangesWithTrivia.push(rangeFull);
                    }
                }
                oneRangesWithTrivia.push(cur);
            }
            return oneRangesWithTrivia;
        });
    });
}
registerModelCommand('_executeSelectionRangeProvider', function (model, ...args) {
    const [positions] = args;
    return provideSelectionRanges(model, positions, { selectLeadingAndTrailingWhitespace: true }, CancellationToken.None);
});
