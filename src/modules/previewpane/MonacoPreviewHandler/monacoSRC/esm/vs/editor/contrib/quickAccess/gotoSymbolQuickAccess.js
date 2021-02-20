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
import { localize } from '../../../nls.js';
import { CancellationTokenSource } from '../../../base/common/cancellation.js';
import { DisposableStore, Disposable, toDisposable } from '../../../base/common/lifecycle.js';
import { Range } from '../../common/core/range.js';
import { AbstractEditorNavigationQuickAccessProvider } from './editorNavigationQuickAccess.js';
import { SymbolKinds, DocumentSymbolProviderRegistry } from '../../common/modes.js';
import { OutlineModel } from '../documentSymbols/outlineModel.js';
import { trim, format } from '../../../base/common/strings.js';
import { prepareQuery, pieceToQuery, scoreFuzzy2 } from '../../../base/common/fuzzyScorer.js';
import { Codicon } from '../../../base/common/codicons.js';
export class AbstractGotoSymbolQuickAccessProvider extends AbstractEditorNavigationQuickAccessProvider {
    constructor(options = Object.create(null)) {
        super(options);
        this.options = options;
        options.canAcceptInBackground = true;
    }
    provideWithoutTextEditor(picker) {
        this.provideLabelPick(picker, localize('cannotRunGotoSymbolWithoutEditor', "To go to a symbol, first open a text editor with symbol information."));
        return Disposable.None;
    }
    provideWithTextEditor(context, picker, token) {
        const editor = context.editor;
        const model = this.getModel(editor);
        if (!model) {
            return Disposable.None;
        }
        // Provide symbols from model if available in registry
        if (DocumentSymbolProviderRegistry.has(model)) {
            return this.doProvideWithEditorSymbols(context, model, picker, token);
        }
        // Otherwise show an entry for a model without registry
        // But give a chance to resolve the symbols at a later
        // point if possible
        return this.doProvideWithoutEditorSymbols(context, model, picker, token);
    }
    doProvideWithoutEditorSymbols(context, model, picker, token) {
        const disposables = new DisposableStore();
        // Generic pick for not having any symbol information
        this.provideLabelPick(picker, localize('cannotRunGotoSymbolWithoutSymbolProvider', "The active text editor does not provide symbol information."));
        // Wait for changes to the registry and see if eventually
        // we do get symbols. This can happen if the picker is opened
        // very early after the model has loaded but before the
        // language registry is ready.
        // https://github.com/microsoft/vscode/issues/70607
        (() => __awaiter(this, void 0, void 0, function* () {
            const result = yield this.waitForLanguageSymbolRegistry(model, disposables);
            if (!result || token.isCancellationRequested) {
                return;
            }
            disposables.add(this.doProvideWithEditorSymbols(context, model, picker, token));
        }))();
        return disposables;
    }
    provideLabelPick(picker, label) {
        picker.items = [{ label, index: 0, kind: 14 /* String */ }];
        picker.ariaLabel = label;
    }
    waitForLanguageSymbolRegistry(model, disposables) {
        return __awaiter(this, void 0, void 0, function* () {
            if (DocumentSymbolProviderRegistry.has(model)) {
                return true;
            }
            let symbolProviderRegistryPromiseResolve;
            const symbolProviderRegistryPromise = new Promise(resolve => symbolProviderRegistryPromiseResolve = resolve);
            // Resolve promise when registry knows model
            const symbolProviderListener = disposables.add(DocumentSymbolProviderRegistry.onDidChange(() => {
                if (DocumentSymbolProviderRegistry.has(model)) {
                    symbolProviderListener.dispose();
                    symbolProviderRegistryPromiseResolve(true);
                }
            }));
            // Resolve promise when we get disposed too
            disposables.add(toDisposable(() => symbolProviderRegistryPromiseResolve(false)));
            return symbolProviderRegistryPromise;
        });
    }
    doProvideWithEditorSymbols(context, model, picker, token) {
        const editor = context.editor;
        const disposables = new DisposableStore();
        // Goto symbol once picked
        disposables.add(picker.onDidAccept(event => {
            const [item] = picker.selectedItems;
            if (item && item.range) {
                this.gotoLocation(context, { range: item.range.selection, keyMods: picker.keyMods, preserveFocus: event.inBackground });
                if (!event.inBackground) {
                    picker.hide();
                }
            }
        }));
        // Goto symbol side by side if enabled
        disposables.add(picker.onDidTriggerItemButton(({ item }) => {
            if (item && item.range) {
                this.gotoLocation(context, { range: item.range.selection, keyMods: picker.keyMods, forceSideBySide: true });
                picker.hide();
            }
        }));
        // Resolve symbols from document once and reuse this
        // request for all filtering and typing then on
        const symbolsPromise = this.getDocumentSymbols(model, token);
        // Set initial picks and update on type
        let picksCts = undefined;
        const updatePickerItems = () => __awaiter(this, void 0, void 0, function* () {
            // Cancel any previous ask for picks and busy
            picksCts === null || picksCts === void 0 ? void 0 : picksCts.dispose(true);
            picker.busy = false;
            // Create new cancellation source for this run
            picksCts = new CancellationTokenSource(token);
            // Collect symbol picks
            picker.busy = true;
            try {
                const query = prepareQuery(picker.value.substr(AbstractGotoSymbolQuickAccessProvider.PREFIX.length).trim());
                const items = yield this.doGetSymbolPicks(symbolsPromise, query, undefined, picksCts.token);
                if (token.isCancellationRequested) {
                    return;
                }
                if (items.length > 0) {
                    picker.items = items;
                }
                else {
                    if (query.original.length > 0) {
                        this.provideLabelPick(picker, localize('noMatchingSymbolResults', "No matching editor symbols"));
                    }
                    else {
                        this.provideLabelPick(picker, localize('noSymbolResults', "No editor symbols"));
                    }
                }
            }
            finally {
                if (!token.isCancellationRequested) {
                    picker.busy = false;
                }
            }
        });
        disposables.add(picker.onDidChangeValue(() => updatePickerItems()));
        updatePickerItems();
        // Reveal and decorate when active item changes
        // However, ignore the very first event so that
        // opening the picker is not immediately revealing
        // and decorating the first entry.
        let ignoreFirstActiveEvent = true;
        disposables.add(picker.onDidChangeActive(() => {
            const [item] = picker.activeItems;
            if (item && item.range) {
                if (ignoreFirstActiveEvent) {
                    ignoreFirstActiveEvent = false;
                    return;
                }
                // Reveal
                editor.revealRangeInCenter(item.range.selection, 0 /* Smooth */);
                // Decorate
                this.addDecorations(editor, item.range.decoration);
            }
        }));
        return disposables;
    }
    doGetSymbolPicks(symbolsPromise, query, options, token) {
        return __awaiter(this, void 0, void 0, function* () {
            const symbols = yield symbolsPromise;
            if (token.isCancellationRequested) {
                return [];
            }
            const filterBySymbolKind = query.original.indexOf(AbstractGotoSymbolQuickAccessProvider.SCOPE_PREFIX) === 0;
            const filterPos = filterBySymbolKind ? 1 : 0;
            // Split between symbol and container query
            let symbolQuery;
            let containerQuery;
            if (query.values && query.values.length > 1) {
                symbolQuery = pieceToQuery(query.values[0]); // symbol: only match on first part
                containerQuery = pieceToQuery(query.values.slice(1)); // container: match on all but first parts
            }
            else {
                symbolQuery = query;
            }
            // Convert to symbol picks and apply filtering
            const filteredSymbolPicks = [];
            for (let index = 0; index < symbols.length; index++) {
                const symbol = symbols[index];
                const symbolLabel = trim(symbol.name);
                const symbolLabelWithIcon = `$(symbol-${SymbolKinds.toString(symbol.kind) || 'property'}) ${symbolLabel}`;
                const symbolLabelIconOffset = symbolLabelWithIcon.length - symbolLabel.length;
                let containerLabel = symbol.containerName;
                if (options === null || options === void 0 ? void 0 : options.extraContainerLabel) {
                    if (containerLabel) {
                        containerLabel = `${options.extraContainerLabel} • ${containerLabel}`;
                    }
                    else {
                        containerLabel = options.extraContainerLabel;
                    }
                }
                let symbolScore = undefined;
                let symbolMatches = undefined;
                let containerScore = undefined;
                let containerMatches = undefined;
                if (query.original.length > filterPos) {
                    // First: try to score on the entire query, it is possible that
                    // the symbol matches perfectly (e.g. searching for "change log"
                    // can be a match on a markdown symbol "change log"). In that
                    // case we want to skip the container query altogether.
                    let skipContainerQuery = false;
                    if (symbolQuery !== query) {
                        [symbolScore, symbolMatches] = scoreFuzzy2(symbolLabelWithIcon, Object.assign(Object.assign({}, query), { values: undefined /* disable multi-query support */ }), filterPos, symbolLabelIconOffset);
                        if (typeof symbolScore === 'number') {
                            skipContainerQuery = true; // since we consumed the query, skip any container matching
                        }
                    }
                    // Otherwise: score on the symbol query and match on the container later
                    if (typeof symbolScore !== 'number') {
                        [symbolScore, symbolMatches] = scoreFuzzy2(symbolLabelWithIcon, symbolQuery, filterPos, symbolLabelIconOffset);
                        if (typeof symbolScore !== 'number') {
                            continue;
                        }
                    }
                    // Score by container if specified
                    if (!skipContainerQuery && containerQuery) {
                        if (containerLabel && containerQuery.original.length > 0) {
                            [containerScore, containerMatches] = scoreFuzzy2(containerLabel, containerQuery);
                        }
                        if (typeof containerScore !== 'number') {
                            continue;
                        }
                        if (typeof symbolScore === 'number') {
                            symbolScore += containerScore; // boost symbolScore by containerScore
                        }
                    }
                }
                const deprecated = symbol.tags && symbol.tags.indexOf(1 /* Deprecated */) >= 0;
                filteredSymbolPicks.push({
                    index,
                    kind: symbol.kind,
                    score: symbolScore,
                    label: symbolLabelWithIcon,
                    ariaLabel: symbolLabel,
                    description: containerLabel,
                    highlights: deprecated ? undefined : {
                        label: symbolMatches,
                        description: containerMatches
                    },
                    range: {
                        selection: Range.collapseToStart(symbol.selectionRange),
                        decoration: symbol.range
                    },
                    strikethrough: deprecated,
                    buttons: (() => {
                        var _a, _b;
                        const openSideBySideDirection = ((_a = this.options) === null || _a === void 0 ? void 0 : _a.openSideBySideDirection) ? (_b = this.options) === null || _b === void 0 ? void 0 : _b.openSideBySideDirection() : undefined;
                        if (!openSideBySideDirection) {
                            return undefined;
                        }
                        return [
                            {
                                iconClass: openSideBySideDirection === 'right' ? Codicon.splitHorizontal.classNames : Codicon.splitVertical.classNames,
                                tooltip: openSideBySideDirection === 'right' ? localize('openToSide', "Open to the Side") : localize('openToBottom', "Open to the Bottom")
                            }
                        ];
                    })()
                });
            }
            // Sort by score
            const sortedFilteredSymbolPicks = filteredSymbolPicks.sort((symbolA, symbolB) => filterBySymbolKind ?
                this.compareByKindAndScore(symbolA, symbolB) :
                this.compareByScore(symbolA, symbolB));
            // Add separator for types
            // - @  only total number of symbols
            // - @: grouped by symbol kind
            let symbolPicks = [];
            if (filterBySymbolKind) {
                let lastSymbolKind = undefined;
                let lastSeparator = undefined;
                let lastSymbolKindCounter = 0;
                function updateLastSeparatorLabel() {
                    if (lastSeparator && typeof lastSymbolKind === 'number' && lastSymbolKindCounter > 0) {
                        lastSeparator.label = format(NLS_SYMBOL_KIND_CACHE[lastSymbolKind] || FALLBACK_NLS_SYMBOL_KIND, lastSymbolKindCounter);
                    }
                }
                for (const symbolPick of sortedFilteredSymbolPicks) {
                    // Found new kind
                    if (lastSymbolKind !== symbolPick.kind) {
                        // Update last separator with number of symbols we found for kind
                        updateLastSeparatorLabel();
                        lastSymbolKind = symbolPick.kind;
                        lastSymbolKindCounter = 1;
                        // Add new separator for new kind
                        lastSeparator = { type: 'separator' };
                        symbolPicks.push(lastSeparator);
                    }
                    // Existing kind, keep counting
                    else {
                        lastSymbolKindCounter++;
                    }
                    // Add to final result
                    symbolPicks.push(symbolPick);
                }
                // Update last separator with number of symbols we found for kind
                updateLastSeparatorLabel();
            }
            else if (sortedFilteredSymbolPicks.length > 0) {
                symbolPicks = [
                    { label: localize('symbols', "symbols ({0})", filteredSymbolPicks.length), type: 'separator' },
                    ...sortedFilteredSymbolPicks
                ];
            }
            return symbolPicks;
        });
    }
    compareByScore(symbolA, symbolB) {
        if (typeof symbolA.score !== 'number' && typeof symbolB.score === 'number') {
            return 1;
        }
        else if (typeof symbolA.score === 'number' && typeof symbolB.score !== 'number') {
            return -1;
        }
        if (typeof symbolA.score === 'number' && typeof symbolB.score === 'number') {
            if (symbolA.score > symbolB.score) {
                return -1;
            }
            else if (symbolA.score < symbolB.score) {
                return 1;
            }
        }
        if (symbolA.index < symbolB.index) {
            return -1;
        }
        else if (symbolA.index > symbolB.index) {
            return 1;
        }
        return 0;
    }
    compareByKindAndScore(symbolA, symbolB) {
        const kindA = NLS_SYMBOL_KIND_CACHE[symbolA.kind] || FALLBACK_NLS_SYMBOL_KIND;
        const kindB = NLS_SYMBOL_KIND_CACHE[symbolB.kind] || FALLBACK_NLS_SYMBOL_KIND;
        // Sort by type first if scoped search
        const result = kindA.localeCompare(kindB);
        if (result === 0) {
            return this.compareByScore(symbolA, symbolB);
        }
        return result;
    }
    getDocumentSymbols(document, token) {
        return __awaiter(this, void 0, void 0, function* () {
            const model = yield OutlineModel.create(document, token);
            return token.isCancellationRequested ? [] : model.asListOfDocumentSymbols();
        });
    }
}
AbstractGotoSymbolQuickAccessProvider.PREFIX = '@';
AbstractGotoSymbolQuickAccessProvider.SCOPE_PREFIX = ':';
AbstractGotoSymbolQuickAccessProvider.PREFIX_BY_CATEGORY = `${AbstractGotoSymbolQuickAccessProvider.PREFIX}${AbstractGotoSymbolQuickAccessProvider.SCOPE_PREFIX}`;
// #region NLS Helpers
const FALLBACK_NLS_SYMBOL_KIND = localize('property', "properties ({0})");
const NLS_SYMBOL_KIND_CACHE = {
    [5 /* Method */]: localize('method', "methods ({0})"),
    [11 /* Function */]: localize('function', "functions ({0})"),
    [8 /* Constructor */]: localize('_constructor', "constructors ({0})"),
    [12 /* Variable */]: localize('variable', "variables ({0})"),
    [4 /* Class */]: localize('class', "classes ({0})"),
    [22 /* Struct */]: localize('struct', "structs ({0})"),
    [23 /* Event */]: localize('event', "events ({0})"),
    [24 /* Operator */]: localize('operator', "operators ({0})"),
    [10 /* Interface */]: localize('interface', "interfaces ({0})"),
    [2 /* Namespace */]: localize('namespace', "namespaces ({0})"),
    [3 /* Package */]: localize('package', "packages ({0})"),
    [25 /* TypeParameter */]: localize('typeParameter', "type parameters ({0})"),
    [1 /* Module */]: localize('modules', "modules ({0})"),
    [6 /* Property */]: localize('property', "properties ({0})"),
    [9 /* Enum */]: localize('enum', "enumerations ({0})"),
    [21 /* EnumMember */]: localize('enumMember', "enumeration members ({0})"),
    [14 /* String */]: localize('string', "strings ({0})"),
    [0 /* File */]: localize('file', "files ({0})"),
    [17 /* Array */]: localize('array', "arrays ({0})"),
    [15 /* Number */]: localize('number', "numbers ({0})"),
    [16 /* Boolean */]: localize('boolean', "booleans ({0})"),
    [18 /* Object */]: localize('object', "objects ({0})"),
    [19 /* Key */]: localize('key', "keys ({0})"),
    [7 /* Field */]: localize('field', "fields ({0})"),
    [13 /* Constant */]: localize('constant', "constants ({0})")
};
//#endregion
