/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
var __extends = (this && this.__extends) || (function () {
    var extendStatics = function (d, b) {
        extendStatics = Object.setPrototypeOf ||
            ({ __proto__: [] } instanceof Array && function (d, b) { d.__proto__ = b; }) ||
            function (d, b) { for (var p in b) if (Object.prototype.hasOwnProperty.call(b, p)) d[p] = b[p]; };
        return extendStatics(d, b);
    };
    return function (d, b) {
        extendStatics(d, b);
        function __() { this.constructor = d; }
        d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
    };
})();
import { TokenType, Scanner } from './cssScanner.js';
var _FSL = '/'.charCodeAt(0);
var _NWL = '\n'.charCodeAt(0);
var _CAR = '\r'.charCodeAt(0);
var _LFD = '\f'.charCodeAt(0);
var _DLR = '$'.charCodeAt(0);
var _HSH = '#'.charCodeAt(0);
var _CUL = '{'.charCodeAt(0);
var _EQS = '='.charCodeAt(0);
var _BNG = '!'.charCodeAt(0);
var _LAN = '<'.charCodeAt(0);
var _RAN = '>'.charCodeAt(0);
var _DOT = '.'.charCodeAt(0);
var _ATS = '@'.charCodeAt(0);
var customTokenValue = TokenType.CustomToken;
export var VariableName = customTokenValue++;
export var InterpolationFunction = customTokenValue++;
export var Default = customTokenValue++;
export var EqualsOperator = customTokenValue++;
export var NotEqualsOperator = customTokenValue++;
export var GreaterEqualsOperator = customTokenValue++;
export var SmallerEqualsOperator = customTokenValue++;
export var Ellipsis = customTokenValue++;
export var Module = customTokenValue++;
var SCSSScanner = /** @class */ (function (_super) {
    __extends(SCSSScanner, _super);
    function SCSSScanner() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    SCSSScanner.prototype.scanNext = function (offset) {
        // scss variable
        if (this.stream.advanceIfChar(_DLR)) {
            var content = ['$'];
            if (this.ident(content)) {
                return this.finishToken(offset, VariableName, content.join(''));
            }
            else {
                this.stream.goBackTo(offset);
            }
        }
        // scss: interpolation function #{..})
        if (this.stream.advanceIfChars([_HSH, _CUL])) {
            return this.finishToken(offset, InterpolationFunction);
        }
        // operator ==
        if (this.stream.advanceIfChars([_EQS, _EQS])) {
            return this.finishToken(offset, EqualsOperator);
        }
        // operator !=
        if (this.stream.advanceIfChars([_BNG, _EQS])) {
            return this.finishToken(offset, NotEqualsOperator);
        }
        // operators <, <=
        if (this.stream.advanceIfChar(_LAN)) {
            if (this.stream.advanceIfChar(_EQS)) {
                return this.finishToken(offset, SmallerEqualsOperator);
            }
            return this.finishToken(offset, TokenType.Delim);
        }
        // ooperators >, >=
        if (this.stream.advanceIfChar(_RAN)) {
            if (this.stream.advanceIfChar(_EQS)) {
                return this.finishToken(offset, GreaterEqualsOperator);
            }
            return this.finishToken(offset, TokenType.Delim);
        }
        // ellipis
        if (this.stream.advanceIfChars([_DOT, _DOT, _DOT])) {
            return this.finishToken(offset, Ellipsis);
        }
        return _super.prototype.scanNext.call(this, offset);
    };
    SCSSScanner.prototype.comment = function () {
        if (_super.prototype.comment.call(this)) {
            return true;
        }
        if (!this.inURL && this.stream.advanceIfChars([_FSL, _FSL])) {
            this.stream.advanceWhileChar(function (ch) {
                switch (ch) {
                    case _NWL:
                    case _CAR:
                    case _LFD:
                        return false;
                    default:
                        return true;
                }
            });
            return true;
        }
        else {
            return false;
        }
    };
    return SCSSScanner;
}(Scanner));
export { SCSSScanner };
