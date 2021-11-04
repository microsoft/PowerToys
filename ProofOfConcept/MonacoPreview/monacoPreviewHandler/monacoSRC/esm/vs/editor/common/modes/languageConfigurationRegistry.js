/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Emitter } from '../../../base/common/event.js';
import { toDisposable } from '../../../base/common/lifecycle.js';
import * as strings from '../../../base/common/strings.js';
import { DEFAULT_WORD_REGEXP, ensureValidWordDefinition } from '../model/wordHelper.js';
import { IndentAction, AutoClosingPairs } from './languageConfiguration.js';
import { createScopedLineTokens } from './supports.js';
import { CharacterPairSupport } from './supports/characterPair.js';
import { BracketElectricCharacterSupport } from './supports/electricCharacter.js';
import { IndentRulesSupport } from './supports/indentRules.js';
import { OnEnterSupport } from './supports/onEnter.js';
import { RichEditBrackets } from './supports/richEditBrackets.js';
export class RichEditSupport {
    constructor(languageIdentifier, previous, rawConf) {
        this._languageIdentifier = languageIdentifier;
        this._brackets = null;
        this._electricCharacter = null;
        let prev = null;
        if (previous) {
            prev = previous._conf;
        }
        this._conf = RichEditSupport._mergeConf(prev, rawConf);
        this._onEnterSupport = (this._conf.brackets || this._conf.indentationRules || this._conf.onEnterRules ? new OnEnterSupport(this._conf) : null);
        this.comments = RichEditSupport._handleComments(this._conf);
        this.characterPair = new CharacterPairSupport(this._conf);
        this.wordDefinition = this._conf.wordPattern || DEFAULT_WORD_REGEXP;
        this.indentationRules = this._conf.indentationRules;
        if (this._conf.indentationRules) {
            this.indentRulesSupport = new IndentRulesSupport(this._conf.indentationRules);
        }
        else {
            this.indentRulesSupport = null;
        }
        this.foldingRules = this._conf.folding || {};
    }
    get brackets() {
        if (!this._brackets && this._conf.brackets) {
            this._brackets = new RichEditBrackets(this._languageIdentifier, this._conf.brackets);
        }
        return this._brackets;
    }
    get electricCharacter() {
        if (!this._electricCharacter) {
            this._electricCharacter = new BracketElectricCharacterSupport(this.brackets);
        }
        return this._electricCharacter;
    }
    onEnter(autoIndent, previousLineText, beforeEnterText, afterEnterText) {
        if (!this._onEnterSupport) {
            return null;
        }
        return this._onEnterSupport.onEnter(autoIndent, previousLineText, beforeEnterText, afterEnterText);
    }
    static _mergeConf(prev, current) {
        return {
            comments: (prev ? current.comments || prev.comments : current.comments),
            brackets: (prev ? current.brackets || prev.brackets : current.brackets),
            wordPattern: (prev ? current.wordPattern || prev.wordPattern : current.wordPattern),
            indentationRules: (prev ? current.indentationRules || prev.indentationRules : current.indentationRules),
            onEnterRules: (prev ? current.onEnterRules || prev.onEnterRules : current.onEnterRules),
            autoClosingPairs: (prev ? current.autoClosingPairs || prev.autoClosingPairs : current.autoClosingPairs),
            surroundingPairs: (prev ? current.surroundingPairs || prev.surroundingPairs : current.surroundingPairs),
            autoCloseBefore: (prev ? current.autoCloseBefore || prev.autoCloseBefore : current.autoCloseBefore),
            folding: (prev ? current.folding || prev.folding : current.folding),
            __electricCharacterSupport: (prev ? current.__electricCharacterSupport || prev.__electricCharacterSupport : current.__electricCharacterSupport),
        };
    }
    static _handleComments(conf) {
        let commentRule = conf.comments;
        if (!commentRule) {
            return null;
        }
        // comment configuration
        let comments = {};
        if (commentRule.lineComment) {
            comments.lineCommentToken = commentRule.lineComment;
        }
        if (commentRule.blockComment) {
            let [blockStart, blockEnd] = commentRule.blockComment;
            comments.blockCommentStartToken = blockStart;
            comments.blockCommentEndToken = blockEnd;
        }
        return comments;
    }
}
export class LanguageConfigurationChangeEvent {
    constructor(languageIdentifier) {
        this.languageIdentifier = languageIdentifier;
    }
}
export class LanguageConfigurationRegistryImpl {
    constructor() {
        this._entries = new Map();
        this._onDidChange = new Emitter();
        this.onDidChange = this._onDidChange.event;
    }
    register(languageIdentifier, configuration) {
        let previous = this._getRichEditSupport(languageIdentifier.id);
        let current = new RichEditSupport(languageIdentifier, previous, configuration);
        this._entries.set(languageIdentifier.id, current);
        this._onDidChange.fire(new LanguageConfigurationChangeEvent(languageIdentifier));
        return toDisposable(() => {
            if (this._entries.get(languageIdentifier.id) === current) {
                this._entries.set(languageIdentifier.id, previous);
                this._onDidChange.fire(new LanguageConfigurationChangeEvent(languageIdentifier));
            }
        });
    }
    _getRichEditSupport(languageId) {
        return this._entries.get(languageId);
    }
    getIndentationRules(languageId) {
        const value = this._entries.get(languageId);
        if (!value) {
            return null;
        }
        return value.indentationRules || null;
    }
    // begin electricCharacter
    _getElectricCharacterSupport(languageId) {
        let value = this._getRichEditSupport(languageId);
        if (!value) {
            return null;
        }
        return value.electricCharacter || null;
    }
    getElectricCharacters(languageId) {
        let electricCharacterSupport = this._getElectricCharacterSupport(languageId);
        if (!electricCharacterSupport) {
            return [];
        }
        return electricCharacterSupport.getElectricCharacters();
    }
    /**
     * Should return opening bracket type to match indentation with
     */
    onElectricCharacter(character, context, column) {
        let scopedLineTokens = createScopedLineTokens(context, column - 1);
        let electricCharacterSupport = this._getElectricCharacterSupport(scopedLineTokens.languageId);
        if (!electricCharacterSupport) {
            return null;
        }
        return electricCharacterSupport.onElectricCharacter(character, scopedLineTokens, column - scopedLineTokens.firstCharOffset);
    }
    // end electricCharacter
    getComments(languageId) {
        let value = this._getRichEditSupport(languageId);
        if (!value) {
            return null;
        }
        return value.comments || null;
    }
    // begin characterPair
    _getCharacterPairSupport(languageId) {
        let value = this._getRichEditSupport(languageId);
        if (!value) {
            return null;
        }
        return value.characterPair || null;
    }
    getAutoClosingPairs(languageId) {
        const characterPairSupport = this._getCharacterPairSupport(languageId);
        return new AutoClosingPairs(characterPairSupport ? characterPairSupport.getAutoClosingPairs() : []);
    }
    getAutoCloseBeforeSet(languageId) {
        let characterPairSupport = this._getCharacterPairSupport(languageId);
        if (!characterPairSupport) {
            return CharacterPairSupport.DEFAULT_AUTOCLOSE_BEFORE_LANGUAGE_DEFINED;
        }
        return characterPairSupport.getAutoCloseBeforeSet();
    }
    getSurroundingPairs(languageId) {
        let characterPairSupport = this._getCharacterPairSupport(languageId);
        if (!characterPairSupport) {
            return [];
        }
        return characterPairSupport.getSurroundingPairs();
    }
    shouldAutoClosePair(autoClosingPair, context, column) {
        const scopedLineTokens = createScopedLineTokens(context, column - 1);
        return CharacterPairSupport.shouldAutoClosePair(autoClosingPair, scopedLineTokens, column - scopedLineTokens.firstCharOffset);
    }
    // end characterPair
    getWordDefinition(languageId) {
        let value = this._getRichEditSupport(languageId);
        if (!value) {
            return ensureValidWordDefinition(null);
        }
        return ensureValidWordDefinition(value.wordDefinition || null);
    }
    getFoldingRules(languageId) {
        let value = this._getRichEditSupport(languageId);
        if (!value) {
            return {};
        }
        return value.foldingRules;
    }
    // begin Indent Rules
    getIndentRulesSupport(languageId) {
        let value = this._getRichEditSupport(languageId);
        if (!value) {
            return null;
        }
        return value.indentRulesSupport || null;
    }
    /**
     * Get nearest preceiding line which doesn't match unIndentPattern or contains all whitespace.
     * Result:
     * -1: run into the boundary of embedded languages
     * 0: every line above are invalid
     * else: nearest preceding line of the same language
     */
    getPrecedingValidLine(model, lineNumber, indentRulesSupport) {
        let languageID = model.getLanguageIdAtPosition(lineNumber, 0);
        if (lineNumber > 1) {
            let lastLineNumber;
            let resultLineNumber = -1;
            for (lastLineNumber = lineNumber - 1; lastLineNumber >= 1; lastLineNumber--) {
                if (model.getLanguageIdAtPosition(lastLineNumber, 0) !== languageID) {
                    return resultLineNumber;
                }
                let text = model.getLineContent(lastLineNumber);
                if (indentRulesSupport.shouldIgnore(text) || /^\s+$/.test(text) || text === '') {
                    resultLineNumber = lastLineNumber;
                    continue;
                }
                return lastLineNumber;
            }
        }
        return -1;
    }
    /**
     * Get inherited indentation from above lines.
     * 1. Find the nearest preceding line which doesn't match unIndentedLinePattern.
     * 2. If this line matches indentNextLinePattern or increaseIndentPattern, it means that the indent level of `lineNumber` should be 1 greater than this line.
     * 3. If this line doesn't match any indent rules
     *   a. check whether the line above it matches indentNextLinePattern
     *   b. If not, the indent level of this line is the result
     *   c. If so, it means the indent of this line is *temporary*, go upward utill we find a line whose indent is not temporary (the same workflow a -> b -> c).
     * 4. Otherwise, we fail to get an inherited indent from aboves. Return null and we should not touch the indent of `lineNumber`
     *
     * This function only return the inherited indent based on above lines, it doesn't check whether current line should decrease or not.
     */
    getInheritIndentForLine(autoIndent, model, lineNumber, honorIntentialIndent = true) {
        if (autoIndent < 4 /* Full */) {
            return null;
        }
        const indentRulesSupport = this.getIndentRulesSupport(model.getLanguageIdentifier().id);
        if (!indentRulesSupport) {
            return null;
        }
        if (lineNumber <= 1) {
            return {
                indentation: '',
                action: null
            };
        }
        const precedingUnIgnoredLine = this.getPrecedingValidLine(model, lineNumber, indentRulesSupport);
        if (precedingUnIgnoredLine < 0) {
            return null;
        }
        else if (precedingUnIgnoredLine < 1) {
            return {
                indentation: '',
                action: null
            };
        }
        const precedingUnIgnoredLineContent = model.getLineContent(precedingUnIgnoredLine);
        if (indentRulesSupport.shouldIncrease(precedingUnIgnoredLineContent) || indentRulesSupport.shouldIndentNextLine(precedingUnIgnoredLineContent)) {
            return {
                indentation: strings.getLeadingWhitespace(precedingUnIgnoredLineContent),
                action: IndentAction.Indent,
                line: precedingUnIgnoredLine
            };
        }
        else if (indentRulesSupport.shouldDecrease(precedingUnIgnoredLineContent)) {
            return {
                indentation: strings.getLeadingWhitespace(precedingUnIgnoredLineContent),
                action: null,
                line: precedingUnIgnoredLine
            };
        }
        else {
            // precedingUnIgnoredLine can not be ignored.
            // it doesn't increase indent of following lines
            // it doesn't increase just next line
            // so current line is not affect by precedingUnIgnoredLine
            // and then we should get a correct inheritted indentation from above lines
            if (precedingUnIgnoredLine === 1) {
                return {
                    indentation: strings.getLeadingWhitespace(model.getLineContent(precedingUnIgnoredLine)),
                    action: null,
                    line: precedingUnIgnoredLine
                };
            }
            const previousLine = precedingUnIgnoredLine - 1;
            const previousLineIndentMetadata = indentRulesSupport.getIndentMetadata(model.getLineContent(previousLine));
            if (!(previousLineIndentMetadata & (1 /* INCREASE_MASK */ | 2 /* DECREASE_MASK */)) &&
                (previousLineIndentMetadata & 4 /* INDENT_NEXTLINE_MASK */)) {
                let stopLine = 0;
                for (let i = previousLine - 1; i > 0; i--) {
                    if (indentRulesSupport.shouldIndentNextLine(model.getLineContent(i))) {
                        continue;
                    }
                    stopLine = i;
                    break;
                }
                return {
                    indentation: strings.getLeadingWhitespace(model.getLineContent(stopLine + 1)),
                    action: null,
                    line: stopLine + 1
                };
            }
            if (honorIntentialIndent) {
                return {
                    indentation: strings.getLeadingWhitespace(model.getLineContent(precedingUnIgnoredLine)),
                    action: null,
                    line: precedingUnIgnoredLine
                };
            }
            else {
                // search from precedingUnIgnoredLine until we find one whose indent is not temporary
                for (let i = precedingUnIgnoredLine; i > 0; i--) {
                    const lineContent = model.getLineContent(i);
                    if (indentRulesSupport.shouldIncrease(lineContent)) {
                        return {
                            indentation: strings.getLeadingWhitespace(lineContent),
                            action: IndentAction.Indent,
                            line: i
                        };
                    }
                    else if (indentRulesSupport.shouldIndentNextLine(lineContent)) {
                        let stopLine = 0;
                        for (let j = i - 1; j > 0; j--) {
                            if (indentRulesSupport.shouldIndentNextLine(model.getLineContent(i))) {
                                continue;
                            }
                            stopLine = j;
                            break;
                        }
                        return {
                            indentation: strings.getLeadingWhitespace(model.getLineContent(stopLine + 1)),
                            action: null,
                            line: stopLine + 1
                        };
                    }
                    else if (indentRulesSupport.shouldDecrease(lineContent)) {
                        return {
                            indentation: strings.getLeadingWhitespace(lineContent),
                            action: null,
                            line: i
                        };
                    }
                }
                return {
                    indentation: strings.getLeadingWhitespace(model.getLineContent(1)),
                    action: null,
                    line: 1
                };
            }
        }
    }
    getGoodIndentForLine(autoIndent, virtualModel, languageId, lineNumber, indentConverter) {
        if (autoIndent < 4 /* Full */) {
            return null;
        }
        const richEditSupport = this._getRichEditSupport(languageId);
        if (!richEditSupport) {
            return null;
        }
        const indentRulesSupport = this.getIndentRulesSupport(languageId);
        if (!indentRulesSupport) {
            return null;
        }
        const indent = this.getInheritIndentForLine(autoIndent, virtualModel, lineNumber);
        const lineContent = virtualModel.getLineContent(lineNumber);
        if (indent) {
            const inheritLine = indent.line;
            if (inheritLine !== undefined) {
                const enterResult = richEditSupport.onEnter(autoIndent, '', virtualModel.getLineContent(inheritLine), '');
                if (enterResult) {
                    let indentation = strings.getLeadingWhitespace(virtualModel.getLineContent(inheritLine));
                    if (enterResult.removeText) {
                        indentation = indentation.substring(0, indentation.length - enterResult.removeText);
                    }
                    if ((enterResult.indentAction === IndentAction.Indent) ||
                        (enterResult.indentAction === IndentAction.IndentOutdent)) {
                        indentation = indentConverter.shiftIndent(indentation);
                    }
                    else if (enterResult.indentAction === IndentAction.Outdent) {
                        indentation = indentConverter.unshiftIndent(indentation);
                    }
                    if (indentRulesSupport.shouldDecrease(lineContent)) {
                        indentation = indentConverter.unshiftIndent(indentation);
                    }
                    if (enterResult.appendText) {
                        indentation += enterResult.appendText;
                    }
                    return strings.getLeadingWhitespace(indentation);
                }
            }
            if (indentRulesSupport.shouldDecrease(lineContent)) {
                if (indent.action === IndentAction.Indent) {
                    return indent.indentation;
                }
                else {
                    return indentConverter.unshiftIndent(indent.indentation);
                }
            }
            else {
                if (indent.action === IndentAction.Indent) {
                    return indentConverter.shiftIndent(indent.indentation);
                }
                else {
                    return indent.indentation;
                }
            }
        }
        return null;
    }
    getIndentForEnter(autoIndent, model, range, indentConverter) {
        if (autoIndent < 4 /* Full */) {
            return null;
        }
        model.forceTokenization(range.startLineNumber);
        const lineTokens = model.getLineTokens(range.startLineNumber);
        const scopedLineTokens = createScopedLineTokens(lineTokens, range.startColumn - 1);
        const scopedLineText = scopedLineTokens.getLineContent();
        let embeddedLanguage = false;
        let beforeEnterText;
        if (scopedLineTokens.firstCharOffset > 0 && lineTokens.getLanguageId(0) !== scopedLineTokens.languageId) {
            // we are in the embeded language content
            embeddedLanguage = true; // if embeddedLanguage is true, then we don't touch the indentation of current line
            beforeEnterText = scopedLineText.substr(0, range.startColumn - 1 - scopedLineTokens.firstCharOffset);
        }
        else {
            beforeEnterText = lineTokens.getLineContent().substring(0, range.startColumn - 1);
        }
        let afterEnterText;
        if (range.isEmpty()) {
            afterEnterText = scopedLineText.substr(range.startColumn - 1 - scopedLineTokens.firstCharOffset);
        }
        else {
            const endScopedLineTokens = this.getScopedLineTokens(model, range.endLineNumber, range.endColumn);
            afterEnterText = endScopedLineTokens.getLineContent().substr(range.endColumn - 1 - scopedLineTokens.firstCharOffset);
        }
        const indentRulesSupport = this.getIndentRulesSupport(scopedLineTokens.languageId);
        if (!indentRulesSupport) {
            return null;
        }
        const beforeEnterResult = beforeEnterText;
        const beforeEnterIndent = strings.getLeadingWhitespace(beforeEnterText);
        const virtualModel = {
            getLineTokens: (lineNumber) => {
                return model.getLineTokens(lineNumber);
            },
            getLanguageIdentifier: () => {
                return model.getLanguageIdentifier();
            },
            getLanguageIdAtPosition: (lineNumber, column) => {
                return model.getLanguageIdAtPosition(lineNumber, column);
            },
            getLineContent: (lineNumber) => {
                if (lineNumber === range.startLineNumber) {
                    return beforeEnterResult;
                }
                else {
                    return model.getLineContent(lineNumber);
                }
            }
        };
        const currentLineIndent = strings.getLeadingWhitespace(lineTokens.getLineContent());
        const afterEnterAction = this.getInheritIndentForLine(autoIndent, virtualModel, range.startLineNumber + 1);
        if (!afterEnterAction) {
            const beforeEnter = embeddedLanguage ? currentLineIndent : beforeEnterIndent;
            return {
                beforeEnter: beforeEnter,
                afterEnter: beforeEnter
            };
        }
        let afterEnterIndent = embeddedLanguage ? currentLineIndent : afterEnterAction.indentation;
        if (afterEnterAction.action === IndentAction.Indent) {
            afterEnterIndent = indentConverter.shiftIndent(afterEnterIndent);
        }
        if (indentRulesSupport.shouldDecrease(afterEnterText)) {
            afterEnterIndent = indentConverter.unshiftIndent(afterEnterIndent);
        }
        return {
            beforeEnter: embeddedLanguage ? currentLineIndent : beforeEnterIndent,
            afterEnter: afterEnterIndent
        };
    }
    /**
     * We should always allow intentional indentation. It means, if users change the indentation of `lineNumber` and the content of
     * this line doesn't match decreaseIndentPattern, we should not adjust the indentation.
     */
    getIndentActionForType(autoIndent, model, range, ch, indentConverter) {
        if (autoIndent < 4 /* Full */) {
            return null;
        }
        const scopedLineTokens = this.getScopedLineTokens(model, range.startLineNumber, range.startColumn);
        if (scopedLineTokens.firstCharOffset) {
            // this line has mixed languages and indentation rules will not work
            return null;
        }
        const indentRulesSupport = this.getIndentRulesSupport(scopedLineTokens.languageId);
        if (!indentRulesSupport) {
            return null;
        }
        const scopedLineText = scopedLineTokens.getLineContent();
        const beforeTypeText = scopedLineText.substr(0, range.startColumn - 1 - scopedLineTokens.firstCharOffset);
        // selection support
        let afterTypeText;
        if (range.isEmpty()) {
            afterTypeText = scopedLineText.substr(range.startColumn - 1 - scopedLineTokens.firstCharOffset);
        }
        else {
            const endScopedLineTokens = this.getScopedLineTokens(model, range.endLineNumber, range.endColumn);
            afterTypeText = endScopedLineTokens.getLineContent().substr(range.endColumn - 1 - scopedLineTokens.firstCharOffset);
        }
        // If previous content already matches decreaseIndentPattern, it means indentation of this line should already be adjusted
        // Users might change the indentation by purpose and we should honor that instead of readjusting.
        if (!indentRulesSupport.shouldDecrease(beforeTypeText + afterTypeText) && indentRulesSupport.shouldDecrease(beforeTypeText + ch + afterTypeText)) {
            // after typing `ch`, the content matches decreaseIndentPattern, we should adjust the indent to a good manner.
            // 1. Get inherited indent action
            const r = this.getInheritIndentForLine(autoIndent, model, range.startLineNumber, false);
            if (!r) {
                return null;
            }
            let indentation = r.indentation;
            if (r.action !== IndentAction.Indent) {
                indentation = indentConverter.unshiftIndent(indentation);
            }
            return indentation;
        }
        return null;
    }
    getIndentMetadata(model, lineNumber) {
        const indentRulesSupport = this.getIndentRulesSupport(model.getLanguageIdentifier().id);
        if (!indentRulesSupport) {
            return null;
        }
        if (lineNumber < 1 || lineNumber > model.getLineCount()) {
            return null;
        }
        return indentRulesSupport.getIndentMetadata(model.getLineContent(lineNumber));
    }
    // end Indent Rules
    // begin onEnter
    getEnterAction(autoIndent, model, range) {
        const scopedLineTokens = this.getScopedLineTokens(model, range.startLineNumber, range.startColumn);
        const richEditSupport = this._getRichEditSupport(scopedLineTokens.languageId);
        if (!richEditSupport) {
            return null;
        }
        const scopedLineText = scopedLineTokens.getLineContent();
        const beforeEnterText = scopedLineText.substr(0, range.startColumn - 1 - scopedLineTokens.firstCharOffset);
        // selection support
        let afterEnterText;
        if (range.isEmpty()) {
            afterEnterText = scopedLineText.substr(range.startColumn - 1 - scopedLineTokens.firstCharOffset);
        }
        else {
            const endScopedLineTokens = this.getScopedLineTokens(model, range.endLineNumber, range.endColumn);
            afterEnterText = endScopedLineTokens.getLineContent().substr(range.endColumn - 1 - scopedLineTokens.firstCharOffset);
        }
        let previousLineText = '';
        if (range.startLineNumber > 1 && scopedLineTokens.firstCharOffset === 0) {
            // This is not the first line and the entire line belongs to this mode
            const oneLineAboveScopedLineTokens = this.getScopedLineTokens(model, range.startLineNumber - 1);
            if (oneLineAboveScopedLineTokens.languageId === scopedLineTokens.languageId) {
                // The line above ends with text belonging to the same mode
                previousLineText = oneLineAboveScopedLineTokens.getLineContent();
            }
        }
        const enterResult = richEditSupport.onEnter(autoIndent, previousLineText, beforeEnterText, afterEnterText);
        if (!enterResult) {
            return null;
        }
        const indentAction = enterResult.indentAction;
        let appendText = enterResult.appendText;
        const removeText = enterResult.removeText || 0;
        // Here we add `\t` to appendText first because enterAction is leveraging appendText and removeText to change indentation.
        if (!appendText) {
            if ((indentAction === IndentAction.Indent) ||
                (indentAction === IndentAction.IndentOutdent)) {
                appendText = '\t';
            }
            else {
                appendText = '';
            }
        }
        else if (indentAction === IndentAction.Indent) {
            appendText = '\t' + appendText;
        }
        let indentation = this.getIndentationAtPosition(model, range.startLineNumber, range.startColumn);
        if (removeText) {
            indentation = indentation.substring(0, indentation.length - removeText);
        }
        return {
            indentAction: indentAction,
            appendText: appendText,
            removeText: removeText,
            indentation: indentation
        };
    }
    getIndentationAtPosition(model, lineNumber, column) {
        const lineText = model.getLineContent(lineNumber);
        let indentation = strings.getLeadingWhitespace(lineText);
        if (indentation.length > column - 1) {
            indentation = indentation.substring(0, column - 1);
        }
        return indentation;
    }
    getScopedLineTokens(model, lineNumber, columnNumber) {
        model.forceTokenization(lineNumber);
        const lineTokens = model.getLineTokens(lineNumber);
        const column = (typeof columnNumber === 'undefined' ? model.getLineMaxColumn(lineNumber) - 1 : columnNumber - 1);
        return createScopedLineTokens(lineTokens, column);
    }
    // end onEnter
    getBracketsSupport(languageId) {
        const value = this._getRichEditSupport(languageId);
        if (!value) {
            return null;
        }
        return value.brackets || null;
    }
}
export const LanguageConfigurationRegistry = new LanguageConfigurationRegistryImpl();
