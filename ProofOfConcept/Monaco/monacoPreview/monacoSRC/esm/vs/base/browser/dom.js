/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as browser from './browser.js';
import { domEvent } from './event.js';
import { StandardKeyboardEvent } from './keyboardEvent.js';
import { StandardMouseEvent } from './mouseEvent.js';
import { TimeoutTimer } from '../common/async.js';
import { onUnexpectedError } from '../common/errors.js';
import { Emitter } from '../common/event.js';
import { Disposable, DisposableStore, toDisposable } from '../common/lifecycle.js';
import * as platform from '../common/platform.js';
import { FileAccess, RemoteAuthorities } from '../common/network.js';
import { BrowserFeatures } from './canIUse.js';
export function clearNode(node) {
    while (node.firstChild) {
        node.firstChild.remove();
    }
}
/**
 * @deprecated Use node.isConnected directly
 */
export function isInDOM(node) {
    var _a;
    return (_a = node === null || node === void 0 ? void 0 : node.isConnected) !== null && _a !== void 0 ? _a : false;
}
class DomListener {
    constructor(node, type, handler, options) {
        this._node = node;
        this._type = type;
        this._handler = handler;
        this._options = (options || false);
        this._node.addEventListener(this._type, this._handler, this._options);
    }
    dispose() {
        if (!this._handler) {
            // Already disposed
            return;
        }
        this._node.removeEventListener(this._type, this._handler, this._options);
        // Prevent leakers from holding on to the dom or handler func
        this._node = null;
        this._handler = null;
    }
}
export function addDisposableListener(node, type, handler, useCaptureOrOptions) {
    return new DomListener(node, type, handler, useCaptureOrOptions);
}
function _wrapAsStandardMouseEvent(handler) {
    return function (e) {
        return handler(new StandardMouseEvent(e));
    };
}
function _wrapAsStandardKeyboardEvent(handler) {
    return function (e) {
        return handler(new StandardKeyboardEvent(e));
    };
}
export let addStandardDisposableListener = function addStandardDisposableListener(node, type, handler, useCapture) {
    let wrapHandler = handler;
    if (type === 'click' || type === 'mousedown') {
        wrapHandler = _wrapAsStandardMouseEvent(handler);
    }
    else if (type === 'keydown' || type === 'keypress' || type === 'keyup') {
        wrapHandler = _wrapAsStandardKeyboardEvent(handler);
    }
    return addDisposableListener(node, type, wrapHandler, useCapture);
};
export let addStandardDisposableGenericMouseDownListner = function addStandardDisposableListener(node, handler, useCapture) {
    let wrapHandler = _wrapAsStandardMouseEvent(handler);
    return addDisposableGenericMouseDownListner(node, wrapHandler, useCapture);
};
export function addDisposableGenericMouseDownListner(node, handler, useCapture) {
    return addDisposableListener(node, platform.isIOS && BrowserFeatures.pointerEvents ? EventType.POINTER_DOWN : EventType.MOUSE_DOWN, handler, useCapture);
}
export function addDisposableGenericMouseUpListner(node, handler, useCapture) {
    return addDisposableListener(node, platform.isIOS && BrowserFeatures.pointerEvents ? EventType.POINTER_UP : EventType.MOUSE_UP, handler, useCapture);
}
export function addDisposableNonBubblingMouseOutListener(node, handler) {
    return addDisposableListener(node, 'mouseout', (e) => {
        // Mouse out bubbles, so this is an attempt to ignore faux mouse outs coming from children elements
        let toElement = (e.relatedTarget);
        while (toElement && toElement !== node) {
            toElement = toElement.parentNode;
        }
        if (toElement === node) {
            return;
        }
        handler(e);
    });
}
export function addDisposableNonBubblingPointerOutListener(node, handler) {
    return addDisposableListener(node, 'pointerout', (e) => {
        // Mouse out bubbles, so this is an attempt to ignore faux mouse outs coming from children elements
        let toElement = (e.relatedTarget);
        while (toElement && toElement !== node) {
            toElement = toElement.parentNode;
        }
        if (toElement === node) {
            return;
        }
        handler(e);
    });
}
let _animationFrame = null;
function doRequestAnimationFrame(callback) {
    if (!_animationFrame) {
        const emulatedRequestAnimationFrame = (callback) => {
            return setTimeout(() => callback(new Date().getTime()), 0);
        };
        _animationFrame = (self.requestAnimationFrame
            || self.msRequestAnimationFrame
            || self.webkitRequestAnimationFrame
            || self.mozRequestAnimationFrame
            || self.oRequestAnimationFrame
            || emulatedRequestAnimationFrame);
    }
    return _animationFrame.call(self, callback);
}
/**
 * Schedule a callback to be run at the next animation frame.
 * This allows multiple parties to register callbacks that should run at the next animation frame.
 * If currently in an animation frame, `runner` will be executed immediately.
 * @return token that can be used to cancel the scheduled runner (only if `runner` was not executed immediately).
 */
