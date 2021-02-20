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
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g;
    return g = { next: verb(0), "throw": verb(1), "return": verb(2) }, typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (_) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
import { CSSNavigation } from './cssNavigation.js';
import * as nodes from '../parser/cssNodes.js';
import { URI, Utils } from './../../vscode-uri/index.js';
import { startsWith } from '../utils/strings.js';
var SCSSNavigation = /** @class */ (function (_super) {
    __extends(SCSSNavigation, _super);
    function SCSSNavigation(fileSystemProvider) {
        return _super.call(this, fileSystemProvider) || this;
    }
    SCSSNavigation.prototype.isRawStringDocumentLinkNode = function (node) {
        return (_super.prototype.isRawStringDocumentLinkNode.call(this, node) ||
            node.type === nodes.NodeType.Use ||
            node.type === nodes.NodeType.Forward);
    };
    SCSSNavigation.prototype.resolveRelativeReference = function (ref, documentUri, documentContext) {
        return __awaiter(this, void 0, void 0, function () {
            function toPathVariations(uri) {
                // No valid path
                if (uri.path === '') {
                    return undefined;
                }
                // No variation for links that ends with suffix
                if (uri.path.endsWith('.scss') || uri.path.endsWith('.css')) {
                    return undefined;
                }
                // If a link is like a/, try resolving a/index.scss and a/_index.scss
                if (uri.path.endsWith('/')) {
                    return [
                        uri.with({ path: uri.path + 'index.scss' }).toString(),
                        uri.with({ path: uri.path + '_index.scss' }).toString()
                    ];
                }
                // Use `uri.path` since it's normalized to use `/` in all platforms
                var pathFragments = uri.path.split('/');
                var basename = pathFragments[pathFragments.length - 1];
                var pathWithoutBasename = uri.path.slice(0, -basename.length);
                // No variation for links such as _a
                if (basename.startsWith('_')) {
                    if (uri.path.endsWith('.scss')) {
                        return undefined;
                    }
                    else {
                        return [uri.with({ path: uri.path + '.scss' }).toString()];
                    }
                }
                var normalizedBasename = basename + '.scss';
                var documentUriWithBasename = function (newBasename) {
                    return uri.with({ path: pathWithoutBasename + newBasename }).toString();
                };
                var normalizedPath = documentUriWithBasename(normalizedBasename);
                var underScorePath = documentUriWithBasename('_' + normalizedBasename);
                var indexPath = documentUriWithBasename(normalizedBasename.slice(0, -5) + '/index.scss');
                var indexUnderscoreUri = documentUriWithBasename(normalizedBasename.slice(0, -5) + '/_index.scss');
                var cssPath = documentUriWithBasename(normalizedBasename.slice(0, -5) + '.css');
                return [normalizedPath, underScorePath, indexPath, indexUnderscoreUri, cssPath];
            }
            var target, parsedUri, pathVariations, j, e_1;
            return __generator(this, function (_a) {
                switch (_a.label) {
                    case 0:
                        if (startsWith(ref, 'sass:')) {
                            return [2 /*return*/, undefined]; // sass library
                        }
                        return [4 /*yield*/, _super.prototype.resolveRelativeReference.call(this, ref, documentUri, documentContext)];
                    case 1:
                        target = _a.sent();
                        if (!(this.fileSystemProvider && target)) return [3 /*break*/, 8];
                        parsedUri = URI.parse(target);
                        if (!(parsedUri.path && Utils.extname(parsedUri).length === 0)) return [3 /*break*/, 8];
                        _a.label = 2;
                    case 2:
                        _a.trys.push([2, 7, , 8]);
                        pathVariations = toPathVariations(parsedUri);
                        if (!pathVariations) return [3 /*break*/, 6];
                        j = 0;
                        _a.label = 3;
                    case 3:
                        if (!(j < pathVariations.length)) return [3 /*break*/, 6];
                        return [4 /*yield*/, this.fileExists(pathVariations[j])];
                    case 4:
                        if (_a.sent()) {
                            return [2 /*return*/, pathVariations[j]];
                        }
                        _a.label = 5;
                    case 5:
                        j++;
                        return [3 /*break*/, 3];
                    case 6: return [2 /*return*/, undefined];
                    case 7:
                        e_1 = _a.sent();
                        return [3 /*break*/, 8];
                    case 8: return [2 /*return*/, target];
                }
            });
        });
    };
    return SCSSNavigation;
}(CSSNavigation));
export { SCSSNavigation };
