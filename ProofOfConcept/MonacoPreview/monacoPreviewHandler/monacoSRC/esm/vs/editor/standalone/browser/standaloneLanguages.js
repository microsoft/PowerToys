/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Color } from '../../../base/common/color.js';
import { Range } from '../../common/core/range.js';
import { Token, TokenizationResult, TokenizationResult2 } from '../../common/core/token.js';
import * as modes from '../../common/modes.js';
import { LanguageConfigurationRegistry } from '../../common/modes/languageConfigurationRegistry.js';
import { ModesRegistry } from '../../common/modes/modesRegistry.js';
import * as standaloneEnums from '../../common/standalone/standaloneEnums.js';
import { StaticServices } from './standaloneServices.js';
import { compile } from '../common/monarch/monarchCompile.js';
import { createTokenizationSupport } from '../common/monarch/monarchLexer.js';
/**
 * Register information about a new language.
 */
export function register(language) {
    ModesRegistry.registerLanguage(language);
}
/**
 * Get the information of all the registered languages.
 */
export function getLanguages() {
    let result = [];
    result = result.concat(ModesRegistry.getLanguages());
    return result;
}
export function getEncodedLanguageId(languageId) {
    let lid = StaticServices.modeService.get().getLanguageIdentifier(languageId);
    return lid ? lid.id : 0;
}
/**
 * An event emitted when a language is first time needed (e.g. a model has it set).
 * @event
 */
export function onLanguage(languageId, callback) {
    let disposable = StaticServices.modeService.get().onDidCreateMode((mode) => {
        if (mode.getId() === languageId) {
            // stop listening
            disposable.dispose();
            // invoke actual listener
            callback();
        }
    });
    return disposable;
}
/**
 * Set the editing configuration for a language.
 */
export function setLanguageConfiguration(languageId, configuration) {
    let languageIdentifier = StaticServices.modeService.get().getLanguageIdentifier(languageId);
    if (!languageIdentifier) {
        throw new Error(`Cannot set configuration for unknown language ${languageId}`);
    }
    return LanguageConfigurationRegistry.register(languageIdentifier, configuration);
}
/**
 * @internal
 */
export class EncodedTokenizationSupport2Adapter {
    constructor(languageIdentifier, actual) {
        this._languageIdentifier = languageIdentifier;
        this._actual = actual;
    }
    getInitialState() {
        return this._actual.getInitialState();
    }
    tokenize(line, hasEOL, state, offsetDelta) {
        if (typeof this._actual.tokenize === 'function') {
            return TokenizationSupport2Adapter.adaptTokenize(this._languageIdentifier.language, this._actual, line, state, offsetDelta);
        }
        throw new Error('Not supported!');
    }
    tokenize2(line, hasEOL, state) {
        let result = this._actual.tokenizeEncoded(line, state);
        return new TokenizationResult2(result.tokens, result.endState);
    }
}
/**
 * @internal
 */
