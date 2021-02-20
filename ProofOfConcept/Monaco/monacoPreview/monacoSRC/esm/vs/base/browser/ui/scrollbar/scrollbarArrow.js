/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { GlobalMouseMoveMonitor, standardMouseMoveMerger } from '../../globalMouseMoveMonitor.js';
import { Widget } from '../widget.js';
import { IntervalTimer, TimeoutTimer } from '../../../common/async.js';
/**
 * The arrow image size.
 */
export const ARROW_IMG_SIZE = 11;
export class ScrollbarArrow extends Widget {
    constructor(opts) {
        super();
        this._onActivate = opts.onActivate;
        this.bgDomNode = document.createElement('div');
        this.bgDomNode.className = 'arrow-background';
        this.bgDomNode.style.position = 'absolute';
        this.bgDomNode.style.width = opts.bgWidth + 'px';
        this.bgDomNode.style.height = opts.bgHeight + 'px';
        if (typeof opts.top !== 'undefined') {
            this.bgDomNode.style.top = '0px';
        }
        if (typeof opts.left !== 'undefined') {
            this.bgDomNode.style.left = '0px';
        }
        if (typeof opts.bottom !== 'undefined') {
            this.bgDomNode.style.bottom = '0px';
        }
        if (typeof opts.right !== 'undefined') {
            this.bgDomNode.style.right = '0px';
        }
        this.domNode = document.createElement('div');
        this.domNode.className = opts.className;
        this.domNode.classList.add(...opts.icon.classNamesArray);
        this.domNode.style.position = 'absolute';
        this.domNode.style.width = ARROW_IMG_SIZE + 'px';
        this.domNode.style.height = ARROW_IMG_SIZE + 'px';
        if (typeof opts.top !== 'undefined') {
            this.domNode.style.top = opts.top + 'px';
        }
        if (typeof opts.left !== 'undefined') {
            this.domNode.style.left = opts.left + 'px';
        }
        if (typeof opts.bottom !== 'undefined') {
            this.domNode.style.bottom = opts.bottom + 'px';
        }
        if (typeof opts.right !== 'undefined') {
            this.domNode.style.right = opts.right + 'px';
        }
        this._mouseMoveMonitor = this._register(new GlobalMouseMoveMonitor());
        this.onmousedown(this.bgDomNode, (e) => this._arrowMouseDown(e));
        this.onmousedown(this.domNode, (e) => this._arrowMouseDown(e));
        this._mousedownRepeatTimer = this._register(new IntervalTimer());
        this._mousedownScheduleRepeatTimer = this._register(new TimeoutTimer());
    }
    _arrowMouseDown(e) {
        const scheduleRepeater = () => {
            this._mousedownRepeatTimer.cancelAndSet(() => this._onActivate(), 1000 / 24);
        };
        this._onActivate();
        this._mousedownRepeatTimer.cancel();
        this._mousedownScheduleRepeatTimer.cancelAndSet(scheduleRepeater, 200);
        this._mouseMoveMonitor.startMonitoring(e.target, e.buttons, standardMouseMoveMerger, (mouseMoveData) => {
            /* Intentional empty */
        }, () => {
            this._mousedownRepeatTimer.cancel();
            this._mousedownScheduleRepeatTimer.cancel();
        });
        e.preventDefault();
    }
}
