/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../../nls.js';
import { EditorAction, registerEditorAction } from '../../browser/editorExtensions.js';
import { EditorZoom } from '../../common/config/editorZoom.js';
class EditorFontZoomIn extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.fontZoomIn',
            label: nls.localize('EditorFontZoomIn.label', "Editor Font Zoom In"),
            alias: 'Editor Font Zoom In',
            precondition: undefined
        });
    }
    run(accessor, editor) {
        EditorZoom.setZoomLevel(EditorZoom.getZoomLevel() + 1);
    }
}
class EditorFontZoomOut extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.fontZoomOut',
            label: nls.localize('EditorFontZoomOut.label', "Editor Font Zoom Out"),
            alias: 'Editor Font Zoom Out',
            precondition: undefined
        });
    }
    run(accessor, editor) {
        EditorZoom.setZoomLevel(EditorZoom.getZoomLevel() - 1);
    }
}
class EditorFontZoomReset extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.fontZoomReset',
            label: nls.localize('EditorFontZoomReset.label', "Editor Font Zoom Reset"),
            alias: 'Editor Font Zoom Reset',
            precondition: undefined
        });
    }
    run(accessor, editor) {
        EditorZoom.setZoomLevel(0);
    }
}
registerEditorAction(EditorFontZoomIn);
registerEditorAction(EditorFontZoomOut);
registerEditorAction(EditorFontZoomReset);
