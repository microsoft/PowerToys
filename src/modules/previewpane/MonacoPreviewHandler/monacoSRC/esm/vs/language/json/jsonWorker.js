/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
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
import * as jsonService from './_deps/vscode-json-languageservice/jsonLanguageService.js';
import { URI } from './_deps/vscode-uri/index.js';
var defaultSchemaRequestService;
if (typeof fetch !== 'undefined') {
    defaultSchemaRequestService = function (url) {
        return fetch(url).then(function (response) { return response.text(); });
    };
}
var JSONWorker = /** @class */ (function () {
    function JSONWorker(ctx, createData) {
        this._ctx = ctx;
        this._languageSettings = createData.languageSettings;
        this._languageId = createData.languageId;
        this._languageService = jsonService.getLanguageService({
            workspaceContext: {
                resolveRelativePath: function (relativePath, resource) {
                    var base = resource.substr(0, resource.lastIndexOf('/') + 1);
                    return resolvePath(base, relativePath);
                }
            },
            schemaRequestService: createData.enableSchemaRequest && defaultSchemaRequestService
        });
        this._languageService.configure(this._languageSettings);
    }
    JSONWorker.prototype.doValidation = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            var document, jsonDocument;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                if (document) {
                    jsonDocument = this._languageService.parseJSONDocument(document);
                    return [2 /*return*/, this._languageService.doValidation(document, jsonDocument, this._languageSettings)];
                }
                return [2 /*return*/, Promise.resolve([])];
            });
        });
    };
    JSONWorker.prototype.doComplete = function (uri, position) {
        return __awaiter(this, void 0, void 0, function () {
            var document, jsonDocument;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                jsonDocument = this._languageService.parseJSONDocument(document);
                return [2 /*return*/, this._languageService.doComplete(document, position, jsonDocument)];
            });
        });
    };
    JSONWorker.prototype.doResolve = function (item) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                return [2 /*return*/, this._languageService.doResolve(item)];
            });
        });
    };
    JSONWorker.prototype.doHover = function (uri, position) {
        return __awaiter(this, void 0, void 0, function () {
            var document, jsonDocument;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                jsonDocument = this._languageService.parseJSONDocument(document);
                return [2 /*return*/, this._languageService.doHover(document, position, jsonDocument)];
            });
        });
    };
    JSONWorker.prototype.format = function (uri, range, options) {
        return __awaiter(this, void 0, void 0, function () {
            var document, textEdits;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                textEdits = this._languageService.format(document, range, options);
                return [2 /*return*/, Promise.resolve(textEdits)];
            });
        });
    };
    JSONWorker.prototype.resetSchema = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                return [2 /*return*/, Promise.resolve(this._languageService.resetSchema(uri))];
            });
        });
    };
    JSONWorker.prototype.findDocumentSymbols = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            var document, jsonDocument, symbols;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                jsonDocument = this._languageService.parseJSONDocument(document);
                symbols = this._languageService.findDocumentSymbols(document, jsonDocument);
                return [2 /*return*/, Promise.resolve(symbols)];
            });
        });
    };
    JSONWorker.prototype.findDocumentColors = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            var document, jsonDocument, colorSymbols;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                jsonDocument = this._languageService.parseJSONDocument(document);
                colorSymbols = this._languageService.findDocumentColors(document, jsonDocument);
                return [2 /*return*/, Promise.resolve(colorSymbols)];
            });
        });
    };
    JSONWorker.prototype.getColorPresentations = function (uri, color, range) {
        return __awaiter(this, void 0, void 0, function () {
            var document, jsonDocument, colorPresentations;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                jsonDocument = this._languageService.parseJSONDocument(document);
                colorPresentations = this._languageService.getColorPresentations(document, jsonDocument, color, range);
                return [2 /*return*/, Promise.resolve(colorPresentations)];
            });
        });
    };
    JSONWorker.prototype.getFoldingRanges = function (uri, context) {
        return __awaiter(this, void 0, void 0, function () {
            var document, ranges;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                ranges = this._languageService.getFoldingRanges(document, context);
                return [2 /*return*/, Promise.resolve(ranges)];
            });
        });
    };
    JSONWorker.prototype.getSelectionRanges = function (uri, positions) {
        return __awaiter(this, void 0, void 0, function () {
            var document, jsonDocument, ranges;
            return __generator(this, function (_a) {
                document = this._getTextDocument(uri);
                jsonDocument = this._languageService.parseJSONDocument(document);
                ranges = this._languageService.getSelectionRanges(document, positions, jsonDocument);
                return [2 /*return*/, Promise.resolve(ranges)];
            });
        });
    };
    JSONWorker.prototype._getTextDocument = function (uri) {
        var models = this._ctx.getMirrorModels();
        for (var _i = 0, models_1 = models; _i < models_1.length; _i++) {
            var model = models_1[_i];
            if (model.uri.toString() === uri) {
                return jsonService.TextDocument.create(uri, this._languageId, model.version, model.getValue());
            }
        }
        return null;
    };
    return JSONWorker;
}());
export { JSONWorker };
// URI path utilities, will (hopefully) move to vscode-uri
var Slash = '/'.charCodeAt(0);
var Dot = '.'.charCodeAt(0);
function isAbsolutePath(path) {
    return path.charCodeAt(0) === Slash;
}
function resolvePath(uriString, path) {
    if (isAbsolutePath(path)) {
        var uri = URI.parse(uriString);
        var parts = path.split('/');
        return uri.with({ path: normalizePath(parts) }).toString();
    }
    return joinPath(uriString, path);
}
function normalizePath(parts) {
    var newParts = [];
    for (var _i = 0, parts_1 = parts; _i < parts_1.length; _i++) {
        var part = parts_1[_i];
        if (part.length === 0 || (part.length === 1 && part.charCodeAt(0) === Dot)) {
            // ignore
        }
        else if (part.length === 2 && part.charCodeAt(0) === Dot && part.charCodeAt(1) === Dot) {
            newParts.pop();
        }
        else {
            newParts.push(part);
        }
    }
    if (parts.length > 1 && parts[parts.length - 1].length === 0) {
        newParts.push('');
    }
    var res = newParts.join('/');
    if (parts[0].length === 0) {
        res = '/' + res;
    }
    return res;
}
function joinPath(uriString) {
    var paths = [];
    for (var _i = 1; _i < arguments.length; _i++) {
        paths[_i - 1] = arguments[_i];
    }
    var uri = URI.parse(uriString);
    var parts = uri.path.split('/');
    for (var _a = 0, paths_1 = paths; _a < paths_1.length; _a++) {
        var path = paths_1[_a];
        parts.push.apply(parts, path.split('/'));
    }
    return uri.with({ path: normalizePath(parts) }).toString();
}
export function create(ctx, createData) {
    return new JSONWorker(ctx, createData);
}
