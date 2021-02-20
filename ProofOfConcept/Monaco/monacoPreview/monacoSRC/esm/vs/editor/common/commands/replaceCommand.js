/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Selection } from '../core/selection.js';
export class ReplaceCommand {
    constructor(range, text, insertsAutoWhitespace = false) {
        this._range = range;
        this._text = text;
        this.insertsAutoWhitespace = insertsAutoWhitespace;
    }
    getEditOperations(model, builder) {
        builder.addTrackedEditOperation(this._range, this._text);
    }
    computeCursorState(model, helper) {
        let inverseEditOperations = helper.getInverseEditOperations();
        let srcRange = inverseEditOperations[0].range;
        return new Selection(srcRange.endLineNumber, srcRange.endColumn, srcRange.endLineNumber, srcRange.endColumn);
    }
}
export class ReplaceCommandThatSelectsText {
    constructor(range, text) {
        this._range = range;
        this._text = text;
    }
    getEditOperations(model, builder) {
        builder.addTrackedEditOperation(this._range, this._text);
    }
    computeCursorState(model, helper) {
        const inverseEditOperations = helper.getInverseEditOperations();
        const srcRange = inverseEditOperations[0].range;
        return new Selection(srcRange.startLineNumber, srcRange.startColumn, srcRange.endLineNumber, srcRange.endColumn);
    }
}
export class ReplaceCommandWithoutChangingPosition {
    constructor(range, text, insertsAutoWhitespace = false) {
        this._range = range;
        this._text = text;
        this.insertsAutoWhitespace = insertsAutoWhitespace;
    }
    getEditOperations(model, builder) {
        builder.addTrackedEditOperation(this._range, this._text);
    }
    computeCursorState(model, helper) {
        let inverseEditOperations = helper.getInverseEditOperations();
        let srcRange = inverseEditOperations[0].range;
        return new Selection(srcRange.startLineNumber, srcRange.startColumn, srcRange.startLineNumber, srcRange.startColumn);
    }
}
export class ReplaceCommandWithOffsetCursorState {
    constructor(range, text, lineNumberDeltaOffset, columnDeltaOffset, insertsAutoWhitespace = false) {
        this._range = range;
        this._text = text;
        this._columnDeltaOffset = columnDeltaOffset;
        this._lineNumberDeltaOffset = lineNumberDeltaOffset;
        this.insertsAutoWhitespace = insertsAutoWhitespace;
    }
    getEditOperations(model, builder) {
        builder.addTrackedEditOperation(this._range, this._text);
    }
    computeCursorState(model, helper) {
        let inverseEditOperations = helper.getInverseEditOperations();
        let srcRange = inverseEditOperations[0].range;
        return new Selection(srcRange.endLineNumber + this._lineNumberDeltaOffset, srcRange.endColumn + this._columnDeltaOffset, srcRange.endLineNumber + this._lineNumberDeltaOffset, srcRange.endColumn + this._columnDeltaOffset);
    }
}
export class ReplaceCommandThatPreservesSelection {
    constructor(editRange, text, initialSelection, forceMoveMarkers = false) {
        this._range = editRange;
        this._text = text;
        this._initialSelection = initialSelection;
        this._forceMoveMarkers = forceMoveMarkers;
        this._selectionId = null;
    }
    getEditOperations(model, builder) {
        builder.addTrackedEditOperation(this._range, this._text, this._forceMoveMarkers);
        this._selectionId = builder.trackSelection(this._initialSelection);
    }
    computeCursorState(model, helper) {
        return helper.getTrackedSelection(this._selectionId);
    }
}