export class TokenizationSupport2Adapter {
    constructor(standaloneThemeService, languageIdentifier, actual) {
        this._standaloneThemeService = standaloneThemeService;
        this._languageIdentifier = languageIdentifier;
        this._actual = actual;
    }
    getInitialState() {
        return this._actual.getInitialState();
    }
    static _toClassicTokens(tokens, language, offsetDelta) {
        let result = [];
        let previousStartIndex = 0;
        for (let i = 0, len = tokens.length; i < len; i++) {
            const t = tokens[i];
            let startIndex = t.startIndex;
            // Prevent issues stemming from a buggy external tokenizer.
            if (i === 0) {
                // Force first token to start at first index!
                startIndex = 0;
            }
            else if (startIndex < previousStartIndex) {
                // Force tokens to be after one another!
                startIndex = previousStartIndex;
            }
            result[i] = new Token(startIndex + offsetDelta, t.scopes, language);
            previousStartIndex = startIndex;
        }
        return result;
    }
    static adaptTokenize(language, actual, line, state, offsetDelta) {
        let actualResult = actual.tokenize(line, state);
        let tokens = TokenizationSupport2Adapter._toClassicTokens(actualResult.tokens, language, offsetDelta);
        let endState;
        // try to save an object if possible
        if (actualResult.endState.equals(state)) {
            endState = state;
        }
        else {
            endState = actualResult.endState;
        }
        return new TokenizationResult(tokens, endState);
    }
    tokenize(line, hasEOL, state, offsetDelta) {
        return TokenizationSupport2Adapter.adaptTokenize(this._languageIdentifier.language, this._actual, line, state, offsetDelta);
    }
    _toBinaryTokens(tokens, offsetDelta) {
        const languageId = this._languageIdentifier.id;
        const tokenTheme = this._standaloneThemeService.getColorTheme().tokenTheme;
        let result = [], resultLen = 0;
        let previousStartIndex = 0;
        for (let i = 0, len = tokens.length; i < len; i++) {
            const t = tokens[i];
            const metadata = tokenTheme.match(languageId, t.scopes);
            if (resultLen > 0 && result[resultLen - 1] === metadata) {
                // same metadata
                continue;
            }
            let startIndex = t.startIndex;
            // Prevent issues stemming from a buggy external tokenizer.
            if (i === 0) {
                // Force first token to start at first index!
                startIndex = 0;
            }
            else if (startIndex < previousStartIndex) {
                // Force tokens to be after one another!
                startIndex = previousStartIndex;
            }
            result[resultLen++] = startIndex + offsetDelta;
            result[resultLen++] = metadata;
            previousStartIndex = startIndex;
        }
        let actualResult = new Uint32Array(resultLen);
        for (let i = 0; i < resultLen; i++) {
            actualResult[i] = result[i];
        }
        return actualResult;
    }
    tokenize2(line, hasEOL, state, offsetDelta) {
        let actualResult = this._actual.tokenize(line, state);
        let tokens = this._toBinaryTokens(actualResult.tokens, offsetDelta);
        let endState;
        // try to save an object if possible
        if (actualResult.endState.equals(state)) {
            endState = state;
        }
        else {
            endState = actualResult.endState;
        }
        return new TokenizationResult2(tokens, endState);
    }
}
function isEncodedTokensProvider(provider) {
    return 'tokenizeEncoded' in provider;
}
function isThenable(obj) {
    return obj && typeof obj.then === 'function';
}
/**
 * Change the color map that is used for token colors.
 * Supported formats (hex): #RRGGBB, $RRGGBBAA, #RGB, #RGBA
 */
export function setColorMap(colorMap) {
    if (colorMap) {
        const result = [null];
        for (let i = 1, len = colorMap.length; i < len; i++) {
            result[i] = Color.fromHex(colorMap[i]);
        }
        StaticServices.standaloneThemeService.get().setColorMapOverride(result);
    }
    else {
        StaticServices.standaloneThemeService.get().setColorMapOverride(null);
    }
}
/**
 * Set the tokens provider for a language (manual implementation).
 */
export function setTokensProvider(languageId, provider) {
    let languageIdentifier = StaticServices.modeService.get().getLanguageIdentifier(languageId);
    if (!languageIdentifier) {
        throw new Error(`Cannot set tokens provider for unknown language ${languageId}`);
    }
    const create = (provider) => {
        if (isEncodedTokensProvider(provider)) {
            return new EncodedTokenizationSupport2Adapter(languageIdentifier, provider);
        }
        else {
            return new TokenizationSupport2Adapter(StaticServices.standaloneThemeService.get(), languageIdentifier, provider);
        }
    };
    if (isThenable(provider)) {
        return modes.TokenizationRegistry.registerPromise(languageId, provider.then(provider => create(provider)));
    }
    return modes.TokenizationRegistry.register(languageId, create(provider));
}
/**
 * Set the tokens provider for a language (monarch implementation).
 */
export function setMonarchTokensProvider(languageId, languageDef) {
    const create = (languageDef) => {
        return createTokenizationSupport(StaticServices.modeService.get(), StaticServices.standaloneThemeService.get(), languageId, compile(languageId, languageDef));
    };
    if (isThenable(languageDef)) {
        return modes.TokenizationRegistry.registerPromise(languageId, languageDef.then(languageDef => create(languageDef)));
    }
    return modes.TokenizationRegistry.register(languageId, create(languageDef));
}
/**
 * Register a reference provider (used by e.g. reference search).
 */
