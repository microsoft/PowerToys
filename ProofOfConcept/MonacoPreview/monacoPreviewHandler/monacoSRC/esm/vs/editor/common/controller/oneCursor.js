/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { CursorState, SingleCursorState } from './cursorCommon.js';
import { Position } from '../core/position.js';
import { Range } from '../core/range.js';
import { Selection } from '../core/selection.js';
export class OneCursor {
    constructor(context) {
        this._selTrackedRange = null;
        this._trackSelection = true;
        this._setState(context, new SingleCursorState(new Range(1, 1, 1, 1), 0, new Position(1, 1), 0), new SingleCursorState(new Range(1, 1, 1, 1), 0, new Position(1, 1), 0));
    }
    dispose(context) {
        this._removeTrackedRange(context);
    }
    startTrackingSelection(context) {
        this._trackSelection = true;
        this._updateTrackedRange(context);
    }
    stopTrackingSelection(context) {
        this._trackSelection = false;
        this._removeTrackedRange(context);
    }
    _updateTrackedRange(context) {
        if (!this._trackSelection) {
            // don't track the selection
            return;
        }
        this._selTrackedRange = context.model._setTrackedRange(this._selTrackedRange, this.modelState.selection, 0 /* AlwaysGrowsWhenTypingAtEdges */);
    }
    _removeTrackedRange(context) {
        this._selTrackedRange = context.model._setTrackedRange(this._selTrackedRange, null, 0 /* AlwaysGrowsWhenTypingAtEdges */);
    }
    asCursorState() {
        return new CursorState(this.modelState, this.viewState);
    }
    readSelectionFromMarkers(context) {
        const range = context.model._getTrackedRange(this._selTrackedRange);
        if (this.modelState.selection.getDirection() === 0 /* LTR */) {
            return new Selection(range.startLineNumber, range.startColumn, range.endLineNumber, range.endColumn);
        }
        return new Selection(range.endLineNumber, range.endColumn, range.startLineNumber, range.startColumn);
    }
    ensureValidState(context) {
        this._setState(context, this.modelState, this.viewState);
    }
    setState(context, modelState, viewState) {
        this._setState(context, modelState, viewState);
    }
    _setState(context, modelState, viewState) {
        if (!modelState) {
            if (!viewState) {
                return;
            }
            // We only have the view state => compute the model state
            const selectionStart = context.model.validateRange(context.coordinatesConverter.convertViewRangeToModelRange(viewState.selectionStart));
            const position = context.model.validatePosition(context.coordinatesConverter.convertViewPositionToModelPosition(viewState.position));
            modelState = new SingleCursorState(selectionStart, viewState.selectionStartLeftoverVisibleColumns, position, viewState.leftoverVisibleColumns);
        }
        else {
            // Validate new model state
            const selectionStart = context.model.validateRange(modelState.selectionStart);
            const selectionStartLeftoverVisibleColumns = modelState.selectionStart.equalsRange(selectionStart) ? modelState.selectionStartLeftoverVisibleColumns : 0;
            const position = context.model.validatePosition(modelState.position);
            const leftoverVisibleColumns = modelState.position.equals(position) ? modelState.leftoverVisibleColumns : 0;
            modelState = new SingleCursorState(selectionStart, selectionStartLeftoverVisibleColumns, position, leftoverVisibleColumns);
        }
        if (!viewState) {
            // We only have the model state => compute the view state
            const viewSelectionStart1 = context.coordinatesConverter.convertModelPositionToViewPosition(new Position(modelState.selectionStart.startLineNumber, modelState.selectionStart.startColumn));
            const viewSelectionStart2 = context.coordinatesConverter.convertModelPositionToViewPosition(new Position(modelState.selectionStart.endLineNumber, modelState.selectionStart.endColumn));
            const viewSelectionStart = new Range(viewSelectionStart1.lineNumber, viewSelectionStart1.column, viewSelectionStart2.lineNumber, viewSelectionStart2.column);
            const viewPosition = context.coordinatesConverter.convertModelPositionToViewPosition(modelState.position);
            viewState = new SingleCursorState(viewSelectionStart, modelState.selectionStartLeftoverVisibleColumns, viewPosition, modelState.leftoverVisibleColumns);
        }
        else {
            // Validate new view state
            const viewSelectionStart = context.coordinatesConverter.validateViewRange(viewState.selectionStart, modelState.selectionStart);
            const viewPosition = context.coordinatesConverter.validateViewPosition(viewState.position, modelState.position);
            viewState = new SingleCursorState(viewSelectionStart, modelState.selectionStartLeftoverVisibleColumns, viewPosition, modelState.leftoverVisibleColumns);
        }
        this.modelState = modelState;
        this.viewState = viewState;
        this._updateTrackedRange(context);
    }
}
