/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import * as objects from '../utils/objects.js';
import { cssData } from '../data/webCustomData.js';
import { CSSDataProvider } from './dataProvider.js';
var CSSDataManager = /** @class */ (function () {
    function CSSDataManager(options) {
        this.dataProviders = [];
        this._propertySet = {};
        this._atDirectiveSet = {};
        this._pseudoClassSet = {};
        this._pseudoElementSet = {};
        this._properties = [];
        this._atDirectives = [];
        this._pseudoClasses = [];
        this._pseudoElements = [];
        this.setDataProviders((options === null || options === void 0 ? void 0 : options.useDefaultDataProvider) !== false, (options === null || options === void 0 ? void 0 : options.customDataProviders) || []);
    }
    CSSDataManager.prototype.setDataProviders = function (builtIn, providers) {
        var _a;
        this.dataProviders = [];
        if (builtIn) {
            this.dataProviders.push(new CSSDataProvider(cssData));
        }
        (_a = this.dataProviders).push.apply(_a, providers);
        this.collectData();
    };
    /**
     * Collect all data  & handle duplicates
     */
    CSSDataManager.prototype.collectData = function () {
        var _this = this;
        this._propertySet = {};
        this._atDirectiveSet = {};
        this._pseudoClassSet = {};
        this._pseudoElementSet = {};
        this.dataProviders.forEach(function (provider) {
            provider.provideProperties().forEach(function (p) {
                if (!_this._propertySet[p.name]) {
                    _this._propertySet[p.name] = p;
                }
            });
            provider.provideAtDirectives().forEach(function (p) {
                if (!_this._atDirectiveSet[p.name]) {
                    _this._atDirectiveSet[p.name] = p;
                }
            });
            provider.providePseudoClasses().forEach(function (p) {
                if (!_this._pseudoClassSet[p.name]) {
                    _this._pseudoClassSet[p.name] = p;
                }
            });
            provider.providePseudoElements().forEach(function (p) {
                if (!_this._pseudoElementSet[p.name]) {
                    _this._pseudoElementSet[p.name] = p;
                }
            });
        });
        this._properties = objects.values(this._propertySet);
        this._atDirectives = objects.values(this._atDirectiveSet);
        this._pseudoClasses = objects.values(this._pseudoClassSet);
        this._pseudoElements = objects.values(this._pseudoElementSet);
    };
    CSSDataManager.prototype.getProperty = function (name) { return this._propertySet[name]; };
    CSSDataManager.prototype.getAtDirective = function (name) { return this._atDirectiveSet[name]; };
    CSSDataManager.prototype.getPseudoClass = function (name) { return this._pseudoClassSet[name]; };
    CSSDataManager.prototype.getPseudoElement = function (name) { return this._pseudoElementSet[name]; };
    CSSDataManager.prototype.getProperties = function () {
        return this._properties;
    };
    CSSDataManager.prototype.getAtDirectives = function () {
        return this._atDirectives;
    };
    CSSDataManager.prototype.getPseudoClasses = function () {
        return this._pseudoClasses;
    };
    CSSDataManager.prototype.getPseudoElements = function () {
        return this._pseudoElements;
    };
    CSSDataManager.prototype.isKnownProperty = function (name) {
        return name.toLowerCase() in this._propertySet;
    };
    CSSDataManager.prototype.isStandardProperty = function (name) {
        return this.isKnownProperty(name) &&
            (!this._propertySet[name.toLowerCase()].status || this._propertySet[name.toLowerCase()].status === 'standard');
    };
    return CSSDataManager;
}());
export { CSSDataManager };
