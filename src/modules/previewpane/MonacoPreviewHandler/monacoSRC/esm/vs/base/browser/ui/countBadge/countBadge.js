/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './countBadge.css';
import { $, append } from '../../dom.js';
import { format } from '../../../common/strings.js';
import { Color } from '../../../common/color.js';
import { mixin } from '../../../common/objects.js';
const defaultOpts = {
    badgeBackground: Color.fromHex('#4D4D4D'),
    badgeForeground: Color.fromHex('#FFFFFF')
};
export class CountBadge {
    constructor(container, options) {
        this.count = 0;
        this.options = options || Object.create(null);
        mixin(this.options, defaultOpts, false);
        this.badgeBackground = this.options.badgeBackground;
        this.badgeForeground = this.options.badgeForeground;
        this.badgeBorder = this.options.badgeBorder;
        this.element = append(container, $('.monaco-count-badge'));
        this.countFormat = this.options.countFormat || '{0}';
        this.titleFormat = this.options.titleFormat || '';
        this.setCount(this.options.count || 0);
    }
    setCount(count) {
        this.count = count;
        this.render();
    }
    setTitleFormat(titleFormat) {
        this.titleFormat = titleFormat;
        this.render();
    }
    render() {
        this.element.textContent = format(this.countFormat, this.count);
        this.element.title = format(this.titleFormat, this.count);
        this.applyStyles();
    }
    style(styles) {
        this.badgeBackground = styles.badgeBackground;
        this.badgeForeground = styles.badgeForeground;
        this.badgeBorder = styles.badgeBorder;
        this.applyStyles();
    }
    applyStyles() {
        if (this.element) {
            const background = this.badgeBackground ? this.badgeBackground.toString() : '';
            const foreground = this.badgeForeground ? this.badgeForeground.toString() : '';
            const border = this.badgeBorder ? this.badgeBorder.toString() : '';
            this.element.style.backgroundColor = background;
            this.element.style.color = foreground;
            this.element.style.borderWidth = border ? '1px' : '';
            this.element.style.borderStyle = border ? 'solid' : '';
            this.element.style.borderColor = border;
        }
    }
}
