/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { CursorState } from './cursorCommon.js';
import { OneCursor } from './oneCursor.js';
import { Selection } from '../core/selection.js';
export class CursorCollection {
    constructor(context) {
        this.context = context;
        this.primaryCursor = new OneCursor(context);
        this.secondaryCursors = [];
        this.lastAddedCursorIndex = 0;
    }
    dispose() {
        this.primaryCursor.dispose(this.context);
        this.killSecondaryCursors();
    }
    startTrackingSelections() {
        this.primaryCursor.startTrackingSelection(this.context);
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            this.secondaryCursors[i].startTrackingSelection(this.context);
        }
    }
    stopTrackingSelections() {
        this.primaryCursor.stopTrackingSelection(this.context);
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            this.secondaryCursors[i].stopTrackingSelection(this.context);
        }
    }
    updateContext(context) {
        this.context = context;
    }
    ensureValidState() {
        this.primaryCursor.ensureValidState(this.context);
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            this.secondaryCursors[i].ensureValidState(this.context);
        }
    }
    readSelectionFromMarkers() {
        let result = [];
        result[0] = this.primaryCursor.readSelectionFromMarkers(this.context);
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            result[i + 1] = this.secondaryCursors[i].readSelectionFromMarkers(this.context);
        }
        return result;
    }
    getAll() {
        let result = [];
        result[0] = this.primaryCursor.asCursorState();
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            result[i + 1] = this.secondaryCursors[i].asCursorState();
        }
        return result;
    }
    getViewPositions() {
        let result = [];
        result[0] = this.primaryCursor.viewState.position;
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            result[i + 1] = this.secondaryCursors[i].viewState.position;
        }
        return result;
    }
    getTopMostViewPosition() {
        let result = this.primaryCursor.viewState.position;
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            const viewPosition = this.secondaryCursors[i].viewState.position;
            if (viewPosition.isBefore(result)) {
                result = viewPosition;
            }
        }
        return result;
    }
    getBottomMostViewPosition() {
        let result = this.primaryCursor.viewState.position;
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            const viewPosition = this.secondaryCursors[i].viewState.position;
            if (result.isBeforeOrEqual(viewPosition)) {
                result = viewPosition;
            }
        }
        return result;
    }
    getSelections() {
        let result = [];
        result[0] = this.primaryCursor.modelState.selection;
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            result[i + 1] = this.secondaryCursors[i].modelState.selection;
        }
        return result;
    }
    getViewSelections() {
        let result = [];
        result[0] = this.primaryCursor.viewState.selection;
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            result[i + 1] = this.secondaryCursors[i].viewState.selection;
        }
        return result;
    }
    setSelections(selections) {
        this.setStates(CursorState.fromModelSelections(selections));
    }
    getPrimaryCursor() {
        return this.primaryCursor.asCursorState();
    }
    setStates(states) {
        if (states === null) {
            return;
        }
        this.primaryCursor.setState(this.context, states[0].modelState, states[0].viewState);
        this._setSecondaryStates(states.slice(1));
    }
    /**
     * Creates or disposes secondary cursors as necessary to match the number of `secondarySelections`.
     */
    _setSecondaryStates(secondaryStates) {
        const secondaryCursorsLength = this.secondaryCursors.length;
        const secondaryStatesLength = secondaryStates.length;
        if (secondaryCursorsLength < secondaryStatesLength) {
            let createCnt = secondaryStatesLength - secondaryCursorsLength;
            for (let i = 0; i < createCnt; i++) {
                this._addSecondaryCursor();
            }
        }
        else if (secondaryCursorsLength > secondaryStatesLength) {
            let removeCnt = secondaryCursorsLength - secondaryStatesLength;
            for (let i = 0; i < removeCnt; i++) {
                this._removeSecondaryCursor(this.secondaryCursors.length - 1);
            }
        }
        for (let i = 0; i < secondaryStatesLength; i++) {
            this.secondaryCursors[i].setState(this.context, secondaryStates[i].modelState, secondaryStates[i].viewState);
        }
    }
    killSecondaryCursors() {
        this._setSecondaryStates([]);
    }
    _addSecondaryCursor() {
        this.secondaryCursors.push(new OneCursor(this.context));
        this.lastAddedCursorIndex = this.secondaryCursors.length;
    }
    getLastAddedCursorIndex() {
        if (this.secondaryCursors.length === 0 || this.lastAddedCursorIndex === 0) {
            return 0;
        }
        return this.lastAddedCursorIndex;
    }
    _removeSecondaryCursor(removeIndex) {
        if (this.lastAddedCursorIndex >= removeIndex + 1) {
            this.lastAddedCursorIndex--;
        }
        this.secondaryCursors[removeIndex].dispose(this.context);
        this.secondaryCursors.splice(removeIndex, 1);
    }
    _getAll() {
        let result = [];
        result[0] = this.primaryCursor;
        for (let i = 0, len = this.secondaryCursors.length; i < len; i++) {
            result[i + 1] = this.secondaryCursors[i];
        }
        return result;
    }
    normalize() {
        if (this.secondaryCursors.length === 0) {
            return;
        }
        let cursors = this._getAll();
        let sortedCursors = [];
        for (let i = 0, len = cursors.length; i < len; i++) {
            sortedCursors.push({
                index: i,
                selection: cursors[i].modelState.selection,
            });
        }
        sortedCursors.sort((a, b) => {
            if (a.selection.startLineNumber === b.selection.startLineNumber) {
                return a.selection.startColumn - b.selection.startColumn;
            }
            return a.selection.startLineNumber - b.selection.startLineNumber;
        });
        for (let sortedCursorIndex = 0; sortedCursorIndex < sortedCursors.length - 1; sortedCursorIndex++) {
            const current = sortedCursors[sortedCursorIndex];
            const next = sortedCursors[sortedCursorIndex + 1];
            const currentSelection = current.selection;
            const nextSelection = next.selection;
            if (!this.context.cursorConfig.multiCursorMergeOverlapping) {
                continue;
            }
            let shouldMergeCursors;
            if (nextSelection.isEmpty() || currentSelection.isEmpty()) {
                // Merge touching cursors if one of them is collapsed
                shouldMergeCursors = nextSelection.getStartPosition().isBeforeOrEqual(currentSelection.getEndPosition());
            }
            else {
                // Merge only overlapping cursors (i.e. allow touching ranges)
                shouldMergeCursors = nextSelection.getStartPosition().isBefore(currentSelection.getEndPosition());
            }
            if (shouldMergeCursors) {
                const winnerSortedCursorIndex = current.index < next.index ? sortedCursorIndex : sortedCursorIndex + 1;
                const looserSortedCursorIndex = current.index < next.index ? sortedCursorIndex + 1 : sortedCursorIndex;
                const looserIndex = sortedCursors[looserSortedCursorIndex].index;
                const winnerIndex = sortedCursors[winnerSortedCursorIndex].index;
                const looserSelection = sortedCursors[looserSortedCursorIndex].selection;
                const winnerSelection = sortedCursors[winnerSortedCursorIndex].selection;
                if (!looserSelection.equalsSelection(winnerSelection)) {
                    const resultingRange = looserSelection.plusRange(winnerSelection);
                    const looserSelectionIsLTR = (looserSelection.selectionStartLineNumber === looserSelection.startLineNumber && looserSelection.selectionStartColumn === looserSelection.startColumn);
                    const winnerSelectionIsLTR = (winnerSelection.selectionStartLineNumber === winnerSelection.startLineNumber && winnerSelection.selectionStartColumn === winnerSelection.startColumn);
                    // Give more importance to the last added cursor (think Ctrl-dragging + hitting another cursor)
                    let resultingSelectionIsLTR;
                    if (looserIndex === this.lastAddedCursorIndex) {
                        resultingSelectionIsLTR = looserSelectionIsLTR;
                        this.lastAddedCursorIndex = winnerIndex;
                    }
                    else {
                        // Winner takes it all
                        resultingSelectionIsLTR = winnerSelectionIsLTR;
                    }
                    let resultingSelection;
                    if (resultingSelectionIsLTR) {
                        resultingSelection = new Selection(resultingRange.startLineNumber, resultingRange.startColumn, resultingRange.endLineNumber, resultingRange.endColumn);
                    }
                    else {
                        resultingSelection = new Selection(resultingRange.endLineNumber, resultingRange.endColumn, resultingRange.startLineNumber, resultingRange.startColumn);
                    }
                    sortedCursors[winnerSortedCursorIndex].selection = resultingSelection;
                    const resultingState = CursorState.fromModelSelection(resultingSelection);
                    cursors[winnerIndex].setState(this.context, resultingState.modelState, resultingState.viewState);
                }
                for (const sortedCursor of sortedCursors) {
                    if (sortedCursor.index > looserIndex) {
                        sortedCursor.index--;
                    }
                }
                cursors.splice(looserIndex, 1);
                sortedCursors.splice(looserSortedCursorIndex, 1);
                this._removeSecondaryCursor(looserIndex - 1);
                sortedCursorIndex--;
            }
        }
    }
}
