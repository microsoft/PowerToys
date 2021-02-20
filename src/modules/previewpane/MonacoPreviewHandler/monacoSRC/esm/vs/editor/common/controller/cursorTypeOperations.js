/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { onUnexpectedError } from '../../../base/common/errors.js';
import * as strings from '../../../base/common/strings.js';
import { ReplaceCommand, ReplaceCommandWithOffsetCursorState, ReplaceCommandWithoutChangingPosition, ReplaceCommandThatPreservesSelection } from '../commands/replaceCommand.js';
import { ShiftCommand } from '../commands/shiftCommand.js';
import { SurroundSelectionCommand } from '../commands/surroundSelectionCommand.js';
import { CursorColumns, EditOperationResult, isQuote } from './cursorCommon.js';
import { getMapForWordSeparators } from './wordCharacterClassifier.js';
import { Range } from '../core/range.js';
import { Selection } from '../core/selection.js';
import { IndentAction } from '../modes/languageConfiguration.js';
import { LanguageConfigurationRegistry } from '../modes/languageConfigurationRegistry.js';
export class TypeOperations {
    static indent(config, model, selections) {
        if (model === null || selections === null) {
            return [];
        }
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            commands[i] = new ShiftCommand(selections[i], {
                isUnshift: false,
                tabSize: config.tabSize,
                indentSize: config.indentSize,
                insertSpaces: config.insertSpaces,
                useTabStops: config.useTabStops,
                autoIndent: config.autoIndent
            });
        }
        return commands;
    }
    static outdent(config, model, selections) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            commands[i] = new ShiftCommand(selections[i], {
                isUnshift: true,
                tabSize: config.tabSize,
                indentSize: config.indentSize,
                insertSpaces: config.insertSpaces,
                useTabStops: config.useTabStops,
                autoIndent: config.autoIndent
            });
        }
        return commands;
    }
    static shiftIndent(config, indentation, count) {
        count = count || 1;
        return ShiftCommand.shiftIndent(indentation, indentation.length + count, config.tabSize, config.indentSize, config.insertSpaces);
    }
    static unshiftIndent(config, indentation, count) {
        count = count || 1;
        return ShiftCommand.unshiftIndent(indentation, indentation.length + count, config.tabSize, config.indentSize, config.insertSpaces);
    }
    static _distributedPaste(config, model, selections, text) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            commands[i] = new ReplaceCommand(selections[i], text[i]);
        }
        return new EditOperationResult(0 /* Other */, commands, {
            shouldPushStackElementBefore: true,
            shouldPushStackElementAfter: true
        });
    }
    static _simplePaste(config, model, selections, text, pasteOnNewLine) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            let position = selection.getPosition();
            if (pasteOnNewLine && !selection.isEmpty()) {
                pasteOnNewLine = false;
            }
            if (pasteOnNewLine && text.indexOf('\n') !== text.length - 1) {
                pasteOnNewLine = false;
            }
            if (pasteOnNewLine) {
                // Paste entire line at the beginning of line
                let typeSelection = new Range(position.lineNumber, 1, position.lineNumber, 1);
                commands[i] = new ReplaceCommandThatPreservesSelection(typeSelection, text, selection, true);
            }
            else {
                commands[i] = new ReplaceCommand(selection, text);
            }
        }
        return new EditOperationResult(0 /* Other */, commands, {
            shouldPushStackElementBefore: true,
            shouldPushStackElementAfter: true
        });
    }
    static _distributePasteToCursors(config, selections, text, pasteOnNewLine, multicursorText) {
        if (pasteOnNewLine) {
            return null;
        }
        if (selections.length === 1) {
            return null;
        }
        if (multicursorText && multicursorText.length === selections.length) {
            return multicursorText;
        }
        if (config.multiCursorPaste === 'spread') {
            // Try to spread the pasted text in case the line count matches the cursor count
            // Remove trailing \n if present
            if (text.charCodeAt(text.length - 1) === 10 /* LineFeed */) {
                text = text.substr(0, text.length - 1);
            }
            // Remove trailing \r if present
            if (text.charCodeAt(text.length - 1) === 13 /* CarriageReturn */) {
                text = text.substr(0, text.length - 1);
            }
            let lines = strings.splitLines(text);
            if (lines.length === selections.length) {
                return lines;
            }
        }
        return null;
    }
    static paste(config, model, selections, text, pasteOnNewLine, multicursorText) {
        const distributedPaste = this._distributePasteToCursors(config, selections, text, pasteOnNewLine, multicursorText);
        if (distributedPaste) {
            selections = selections.sort(Range.compareRangesUsingStarts);
            return this._distributedPaste(config, model, selections, distributedPaste);
        }
        else {
            return this._simplePaste(config, model, selections, text, pasteOnNewLine);
        }
    }
    static _goodIndentForLine(config, model, lineNumber) {
        let action = null;
        let indentation = '';
        const expectedIndentAction = LanguageConfigurationRegistry.getInheritIndentForLine(config.autoIndent, model, lineNumber, false);
        if (expectedIndentAction) {
            action = expectedIndentAction.action;
            indentation = expectedIndentAction.indentation;
        }
        else if (lineNumber > 1) {
            let lastLineNumber;
            for (lastLineNumber = lineNumber - 1; lastLineNumber >= 1; lastLineNumber--) {
                const lineText = model.getLineContent(lastLineNumber);
                const nonWhitespaceIdx = strings.lastNonWhitespaceIndex(lineText);
                if (nonWhitespaceIdx >= 0) {
                    break;
                }
            }
            if (lastLineNumber < 1) {
                // No previous line with content found
                return null;
            }
            const maxColumn = model.getLineMaxColumn(lastLineNumber);
            const expectedEnterAction = LanguageConfigurationRegistry.getEnterAction(config.autoIndent, model, new Range(lastLineNumber, maxColumn, lastLineNumber, maxColumn));
            if (expectedEnterAction) {
                indentation = expectedEnterAction.indentation + expectedEnterAction.appendText;
            }
        }
        if (action) {
            if (action === IndentAction.Indent) {
                indentation = TypeOperations.shiftIndent(config, indentation);
            }
            if (action === IndentAction.Outdent) {
                indentation = TypeOperations.unshiftIndent(config, indentation);
            }
            indentation = config.normalizeIndentation(indentation);
        }
        if (!indentation) {
            return null;
        }
        return indentation;
    }
    static _replaceJumpToNextIndent(config, model, selection, insertsAutoWhitespace) {
        let typeText = '';
        let position = selection.getStartPosition();
        if (config.insertSpaces) {
            let visibleColumnFromColumn = CursorColumns.visibleColumnFromColumn2(config, model, position);
            let indentSize = config.indentSize;
            let spacesCnt = indentSize - (visibleColumnFromColumn % indentSize);
            for (let i = 0; i < spacesCnt; i++) {
                typeText += ' ';
            }
        }
        else {
            typeText = '\t';
        }
        return new ReplaceCommand(selection, typeText, insertsAutoWhitespace);
    }
    static tab(config, model, selections) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            if (selection.isEmpty()) {
                let lineText = model.getLineContent(selection.startLineNumber);
                if (/^\s*$/.test(lineText) && model.isCheapToTokenize(selection.startLineNumber)) {
                    let goodIndent = this._goodIndentForLine(config, model, selection.startLineNumber);
                    goodIndent = goodIndent || '\t';
                    let possibleTypeText = config.normalizeIndentation(goodIndent);
                    if (!lineText.startsWith(possibleTypeText)) {
                        commands[i] = new ReplaceCommand(new Range(selection.startLineNumber, 1, selection.startLineNumber, lineText.length + 1), possibleTypeText, true);
                        continue;
                    }
                }
                commands[i] = this._replaceJumpToNextIndent(config, model, selection, true);
            }
            else {
                if (selection.startLineNumber === selection.endLineNumber) {
                    let lineMaxColumn = model.getLineMaxColumn(selection.startLineNumber);
                    if (selection.startColumn !== 1 || selection.endColumn !== lineMaxColumn) {
                        // This is a single line selection that is not the entire line
                        commands[i] = this._replaceJumpToNextIndent(config, model, selection, false);
                        continue;
                    }
                }
                commands[i] = new ShiftCommand(selection, {
                    isUnshift: false,
                    tabSize: config.tabSize,
                    indentSize: config.indentSize,
                    insertSpaces: config.insertSpaces,
                    useTabStops: config.useTabStops,
                    autoIndent: config.autoIndent
                });
            }
        }
        return commands;
    }
    static replacePreviousChar(prevEditOperationType, config, model, selections, txt, replaceCharCnt) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            if (!selection.isEmpty()) {
                // looks like https://github.com/microsoft/vscode/issues/2773
                // where a cursor operation occurred before a canceled composition
                // => ignore composition
                commands[i] = null;
                continue;
            }
            const pos = selection.getPosition();
            const startColumn = Math.max(1, pos.column - replaceCharCnt);
            const range = new Range(pos.lineNumber, startColumn, pos.lineNumber, pos.column);
            const oldText = model.getValueInRange(range);
            if (oldText === txt) {
                // => ignore composition that doesn't do anything
                commands[i] = null;
                continue;
            }
            commands[i] = new ReplaceCommand(range, txt);
        }
        return new EditOperationResult(1 /* Typing */, commands, {
            shouldPushStackElementBefore: (prevEditOperationType !== 1 /* Typing */),
            shouldPushStackElementAfter: false
        });
    }
    static _typeCommand(range, text, keepPosition) {
        if (keepPosition) {
            return new ReplaceCommandWithoutChangingPosition(range, text, true);
        }
        else {
            return new ReplaceCommand(range, text, true);
        }
    }
    static _enter(config, model, keepPosition, range) {
        if (config.autoIndent === 0 /* None */) {
            return TypeOperations._typeCommand(range, '\n', keepPosition);
        }
        if (!model.isCheapToTokenize(range.getStartPosition().lineNumber) || config.autoIndent === 1 /* Keep */) {
            let lineText = model.getLineContent(range.startLineNumber);
            let indentation = strings.getLeadingWhitespace(lineText).substring(0, range.startColumn - 1);
            return TypeOperations._typeCommand(range, '\n' + config.normalizeIndentation(indentation), keepPosition);
        }
        const r = LanguageConfigurationRegistry.getEnterAction(config.autoIndent, model, range);
        if (r) {
            if (r.indentAction === IndentAction.None) {
                // Nothing special
                return TypeOperations._typeCommand(range, '\n' + config.normalizeIndentation(r.indentation + r.appendText), keepPosition);
            }
            else if (r.indentAction === IndentAction.Indent) {
                // Indent once
                return TypeOperations._typeCommand(range, '\n' + config.normalizeIndentation(r.indentation + r.appendText), keepPosition);
            }
            else if (r.indentAction === IndentAction.IndentOutdent) {
                // Ultra special
                const normalIndent = config.normalizeIndentation(r.indentation);
                const increasedIndent = config.normalizeIndentation(r.indentation + r.appendText);
                const typeText = '\n' + increasedIndent + '\n' + normalIndent;
                if (keepPosition) {
                    return new ReplaceCommandWithoutChangingPosition(range, typeText, true);
                }
                else {
                    return new ReplaceCommandWithOffsetCursorState(range, typeText, -1, increasedIndent.length - normalIndent.length, true);
                }
            }
            else if (r.indentAction === IndentAction.Outdent) {
                const actualIndentation = TypeOperations.unshiftIndent(config, r.indentation);
                return TypeOperations._typeCommand(range, '\n' + config.normalizeIndentation(actualIndentation + r.appendText), keepPosition);
            }
        }
        const lineText = model.getLineContent(range.startLineNumber);
        const indentation = strings.getLeadingWhitespace(lineText).substring(0, range.startColumn - 1);
        if (config.autoIndent >= 4 /* Full */) {
            const ir = LanguageConfigurationRegistry.getIndentForEnter(config.autoIndent, model, range, {
                unshiftIndent: (indent) => {
                    return TypeOperations.unshiftIndent(config, indent);
                },
                shiftIndent: (indent) => {
                    return TypeOperations.shiftIndent(config, indent);
                },
                normalizeIndentation: (indent) => {
                    return config.normalizeIndentation(indent);
                }
            });
            if (ir) {
                let oldEndViewColumn = CursorColumns.visibleColumnFromColumn2(config, model, range.getEndPosition());
                const oldEndColumn = range.endColumn;
                const newLineContent = model.getLineContent(range.endLineNumber);
                const firstNonWhitespace = strings.firstNonWhitespaceIndex(newLineContent);
                if (firstNonWhitespace >= 0) {
                    range = range.setEndPosition(range.endLineNumber, Math.max(range.endColumn, firstNonWhitespace + 1));
                }
                else {
                    range = range.setEndPosition(range.endLineNumber, model.getLineMaxColumn(range.endLineNumber));
                }
                if (keepPosition) {
                    return new ReplaceCommandWithoutChangingPosition(range, '\n' + config.normalizeIndentation(ir.afterEnter), true);
                }
                else {
                    let offset = 0;
                    if (oldEndColumn <= firstNonWhitespace + 1) {
                        if (!config.insertSpaces) {
                            oldEndViewColumn = Math.ceil(oldEndViewColumn / config.indentSize);
                        }
                        offset = Math.min(oldEndViewColumn + 1 - config.normalizeIndentation(ir.afterEnter).length - 1, 0);
                    }
                    return new ReplaceCommandWithOffsetCursorState(range, '\n' + config.normalizeIndentation(ir.afterEnter), 0, offset, true);
                }
            }
        }
        return TypeOperations._typeCommand(range, '\n' + config.normalizeIndentation(indentation), keepPosition);
    }
    static _isAutoIndentType(config, model, selections) {
        if (config.autoIndent < 4 /* Full */) {
            return false;
        }
        for (let i = 0, len = selections.length; i < len; i++) {
            if (!model.isCheapToTokenize(selections[i].getEndPosition().lineNumber)) {
                return false;
            }
        }
        return true;
    }
    static _runAutoIndentType(config, model, range, ch) {
        const currentIndentation = LanguageConfigurationRegistry.getIndentationAtPosition(model, range.startLineNumber, range.startColumn);
        const actualIndentation = LanguageConfigurationRegistry.getIndentActionForType(config.autoIndent, model, range, ch, {
            shiftIndent: (indentation) => {
                return TypeOperations.shiftIndent(config, indentation);
            },
            unshiftIndent: (indentation) => {
                return TypeOperations.unshiftIndent(config, indentation);
            },
        });
        if (actualIndentation === null) {
            return null;
        }
        if (actualIndentation !== config.normalizeIndentation(currentIndentation)) {
            const firstNonWhitespace = model.getLineFirstNonWhitespaceColumn(range.startLineNumber);
            if (firstNonWhitespace === 0) {
                return TypeOperations._typeCommand(new Range(range.startLineNumber, 1, range.endLineNumber, range.endColumn), config.normalizeIndentation(actualIndentation) + ch, false);
            }
            else {
                return TypeOperations._typeCommand(new Range(range.startLineNumber, 1, range.endLineNumber, range.endColumn), config.normalizeIndentation(actualIndentation) +
                    model.getLineContent(range.startLineNumber).substring(firstNonWhitespace - 1, range.startColumn - 1) + ch, false);
            }
        }
        return null;
    }
    static _isAutoClosingOvertype(config, model, selections, autoClosedCharacters, ch) {
        if (config.autoClosingOvertype === 'never') {
            return false;
        }
        if (!config.autoClosingPairs.autoClosingPairsCloseSingleChar.has(ch)) {
            return false;
        }
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            if (!selection.isEmpty()) {
                return false;
            }
            const position = selection.getPosition();
            const lineText = model.getLineContent(position.lineNumber);
            const afterCharacter = lineText.charAt(position.column - 1);
            if (afterCharacter !== ch) {
                return false;
            }
            // Do not over-type quotes after a backslash
            const chIsQuote = isQuote(ch);
            const beforeCharacter = position.column > 2 ? lineText.charCodeAt(position.column - 2) : 0 /* Null */;
            if (beforeCharacter === 92 /* Backslash */ && chIsQuote) {
                return false;
            }
            // Must over-type a closing character typed by the editor
            if (config.autoClosingOvertype === 'auto') {
                let found = false;
                for (let j = 0, lenJ = autoClosedCharacters.length; j < lenJ; j++) {
                    const autoClosedCharacter = autoClosedCharacters[j];
                    if (position.lineNumber === autoClosedCharacter.startLineNumber && position.column === autoClosedCharacter.startColumn) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    return false;
                }
            }
        }
        return true;
    }
    static _runAutoClosingOvertype(prevEditOperationType, config, model, selections, ch) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            const position = selection.getPosition();
            const typeSelection = new Range(position.lineNumber, position.column, position.lineNumber, position.column + 1);
            commands[i] = new ReplaceCommand(typeSelection, ch);
        }
        return new EditOperationResult(1 /* Typing */, commands, {
            shouldPushStackElementBefore: (prevEditOperationType !== 1 /* Typing */),
            shouldPushStackElementAfter: false
        });
    }
    static _isBeforeClosingBrace(config, lineAfter) {
        // If the start of lineAfter can be interpretted as both a starting or ending brace, default to returning false
        const nextChar = lineAfter.charAt(0);
        const potentialStartingBraces = config.autoClosingPairs.autoClosingPairsOpenByStart.get(nextChar) || [];
        const potentialClosingBraces = config.autoClosingPairs.autoClosingPairsCloseByStart.get(nextChar) || [];
        const isBeforeStartingBrace = potentialStartingBraces.some(x => lineAfter.startsWith(x.open));
        const isBeforeClosingBrace = potentialClosingBraces.some(x => lineAfter.startsWith(x.close));
        return !isBeforeStartingBrace && isBeforeClosingBrace;
    }
    static _findAutoClosingPairOpen(config, model, positions, ch) {
        const autoClosingPairCandidates = config.autoClosingPairs.autoClosingPairsOpenByEnd.get(ch);
        if (!autoClosingPairCandidates) {
            return null;
        }
        // Determine which auto-closing pair it is
        let autoClosingPair = null;
        for (const autoClosingPairCandidate of autoClosingPairCandidates) {
            if (autoClosingPair === null || autoClosingPairCandidate.open.length > autoClosingPair.open.length) {
                let candidateIsMatch = true;
                for (const position of positions) {
                    const relevantText = model.getValueInRange(new Range(position.lineNumber, position.column - autoClosingPairCandidate.open.length + 1, position.lineNumber, position.column));
                    if (relevantText + ch !== autoClosingPairCandidate.open) {
                        candidateIsMatch = false;
                        break;
                    }
                }
                if (candidateIsMatch) {
                    autoClosingPair = autoClosingPairCandidate;
                }
            }
        }
        return autoClosingPair;
    }
    static _findSubAutoClosingPairClose(config, autoClosingPair) {
        if (autoClosingPair.open.length <= 1) {
            return '';
        }
        const lastChar = autoClosingPair.close.charAt(autoClosingPair.close.length - 1);
        // get candidates with the same last character as close
        const subPairCandidates = config.autoClosingPairs.autoClosingPairsCloseByEnd.get(lastChar) || [];
        let subPairMatch = null;
        for (const x of subPairCandidates) {
            if (x.open !== autoClosingPair.open && autoClosingPair.open.includes(x.open) && autoClosingPair.close.endsWith(x.close)) {
                if (!subPairMatch || x.open.length > subPairMatch.open.length) {
                    subPairMatch = x;
                }
            }
        }
        if (subPairMatch) {
            return subPairMatch.close;
        }
        else {
            return '';
        }
    }
    static _getAutoClosingPairClose(config, model, selections, ch, insertOpenCharacter) {
        const chIsQuote = isQuote(ch);
        const autoCloseConfig = chIsQuote ? config.autoClosingQuotes : config.autoClosingBrackets;
        if (autoCloseConfig === 'never') {
            return null;
        }
        const autoClosingPair = this._findAutoClosingPairOpen(config, model, selections.map(s => s.getPosition()), ch);
        if (!autoClosingPair) {
            return null;
        }
        const subAutoClosingPairClose = this._findSubAutoClosingPairClose(config, autoClosingPair);
        let isSubAutoClosingPairPresent = true;
        const shouldAutoCloseBefore = chIsQuote ? config.shouldAutoCloseBefore.quote : config.shouldAutoCloseBefore.bracket;
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            if (!selection.isEmpty()) {
                return null;
            }
            const position = selection.getPosition();
            const lineText = model.getLineContent(position.lineNumber);
            const lineAfter = lineText.substring(position.column - 1);
            if (!lineAfter.startsWith(subAutoClosingPairClose)) {
                isSubAutoClosingPairPresent = false;
            }
            // Only consider auto closing the pair if an allowed character follows or if another autoclosed pair closing brace follows
            if (lineText.length > position.column - 1) {
                const characterAfter = lineText.charAt(position.column - 1);
                const isBeforeCloseBrace = TypeOperations._isBeforeClosingBrace(config, lineAfter);
                if (!isBeforeCloseBrace && !shouldAutoCloseBefore(characterAfter)) {
                    return null;
                }
            }
            if (!model.isCheapToTokenize(position.lineNumber)) {
                // Do not force tokenization
                return null;
            }
            // Do not auto-close ' or " after a word character
            if (autoClosingPair.open.length === 1 && chIsQuote && autoCloseConfig !== 'always') {
                const wordSeparators = getMapForWordSeparators(config.wordSeparators);
                if (insertOpenCharacter && position.column > 1 && wordSeparators.get(lineText.charCodeAt(position.column - 2)) === 0 /* Regular */) {
                    return null;
                }
                if (!insertOpenCharacter && position.column > 2 && wordSeparators.get(lineText.charCodeAt(position.column - 3)) === 0 /* Regular */) {
                    return null;
                }
            }
            model.forceTokenization(position.lineNumber);
            const lineTokens = model.getLineTokens(position.lineNumber);
            let shouldAutoClosePair = false;
            try {
                shouldAutoClosePair = LanguageConfigurationRegistry.shouldAutoClosePair(autoClosingPair, lineTokens, insertOpenCharacter ? position.column : position.column - 1);
            }
            catch (e) {
                onUnexpectedError(e);
            }
            if (!shouldAutoClosePair) {
                return null;
            }
        }
        if (isSubAutoClosingPairPresent) {
            return autoClosingPair.close.substring(0, autoClosingPair.close.length - subAutoClosingPairClose.length);
        }
        else {
            return autoClosingPair.close;
        }
    }
    static _runAutoClosingOpenCharType(prevEditOperationType, config, model, selections, ch, insertOpenCharacter, autoClosingPairClose) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            commands[i] = new TypeWithAutoClosingCommand(selection, ch, insertOpenCharacter, autoClosingPairClose);
        }
        return new EditOperationResult(1 /* Typing */, commands, {
            shouldPushStackElementBefore: true,
            shouldPushStackElementAfter: false
        });
    }
    static _shouldSurroundChar(config, ch) {
        if (isQuote(ch)) {
            return (config.autoSurround === 'quotes' || config.autoSurround === 'languageDefined');
        }
        else {
            // Character is a bracket
            return (config.autoSurround === 'brackets' || config.autoSurround === 'languageDefined');
        }
    }
    static _isSurroundSelectionType(config, model, selections, ch) {
        if (!TypeOperations._shouldSurroundChar(config, ch) || !config.surroundingPairs.hasOwnProperty(ch)) {
            return false;
        }
        const isTypingAQuoteCharacter = isQuote(ch);
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            if (selection.isEmpty()) {
                return false;
            }
            let selectionContainsOnlyWhitespace = true;
            for (let lineNumber = selection.startLineNumber; lineNumber <= selection.endLineNumber; lineNumber++) {
                const lineText = model.getLineContent(lineNumber);
                const startIndex = (lineNumber === selection.startLineNumber ? selection.startColumn - 1 : 0);
                const endIndex = (lineNumber === selection.endLineNumber ? selection.endColumn - 1 : lineText.length);
                const selectedText = lineText.substring(startIndex, endIndex);
                if (/[^ \t]/.test(selectedText)) {
                    // this selected text contains something other than whitespace
                    selectionContainsOnlyWhitespace = false;
                    break;
                }
            }
            if (selectionContainsOnlyWhitespace) {
                return false;
            }
            if (isTypingAQuoteCharacter && selection.startLineNumber === selection.endLineNumber && selection.startColumn + 1 === selection.endColumn) {
                const selectionText = model.getValueInRange(selection);
                if (isQuote(selectionText)) {
                    // Typing a quote character on top of another quote character
                    // => disable surround selection type
                    return false;
                }
            }
        }
        return true;
    }
    static _runSurroundSelectionType(prevEditOperationType, config, model, selections, ch) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const selection = selections[i];
            const closeCharacter = config.surroundingPairs[ch];
            commands[i] = new SurroundSelectionCommand(selection, ch, closeCharacter);
        }
        return new EditOperationResult(0 /* Other */, commands, {
            shouldPushStackElementBefore: true,
            shouldPushStackElementAfter: true
        });
    }
    static _isTypeInterceptorElectricChar(config, model, selections) {
        if (selections.length === 1 && model.isCheapToTokenize(selections[0].getEndPosition().lineNumber)) {
            return true;
        }
        return false;
    }
    static _typeInterceptorElectricChar(prevEditOperationType, config, model, selection, ch) {
        if (!config.electricChars.hasOwnProperty(ch) || !selection.isEmpty()) {
            return null;
        }
        let position = selection.getPosition();
        model.forceTokenization(position.lineNumber);
        let lineTokens = model.getLineTokens(position.lineNumber);
        let electricAction;
        try {
            electricAction = LanguageConfigurationRegistry.onElectricCharacter(ch, lineTokens, position.column);
        }
        catch (e) {
            onUnexpectedError(e);
            return null;
        }
        if (!electricAction) {
            return null;
        }
        if (electricAction.matchOpenBracket) {
            let endColumn = (lineTokens.getLineContent() + ch).lastIndexOf(electricAction.matchOpenBracket) + 1;
            let match = model.findMatchingBracketUp(electricAction.matchOpenBracket, {
                lineNumber: position.lineNumber,
                column: endColumn
            });
            if (match) {
                if (match.startLineNumber === position.lineNumber) {
                    // matched something on the same line => no change in indentation
                    return null;
                }
                let matchLine = model.getLineContent(match.startLineNumber);
                let matchLineIndentation = strings.getLeadingWhitespace(matchLine);
                let newIndentation = config.normalizeIndentation(matchLineIndentation);
                let lineText = model.getLineContent(position.lineNumber);
                let lineFirstNonBlankColumn = model.getLineFirstNonWhitespaceColumn(position.lineNumber) || position.column;
                let prefix = lineText.substring(lineFirstNonBlankColumn - 1, position.column - 1);
                let typeText = newIndentation + prefix + ch;
                let typeSelection = new Range(position.lineNumber, 1, position.lineNumber, position.column);
                const command = new ReplaceCommand(typeSelection, typeText);
                return new EditOperationResult(1 /* Typing */, [command], {
                    shouldPushStackElementBefore: false,
                    shouldPushStackElementAfter: true
                });
            }
        }
        return null;
    }
    /**
     * This is very similar with typing, but the character is already in the text buffer!
     */
    static compositionEndWithInterceptors(prevEditOperationType, config, model, selectionsWhenCompositionStarted, selections, autoClosedCharacters) {
        if (!selectionsWhenCompositionStarted || Selection.selectionsArrEqual(selectionsWhenCompositionStarted, selections)) {
            // no content was typed
            return null;
        }
        let ch = null;
        // extract last typed character
        for (const selection of selections) {
            if (!selection.isEmpty()) {
                return null;
            }
            const position = selection.getPosition();
            const currentChar = model.getValueInRange(new Range(position.lineNumber, position.column - 1, position.lineNumber, position.column));
            if (ch === null) {
                ch = currentChar;
            }
            else if (ch !== currentChar) {
                return null;
            }
        }
        if (!ch) {
            return null;
        }
        if (this._isAutoClosingOvertype(config, model, selections, autoClosedCharacters, ch)) {
            // Unfortunately, the close character is at this point "doubled", so we need to delete it...
            const commands = selections.map(s => new ReplaceCommand(new Range(s.positionLineNumber, s.positionColumn, s.positionLineNumber, s.positionColumn + 1), '', false));
            return new EditOperationResult(1 /* Typing */, commands, {
                shouldPushStackElementBefore: true,
                shouldPushStackElementAfter: false
            });
        }
        const autoClosingPairClose = this._getAutoClosingPairClose(config, model, selections, ch, false);
        if (autoClosingPairClose !== null) {
            return this._runAutoClosingOpenCharType(prevEditOperationType, config, model, selections, ch, false, autoClosingPairClose);
        }
        return null;
    }
    static typeWithInterceptors(isDoingComposition, prevEditOperationType, config, model, selections, autoClosedCharacters, ch) {
        if (!isDoingComposition && ch === '\n') {
            let commands = [];
            for (let i = 0, len = selections.length; i < len; i++) {
                commands[i] = TypeOperations._enter(config, model, false, selections[i]);
            }
            return new EditOperationResult(1 /* Typing */, commands, {
                shouldPushStackElementBefore: true,
                shouldPushStackElementAfter: false,
            });
        }
        if (!isDoingComposition && this._isAutoIndentType(config, model, selections)) {
            let commands = [];
            let autoIndentFails = false;
            for (let i = 0, len = selections.length; i < len; i++) {
                commands[i] = this._runAutoIndentType(config, model, selections[i], ch);
                if (!commands[i]) {
                    autoIndentFails = true;
                    break;
                }
            }
            if (!autoIndentFails) {
                return new EditOperationResult(1 /* Typing */, commands, {
                    shouldPushStackElementBefore: true,
                    shouldPushStackElementAfter: false,
                });
            }
        }
        if (!isDoingComposition && this._isAutoClosingOvertype(config, model, selections, autoClosedCharacters, ch)) {
            return this._runAutoClosingOvertype(prevEditOperationType, config, model, selections, ch);
        }
        if (!isDoingComposition) {
            const autoClosingPairClose = this._getAutoClosingPairClose(config, model, selections, ch, true);
            if (autoClosingPairClose) {
                return this._runAutoClosingOpenCharType(prevEditOperationType, config, model, selections, ch, true, autoClosingPairClose);
            }
        }
        if (this._isSurroundSelectionType(config, model, selections, ch)) {
            return this._runSurroundSelectionType(prevEditOperationType, config, model, selections, ch);
        }
        // Electric characters make sense only when dealing with a single cursor,
        // as multiple cursors typing brackets for example would interfer with bracket matching
        if (!isDoingComposition && this._isTypeInterceptorElectricChar(config, model, selections)) {
            const r = this._typeInterceptorElectricChar(prevEditOperationType, config, model, selections[0], ch);
            if (r) {
                return r;
            }
        }
        // A simple character type
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            commands[i] = new ReplaceCommand(selections[i], ch);
        }
        let shouldPushStackElementBefore = (prevEditOperationType !== 1 /* Typing */);
        if (ch === ' ') {
            shouldPushStackElementBefore = true;
        }
        return new EditOperationResult(1 /* Typing */, commands, {
            shouldPushStackElementBefore: shouldPushStackElementBefore,
            shouldPushStackElementAfter: false
        });
    }
    static typeWithoutInterceptors(prevEditOperationType, config, model, selections, str) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            commands[i] = new ReplaceCommand(selections[i], str);
        }
        return new EditOperationResult(1 /* Typing */, commands, {
            shouldPushStackElementBefore: (prevEditOperationType !== 1 /* Typing */),
            shouldPushStackElementAfter: false
        });
    }
    static lineInsertBefore(config, model, selections) {
        if (model === null || selections === null) {
            return [];
        }
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            let lineNumber = selections[i].positionLineNumber;
            if (lineNumber === 1) {
                commands[i] = new ReplaceCommandWithoutChangingPosition(new Range(1, 1, 1, 1), '\n');
            }
            else {
                lineNumber--;
                let column = model.getLineMaxColumn(lineNumber);
                commands[i] = this._enter(config, model, false, new Range(lineNumber, column, lineNumber, column));
            }
        }
        return commands;
    }
    static lineInsertAfter(config, model, selections) {
        if (model === null || selections === null) {
            return [];
        }
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            const lineNumber = selections[i].positionLineNumber;
            let column = model.getLineMaxColumn(lineNumber);
            commands[i] = this._enter(config, model, false, new Range(lineNumber, column, lineNumber, column));
        }
        return commands;
    }
    static lineBreakInsert(config, model, selections) {
        let commands = [];
        for (let i = 0, len = selections.length; i < len; i++) {
            commands[i] = this._enter(config, model, true, selections[i]);
        }
        return commands;
    }
}
export class TypeWithAutoClosingCommand extends ReplaceCommandWithOffsetCursorState {
    constructor(selection, openCharacter, insertOpenCharacter, closeCharacter) {
        super(selection, (insertOpenCharacter ? openCharacter : '') + closeCharacter, 0, -closeCharacter.length);
        this._openCharacter = openCharacter;
        this._closeCharacter = closeCharacter;
        this.closeCharacterRange = null;
        this.enclosingRange = null;
    }
    computeCursorState(model, helper) {
        let inverseEditOperations = helper.getInverseEditOperations();
        let range = inverseEditOperations[0].range;
        this.closeCharacterRange = new Range(range.startLineNumber, range.endColumn - this._closeCharacter.length, range.endLineNumber, range.endColumn);
        this.enclosingRange = new Range(range.startLineNumber, range.endColumn - this._openCharacter.length - this._closeCharacter.length, range.endLineNumber, range.endColumn);
        return super.computeCursorState(model, helper);
    }
}
