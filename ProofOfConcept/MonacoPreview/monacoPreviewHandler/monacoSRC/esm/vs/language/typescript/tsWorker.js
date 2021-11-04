/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
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
import * as ts from './lib/typescriptServices.js';
import { libFileMap } from './lib/lib.js';
/**
 * Loading a default lib as a source file will mess up TS completely.
 * So our strategy is to hide such a text model from TS.
 * See https://github.com/microsoft/monaco-editor/issues/2182
 */
function fileNameIsLib(resource) {
    if (typeof resource === 'string') {
        if (/^file:\/\/\//.test(resource)) {
            return !!libFileMap[resource.substr(8)];
        }
        return false;
    }
    if (resource.path.indexOf('/lib.') === 0) {
        return !!libFileMap[resource.path.slice(1)];
    }
    return false;
}
var TypeScriptWorker = /** @class */ (function () {
    function TypeScriptWorker(ctx, createData) {
        this._extraLibs = Object.create(null);
        this._languageService = ts.createLanguageService(this);
        this._ctx = ctx;
        this._compilerOptions = createData.compilerOptions;
        this._extraLibs = createData.extraLibs;
    }
    // --- language service host ---------------
    TypeScriptWorker.prototype.getCompilationSettings = function () {
        return this._compilerOptions;
    };
    TypeScriptWorker.prototype.getScriptFileNames = function () {
        var allModels = this._ctx.getMirrorModels().map(function (model) { return model.uri; });
        var models = allModels.filter(function (uri) { return !fileNameIsLib(uri); }).map(function (uri) { return uri.toString(); });
        return models.concat(Object.keys(this._extraLibs));
    };
    TypeScriptWorker.prototype._getModel = function (fileName) {
        var models = this._ctx.getMirrorModels();
        for (var i = 0; i < models.length; i++) {
            if (models[i].uri.toString() === fileName) {
                return models[i];
            }
        }
        return null;
    };
    TypeScriptWorker.prototype.getScriptVersion = function (fileName) {
        var model = this._getModel(fileName);
        if (model) {
            return model.version.toString();
        }
        else if (this.isDefaultLibFileName(fileName)) {
            // default lib is static
            return '1';
        }
        else if (fileName in this._extraLibs) {
            return String(this._extraLibs[fileName].version);
        }
        return '';
    };
    TypeScriptWorker.prototype.getScriptText = function (fileName) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                return [2 /*return*/, this._getScriptText(fileName)];
            });
        });
    };
    TypeScriptWorker.prototype._getScriptText = function (fileName) {
        var text;
        var model = this._getModel(fileName);
        var libizedFileName = 'lib.' + fileName + '.d.ts';
        if (model) {
            // a true editor model
            text = model.getValue();
        }
        else if (fileName in libFileMap) {
            text = libFileMap[fileName];
        }
        else if (libizedFileName in libFileMap) {
            text = libFileMap[libizedFileName];
        }
        else if (fileName in this._extraLibs) {
            // extra lib
            text = this._extraLibs[fileName].content;
        }
        else {
            return;
        }
        return text;
    };
    TypeScriptWorker.prototype.getScriptSnapshot = function (fileName) {
        var text = this._getScriptText(fileName);
        if (text === undefined) {
            return;
        }
        return {
            getText: function (start, end) { return text.substring(start, end); },
            getLength: function () { return text.length; },
            getChangeRange: function () { return undefined; }
        };
    };
    TypeScriptWorker.prototype.getScriptKind = function (fileName) {
        var suffix = fileName.substr(fileName.lastIndexOf('.') + 1);
        switch (suffix) {
            case 'ts':
                return ts.ScriptKind.TS;
            case 'tsx':
                return ts.ScriptKind.TSX;
            case 'js':
                return ts.ScriptKind.JS;
            case 'jsx':
                return ts.ScriptKind.JSX;
            default:
                return this.getCompilationSettings().allowJs ? ts.ScriptKind.JS : ts.ScriptKind.TS;
        }
    };
    TypeScriptWorker.prototype.getCurrentDirectory = function () {
        return '';
    };
    TypeScriptWorker.prototype.getDefaultLibFileName = function (options) {
        switch (options.target) {
            case 99 /* ESNext */:
                var esnext = 'lib.esnext.full.d.ts';
                if (esnext in libFileMap || esnext in this._extraLibs)
                    return esnext;
            case 7 /* ES2020 */:
            case 6 /* ES2019 */:
            case 5 /* ES2018 */:
            case 4 /* ES2017 */:
            case 3 /* ES2016 */:
            case 2 /* ES2015 */:
            default:
                // Support a dynamic lookup for the ES20XX version based on the target
                // which is safe unless TC39 changes their numbering system
                var eslib = "lib.es" + (2013 + (options.target || 99)) + ".full.d.ts";
                // Note: This also looks in _extraLibs, If you want
                // to add support for additional target options, you will need to
                // add the extra dts files to _extraLibs via the API.
                if (eslib in libFileMap || eslib in this._extraLibs) {
                    return eslib;
                }
                return 'lib.es6.d.ts'; // We don't use lib.es2015.full.d.ts due to breaking change.
            case 1:
            case 0:
                return 'lib.d.ts';
        }
    };
    TypeScriptWorker.prototype.isDefaultLibFileName = function (fileName) {
        return fileName === this.getDefaultLibFileName(this._compilerOptions);
    };
    TypeScriptWorker.prototype.getLibFiles = function () {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                return [2 /*return*/, libFileMap];
            });
        });
    };
    // --- language features
    TypeScriptWorker.clearFiles = function (diagnostics) {
        // Clear the `file` field, which cannot be JSON'yfied because it
        // contains cyclic data structures.
        diagnostics.forEach(function (diag) {
            diag.file = undefined;
            var related = diag.relatedInformation;
            if (related) {
                related.forEach(function (diag2) { return (diag2.file = undefined); });
            }
        });
        return diagnostics;
    };
    TypeScriptWorker.prototype.getSyntacticDiagnostics = function (fileName) {
        return __awaiter(this, void 0, void 0, function () {
            var diagnostics;
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                diagnostics = this._languageService.getSyntacticDiagnostics(fileName);
                return [2 /*return*/, TypeScriptWorker.clearFiles(diagnostics)];
            });
        });
    };
    TypeScriptWorker.prototype.getSemanticDiagnostics = function (fileName) {
        return __awaiter(this, void 0, void 0, function () {
            var diagnostics;
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                diagnostics = this._languageService.getSemanticDiagnostics(fileName);
                return [2 /*return*/, TypeScriptWorker.clearFiles(diagnostics)];
            });
        });
    };
    TypeScriptWorker.prototype.getSuggestionDiagnostics = function (fileName) {
        return __awaiter(this, void 0, void 0, function () {
            var diagnostics;
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                diagnostics = this._languageService.getSuggestionDiagnostics(fileName);
                return [2 /*return*/, TypeScriptWorker.clearFiles(diagnostics)];
            });
        });
    };
    TypeScriptWorker.prototype.getCompilerOptionsDiagnostics = function (fileName) {
        return __awaiter(this, void 0, void 0, function () {
            var diagnostics;
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                diagnostics = this._languageService.getCompilerOptionsDiagnostics();
                return [2 /*return*/, TypeScriptWorker.clearFiles(diagnostics)];
            });
        });
    };
    TypeScriptWorker.prototype.getCompletionsAtPosition = function (fileName, position) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, undefined];
                }
                return [2 /*return*/, this._languageService.getCompletionsAtPosition(fileName, position, undefined)];
            });
        });
    };
    TypeScriptWorker.prototype.getCompletionEntryDetails = function (fileName, position, entry) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                return [2 /*return*/, this._languageService.getCompletionEntryDetails(fileName, position, entry, undefined, undefined, undefined)];
            });
        });
    };
    TypeScriptWorker.prototype.getSignatureHelpItems = function (fileName, position, options) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, undefined];
                }
                return [2 /*return*/, this._languageService.getSignatureHelpItems(fileName, position, options)];
            });
        });
    };
    TypeScriptWorker.prototype.getQuickInfoAtPosition = function (fileName, position) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, undefined];
                }
                return [2 /*return*/, this._languageService.getQuickInfoAtPosition(fileName, position)];
            });
        });
    };
    TypeScriptWorker.prototype.getOccurrencesAtPosition = function (fileName, position) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, undefined];
                }
                return [2 /*return*/, this._languageService.getOccurrencesAtPosition(fileName, position)];
            });
        });
    };
    TypeScriptWorker.prototype.getDefinitionAtPosition = function (fileName, position) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, undefined];
                }
                return [2 /*return*/, this._languageService.getDefinitionAtPosition(fileName, position)];
            });
        });
    };
    TypeScriptWorker.prototype.getReferencesAtPosition = function (fileName, position) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, undefined];
                }
                return [2 /*return*/, this._languageService.getReferencesAtPosition(fileName, position)];
            });
        });
    };
    TypeScriptWorker.prototype.getNavigationBarItems = function (fileName) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                return [2 /*return*/, this._languageService.getNavigationBarItems(fileName)];
            });
        });
    };
    TypeScriptWorker.prototype.getFormattingEditsForDocument = function (fileName, options) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                return [2 /*return*/, this._languageService.getFormattingEditsForDocument(fileName, options)];
            });
        });
    };
    TypeScriptWorker.prototype.getFormattingEditsForRange = function (fileName, start, end, options) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                return [2 /*return*/, this._languageService.getFormattingEditsForRange(fileName, start, end, options)];
            });
        });
    };
    TypeScriptWorker.prototype.getFormattingEditsAfterKeystroke = function (fileName, postion, ch, options) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                return [2 /*return*/, this._languageService.getFormattingEditsAfterKeystroke(fileName, postion, ch, options)];
            });
        });
    };
    TypeScriptWorker.prototype.findRenameLocations = function (fileName, position, findInStrings, findInComments, providePrefixAndSuffixTextForRename) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, undefined];
                }
                return [2 /*return*/, this._languageService.findRenameLocations(fileName, position, findInStrings, findInComments, providePrefixAndSuffixTextForRename)];
            });
        });
    };
    TypeScriptWorker.prototype.getRenameInfo = function (fileName, position, options) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, { canRename: false, localizedErrorMessage: 'Cannot rename in lib file' }];
                }
                return [2 /*return*/, this._languageService.getRenameInfo(fileName, position, options)];
            });
        });
    };
    TypeScriptWorker.prototype.getEmitOutput = function (fileName) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, { outputFiles: [], emitSkipped: true }];
                }
                return [2 /*return*/, this._languageService.getEmitOutput(fileName)];
            });
        });
    };
    TypeScriptWorker.prototype.getCodeFixesAtPosition = function (fileName, start, end, errorCodes, formatOptions) {
        return __awaiter(this, void 0, void 0, function () {
            var preferences;
            return __generator(this, function (_a) {
                if (fileNameIsLib(fileName)) {
                    return [2 /*return*/, []];
                }
                preferences = {};
                try {
                    return [2 /*return*/, this._languageService.getCodeFixesAtPosition(fileName, start, end, errorCodes, formatOptions, preferences)];
                }
                catch (_b) {
                    return [2 /*return*/, []];
                }
                return [2 /*return*/];
            });
        });
    };
    TypeScriptWorker.prototype.updateExtraLibs = function (extraLibs) {
        return __awaiter(this, void 0, void 0, function () {
            return __generator(this, function (_a) {
                this._extraLibs = extraLibs;
                return [2 /*return*/];
            });
        });
    };
    return TypeScriptWorker;
}());
export { TypeScriptWorker };
export function create(ctx, createData) {
    var TSWorkerClass = TypeScriptWorker;
    if (createData.customWorkerPath) {
        if (typeof importScripts === 'undefined') {
            console.warn('Monaco is not using webworkers for background tasks, and that is needed to support the customWorkerPath flag');
        }
        else {
            importScripts(createData.customWorkerPath);
            var workerFactoryFunc = self.customTSWorkerFactory;
            if (!workerFactoryFunc) {
                throw new Error("The script at " + createData.customWorkerPath + " does not add customTSWorkerFactory to self");
            }
            TSWorkerClass = workerFactoryFunc(TypeScriptWorker, ts, libFileMap);
        }
    }
    return new TSWorkerClass(ctx, createData);
}
