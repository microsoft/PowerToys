/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './progressbar.css';
import { Disposable } from '../../../common/lifecycle.js';
import { Color } from '../../../common/color.js';
import { mixin } from '../../../common/objects.js';
import { show } from '../../dom.js';
import { RunOnceScheduler } from '../../../common/async.js';
const CSS_DONE = 'done';
const CSS_ACTIVE = 'active';
const CSS_INFINITE = 'infinite';
const CSS_DISCRETE = 'discrete';
const defaultOpts = {
    progressBarBackground: Color.fromHex('#0E70C0')
};
/**
 * A progress bar with support for infinite or discrete progress.
 */
export class ProgressBar extends Disposable {
    constructor(container, options) {
        super();
        this.options = options || Object.create(null);
        mixin(this.options, defaultOpts, false);
        this.workedVal = 0;
        this.progressBarBackground = this.options.progressBarBackground;
        this._register(this.showDelayedScheduler = new RunOnceScheduler(() => show(this.element), 0));
        this.create(container);
    }
    create(container) {
        this.element = document.createElement('div');
        this.element.classList.add('monaco-progress-container');
        this.element.setAttribute('role', 'progressbar');
        this.element.setAttribute('aria-valuemin', '0');
        container.appendChild(this.element);
        this.bit = document.createElement('div');
        this.bit.classList.add('progress-bit');
        this.element.appendChild(this.bit);
        this.applyStyles();
    }
    off() {
        this.bit.style.width = 'inherit';
        this.bit.style.opacity = '1';
        this.element.classList.remove(CSS_ACTIVE, CSS_INFINITE, CSS_DISCRETE);
        this.workedVal = 0;
        this.totalWork = undefined;
    }
    /**
     * Stops the progressbar from showing any progress instantly without fading out.
     */
    stop() {
        return this.doDone(false);
    }
    doDone(delayed) {
        this.element.classList.add(CSS_DONE);
        // let it grow to 100% width and hide afterwards
        if (!this.element.classList.contains(CSS_INFINITE)) {
            this.bit.style.width = 'inherit';
            if (delayed) {
                setTimeout(() => this.off(), 200);
            }
            else {
                this.off();
            }
        }
        // let it fade out and hide afterwards
        else {
            this.bit.style.opacity = '0';
            if (delayed) {
                setTimeout(() => this.off(), 200);
            }
            else {
                this.off();
            }
        }
        return this;
    }
    /**
     * Use this mode to indicate progress that has no total number of work units.
     */
    infinite() {
        this.bit.style.width = '2%';
        this.bit.style.opacity = '1';
        this.element.classList.remove(CSS_DISCRETE, CSS_DONE);
        this.element.classList.add(CSS_ACTIVE, CSS_INFINITE);
        return this;
    }
    getContainer() {
        return this.element;
    }
    style(styles) {
        this.progressBarBackground = styles.progressBarBackground;
        this.applyStyles();
    }
    applyStyles() {
        if (this.bit) {
            const background = this.progressBarBackground ? this.progressBarBackground.toString() : '';
            this.bit.style.backgroundColor = background;
        }
    }
}
