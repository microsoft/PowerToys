/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../../nls.js';
import { EditorAction, registerEditorAction } from '../../browser/editorExtensions.js';
import { ReplaceCommand } from '../../common/commands/replaceCommand.js';
import { Range } from '../../common/core/range.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { MoveOperations } from '../../common/controller/cursorMoveOperations.js';
class TransposeLettersAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.transposeLetters',
            label: nls.localize('transposeLetters.label', "Transpose Letters"),
            alias: 'Transpose Letters',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 0,
                mac: {
                    primary: 256 /* WinCtrl */ | 50 /* KEY_T */
                },
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(accessor, editor) {
        if (!editor.hasModel()) {
            return;
        }
        let model = editor.getModel();
        let commands = [];
        let selections = editor.getSelections();
        for (let selection of selections) {
            if (!selection.isEmpty()) {
                continue;
            }
            let lineNumber = selection.startLineNumber;
            let column = selection.startColumn;
            let lastColumn = model.getLineMaxColumn(lineNumber);
            if (lineNumber === 1 && (column === 1 || (column === 2 && lastColumn === 2))) {
                // at beginning of file, nothing to do
                continue;
            }
            // handle special case: when at end of line, transpose left two chars
            // otherwise, transpose left and right chars
            let endPosition = (column === lastColumn) ?
                selection.getPosition() :
                MoveOperations.rightPosition(model, selection.getPosition().lineNumber, selection.getPosition().column);
            let middlePosition = MoveOperations.leftPosition(model, endPosition.lineNumber, endPosition.column);
            let beginPosition = MoveOperations.leftPosition(model, middlePosition.lineNumber, middlePosition.column);
            let leftChar = model.getValueInRange(Range.fromPositions(beginPosition, middlePosition));
            let rightChar = model.getValueInRange(Range.fromPositions(middlePosition, endPosition));
            let replaceRange = Range.fromPositions(beginPosition, endPosition);
            commands.push(new ReplaceCommand(replaceRange, rightChar + leftChar));
        }
        if (commands.length > 0) {
            editor.pushUndoStop();
            editor.executeCommands(this.id, commands);
            editor.pushUndoStop();
        }
    }
}
registerEditorAction(TransposeLettersAction);
