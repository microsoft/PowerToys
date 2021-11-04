/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './rulers.css';
import { createFastDomNode } from '../../../../base/browser/fastDomNode.js';
import { ViewPart } from '../../view/viewPart.js';
import { editorRuler } from '../../../common/view/editorColorRegistry.js';
import { registerThemingParticipant } from '../../../../platform/theme/common/themeService.js';
export class Rulers extends ViewPart {
    constructor(context) {
        super(context);
        this.domNode = createFastDomNode(document.createElement('div'));
        this.domNode.setAttribute('role', 'presentation');
        this.domNode.setAttribute('aria-hidden', 'true');
        this.domNode.setClassName('view-rulers');
        this._renderedRulers = [];
        const options = this._context.configuration.options;
        this._rulers = options.get(86 /* rulers */);
        this._typicalHalfwidthCharacterWidth = options.get(38 /* fontInfo */).typicalHalfwidthCharacterWidth;
    }
    dispose() {
        super.dispose();
    }
    // --- begin event handlers
    onConfigurationChanged(e) {
        const options = this._context.configuration.options;
        this._rulers = options.get(86 /* rulers */);
        this._typicalHalfwidthCharacterWidth = options.get(38 /* fontInfo */).typicalHalfwidthCharacterWidth;
        return true;
    }
    onScrollChanged(e) {
        return e.scrollHeightChanged;
    }
    // --- end event handlers
    prepareRender(ctx) {
        // Nothing to read
    }
    _ensureRulersCount() {
        const currentCount = this._renderedRulers.length;
        const desiredCount = this._rulers.length;
        if (currentCount === desiredCount) {
            // Nothing to do
            return;
        }
        if (currentCount < desiredCount) {
            const { tabSize } = this._context.model.getTextModelOptions();
            const rulerWidth = tabSize;
            let addCount = desiredCount - currentCount;
            while (addCount > 0) {
                const node = createFastDomNode(document.createElement('div'));
                node.setClassName('view-ruler');
                node.setWidth(rulerWidth);
                this.domNode.appendChild(node);
                this._renderedRulers.push(node);
                addCount--;
            }
            return;
        }
        let removeCount = currentCount - desiredCount;
        while (removeCount > 0) {
            const node = this._renderedRulers.pop();
            this.domNode.removeChild(node);
            removeCount--;
        }
    }
    render(ctx) {
        this._ensureRulersCount();
        for (let i = 0, len = this._rulers.length; i < len; i++) {
            const node = this._renderedRulers[i];
            const ruler = this._rulers[i];
            node.setBoxShadow(ruler.color ? `1px 0 0 0 ${ruler.color} inset` : ``);
            node.setHeight(Math.min(ctx.scrollHeight, 1000000));
            node.setLeft(ruler.column * this._typicalHalfwidthCharacterWidth);
        }
    }
}
registerThemingParticipant((theme, collector) => {
    const rulerColor = theme.getColor(editorRuler);
    if (rulerColor) {
        collector.addRule(`.monaco-editor .view-ruler { box-shadow: 1px 0 0 0 ${rulerColor} inset; }`);
    }
});
