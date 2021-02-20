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
import * as nodes from '../parser/cssNodes.js';
import { Symbols } from '../parser/cssSymbolScope.js';
import * as languageFacts from '../languageFacts/facts.js';
import * as strings from '../utils/strings.js';
import { Position, CompletionItemKind, Range, TextEdit, InsertTextFormat, MarkupKind, CompletionItemTag } from '../cssLanguageTypes.js';
import * as nls from './../../../fillers/vscode-nls.js';
import { isDefined } from '../utils/objects.js';
import { PathCompletionParticipant } from './pathCompletion.js';
var localize = nls.loadMessageBundle();
var SnippetFormat = InsertTextFormat.Snippet;
var SortTexts;
(function (SortTexts) {
    // char code 32, comes before everything
    SortTexts["Enums"] = " ";
    SortTexts["Normal"] = "d";
    SortTexts["VendorPrefixed"] = "x";
    SortTexts["Term"] = "y";
    SortTexts["Variable"] = "z";
})(SortTexts || (SortTexts = {}));
var CSSCompletion = /** @class */ (function () {
    function CSSCompletion(variablePrefix, lsOptions, cssDataManager) {
        if (variablePrefix === void 0) { variablePrefix = null; }
        this.variablePrefix = variablePrefix;
        this.lsOptions = lsOptions;
        this.cssDataManager = cssDataManager;
        this.completionParticipants = [];
    }
    CSSCompletion.prototype.configure = function (settings) {
        this.settings = settings;
    };
    CSSCompletion.prototype.getSymbolContext = function () {
        if (!this.symbolContext) {
            this.symbolContext = new Symbols(this.styleSheet);
        }
        return this.symbolContext;
    };
    CSSCompletion.prototype.setCompletionParticipants = function (registeredCompletionParticipants) {
        this.completionParticipants = registeredCompletionParticipants || [];
    };
    CSSCompletion.prototype.doComplete2 = function (document, position, styleSheet, documentContext) {
        return __awaiter(this, void 0, void 0, function () {
            var participant, contributedParticipants, result, pathCompletionResult;
            return __generator(this, function (_a) {
                switch (_a.label) {
                    case 0:
                        if (!this.lsOptions.fileSystemProvider || !this.lsOptions.fileSystemProvider.readDirectory) {
                            return [2 /*return*/, this.doComplete(document, position, styleSheet)];
                        }
                        participant = new PathCompletionParticipant(this.lsOptions.fileSystemProvider.readDirectory);
                        contributedParticipants = this.completionParticipants;
                        this.completionParticipants = [participant].concat(contributedParticipants);
                        result = this.doComplete(document, position, styleSheet);
                        _a.label = 1;
                    case 1:
                        _a.trys.push([1, , 3, 4]);
                        return [4 /*yield*/, participant.computeCompletions(document, documentContext)];
                    case 2:
                        pathCompletionResult = _a.sent();
                        return [2 /*return*/, {
                                isIncomplete: result.isIncomplete || pathCompletionResult.isIncomplete,
                                items: pathCompletionResult.items.concat(result.items)
                            }];
                    case 3:
                        this.completionParticipants = contributedParticipants;
                        return [7 /*endfinally*/];
                    case 4: return [2 /*return*/];
                }
            });
        });
    };
    CSSCompletion.prototype.doComplete = function (document, position, styleSheet) {
        this.offset = document.offsetAt(position);
        this.position = position;
        this.currentWord = getCurrentWord(document, this.offset);
        this.defaultReplaceRange = Range.create(Position.create(this.position.line, this.position.character - this.currentWord.length), this.position);
        this.textDocument = document;
        this.styleSheet = styleSheet;
        try {
            var result = { isIncomplete: false, items: [] };
            this.nodePath = nodes.getNodePath(this.styleSheet, this.offset);
            for (var i = this.nodePath.length - 1; i >= 0; i--) {
                var node = this.nodePath[i];
                if (node instanceof nodes.Property) {
                    this.getCompletionsForDeclarationProperty(node.getParent(), result);
                }
                else if (node instanceof nodes.Expression) {
                    if (node.parent instanceof nodes.Interpolation) {
                        this.getVariableProposals(null, result);
                    }
                    else {
                        this.getCompletionsForExpression(node, result);
                    }
                }
                else if (node instanceof nodes.SimpleSelector) {
                    var parentRef = node.findAParent(nodes.NodeType.ExtendsReference, nodes.NodeType.Ruleset);
                    if (parentRef) {
                        if (parentRef.type === nodes.NodeType.ExtendsReference) {
                            this.getCompletionsForExtendsReference(parentRef, node, result);
                        }
                        else {
                            var parentRuleSet = parentRef;
                            this.getCompletionsForSelector(parentRuleSet, parentRuleSet && parentRuleSet.isNested(), result);
                        }
                    }
                }
                else if (node instanceof nodes.FunctionArgument) {
                    this.getCompletionsForFunctionArgument(node, node.getParent(), result);
                }
                else if (node instanceof nodes.Declarations) {
                    this.getCompletionsForDeclarations(node, result);
                }
                else if (node instanceof nodes.VariableDeclaration) {
                    this.getCompletionsForVariableDeclaration(node, result);
                }
                else if (node instanceof nodes.RuleSet) {
                    this.getCompletionsForRuleSet(node, result);
                }
                else if (node instanceof nodes.Interpolation) {
                    this.getCompletionsForInterpolation(node, result);
                }
                else if (node instanceof nodes.FunctionDeclaration) {
                    this.getCompletionsForFunctionDeclaration(node, result);
                }
                else if (node instanceof nodes.MixinReference) {
                    this.getCompletionsForMixinReference(node, result);
                }
                else if (node instanceof nodes.Function) {
                    this.getCompletionsForFunctionArgument(null, node, result);
                }
                else if (node instanceof nodes.Supports) {
                    this.getCompletionsForSupports(node, result);
                }
                else if (node instanceof nodes.SupportsCondition) {
                    this.getCompletionsForSupportsCondition(node, result);
                }
                else if (node instanceof nodes.ExtendsReference) {
                    this.getCompletionsForExtendsReference(node, null, result);
                }
                else if (node.type === nodes.NodeType.URILiteral) {
                    this.getCompletionForUriLiteralValue(node, result);
                }
                else if (node.parent === null) {
                    this.getCompletionForTopLevel(result);
                }
                else if (node.type === nodes.NodeType.StringLiteral && this.isImportPathParent(node.parent.type)) {
                    this.getCompletionForImportPath(node, result);
                    // } else if (node instanceof nodes.Variable) {
                    // this.getCompletionsForVariableDeclaration()
                }
                else {
                    continue;
                }
                if (result.items.length > 0 || this.offset > node.offset) {
                    return this.finalize(result);
                }
            }
            this.getCompletionsForStylesheet(result);
            if (result.items.length === 0) {
                if (this.variablePrefix && this.currentWord.indexOf(this.variablePrefix) === 0) {
                    this.getVariableProposals(null, result);
                }
            }
            return this.finalize(result);
        }
        finally {
            // don't hold on any state, clear symbolContext
            this.position = null;
            this.currentWord = null;
            this.textDocument = null;
            this.styleSheet = null;
            this.symbolContext = null;
            this.defaultReplaceRange = null;
            this.nodePath = null;
        }
    };
    CSSCompletion.prototype.isImportPathParent = function (type) {
        return type === nodes.NodeType.Import;
    };
    CSSCompletion.prototype.finalize = function (result) {
        return result;
    };
    CSSCompletion.prototype.findInNodePath = function () {
        var types = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            types[_i] = arguments[_i];
        }
        for (var i = this.nodePath.length - 1; i >= 0; i--) {
            var node = this.nodePath[i];
            if (types.indexOf(node.type) !== -1) {
                return node;
            }
        }
        return null;
    };
    CSSCompletion.prototype.getCompletionsForDeclarationProperty = function (declaration, result) {
        return this.getPropertyProposals(declaration, result);
    };
    CSSCompletion.prototype.getPropertyProposals = function (declaration, result) {
        var _this = this;
        var triggerPropertyValueCompletion = this.isTriggerPropertyValueCompletionEnabled;
        var completePropertyWithSemicolon = this.isCompletePropertyWithSemicolonEnabled;
        var properties = this.cssDataManager.getProperties();
        properties.forEach(function (entry) {
            var range;
            var insertText;
            var retrigger = false;
            if (declaration) {
                range = _this.getCompletionRange(declaration.getProperty());
                insertText = entry.name;
                if (!isDefined(declaration.colonPosition)) {
                    insertText += ': ';
                    retrigger = true;
                }
            }
            else {
                range = _this.getCompletionRange(null);
                insertText = entry.name + ': ';
                retrigger = true;
            }
            // Empty .selector { | } case
            if (!declaration && completePropertyWithSemicolon) {
                insertText += '$0;';
            }
            // Cases such as .selector { p; } or .selector { p:; }
            if (declaration && !declaration.semicolonPosition) {
                if (completePropertyWithSemicolon && _this.offset >= _this.textDocument.offsetAt(range.end)) {
                    insertText += '$0;';
                }
            }
            var item = {
                label: entry.name,
                documentation: languageFacts.getEntryDescription(entry, _this.doesSupportMarkdown()),
                tags: isDeprecated(entry) ? [CompletionItemTag.Deprecated] : [],
                textEdit: TextEdit.replace(range, insertText),
                insertTextFormat: InsertTextFormat.Snippet,
                kind: CompletionItemKind.Property
            };
            if (!entry.restrictions) {
                retrigger = false;
            }
            if (triggerPropertyValueCompletion && retrigger) {
                item.command = {
                    title: 'Suggest',
                    command: 'editor.action.triggerSuggest'
                };
            }
            var relevance = typeof entry.relevance === 'number' ? Math.min(Math.max(entry.relevance, 0), 99) : 50;
            var sortTextSuffix = (255 - relevance).toString(16);
            var sortTextPrefix = strings.startsWith(entry.name, '-') ? SortTexts.VendorPrefixed : SortTexts.Normal;
            item.sortText = sortTextPrefix + '_' + sortTextSuffix;
            result.items.push(item);
        });
        this.completionParticipants.forEach(function (participant) {
            if (participant.onCssProperty) {
                participant.onCssProperty({
                    propertyName: _this.currentWord,
                    range: _this.defaultReplaceRange
                });
            }
        });
        return result;
    };
    Object.defineProperty(CSSCompletion.prototype, "isTriggerPropertyValueCompletionEnabled", {
        get: function () {
            if (!this.settings ||
                !this.settings.completion ||
                this.settings.completion.triggerPropertyValueCompletion === undefined) {
                return true;
            }
            return this.settings.completion.triggerPropertyValueCompletion;
        },
        enumerable: false,
        configurable: true
    });
    Object.defineProperty(CSSCompletion.prototype, "isCompletePropertyWithSemicolonEnabled", {
        get: function () {
            if (!this.settings ||
                !this.settings.completion ||
                this.settings.completion.completePropertyWithSemicolon === undefined) {
                return true;
            }
            return this.settings.completion.completePropertyWithSemicolon;
        },
        enumerable: false,
        configurable: true
    });
    CSSCompletion.prototype.getCompletionsForDeclarationValue = function (node, result) {
        var _this = this;
        var propertyName = node.getFullPropertyName();
        var entry = this.cssDataManager.getProperty(propertyName);
        var existingNode = node.getValue() || null;
        while (existingNode && existingNode.hasChildren()) {
            existingNode = existingNode.findChildAtOffset(this.offset, false);
        }
        this.completionParticipants.forEach(function (participant) {
            if (participant.onCssPropertyValue) {
                participant.onCssPropertyValue({
                    propertyName: propertyName,
                    propertyValue: _this.currentWord,
                    range: _this.getCompletionRange(existingNode)
                });
            }
        });
        if (entry) {
            if (entry.restrictions) {
                for (var _i = 0, _a = entry.restrictions; _i < _a.length; _i++) {
                    var restriction = _a[_i];
                    switch (restriction) {
                        case 'color':
                            this.getColorProposals(entry, existingNode, result);
                            break;
                        case 'position':
                            this.getPositionProposals(entry, existingNode, result);
                            break;
                        case 'repeat':
                            this.getRepeatStyleProposals(entry, existingNode, result);
                            break;
                        case 'line-style':
                            this.getLineStyleProposals(entry, existingNode, result);
                            break;
                        case 'line-width':
                            this.getLineWidthProposals(entry, existingNode, result);
                            break;
                        case 'geometry-box':
                            this.getGeometryBoxProposals(entry, existingNode, result);
                            break;
                        case 'box':
                            this.getBoxProposals(entry, existingNode, result);
                            break;
                        case 'image':
                            this.getImageProposals(entry, existingNode, result);
                            break;
                        case 'timing-function':
                            this.getTimingFunctionProposals(entry, existingNode, result);
                            break;
                        case 'shape':
                            this.getBasicShapeProposals(entry, existingNode, result);
                            break;
                    }
                }
            }
            this.getValueEnumProposals(entry, existingNode, result);
            this.getCSSWideKeywordProposals(entry, existingNode, result);
            this.getUnitProposals(entry, existingNode, result);
        }
        else {
            var existingValues = collectValues(this.styleSheet, node);
            for (var _b = 0, _c = existingValues.getEntries(); _b < _c.length; _b++) {
                var existingValue = _c[_b];
                result.items.push({
                    label: existingValue,
                    textEdit: TextEdit.replace(this.getCompletionRange(existingNode), existingValue),
                    kind: CompletionItemKind.Value
                });
            }
        }
        this.getVariableProposals(existingNode, result);
        this.getTermProposals(entry, existingNode, result);
        return result;
    };
    CSSCompletion.prototype.getValueEnumProposals = function (entry, existingNode, result) {
        if (entry.values) {
            for (var _i = 0, _a = entry.values; _i < _a.length; _i++) {
                var value = _a[_i];
                var insertString = value.name;
                var insertTextFormat = void 0;
                if (strings.endsWith(insertString, ')')) {
                    var from = insertString.lastIndexOf('(');
                    if (from !== -1) {
                        insertString = insertString.substr(0, from) + '($1)';
                        insertTextFormat = SnippetFormat;
                    }
                }
                var sortText = SortTexts.Enums;
                if (strings.startsWith(value.name, '-')) {
                    sortText += SortTexts.VendorPrefixed;
                }
                var item = {
                    label: value.name,
                    documentation: languageFacts.getEntryDescription(value, this.doesSupportMarkdown()),
                    tags: isDeprecated(entry) ? [CompletionItemTag.Deprecated] : [],
                    textEdit: TextEdit.replace(this.getCompletionRange(existingNode), insertString),
                    sortText: sortText,
                    kind: CompletionItemKind.Value,
                    insertTextFormat: insertTextFormat
                };
                result.items.push(item);
            }
        }
        return result;
    };
    CSSCompletion.prototype.getCSSWideKeywordProposals = function (entry, existingNode, result) {
        for (var keywords in languageFacts.cssWideKeywords) {
            result.items.push({
                label: keywords,
                documentation: languageFacts.cssWideKeywords[keywords],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), keywords),
                kind: CompletionItemKind.Value
            });
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForInterpolation = function (node, result) {
        if (this.offset >= node.offset + 2) {
            this.getVariableProposals(null, result);
        }
        return result;
    };
    CSSCompletion.prototype.getVariableProposals = function (existingNode, result) {
        var symbols = this.getSymbolContext().findSymbolsAtOffset(this.offset, nodes.ReferenceType.Variable);
        for (var _i = 0, symbols_1 = symbols; _i < symbols_1.length; _i++) {
            var symbol = symbols_1[_i];
            var insertText = strings.startsWith(symbol.name, '--') ? "var(" + symbol.name + ")" : symbol.name;
            var completionItem = {
                label: symbol.name,
                documentation: symbol.value ? strings.getLimitedString(symbol.value) : symbol.value,
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), insertText),
                kind: CompletionItemKind.Variable,
                sortText: SortTexts.Variable
            };
            if (typeof completionItem.documentation === 'string' && isColorString(completionItem.documentation)) {
                completionItem.kind = CompletionItemKind.Color;
            }
            if (symbol.node.type === nodes.NodeType.FunctionParameter) {
                var mixinNode = (symbol.node.getParent());
                if (mixinNode.type === nodes.NodeType.MixinDeclaration) {
                    completionItem.detail = localize('completion.argument', 'argument from \'{0}\'', mixinNode.getName());
                }
            }
            result.items.push(completionItem);
        }
        return result;
    };
    CSSCompletion.prototype.getVariableProposalsForCSSVarFunction = function (result) {
        var symbols = this.getSymbolContext().findSymbolsAtOffset(this.offset, nodes.ReferenceType.Variable);
        symbols = symbols.filter(function (symbol) {
            return strings.startsWith(symbol.name, '--');
        });
        for (var _i = 0, symbols_2 = symbols; _i < symbols_2.length; _i++) {
            var symbol = symbols_2[_i];
            var completionItem = {
                label: symbol.name,
                documentation: symbol.value ? strings.getLimitedString(symbol.value) : symbol.value,
                textEdit: TextEdit.replace(this.getCompletionRange(null), symbol.name),
                kind: CompletionItemKind.Variable
            };
            if (typeof completionItem.documentation === 'string' && isColorString(completionItem.documentation)) {
                completionItem.kind = CompletionItemKind.Color;
            }
            result.items.push(completionItem);
        }
        return result;
    };
    CSSCompletion.prototype.getUnitProposals = function (entry, existingNode, result) {
        var currentWord = '0';
        if (this.currentWord.length > 0) {
            var numMatch = this.currentWord.match(/^-?\d[\.\d+]*/);
            if (numMatch) {
                currentWord = numMatch[0];
                result.isIncomplete = currentWord.length === this.currentWord.length;
            }
        }
        else if (this.currentWord.length === 0) {
            result.isIncomplete = true;
        }
        if (existingNode && existingNode.parent && existingNode.parent.type === nodes.NodeType.Term) {
            existingNode = existingNode.getParent(); // include the unary operator
        }
        if (entry.restrictions) {
            for (var _i = 0, _a = entry.restrictions; _i < _a.length; _i++) {
                var restriction = _a[_i];
                var units = languageFacts.units[restriction];
                if (units) {
                    for (var _b = 0, units_1 = units; _b < units_1.length; _b++) {
                        var unit = units_1[_b];
                        var insertText = currentWord + unit;
                        result.items.push({
                            label: insertText,
                            textEdit: TextEdit.replace(this.getCompletionRange(existingNode), insertText),
                            kind: CompletionItemKind.Unit
                        });
                    }
                }
            }
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionRange = function (existingNode) {
        if (existingNode && existingNode.offset <= this.offset && this.offset <= existingNode.end) {
            var end = existingNode.end !== -1 ? this.textDocument.positionAt(existingNode.end) : this.position;
            var start = this.textDocument.positionAt(existingNode.offset);
            if (start.line === end.line) {
                return Range.create(start, end); // multi line edits are not allowed
            }
        }
        return this.defaultReplaceRange;
    };
    CSSCompletion.prototype.getColorProposals = function (entry, existingNode, result) {
        for (var color in languageFacts.colors) {
            result.items.push({
                label: color,
                documentation: languageFacts.colors[color],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), color),
                kind: CompletionItemKind.Color
            });
        }
        for (var color in languageFacts.colorKeywords) {
            result.items.push({
                label: color,
                documentation: languageFacts.colorKeywords[color],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), color),
                kind: CompletionItemKind.Value
            });
        }
        var colorValues = new Set();
        this.styleSheet.acceptVisitor(new ColorValueCollector(colorValues, this.offset));
        for (var _i = 0, _a = colorValues.getEntries(); _i < _a.length; _i++) {
            var color = _a[_i];
            result.items.push({
                label: color,
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), color),
                kind: CompletionItemKind.Color
            });
        }
        var _loop_1 = function (p) {
            var tabStop = 1;
            var replaceFunction = function (_match, p1) { return '${' + tabStop++ + ':' + p1 + '}'; };
            var insertText = p.func.replace(/\[?\$(\w+)\]?/g, replaceFunction);
            result.items.push({
                label: p.func.substr(0, p.func.indexOf('(')),
                detail: p.func,
                documentation: p.desc,
                textEdit: TextEdit.replace(this_1.getCompletionRange(existingNode), insertText),
                insertTextFormat: SnippetFormat,
                kind: CompletionItemKind.Function
            });
        };
        var this_1 = this;
        for (var _b = 0, _c = languageFacts.colorFunctions; _b < _c.length; _b++) {
            var p = _c[_b];
            _loop_1(p);
        }
        return result;
    };
    CSSCompletion.prototype.getPositionProposals = function (entry, existingNode, result) {
        for (var position in languageFacts.positionKeywords) {
            result.items.push({
                label: position,
                documentation: languageFacts.positionKeywords[position],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), position),
                kind: CompletionItemKind.Value
            });
        }
        return result;
    };
    CSSCompletion.prototype.getRepeatStyleProposals = function (entry, existingNode, result) {
        for (var repeat in languageFacts.repeatStyleKeywords) {
            result.items.push({
                label: repeat,
                documentation: languageFacts.repeatStyleKeywords[repeat],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), repeat),
                kind: CompletionItemKind.Value
            });
        }
        return result;
    };
    CSSCompletion.prototype.getLineStyleProposals = function (entry, existingNode, result) {
        for (var lineStyle in languageFacts.lineStyleKeywords) {
            result.items.push({
                label: lineStyle,
                documentation: languageFacts.lineStyleKeywords[lineStyle],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), lineStyle),
                kind: CompletionItemKind.Value
            });
        }
        return result;
    };
    CSSCompletion.prototype.getLineWidthProposals = function (entry, existingNode, result) {
        for (var _i = 0, _a = languageFacts.lineWidthKeywords; _i < _a.length; _i++) {
            var lineWidth = _a[_i];
            result.items.push({
                label: lineWidth,
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), lineWidth),
                kind: CompletionItemKind.Value
            });
        }
        return result;
    };
    CSSCompletion.prototype.getGeometryBoxProposals = function (entry, existingNode, result) {
        for (var box in languageFacts.geometryBoxKeywords) {
            result.items.push({
                label: box,
                documentation: languageFacts.geometryBoxKeywords[box],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), box),
                kind: CompletionItemKind.Value
            });
        }
        return result;
    };
    CSSCompletion.prototype.getBoxProposals = function (entry, existingNode, result) {
        for (var box in languageFacts.boxKeywords) {
            result.items.push({
                label: box,
                documentation: languageFacts.boxKeywords[box],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), box),
                kind: CompletionItemKind.Value
            });
        }
        return result;
    };
    CSSCompletion.prototype.getImageProposals = function (entry, existingNode, result) {
        for (var image in languageFacts.imageFunctions) {
            var insertText = moveCursorInsideParenthesis(image);
            result.items.push({
                label: image,
                documentation: languageFacts.imageFunctions[image],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), insertText),
                kind: CompletionItemKind.Function,
                insertTextFormat: image !== insertText ? SnippetFormat : void 0
            });
        }
        return result;
    };
    CSSCompletion.prototype.getTimingFunctionProposals = function (entry, existingNode, result) {
        for (var timing in languageFacts.transitionTimingFunctions) {
            var insertText = moveCursorInsideParenthesis(timing);
            result.items.push({
                label: timing,
                documentation: languageFacts.transitionTimingFunctions[timing],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), insertText),
                kind: CompletionItemKind.Function,
                insertTextFormat: timing !== insertText ? SnippetFormat : void 0
            });
        }
        return result;
    };
    CSSCompletion.prototype.getBasicShapeProposals = function (entry, existingNode, result) {
        for (var shape in languageFacts.basicShapeFunctions) {
            var insertText = moveCursorInsideParenthesis(shape);
            result.items.push({
                label: shape,
                documentation: languageFacts.basicShapeFunctions[shape],
                textEdit: TextEdit.replace(this.getCompletionRange(existingNode), insertText),
                kind: CompletionItemKind.Function,
                insertTextFormat: shape !== insertText ? SnippetFormat : void 0
            });
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForStylesheet = function (result) {
        var node = this.styleSheet.findFirstChildBeforeOffset(this.offset);
        if (!node) {
            return this.getCompletionForTopLevel(result);
        }
        if (node instanceof nodes.RuleSet) {
            return this.getCompletionsForRuleSet(node, result);
        }
        if (node instanceof nodes.Supports) {
            return this.getCompletionsForSupports(node, result);
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionForTopLevel = function (result) {
        var _this = this;
        this.cssDataManager.getAtDirectives().forEach(function (entry) {
            result.items.push({
                label: entry.name,
                textEdit: TextEdit.replace(_this.getCompletionRange(null), entry.name),
                documentation: languageFacts.getEntryDescription(entry, _this.doesSupportMarkdown()),
                tags: isDeprecated(entry) ? [CompletionItemTag.Deprecated] : [],
                kind: CompletionItemKind.Keyword
            });
        });
        this.getCompletionsForSelector(null, false, result);
        return result;
    };
    CSSCompletion.prototype.getCompletionsForRuleSet = function (ruleSet, result) {
        var declarations = ruleSet.getDeclarations();
        var isAfter = declarations && declarations.endsWith('}') && this.offset >= declarations.end;
        if (isAfter) {
            return this.getCompletionForTopLevel(result);
        }
        var isInSelectors = !declarations || this.offset <= declarations.offset;
        if (isInSelectors) {
            return this.getCompletionsForSelector(ruleSet, ruleSet.isNested(), result);
        }
        return this.getCompletionsForDeclarations(ruleSet.getDeclarations(), result);
    };
    CSSCompletion.prototype.getCompletionsForSelector = function (ruleSet, isNested, result) {
        var _this = this;
        var existingNode = this.findInNodePath(nodes.NodeType.PseudoSelector, nodes.NodeType.IdentifierSelector, nodes.NodeType.ClassSelector, nodes.NodeType.ElementNameSelector);
        if (!existingNode && this.hasCharacterAtPosition(this.offset - this.currentWord.length - 1, ':')) {
            // after the ':' of a pseudo selector, no node generated for just ':'
            this.currentWord = ':' + this.currentWord;
            if (this.hasCharacterAtPosition(this.offset - this.currentWord.length - 1, ':')) {
                this.currentWord = ':' + this.currentWord; // for '::'
            }
            this.defaultReplaceRange = Range.create(Position.create(this.position.line, this.position.character - this.currentWord.length), this.position);
        }
        var pseudoClasses = this.cssDataManager.getPseudoClasses();
        pseudoClasses.forEach(function (entry) {
            var insertText = moveCursorInsideParenthesis(entry.name);
            var item = {
                label: entry.name,
                textEdit: TextEdit.replace(_this.getCompletionRange(existingNode), insertText),
                documentation: languageFacts.getEntryDescription(entry, _this.doesSupportMarkdown()),
                tags: isDeprecated(entry) ? [CompletionItemTag.Deprecated] : [],
                kind: CompletionItemKind.Function,
                insertTextFormat: entry.name !== insertText ? SnippetFormat : void 0
            };
            if (strings.startsWith(entry.name, ':-')) {
                item.sortText = SortTexts.VendorPrefixed;
            }
            result.items.push(item);
        });
        var pseudoElements = this.cssDataManager.getPseudoElements();
        pseudoElements.forEach(function (entry) {
            var insertText = moveCursorInsideParenthesis(entry.name);
            var item = {
                label: entry.name,
                textEdit: TextEdit.replace(_this.getCompletionRange(existingNode), insertText),
                documentation: languageFacts.getEntryDescription(entry, _this.doesSupportMarkdown()),
                tags: isDeprecated(entry) ? [CompletionItemTag.Deprecated] : [],
                kind: CompletionItemKind.Function,
                insertTextFormat: entry.name !== insertText ? SnippetFormat : void 0
            };
            if (strings.startsWith(entry.name, '::-')) {
                item.sortText = SortTexts.VendorPrefixed;
            }
            result.items.push(item);
        });
        if (!isNested) { // show html tags only for top level
            for (var _i = 0, _a = languageFacts.html5Tags; _i < _a.length; _i++) {
                var entry = _a[_i];
                result.items.push({
                    label: entry,
                    textEdit: TextEdit.replace(this.getCompletionRange(existingNode), entry),
                    kind: CompletionItemKind.Keyword
                });
            }
            for (var _b = 0, _c = languageFacts.svgElements; _b < _c.length; _b++) {
                var entry = _c[_b];
                result.items.push({
                    label: entry,
                    textEdit: TextEdit.replace(this.getCompletionRange(existingNode), entry),
                    kind: CompletionItemKind.Keyword
                });
            }
        }
        var visited = {};
        visited[this.currentWord] = true;
        var docText = this.textDocument.getText();
        this.styleSheet.accept(function (n) {
            if (n.type === nodes.NodeType.SimpleSelector && n.length > 0) {
                var selector = docText.substr(n.offset, n.length);
                if (selector.charAt(0) === '.' && !visited[selector]) {
                    visited[selector] = true;
                    result.items.push({
                        label: selector,
                        textEdit: TextEdit.replace(_this.getCompletionRange(existingNode), selector),
                        kind: CompletionItemKind.Keyword
                    });
                }
                return false;
            }
            return true;
        });
        if (ruleSet && ruleSet.isNested()) {
            var selector = ruleSet.getSelectors().findFirstChildBeforeOffset(this.offset);
            if (selector && ruleSet.getSelectors().getChildren().indexOf(selector) === 0) {
                this.getPropertyProposals(null, result);
            }
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForDeclarations = function (declarations, result) {
        if (!declarations || this.offset === declarations.offset) { // incomplete nodes
            return result;
        }
        var node = declarations.findFirstChildBeforeOffset(this.offset);
        if (!node) {
            return this.getCompletionsForDeclarationProperty(null, result);
        }
        if (node instanceof nodes.AbstractDeclaration) {
            var declaration = node;
            if (!isDefined(declaration.colonPosition) || this.offset <= declaration.colonPosition) {
                // complete property
                return this.getCompletionsForDeclarationProperty(declaration, result);
            }
            else if ((isDefined(declaration.semicolonPosition) && declaration.semicolonPosition < this.offset)) {
                if (this.offset === declaration.semicolonPosition + 1) {
                    return result; // don't show new properties right after semicolon (see Bug 15421:[intellisense] [css] Be less aggressive when manually typing CSS)
                }
                // complete next property
                return this.getCompletionsForDeclarationProperty(null, result);
            }
            if (declaration instanceof nodes.Declaration) {
                // complete value
                return this.getCompletionsForDeclarationValue(declaration, result);
            }
        }
        else if (node instanceof nodes.ExtendsReference) {
            this.getCompletionsForExtendsReference(node, null, result);
        }
        else if (this.currentWord && this.currentWord[0] === '@') {
            this.getCompletionsForDeclarationProperty(null, result);
        }
        else if (node instanceof nodes.RuleSet) {
            this.getCompletionsForDeclarationProperty(null, result);
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForVariableDeclaration = function (declaration, result) {
        if (this.offset && isDefined(declaration.colonPosition) && this.offset > declaration.colonPosition) {
            this.getVariableProposals(declaration.getValue(), result);
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForExpression = function (expression, result) {
        var parent = expression.getParent();
        if (parent instanceof nodes.FunctionArgument) {
            this.getCompletionsForFunctionArgument(parent, parent.getParent(), result);
            return result;
        }
        var declaration = expression.findParent(nodes.NodeType.Declaration);
        if (!declaration) {
            this.getTermProposals(undefined, null, result);
            return result;
        }
        var node = expression.findChildAtOffset(this.offset, true);
        if (!node) {
            return this.getCompletionsForDeclarationValue(declaration, result);
        }
        if (node instanceof nodes.NumericValue || node instanceof nodes.Identifier) {
            return this.getCompletionsForDeclarationValue(declaration, result);
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForFunctionArgument = function (arg, func, result) {
        var identifier = func.getIdentifier();
        if (identifier && identifier.matches('var')) {
            if (!func.getArguments().hasChildren() || func.getArguments().getChild(0) === arg) {
                this.getVariableProposalsForCSSVarFunction(result);
            }
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForFunctionDeclaration = function (decl, result) {
        var declarations = decl.getDeclarations();
        if (declarations && this.offset > declarations.offset && this.offset < declarations.end) {
            this.getTermProposals(undefined, null, result);
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForMixinReference = function (ref, result) {
        var _this = this;
        var allMixins = this.getSymbolContext().findSymbolsAtOffset(this.offset, nodes.ReferenceType.Mixin);
        for (var _i = 0, allMixins_1 = allMixins; _i < allMixins_1.length; _i++) {
            var mixinSymbol = allMixins_1[_i];
            if (mixinSymbol.node instanceof nodes.MixinDeclaration) {
                result.items.push(this.makeTermProposal(mixinSymbol, mixinSymbol.node.getParameters(), null));
            }
        }
        var identifierNode = ref.getIdentifier() || null;
        this.completionParticipants.forEach(function (participant) {
            if (participant.onCssMixinReference) {
                participant.onCssMixinReference({
                    mixinName: _this.currentWord,
                    range: _this.getCompletionRange(identifierNode)
                });
            }
        });
        return result;
    };
    CSSCompletion.prototype.getTermProposals = function (entry, existingNode, result) {
        var allFunctions = this.getSymbolContext().findSymbolsAtOffset(this.offset, nodes.ReferenceType.Function);
        for (var _i = 0, allFunctions_1 = allFunctions; _i < allFunctions_1.length; _i++) {
            var functionSymbol = allFunctions_1[_i];
            if (functionSymbol.node instanceof nodes.FunctionDeclaration) {
                result.items.push(this.makeTermProposal(functionSymbol, functionSymbol.node.getParameters(), existingNode));
            }
        }
        return result;
    };
    CSSCompletion.prototype.makeTermProposal = function (symbol, parameters, existingNode) {
        var decl = symbol.node;
        var params = parameters.getChildren().map(function (c) {
            return (c instanceof nodes.FunctionParameter) ? c.getName() : c.getText();
        });
        var insertText = symbol.name + '(' + params.map(function (p, index) { return '${' + (index + 1) + ':' + p + '}'; }).join(', ') + ')';
        return {
            label: symbol.name,
            detail: symbol.name + '(' + params.join(', ') + ')',
            textEdit: TextEdit.replace(this.getCompletionRange(existingNode), insertText),
            insertTextFormat: SnippetFormat,
            kind: CompletionItemKind.Function,
            sortText: SortTexts.Term
        };
    };
    CSSCompletion.prototype.getCompletionsForSupportsCondition = function (supportsCondition, result) {
        var child = supportsCondition.findFirstChildBeforeOffset(this.offset);
        if (child) {
            if (child instanceof nodes.Declaration) {
                if (!isDefined(child.colonPosition) || this.offset <= child.colonPosition) {
                    return this.getCompletionsForDeclarationProperty(child, result);
                }
                else {
                    return this.getCompletionsForDeclarationValue(child, result);
                }
            }
            else if (child instanceof nodes.SupportsCondition) {
                return this.getCompletionsForSupportsCondition(child, result);
            }
        }
        if (isDefined(supportsCondition.lParent) && this.offset > supportsCondition.lParent && (!isDefined(supportsCondition.rParent) || this.offset <= supportsCondition.rParent)) {
            return this.getCompletionsForDeclarationProperty(null, result);
        }
        return result;
    };
    CSSCompletion.prototype.getCompletionsForSupports = function (supports, result) {
        var declarations = supports.getDeclarations();
        var inInCondition = !declarations || this.offset <= declarations.offset;
        if (inInCondition) {
            var child = supports.findFirstChildBeforeOffset(this.offset);
            if (child instanceof nodes.SupportsCondition) {
                return this.getCompletionsForSupportsCondition(child, result);
            }
            return result;
        }
        return this.getCompletionForTopLevel(result);
    };
    CSSCompletion.prototype.getCompletionsForExtendsReference = function (extendsRef, existingNode, result) {
        return result;
    };
    CSSCompletion.prototype.getCompletionForUriLiteralValue = function (uriLiteralNode, result) {
        var uriValue;
        var position;
        var range;
        // No children, empty value
        if (!uriLiteralNode.hasChildren()) {
            uriValue = '';
            position = this.position;
            var emptyURIValuePosition = this.textDocument.positionAt(uriLiteralNode.offset + 'url('.length);
            range = Range.create(emptyURIValuePosition, emptyURIValuePosition);
        }
        else {
            var uriValueNode = uriLiteralNode.getChild(0);
            uriValue = uriValueNode.getText();
            position = this.position;
            range = this.getCompletionRange(uriValueNode);
        }
        this.completionParticipants.forEach(function (participant) {
            if (participant.onCssURILiteralValue) {
                participant.onCssURILiteralValue({
                    uriValue: uriValue,
                    position: position,
                    range: range
                });
            }
        });
        return result;
    };
    CSSCompletion.prototype.getCompletionForImportPath = function (importPathNode, result) {
        var _this = this;
        this.completionParticipants.forEach(function (participant) {
            if (participant.onCssImportPath) {
                participant.onCssImportPath({
                    pathValue: importPathNode.getText(),
                    position: _this.position,
                    range: _this.getCompletionRange(importPathNode)
                });
            }
        });
        return result;
    };
    CSSCompletion.prototype.hasCharacterAtPosition = function (offset, char) {
        var text = this.textDocument.getText();
        return (offset >= 0 && offset < text.length) && text.charAt(offset) === char;
    };
    CSSCompletion.prototype.doesSupportMarkdown = function () {
        var _a, _b, _c;
        if (!isDefined(this.supportsMarkdown)) {
            if (!isDefined(this.lsOptions.clientCapabilities)) {
                this.supportsMarkdown = true;
                return this.supportsMarkdown;
            }
            var documentationFormat = (_c = (_b = (_a = this.lsOptions.clientCapabilities.textDocument) === null || _a === void 0 ? void 0 : _a.completion) === null || _b === void 0 ? void 0 : _b.completionItem) === null || _c === void 0 ? void 0 : _c.documentationFormat;
            this.supportsMarkdown = Array.isArray(documentationFormat) && documentationFormat.indexOf(MarkupKind.Markdown) !== -1;
        }
        return this.supportsMarkdown;
    };
    return CSSCompletion;
}());
export { CSSCompletion };
function isDeprecated(entry) {
    if (entry.status && (entry.status === 'nonstandard' || entry.status === 'obsolete')) {
        return true;
    }
    return false;
}
/**
 * Rank number should all be same length strings
 */
function computeRankNumber(n) {
    var nstr = n.toString();
    switch (nstr.length) {
        case 4:
            return nstr;
        case 3:
            return '0' + nstr;
        case 2:
            return '00' + nstr;
        case 1:
            return '000' + nstr;
        default:
            return '0000';
    }
}
var Set = /** @class */ (function () {
    function Set() {
        this.entries = {};
    }
    Set.prototype.add = function (entry) {
        this.entries[entry] = true;
    };
    Set.prototype.getEntries = function () {
        return Object.keys(this.entries);
    };
    return Set;
}());
function moveCursorInsideParenthesis(text) {
    return text.replace(/\(\)$/, "($1)");
}
function collectValues(styleSheet, declaration) {
    var fullPropertyName = declaration.getFullPropertyName();
    var entries = new Set();
    function visitValue(node) {
        if (node instanceof nodes.Identifier || node instanceof nodes.NumericValue || node instanceof nodes.HexColorValue) {
            entries.add(node.getText());
        }
        return true;
    }
    function matchesProperty(decl) {
        var propertyName = decl.getFullPropertyName();
        return fullPropertyName === propertyName;
    }
    function vistNode(node) {
        if (node instanceof nodes.Declaration && node !== declaration) {
            if (matchesProperty(node)) {
                var value = node.getValue();
                if (value) {
                    value.accept(visitValue);
                }
            }
        }
        return true;
    }
    styleSheet.accept(vistNode);
    return entries;
}
var ColorValueCollector = /** @class */ (function () {
    function ColorValueCollector(entries, currentOffset) {
        this.entries = entries;
        this.currentOffset = currentOffset;
        // nothing to do
    }
    ColorValueCollector.prototype.visitNode = function (node) {
        if (node instanceof nodes.HexColorValue || (node instanceof nodes.Function && languageFacts.isColorConstructor(node))) {
            if (this.currentOffset < node.offset || node.end < this.currentOffset) {
                this.entries.add(node.getText());
            }
        }
        return true;
    };
    return ColorValueCollector;
}());
function getCurrentWord(document, offset) {
    var i = offset - 1;
    var text = document.getText();
    while (i >= 0 && ' \t\n\r":{[()]},*>+'.indexOf(text.charAt(i)) === -1) {
        i--;
    }
    return text.substring(i + 1, offset);
}
function isColorString(s) {
    // From https://stackoverflow.com/questions/8027423/how-to-check-if-a-string-is-a-valid-hex-color-representation/8027444
    return (s.toLowerCase() in languageFacts.colors) || /(^#[0-9A-F]{6}$)|(^#[0-9A-F]{3}$)/i.test(s);
}
