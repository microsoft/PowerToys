/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export class FastDomNode {
    constructor(domNode) {
        this.domNode = domNode;
        this._maxWidth = -1;
        this._width = -1;
        this._height = -1;
        this._top = -1;
        this._left = -1;
        this._bottom = -1;
        this._right = -1;
        this._fontFamily = '';
        this._fontWeight = '';
        this._fontSize = -1;
        this._fontFeatureSettings = '';
        this._lineHeight = -1;
        this._letterSpacing = -100;
        this._className = '';
        this._display = '';
        this._position = '';
        this._visibility = '';
        this._backgroundColor = '';
        this._layerHint = false;
        this._contain = 'none';
        this._boxShadow = '';
    }
    setMaxWidth(maxWidth) {
        if (this._maxWidth === maxWidth) {
            return;
        }
        this._maxWidth = maxWidth;
        this.domNode.style.maxWidth = this._maxWidth + 'px';
    }
    setWidth(width) {
        if (this._width === width) {
            return;
        }
        this._width = width;
        this.domNode.style.width = this._width + 'px';
    }
    setHeight(height) {
        if (this._height === height) {
            return;
        }
        this._height = height;
        this.domNode.style.height = this._height + 'px';
    }
    setTop(top) {
        if (this._top === top) {
            return;
        }
        this._top = top;
        this.domNode.style.top = this._top + 'px';
    }
    unsetTop() {
        if (this._top === -1) {
            return;
        }
        this._top = -1;
        this.domNode.style.top = '';
    }
    setLeft(left) {
        if (this._left === left) {
            return;
        }
        this._left = left;
        this.domNode.style.left = this._left + 'px';
    }
    setBottom(bottom) {
        if (this._bottom === bottom) {
            return;
        }
        this._bottom = bottom;
        this.domNode.style.bottom = this._bottom + 'px';
    }
    setRight(right) {
        if (this._right === right) {
            return;
        }
        this._right = right;
        this.domNode.style.right = this._right + 'px';
    }
    setFontFamily(fontFamily) {
        if (this._fontFamily === fontFamily) {
            return;
        }
        this._fontFamily = fontFamily;
        this.domNode.style.fontFamily = this._fontFamily;
    }
    setFontWeight(fontWeight) {
        if (this._fontWeight === fontWeight) {
            return;
        }
        this._fontWeight = fontWeight;
        this.domNode.style.fontWeight = this._fontWeight;
    }
    setFontSize(fontSize) {
        if (this._fontSize === fontSize) {
            return;
        }
        this._fontSize = fontSize;
        this.domNode.style.fontSize = this._fontSize + 'px';
    }
    setFontFeatureSettings(fontFeatureSettings) {
        if (this._fontFeatureSettings === fontFeatureSettings) {
            return;
        }
        this._fontFeatureSettings = fontFeatureSettings;
        this.domNode.style.fontFeatureSettings = this._fontFeatureSettings;
    }
    setLineHeight(lineHeight) {
        if (this._lineHeight === lineHeight) {
            return;
        }
        this._lineHeight = lineHeight;
        this.domNode.style.lineHeight = this._lineHeight + 'px';
    }
    setLetterSpacing(letterSpacing) {
        if (this._letterSpacing === letterSpacing) {
            return;
        }
        this._letterSpacing = letterSpacing;
        this.domNode.style.letterSpacing = this._letterSpacing + 'px';
    }
    setClassName(className) {
        if (this._className === className) {
            return;
        }
        this._className = className;
        this.domNode.className = this._className;
    }
    toggleClassName(className, shouldHaveIt) {
        this.domNode.classList.toggle(className, shouldHaveIt);
        this._className = this.domNode.className;
    }
    setDisplay(display) {
        if (this._display === display) {
            return;
        }
        this._display = display;
        this.domNode.style.display = this._display;
    }
    setPosition(position) {
        if (this._position === position) {
            return;
        }
        this._position = position;
        this.domNode.style.position = this._position;
    }
    setVisibility(visibility) {
        if (this._visibility === visibility) {
            return;
        }
        this._visibility = visibility;
        this.domNode.style.visibility = this._visibility;
    }
    setBackgroundColor(backgroundColor) {
        if (this._backgroundColor === backgroundColor) {
            return;
        }
        this._backgroundColor = backgroundColor;
        this.domNode.style.backgroundColor = this._backgroundColor;
    }
    setLayerHinting(layerHint) {
        if (this._layerHint === layerHint) {
            return;
        }
        this._layerHint = layerHint;
        this.domNode.style.transform = this._layerHint ? 'translate3d(0px, 0px, 0px)' : '';
    }
    setBoxShadow(boxShadow) {
        if (this._boxShadow === boxShadow) {
            return;
        }
        this._boxShadow = boxShadow;
        this.domNode.style.boxShadow = boxShadow;
    }
    setContain(contain) {
        if (this._contain === contain) {
            return;
        }
        this._contain = contain;
        this.domNode.style.contain = this._contain;
    }
    setAttribute(name, value) {
        this.domNode.setAttribute(name, value);
    }
    removeAttribute(name) {
        this.domNode.removeAttribute(name);
    }
    appendChild(child) {
        this.domNode.appendChild(child.domNode);
    }
    removeChild(child) {
        this.domNode.removeChild(child.domNode);
    }
}
export function createFastDomNode(domNode) {
    return new FastDomNode(domNode);
}