export function registerReferenceProvider(languageId, provider) {
    return modes.ReferenceProviderRegistry.register(languageId, provider);
}
/**
 * Register a rename provider (used by e.g. rename symbol).
 */
export function registerRenameProvider(languageId, provider) {
    return modes.RenameProviderRegistry.register(languageId, provider);
}
/**
 * Register a signature help provider (used by e.g. parameter hints).
 */
export function registerSignatureHelpProvider(languageId, provider) {
    return modes.SignatureHelpProviderRegistry.register(languageId, provider);
}
/**
 * Register a hover provider (used by e.g. editor hover).
 */
export function registerHoverProvider(languageId, provider) {
    return modes.HoverProviderRegistry.register(languageId, {
        provideHover: (model, position, token) => {
            let word = model.getWordAtPosition(position);
            return Promise.resolve(provider.provideHover(model, position, token)).then((value) => {
                if (!value) {
                    return undefined;
                }
                if (!value.range && word) {
                    value.range = new Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn);
                }
                if (!value.range) {
                    value.range = new Range(position.lineNumber, position.column, position.lineNumber, position.column);
                }
                return value;
            });
        }
    });
}
/**
 * Register a document symbol provider (used by e.g. outline).
 */
export function registerDocumentSymbolProvider(languageId, provider) {
    return modes.DocumentSymbolProviderRegistry.register(languageId, provider);
}
/**
 * Register a document highlight provider (used by e.g. highlight occurrences).
 */
export function registerDocumentHighlightProvider(languageId, provider) {
    return modes.DocumentHighlightProviderRegistry.register(languageId, provider);
}
/**
 * Register an linked editing range provider.
 */
export function registerLinkedEditingRangeProvider(languageId, provider) {
    return modes.LinkedEditingRangeProviderRegistry.register(languageId, provider);
}
/**
 * Register a definition provider (used by e.g. go to definition).
 */
export function registerDefinitionProvider(languageId, provider) {
    return modes.DefinitionProviderRegistry.register(languageId, provider);
}
/**
 * Register a implementation provider (used by e.g. go to implementation).
 */
export function registerImplementationProvider(languageId, provider) {
    return modes.ImplementationProviderRegistry.register(languageId, provider);
}
/**
 * Register a type definition provider (used by e.g. go to type definition).
 */
export function registerTypeDefinitionProvider(languageId, provider) {
    return modes.TypeDefinitionProviderRegistry.register(languageId, provider);
}
/**
 * Register a code lens provider (used by e.g. inline code lenses).
 */
export function registerCodeLensProvider(languageId, provider) {
    return modes.CodeLensProviderRegistry.register(languageId, provider);
}
/**
 * Register a code action provider (used by e.g. quick fix).
 */
export function registerCodeActionProvider(languageId, provider) {
    return modes.CodeActionProviderRegistry.register(languageId, {
        provideCodeActions: (model, range, context, token) => {
            let markers = StaticServices.markerService.get().read({ resource: model.uri }).filter(m => {
                return Range.areIntersectingOrTouching(m, range);
            });
            return provider.provideCodeActions(model, range, { markers, only: context.only }, token);
        }
    });
}
/**
 * Register a formatter that can handle only entire models.
 */
export function registerDocumentFormattingEditProvider(languageId, provider) {
    return modes.DocumentFormattingEditProviderRegistry.register(languageId, provider);
}
/**
 * Register a formatter that can handle a range inside a model.
 */
export function registerDocumentRangeFormattingEditProvider(languageId, provider) {
    return modes.DocumentRangeFormattingEditProviderRegistry.register(languageId, provider);
}
/**
 * Register a formatter than can do formatting as the user types.
 */
export function registerOnTypeFormattingEditProvider(languageId, provider) {
    return modes.OnTypeFormattingEditProviderRegistry.register(languageId, provider);
}
/**
 * Register a link provider that can find links in text.
 */
export function registerLinkProvider(languageId, provider) {
    return modes.LinkProviderRegistry.register(languageId, provider);
}
/**
 * Register a completion item provider (use by e.g. suggestions).
 */
