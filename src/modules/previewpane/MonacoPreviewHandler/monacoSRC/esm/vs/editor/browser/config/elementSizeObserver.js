/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Disposable } from '../../../base/common/lifecycle.js';
export class ElementSizeObserver extends Disposable {
    constructor(referenceDomElement, dimension, changeCallback) {
        super();
        this.referenceDomElement = referenceDomElement;
        this.changeCallback = changeCallback;
        this.width = -1;
        this.height = -1;
        this.resizeObserver = null;
        this.measureReferenceDomElementToken = -1;
        this.measureReferenceDomElement(false, dimension);
    }
    dispose() {
        this.stopObserving();
        super.dispose();
    }
    getWidth() {
        return this.width;
    }
    getHeight() {
        return this.height;
    }
    startObserving() {
        if (typeof ResizeObserver !== 'undefined') {
            if (!this.resizeObserver && this.referenceDomElement) {
                this.resizeObserver = new ResizeObserver((entries) => {
                    if (entries && entries[0] && entries[0].contentRect) {
                        this.observe({ width: entries[0].contentRect.width, height: entries[0].contentRect.height });
                    }
                    else {
                        this.observe();
                    }
                });
                this.resizeObserver.observe(this.referenceDomElement);
            }
        }
        else {
            if (this.measureReferenceDomElementToken === -1) {
                // setInterval type defaults to NodeJS.Timeout instead of number, so specify it as a number
                this.measureReferenceDomElementToken = setInterval(() => this.observe(), 100);
            }
        }
    }
    stopObserving() {
        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = null;
        }
        if (this.measureReferenceDomElementToken !== -1) {
            clearInterval(this.measureReferenceDomElementToken);
            this.measureReferenceDomElementToken = -1;
        }
    }
    observe(dimension) {
        this.measureReferenceDomElement(true, dimension);
    }
    measureReferenceDomElement(callChangeCallback, dimension) {
        let observedWidth = 0;
        let observedHeight = 0;
        if (dimension) {
            observedWidth = dimension.width;
            observedHeight = dimension.height;
        }
        else if (this.referenceDomElement) {
            observedWidth = this.referenceDomElement.clientWidth;
            observedHeight = this.referenceDomElement.clientHeight;
        }
        observedWidth = Math.max(5, observedWidth);
        observedHeight = Math.max(5, observedHeight);
        if (this.width !== observedWidth || this.height !== observedHeight) {
            this.width = observedWidth;
            this.height = observedHeight;
            if (callChangeCallback) {
                this.changeCallback();
            }
        }
    }
}
