/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as dom from './dom.js';
import { IframeUtils } from './iframe.js';
import { StandardMouseEvent } from './mouseEvent.js';
import { DisposableStore } from '../common/lifecycle.js';
export function standardMouseMoveMerger(lastEvent, currentEvent) {
    let ev = new StandardMouseEvent(currentEvent);
    ev.preventDefault();
    return {
        leftButton: ev.leftButton,
        buttons: ev.buttons,
        posx: ev.posx,
        posy: ev.posy
    };
}
export class GlobalMouseMoveMonitor {
    constructor() {
        this._hooks = new DisposableStore();
        this._mouseMoveEventMerger = null;
        this._mouseMoveCallback = null;
        this._onStopCallback = null;
    }
    dispose() {
        this.stopMonitoring(false);
        this._hooks.dispose();
    }
    stopMonitoring(invokeStopCallback, browserEvent) {
        if (!this.isMonitoring()) {
            // Not monitoring
            return;
        }
        // Unhook
        this._hooks.clear();
        this._mouseMoveEventMerger = null;
        this._mouseMoveCallback = null;
        const onStopCallback = this._onStopCallback;
        this._onStopCallback = null;
        if (invokeStopCallback && onStopCallback) {
            onStopCallback(browserEvent);
        }
    }
    isMonitoring() {
        return !!this._mouseMoveEventMerger;
    }
    startMonitoring(initialElement, initialButtons, mouseMoveEventMerger, mouseMoveCallback, onStopCallback) {
        if (this.isMonitoring()) {
            // I am already hooked
            return;
        }
        this._mouseMoveEventMerger = mouseMoveEventMerger;
        this._mouseMoveCallback = mouseMoveCallback;
        this._onStopCallback = onStopCallback;
        const windowChain = IframeUtils.getSameOriginWindowChain();
        const mouseMove = 'mousemove';
        const mouseUp = 'mouseup';
        const listenTo = windowChain.map(element => element.window.document);
        const shadowRoot = dom.getShadowRoot(initialElement);
        if (shadowRoot) {
            listenTo.unshift(shadowRoot);
        }
        for (const element of listenTo) {
            this._hooks.add(dom.addDisposableThrottledListener(element, mouseMove, (data) => {
                if (data.buttons !== initialButtons) {
                    // Buttons state has changed in the meantime
                    this.stopMonitoring(true);
                    return;
                }
                this._mouseMoveCallback(data);
            }, (lastEvent, currentEvent) => this._mouseMoveEventMerger(lastEvent, currentEvent)));
            this._hooks.add(dom.addDisposableListener(element, mouseUp, (e) => this.stopMonitoring(true)));
        }
        if (IframeUtils.hasDifferentOriginAncestor()) {
            let lastSameOriginAncestor = windowChain[windowChain.length - 1];
            // We might miss a mouse up if it happens outside the iframe
            // This one is for Chrome
            this._hooks.add(dom.addDisposableListener(lastSameOriginAncestor.window.document, 'mouseout', (browserEvent) => {
                let e = new StandardMouseEvent(browserEvent);
                if (e.target.tagName.toLowerCase() === 'html') {
                    this.stopMonitoring(true);
                }
            }));
            // This one is for FF
            this._hooks.add(dom.addDisposableListener(lastSameOriginAncestor.window.document, 'mouseover', (browserEvent) => {
                let e = new StandardMouseEvent(browserEvent);
                if (e.target.tagName.toLowerCase() === 'html') {
                    this.stopMonitoring(true);
                }
            }));
            // This one is for IE
            this._hooks.add(dom.addDisposableListener(lastSameOriginAncestor.window.document.body, 'mouseleave', (browserEvent) => {
                this.stopMonitoring(true);
            }));
        }
    }
}
