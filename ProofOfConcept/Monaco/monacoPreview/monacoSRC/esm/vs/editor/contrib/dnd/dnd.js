/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './dnd.css';
import { Disposable } from '../../../base/common/lifecycle.js';
import { isMacintosh } from '../../../base/common/platform.js';
import { registerEditorContribution } from '../../browser/editorExtensions.js';
import { Position } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { DragAndDropCommand } from './dragAndDropCommand.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
function hasTriggerModifier(e) {
    if (isMacintosh) {
        return e.altKey;
    }
    else {
        return e.ctrlKey;
    }
}
export class DragAndDropController extends Disposable {
    constructor(editor) {
        super();
        this._editor = editor;
        this._register(this._editor.onMouseDown((e) => this._onEditorMouseDown(e)));
        this._register(this._editor.onMouseUp((e) => this._onEditorMouseUp(e)));
        this._register(this._editor.onMouseDrag((e) => this._onEditorMouseDrag(e)));
        this._register(this._editor.onMouseDrop((e) => this._onEditorMouseDrop(e)));
        this._register(this._editor.onMouseDropCanceled(() => this._onEditorMouseDropCanceled()));
        this._register(this._editor.onKeyDown((e) => this.onEditorKeyDown(e)));
        this._register(this._editor.onKeyUp((e) => this.onEditorKeyUp(e)));
        this._register(this._editor.onDidBlurEditorWidget(() => this.onEditorBlur()));
        this._register(this._editor.onDidBlurEditorText(() => this.onEditorBlur()));
        this._dndDecorationIds = [];
        this._mouseDown = false;
        this._modifierPressed = false;
        this._dragSelection = null;
    }
    onEditorBlur() {
        this._removeDecoration();
        this._dragSelection = null;
        this._mouseDown = false;
        this._modifierPressed = false;
    }
    onEditorKeyDown(e) {
        if (!this._editor.getOption(27 /* dragAndDrop */) || this._editor.getOption(15 /* columnSelection */)) {
            return;
        }
        if (hasTriggerModifier(e)) {
            this._modifierPressed = true;
        }
        if (this._mouseDown && hasTriggerModifier(e)) {
            this._editor.updateOptions({
                mouseStyle: 'copy'
            });
        }
    }
    onEditorKeyUp(e) {
        if (!this._editor.getOption(27 /* dragAndDrop */) || this._editor.getOption(15 /* columnSelection */)) {
            return;
        }
        if (hasTriggerModifier(e)) {
            this._modifierPressed = false;
        }
        if (this._mouseDown && e.keyCode === DragAndDropController.TRIGGER_KEY_VALUE) {
            this._editor.updateOptions({
                mouseStyle: 'default'
            });
        }
    }
    _onEditorMouseDown(mouseEvent) {
        this._mouseDown = true;
    }
    _onEditorMouseUp(mouseEvent) {
        this._mouseDown = false;
        // Whenever users release the mouse, the drag and drop operation should finish and the cursor should revert to text.
        this._editor.updateOptions({
            mouseStyle: 'text'
        });
    }
    _onEditorMouseDrag(mouseEvent) {
        let target = mouseEvent.target;
        if (this._dragSelection === null) {
            const selections = this._editor.getSelections() || [];
            let possibleSelections = selections.filter(selection => target.position && selection.containsPosition(target.position));
            if (possibleSelections.length === 1) {
                this._dragSelection = possibleSelections[0];
            }
            else {
                return;
            }
        }
        if (hasTriggerModifier(mouseEvent.event)) {
            this._editor.updateOptions({
                mouseStyle: 'copy'
            });
        }
        else {
            this._editor.updateOptions({
                mouseStyle: 'default'
            });
        }
        if (target.position) {
            if (this._dragSelection.containsPosition(target.position)) {
                this._removeDecoration();
            }
            else {
                this.showAt(target.position);
            }
        }
    }
    _onEditorMouseDropCanceled() {
        this._editor.updateOptions({
            mouseStyle: 'text'
        });
        this._removeDecoration();
        this._dragSelection = null;
        this._mouseDown = false;
    }
    _onEditorMouseDrop(mouseEvent) {
        if (mouseEvent.target && (this._hitContent(mouseEvent.target) || this._hitMargin(mouseEvent.target)) && mouseEvent.target.position) {
            let newCursorPosition = new Position(mouseEvent.target.position.lineNumber, mouseEvent.target.position.column);
            if (this._dragSelection === null) {
                let newSelections = null;
                if (mouseEvent.event.shiftKey) {
                    let primarySelection = this._editor.getSelection();
                    if (primarySelection) {
                        const { selectionStartLineNumber, selectionStartColumn } = primarySelection;
                        newSelections = [new Selection(selectionStartLineNumber, selectionStartColumn, newCursorPosition.lineNumber, newCursorPosition.column)];
                    }
                }
                else {
                    newSelections = (this._editor.getSelections() || []).map(selection => {
                        if (selection.containsPosition(newCursorPosition)) {
                            return new Selection(newCursorPosition.lineNumber, newCursorPosition.column, newCursorPosition.lineNumber, newCursorPosition.column);
                        }
                        else {
                            return selection;
                        }
                    });
                }
                // Use `mouse` as the source instead of `api` and setting the reason to explicit (to behave like any other mouse operation).
                this._editor.setSelections(newSelections || [], 'mouse', 3 /* Explicit */);
            }
            else if (!this._dragSelection.containsPosition(newCursorPosition) ||
                ((hasTriggerModifier(mouseEvent.event) ||
                    this._modifierPressed) && (this._dragSelection.getEndPosition().equals(newCursorPosition) || this._dragSelection.getStartPosition().equals(newCursorPosition)) // we allow users to paste content beside the selection
                )) {
                this._editor.pushUndoStop();
                this._editor.executeCommand(DragAndDropController.ID, new DragAndDropCommand(this._dragSelection, newCursorPosition, hasTriggerModifier(mouseEvent.event) || this._modifierPressed));
                this._editor.pushUndoStop();
            }
        }
        this._editor.updateOptions({
            mouseStyle: 'text'
        });
        this._removeDecoration();
        this._dragSelection = null;
        this._mouseDown = false;
    }
    showAt(position) {
        let newDecorations = [{
                range: new Range(position.lineNumber, position.column, position.lineNumber, position.column),
                options: DragAndDropController._DECORATION_OPTIONS
            }];
        this._dndDecorationIds = this._editor.deltaDecorations(this._dndDecorationIds, newDecorations);
        this._editor.revealPosition(position, 1 /* Immediate */);
    }
    _removeDecoration() {
        this._dndDecorationIds = this._editor.deltaDecorations(this._dndDecorationIds, []);
    }
    _hitContent(target) {
        return target.type === 6 /* CONTENT_TEXT */ ||
            target.type === 7 /* CONTENT_EMPTY */;
    }
    _hitMargin(target) {
        return target.type === 2 /* GUTTER_GLYPH_MARGIN */ ||
            target.type === 3 /* GUTTER_LINE_NUMBERS */ ||
            target.type === 4 /* GUTTER_LINE_DECORATIONS */;
    }
    dispose() {
        this._removeDecoration();
        this._dragSelection = null;
        this._mouseDown = false;
        this._modifierPressed = false;
        super.dispose();
    }
}
DragAndDropController.ID = 'editor.contrib.dragAndDrop';
DragAndDropController.TRIGGER_KEY_VALUE = isMacintosh ? 6 /* Alt */ : 5 /* Ctrl */;
DragAndDropController._DECORATION_OPTIONS = ModelDecorationOptions.register({
    className: 'dnd-target'
});
registerEditorContribution(DragAndDropController.ID, DragAndDropController);
