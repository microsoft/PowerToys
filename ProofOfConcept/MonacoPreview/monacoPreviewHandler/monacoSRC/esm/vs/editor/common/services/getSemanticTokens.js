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
import { CancellationToken } from '../../../base/common/cancellation.js';
import { onUnexpectedExternalError } from '../../../base/common/errors.js';
import { URI } from '../../../base/common/uri.js';
import { DocumentSemanticTokensProviderRegistry, DocumentRangeSemanticTokensProviderRegistry } from '../modes.js';
import { IModelService } from './modelService.js';
import { CommandsRegistry, ICommandService } from '../../../platform/commands/common/commands.js';
import { assertType } from '../../../base/common/types.js';
import { encodeSemanticTokensDto } from './semanticTokensDto.js';
import { Range } from '../core/range.js';
export function isSemanticTokens(v) {
    return v && !!(v.data);
}
export function isSemanticTokensEdits(v) {
    return v && Array.isArray(v.edits);
}
export function getDocumentSemanticTokens(model, lastResultId, token) {
    const provider = _getDocumentSemanticTokensProvider(model);
    if (!provider) {
        return null;
    }
    return {
        provider: provider,
        request: Promise.resolve(provider.provideDocumentSemanticTokens(model, lastResultId, token))
    };
}
function _getDocumentSemanticTokensProvider(model) {
    const result = DocumentSemanticTokensProviderRegistry.ordered(model);
    return (result.length > 0 ? result[0] : null);
}
export function getDocumentRangeSemanticTokensProvider(model) {
    const result = DocumentRangeSemanticTokensProviderRegistry.ordered(model);
    return (result.length > 0 ? result[0] : null);
}
CommandsRegistry.registerCommand('_provideDocumentSemanticTokensLegend', (accessor, ...args) => __awaiter(void 0, void 0, void 0, function* () {
    const [uri] = args;
    assertType(uri instanceof URI);
    const model = accessor.get(IModelService).getModel(uri);
    if (!model) {
        return undefined;
    }
    const provider = _getDocumentSemanticTokensProvider(model);
    if (!provider) {
        // there is no provider => fall back to a document range semantic tokens provider
        return accessor.get(ICommandService).executeCommand('_provideDocumentRangeSemanticTokensLegend', uri);
    }
    return provider.getLegend();
}));
CommandsRegistry.registerCommand('_provideDocumentSemanticTokens', (accessor, ...args) => __awaiter(void 0, void 0, void 0, function* () {
    const [uri] = args;
    assertType(uri instanceof URI);
    const model = accessor.get(IModelService).getModel(uri);
    if (!model) {
        return undefined;
    }
    const r = getDocumentSemanticTokens(model, null, CancellationToken.None);
    if (!r) {
        // there is no provider => fall back to a document range semantic tokens provider
        return accessor.get(ICommandService).executeCommand('_provideDocumentRangeSemanticTokens', uri, model.getFullModelRange());
    }
    const { provider, request } = r;
    let result;
    try {
        result = yield request;
    }
    catch (err) {
        onUnexpectedExternalError(err);
        return undefined;
    }
    if (!result || !isSemanticTokens(result)) {
        return undefined;
    }
    const buff = encodeSemanticTokensDto({
        id: 0,
        type: 'full',
        data: result.data
    });
    if (result.resultId) {
        provider.releaseDocumentSemanticTokens(result.resultId);
    }
    return buff;
}));
CommandsRegistry.registerCommand('_provideDocumentRangeSemanticTokensLegend', (accessor, ...args) => __awaiter(void 0, void 0, void 0, function* () {
    const [uri] = args;
    assertType(uri instanceof URI);
    const model = accessor.get(IModelService).getModel(uri);
    if (!model) {
        return undefined;
    }
    const provider = getDocumentRangeSemanticTokensProvider(model);
    if (!provider) {
        return undefined;
    }
    return provider.getLegend();
}));
CommandsRegistry.registerCommand('_provideDocumentRangeSemanticTokens', (accessor, ...args) => __awaiter(void 0, void 0, void 0, function* () {
    const [uri, range] = args;
    assertType(uri instanceof URI);
    assertType(Range.isIRange(range));
    const model = accessor.get(IModelService).getModel(uri);
    if (!model) {
        return undefined;
    }
    const provider = getDocumentRangeSemanticTokensProvider(model);
    if (!provider) {
        // there is no provider
        return undefined;
    }
    let result;
    try {
        result = yield provider.provideDocumentRangeSemanticTokens(model, Range.lift(range), CancellationToken.None);
    }
    catch (err) {
        onUnexpectedExternalError(err);
        return undefined;
    }
    if (!result || !isSemanticTokens(result)) {
        return undefined;
    }
    return encodeSemanticTokensDto({
        id: 0,
        type: 'full',
        data: result.data
    });
}));
