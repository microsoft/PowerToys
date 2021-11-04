/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './scrollDecoration.css';
import { createFastDomNode } from '../../../../base/browser/fastDomNode.js';
import { ViewPart } from '../../view/viewPart.js';
import { scrollbarShadow } from '../../../../platform/theme/common/colorRegistry.js';
import { registerThemingParticipant } from '../../../../platform/theme/common/themeService.js';
export class ScrollDecorationViewPart extends ViewPart {
    constructor(context) {
        super(context);
        this._scrollTop = 0;
        this._width = 0;
        this._updateWidth();
        this._shouldShow = false;
        const options = this._context.configuration.options;
        const scrollbar = options.get(87 /* scrollbar */);
        this._useShadows = scrollbar.useShadows;
        this._domNode = createFastDomNode(document.createElement('div'));
        this._domNode.setAttribute('role', 'presentation');
        this._domNode.setAttribute('aria-hidden', 'true');
    }
    dispose() {
        super.dispose();
    }
    _updateShouldShow() {
        const newShouldShow = (this._useShadows && this._scrollTop > 0);
        if (this._shouldShow !== newShouldShow) {
            this._shouldShow = newShouldShow;
            return true;
        }
        return false;
    }
    getDomNode() {
        return this._domNode;
    }
    _updateWidth() {
        const options = this._context.configuration.options;
        const layoutInfo = options.get(124 /* layoutInfo */);
        if (layoutInfo.minimap.renderMinimap === 0 || (layoutInfo.minimap.minimapWidth > 0 && layoutInfo.minimap.minimapLeft === 0)) {
            this._width = layoutInfo.width;
        }
        else {
            this._width = layoutInfo.width - layoutInfo.minimap.minimapWidth - layoutInfo.verticalScrollbarWidth;
        }
    }
    // --- begin event handlers
    onConfigurationChanged(e) {
        const options = this._context.configuration.options;
        const scrollbar = options.get(87 /* scrollbar */);
        this._useShadows = scrollbar.useShadows;
        this._updateWidth();
        this._updateShouldShow();
        return true;
    }
    onScrollChanged(e) {
        this._scrollTop = e.scrollTop;
        return this._updateShouldShow();
    }
    // --- end event handlers
    prepareRender(ctx) {
        // Nothing to read
    }
    render(ctx) {
        this._domNode.setWidth(this._width);
        this._domNode.setClassName(this._shouldShow ? 'scroll-decoration' : '');
    }
}
registerThemingParticipant((theme, collector) => {
    const shadow = theme.getColor(scrollbarShadow);
    if (shadow) {
        collector.addRule(`.monaco-editor .scroll-decoration { box-shadow: ${shadow} 0 6px 6px -6px inset; }`);
    }
});
