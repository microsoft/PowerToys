/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Emitter } from '../common/event.js';
class WindowManager {
    constructor() {
        // --- Zoom Level
        this._zoomLevel = 0;
        this._lastZoomLevelChangeTime = 0;
        this._onDidChangeZoomLevel = new Emitter();
        this.onDidChangeZoomLevel = this._onDidChangeZoomLevel.event;
        // --- Zoom Factor
        this._zoomFactor = 1;
    }
    getZoomLevel() {
        return this._zoomLevel;
    }
    getTimeSinceLastZoomLevelChanged() {
        return Date.now() - this._lastZoomLevelChangeTime;
    }
    getZoomFactor() {
        return this._zoomFactor;
    }
    // --- Pixel Ratio
    getPixelRatio() {
        let ctx = document.createElement('canvas').getContext('2d');
        let dpr = window.devicePixelRatio || 1;
        let bsr = ctx.webkitBackingStorePixelRatio ||
            ctx.mozBackingStorePixelRatio ||
            ctx.msBackingStorePixelRatio ||
            ctx.oBackingStorePixelRatio ||
            ctx.backingStorePixelRatio || 1;
        return dpr / bsr;
    }
}
WindowManager.INSTANCE = new WindowManager();
export function getZoomLevel() {
    return WindowManager.INSTANCE.getZoomLevel();
}
/** Returns the time (in ms) since the zoom level was changed */
export function getTimeSinceLastZoomLevelChanged() {
    return WindowManager.INSTANCE.getTimeSinceLastZoomLevelChanged();
}
export function onDidChangeZoomLevel(callback) {
    return WindowManager.INSTANCE.onDidChangeZoomLevel(callback);
}
/** The zoom scale for an index, e.g. 1, 1.2, 1.4 */
export function getZoomFactor() {
    return WindowManager.INSTANCE.getZoomFactor();
}
export function getPixelRatio() {
    return WindowManager.INSTANCE.getPixelRatio();
}
const userAgent = navigator.userAgent;
export const isEdgeLegacy = (userAgent.indexOf('Edge/') >= 0);
export const isFirefox = (userAgent.indexOf('Firefox') >= 0);
export const isWebKit = (userAgent.indexOf('AppleWebKit') >= 0);
export const isChrome = (userAgent.indexOf('Chrome') >= 0);
export const isSafari = (!isChrome && (userAgent.indexOf('Safari') >= 0));
export const isWebkitWebView = (!isChrome && !isSafari && isWebKit);
export const isIPad = (userAgent.indexOf('iPad') >= 0 || (isSafari && navigator.maxTouchPoints > 0));
export const isEdgeLegacyWebView = isEdgeLegacy && (userAgent.indexOf('WebView/') >= 0);
export const isElectron = (userAgent.indexOf('Electron/') >= 0);
export const isStandalone = (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches);
