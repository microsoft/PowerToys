/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
var CSSDataProvider = /** @class */ (function () {
    /**
     * Currently, unversioned data uses the V1 implementation
     * In the future when the provider handles multiple versions of HTML custom data,
     * use the latest implementation for unversioned data
     */
    function CSSDataProvider(data) {
        this._properties = [];
        this._atDirectives = [];
        this._pseudoClasses = [];
        this._pseudoElements = [];
        this.addData(data);
    }
    CSSDataProvider.prototype.provideProperties = function () {
        return this._properties;
    };
    CSSDataProvider.prototype.provideAtDirectives = function () {
        return this._atDirectives;
    };
    CSSDataProvider.prototype.providePseudoClasses = function () {
        return this._pseudoClasses;
    };
    CSSDataProvider.prototype.providePseudoElements = function () {
        return this._pseudoElements;
    };
    CSSDataProvider.prototype.addData = function (data) {
        if (Array.isArray(data.properties)) {
            for (var _i = 0, _a = data.properties; _i < _a.length; _i++) {
                var prop = _a[_i];
                if (isPropertyData(prop)) {
                    this._properties.push(prop);
                }
            }
        }
        if (Array.isArray(data.atDirectives)) {
            for (var _b = 0, _c = data.atDirectives; _b < _c.length; _b++) {
                var prop = _c[_b];
                if (isAtDirective(prop)) {
                    this._atDirectives.push(prop);
                }
            }
        }
        if (Array.isArray(data.pseudoClasses)) {
            for (var _d = 0, _e = data.pseudoClasses; _d < _e.length; _d++) {
                var prop = _e[_d];
                if (isPseudoClassData(prop)) {
                    this._pseudoClasses.push(prop);
                }
            }
        }
        if (Array.isArray(data.pseudoElements)) {
            for (var _f = 0, _g = data.pseudoElements; _f < _g.length; _f++) {
                var prop = _g[_f];
                if (isPseudoElementData(prop)) {
                    this._pseudoElements.push(prop);
                }
            }
        }
    };
    return CSSDataProvider;
}());
export { CSSDataProvider };
function isPropertyData(d) {
    return typeof d.name === 'string';
}
function isAtDirective(d) {
    return typeof d.name === 'string';
}
function isPseudoClassData(d) {
    return typeof d.name === 'string';
}
function isPseudoElementData(d) {
    return typeof d.name === 'string';
}
