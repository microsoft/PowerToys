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
import './goToDefinitionAtPosition.css';
import * as nls from '../../../../nls.js';
import { createCancelablePromise } from '../../../../base/common/async.js';
import { onUnexpectedError } from '../../../../base/common/errors.js';
import { MarkdownString } from '../../../../base/common/htmlContent.js';
import { IModeService } from '../../../common/services/modeService.js';
import { Range } from '../../../common/core/range.js';
import { DefinitionProviderRegistry } from '../../../common/modes.js';
import { registerEditorContribution } from '../../../browser/editorExtensions.js';
import { getDefinitionsAtPosition } from '../goToSymbol.js';
import { DisposableStore } from '../../../../base/common/lifecycle.js';
import { ITextModelService } from '../../../common/services/resolverService.js';
import { registerThemingParticipant } from '../../../../platform/theme/common/themeService.js';
import { editorActiveLinkForeground } from '../../../../platform/theme/common/colorRegistry.js';
import { EditorState } from '../../../browser/core/editorState.js';
import { DefinitionAction } from '../goToCommands.js';
import { ClickLinkGesture } from './clickLinkGesture.js';
import { Position } from '../../../common/core/position.js';
import { withNullAsUndefined } from '../../../../base/common/types.js';
import { PeekContext } from '../../peekView/peekView.js';
import { IContextKeyService } from '../../../../platform/contextkey/common/contextkey.js';
let GotoDefinitionAtPositionEditorContribution = class GotoDefinitionAtPositionEditorContribution {
    constructor(editor, textModelResolverService, modeService) {
        this.textModelResolverService = textModelResolverService;
        this.modeService = modeService;
        this.toUnhook = new DisposableStore();
        this.toUnhookForKeyboard = new DisposableStore();
        this.linkDecorations = [];
        this.currentWordAtPosition = null;
        this.previousPromise = null;
        this.editor = editor;
        let linkGesture = new ClickLinkGesture(editor);
        this.toUnhook.add(linkGesture);
        this.toUnhook.add(linkGesture.onMouseMoveOrRelevantKeyDown(([mouseEvent, keyboardEvent]) => {
            this.startFindDefinitionFromMouse(mouseEvent, withNullAsUndefined(keyboardEvent));
        }));
        this.toUnhook.add(linkGesture.onExecute((mouseEvent) => {
            if (this.isEnabled(mouseEvent)) {
                this.gotoDefinition(mouseEvent.target.position, mouseEvent.hasSideBySideModifier).then(() => {
                    this.removeLinkDecorations();
                }, (error) => {
                    this.removeLinkDecorations();
                    onUnexpectedError(error);
                });
            }
        }));
        this.toUnhook.add(linkGesture.onCancel(() => {
            this.removeLinkDecorations();
            this.currentWordAtPosition = null;
        }));
    }
    static get(editor) {
        return editor.getContribution(GotoDefinitionAtPositionEditorContribution.ID);
    }
    startFindDefinitionFromCursor(position) {
        // For issue: https://github.com/microsoft/vscode/issues/46257
        // equivalent to mouse move with meta/ctrl key
        // First find the definition and add decorations
        // to the editor to be shown with the content hover widget
        return this.startFindDefinition(position).then(() => {
            // Add listeners for editor cursor move and key down events
            // Dismiss the "extended" editor decorations when the user hides
            // the hover widget. There is no event for the widget itself so these
            // serve as a best effort. After removing the link decorations, the hover
            // widget is clean and will only show declarations per next request.
            this.toUnhookForKeyboard.add(this.editor.onDidChangeCursorPosition(() => {
                this.currentWordAtPosition = null;
                this.removeLinkDecorations();
                this.toUnhookForKeyboard.clear();
            }));
            this.toUnhookForKeyboard.add(this.editor.onKeyDown((e) => {
                if (e) {
                    this.currentWordAtPosition = null;
                    this.removeLinkDecorations();
                    this.toUnhookForKeyboard.clear();
                }
            }));
        });
    }
    startFindDefinitionFromMouse(mouseEvent, withKey) {
        // check if we are active and on a content widget
        if (mouseEvent.target.type === 9 /* CONTENT_WIDGET */ && this.linkDecorations.length > 0) {
            return;
        }
        if (!this.editor.hasModel() || !this.isEnabled(mouseEvent, withKey)) {
            this.currentWordAtPosition = null;
            this.removeLinkDecorations();
            return;
        }
        const position = mouseEvent.target.position;
        this.startFindDefinition(position);
    }
    startFindDefinition(position) {
        var _a;
        // Dispose listeners for updating decorations when using keyboard to show definition hover
        this.toUnhookForKeyboard.clear();
        // Find word at mouse position
        const word = position ? (_a = this.editor.getModel()) === null || _a === void 0 ? void 0 : _a.getWordAtPosition(position) : null;
        if (!word) {
            this.currentWordAtPosition = null;
            this.removeLinkDecorations();
            return Promise.resolve(0);
        }
        // Return early if word at position is still the same
        if (this.currentWordAtPosition && this.currentWordAtPosition.startColumn === word.startColumn && this.currentWordAtPosition.endColumn === word.endColumn && this.currentWordAtPosition.word === word.word) {
            return Promise.resolve(0);
        }
        this.currentWordAtPosition = word;
        // Find definition and decorate word if found
        let state = new EditorState(this.editor, 4 /* Position */ | 1 /* Value */ | 2 /* Selection */ | 8 /* Scroll */);
        if (this.previousPromise) {
            this.previousPromise.cancel();
            this.previousPromise = null;
        }
        this.previousPromise = createCancelablePromise(token => this.findDefinition(position, token));
        return this.previousPromise.then(results => {
            if (!results || !results.length || !state.validate(this.editor)) {
                this.removeLinkDecorations();
                return;
            }
            // Multiple results
            if (results.length > 1) {
                this.addDecoration(new Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn), new MarkdownString().appendText(nls.localize('multipleResults', "Click to show {0} definitions.", results.length)));
            }
            // Single result
            else {
                let result = results[0];
                if (!result.uri) {
                    return;
                }
                this.textModelResolverService.createModelReference(result.uri).then(ref => {
                    if (!ref.object || !ref.object.textEditorModel) {
                        ref.dispose();
                        return;
                    }
                    const { object: { textEditorModel } } = ref;
                    const { startLineNumber } = result.range;
                    if (startLineNumber < 1 || startLineNumber > textEditorModel.getLineCount()) {
                        // invalid range
                        ref.dispose();
                        return;
                    }
                    const previewValue = this.getPreviewValue(textEditorModel, startLineNumber, result);
                    let wordRange;
                    if (result.originSelectionRange) {
                        wordRange = Range.lift(result.originSelectionRange);
                    }
                    else {
                        wordRange = new Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn);
                    }
                    const modeId = this.modeService.getModeIdByFilepathOrFirstLine(textEditorModel.uri);
                    this.addDecoration(wordRange, new MarkdownString().appendCodeblock(modeId ? modeId : '', previewValue));
                    ref.dispose();
                });
            }
        }).then(undefined, onUnexpectedError);
    }
    getPreviewValue(textEditorModel, startLineNumber, result) {
        let rangeToUse = result.targetSelectionRange ? result.range : this.getPreviewRangeBasedOnBrackets(textEditorModel, startLineNumber);
        const numberOfLinesInRange = rangeToUse.endLineNumber - rangeToUse.startLineNumber;
        if (numberOfLinesInRange >= GotoDefinitionAtPositionEditorContribution.MAX_SOURCE_PREVIEW_LINES) {
            rangeToUse = this.getPreviewRangeBasedOnIndentation(textEditorModel, startLineNumber);
        }
        const previewValue = this.stripIndentationFromPreviewRange(textEditorModel, startLineNumber, rangeToUse);
        return previewValue;
    }
    stripIndentationFromPreviewRange(textEditorModel, startLineNumber, previewRange) {
        const startIndent = textEditorModel.getLineFirstNonWhitespaceColumn(startLineNumber);
        let minIndent = startIndent;
        for (let endLineNumber = startLineNumber + 1; endLineNumber < previewRange.endLineNumber; endLineNumber++) {
            const endIndent = textEditorModel.getLineFirstNonWhitespaceColumn(endLineNumber);
            minIndent = Math.min(minIndent, endIndent);
        }
        const previewValue = textEditorModel.getValueInRange(previewRange).replace(new RegExp(`^\\s{${minIndent - 1}}`, 'gm'), '').trim();
        return previewValue;
    }
    getPreviewRangeBasedOnIndentation(textEditorModel, startLineNumber) {
        const startIndent = textEditorModel.getLineFirstNonWhitespaceColumn(startLineNumber);
        const maxLineNumber = Math.min(textEditorModel.getLineCount(), startLineNumber + GotoDefinitionAtPositionEditorContribution.MAX_SOURCE_PREVIEW_LINES);
        let endLineNumber = startLineNumber + 1;
        for (; endLineNumber < maxLineNumber; endLineNumber++) {
            let endIndent = textEditorModel.getLineFirstNonWhitespaceColumn(endLineNumber);
            if (startIndent === endIndent) {
                break;
            }
        }
        return new Range(startLineNumber, 1, endLineNumber + 1, 1);
    }
    getPreviewRangeBasedOnBrackets(textEditorModel, startLineNumber) {
        const maxLineNumber = Math.min(textEditorModel.getLineCount(), startLineNumber + GotoDefinitionAtPositionEditorContribution.MAX_SOURCE_PREVIEW_LINES);
        const brackets = [];
        let ignoreFirstEmpty = true;
        let currentBracket = textEditorModel.findNextBracket(new Position(startLineNumber, 1));
        while (currentBracket !== null) {
            if (brackets.length === 0) {
                brackets.push(currentBracket);
            }
            else {
                const lastBracket = brackets[brackets.length - 1];
                if (lastBracket.open[0] === currentBracket.open[0] && lastBracket.isOpen && !currentBracket.isOpen) {
                    brackets.pop();
                }
                else {
                    brackets.push(currentBracket);
                }
                if (brackets.length === 0) {
                    if (ignoreFirstEmpty) {
                        ignoreFirstEmpty = false;
                    }
                    else {
                        return new Range(startLineNumber, 1, currentBracket.range.endLineNumber + 1, 1);
                    }
                }
            }
            const maxColumn = textEditorModel.getLineMaxColumn(startLineNumber);
            let nextLineNumber = currentBracket.range.endLineNumber;
            let nextColumn = currentBracket.range.endColumn;
            if (maxColumn === currentBracket.range.endColumn) {
                nextLineNumber++;
                nextColumn = 1;
            }
            if (nextLineNumber > maxLineNumber) {
                return new Range(startLineNumber, 1, maxLineNumber + 1, 1);
            }
            currentBracket = textEditorModel.findNextBracket(new Position(nextLineNumber, nextColumn));
        }
        return new Range(startLineNumber, 1, maxLineNumber + 1, 1);
    }
    addDecoration(range, hoverMessage) {
        const newDecorations = {
            range: range,
            options: {
                inlineClassName: 'goto-definition-link',
                hoverMessage
            }
        };
        this.linkDecorations = this.editor.deltaDecorations(this.linkDecorations, [newDecorations]);
    }
    removeLinkDecorations() {
        if (this.linkDecorations.length > 0) {
            this.linkDecorations = this.editor.deltaDecorations(this.linkDecorations, []);
        }
    }
    isEnabled(mouseEvent, withKey) {
        return this.editor.hasModel() &&
            mouseEvent.isNoneOrSingleMouseDown &&
            (mouseEvent.target.type === 6 /* CONTENT_TEXT */) &&
            (mouseEvent.hasTriggerModifier || (withKey ? withKey.keyCodeIsTriggerKey : false)) &&
            DefinitionProviderRegistry.has(this.editor.getModel());
    }
    findDefinition(position, token) {
        const model = this.editor.getModel();
        if (!model) {
            return Promise.resolve(null);
        }
        return getDefinitionsAtPosition(model, position, token);
    }
    gotoDefinition(position, openToSide) {
        this.editor.setPosition(position);
        return this.editor.invokeWithinContext((accessor) => {
            const canPeek = !openToSide && this.editor.getOption(72 /* definitionLinkOpensInPeek */) && !this.isInPeekEditor(accessor);
            const action = new DefinitionAction({ openToSide, openInPeek: canPeek, muteMessage: true }, { alias: '', label: '', id: '', precondition: undefined });
            return action.run(accessor, this.editor);
        });
    }
    isInPeekEditor(accessor) {
        const contextKeyService = accessor.get(IContextKeyService);
        return PeekContext.inPeekEditor.getValue(contextKeyService);
    }
    dispose() {
        this.toUnhook.dispose();
    }
};
GotoDefinitionAtPositionEditorContribution.ID = 'editor.contrib.gotodefinitionatposition';
GotoDefinitionAtPositionEditorContribution.MAX_SOURCE_PREVIEW_LINES = 8;
GotoDefinitionAtPositionEditorContribution = __decorate([
    __param(1, ITextModelService),
    __param(2, IModeService)
], GotoDefinitionAtPositionEditorContribution);
export { GotoDefinitionAtPositionEditorContribution };
registerEditorContribution(GotoDefinitionAtPositionEditorContribution.ID, GotoDefinitionAtPositionEditorContribution);
registerThemingParticipant((theme, collector) => {
    const activeLinkForeground = theme.getColor(editorActiveLinkForeground);
    if (activeLinkForeground) {
        collector.addRule(`.monaco-editor .goto-definition-link { color: ${activeLinkForeground} !important; }`);
    }
});
