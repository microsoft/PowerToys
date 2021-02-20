/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { createScanner } from './parser/htmlScanner.js';
import { parse } from './parser/htmlParser.js';
import { HTMLCompletion } from './services/htmlCompletion.js';
import { HTMLHover } from './services/htmlHover.js';
import { format } from './services/htmlFormatter.js';
import { findDocumentLinks } from './services/htmlLinks.js';
import { findDocumentHighlights } from './services/htmlHighlighting.js';
import { findDocumentSymbols } from './services/htmlSymbolsProvider.js';
import { doRename } from './services/htmlRename.js';
import { findMatchingTagPosition } from './services/htmlMatchingTagPosition.js';
import { findLinkedEditingRanges } from './services/htmlLinkedEditing.js';
import { getFoldingRanges } from './services/htmlFolding.js';
import { getSelectionRanges } from './services/htmlSelectionRange.js';
import { HTMLDataProvider } from './languageFacts/dataProvider.js';
import { HTMLDataManager } from './languageFacts/dataManager.js';
import { htmlData } from './languageFacts/data/webCustomData.js';
export * from './htmlLanguageTypes.js';
var defaultLanguageServiceOptions = {};
export function getLanguageService(options) {
    if (options === void 0) { options = defaultLanguageServiceOptions; }
    var dataManager = new HTMLDataManager(options);
    var htmlHover = new HTMLHover(options, dataManager);
    var htmlCompletion = new HTMLCompletion(options, dataManager);
    return {
        setDataProviders: dataManager.setDataProviders.bind(dataManager),
        createScanner: createScanner,
        parseHTMLDocument: function (document) { return parse(document.getText()); },
        doComplete: htmlCompletion.doComplete.bind(htmlCompletion),
        doComplete2: htmlCompletion.doComplete2.bind(htmlCompletion),
        setCompletionParticipants: htmlCompletion.setCompletionParticipants.bind(htmlCompletion),
        doHover: htmlHover.doHover.bind(htmlHover),
        format: format,
        findDocumentHighlights: findDocumentHighlights,
        findDocumentLinks: findDocumentLinks,
        findDocumentSymbols: findDocumentSymbols,
        getFoldingRanges: getFoldingRanges,
        getSelectionRanges: getSelectionRanges,
        doTagComplete: htmlCompletion.doTagComplete.bind(htmlCompletion),
        doRename: doRename,
        findMatchingTagPosition: findMatchingTagPosition,
        findOnTypeRenameRanges: findLinkedEditingRanges,
        findLinkedEditingRanges: findLinkedEditingRanges
    };
}
export function newHTMLDataProvider(id, customData) {
    return new HTMLDataProvider(id, customData);
}
export function getDefaultHTMLDataProvider() {
    return newHTMLDataProvider('default', htmlData);
}
