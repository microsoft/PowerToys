/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import { WorkerManager } from './workerManager.js';
import * as languageFeatures from './languageFeatures.js';
import { languages } from './fillers/monaco-editor-core.js';
var javaScriptWorker;
var typeScriptWorker;
export function setupTypeScript(defaults) {
    typeScriptWorker = setupMode(defaults, 'typescript');
}
export function setupJavaScript(defaults) {
    javaScriptWorker = setupMode(defaults, 'javascript');
}
export function getJavaScriptWorker() {
    return new Promise(function (resolve, reject) {
        if (!javaScriptWorker) {
            return reject('JavaScript not registered!');
        }
        resolve(javaScriptWorker);
    });
}
export function getTypeScriptWorker() {
    return new Promise(function (resolve, reject) {
        if (!typeScriptWorker) {
            return reject('TypeScript not registered!');
        }
        resolve(typeScriptWorker);
    });
}
function setupMode(defaults, modeId) {
    var client = new WorkerManager(modeId, defaults);
    var worker = function () {
        var uris = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            uris[_i] = arguments[_i];
        }
        return client.getLanguageServiceWorker.apply(client, uris);
    };
    var libFiles = new languageFeatures.LibFiles(worker);
    languages.registerCompletionItemProvider(modeId, new languageFeatures.SuggestAdapter(worker));
    languages.registerSignatureHelpProvider(modeId, new languageFeatures.SignatureHelpAdapter(worker));
    languages.registerHoverProvider(modeId, new languageFeatures.QuickInfoAdapter(worker));
    languages.registerDocumentHighlightProvider(modeId, new languageFeatures.OccurrencesAdapter(worker));
    languages.registerDefinitionProvider(modeId, new languageFeatures.DefinitionAdapter(libFiles, worker));
    languages.registerReferenceProvider(modeId, new languageFeatures.ReferenceAdapter(libFiles, worker));
    languages.registerDocumentSymbolProvider(modeId, new languageFeatures.OutlineAdapter(worker));
    languages.registerDocumentRangeFormattingEditProvider(modeId, new languageFeatures.FormatAdapter(worker));
    languages.registerOnTypeFormattingEditProvider(modeId, new languageFeatures.FormatOnTypeAdapter(worker));
    languages.registerCodeActionProvider(modeId, new languageFeatures.CodeActionAdaptor(worker));
    languages.registerRenameProvider(modeId, new languageFeatures.RenameAdapter(worker));
    new languageFeatures.DiagnosticsAdapter(libFiles, defaults, modeId, worker);
    return worker;
}
