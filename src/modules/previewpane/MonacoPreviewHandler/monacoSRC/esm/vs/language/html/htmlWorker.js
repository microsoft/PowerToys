/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __assign = (this && this.__assign) || function () {
    __assign = Object.assign || function(t) {
        for (var s, i = 1, n = arguments.length; i < n; i++) {
            s = arguments[i];
            for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
                t[p] = s[p];
        }
        return t;
    };
    return __assign.apply(this, arguments);
};
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
import * as htmlService from './_deps/vscode-html-languageservice/htmlLanguageService.js';
var HTMLWorker = /** @class */ (function () {
    function HTMLWorker(ctx, createData) {
        this._ctx = ctx;
        this._languageSettings = createData.languageSettings;
        this._languageId = createData.languageId;
        this._languageService = htmlService.getLanguageService();
    }
    HTMLWorker.prototype.doValidation = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                // not yet suported
                return [2 /*return*/, Promise.resolve([])];
            });
        });
    };
    HTMLWorker.prototype.doComplete = function (uri, position) {
        return __awaiter(this, void 0, void 0, function () {
            var document, htmlDocument;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                htmlDocument = this._languageService.parseHTMLDocument(document);
                return [2 /*return*/, Promise.resolve(this._languageService.doComplete(document, position, htmlDocument, this._languageSettings && this._languageSettings.suggest))];
            });
        });
    };
    HTMLWorker.prototype.format = function (uri, range, options) {
        return __awaiter(this, void 0, void 0, function () {
            var document, formattingOptions, textEdits;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                formattingOptions = __assign(__assign({}, this._languageSettings.format), options);
                textEdits = this._languageService.format(document, range, formattingOptions);
                return [2 /*return*/, Promise.resolve(textEdits)];
            });
        });
    };
    HTMLWorker.prototype.doHover = function (uri, position) {
        return __awaiter(this, void 0, void 0, function () {
            var document, htmlDocument, hover;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                htmlDocument = this._languageService.parseHTMLDocument(document);
                hover = this._languageService.doHover(document, position, htmlDocument);
                return [2 /*return*/, Promise.resolve(hover)];
            });
        });
    };
    HTMLWorker.prototype.findDocumentHighlights = function (uri, position) {
        return __awaiter(this, void 0, void 0, function () {
            var document, htmlDocument, highlights;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                htmlDocument = this._languageService.parseHTMLDocument(document);
                highlights = this._languageService.findDocumentHighlights(document, position, htmlDocument);
                return [2 /*return*/, Promise.resolve(highlights)];
            });
        });
    };
    HTMLWorker.prototype.findDocumentLinks = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            var document, links;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                links = this._languageService.findDocumentLinks(document, null);
                return [2 /*return*/, Promise.resolve(links)];
            });
        });
    };
    HTMLWorker.prototype.findDocumentSymbols = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            var document, htmlDocument, symbols;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                htmlDocument = this._languageService.parseHTMLDocument(document);
                symbols = this._languageService.findDocumentSymbols(document, htmlDocument);
                return [2 /*return*/, Promise.resolve(symbols)];
            });
        });
    };
    HTMLWorker.prototype.getFoldingRanges = function (uri, context) {
        return __awaiter(this, void 0, void 0, function () {
            var document, ranges;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                ranges = this._languageService.getFoldingRanges(document, context);
                return [2 /*return*/, Promise.resolve(ranges)];
            });
        });
    };
    HTMLWorker.prototype.getSelectionRanges = function (uri, positions) {
        return __awaiter(this, void 0, void 0, function () {
            var document, ranges;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                ranges = this._languageService.getSelectionRanges(document, positions);
                return [2 /*return*/, Promise.resolve(ranges)];
            });
        });
    };
    HTMLWorker.prototype.doRename = function (uri, position, newName) {
        return __awaiter(this, void 0, void 0, function () {
            var document, htmlDocument, renames;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                htmlDocument = this._languageService.parseHTMLDocument(document);
                renames = this._languageService.doRename(document, position, newName, htmlDocument);
                return [2 /*return*/, Promise.resolve(renames)];
            });
        });
    };
    HTMLWorker.prototype._getTextDocument = function (uri) {
        var models = this._ctx.getMirrorModels();
        for (var _i = 0, models_1 = models; _i < models_1.length; _i++) {
            var model = models_1[_i];
            if (model.uri.toString() === uri) {
                return htmlService.TextDocument.create(uri, this._languageId, model.version, model.getValue());
            }
        }
        return null;
    };
    return HTMLWorker;
}());
export { HTMLWorker };
export function create(ctx, createData) {
    return new HTMLWorker(ctx, createData);
}
