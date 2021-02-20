/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as strings from '../../../base/common/strings.js';
import { ReplaceCommand } from '../commands/replaceCommand.js';
import { CursorColumns, EditOperationResult, isQuote } from './cursorCommon.js';
import { MoveOperations } from './cursorMoveOperations.js';
import { Range } from '../core/range.js';
export class DeleteOperations {
    static deleteRight(prevEditOperationType, config, model, selections) {
        let commands = [];
        let shouldPushStackElementBefore = (prevEditOperationType !== 3 /* DeletingRight */);
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            let deleteSelection = selection;
            if (deleteSelection.isEmpty()) {
                let position = selection.getPosition();
                let rightOfPosition = MoveOperations.right(config, model, position.lineNumber, position.column);
                deleteSelection = new Range(rightOfPosition.lineNumber, rightOfPosition.column, position.lineNumber, position.column);
            }
            if (deleteSelection.isEmpty()) {
                // Probably at end of file => ignore
                commands[i] = null;
                continue;
            }
            if (deleteSelection.startLineNumber !== deleteSelection.endLineNumber) {
                shouldPushStackElementBefore = true;
            }
            commands[i] = new ReplaceCommand(deleteSelection, '');
        }
        return [shouldPushStackElementBefore, commands];
    }
    static isAutoClosingPairDelete(autoClosingBrackets, autoClosingQuotes, autoClosingPairsOpen, model, selections) {
        if (autoClosingBrackets === 'never' && autoClosingQuotes === 'never') {
            return false;
        }
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            const position = selection.getPosition();
            if (!selection.isEmpty()) {
                return false;
            }
            const lineText = model.getLineContent(position.lineNumber);
            if (position.column < 2 || position.column >= lineText.length + 1) {
                return false;
            }
            const character = lineText.charAt(position.column - 2);
            const autoClosingPairCandidates = autoClosingPairsOpen.get(character);
            if (!autoClosingPairCandidates) {
                return false;
            }
            if (isQuote(character)) {
                if (autoClosingQuotes === 'never') {
                    return false;
                }
            }
            else {
                if (autoClosingBrackets === 'never') {
                    return false;
                }
            }
            const afterCharacter = lineText.charAt(position.column - 1);
            let foundAutoClosingPair = false;
            for (const autoClosingPairCandidate of autoClosingPairCandidates) {
                if (autoClosingPairCandidate.open === character && autoClosingPairCandidate.close === afterCharacter) {
                    foundAutoClosingPair = true;
                }
            }
            if (!foundAutoClosingPair) {
                return false;
            }
        }
        return true;
    }
    static _runAutoClosingPairDelete(config, model, selections) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const position = selections[i].getPosition();
            const deleteSelection = new Range(position.lineNumber, position.column - 1, position.lineNumber, position.column + 1);
            commands[i] = new ReplaceCommand(deleteSelection, '');
        }
        return [true, commands];
    }
    static deleteLeft(prevEditOperationType, config, model, selections) {
        if (this.isAutoClosingPairDelete(config.autoClosingBrackets, config.autoClosingQuotes, config.autoClosingPairs.autoClosingPairsOpenByEnd, model, selections)) {
            return this._runAutoClosingPairDelete(config, model, selections);
        }
        let commands = [];
        let shouldPushStackElementBefore = (prevEditOperationType !== 2 /* DeletingLeft */);
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            let deleteSelection = selection;
            if (deleteSelection.isEmpty()) {
                let position = selection.getPosition();
                if (config.useTabStops && position.column > 1) {
                    let lineContent = model.getLineContent(position.lineNumber);
                    let firstNonWhitespaceIndex = strings.firstNonWhitespaceIndex(lineContent);
                    let lastIndentationColumn = (firstNonWhitespaceIndex === -1
                        ? /* entire string is whitespace */ lineContent.length + 1
                        : firstNonWhitespaceIndex + 1);
                    if (position.column <= lastIndentationColumn) {
                        let fromVisibleColumn = CursorColumns.visibleColumnFromColumn2(config, model, position);
                        let toVisibleColumn = CursorColumns.prevIndentTabStop(fromVisibleColumn, config.indentSize);
                        let toColumn = CursorColumns.columnFromVisibleColumn2(config, model, position.lineNumber, toVisibleColumn);
                        deleteSelection = new Range(position.lineNumber, toColumn, position.lineNumber, position.column);
                    }
                    else {
                        deleteSelection = new Range(position.lineNumber, position.column - 1, position.lineNumber, position.column);
                    }
                }
                else {
                    let leftOfPosition = MoveOperations.left(config, model, position.lineNumber, position.column);
                    deleteSelection = new Range(leftOfPosition.lineNumber, leftOfPosition.column, position.lineNumber, position.column);
                }
            }
            if (deleteSelection.isEmpty()) {
                // Probably at beginning of file => ignore
                commands[i] = null;
                continue;
            }
            if (deleteSelection.startLineNumber !== deleteSelection.endLineNumber) {
                shouldPushStackElementBefore = true;
            }
            commands[i] = new ReplaceCommand(deleteSelection, '');
        }
        return [shouldPushStackElementBefore, commands];
    }
    static cut(config, model, selections) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            if (selection.isEmpty()) {
                if (config.emptySelectionClipboard) {
                    // This is a full line cut
                    let position = selection.getPosition();
                    let startLineNumber, startColumn, endLineNumber, endColumn;
                    if (position.lineNumber < model.getLineCount()) {
                        // Cutting a line in the middle of the model
                        startLineNumber = position.lineNumber;
                        startColumn = 1;
                        endLineNumber = position.lineNumber + 1;
                        endColumn = 1;
                    }
                    else if (position.lineNumber > 1) {
                        // Cutting the last line & there are more than 1 lines in the model
                        startLineNumber = position.lineNumber - 1;
                        startColumn = model.getLineMaxColumn(position.lineNumber - 1);
                        endLineNumber = position.lineNumber;
                        endColumn = model.getLineMaxColumn(position.lineNumber);
                    }
                    else {
                        // Cutting the single line that the model contains
                        startLineNumber = position.lineNumber;
                        startColumn = 1;
                        endLineNumber = position.lineNumber;
                        endColumn = model.getLineMaxColumn(position.lineNumber);
                    }
                    let deleteSelection = new Range(startLineNumber, startColumn, endLineNumber, endColumn);
                    if (!deleteSelection.isEmpty()) {
                        commands[i] = new ReplaceCommand(deleteSelection, '');
                    }
                    else {
                        commands[i] = null;
                    }
                }
                else {
                    // Cannot cut empty selection
                    commands[i] = null;
                }
            }
            else {
                commands[i] = new ReplaceCommand(selection, '');
            }
        }
        return new EditOperationResult(0 /* Other */, commands, {
            shouldPushStackElementBefore: true,
            shouldPushStackElementAfter: true
        });
    }
}
