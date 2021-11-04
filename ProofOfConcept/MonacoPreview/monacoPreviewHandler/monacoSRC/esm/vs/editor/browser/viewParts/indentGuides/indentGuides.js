/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './indentGuides.css';
import { DynamicViewOverlay } from '../../view/dynamicViewOverlay.js';
import { Position } from '../../../common/core/position.js';
import { editorActiveIndentGuides, editorIndentGuides } from '../../../common/view/editorColorRegistry.js';
import { registerThemingParticipant } from '../../../../platform/theme/common/themeService.js';
export class IndentGuidesOverlay extends DynamicViewOverlay {
    constructor(context) {
        super();
        this._context = context;
        this._primaryLineNumber = 0;
        const options = this._context.configuration.options;
        const wrappingInfo = options.get(125 /* wrappingInfo */);
        const fontInfo = options.get(38 /* fontInfo */);
        this._lineHeight = options.get(53 /* lineHeight */);
        this._spaceWidth = fontInfo.spaceWidth;
        this._enabled = options.get(78 /* renderIndentGuides */);
        this._activeIndentEnabled = options.get(47 /* highlightActiveIndentGuide */);
        this._maxIndentLeft = wrappingInfo.wrappingColumn === -1 ? -1 : (wrappingInfo.wrappingColumn * fontInfo.typicalHalfwidthCharacterWidth);
        this._renderResult = null;
        this._context.addEventHandler(this);
    }
    dispose() {
        this._context.removeEventHandler(this);
        this._renderResult = null;
        super.dispose();
    }
    // --- begin event handlers
    onConfigurationChanged(e) {
        const options = this._context.configuration.options;
        const wrappingInfo = options.get(125 /* wrappingInfo */);
        const fontInfo = options.get(38 /* fontInfo */);
        this._lineHeight = options.get(53 /* lineHeight */);
        this._spaceWidth = fontInfo.spaceWidth;
        this._enabled = options.get(78 /* renderIndentGuides */);
        this._activeIndentEnabled = options.get(47 /* highlightActiveIndentGuide */);
        this._maxIndentLeft = wrappingInfo.wrappingColumn === -1 ? -1 : (wrappingInfo.wrappingColumn * fontInfo.typicalHalfwidthCharacterWidth);
        return true;
    }
    onCursorStateChanged(e) {
        const selection = e.selections[0];
        const newPrimaryLineNumber = selection.isEmpty() ? selection.positionLineNumber : 0;
        if (this._primaryLineNumber !== newPrimaryLineNumber) {
            this._primaryLineNumber = newPrimaryLineNumber;
            return true;
        }
        return false;
    }
    onDecorationsChanged(e) {
        // true for inline decorations
        return true;
    }
    onFlushed(e) {
        return true;
    }
    onLinesChanged(e) {
        return true;
    }
    onLinesDeleted(e) {
        return true;
    }
    onLinesInserted(e) {
        return true;
    }
    onScrollChanged(e) {
        return e.scrollTopChanged; // || e.scrollWidthChanged;
    }
    onZonesChanged(e) {
        return true;
    }
    onLanguageConfigurationChanged(e) {
        return true;
    }
    // --- end event handlers
    prepareRender(ctx) {
        if (!this._enabled) {
            this._renderResult = null;
            return;
        }
        const visibleStartLineNumber = ctx.visibleRange.startLineNumber;
        const visibleEndLineNumber = ctx.visibleRange.endLineNumber;
        const { indentSize } = this._context.model.getTextModelOptions();
        const indentWidth = indentSize * this._spaceWidth;
        const scrollWidth = ctx.scrollWidth;
        const lineHeight = this._lineHeight;
        const indents = this._context.model.getLinesIndentGuides(visibleStartLineNumber, visibleEndLineNumber);
        let activeIndentStartLineNumber = 0;
        let activeIndentEndLineNumber = 0;
        let activeIndentLevel = 0;
        if (this._activeIndentEnabled && this._primaryLineNumber) {
            const activeIndentInfo = this._context.model.getActiveIndentGuide(this._primaryLineNumber, visibleStartLineNumber, visibleEndLineNumber);
            activeIndentStartLineNumber = activeIndentInfo.startLineNumber;
            activeIndentEndLineNumber = activeIndentInfo.endLineNumber;
            activeIndentLevel = activeIndentInfo.indent;
        }
        const output = [];
        for (let lineNumber = visibleStartLineNumber; lineNumber <= visibleEndLineNumber; lineNumber++) {
            const containsActiveIndentGuide = (activeIndentStartLineNumber <= lineNumber && lineNumber <= activeIndentEndLineNumber);
            const lineIndex = lineNumber - visibleStartLineNumber;
            const indent = indents[lineIndex];
            let result = '';
            if (indent >= 1) {
                const leftMostVisiblePosition = ctx.visibleRangeForPosition(new Position(lineNumber, 1));
                let left = leftMostVisiblePosition ? leftMostVisiblePosition.left : 0;
                for (let i = 1; i <= indent; i++) {
                    const className = (containsActiveIndentGuide && i === activeIndentLevel ? 'cigra' : 'cigr');
                    result += `<div class="${className}" style="left:${left}px;height:${lineHeight}px;width:${indentWidth}px"></div>`;
                    left += indentWidth;
                    if (left > scrollWidth || (this._maxIndentLeft > 0 && left > this._maxIndentLeft)) {
                        break;
                    }
                }
            }
            output[lineIndex] = result;
        }
        this._renderResult = output;
    }
    render(startLineNumber, lineNumber) {
        if (!this._renderResult) {
            return '';
        }
        const lineIndex = lineNumber - startLineNumber;
        if (lineIndex < 0 || lineIndex >= this._renderResult.length) {
            return '';
        }
        return this._renderResult[lineIndex];
    }
}
registerThemingParticipant((theme, collector) => {
    const editorIndentGuidesColor = theme.getColor(editorIndentGuides);
    if (editorIndentGuidesColor) {
        collector.addRule(`.monaco-editor .lines-content .cigr { box-shadow: 1px 0 0 0 ${editorIndentGuidesColor} inset; }`);
    }
    const editorActiveIndentGuidesColor = theme.getColor(editorActiveIndentGuides) || editorIndentGuidesColor;
    if (editorActiveIndentGuidesColor) {
        collector.addRule(`.monaco-editor .lines-content .cigra { box-shadow: 1px 0 0 0 ${editorActiveIndentGuidesColor} inset; }`);
    }
});
