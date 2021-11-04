/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { sanitizeRanges } from './syntaxRangeProvider.js';
export const ID_INIT_PROVIDER = 'init';
export class InitializingRangeProvider {
    constructor(editorModel, initialRanges, onTimeout, timeoutTime) {
        this.editorModel = editorModel;
        this.id = ID_INIT_PROVIDER;
        if (initialRanges.length) {
            let toDecorationRange = (range) => {
                return {
                    range: {
                        startLineNumber: range.startLineNumber,
                        startColumn: 0,
                        endLineNumber: range.endLineNumber,
                        endColumn: editorModel.getLineLength(range.endLineNumber)
                    },
                    options: {
                        stickiness: 1 /* NeverGrowsWhenTypingAtEdges */
                    }
                };
            };
            this.decorationIds = editorModel.deltaDecorations([], initialRanges.map(toDecorationRange));
            this.timeout = setTimeout(onTimeout, timeoutTime);
        }
    }
    dispose() {
        if (this.decorationIds) {
            this.editorModel.deltaDecorations(this.decorationIds, []);
            this.decorationIds = undefined;
        }
        if (typeof this.timeout === 'number') {
            clearTimeout(this.timeout);
            this.timeout = undefined;
        }
    }
    compute(cancelationToken) {
        let foldingRangeData = [];
        if (this.decorationIds) {
            for (let id of this.decorationIds) {
                let range = this.editorModel.getDecorationRange(id);
                if (range) {
                    foldingRangeData.push({ start: range.startLineNumber, end: range.endLineNumber, rank: 1 });
                }
            }
        }
        return Promise.resolve(sanitizeRanges(foldingRangeData, Number.MAX_VALUE));
    }
}