export function registerCompletionItemProvider(languageId, provider) {
    return modes.CompletionProviderRegistry.register(languageId, provider);
}
/**
 * Register a document color provider (used by Color Picker, Color Decorator).
 */
export function registerColorProvider(languageId, provider) {
    return modes.ColorProviderRegistry.register(languageId, provider);
}
/**
 * Register a folding range provider
 */
export function registerFoldingRangeProvider(languageId, provider) {
    return modes.FoldingRangeProviderRegistry.register(languageId, provider);
}
/**
 * Register a declaration provider
 */
export function registerDeclarationProvider(languageId, provider) {
    return modes.DeclarationProviderRegistry.register(languageId, provider);
}
/**
 * Register a selection range provider
 */
export function registerSelectionRangeProvider(languageId, provider) {
    return modes.SelectionRangeRegistry.register(languageId, provider);
}
/**
 * Register a document semantic tokens provider
 */
export function registerDocumentSemanticTokensProvider(languageId, provider) {
    return modes.DocumentSemanticTokensProviderRegistry.register(languageId, provider);
}
/**
 * Register a document range semantic tokens provider
 */
export function registerDocumentRangeSemanticTokensProvider(languageId, provider) {
    return modes.DocumentRangeSemanticTokensProviderRegistry.register(languageId, provider);
}
/**
 * @internal
 */
export function createMonacoLanguagesAPI() {
    return {
        register: register,
        getLanguages: getLanguages,
        onLanguage: onLanguage,
        getEncodedLanguageId: getEncodedLanguageId,
        // provider methods
        setLanguageConfiguration: setLanguageConfiguration,
        setColorMap: setColorMap,
        setTokensProvider: setTokensProvider,
        setMonarchTokensProvider: setMonarchTokensProvider,
        registerReferenceProvider: registerReferenceProvider,
        registerRenameProvider: registerRenameProvider,
        registerCompletionItemProvider: registerCompletionItemProvider,
        registerSignatureHelpProvider: registerSignatureHelpProvider,
        registerHoverProvider: registerHoverProvider,
        registerDocumentSymbolProvider: registerDocumentSymbolProvider,
        registerDocumentHighlightProvider: registerDocumentHighlightProvider,
        registerLinkedEditingRangeProvider: registerLinkedEditingRangeProvider,
        registerDefinitionProvider: registerDefinitionProvider,
        registerImplementationProvider: registerImplementationProvider,
        registerTypeDefinitionProvider: registerTypeDefinitionProvider,
        registerCodeLensProvider: registerCodeLensProvider,
        registerCodeActionProvider: registerCodeActionProvider,
        registerDocumentFormattingEditProvider: registerDocumentFormattingEditProvider,
        registerDocumentRangeFormattingEditProvider: registerDocumentRangeFormattingEditProvider,
        registerOnTypeFormattingEditProvider: registerOnTypeFormattingEditProvider,
        registerLinkProvider: registerLinkProvider,
        registerColorProvider: registerColorProvider,
        registerFoldingRangeProvider: registerFoldingRangeProvider,
        registerDeclarationProvider: registerDeclarationProvider,
        registerSelectionRangeProvider: registerSelectionRangeProvider,
        registerDocumentSemanticTokensProvider: registerDocumentSemanticTokensProvider,
        registerDocumentRangeSemanticTokensProvider: registerDocumentRangeSemanticTokensProvider,
        // enums
        DocumentHighlightKind: standaloneEnums.DocumentHighlightKind,
        CompletionItemKind: standaloneEnums.CompletionItemKind,
        CompletionItemTag: standaloneEnums.CompletionItemTag,
        CompletionItemInsertTextRule: standaloneEnums.CompletionItemInsertTextRule,
        SymbolKind: standaloneEnums.SymbolKind,
        SymbolTag: standaloneEnums.SymbolTag,
        IndentAction: standaloneEnums.IndentAction,
        CompletionTriggerKind: standaloneEnums.CompletionTriggerKind,
        SignatureHelpTriggerKind: standaloneEnums.SignatureHelpTriggerKind,
        // classes
        FoldingRangeKind: modes.FoldingRangeKind,
    };
}