export let runAtThisOrScheduleAtNextAnimationFrame;
/**
 * Schedule a callback to be run at the next animation frame.
 * This allows multiple parties to register callbacks that should run at the next animation frame.
 * If currently in an animation frame, `runner` will be executed at the next animation frame.
 * @return token that can be used to cancel the scheduled runner.
 */
export let scheduleAtNextAnimationFrame;
class AnimationFrameQueueItem {
    constructor(runner, priority = 0) {
        this._runner = runner;
        this.priority = priority;
        this._canceled = false;
    }
    dispose() {
        this._canceled = true;
    }
    execute() {
        if (this._canceled) {
            return;
        }
        try {
            this._runner();
        }
        catch (e) {
            onUnexpectedError(e);
        }
    }
    // Sort by priority (largest to lowest)
    static sort(a, b) {
        return b.priority - a.priority;
    }
}
(function () {
    /**
     * The runners scheduled at the next animation frame
     */
    let NEXT_QUEUE = [];
    /**
     * The runners scheduled at the current animation frame
     */
    let CURRENT_QUEUE = null;
    /**
     * A flag to keep track if the native requestAnimationFrame was already called
     */
    let animFrameRequested = false;
    /**
     * A flag to indicate if currently handling a native requestAnimationFrame callback
     */
    let inAnimationFrameRunner = false;
    let animationFrameRunner = () => {
        animFrameRequested = false;
        CURRENT_QUEUE = NEXT_QUEUE;
        NEXT_QUEUE = [];
        inAnimationFrameRunner = true;
        while (CURRENT_QUEUE.length > 0) {
            CURRENT_QUEUE.sort(AnimationFrameQueueItem.sort);
            let top = CURRENT_QUEUE.shift();
            top.execute();
        }
        inAnimationFrameRunner = false;
    };
    scheduleAtNextAnimationFrame = (runner, priority = 0) => {
        let item = new AnimationFrameQueueItem(runner, priority);
        NEXT_QUEUE.push(item);
        if (!animFrameRequested) {
            animFrameRequested = true;
            doRequestAnimationFrame(animationFrameRunner);
        }
        return item;
    };
    runAtThisOrScheduleAtNextAnimationFrame = (runner, priority) => {
        if (inAnimationFrameRunner) {
            let item = new AnimationFrameQueueItem(runner, priority);
            CURRENT_QUEUE.push(item);
            return item;
        }
        else {
            return scheduleAtNextAnimationFrame(runner, priority);
        }
    };
})();
const MINIMUM_TIME_MS = 16;
const DEFAULT_EVENT_MERGER = function (lastEvent, currentEvent) {
    return currentEvent;
};
class TimeoutThrottledDomListener extends Disposable {
    constructor(node, type, handler, eventMerger = DEFAULT_EVENT_MERGER, minimumTimeMs = MINIMUM_TIME_MS) {
        super();
        let lastEvent = null;
        let lastHandlerTime = 0;
        let timeout = this._register(new TimeoutTimer());
        let invokeHandler = () => {
            lastHandlerTime = (new Date()).getTime();
            handler(lastEvent);
            lastEvent = null;
        };
        this._register(addDisposableListener(node, type, (e) => {
            lastEvent = eventMerger(lastEvent, e);
            let elapsedTime = (new Date()).getTime() - lastHandlerTime;
            if (elapsedTime >= minimumTimeMs) {
                timeout.cancel();
                invokeHandler();
            }
            else {
                timeout.setIfNotSet(invokeHandler, minimumTimeMs - elapsedTime);
            }
        }));
    }
}
export function addDisposableThrottledListener(node, type, handler, eventMerger, minimumTimeMs) {
    return new TimeoutThrottledDomListener(node, type, handler, eventMerger, minimumTimeMs);
}
export function getComputedStyle(el) {
    return document.defaultView.getComputedStyle(el, null);
}
export function getClientArea(element) {
    // Try with DOM clientWidth / clientHeight
    if (element !== document.body) {
        return new Dimension(element.clientWidth, element.clientHeight);
    }
    // If visual view port exits and it's on mobile, it should be used instead of window innerWidth / innerHeight, or document.body.clientWidth / document.body.clientHeight
    if (platform.isIOS && window.visualViewport) {
        const width = window.visualViewport.width;
        const height = window.visualViewport.height - (browser.isStandalone
            // in PWA mode, the visual viewport always includes the safe-area-inset-bottom (which is for the home indicator)
            // even when you are using the onscreen monitor, the visual viewport will include the area between system statusbar and the onscreen keyboard
            // plus the area between onscreen keyboard and the bottom bezel, which is 20px on iOS.
            ? (20 + 4) // + 4px for body margin
            : 0);
        return new Dimension(width, height);
    }
    // Try innerWidth / innerHeight
    if (window.innerWidth && window.innerHeight) {
        return new Dimension(window.innerWidth, window.innerHeight);
    }
    // Try with document.body.clientWidth / document.body.clientHeight
    if (document.body && document.body.clientWidth && document.body.clientHeight) {
        return new Dimension(document.body.clientWidth, document.body.clientHeight);
    }
    // Try with document.documentElement.clientWidth / document.documentElement.clientHeight
    if (document.documentElement && document.documentElement.clientWidth && document.documentElement.clientHeight) {
        return new Dimension(document.documentElement.clientWidth, document.documentElement.clientHeight);
    }
    throw new Error('Unable to figure out browser width and height');
}
class SizeUtils {
    // Adapted from WinJS
    // Converts a CSS positioning string for the specified element to pixels.
    static convertToPixels(element, value) {
        return parseFloat(value) || 0;
    }
    static getDimension(element, cssPropertyName, jsPropertyName) {
        let computedStyle = getComputedStyle(element);
        let value = '0';
        if (computedStyle) {
            if (computedStyle.getPropertyValue) {
                value = computedStyle.getPropertyValue(cssPropertyName);
            }
            else {
                // IE8
                value = computedStyle.getAttribute(jsPropertyName);
            }
        }
        return SizeUtils.convertToPixels(element, value);
    }
    static getBorderLeftWidth(element) {
        return SizeUtils.getDimension(element, 'border-left-width', 'borderLeftWidth');
    }
    static getBorderRightWidth(element) {
        return SizeUtils.getDimension(element, 'border-right-width', 'borderRightWidth');
    }
    static getBorderTopWidth(element) {
        return SizeUtils.getDimension(element, 'border-top-width', 'borderTopWidth');
    }
    static getBorderBottomWidth(element) {
        return SizeUtils.getDimension(element, 'border-bottom-width', 'borderBottomWidth');
    }
    static getPaddingLeft(element) {
        return SizeUtils.getDimension(element, 'padding-left', 'paddingLeft');
    }
    static getPaddingRight(element) {
        return SizeUtils.getDimension(element, 'padding-right', 'paddingRight');
    }
    static getPaddingTop(element) {
        return SizeUtils.getDimension(element, 'padding-top', 'paddingTop');
    }
    static getPaddingBottom(element) {
        return SizeUtils.getDimension(element, 'padding-bottom', 'paddingBottom');
    }
    static getMarginLeft(element) {
        return SizeUtils.getDimension(element, 'margin-left', 'marginLeft');
    }
    static getMarginTop(element) {
        return SizeUtils.getDimension(element, 'margin-top', 'marginTop');
    }
    static getMarginRight(element) {
        return SizeUtils.getDimension(element, 'margin-right', 'marginRight');
    }
    static getMarginBottom(element) {
        return SizeUtils.getDimension(element, 'margin-bottom', 'marginBottom');
    }
}
export class Dimension {
    constructor(width, height) {
        this.width = width;
        this.height = height;
    }
    with(width = this.width, height = this.height) {
        if (width !== this.width || height !== this.height) {
            return new Dimension(width, height);
        }
        else {
            return this;
        }
    }
    static is(obj) {
        return typeof obj === 'object' && typeof obj.height === 'number' && typeof obj.width === 'number';
    }
    static lift(obj) {
        if (obj instanceof Dimension) {
            return obj;
        }
        else {
            return new Dimension(obj.width, obj.height);
        }
    }
    static equals(a, b) {
        if (a === b) {
            return true;
        }
        if (!a || !b) {
            return false;
        }
        return a.width === b.width && a.height === b.height;
    }
}
export function getTopLeftOffset(element) {
    // Adapted from WinJS.Utilities.getPosition
    // and added borders to the mix
    let offsetParent = element.offsetParent;
    let top = element.offsetTop;
    let left = element.offsetLeft;
    while ((element = element.parentNode) !== null
        && element !== document.body
        && element !== document.documentElement) {
        top -= element.scrollTop;
        const c = isShadowRoot(element) ? null : getComputedStyle(element);
        if (c) {
            left -= c.direction !== 'rtl' ? element.scrollLeft : -element.scrollLeft;
        }
        if (element === offsetParent) {
            left += SizeUtils.getBorderLeftWidth(element);
            top += SizeUtils.getBorderTopWidth(element);
            top += element.offsetTop;
            left += element.offsetLeft;
            offsetParent = element.offsetParent;
        }
    }
    return {
        left: left,
        top: top
    };
}
export function size(element, width, height) {
    if (typeof width === 'number') {
        element.style.width = `${width}px`;
    }
    if (typeof height === 'number') {
        element.style.height = `${height}px`;
    }
}
/**
 * Returns the position of a dom node relative to the entire page.
 */
