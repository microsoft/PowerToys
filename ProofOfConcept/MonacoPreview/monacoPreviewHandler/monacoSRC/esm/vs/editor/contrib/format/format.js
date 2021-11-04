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
import { alert } from '../../../base/browser/ui/aria/aria.js';
import { asArray, isNonEmptyArray } from '../../../base/common/arrays.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { illegalArgument, onUnexpectedExternalError } from '../../../base/common/errors.js';
import { URI } from '../../../base/common/uri.js';
import { EditorStateCancellationTokenSource, TextModelCancellationTokenSource } from '../../browser/core/editorState.js';
import { isCodeEditor } from '../../browser/editorBrowser.js';
import { Position } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { DocumentFormattingEditProviderRegistry, DocumentRangeFormattingEditProviderRegistry, OnTypeFormattingEditProviderRegistry } from '../../common/modes.js';
import { IEditorWorkerService } from '../../common/services/editorWorkerService.js';
import { IModelService } from '../../common/services/modelService.js';
import { FormattingEdit } from './formattingEdit.js';
import * as nls from '../../../nls.js';
import { ExtensionIdentifier } from '../../../platform/extensions/common/extensions.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { LinkedList } from '../../../base/common/linkedList.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
import { assertType } from '../../../base/common/types.js';
import { Iterable } from '../../../base/common/iterator.js';
export function alertFormattingEdits(edits) {
    edits = edits.filter(edit => edit.range);
    if (!edits.length) {
        return;
    }
    let { range } = edits[0];
    for (let i = 1; i < edits.length; i++) {
        range = Range.plusRange(range, edits[i].range);
    }
    const { startLineNumber, endLineNumber } = range;
    if (startLineNumber === endLineNumber) {
        if (edits.length === 1) {
            alert(nls.localize('hint11', "Made 1 formatting edit on line {0}", startLineNumber));
        }
        else {
            alert(nls.localize('hintn1', "Made {0} formatting edits on line {1}", edits.length, startLineNumber));
        }
    }
    else {
        if (edits.length === 1) {
            alert(nls.localize('hint1n', "Made 1 formatting edit between lines {0} and {1}", startLineNumber, endLineNumber));
        }
        else {
            alert(nls.localize('hintnn', "Made {0} formatting edits between lines {1} and {2}", edits.length, startLineNumber, endLineNumber));
        }
    }
}
export function getRealAndSyntheticDocumentFormattersOrdered(model) {
    const result = [];
    const seen = new Set();
    // (1) add all document formatter
    const docFormatter = DocumentFormattingEditProviderRegistry.ordered(model);
    for (const formatter of docFormatter) {
        result.push(formatter);
        if (formatter.extensionId) {
            seen.add(ExtensionIdentifier.toKey(formatter.extensionId));
        }
    }
    // (2) add all range formatter as document formatter (unless the same extension already did that)
    const rangeFormatter = DocumentRangeFormattingEditProviderRegistry.ordered(model);
    for (const formatter of rangeFormatter) {
        if (formatter.extensionId) {
            if (seen.has(ExtensionIdentifier.toKey(formatter.extensionId))) {
                continue;
            }
            seen.add(ExtensionIdentifier.toKey(formatter.extensionId));
        }
        result.push({
            displayName: formatter.displayName,
            extensionId: formatter.extensionId,
            provideDocumentFormattingEdits(model, options, token) {
                return formatter.provideDocumentRangeFormattingEdits(model, model.getFullModelRange(), options, token);
            }
        });
    }
    return result;
}
export class FormattingConflicts {
    static select(formatter, document, mode) {
        return __awaiter(this, void 0, void 0, function* () {
            if (formatter.length === 0) {
                return undefined;
            }
            const selector = Iterable.first(FormattingConflicts._selectors);
            if (selector) {
                return yield selector(formatter, document, mode);
            }
            return undefined;
        });
    }
}
FormattingConflicts._selectors = new LinkedList();
export function formatDocumentRangesWithSelectedProvider(accessor, editorOrModel, rangeOrRanges, mode, progress, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const instaService = accessor.get(IInstantiationService);
        const model = isCodeEditor(editorOrModel) ? editorOrModel.getModel() : editorOrModel;
        const provider = DocumentRangeFormattingEditProviderRegistry.ordered(model);
        const selected = yield FormattingConflicts.select(provider, model, mode);
        if (selected) {
            progress.report(selected);
            yield instaService.invokeFunction(formatDocumentRangesWithProvider, selected, editorOrModel, rangeOrRanges, token);
        }
    });
}
export function formatDocumentRangesWithProvider(accessor, provider, editorOrModel, rangeOrRanges, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const workerService = accessor.get(IEditorWorkerService);
        let model;
        let cts;
        if (isCodeEditor(editorOrModel)) {
            model = editorOrModel.getModel();
            cts = new EditorStateCancellationTokenSource(editorOrModel, 1 /* Value */ | 4 /* Position */, undefined, token);
        }
        else {
            model = editorOrModel;
            cts = new TextModelCancellationTokenSource(editorOrModel, token);
        }
        // make sure that ranges don't overlap nor touch each other
        let ranges = [];
        let len = 0;
        for (let range of asArray(rangeOrRanges).sort(Range.compareRangesUsingStarts)) {
            if (len > 0 && Range.areIntersectingOrTouching(ranges[len - 1], range)) {
                ranges[len - 1] = Range.fromPositions(ranges[len - 1].getStartPosition(), range.getEndPosition());
            }
            else {
                len = ranges.push(range);
            }
        }
        const allEdits = [];
        for (let range of ranges) {
            try {
                const rawEdits = yield provider.provideDocumentRangeFormattingEdits(model, range, model.getFormattingOptions(), cts.token);
                const minEdits = yield workerService.computeMoreMinimalEdits(model.uri, rawEdits);
                if (minEdits) {
                    allEdits.push(...minEdits);
                }
                if (cts.token.isCancellationRequested) {
                    return true;
                }
            }
            finally {
                cts.dispose();
            }
        }
        if (allEdits.length === 0) {
            return false;
        }
        if (isCodeEditor(editorOrModel)) {
            // use editor to apply edits
            FormattingEdit.execute(editorOrModel, allEdits, true);
            alertFormattingEdits(allEdits);
            editorOrModel.revealPositionInCenterIfOutsideViewport(editorOrModel.getPosition(), 1 /* Immediate */);
        }
        else {
            // use model to apply edits
            const [{ range }] = allEdits;
            const initialSelection = new Selection(range.startLineNumber, range.startColumn, range.endLineNumber, range.endColumn);
            model.pushEditOperations([initialSelection], allEdits.map(edit => {
                return {
                    text: edit.text,
                    range: Range.lift(edit.range),
                    forceMoveMarkers: true
                };
            }), undoEdits => {
                for (const { range } of undoEdits) {
                    if (Range.areIntersectingOrTouching(range, initialSelection)) {
                        return [new Selection(range.startLineNumber, range.startColumn, range.endLineNumber, range.endColumn)];
                    }
                }
                return null;
            });
        }
        return true;
    });
}
export function formatDocumentWithSelectedProvider(accessor, editorOrModel, mode, progress, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const instaService = accessor.get(IInstantiationService);
        const model = isCodeEditor(editorOrModel) ? editorOrModel.getModel() : editorOrModel;
        const provider = getRealAndSyntheticDocumentFormattersOrdered(model);
        const selected = yield FormattingConflicts.select(provider, model, mode);
        if (selected) {
            progress.report(selected);
            yield instaService.invokeFunction(formatDocumentWithProvider, selected, editorOrModel, mode, token);
        }
    });
}
export function formatDocumentWithProvider(accessor, provider, editorOrModel, mode, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const workerService = accessor.get(IEditorWorkerService);
        let model;
        let cts;
        if (isCodeEditor(editorOrModel)) {
            model = editorOrModel.getModel();
            cts = new EditorStateCancellationTokenSource(editorOrModel, 1 /* Value */ | 4 /* Position */, undefined, token);
        }
        else {
            model = editorOrModel;
            cts = new TextModelCancellationTokenSource(editorOrModel, token);
        }
        let edits;
        try {
            const rawEdits = yield provider.provideDocumentFormattingEdits(model, model.getFormattingOptions(), cts.token);
            edits = yield workerService.computeMoreMinimalEdits(model.uri, rawEdits);
            if (cts.token.isCancellationRequested) {
                return true;
            }
        }
        finally {
            cts.dispose();
        }
        if (!edits || edits.length === 0) {
            return false;
        }
        if (isCodeEditor(editorOrModel)) {
            // use editor to apply edits
            FormattingEdit.execute(editorOrModel, edits, mode !== 2 /* Silent */);
            if (mode !== 2 /* Silent */) {
                alertFormattingEdits(edits);
                editorOrModel.revealPositionInCenterIfOutsideViewport(editorOrModel.getPosition(), 1 /* Immediate */);
            }
        }
        else {
            // use model to apply edits
            const [{ range }] = edits;
            const initialSelection = new Selection(range.startLineNumber, range.startColumn, range.endLineNumber, range.endColumn);
            model.pushEditOperations([initialSelection], edits.map(edit => {
                return {
                    text: edit.text,
                    range: Range.lift(edit.range),
                    forceMoveMarkers: true
                };
            }), undoEdits => {
                for (const { range } of undoEdits) {
                    if (Range.areIntersectingOrTouching(range, initialSelection)) {
                        return [new Selection(range.startLineNumber, range.startColumn, range.endLineNumber, range.endColumn)];
                    }
                }
                return null;
            });
        }
        return true;
    });
}
export function getDocumentRangeFormattingEditsUntilResult(workerService, model, range, options, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const providers = DocumentRangeFormattingEditProviderRegistry.ordered(model);
        for (const provider of providers) {
            let rawEdits = yield Promise.resolve(provider.provideDocumentRangeFormattingEdits(model, range, options, token)).catch(onUnexpectedExternalError);
            if (isNonEmptyArray(rawEdits)) {
                return yield workerService.computeMoreMinimalEdits(model.uri, rawEdits);
            }
        }
        return undefined;
    });
}
export function getDocumentFormattingEditsUntilResult(workerService, model, options, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const providers = getRealAndSyntheticDocumentFormattersOrdered(model);
        for (const provider of providers) {
            let rawEdits = yield Promise.resolve(provider.provideDocumentFormattingEdits(model, options, token)).catch(onUnexpectedExternalError);
            if (isNonEmptyArray(rawEdits)) {
                return yield workerService.computeMoreMinimalEdits(model.uri, rawEdits);
            }
        }
        return undefined;
    });
}
export function getOnTypeFormattingEdits(workerService, model, position, ch, options) {
    const providers = OnTypeFormattingEditProviderRegistry.ordered(model);
    if (providers.length === 0) {
        return Promise.resolve(undefined);
    }
    if (providers[0].autoFormatTriggerCharacters.indexOf(ch) < 0) {
        return Promise.resolve(undefined);
    }
    return Promise.resolve(providers[0].provideOnTypeFormattingEdits(model, position, ch, options, CancellationToken.None)).catch(onUnexpectedExternalError).then(edits => {
        return workerService.computeMoreMinimalEdits(model.uri, edits);
    });
}
CommandsRegistry.registerCommand('_executeFormatRangeProvider', function (accessor, ...args) {
    const [resource, range, options] = args;
    assertType(URI.isUri(resource));
    assertType(Range.isIRange(range));
    const model = accessor.get(IModelService).getModel(resource);
    if (!model) {
        throw illegalArgument('resource');
    }
    return getDocumentRangeFormattingEditsUntilResult(accessor.get(IEditorWorkerService), model, Range.lift(range), options, CancellationToken.None);
});
CommandsRegistry.registerCommand('_executeFormatDocumentProvider', function (accessor, ...args) {
    const [resource, options] = args;
    assertType(URI.isUri(resource));
    const model = accessor.get(IModelService).getModel(resource);
    if (!model) {
        throw illegalArgument('resource');
    }
    return getDocumentFormattingEditsUntilResult(accessor.get(IEditorWorkerService), model, options, CancellationToken.None);
});
CommandsRegistry.registerCommand('_executeFormatOnTypeProvider', function (accessor, ...args) {
    const [resource, position, ch, options] = args;
    assertType(URI.isUri(resource));
    assertType(Position.isIPosition(position));
    assertType(typeof ch === 'string');
    const model = accessor.get(IModelService).getModel(resource);
    if (!model) {
        throw illegalArgument('resource');
    }
    return getOnTypeFormattingEdits(accessor.get(IEditorWorkerService), model, Position.lift(position), ch, options);
});
