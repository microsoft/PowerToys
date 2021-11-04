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
import './inspectTokens.css';
import { $, append, reset } from '../../../../base/browser/dom.js';
import { Color } from '../../../../base/common/color.js';
import { Disposable } from '../../../../base/common/lifecycle.js';
import { EditorAction, registerEditorAction, registerEditorContribution } from '../../../browser/editorExtensions.js';
import { TokenMetadata, TokenizationRegistry } from '../../../common/modes.js';
import { NULL_STATE, nullTokenize, nullTokenize2 } from '../../../common/modes/nullMode.js';
import { IModeService } from '../../../common/services/modeService.js';
import { IStandaloneThemeService } from '../../common/standaloneThemeService.js';
import { editorHoverBackground, editorHoverBorder, editorHoverForeground } from '../../../../platform/theme/common/colorRegistry.js';
import { registerThemingParticipant } from '../../../../platform/theme/common/themeService.js';
import { InspectTokensNLS } from '../../../common/standaloneStrings.js';
import { ColorScheme } from '../../../../platform/theme/common/theme.js';
let InspectTokensController = class InspectTokensController extends Disposable {
    constructor(editor, standaloneColorService, modeService) {
        super();
        this._editor = editor;
        this._modeService = modeService;
        this._widget = null;
        this._register(this._editor.onDidChangeModel((e) => this.stop()));
        this._register(this._editor.onDidChangeModelLanguage((e) => this.stop()));
        this._register(TokenizationRegistry.onDidChange((e) => this.stop()));
        this._register(this._editor.onKeyUp((e) => e.keyCode === 9 /* Escape */ && this.stop()));
    }
    static get(editor) {
        return editor.getContribution(InspectTokensController.ID);
    }
    dispose() {
        this.stop();
        super.dispose();
    }
    launch() {
        if (this._widget) {
            return;
        }
        if (!this._editor.hasModel()) {
            return;
        }
        this._widget = new InspectTokensWidget(this._editor, this._modeService);
    }
    stop() {
        if (this._widget) {
            this._widget.dispose();
            this._widget = null;
        }
    }
};
InspectTokensController.ID = 'editor.contrib.inspectTokens';
InspectTokensController = __decorate([
    __param(1, IStandaloneThemeService),
    __param(2, IModeService)
], InspectTokensController);
class InspectTokens extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.inspectTokens',
            label: InspectTokensNLS.inspectTokensAction,
            alias: 'Developer: Inspect Tokens',
            precondition: undefined
        });
    }
    run(accessor, editor) {
        let controller = InspectTokensController.get(editor);
        if (controller) {
            controller.launch();
        }
    }
}
function renderTokenText(tokenText) {
    let result = '';
    for (let charIndex = 0, len = tokenText.length; charIndex < len; charIndex++) {
        let charCode = tokenText.charCodeAt(charIndex);
        switch (charCode) {
            case 9 /* Tab */:
                result += '\u2192'; // &rarr;
                break;
            case 32 /* Space */:
                result += '\u00B7'; // &middot;
                break;
            default:
                result += String.fromCharCode(charCode);
        }
    }
    return result;
}
function getSafeTokenizationSupport(languageIdentifier) {
    let tokenizationSupport = TokenizationRegistry.get(languageIdentifier.language);
    if (tokenizationSupport) {
        return tokenizationSupport;
    }
    return {
        getInitialState: () => NULL_STATE,
        tokenize: (line, hasEOL, state, deltaOffset) => nullTokenize(languageIdentifier.language, line, state, deltaOffset),
        tokenize2: (line, hasEOL, state, deltaOffset) => nullTokenize2(languageIdentifier.id, line, state, deltaOffset)
    };
}
class InspectTokensWidget extends Disposable {
    constructor(editor, modeService) {
        super();
        // Editor.IContentWidget.allowEditorOverflow
        this.allowEditorOverflow = true;
        this._editor = editor;
        this._modeService = modeService;
        this._model = this._editor.getModel();
        this._domNode = document.createElement('div');
        this._domNode.className = 'tokens-inspect-widget';
        this._tokenizationSupport = getSafeTokenizationSupport(this._model.getLanguageIdentifier());
        this._compute(this._editor.getPosition());
        this._register(this._editor.onDidChangeCursorPosition((e) => this._compute(this._editor.getPosition())));
        this._editor.addContentWidget(this);
    }
    dispose() {
        this._editor.removeContentWidget(this);
        super.dispose();
    }
    getId() {
        return InspectTokensWidget._ID;
    }
    _compute(position) {
        let data = this._getTokensAtLine(position.lineNumber);
        let token1Index = 0;
        for (let i = data.tokens1.length - 1; i >= 0; i--) {
            let t = data.tokens1[i];
            if (position.column - 1 >= t.offset) {
                token1Index = i;
                break;
            }
        }
        let token2Index = 0;
        for (let i = (data.tokens2.length >>> 1); i >= 0; i--) {
            if (position.column - 1 >= data.tokens2[(i << 1)]) {
                token2Index = i;
                break;
            }
        }
        let lineContent = this._model.getLineContent(position.lineNumber);
        let tokenText = '';
        if (token1Index < data.tokens1.length) {
            let tokenStartIndex = data.tokens1[token1Index].offset;
            let tokenEndIndex = token1Index + 1 < data.tokens1.length ? data.tokens1[token1Index + 1].offset : lineContent.length;
            tokenText = lineContent.substring(tokenStartIndex, tokenEndIndex);
        }
        reset(this._domNode, $('h2.tm-token', undefined, renderTokenText(tokenText), $('span.tm-token-length', undefined, `${tokenText.length} ${tokenText.length === 1 ? 'char' : 'chars'}`)));
        append(this._domNode, $('hr.tokens-inspect-separator', { 'style': 'clear:both' }));
        const metadata = (token2Index << 1) + 1 < data.tokens2.length ? this._decodeMetadata(data.tokens2[(token2Index << 1) + 1]) : null;
        append(this._domNode, $('table.tm-metadata-table', undefined, $('tbody', undefined, $('tr', undefined, $('td.tm-metadata-key', undefined, 'language'), $('td.tm-metadata-value', undefined, `${metadata ? metadata.languageIdentifier.language : '-?-'}`)), $('tr', undefined, $('td.tm-metadata-key', undefined, 'token type'), $('td.tm-metadata-value', undefined, `${metadata ? this._tokenTypeToString(metadata.tokenType) : '-?-'}`)), $('tr', undefined, $('td.tm-metadata-key', undefined, 'font style'), $('td.tm-metadata-value', undefined, `${metadata ? this._fontStyleToString(metadata.fontStyle) : '-?-'}`)), $('tr', undefined, $('td.tm-metadata-key', undefined, 'foreground'), $('td.tm-metadata-value', undefined, `${metadata ? Color.Format.CSS.formatHex(metadata.foreground) : '-?-'}`)), $('tr', undefined, $('td.tm-metadata-key', undefined, 'background'), $('td.tm-metadata-value', undefined, `${metadata ? Color.Format.CSS.formatHex(metadata.background) : '-?-'}`)))));
        append(this._domNode, $('hr.tokens-inspect-separator'));
        if (token1Index < data.tokens1.length) {
            append(this._domNode, $('span.tm-token-type', undefined, data.tokens1[token1Index].type));
        }
        this._editor.layoutContentWidget(this);
    }
    _decodeMetadata(metadata) {
        let colorMap = TokenizationRegistry.getColorMap();
        let languageId = TokenMetadata.getLanguageId(metadata);
        let tokenType = TokenMetadata.getTokenType(metadata);
        let fontStyle = TokenMetadata.getFontStyle(metadata);
        let foreground = TokenMetadata.getForeground(metadata);
        let background = TokenMetadata.getBackground(metadata);
        return {
            languageIdentifier: this._modeService.getLanguageIdentifier(languageId),
            tokenType: tokenType,
            fontStyle: fontStyle,
            foreground: colorMap[foreground],
            background: colorMap[background]
        };
    }
    _tokenTypeToString(tokenType) {
        switch (tokenType) {
            case 0 /* Other */: return 'Other';
            case 1 /* Comment */: return 'Comment';
            case 2 /* String */: return 'String';
            case 4 /* RegEx */: return 'RegEx';
            default: return '??';
        }
    }
    _fontStyleToString(fontStyle) {
        let r = '';
        if (fontStyle & 1 /* Italic */) {
            r += 'italic ';
        }
        if (fontStyle & 2 /* Bold */) {
            r += 'bold ';
        }
        if (fontStyle & 4 /* Underline */) {
            r += 'underline ';
        }
        if (r.length === 0) {
            r = '---';
        }
        return r;
    }
    _getTokensAtLine(lineNumber) {
        let stateBeforeLine = this._getStateBeforeLine(lineNumber);
        let tokenizationResult1 = this._tokenizationSupport.tokenize(this._model.getLineContent(lineNumber), true, stateBeforeLine, 0);
        let tokenizationResult2 = this._tokenizationSupport.tokenize2(this._model.getLineContent(lineNumber), true, stateBeforeLine, 0);
        return {
            startState: stateBeforeLine,
            tokens1: tokenizationResult1.tokens,
            tokens2: tokenizationResult2.tokens,
            endState: tokenizationResult1.endState
        };
    }
    _getStateBeforeLine(lineNumber) {
        let state = this._tokenizationSupport.getInitialState();
        for (let i = 1; i < lineNumber; i++) {
            let tokenizationResult = this._tokenizationSupport.tokenize(this._model.getLineContent(i), true, state, 0);
            state = tokenizationResult.endState;
        }
        return state;
    }
    getDomNode() {
        return this._domNode;
    }
    getPosition() {
        return {
            position: this._editor.getPosition(),
            preference: [2 /* BELOW */, 1 /* ABOVE */]
        };
    }
}
InspectTokensWidget._ID = 'editor.contrib.inspectTokensWidget';
registerEditorContribution(InspectTokensController.ID, InspectTokensController);
registerEditorAction(InspectTokens);
registerThemingParticipant((theme, collector) => {
    const border = theme.getColor(editorHoverBorder);
    if (border) {
        let borderWidth = theme.type === ColorScheme.HIGH_CONTRAST ? 2 : 1;
        collector.addRule(`.monaco-editor .tokens-inspect-widget { border: ${borderWidth}px solid ${border}; }`);
        collector.addRule(`.monaco-editor .tokens-inspect-widget .tokens-inspect-separator { background-color: ${border}; }`);
    }
    const background = theme.getColor(editorHoverBackground);
    if (background) {
        collector.addRule(`.monaco-editor .tokens-inspect-widget { background-color: ${background}; }`);
    }
    const foreground = theme.getColor(editorHoverForeground);
    if (foreground) {
        collector.addRule(`.monaco-editor .tokens-inspect-widget { color: ${foreground}; }`);
    }
});