export function getDomNodePagePosition(domNode) {
    let bb = domNode.getBoundingClientRect();
    return {
        left: bb.left + StandardWindow.scrollX,
        top: bb.top + StandardWindow.scrollY,
        width: bb.width,
        height: bb.height
    };
}
export const StandardWindow = new class {
    get scrollX() {
        if (typeof window.scrollX === 'number') {
            // modern browsers
            return window.scrollX;
        }
        else {
            return document.body.scrollLeft + document.documentElement.scrollLeft;
        }
    }
    get scrollY() {
        if (typeof window.scrollY === 'number') {
            // modern browsers
            return window.scrollY;
        }
        else {
            return document.body.scrollTop + document.documentElement.scrollTop;
        }
    }
};
// Adapted from WinJS
// Gets the width of the element, including margins.
export function getTotalWidth(element) {
    let margin = SizeUtils.getMarginLeft(element) + SizeUtils.getMarginRight(element);
    return element.offsetWidth + margin;
}
export function getContentWidth(element) {
    let border = SizeUtils.getBorderLeftWidth(element) + SizeUtils.getBorderRightWidth(element);
    let padding = SizeUtils.getPaddingLeft(element) + SizeUtils.getPaddingRight(element);
    return element.offsetWidth - border - padding;
}
// Adapted from WinJS
// Gets the height of the content of the specified element. The content height does not include borders or padding.
export function getContentHeight(element) {
    let border = SizeUtils.getBorderTopWidth(element) + SizeUtils.getBorderBottomWidth(element);
    let padding = SizeUtils.getPaddingTop(element) + SizeUtils.getPaddingBottom(element);
    return element.offsetHeight - border - padding;
}
// Adapted from WinJS
// Gets the height of the element, including its margins.
export function getTotalHeight(element) {
    let margin = SizeUtils.getMarginTop(element) + SizeUtils.getMarginBottom(element);
    return element.offsetHeight + margin;
}
// ----------------------------------------------------------------------------------------
export function isAncestor(testChild, testAncestor) {
    while (testChild) {
        if (testChild === testAncestor) {
            return true;
        }
        testChild = testChild.parentNode;
    }
    return false;
}
export function findParentWithClass(node, clazz, stopAtClazzOrNode) {
    while (node && node.nodeType === node.ELEMENT_NODE) {
        if (node.classList.contains(clazz)) {
            return node;
        }
        if (stopAtClazzOrNode) {
            if (typeof stopAtClazzOrNode === 'string') {
                if (node.classList.contains(stopAtClazzOrNode)) {
                    return null;
                }
            }
            else {
                if (node === stopAtClazzOrNode) {
                    return null;
                }
            }
        }
        node = node.parentNode;
    }
    return null;
}
export function hasParentWithClass(node, clazz, stopAtClazzOrNode) {
    return !!findParentWithClass(node, clazz, stopAtClazzOrNode);
}
export function isShadowRoot(node) {
    return (node && !!node.host && !!node.mode);
}
export function isInShadowDOM(domNode) {
    return !!getShadowRoot(domNode);
}
export function getShadowRoot(domNode) {
    while (domNode.parentNode) {
        if (domNode === document.body) {
            // reached the body
            return null;
        }
        domNode = domNode.parentNode;
    }
    return isShadowRoot(domNode) ? domNode : null;
}
export function getActiveElement() {
    let result = document.activeElement;
    while (result === null || result === void 0 ? void 0 : result.shadowRoot) {
        result = result.shadowRoot.activeElement;
    }
    return result;
}
export function createStyleSheet(container = document.getElementsByTagName('head')[0]) {
    let style = document.createElement('style');
    style.type = 'text/css';
    style.media = 'screen';
    container.appendChild(style);
    return style;
}
let _sharedStyleSheet = null;
function getSharedStyleSheet() {
    if (!_sharedStyleSheet) {
        _sharedStyleSheet = createStyleSheet();
    }
    return _sharedStyleSheet;
}
function getDynamicStyleSheetRules(style) {
    var _a, _b;
    if ((_a = style === null || style === void 0 ? void 0 : style.sheet) === null || _a === void 0 ? void 0 : _a.rules) {
        // Chrome, IE
        return style.sheet.rules;
    }
    if ((_b = style === null || style === void 0 ? void 0 : style.sheet) === null || _b === void 0 ? void 0 : _b.cssRules) {
        // FF
        return style.sheet.cssRules;
    }
    return [];
}
export function createCSSRule(selector, cssText, style = getSharedStyleSheet()) {
    if (!style || !cssText) {
        return;
    }
    style.sheet.insertRule(selector + '{' + cssText + '}', 0);
}
export function removeCSSRulesContainingSelector(ruleName, style = getSharedStyleSheet()) {
    if (!style) {
        return;
    }
    let rules = getDynamicStyleSheetRules(style);
    let toDelete = [];
    for (let i = 0; i < rules.length; i++) {
        let rule = rules[i];
        if (rule.selectorText.indexOf(ruleName) !== -1) {
            toDelete.push(i);
        }
    }
    for (let i = toDelete.length - 1; i >= 0; i--) {
        style.sheet.deleteRule(toDelete[i]);
    }
}
export function isHTMLElement(o) {
    if (typeof HTMLElement === 'object') {
        return o instanceof HTMLElement;
    }
    return o && typeof o === 'object' && o.nodeType === 1 && typeof o.nodeName === 'string';
}
export const EventType = {
    // Mouse
    CLICK: 'click',
    AUXCLICK: 'auxclick',
    DBLCLICK: 'dblclick',
    MOUSE_UP: 'mouseup',
    MOUSE_DOWN: 'mousedown',
    MOUSE_OVER: 'mouseover',
    MOUSE_MOVE: 'mousemove',
    MOUSE_OUT: 'mouseout',
    MOUSE_ENTER: 'mouseenter',
    MOUSE_LEAVE: 'mouseleave',
    MOUSE_WHEEL: browser.isEdgeLegacy ? 'mousewheel' : 'wheel',
    POINTER_UP: 'pointerup',
    POINTER_DOWN: 'pointerdown',
    POINTER_MOVE: 'pointermove',
    CONTEXT_MENU: 'contextmenu',
    WHEEL: 'wheel',
    // Keyboard
    KEY_DOWN: 'keydown',
    KEY_PRESS: 'keypress',
    KEY_UP: 'keyup',
    // HTML Document
    LOAD: 'load',
    BEFORE_UNLOAD: 'beforeunload',
    UNLOAD: 'unload',
    ABORT: 'abort',
    ERROR: 'error',
    RESIZE: 'resize',
    SCROLL: 'scroll',
    FULLSCREEN_CHANGE: 'fullscreenchange',
    WK_FULLSCREEN_CHANGE: 'webkitfullscreenchange',
    // Form
    SELECT: 'select',
    CHANGE: 'change',
    SUBMIT: 'submit',
    RESET: 'reset',
    FOCUS: 'focus',
    FOCUS_IN: 'focusin',
    FOCUS_OUT: 'focusout',
    BLUR: 'blur',
    INPUT: 'input',
    // Local Storage
    STORAGE: 'storage',
    // Drag
    DRAG_START: 'dragstart',
    DRAG: 'drag',
    DRAG_ENTER: 'dragenter',
    DRAG_LEAVE: 'dragleave',
    DRAG_OVER: 'dragover',
    DROP: 'drop',
    DRAG_END: 'dragend',
    // Animation
    ANIMATION_START: browser.isWebKit ? 'webkitAnimationStart' : 'animationstart',
    ANIMATION_END: browser.isWebKit ? 'webkitAnimationEnd' : 'animationend',
    ANIMATION_ITERATION: browser.isWebKit ? 'webkitAnimationIteration' : 'animationiteration'
};
export const EventHelper = {
    stop: function (e, cancelBubble) {
        if (e.preventDefault) {
            e.preventDefault();
        }
        else {
            // IE8
            e.returnValue = false;
        }
        if (cancelBubble) {
            if (e.stopPropagation) {
                e.stopPropagation();
            }
            else {
                // IE8
                e.cancelBubble = true;
            }
        }
    }
};
export function saveParentsScrollTop(node) {
    let r = [];
    for (let i = 0; node && node.nodeType === node.ELEMENT_NODE; i++) {
        r[i] = node.scrollTop;
        node = node.parentNode;
    }
    return r;
}
export function restoreParentsScrollTop(node, state) {
    for (let i = 0; node && node.nodeType === node.ELEMENT_NODE; i++) {
        if (node.scrollTop !== state[i]) {
            node.scrollTop = state[i];
        }
        node = node.parentNode;
    }
}
class FocusTracker extends Disposable {
    constructor(element) {
        super();
        this._onDidFocus = this._register(new Emitter());
        this.onDidFocus = this._onDidFocus.event;
        this._onDidBlur = this._register(new Emitter());
        this.onDidBlur = this._onDidBlur.event;
        let hasFocus = isAncestor(document.activeElement, element);
        let loosingFocus = false;
        const onFocus = () => {
            loosingFocus = false;
            if (!hasFocus) {
                hasFocus = true;
                this._onDidFocus.fire();
            }
        };
        const onBlur = () => {
            if (hasFocus) {
                loosingFocus = true;
                window.setTimeout(() => {
                    if (loosingFocus) {
                        loosingFocus = false;
                        hasFocus = false;
                        this._onDidBlur.fire();
                    }
                }, 0);
            }
        };
        this._refreshStateHandler = () => {
            let currentNodeHasFocus = isAncestor(document.activeElement, element);
            if (currentNodeHasFocus !== hasFocus) {
                if (hasFocus) {
                    onBlur();
                }
                else {
                    onFocus();
                }
            }
        };
        this._register(domEvent(element, EventType.FOCUS, true)(onFocus));
        this._register(domEvent(element, EventType.BLUR, true)(onBlur));
    }
}
export function trackFocus(element) {
    return new FocusTracker(element);
}
export function append(parent, ...children) {
    parent.append(...children);
    if (children.length === 1 && typeof children[0] !== 'string') {
        return children[0];
    }
}
/**
 * Removes all children from `parent` and appends `children`
 */
