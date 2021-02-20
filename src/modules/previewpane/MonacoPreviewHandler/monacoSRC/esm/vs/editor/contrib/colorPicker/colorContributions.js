/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
// import color detector contribution
import './colorDetector.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { registerEditorContribution } from '../../browser/editorExtensions.js';
import { ModesHoverController } from '../hover/hover.js';
import { Range } from '../../common/core/range.js';
export class ColorContribution extends Disposable {
    constructor(_editor) {
        super();
        this._editor = _editor;
        this._register(_editor.onMouseDown((e) => this.onMouseDown(e)));
    }
    dispose() {
        super.dispose();
    }
    onMouseDown(mouseEvent) {
        var _a;
        const targetType = mouseEvent.target.type;
        if (targetType !== 6 /* CONTENT_TEXT */) {
            return;
        }
        const hoverOnColorDecorator = [...((_a = mouseEvent.target.element) === null || _a === void 0 ? void 0 : _a.classList.values()) || []].find(className => className.startsWith('ced-colorBox'));
        if (!hoverOnColorDecorator) {
            return;
        }
        if (!mouseEvent.target.range) {
            return;
        }
        const hoverController = this._editor.getContribution(ModesHoverController.ID);
        if (!hoverController.isColorPickerVisible()) {
            const range = new Range(mouseEvent.target.range.startLineNumber, mouseEvent.target.range.startColumn + 1, mouseEvent.target.range.endLineNumber, mouseEvent.target.range.endColumn + 1);
            hoverController.showContentHover(range, 0 /* Delayed */, false);
        }
    }
}
ColorContribution.ID = 'editor.contrib.colorContribution'; // ms
registerEditorContribution(ColorContribution.ID, ColorContribution);
