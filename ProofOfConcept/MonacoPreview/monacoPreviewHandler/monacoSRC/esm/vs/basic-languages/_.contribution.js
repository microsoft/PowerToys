/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { languages } from './fillers/monaco-editor-core.js';
var languageDefinitions = {};
var lazyLanguageLoaders = {};
var LazyLanguageLoader = /** @class */ (function () {
    function LazyLanguageLoader(languageId) {
        var _this = this;
        this._languageId = languageId;
        this._loadingTriggered = false;
        this._lazyLoadPromise = new Promise(function (resolve, reject) {
            _this._lazyLoadPromiseResolve = resolve;
            _this._lazyLoadPromiseReject = reject;
        });
    }
    LazyLanguageLoader.getOrCreate = function (languageId) {
        if (!lazyLanguageLoaders[languageId]) {
            lazyLanguageLoaders[languageId] = new LazyLanguageLoader(languageId);
        }
        return lazyLanguageLoaders[languageId];
    };
    LazyLanguageLoader.prototype.whenLoaded = function () {
        return this._lazyLoadPromise;
    };
    LazyLanguageLoader.prototype.load = function () {
        var _this = this;
        if (!this._loadingTriggered) {
            this._loadingTriggered = true;
            languageDefinitions[this._languageId].loader().then(function (mod) { return _this._lazyLoadPromiseResolve(mod); }, function (err) { return _this._lazyLoadPromiseReject(err); });
        }
        return this._lazyLoadPromise;
    };
    return LazyLanguageLoader;
}());
export function loadLanguage(languageId) {
    return LazyLanguageLoader.getOrCreate(languageId).load();
}
export function registerLanguage(def) {
    var languageId = def.id;
    languageDefinitions[languageId] = def;
    languages.register(def);
    var lazyLanguageLoader = LazyLanguageLoader.getOrCreate(languageId);
    languages.setMonarchTokensProvider(languageId, lazyLanguageLoader.whenLoaded().then(function (mod) { return mod.language; }));
    languages.onLanguage(languageId, function () {
        lazyLanguageLoader.load().then(function (mod) {
            languages.setLanguageConfiguration(languageId, mod.conf);
        });
    });
}
