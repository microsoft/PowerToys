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
import { FileType, CompletionItemKind, TextEdit, Range, Position } from '../cssLanguageTypes.js';
import { startsWith, endsWith } from '../utils/strings.js';
import { joinPath } from '../utils/resources.js';
var PathCompletionParticipant = /** @class */ (function () {
    function PathCompletionParticipant(readDirectory) {
        this.readDirectory = readDirectory;
        this.literalCompletions = [];
        this.importCompletions = [];
    }
    PathCompletionParticipant.prototype.onCssURILiteralValue = function (context) {
        this.literalCompletions.push(context);
    };
    PathCompletionParticipant.prototype.onCssImportPath = function (context) {
        this.importCompletions.push(context);
    };
    PathCompletionParticipant.prototype.computeCompletions = function (document, documentContext) {
        return __awaiter(this, void 0, void 0, function () {
            var result, _i, _a, literalCompletion, uriValue, fullValue, items, _b, items_1, item, _c, _d, importCompletion, pathValue, fullValue, suggestions, _e, suggestions_1, item;
            return __generator(this, function (_f) {
                switch (_f.label) {
                    case 0:
                        result = { items: [], isIncomplete: false };
                        _i = 0, _a = this.literalCompletions;
                        _f.label = 1;
                    case 1:
                        if (!(_i < _a.length)) return [3 /*break*/, 5];
                        literalCompletion = _a[_i];
                        uriValue = literalCompletion.uriValue;
                        fullValue = stripQuotes(uriValue);
                        if (!(fullValue === '.' || fullValue === '..')) return [3 /*break*/, 2];
                        result.isIncomplete = true;
                        return [3 /*break*/, 4];
                    case 2: return [4 /*yield*/, this.providePathSuggestions(uriValue, literalCompletion.position, literalCompletion.range, document, documentContext)];
                    case 3:
                        items = _f.sent();
                        for (_b = 0, items_1 = items; _b < items_1.length; _b++) {
                            item = items_1[_b];
                            result.items.push(item);
                        }
                        _f.label = 4;
                    case 4:
                        _i++;
                        return [3 /*break*/, 1];
                    case 5:
                        _c = 0, _d = this.importCompletions;
                        _f.label = 6;
                    case 6:
                        if (!(_c < _d.length)) return [3 /*break*/, 10];
                        importCompletion = _d[_c];
                        pathValue = importCompletion.pathValue;
                        fullValue = stripQuotes(pathValue);
                        if (!(fullValue === '.' || fullValue === '..')) return [3 /*break*/, 7];
                        result.isIncomplete = true;
                        return [3 /*break*/, 9];
                    case 7: return [4 /*yield*/, this.providePathSuggestions(pathValue, importCompletion.position, importCompletion.range, document, documentContext)];
                    case 8:
                        suggestions = _f.sent();
                        if (document.languageId === 'scss') {
                            suggestions.forEach(function (s) {
                                if (startsWith(s.label, '_') && endsWith(s.label, '.scss')) {
                                    if (s.textEdit) {
                                        s.textEdit.newText = s.label.slice(1, -5);
                                    }
                                    else {
                                        s.label = s.label.slice(1, -5);
                                    }
                                }
                            });
                        }
                        for (_e = 0, suggestions_1 = suggestions; _e < suggestions_1.length; _e++) {
                            item = suggestions_1[_e];
                            result.items.push(item);
                        }
                        _f.label = 9;
                    case 9:
                        _c++;
                        return [3 /*break*/, 6];
                    case 10: return [2 /*return*/, result];
                }
            });
        });
    };
    PathCompletionParticipant.prototype.providePathSuggestions = function (pathValue, position, range, document, documentContext) {
        return __awaiter(this, void 0, void 0, function () {
            var fullValue, isValueQuoted, valueBeforeCursor, currentDocUri, fullValueRange, replaceRange, valueBeforeLastSlash, parentDir, result, infos, _i, infos_1, _a, name, type, e_1;
            return __generator(this, function (_b) {
                switch (_b.label) {
                    case 0:
                        fullValue = stripQuotes(pathValue);
                        isValueQuoted = startsWith(pathValue, "'") || startsWith(pathValue, "\"");
                        valueBeforeCursor = isValueQuoted
                            ? fullValue.slice(0, position.character - (range.start.character + 1))
                            : fullValue.slice(0, position.character - range.start.character);
                        currentDocUri = document.uri;
                        fullValueRange = isValueQuoted ? shiftRange(range, 1, -1) : range;
                        replaceRange = pathToReplaceRange(valueBeforeCursor, fullValue, fullValueRange);
                        valueBeforeLastSlash = valueBeforeCursor.substring(0, valueBeforeCursor.lastIndexOf('/') + 1);
                        parentDir = documentContext.resolveReference(valueBeforeLastSlash || '.', currentDocUri);
                        if (!parentDir) return [3 /*break*/, 4];
                        _b.label = 1;
                    case 1:
                        _b.trys.push([1, 3, , 4]);
                        result = [];
                        return [4 /*yield*/, this.readDirectory(parentDir)];
                    case 2:
                        infos = _b.sent();
                        for (_i = 0, infos_1 = infos; _i < infos_1.length; _i++) {
                            _a = infos_1[_i], name = _a[0], type = _a[1];
                            // Exclude paths that start with `.`
                            if (name.charCodeAt(0) !== CharCode_dot && (type === FileType.Directory || joinPath(parentDir, name) !== currentDocUri)) {
                                result.push(createCompletionItem(name, type === FileType.Directory, replaceRange));
                            }
                        }
                        return [2 /*return*/, result];
                    case 3:
                        e_1 = _b.sent();
                        return [3 /*break*/, 4];
                    case 4: return [2 /*return*/, []];
                }
            });
        });
    };
    return PathCompletionParticipant;
}());
export { PathCompletionParticipant };
var CharCode_dot = '.'.charCodeAt(0);
function stripQuotes(fullValue) {
    if (startsWith(fullValue, "'") || startsWith(fullValue, "\"")) {
        return fullValue.slice(1, -1);
    }
    else {
        return fullValue;
    }
}
function pathToReplaceRange(valueBeforeCursor, fullValue, fullValueRange) {
    var replaceRange;
    var lastIndexOfSlash = valueBeforeCursor.lastIndexOf('/');
    if (lastIndexOfSlash === -1) {
        replaceRange = fullValueRange;
    }
    else {
        // For cases where cursor is in the middle of attribute value, like <script src="./s|rc/test.js">
        // Find the last slash before cursor, and calculate the start of replace range from there
        var valueAfterLastSlash = fullValue.slice(lastIndexOfSlash + 1);
        var startPos = shiftPosition(fullValueRange.end, -valueAfterLastSlash.length);
        // If whitespace exists, replace until it
        var whitespaceIndex = valueAfterLastSlash.indexOf(' ');
        var endPos = void 0;
        if (whitespaceIndex !== -1) {
            endPos = shiftPosition(startPos, whitespaceIndex);
        }
        else {
            endPos = fullValueRange.end;
        }
        replaceRange = Range.create(startPos, endPos);
    }
    return replaceRange;
}
function createCompletionItem(name, isDir, replaceRange) {
    if (isDir) {
        name = name + '/';
        return {
            label: escapePath(name),
            kind: CompletionItemKind.Folder,
            textEdit: TextEdit.replace(replaceRange, escapePath(name)),
            command: {
                title: 'Suggest',
                command: 'editor.action.triggerSuggest'
            }
        };
    }
    else {
        return {
            label: escapePath(name),
            kind: CompletionItemKind.File,
            textEdit: TextEdit.replace(replaceRange, escapePath(name))
        };
    }
}
// Escape https://www.w3.org/TR/CSS1/#url
function escapePath(p) {
    return p.replace(/(\s|\(|\)|,|"|')/g, '\\$1');
}
function shiftPosition(pos, offset) {
    return Position.create(pos.line, pos.character + offset);
}
function shiftRange(range, startOffset, endOffset) {
    var start = shiftPosition(range.start, startOffset);
    var end = shiftPosition(range.end, endOffset);
    return Range.create(start, end);
}
