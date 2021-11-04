/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './sash.css';
import { dispose, Disposable, DisposableStore } from '../../../common/lifecycle.js';
import { isMacintosh } from '../../../common/platform.js';
import * as types from '../../../common/types.js';
import { EventType, Gesture } from '../../touch.js';
import { StandardMouseEvent } from '../../mouseEvent.js';
import { Emitter } from '../../../common/event.js';
import { getElementsByTagName, EventHelper, createStyleSheet, addDisposableListener, append, $ } from '../../dom.js';
import { domEvent } from '../../event.js';
const DEBUG = false;
export var OrthogonalEdge;
(function (OrthogonalEdge) {
    OrthogonalEdge["North"] = "north";
    OrthogonalEdge["South"] = "south";
    OrthogonalEdge["East"] = "east";
    OrthogonalEdge["West"] = "west";
})(OrthogonalEdge || (OrthogonalEdge = {}));
let globalSize = 4;
const onDidChangeGlobalSize = new Emitter();
export class Sash extends Disposable {
    constructor(container, layoutProvider, options) {
        super();
        this._state = 3 /* Enabled */;
        this._onDidEnablementChange = this._register(new Emitter());
        this.onDidEnablementChange = this._onDidEnablementChange.event;
        this._onDidStart = this._register(new Emitter());
        this.onDidStart = this._onDidStart.event;
        this._onDidChange = this._register(new Emitter());
        this.onDidChange = this._onDidChange.event;
        this._onDidReset = this._register(new Emitter());
        this.onDidReset = this._onDidReset.event;
        this._onDidEnd = this._register(new Emitter());
        this.onDidEnd = this._onDidEnd.event;
        this.linkedSash = undefined;
        this.orthogonalStartSashDisposables = this._register(new DisposableStore());
        this.orthogonalEndSashDisposables = this._register(new DisposableStore());
        this.el = append(container, $('.monaco-sash'));
        if (options.orthogonalEdge) {
            this.el.classList.add(`orthogonal-edge-${options.orthogonalEdge}`);
        }
        if (isMacintosh) {
            this.el.classList.add('mac');
        }
        this._register(domEvent(this.el, 'mousedown')(this.onMouseDown, this));
        this._register(domEvent(this.el, 'dblclick')(this.onMouseDoubleClick, this));
        this._register(Gesture.addTarget(this.el));
        this._register(domEvent(this.el, EventType.Start)(this.onTouchStart, this));
        if (typeof options.size === 'number') {
            this.size = options.size;
            if (options.orientation === 0 /* VERTICAL */) {
                this.el.style.width = `${this.size}px`;
            }
            else {
                this.el.style.height = `${this.size}px`;
            }
        }
        else {
            this.size = globalSize;
            this._register(onDidChangeGlobalSize.event(size => {
                this.size = size;
                this.layout();
            }));
        }
        this.hidden = false;
        this.layoutProvider = layoutProvider;
        this.orthogonalStartSash = options.orthogonalStartSash;
        this.orthogonalEndSash = options.orthogonalEndSash;
        this.orientation = options.orientation || 0 /* VERTICAL */;
        if (this.orientation === 1 /* HORIZONTAL */) {
            this.el.classList.add('horizontal');
            this.el.classList.remove('vertical');
        }
        else {
            this.el.classList.remove('horizontal');
            this.el.classList.add('vertical');
        }
        this.el.classList.toggle('debug', DEBUG);
        this.layout();
    }
    get state() { return this._state; }
    set state(state) {
        if (this._state === state) {
            return;
        }
        this.el.classList.toggle('disabled', state === 0 /* Disabled */);
        this.el.classList.toggle('minimum', state === 1 /* Minimum */);
        this.el.classList.toggle('maximum', state === 2 /* Maximum */);
        this._state = state;
        this._onDidEnablementChange.fire(state);
    }
    get orthogonalStartSash() { return this._orthogonalStartSash; }
    set orthogonalStartSash(sash) {
        this.orthogonalStartSashDisposables.clear();
        if (sash) {
            this.orthogonalStartSashDisposables.add(sash.onDidEnablementChange(this.onOrthogonalStartSashEnablementChange, this));
            this.onOrthogonalStartSashEnablementChange(sash.state);
        }
        else {
            this.onOrthogonalStartSashEnablementChange(0 /* Disabled */);
        }
        this._orthogonalStartSash = sash;
    }
    get orthogonalEndSash() { return this._orthogonalEndSash; }
    set orthogonalEndSash(sash) {
        this.orthogonalEndSashDisposables.clear();
        if (sash) {
            this.orthogonalEndSashDisposables.add(sash.onDidEnablementChange(this.onOrthogonalEndSashEnablementChange, this));
            this.onOrthogonalEndSashEnablementChange(sash.state);
        }
        else {
            this.onOrthogonalEndSashEnablementChange(0 /* Disabled */);
        }
        this._orthogonalEndSash = sash;
    }
    onMouseDown(e) {
        EventHelper.stop(e, false);
        let isMultisashResize = false;
        if (!e.__orthogonalSashEvent) {
            const orthogonalSash = this.getOrthogonalSash(e);
            if (orthogonalSash) {
                isMultisashResize = true;
                e.__orthogonalSashEvent = true;
                orthogonalSash.onMouseDown(e);
            }
        }
        if (this.linkedSash && !e.__linkedSashEvent) {
            e.__linkedSashEvent = true;
            this.linkedSash.onMouseDown(e);
        }
        if (!this.state) {
            return;
        }
        // Select both iframes and webviews; internally Electron nests an iframe
        // in its <webview> component, but this isn't queryable.
        const iframes = [
            ...getElementsByTagName('iframe'),
            ...getElementsByTagName('webview'),
        ];
        for (const iframe of iframes) {
            iframe.style.pointerEvents = 'none'; // disable mouse events on iframes as long as we drag the sash
        }
        const mouseDownEvent = new StandardMouseEvent(e);
        const startX = mouseDownEvent.posx;
        const startY = mouseDownEvent.posy;
        const altKey = mouseDownEvent.altKey;
        const startEvent = { startX, currentX: startX, startY, currentY: startY, altKey };
        this.el.classList.add('active');
        this._onDidStart.fire(startEvent);
        // fix https://github.com/microsoft/vscode/issues/21675
        const style = createStyleSheet(this.el);
        const updateStyle = () => {
            let cursor = '';
            if (isMultisashResize) {
                cursor = 'all-scroll';
            }
            else if (this.orientation === 1 /* HORIZONTAL */) {
                if (this.state === 1 /* Minimum */) {
                    cursor = 's-resize';
                }
                else if (this.state === 2 /* Maximum */) {
                    cursor = 'n-resize';
                }
                else {
                    cursor = isMacintosh ? 'row-resize' : 'ns-resize';
                }
            }
            else {
                if (this.state === 1 /* Minimum */) {
                    cursor = 'e-resize';
                }
                else if (this.state === 2 /* Maximum */) {
                    cursor = 'w-resize';
                }
                else {
                    cursor = isMacintosh ? 'col-resize' : 'ew-resize';
                }
            }
            style.textContent = `* { cursor: ${cursor} !important; }`;
        };
        const disposables = new DisposableStore();
        updateStyle();
        if (!isMultisashResize) {
            this.onDidEnablementChange(updateStyle, null, disposables);
        }
        const onMouseMove = (e) => {
            EventHelper.stop(e, false);
            const mouseMoveEvent = new StandardMouseEvent(e);
            const event = { startX, currentX: mouseMoveEvent.posx, startY, currentY: mouseMoveEvent.posy, altKey };
            this._onDidChange.fire(event);
        };
        const onMouseUp = (e) => {
            EventHelper.stop(e, false);
            this.el.removeChild(style);
            this.el.classList.remove('active');
            this._onDidEnd.fire();
            disposables.dispose();
            for (const iframe of iframes) {
                iframe.style.pointerEvents = 'auto';
            }
        };
        domEvent(window, 'mousemove')(onMouseMove, null, disposables);
        domEvent(window, 'mouseup')(onMouseUp, null, disposables);
    }
    onMouseDoubleClick(e) {
        const orthogonalSash = this.getOrthogonalSash(e);
        if (orthogonalSash) {
            orthogonalSash._onDidReset.fire();
        }
        if (this.linkedSash) {
            this.linkedSash._onDidReset.fire();
        }
        this._onDidReset.fire();
    }
    onTouchStart(event) {
        EventHelper.stop(event);
        const listeners = [];
        const startX = event.pageX;
        const startY = event.pageY;
        const altKey = event.altKey;
        this._onDidStart.fire({
            startX: startX,
            currentX: startX,
            startY: startY,
            currentY: startY,
            altKey
        });
        listeners.push(addDisposableListener(this.el, EventType.Change, (event) => {
            if (types.isNumber(event.pageX) && types.isNumber(event.pageY)) {
                this._onDidChange.fire({
                    startX: startX,
                    currentX: event.pageX,
                    startY: startY,
                    currentY: event.pageY,
                    altKey
                });
            }
        }));
        listeners.push(addDisposableListener(this.el, EventType.End, (event) => {
            this._onDidEnd.fire();
            dispose(listeners);
        }));
    }
    layout() {
        if (this.orientation === 0 /* VERTICAL */) {
            const verticalProvider = this.layoutProvider;
            this.el.style.left = verticalProvider.getVerticalSashLeft(this) - (this.size / 2) + 'px';
            if (verticalProvider.getVerticalSashTop) {
                this.el.style.top = verticalProvider.getVerticalSashTop(this) + 'px';
            }
            if (verticalProvider.getVerticalSashHeight) {
                this.el.style.height = verticalProvider.getVerticalSashHeight(this) + 'px';
            }
        }
        else {
            const horizontalProvider = this.layoutProvider;
            this.el.style.top = horizontalProvider.getHorizontalSashTop(this) - (this.size / 2) + 'px';
            if (horizontalProvider.getHorizontalSashLeft) {
                this.el.style.left = horizontalProvider.getHorizontalSashLeft(this) + 'px';
            }
            if (horizontalProvider.getHorizontalSashWidth) {
                this.el.style.width = horizontalProvider.getHorizontalSashWidth(this) + 'px';
            }
        }
    }
    hide() {
        this.hidden = true;
        this.el.style.display = 'none';
        this.el.setAttribute('aria-hidden', 'true');
    }
    onOrthogonalStartSashEnablementChange(state) {
        this.el.classList.toggle('orthogonal-start', state !== 0 /* Disabled */);
    }
    onOrthogonalEndSashEnablementChange(state) {
        this.el.classList.toggle('orthogonal-end', state !== 0 /* Disabled */);
    }
    getOrthogonalSash(e) {
        if (this.orientation === 0 /* VERTICAL */) {
            if (e.offsetY <= this.size) {
                return this.orthogonalStartSash;
            }
            else if (e.offsetY >= this.el.clientHeight - this.size) {
                return this.orthogonalEndSash;
            }
        }
        else {
            if (e.offsetX <= this.size) {
                return this.orthogonalStartSash;
            }
            else if (e.offsetX >= this.el.clientWidth - this.size) {
                return this.orthogonalEndSash;
            }
        }
        return undefined;
    }
    dispose() {
        super.dispose();
        this.el.remove();
    }
}
