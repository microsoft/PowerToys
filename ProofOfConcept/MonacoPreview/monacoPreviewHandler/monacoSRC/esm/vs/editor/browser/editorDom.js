/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as dom from '../../base/browser/dom.js';
import { GlobalMouseMoveMonitor } from '../../base/browser/globalMouseMoveMonitor.js';
import { StandardMouseEvent } from '../../base/browser/mouseEvent.js';
import { Disposable } from '../../base/common/lifecycle.js';
/**
 * Coordinates relative to the whole document (e.g. mouse event's pageX and pageY)
 */
export class PageCoordinates {
    constructor(x, y) {
        this.x = x;
        this.y = y;
    }
    toClientCoordinates() {
        return new ClientCoordinates(this.x - dom.StandardWindow.scrollX, this.y - dom.StandardWindow.scrollY);
    }
}
/**
 * Coordinates within the application's client area (i.e. origin is document's scroll position).
 *
 * For example, clicking in the top-left corner of the client area will
 * always result in a mouse event with a client.x value of 0, regardless
 * of whether the page is scrolled horizontally.
 */
export class ClientCoordinates {
    constructor(clientX, clientY) {
        this.clientX = clientX;
        this.clientY = clientY;
    }
    toPageCoordinates() {
        return new PageCoordinates(this.clientX + dom.StandardWindow.scrollX, this.clientY + dom.StandardWindow.scrollY);
    }
}
/**
 * The position of the editor in the page.
 */
export class EditorPagePosition {
    constructor(x, y, width, height) {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }
}
export function createEditorPagePosition(editorViewDomNode) {
    const editorPos = dom.getDomNodePagePosition(editorViewDomNode);
    return new EditorPagePosition(editorPos.left, editorPos.top, editorPos.width, editorPos.height);
}
export class EditorMouseEvent extends StandardMouseEvent {
    constructor(e, editorViewDomNode) {
        super(e);
        this.pos = new PageCoordinates(this.posx, this.posy);
        this.editorPos = createEditorPagePosition(editorViewDomNode);
    }
}
export class EditorMouseEventFactory {
    constructor(editorViewDomNode) {
        this._editorViewDomNode = editorViewDomNode;
    }
    _create(e) {
        return new EditorMouseEvent(e, this._editorViewDomNode);
    }
    onContextMenu(target, callback) {
        return dom.addDisposableListener(target, 'contextmenu', (e) => {
            callback(this._create(e));
        });
    }
    onMouseUp(target, callback) {
        return dom.addDisposableListener(target, 'mouseup', (e) => {
            callback(this._create(e));
        });
    }
    onMouseDown(target, callback) {
        return dom.addDisposableListener(target, 'mousedown', (e) => {
            callback(this._create(e));
        });
    }
    onMouseLeave(target, callback) {
        return dom.addDisposableNonBubblingMouseOutListener(target, (e) => {
            callback(this._create(e));
        });
    }
    onMouseMoveThrottled(target, callback, merger, minimumTimeMs) {
        const myMerger = (lastEvent, currentEvent) => {
            return merger(lastEvent, this._create(currentEvent));
        };
        return dom.addDisposableThrottledListener(target, 'mousemove', callback, myMerger, minimumTimeMs);
    }
}
export class EditorPointerEventFactory {
    constructor(editorViewDomNode) {
        this._editorViewDomNode = editorViewDomNode;
    }
    _create(e) {
        return new EditorMouseEvent(e, this._editorViewDomNode);
    }
    onPointerUp(target, callback) {
        return dom.addDisposableListener(target, 'pointerup', (e) => {
            callback(this._create(e));
        });
    }
    onPointerDown(target, callback) {
        return dom.addDisposableListener(target, 'pointerdown', (e) => {
            callback(this._create(e));
        });
    }
    onPointerLeave(target, callback) {
        return dom.addDisposableNonBubblingPointerOutListener(target, (e) => {
            callback(this._create(e));
        });
    }
    onPointerMoveThrottled(target, callback, merger, minimumTimeMs) {
        const myMerger = (lastEvent, currentEvent) => {
            return merger(lastEvent, this._create(currentEvent));
        };
        return dom.addDisposableThrottledListener(target, 'pointermove', callback, myMerger, minimumTimeMs);
    }
}
export class GlobalEditorMouseMoveMonitor extends Disposable {
    constructor(editorViewDomNode) {
        super();
        this._editorViewDomNode = editorViewDomNode;
        this._globalMouseMoveMonitor = this._register(new GlobalMouseMoveMonitor());
        this._keydownListener = null;
    }
    startMonitoring(initialElement, initialButtons, merger, mouseMoveCallback, onStopCallback) {
        // Add a <<capture>> keydown event listener that will cancel the monitoring
        // if something other than a modifier key is pressed
        this._keydownListener = dom.addStandardDisposableListener(document, 'keydown', (e) => {
            const kb = e.toKeybinding();
            if (kb.isModifierKey()) {
                // Allow modifier keys
                return;
            }
            this._globalMouseMoveMonitor.stopMonitoring(true, e.browserEvent);
        }, true);
        const myMerger = (lastEvent, currentEvent) => {
            return merger(lastEvent, new EditorMouseEvent(currentEvent, this._editorViewDomNode));
        };
        this._globalMouseMoveMonitor.startMonitoring(initialElement, initialButtons, myMerger, mouseMoveCallback, (e) => {
            this._keydownListener.dispose();
            onStopCallback(e);
        });
    }
}
