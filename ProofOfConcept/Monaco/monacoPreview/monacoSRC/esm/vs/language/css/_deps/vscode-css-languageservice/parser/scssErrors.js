/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import * as nls from './../../../fillers/vscode-nls.js';
var localize = nls.loadMessageBundle();
var SCSSIssueType = /** @class */ (function () {
    function SCSSIssueType(id, message) {
        this.id = id;
        this.message = message;
    }
    return SCSSIssueType;
}());
export { SCSSIssueType };
export var SCSSParseError = {
    FromExpected: new SCSSIssueType('scss-fromexpected', localize('expected.from', "'from' expected")),
    ThroughOrToExpected: new SCSSIssueType('scss-throughexpected', localize('expected.through', "'through' or 'to' expected")),
    InExpected: new SCSSIssueType('scss-fromexpected', localize('expected.in', "'in' expected")),
};
