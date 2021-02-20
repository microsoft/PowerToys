/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { HTMLDataProvider } from './dataProvider.js';
import { htmlData } from './data/webCustomData.js';
var HTMLDataManager = /** @class */ (function () {
    function HTMLDataManager(options) {
        this.dataProviders = [];
        this.setDataProviders(options.useDefaultDataProvider !== false, options.customDataProviders || []);
    }
    HTMLDataManager.prototype.setDataProviders = function (builtIn, providers) {
        var _a;
        this.dataProviders = [];
        if (builtIn) {
            this.dataProviders.push(new HTMLDataProvider('html5', htmlData));
        }
        (_a = this.dataProviders).push.apply(_a, providers);
    };
    HTMLDataManager.prototype.getDataProviders = function () {
        return this.dataProviders;
    };
    return HTMLDataManager;
}());
export { HTMLDataManager };