export function reset(parent, ...children) {
    parent.innerText = '';
    append(parent, ...children);
}
const SELECTOR_REGEX = /([\w\-]+)?(#([\w\-]+))?((\.([\w\-]+))*)/;
export var Namespace;
(function (Namespace) {
    Namespace["HTML"] = "http://www.w3.org/1999/xhtml";
    Namespace["SVG"] = "http://www.w3.org/2000/svg";
})(Namespace || (Namespace = {}));
function _$(namespace, description, attrs, ...children) {
    let match = SELECTOR_REGEX.exec(description);
    if (!match) {
        throw new Error('Bad use of emmet');
    }
    attrs = Object.assign({}, (attrs || {}));
    let tagName = match[1] || 'div';
    let result;
    if (namespace !== Namespace.HTML) {
        result = document.createElementNS(namespace, tagName);
    }
    else {
        result = document.createElement(tagName);
    }
    if (match[3]) {
        result.id = match[3];
    }
    if (match[4]) {
        result.className = match[4].replace(/\./g, ' ').trim();
    }
    Object.keys(attrs).forEach(name => {
        const value = attrs[name];
        if (typeof value === 'undefined') {
            return;
        }
        if (/^on\w+$/.test(name)) {
            result[name] = value;
        }
        else if (name === 'selected') {
            if (value) {
                result.setAttribute(name, 'true');
            }
        }
        else {
            result.setAttribute(name, value);
        }
    });
    result.append(...children);
    return result;
}
export function $(description, attrs, ...children) {
    return _$(Namespace.HTML, description, attrs, ...children);
}
$.SVG = function (description, attrs, ...children) {
    return _$(Namespace.SVG, description, attrs, ...children);
};
export function show(...elements) {
    for (let element of elements) {
        element.style.display = '';
        element.removeAttribute('aria-hidden');
    }
}
export function hide(...elements) {
    for (let element of elements) {
        element.style.display = 'none';
        element.setAttribute('aria-hidden', 'true');
    }
}
function findParentWithAttribute(node, attribute) {
    while (node && node.nodeType === node.ELEMENT_NODE) {
        if (node instanceof HTMLElement && node.hasAttribute(attribute)) {
            return node;
        }
        node = node.parentNode;
    }
    return null;
}
export function removeTabIndexAndUpdateFocus(node) {
    if (!node || !node.hasAttribute('tabIndex')) {
        return;
    }
    // If we are the currently focused element and tabIndex is removed,
    // standard DOM behavior is to move focus to the <body> element. We
    // typically never want that, rather put focus to the closest element
    // in the hierarchy of the parent DOM nodes.
    if (document.activeElement === node) {
        let parentFocusable = findParentWithAttribute(node.parentElement, 'tabIndex');
        if (parentFocusable) {
            parentFocusable.focus();
        }
    }
    node.removeAttribute('tabindex');
}
export function getElementsByTagName(tag) {
    return Array.prototype.slice.call(document.getElementsByTagName(tag), 0);
}
/**
 * Find a value usable for a dom node size such that the likelihood that it would be
 * displayed with constant screen pixels size is as high as possible.
 *
 * e.g. We would desire for the cursors to be 2px (CSS px) wide. Under a devicePixelRatio
 * of 1.25, the cursor will be 2.5 screen pixels wide. Depending on how the dom node aligns/"snaps"
 * with the screen pixels, it will sometimes be rendered with 2 screen pixels, and sometimes with 3 screen pixels.
 */
export function computeScreenAwareSize(cssPx) {
    const screenPx = window.devicePixelRatio * cssPx;
    return Math.max(1, Math.floor(screenPx)) / window.devicePixelRatio;
}
/**
 * See https://github.com/microsoft/monaco-editor/issues/601
 * To protect against malicious code in the linked site, particularly phishing attempts,
 * the window.opener should be set to null to prevent the linked site from having access
 * to change the location of the current page.
 * See https://mathiasbynens.github.io/rel-noopener/
 */
export function windowOpenNoOpener(url) {
    if (browser.isElectron || browser.isEdgeLegacyWebView) {
        // In VSCode, window.open() always returns null...
        // The same is true for a WebView (see https://github.com/microsoft/monaco-editor/issues/628)
        // Also call directly window.open in sandboxed Electron (see https://github.com/microsoft/monaco-editor/issues/2220)
        window.open(url);
    }
    else {
        let newTab = window.open();
        if (newTab) {
            newTab.opener = null;
            newTab.location.href = url;
        }
    }
}
export function animate(fn) {
    const step = () => {
        fn();
        stepDisposable = scheduleAtNextAnimationFrame(step);
    };
    let stepDisposable = scheduleAtNextAnimationFrame(step);
    return toDisposable(() => stepDisposable.dispose());
}
RemoteAuthorities.setPreferredWebSchema(/^https:/.test(window.location.href) ? 'https' : 'http');
/**
 * returns url('...')
 */
export function asCSSUrl(uri) {
    if (!uri) {
        return `url('')`;
    }
    return `url('${FileAccess.asBrowserUri(uri).toString(true).replace(/'/g, '%27')}')`;
}
export class ModifierKeyEmitter extends Emitter {
    constructor() {
        super();
        this._subscriptions = new DisposableStore();
        this._keyStatus = {
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            metaKey: false
        };
        this._subscriptions.add(domEvent(document.body, 'keydown', true)(e => {
            // if keydown event is repeated, ignore it #112347
            if (e.repeat) {
                return;
            }
            const event = new StandardKeyboardEvent(e);
            if (e.altKey && !this._keyStatus.altKey) {
                this._keyStatus.lastKeyPressed = 'alt';
            }
            else if (e.ctrlKey && !this._keyStatus.ctrlKey) {
                this._keyStatus.lastKeyPressed = 'ctrl';
            }
            else if (e.metaKey && !this._keyStatus.metaKey) {
                this._keyStatus.lastKeyPressed = 'meta';
            }
            else if (e.shiftKey && !this._keyStatus.shiftKey) {
                this._keyStatus.lastKeyPressed = 'shift';
            }
            else if (event.keyCode !== 6 /* Alt */) {
                this._keyStatus.lastKeyPressed = undefined;
            }
            else {
                return;
            }
            this._keyStatus.altKey = e.altKey;
            this._keyStatus.ctrlKey = e.ctrlKey;
            this._keyStatus.metaKey = e.metaKey;
            this._keyStatus.shiftKey = e.shiftKey;
            if (this._keyStatus.lastKeyPressed) {
                this._keyStatus.event = e;
                this.fire(this._keyStatus);
            }
        }));
        this._subscriptions.add(domEvent(document.body, 'keyup', true)(e => {
            if (!e.altKey && this._keyStatus.altKey) {
                this._keyStatus.lastKeyReleased = 'alt';
            }
            else if (!e.ctrlKey && this._keyStatus.ctrlKey) {
                this._keyStatus.lastKeyReleased = 'ctrl';
            }
            else if (!e.metaKey && this._keyStatus.metaKey) {
                this._keyStatus.lastKeyReleased = 'meta';
            }
            else if (!e.shiftKey && this._keyStatus.shiftKey) {
                this._keyStatus.lastKeyReleased = 'shift';
            }
            else {
                this._keyStatus.lastKeyReleased = undefined;
            }
            if (this._keyStatus.lastKeyPressed !== this._keyStatus.lastKeyReleased) {
                this._keyStatus.lastKeyPressed = undefined;
            }
            this._keyStatus.altKey = e.altKey;
            this._keyStatus.ctrlKey = e.ctrlKey;
            this._keyStatus.metaKey = e.metaKey;
            this._keyStatus.shiftKey = e.shiftKey;
            if (this._keyStatus.lastKeyReleased) {
                this._keyStatus.event = e;
                this.fire(this._keyStatus);
            }
        }));
        this._subscriptions.add(domEvent(document.body, 'mousedown', true)(e => {
            this._keyStatus.lastKeyPressed = undefined;
        }));
        this._subscriptions.add(domEvent(document.body, 'mouseup', true)(e => {
            this._keyStatus.lastKeyPressed = undefined;
        }));
        this._subscriptions.add(domEvent(document.body, 'mousemove', true)(e => {
            if (e.buttons) {
                this._keyStatus.lastKeyPressed = undefined;
            }
        }));
        this._subscriptions.add(domEvent(window, 'blur')(e => {
            this.resetKeyStatus();
        }));
    }
    get keyStatus() {
        return this._keyStatus;
    }
    /**
     * Allows to explicitly reset the key status based on more knowledge (#109062)
     */
    resetKeyStatus() {
        this.doResetKeyStatus();
        this.fire(this._keyStatus);
    }
    doResetKeyStatus() {
        this._keyStatus = {
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            metaKey: false
        };
    }
    static getInstance() {
        if (!ModifierKeyEmitter.instance) {
            ModifierKeyEmitter.instance = new ModifierKeyEmitter();
        }
        return ModifierKeyEmitter.instance;
    }
    dispose() {
        super.dispose();
        this._subscriptions.dispose();
    }
}
