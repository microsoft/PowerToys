/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { WorkerManager } from './workerManager.js';
import * as languageFeatures from './languageFeatures.js';
import { languages } from './fillers/monaco-editor-core.js';
export function setupMode1(defaults) {
    var client = new WorkerManager(defaults);
    var worker = function () {
        var uris = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            uris[_i] = arguments[_i];
        }
        return client.getLanguageServiceWorker.apply(client, uris);
    };
    var languageId = defaults.languageId;
    // all modes
    languages.registerCompletionItemProvider(languageId, new languageFeatures.CompletionAdapter(worker));
    languages.registerHoverProvider(languageId, new languageFeatures.HoverAdapter(worker));
    languages.registerDocumentHighlightProvider(languageId, new languageFeatures.DocumentHighlightAdapter(worker));
    languages.registerLinkProvider(languageId, new languageFeatures.DocumentLinkAdapter(worker));
    languages.registerFoldingRangeProvider(languageId, new languageFeatures.FoldingRangeAdapter(worker));
    languages.registerDocumentSymbolProvider(languageId, new languageFeatures.DocumentSymbolAdapter(worker));
    languages.registerSelectionRangeProvider(languageId, new languageFeatures.SelectionRangeAdapter(worker));
    languages.registerRenameProvider(languageId, new languageFeatures.RenameAdapter(worker));
    // only html
    if (languageId === 'html') {
        languages.registerDocumentFormattingEditProvider(languageId, new languageFeatures.DocumentFormattingEditProvider(worker));
        languages.registerDocumentRangeFormattingEditProvider(languageId, new languageFeatures.DocumentRangeFormattingEditProvider(worker));
        new languageFeatures.DiagnosticsAdapter(languageId, worker, defaults);
    }
}
export function setupMode(defaults) {
    var disposables = [];
    var providers = [];
    var client = new WorkerManager(defaults);
    disposables.push(client);
    var worker = function () {
        var uris = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            uris[_i] = arguments[_i];
        }
        return client.getLanguageServiceWorker.apply(client, uris);
    };
    function registerProviders() {
        var languageId = defaults.languageId, modeConfiguration = defaults.modeConfiguration;
        disposeAll(providers);
        if (modeConfiguration.completionItems) {
            providers.push(languages.registerCompletionItemProvider(languageId, new languageFeatures.CompletionAdapter(worker)));
        }
        if (modeConfiguration.hovers) {
            providers.push(languages.registerHoverProvider(languageId, new languageFeatures.HoverAdapter(worker)));
        }
        if (modeConfiguration.documentHighlights) {
            providers.push(languages.registerDocumentHighlightProvider(languageId, new languageFeatures.DocumentHighlightAdapter(worker)));
        }
        if (modeConfiguration.links) {
            providers.push(languages.registerLinkProvider(languageId, new languageFeatures.DocumentLinkAdapter(worker)));
        }
        if (modeConfiguration.documentSymbols) {
            providers.push(languages.registerDocumentSymbolProvider(languageId, new languageFeatures.DocumentSymbolAdapter(worker)));
        }
        if (modeConfiguration.rename) {
            providers.push(languages.registerRenameProvider(languageId, new languageFeatures.RenameAdapter(worker)));
        }
        if (modeConfiguration.foldingRanges) {
            providers.push(languages.registerFoldingRangeProvider(languageId, new languageFeatures.FoldingRangeAdapter(worker)));
        }
        if (modeConfiguration.selectionRanges) {
            providers.push(languages.registerSelectionRangeProvider(languageId, new languageFeatures.SelectionRangeAdapter(worker)));
        }
        if (modeConfiguration.documentFormattingEdits) {
            providers.push(languages.registerDocumentFormattingEditProvider(languageId, new languageFeatures.DocumentFormattingEditProvider(worker)));
        }
        if (modeConfiguration.documentRangeFormattingEdits) {
            providers.push(languages.registerDocumentRangeFormattingEditProvider(languageId, new languageFeatures.DocumentRangeFormattingEditProvider(worker)));
        }
        if (modeConfiguration.diagnostics) {
            providers.push(new languageFeatures.DiagnosticsAdapter(languageId, worker, defaults));
        }
    }
    registerProviders();
    disposables.push(asDisposable(providers));
    return asDisposable(disposables);
}
function asDisposable(disposables) {
    return { dispose: function () { return disposeAll(disposables); } };
}
function disposeAll(disposables) {
    while (disposables.length) {
        disposables.pop().dispose();
    }
}
