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
import { onUnexpectedExternalError, canceled, isPromiseCanceledError } from '../../../base/common/errors.js';
import * as modes from '../../common/modes.js';
import { Position } from '../../common/core/position.js';
import { RawContextKey } from '../../../platform/contextkey/common/contextkey.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { Range } from '../../common/core/range.js';
import { FuzzyScore } from '../../../base/common/filters.js';
import { isDisposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { MenuId } from '../../../platform/actions/common/actions.js';
import { SnippetParser } from '../snippet/snippetParser.js';
import { StopWatch } from '../../../base/common/stopwatch.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
import { assertType } from '../../../base/common/types.js';
import { URI } from '../../../base/common/uri.js';
import { ITextModelService } from '../../common/services/resolverService.js';
export const Context = {
    Visible: new RawContextKey('suggestWidgetVisible', false),
    DetailsVisible: new RawContextKey('suggestWidgetDetailsVisible', false),
    MultipleSuggestions: new RawContextKey('suggestWidgetMultipleSuggestions', false),
    MakesTextEdit: new RawContextKey('suggestionMakesTextEdit', true),
    AcceptSuggestionsOnEnter: new RawContextKey('acceptSuggestionOnEnter', true),
    HasInsertAndReplaceRange: new RawContextKey('suggestionHasInsertAndReplaceRange', false),
    InsertMode: new RawContextKey('suggestionInsertMode', undefined),
    CanResolve: new RawContextKey('suggestionCanResolve', false),
};
export const suggestWidgetStatusbarMenu = new MenuId('suggestWidgetStatusBar');
export class CompletionItem {
    constructor(position, completion, container, provider) {
        this.position = position;
        this.completion = completion;
        this.container = container;
        this.provider = provider;
        // validation
        this.isInvalid = false;
        // sorting, filtering
        this.score = FuzzyScore.Default;
        this.distance = 0;
        this.textLabel = typeof completion.label === 'string'
            ? completion.label
            : completion.label.name;
        // ensure lower-variants (perf)
        this.labelLow = this.textLabel.toLowerCase();
        // validate label
        this.isInvalid = !this.textLabel;
        this.sortTextLow = completion.sortText && completion.sortText.toLowerCase();
        this.filterTextLow = completion.filterText && completion.filterText.toLowerCase();
        // normalize ranges
        if (Range.isIRange(completion.range)) {
            this.editStart = new Position(completion.range.startLineNumber, completion.range.startColumn);
            this.editInsertEnd = new Position(completion.range.endLineNumber, completion.range.endColumn);
            this.editReplaceEnd = new Position(completion.range.endLineNumber, completion.range.endColumn);
            // validate range
            this.isInvalid = this.isInvalid
                || Range.spansMultipleLines(completion.range) || completion.range.startLineNumber !== position.lineNumber;
        }
        else {
            this.editStart = new Position(completion.range.insert.startLineNumber, completion.range.insert.startColumn);
            this.editInsertEnd = new Position(completion.range.insert.endLineNumber, completion.range.insert.endColumn);
            this.editReplaceEnd = new Position(completion.range.replace.endLineNumber, completion.range.replace.endColumn);
            // validate ranges
            this.isInvalid = this.isInvalid
                || Range.spansMultipleLines(completion.range.insert) || Range.spansMultipleLines(completion.range.replace)
                || completion.range.insert.startLineNumber !== position.lineNumber || completion.range.replace.startLineNumber !== position.lineNumber
                || completion.range.insert.startColumn !== completion.range.replace.startColumn;
        }
        // create the suggestion resolver
        if (typeof provider.resolveCompletionItem !== 'function') {
            this._resolveCache = Promise.resolve();
            this._isResolved = true;
        }
    }
    // ---- resolving
    get isResolved() {
        return !!this._isResolved;
    }
    resolve(token) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this._resolveCache) {
                const sub = token.onCancellationRequested(() => {
                    this._resolveCache = undefined;
                    this._isResolved = false;
                });
                this._resolveCache = Promise.resolve(this.provider.resolveCompletionItem(this.completion, token)).then(value => {
                    Object.assign(this.completion, value);
                    this._isResolved = true;
                    sub.dispose();
                }, err => {
                    if (isPromiseCanceledError(err)) {
                        // the IPC queue will reject the request with the
                        // cancellation error -> reset cached
                        this._resolveCache = undefined;
                        this._isResolved = false;
                    }
                });
            }
            return this._resolveCache;
        });
    }
}
export class CompletionOptions {
    constructor(snippetSortOrder = 2 /* Bottom */, kindFilter = new Set(), providerFilter = new Set()) {
        this.snippetSortOrder = snippetSortOrder;
        this.kindFilter = kindFilter;
        this.providerFilter = providerFilter;
    }
}
CompletionOptions.default = new CompletionOptions();
let _snippetSuggestSupport;
export function getSnippetSuggestSupport() {
    return _snippetSuggestSupport;
}
export class CompletionItemModel {
    constructor(items, needsClipboard, durations, disposable) {
        this.items = items;
        this.needsClipboard = needsClipboard;
        this.durations = durations;
        this.disposable = disposable;
    }
}
export function provideSuggestionItems(model, position, options = CompletionOptions.default, context = { triggerKind: 0 /* Invoke */ }, token = CancellationToken.None) {
    return __awaiter(this, void 0, void 0, function* () {
        const sw = new StopWatch(true);
        position = position.clone();
        const word = model.getWordAtPosition(position);
        const defaultReplaceRange = word ? new Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn) : Range.fromPositions(position);
        const defaultRange = { replace: defaultReplaceRange, insert: defaultReplaceRange.setEndPosition(position.lineNumber, position.column) };
        const result = [];
        const disposables = new DisposableStore();
        const durations = [];
        let needsClipboard = false;
        const onCompletionList = (provider, container, sw) => {
            var _a, _b;
            if (!container) {
                return;
            }
            for (let suggestion of container.suggestions) {
                if (!options.kindFilter.has(suggestion.kind)) {
                    // fill in default range when missing
                    if (!suggestion.range) {
                        suggestion.range = defaultRange;
                    }
                    // fill in default sortText when missing
                    if (!suggestion.sortText) {
                        suggestion.sortText = typeof suggestion.label === 'string' ? suggestion.label : suggestion.label.name;
                    }
                    if (!needsClipboard && suggestion.insertTextRules && suggestion.insertTextRules & 4 /* InsertAsSnippet */) {
                        needsClipboard = SnippetParser.guessNeedsClipboard(suggestion.insertText);
                    }
                    result.push(new CompletionItem(position, suggestion, container, provider));
                }
            }
            if (isDisposable(container)) {
                disposables.add(container);
            }
            durations.push({
                providerName: (_a = provider._debugDisplayName) !== null && _a !== void 0 ? _a : 'unkown_provider', elapsedProvider: (_b = container.duration) !== null && _b !== void 0 ? _b : -1,
                elapsedOverall: sw.elapsed()
            });
        };
        // ask for snippets in parallel to asking "real" providers. Only do something if configured to
        // do so - no snippet filter, no special-providers-only request
        const snippetCompletions = (() => __awaiter(this, void 0, void 0, function* () {
            if (!_snippetSuggestSupport || options.kindFilter.has(27 /* Snippet */)) {
                return;
            }
            if (options.providerFilter.size > 0 && !options.providerFilter.has(_snippetSuggestSupport)) {
                return;
            }
            const sw = new StopWatch(true);
            const list = yield _snippetSuggestSupport.provideCompletionItems(model, position, context, token);
            onCompletionList(_snippetSuggestSupport, list, sw);
        }))();
        // add suggestions from contributed providers - providers are ordered in groups of
        // equal score and once a group produces a result the process stops
        // get provider groups, always add snippet suggestion provider
        for (let providerGroup of modes.CompletionProviderRegistry.orderedGroups(model)) {
            // for each support in the group ask for suggestions
            let lenBefore = result.length;
            yield Promise.all(providerGroup.map((provider) => __awaiter(this, void 0, void 0, function* () {
                if (options.providerFilter.size > 0 && !options.providerFilter.has(provider)) {
                    return;
                }
                try {
                    const sw = new StopWatch(true);
                    const list = yield provider.provideCompletionItems(model, position, context, token);
                    onCompletionList(provider, list, sw);
                }
                catch (err) {
                    onUnexpectedExternalError(err);
                }
            })));
            if (lenBefore !== result.length || token.isCancellationRequested) {
                break;
            }
        }
        yield snippetCompletions;
        if (token.isCancellationRequested) {
            disposables.dispose();
            return Promise.reject(canceled());
        }
        return new CompletionItemModel(result.sort(getSuggestionComparator(options.snippetSortOrder)), needsClipboard, { entries: durations, elapsed: sw.elapsed() }, disposables);
    });
}
function defaultComparator(a, b) {
    // check with 'sortText'
    if (a.sortTextLow && b.sortTextLow) {
        if (a.sortTextLow < b.sortTextLow) {
            return -1;
        }
        else if (a.sortTextLow > b.sortTextLow) {
            return 1;
        }
    }
    // check with 'label'
    if (a.completion.label < b.completion.label) {
        return -1;
    }
    else if (a.completion.label > b.completion.label) {
        return 1;
    }
    // check with 'type'
    return a.completion.kind - b.completion.kind;
}
function snippetUpComparator(a, b) {
    if (a.completion.kind !== b.completion.kind) {
        if (a.completion.kind === 27 /* Snippet */) {
            return -1;
        }
        else if (b.completion.kind === 27 /* Snippet */) {
            return 1;
        }
    }
    return defaultComparator(a, b);
}
function snippetDownComparator(a, b) {
    if (a.completion.kind !== b.completion.kind) {
        if (a.completion.kind === 27 /* Snippet */) {
            return 1;
        }
        else if (b.completion.kind === 27 /* Snippet */) {
            return -1;
        }
    }
    return defaultComparator(a, b);
}
const _snippetComparators = new Map();
_snippetComparators.set(0 /* Top */, snippetUpComparator);
_snippetComparators.set(2 /* Bottom */, snippetDownComparator);
_snippetComparators.set(1 /* Inline */, defaultComparator);
export function getSuggestionComparator(snippetConfig) {
    return _snippetComparators.get(snippetConfig);
}
CommandsRegistry.registerCommand('_executeCompletionItemProvider', (accessor, ...args) => __awaiter(void 0, void 0, void 0, function* () {
    const [uri, position, triggerCharacter, maxItemsToResolve] = args;
    assertType(URI.isUri(uri));
    assertType(Position.isIPosition(position));
    assertType(typeof triggerCharacter === 'string' || !triggerCharacter);
    assertType(typeof maxItemsToResolve === 'number' || !maxItemsToResolve);
    const ref = yield accessor.get(ITextModelService).createModelReference(uri);
    try {
        const result = {
            incomplete: false,
            suggestions: []
        };
        const resolving = [];
        const completions = yield provideSuggestionItems(ref.object.textEditorModel, Position.lift(position), undefined, { triggerCharacter, triggerKind: triggerCharacter ? 1 /* TriggerCharacter */ : 0 /* Invoke */ });
        for (const item of completions.items) {
            if (resolving.length < (maxItemsToResolve !== null && maxItemsToResolve !== void 0 ? maxItemsToResolve : 0)) {
                resolving.push(item.resolve(CancellationToken.None));
            }
            result.incomplete = result.incomplete || item.container.incomplete;
            result.suggestions.push(item.completion);
        }
        try {
            yield Promise.all(resolving);
            return result;
        }
        finally {
            setTimeout(() => completions.disposable.dispose(), 100);
        }
    }
    finally {
        ref.dispose();
    }
}));
const _provider = new class {
    constructor() {
        this.onlyOnceSuggestions = [];
    }
    provideCompletionItems() {
        let suggestions = this.onlyOnceSuggestions.slice(0);
        let result = { suggestions };
        this.onlyOnceSuggestions.length = 0;
        return result;
    }
};
modes.CompletionProviderRegistry.register('*', _provider);
export function showSimpleSuggestions(editor, suggestions) {
    setTimeout(() => {
        _provider.onlyOnceSuggestions.push(...suggestions);
        editor.getContribution('editor.contrib.suggestController').triggerSuggest(new Set().add(_provider));
    }, 0);
}
